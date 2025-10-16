using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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

        logger.LogInformation("Received run creation request for template {TemplateId}", request.TemplateId);

        var runsRoot = Program.ServiceHelpers.RunsRoot(configuration);
        var parameters = ConvertParameters(request.Parameters);
        var telemetryBindings = request.Telemetry?.Bindings is not null
            ? new Dictionary<string, string>(request.Telemetry.Bindings, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

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
            var result = await orchestration.CreateRunAsync(orchestrationRequest, cancellationToken).ConfigureAwait(false);
            var metadata = Program.BuildStateMetadata(result);
            var warnings = Program.BuildStateWarnings(result.TelemetryManifest);

            return Results.Created($"/v1/runs/{metadata.RunId}", new RunCreateResponse
            {
                Metadata = metadata,
                Warnings = warnings
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create run for template {TemplateId}", request.TemplateId);
            return Results.Problem(title: "Run creation failed", detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> HandleListRunsAsync(
        RunOrchestrationService orchestration,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var runsRoot = Program.ServiceHelpers.RunsRoot(configuration);
        if (!Directory.Exists(runsRoot))
        {
            return Results.Ok(new RunSummaryResponse());
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
        return Results.Ok(new RunSummaryResponse { Items = items, TotalCount = items.Count });
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
        return Results.Ok(new RunCreateResponse { Metadata = metadata, Warnings = warnings });
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
            WarningCount = result.TelemetryManifest.Warnings?.Count ?? 0
        };
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
