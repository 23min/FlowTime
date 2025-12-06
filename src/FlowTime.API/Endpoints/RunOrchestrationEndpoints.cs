using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using FlowTime.API.Services;
using FlowTime.Contracts.TimeTravel;
using FlowTime.Generator.Artifacts;
using FlowTime.Generator.Orchestration;

namespace FlowTime.API.Endpoints;

internal static class RunOrchestrationEndpoints
{
    public static RouteGroupBuilder MapRunOrchestrationEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/runs", HandleCreateRunAsync);
        group.MapGet("/runs", HandleListRunsAsync);
        group.MapGet("/runs/{runId}", HandleGetRunAsync);
        return group;
    }

    private static async Task<IResult> HandleCreateRunAsync(
        RunImportRequest request,
        RunOrchestrationService orchestration,
        IConfiguration configuration,
        MetricsService metricsService,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Results.BadRequest(new { error = "Request body is required." });
        }

        var hasBundlePath = !string.IsNullOrWhiteSpace(request.BundlePath);
        var hasArchive = !string.IsNullOrWhiteSpace(request.BundleArchiveBase64);
        if (!hasBundlePath && !hasArchive)
        {
            return Results.Json(
                new { error = "Template-driven run creation moved to FlowTime.Sim Service (/api/v1/orchestration/runs). Import canonical bundles via bundlePath or bundleArchiveBase64." },
                statusCode: StatusCodes.Status410Gone);
        }

        string? extractionRoot = null;
        try
        {
            var sourceDirectory = hasArchive
                ? await ExtractArchiveAsync(request.BundleArchiveBase64!, cancellationToken).ConfigureAwait(false)
                : Path.GetFullPath(request.BundlePath!);

            extractionRoot = hasArchive ? sourceDirectory : null;

            if (!Directory.Exists(sourceDirectory))
            {
                return Results.BadRequest(new { error = $"Bundle directory '{sourceDirectory}' was not found." });
            }

            var bundleRoot = FindBundleRoot(sourceDirectory);
            var bundleRunId = await TryReadRunIdAsync(bundleRoot, cancellationToken).ConfigureAwait(false);
            var runId = !string.IsNullOrWhiteSpace(bundleRunId)
                ? bundleRunId
                : (!string.IsNullOrWhiteSpace(request.RunId) ? request.RunId : Path.GetFileName(bundleRoot));

            if (string.IsNullOrWhiteSpace(runId))
            {
                return Results.BadRequest(new { error = "Run id could not be determined from the bundle." });
            }

            var runsRoot = Program.ServiceHelpers.RunsRoot(configuration);
            var destination = Path.Combine(runsRoot, runId!);

            if (Directory.Exists(destination))
            {
                if (!request.OverwriteExisting)
                {
                    return Results.Conflict(new { error = $"Run '{runId}' already exists. Set overwriteExisting=true to replace it." });
                }

                Directory.Delete(destination, recursive: true);
            }

            CopyDirectory(bundleRoot, destination);

            var result = await orchestration.TryLoadRunAsync(destination, cancellationToken).ConfigureAwait(false);
            if (result is null)
            {
                Directory.Delete(destination, recursive: true);
                return Results.BadRequest(new { error = "Bundle was invalid or missing canonical artifacts." });
            }

            await MetricsArtifactWriter.TryWriteAsync(metricsService, result.RunId, destination, logger, cancellationToken);

            var metadata = RunOrchestrationContractMapper.BuildStateMetadata(result);
            var warnings = RunOrchestrationContractMapper.BuildStateWarnings(result.TelemetryManifest);
            var canReplay = RunOrchestrationContractMapper.DetermineCanReplay(result);
            var telemetry = RunOrchestrationContractMapper.BuildTelemetrySummary(result);

            return Results.Created($"/v1/runs/{metadata.RunId}", new RunCreateResponse
            {
                IsDryRun = false,
                Metadata = metadata,
                Warnings = warnings,
                CanReplay = canReplay,
                Telemetry = telemetry,
                WasReused = false
            });
        }
        catch (FormatException ex)
        {
            logger.LogWarning(ex, "Bundle archive payload was not valid base64.");
            return Results.BadRequest(new { error = "bundleArchiveBase64 must be a valid base64 string." });
        }
        catch (InvalidDataException ex)
        {
            logger.LogWarning(ex, "Failed to extract bundle archive.");
            return Results.BadRequest(new { error = $"Bundle archive could not be extracted: {ex.Message}" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to import canonical run bundle.");
            return Results.Problem(title: "Run import failed", detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(extractionRoot) && Directory.Exists(extractionRoot))
            {
                TryDeleteDirectory(extractionRoot);
            }
        }
    }

    private static async Task<IResult> HandleListRunsAsync(
        HttpContext context,
        RunOrchestrationService orchestration,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var runsRoot = Program.ServiceHelpers.RunsRoot(configuration);
        if (!Directory.Exists(runsRoot))
        {
            return Results.Ok(new RunSummaryResponse());
        }

        var query = context.Request.Query;
        var modeFilter = query.TryGetValue("mode", out var modeValues) ? modeValues.ToString() : null;
        var templateFilter = query.TryGetValue("templateId", out var templateValues) ? templateValues.ToString() : null;

        bool? hasWarningsFilter = null;
        if (query.TryGetValue("hasWarnings", out var warningsValues) && warningsValues.Count > 0)
        {
            if (bool.TryParse(warningsValues.ToString(), out var parsedWarnings))
            {
                hasWarningsFilter = parsedWarnings;
            }
            else
            {
                return Results.BadRequest(new { error = "hasWarnings must be 'true' or 'false'." });
            }
        }

        const int defaultPage = 1;
        const int defaultPageSize = 50;
        const int maxPageSize = 200;

        var page = defaultPage;
        if (query.TryGetValue("page", out var pageValues) && pageValues.Count > 0)
        {
            if (!int.TryParse(pageValues.ToString(), out page) || page < 1)
            {
                return Results.BadRequest(new { error = "page must be an integer greater than 0." });
            }
        }

        var pageSize = defaultPageSize;
        if (query.TryGetValue("pageSize", out var pageSizeValues) && pageSizeValues.Count > 0)
        {
            if (!int.TryParse(pageSizeValues.ToString(), out pageSize) || pageSize < 1)
            {
                return Results.BadRequest(new { error = "pageSize must be an integer greater than 0." });
            }

            pageSize = Math.Min(pageSize, maxPageSize);
        }

        var items = new List<RunSummary>();
        foreach (var directory in Directory.EnumerateDirectories(runsRoot))
        {
            try
            {
                var result = await orchestration.TryLoadRunAsync(directory, cancellationToken).ConfigureAwait(false);
                if (result is null)
                {
                    continue;
                }

                items.Add(RunOrchestrationContractMapper.BuildRunSummary(result));
            }
            catch
            {
                // ignore invalid runs to keep listing resilient
            }
        }

        items.Sort((a, b) => Nullable.Compare(b.CreatedUtc, a.CreatedUtc));

        IEnumerable<RunSummary> filtered = items;

        if (!string.IsNullOrWhiteSpace(modeFilter))
        {
            filtered = filtered.Where(item => string.Equals(item.Mode, modeFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(templateFilter))
        {
            filtered = filtered.Where(item => string.Equals(item.TemplateId, templateFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (hasWarningsFilter.HasValue)
        {
            filtered = filtered.Where(item => (item.WarningCount > 0) == hasWarningsFilter.Value);
        }

        var filteredList = filtered.ToList();
        var totalCount = filteredList.Count;

        var skip = (page - 1) * pageSize;
        if (skip >= totalCount)
        {
            filteredList = new List<RunSummary>();
        }
        else if (skip > 0)
        {
            filteredList = filteredList.Skip(skip).Take(pageSize).ToList();
        }
        else
        {
            filteredList = filteredList.Take(pageSize).ToList();
        }

        return Results.Ok(new RunSummaryResponse
        {
            Items = filteredList,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    private static async Task<IResult> HandleGetRunAsync(
        string runId,
        RunOrchestrationService orchestration,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(runId))
        {
            return Results.BadRequest(new { error = "runId is required." });
        }

        var runsRoot = Program.ServiceHelpers.RunsRoot(configuration);
        var runDirectory = Path.Combine(runsRoot, runId);

        var result = await orchestration.TryLoadRunAsync(runDirectory, cancellationToken).ConfigureAwait(false);
        if (result is null)
        {
            return Results.NotFound(new { error = $"Run '{runId}' not found." });
        }

        var metadata = RunOrchestrationContractMapper.BuildStateMetadata(result);
        var warnings = RunOrchestrationContractMapper.BuildStateWarnings(result.TelemetryManifest);
        var canReplay = RunOrchestrationContractMapper.DetermineCanReplay(result);
        return Results.Ok(new RunCreateResponse
        {
            IsDryRun = false,
            Metadata = metadata,
            Warnings = warnings,
            CanReplay = canReplay,
            Telemetry = RunOrchestrationContractMapper.BuildTelemetrySummary(result)
        });
    }

    private static async Task<string?> TryReadRunIdAsync(string bundleRoot, CancellationToken cancellationToken)
    {
        var runPath = Path.Combine(bundleRoot, "run.json");
        if (!File.Exists(runPath))
        {
            return null;
        }

        await using var stream = File.OpenRead(runPath);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        return document.RootElement.TryGetProperty("runId", out var runIdProp)
            ? runIdProp.GetString()
            : null;
    }

    private static async Task<string> ExtractArchiveAsync(string base64, CancellationToken cancellationToken)
    {
        var bytes = Convert.FromBase64String(base64);
        var extractionRoot = Path.Combine(Path.GetTempPath(), $"flowtime_import_{Guid.NewGuid():N}");
        Directory.CreateDirectory(extractionRoot);

        await using var stream = new MemoryStream(bytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        archive.ExtractToDirectory(extractionRoot, overwriteFiles: true);

        return extractionRoot;
    }

    private static string FindBundleRoot(string path)
    {
        if (File.Exists(Path.Combine(path, "run.json")))
        {
            return path;
        }

        foreach (var directory in Directory.EnumerateDirectories(path))
        {
            if (File.Exists(Path.Combine(directory, "run.json")))
            {
                return directory;
            }
        }

        return path;
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);
        foreach (var file in Directory.EnumerateFiles(sourceDir))
        {
            var fileName = Path.GetFileName(file);
            var target = Path.Combine(destinationDir, fileName);
            File.Copy(file, target, overwrite: true);
        }

        foreach (var directory in Directory.EnumerateDirectories(sourceDir))
        {
            var name = Path.GetFileName(directory);
            CopyDirectory(directory, Path.Combine(destinationDir, name));
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // best effort cleanup
        }
    }
}
