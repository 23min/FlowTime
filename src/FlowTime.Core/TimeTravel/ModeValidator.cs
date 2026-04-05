using System.Globalization;
using FlowTime.Core.Compiler;
using FlowTime.Core.Models;

namespace FlowTime.Core.TimeTravel;

public sealed class ModeValidator
{
    public ModeValidationResult Validate(ModeValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var mode = context.Mode;
        var isSimulation = string.Equals(mode, "simulation", StringComparison.OrdinalIgnoreCase);
        var isTelemetry = string.Equals(mode, "telemetry", StringComparison.OrdinalIgnoreCase);

        var warnings = new List<ModeValidationWarning>(context.InitialWarnings);
        var nodeWarnings = context.InitialNodeWarnings.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.ToList(),
            StringComparer.Ordinal);

        if (isTelemetry && context.ManifestMetadata.TelemetrySources.Count == 0)
        {
            warnings.Add(new ModeValidationWarning
            {
                Code = "telemetry_sources_missing",
                Message = "Telemetry mode run resolved no telemetry sources."
            });
        }

        string? errorCode = null;
        string? errorMessage = null;

        foreach (var node in context.Topology.Nodes)
        {
            if (!context.NodeData.TryGetValue(node.Id, out var data))
            {
                errorCode = "missing_node_data";
                errorMessage = $"Node '{node.Id}' is missing data in the run artifacts.";
                break;
            }

            var missingSeries = new List<string>();
            var invalidSeries = new List<string>();

            void ProcessSeries(string? semanticsValue, double[]? series, string label, bool required)
            {
                var expectedLength = context.Window.Bins;
                var isMissing = series == null || series.Length != expectedLength;

                if (required && isMissing)
                {
                    missingSeries.Add(label);
                }

                if (!isMissing && series != null)
                {
                    for (var i = 0; i < series.Length; i++)
                    {
                        if (double.IsNaN(series[i]) || double.IsInfinity(series[i]))
                        {
                            invalidSeries.Add(label);
                            break;
                        }
                    }
                }
            }

            ProcessSeries(node.Semantics.Arrivals, data.Arrivals, "arrivals", required: true);
            ProcessSeries(node.Semantics.Served, data.Served, "served", required: IsServiceNode(node));
            ProcessSeries(node.Semantics.QueueDepth, data.QueueDepth, "queue", required: IsQueueNode(node));
            ProcessSeries(node.Semantics.Attempts, data.Attempts, "attempts", required: false);
            ProcessSeries(node.Semantics.Failures, data.Failures, "failures", required: false);
            ProcessSeries(node.Semantics.RetryEcho, data.RetryEcho, "retryEcho", required: false);

            if (isSimulation && invalidSeries.Count > 0)
            {
                errorCode = "mode_validation_failed";
                errorMessage = $"Node '{node.Id}' contains invalid values for {string.Join(", ", invalidSeries)} in simulation mode.";
                break;
            }

            if (isSimulation && missingSeries.Count > 0)
            {
                errorCode = "mode_validation_failed";
                errorMessage = $"Node '{node.Id}' is missing required {string.Join(", ", missingSeries)} series for simulation mode.";
                break;
            }

            if (isTelemetry)
            {
                var unresolvedSources = CollectUnresolvedSources(node.Semantics);
                if (unresolvedSources.Count > 0 || invalidSeries.Count > 0)
                {
                    var messages = new List<string>();
                    if (unresolvedSources.Count > 0)
                    {
                        messages.Add($"sources for {string.Join(", ", unresolvedSources)}");
                    }

                    if (invalidSeries.Count > 0)
                    {
                        messages.Add($"invalid values in {string.Join(", ", invalidSeries)}");
                    }

                    if (!nodeWarnings.TryGetValue(node.Id, out var nodeList))
                    {
                        nodeList = new List<ModeValidationWarning>();
                        nodeWarnings[node.Id] = nodeList;
                    }

                    nodeList.Add(new ModeValidationWarning
                    {
                        Code = unresolvedSources.Count > 0 ? "telemetry_sources_unresolved" : "telemetry_series_invalid",
                        Message = $"Telemetry mode detected {(string.Join(" and ", messages))}.",
                        NodeId = node.Id
                    });
                }
            }
        }

        if (errorCode != null)
        {
            return ModeValidationResult.WithError(errorCode, errorMessage ?? "Mode validation failed.");
        }

