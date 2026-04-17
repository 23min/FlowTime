using System.IO;
using System.Linq;
using System.Text.Json;
using FlowTime.Contracts.TimeTravel;
using FlowTime.TimeMachine.Artifacts;
using FlowTime.TimeMachine.Models;

namespace FlowTime.TimeMachine.Orchestration;

public static class RunOrchestrationContractMapper
{
    public static Dictionary<string, object?> ConvertParameters(Dictionary<string, JsonElement>? source)
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

    public static Dictionary<string, string>? CloneTelemetryBindings(RunTelemetryOptions? telemetry)
    {
        if (telemetry?.Bindings is not { Count: > 0 })
        {
            return null;
        }

        return new Dictionary<string, string>(telemetry.Bindings, StringComparer.OrdinalIgnoreCase);
    }

    public static RunCreatePlan BuildPlan(RunOrchestrationPlan plan)
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

    public static StateMetadata BuildStateMetadata(RunOrchestrationResult result)
    {
        var manifest = result.ManifestMetadata;
        return new StateMetadata
        {
            RunId = result.RunId,
            TemplateId = manifest.TemplateId,
            TemplateTitle = manifest.TemplateTitle,
            TemplateNarrative = manifest.TemplateNarrative,
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
            },
            InputHash = result.InputHash ?? result.RunDocument.InputHash,
            Rng = new RunRngOptions { Kind = "pcg32", Seed = result.RngSeed },
            EdgeQuality = "missing"
        };
    }

    public static IReadOnlyList<StateWarning> BuildStateWarnings(TelemetryManifest manifest)
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
            Severity = string.IsNullOrWhiteSpace(w.Severity) ? "warning" : w.Severity
        }).ToArray();
    }

    public static RunTelemetrySummary BuildTelemetrySummary(RunOrchestrationResult result)
    {
        var manifest = result.TelemetryManifest;

        var available = manifest.Files is { Count: > 0 };
        string? generatedAtUtc = available ? manifest.Provenance?.CapturedAtUtc : null;
        string? sourceRunId = available && !string.IsNullOrWhiteSpace(manifest.Provenance?.RunId)
            ? manifest.Provenance!.RunId
            : null;

        if (!available)
        {
            TryInferLegacyTelemetry(result, ref available, ref generatedAtUtc, ref sourceRunId);
        }

        return new RunTelemetrySummary
        {
            Available = available,
            GeneratedAtUtc = generatedAtUtc,
            WarningCount = manifest.Warnings?.Count ?? 0,
            SourceRunId = string.IsNullOrWhiteSpace(sourceRunId) ? null : sourceRunId
        };
    }

    public static RunSummary BuildRunSummary(RunOrchestrationResult result)
    {
        var created = ParseCreated(result.RunDocument.CreatedUtc);
        return new RunSummary
        {
            RunId = result.RunId,
            TemplateId = result.ManifestMetadata.TemplateId,
            TemplateTitle = result.ManifestMetadata.TemplateTitle,
            TemplateNarrative = result.ManifestMetadata.TemplateNarrative,
            TemplateVersion = result.ManifestMetadata.TemplateVersion,
            Mode = result.ManifestMetadata.Mode,
            CreatedUtc = created,
            WarningCount = result.TelemetryManifest.Warnings?.Count ?? 0,
            Telemetry = BuildTelemetrySummary(result),
            Rng = new RunRngOptions { Kind = "pcg32", Seed = result.RngSeed },
            InputHash = result.InputHash ?? result.RunDocument.InputHash
        };
    }

    public static bool DetermineCanReplay(RunOrchestrationResult result)
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

    private static void TryInferLegacyTelemetry(
        RunOrchestrationResult result,
        ref bool available,
        ref string? generatedAtUtc,
        ref string? sourceRunId)
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
                        using var doc = JsonDocument.Parse(json);
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

    private static object? ConvertJsonElement(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var i) ? i : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElement).ToArray(),
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(p => p.Name, p => ConvertJsonElement(p.Value), StringComparer.OrdinalIgnoreCase),
            _ => element.GetRawText()
        };

    private static DateTimeOffset? ParseCreated(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;
    }
}
