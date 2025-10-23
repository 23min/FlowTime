using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FlowTime.Contracts.TimeTravel;
using FlowTime.Generator.Artifacts;
using FlowTime.Generator.Orchestration;
using FlowTime.Sim.Core.Templates.Exceptions;

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
        RunCreateRequest request,
        RunOrchestrationService orchestration,
        IConfiguration configuration,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Results.BadRequest(new { error = "Request body is required." });
        }

        if (string.IsNullOrWhiteSpace(request.TemplateId))
        {
            return Results.BadRequest(new { error = "templateId is required." });
        }

        var mode = string.IsNullOrWhiteSpace(request.Mode) ? "telemetry" : request.Mode.Trim().ToLowerInvariant();
        if (mode is not ("telemetry" or "simulation"))
        {
            return Results.BadRequest(new { error = "mode must be 'telemetry' or 'simulation'." });
        }

        if (mode == "telemetry" && (request.Telemetry?.CaptureDirectory is null || string.IsNullOrWhiteSpace(request.Telemetry.CaptureDirectory)))
        {
            return Results.BadRequest(new { error = "telemetry.captureDirectory is required for telemetry runs." });
        }

        logger.LogInformation("Received {Mode} run creation request for template {TemplateId}", mode, request.TemplateId);

        var runsRoot = Program.ServiceHelpers.RunsRoot(configuration);
        var parameters = ConvertParameters(request.Parameters);
        var telemetryBindings = request.Telemetry?.Bindings is not null
            ? new Dictionary<string, string>(request.Telemetry.Bindings, StringComparer.OrdinalIgnoreCase)
            : null;

        var orchestrationRequest = new RunOrchestrationRequest
        {
            TemplateId = request.TemplateId,
            Mode = mode,
            CaptureDirectory = request.Telemetry?.CaptureDirectory,
            TelemetryBindings = telemetryBindings,
            Parameters = parameters,
            OutputRoot = runsRoot,
            DeterministicRunId = request.Options?.DeterministicRunId ?? false,
            RunId = request.Options?.RunId,
            DryRun = request.Options?.DryRun ?? false,
            OverwriteExisting = request.Options?.OverwriteExisting ?? false
        };

        try
        {
            var outcome = await orchestration.CreateRunAsync(orchestrationRequest, cancellationToken).ConfigureAwait(false);

            if (outcome.IsDryRun)
            {
                var plan = outcome.Plan ?? throw new InvalidOperationException("Dry-run outcome missing plan details.");
                return Results.Ok(new RunCreateResponse
                {
                    IsDryRun = true,
                    Plan = BuildPlan(plan),
                    Warnings = Array.Empty<StateWarning>(),
                    CanReplay = false,
                    Telemetry = null
                });
            }

            var result = outcome.Result ?? throw new InvalidOperationException("Run outcome missing result payload.");
            var metadata = BuildStateMetadata(result);
            var warnings = BuildStateWarnings(result.TelemetryManifest);
            var canReplay = DetermineCanReplay(result);

            return Results.Created($"/v1/runs/{metadata.RunId}", new RunCreateResponse
            {
                IsDryRun = false,
                Metadata = metadata,
                Warnings = warnings,
                CanReplay = canReplay,
                Telemetry = BuildTelemetrySummary(result)
            });
        }
        catch (TemplateValidationException ex)
        {
            logger.LogWarning(ex, "Template validation failed for template {TemplateId}", request.TemplateId);
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (ArgumentException ex) when (ex.Message.Contains("Template not found", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(ex, "Template {TemplateId} was not found", request.TemplateId);
            return Results.NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Run validation failed for template {TemplateId}", request.TemplateId);
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (FileNotFoundException ex)
        {
            logger.LogWarning(ex, "Missing capture artifact for template {TemplateId}", request.TemplateId);
            return Results.Problem(
                title: "Capture artifacts missing",
                detail: ex.Message,
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create run for template {TemplateId}", request.TemplateId);
            return Results.Problem(title: "Run creation failed", detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
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

                items.Add(BuildRunSummary(result));
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

        var metadata = BuildStateMetadata(result);
        var warnings = BuildStateWarnings(result.TelemetryManifest);
        var canReplay = DetermineCanReplay(result);
        return Results.Ok(new RunCreateResponse { IsDryRun = false, Metadata = metadata, Warnings = warnings, CanReplay = canReplay, Telemetry = BuildTelemetrySummary(result) });
    }

    private static Dictionary<string, object?> ConvertParameters(Dictionary<string, JsonElement>? source)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (source is null)
        {
            return result;
        }

        foreach (var kvp in source)
        {
            result[kvp.Key] = ConvertJsonElement(kvp.Value);
        }

        return result;
    }

    private static object? ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var i) ? i : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElement).ToArray(),
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => ConvertJsonElement(p.Value), StringComparer.OrdinalIgnoreCase),
            _ => element.GetRawText()
        };
    }

    private static StateMetadata BuildStateMetadata(RunOrchestrationResult result)
    {
        var manifest = result.ManifestMetadata;
        return new StateMetadata
        {
            RunId = result.RunId,
            TemplateId = manifest.TemplateId,
            TemplateTitle = manifest.TemplateTitle,
            TemplateVersion = manifest.TemplateVersion,
            Mode = manifest.Mode,
            TelemetrySourcesResolved = result.TelemetrySourcesResolved,
            ProvenanceHash = manifest.ProvenanceHash,
            Schema = new SchemaMetadata
            {
                Id = manifest.Schema.Id,
                Version = manifest.Schema.Version,
                Hash = manifest.Schema.Hash
            },
            Storage = new StorageDescriptor
            {
                ModelPath = manifest.Storage.ModelPath,
                MetadataPath = manifest.Storage.MetadataPath,
                ProvenancePath = manifest.Storage.ProvenancePath
            }
        };
    }

    private static IReadOnlyList<StateWarning> BuildStateWarnings(TelemetryManifest manifest)
    {
        if (manifest.Warnings is null || manifest.Warnings.Count == 0)
        {
            return Array.Empty<StateWarning>();
        }

        return manifest.Warnings.Select(w => new StateWarning
        {
            Code = w.Code,
            Message = w.Message,
            NodeId = w.NodeId,
            Severity = "warning"
        }).ToArray();
    }

    private static RunSummary BuildRunSummary(RunOrchestrationResult result)
    {
        var created = ParseCreated(result.RunDocument.CreatedUtc);
        return new RunSummary
        {
            RunId = result.RunId,
            TemplateId = result.ManifestMetadata.TemplateId,
            TemplateTitle = result.ManifestMetadata.TemplateTitle,
            TemplateVersion = result.ManifestMetadata.TemplateVersion,
            Mode = result.ManifestMetadata.Mode,
            CreatedUtc = created,
            WarningCount = result.TelemetryManifest.Warnings?.Count ?? 0,
            Telemetry = BuildTelemetrySummary(result)
        };
    }

    private static RunTelemetrySummary BuildTelemetrySummary(RunOrchestrationResult result)
    {
        var manifest = result.TelemetryManifest;

        var available = manifest.Files is { Count: > 0 };
        string? generatedAtUtc = available ? manifest.Provenance?.CapturedAtUtc : null;
        string? sourceRunId = available && !string.IsNullOrWhiteSpace(manifest.Provenance?.RunId)
            ? manifest.Provenance!.RunId
            : null;

        // Fallback: if manifest has no files (older runs), infer from directory contents and autocapture.json
        if (!available)
        {
            try
            {
                var telemetryDir = Path.Combine(result.RunDirectory, "model", "telemetry");
                if (Directory.Exists(telemetryDir))
                {
                    var hasCsvs = Directory.EnumerateFiles(telemetryDir, "*.csv").Any();
                    if (hasCsvs)
                    {
                        available = true;
                        var autoPath = Path.Combine(telemetryDir, "autocapture.json");
                        if (File.Exists(autoPath))
                        {
                            var json = File.ReadAllText(autoPath);
                            using var doc = System.Text.Json.JsonDocument.Parse(json);
                            var root = doc.RootElement;
                            if (generatedAtUtc is null && root.TryGetProperty("generatedAtUtc", out var genProp))
                            {
                                generatedAtUtc = genProp.GetString();
                            }
                            if (sourceRunId is null && root.TryGetProperty("sourceRunId", out var srcProp))
                            {
                                sourceRunId = srcProp.GetString();
                            }
                        }
                    }
                }
            }
            catch
            {
                // Ignore fallback errors
            }
        }

        return new RunTelemetrySummary
        {
            Available = available,
            GeneratedAtUtc = generatedAtUtc,
            WarningCount = manifest.Warnings?.Count ?? 0,
            SourceRunId = string.IsNullOrWhiteSpace(sourceRunId) ? null : sourceRunId
        };
    }

    private static RunCreatePlan BuildPlan(RunOrchestrationPlan plan)
    {
        var files = plan.TelemetryManifest.Files is { Count: > 0 }
            ? plan.TelemetryManifest.Files.Select(file => new RunCreatePlanFile
            {
                NodeId = file.NodeId,
                Metric = file.Metric.ToString(),
                Path = file.Path
            }).ToArray()
            : Array.Empty<RunCreatePlanFile>();

        var warnings = plan.TelemetryManifest.Warnings is { Count: > 0 }
            ? plan.TelemetryManifest.Warnings.Select(w => new RunCreatePlanWarning
            {
                Code = w.Code,
                Message = w.Message,
                NodeId = w.NodeId,
                Bins = w.Bins is null ? null : w.Bins.ToArray()
            }).ToArray()
            : Array.Empty<RunCreatePlanWarning>();

        return new RunCreatePlan
        {
            TemplateId = plan.TemplateId,
            Mode = plan.Mode,
            OutputRoot = plan.OutputRoot,
            CaptureDirectory = plan.CaptureDirectory,
            DeterministicRunId = plan.DeterministicRunId,
            RequestedRunId = plan.RequestedRunId,
            Parameters = plan.Parameters,
            TelemetryBindings = plan.TelemetryBindings,
            Files = files,
            Warnings = warnings
        };
    }

    private static bool DetermineCanReplay(RunOrchestrationResult result)
    {
        var storage = result.ManifestMetadata.Storage;
        var modelPath = storage.ModelPath;
        var metadataPath = storage.MetadataPath;
        var modelDirectory = Path.GetDirectoryName(modelPath);
        var hasModel = !string.IsNullOrWhiteSpace(modelPath) && File.Exists(modelPath);
        var hasMetadata = !string.IsNullOrWhiteSpace(metadataPath) && File.Exists(metadataPath);

        var telemetryManifestPath = modelDirectory is null
            ? null
            : Path.Combine(modelDirectory, "telemetry", "telemetry-manifest.json");
        var hasTelemetryManifest = telemetryManifestPath is not null && File.Exists(telemetryManifestPath);

        return hasModel && hasMetadata && hasTelemetryManifest && result.TelemetrySourcesResolved;
    }

    private static DateTimeOffset? ParseCreated(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;
    }
}