        var readonlyNodeWarnings = nodeWarnings.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<ModeValidationWarning>)kvp.Value,
            StringComparer.OrdinalIgnoreCase);

        return ModeValidationResult.Success(warnings, readonlyNodeWarnings);

        IReadOnlyList<string> CollectUnresolvedSources(NodeSemantics semantics)
        {
            var unresolved = new List<string>();

            Assess(semantics.Arrivals, semantics.ArrivalsRef, "arrivals");
            Assess(semantics.Served, semantics.ServedRef, "served");
            Assess(semantics.Errors, semantics.ErrorsRef, "errors");
            Assess(semantics.Attempts, semantics.AttemptsRef, "attempts");
            Assess(semantics.Failures, semantics.FailuresRef, "failures");
            Assess(semantics.RetryEcho, semantics.RetryEchoRef, "retryEcho");
            Assess(semantics.QueueDepth, semantics.QueueDepthRef, "queue");
            Assess(semantics.Capacity, semantics.CapacityRef, "capacity");
            Assess(semantics.ExternalDemand, semantics.ExternalDemandRef, "external_demand");
            AssessParallelism(semantics.ParallelismRawText, semantics.ParallelismRef, "parallelism");

            return unresolved;

            void Assess(string? rawValue, CompiledSeriesReference? reference, string label)
            {
                reference ??= TryParseSeriesReference(rawValue);
                if (reference is null || reference.Kind == CompiledSeriesReferenceKind.File)
                {
                    return;
                }

                var trimmed = reference.LookupKey;
                if (context.ManifestMetadata.NodeSources.ContainsKey(trimmed))
                {
                    return;
                }

                unresolved.Add(label);
            }

            void AssessParallelism(string? rawValue, CompiledParallelismReference? reference, string label)
            {
                reference ??= SemanticReferenceResolver.ParseParallelismReference(rawValue);
                if (reference?.Series is null)
                {
                    return;
                }

                Assess(reference.Series.RawText, reference.Series, label);
            }

            static CompiledSeriesReference? TryParseSeriesReference(string? rawValue)
            {
                if (string.IsNullOrWhiteSpace(rawValue))
                {
                    return null;
                }

                try
                {
                    return SemanticReferenceResolver.ParseOptionalSeriesReference(rawValue);
                }
                catch (ArgumentException)
                {
                    return null;
                }
            }
        }
    }

    private static bool IsServiceNode(Node node) =>
        string.Equals(node.Kind, "service", StringComparison.OrdinalIgnoreCase);

    private static bool IsQueueNode(Node node)
    {
        if (string.IsNullOrWhiteSpace(node.Kind))
        {
            return false;
        }

        return string.Equals(node.Kind, "queue", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(node.Kind, "dlq", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class ModeValidationContext
{
    public ModeValidationContext(
        RunManifestMetadata manifestMetadata,
        Window window,
        Topology topology,
        IReadOnlyDictionary<string, NodeData> nodeData,
        IReadOnlyList<ModeValidationWarning> initialWarnings,
        IReadOnlyDictionary<string, IReadOnlyList<ModeValidationWarning>> initialNodeWarnings)
    {
        ManifestMetadata = manifestMetadata ?? throw new ArgumentNullException(nameof(manifestMetadata));
        Window = window ?? throw new ArgumentNullException(nameof(window));
        Topology = topology ?? throw new ArgumentNullException(nameof(topology));
        NodeData = nodeData ?? throw new ArgumentNullException(nameof(nodeData));
        InitialWarnings = initialWarnings ?? Array.Empty<ModeValidationWarning>();
        InitialNodeWarnings = initialNodeWarnings ?? new Dictionary<string, IReadOnlyList<ModeValidationWarning>>();
    }

    public RunManifestMetadata ManifestMetadata { get; }
    public Window Window { get; }
    public Topology Topology { get; }
    public IReadOnlyDictionary<string, NodeData> NodeData { get; }
    public string Mode => ManifestMetadata.Mode;
    public IReadOnlyList<ModeValidationWarning> InitialWarnings { get; }
    public IReadOnlyDictionary<string, IReadOnlyList<ModeValidationWarning>> InitialNodeWarnings { get; }
}

public sealed class ModeValidationResult
{
    private ModeValidationResult(
        string? errorCode,
        string? errorMessage,
        IReadOnlyList<ModeValidationWarning> warnings,
        IReadOnlyDictionary<string, IReadOnlyList<ModeValidationWarning>> nodeWarnings)
    {
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
        Warnings = warnings;
        NodeWarnings = nodeWarnings;
    }

    public string? ErrorCode { get; }
    public string? ErrorMessage { get; }
    public bool HasErrors => ErrorCode != null;
    public IReadOnlyList<ModeValidationWarning> Warnings { get; }
    public IReadOnlyDictionary<string, IReadOnlyList<ModeValidationWarning>> NodeWarnings { get; }

    public static ModeValidationResult WithError(string errorCode, string errorMessage) =>
        new(errorCode, errorMessage, Array.Empty<ModeValidationWarning>(), new Dictionary<string, IReadOnlyList<ModeValidationWarning>>());

    public static ModeValidationResult Success(
        IReadOnlyList<ModeValidationWarning> warnings,
        IReadOnlyDictionary<string, IReadOnlyList<ModeValidationWarning>> nodeWarnings) =>
        new(null, null, warnings, nodeWarnings);
}

public sealed class ModeValidationWarning
{
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? NodeId { get; init; }
    public int? StartBin { get; init; }
    public int? EndBin { get; init; }
    public string? Signal { get; init; }
}
