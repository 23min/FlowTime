using FlowTime.Adapters.Synthetic;
using FlowTime.Contracts;
using FlowTime.Contracts.Services;
using FlowTime.Core.DataSources;
using FlowTime.Core.Metrics;
using FlowTime.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FlowTime.API.Services;

public sealed class StateQueryService
{
    private const int maxWindowBins = 500;
    private readonly IConfiguration configuration;
    private readonly ILogger<StateQueryService> logger;

    public StateQueryService(IConfiguration configuration, ILogger<StateQueryService> logger)
    {
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<StateResponse> GetStateAsync(string runId, int binIndex, CancellationToken cancellationToken = default)
    {
        var context = await LoadContextAsync(runId, cancellationToken);

        if (binIndex < 0 || binIndex >= context.Window.Bins)
        {
            throw new StateQueryException(400, $"binIndex must be between 0 and {context.Window.Bins - 1}.");
        }

        var binStart = context.Window.GetBinStartTime(binIndex);
        var binEnd = binStart?.Add(context.Window.BinDuration);

        var nodes = new Dictionary<string, NodeState>(StringComparer.Ordinal);
        foreach (var topologyNode in context.Topology.Nodes)
        {
            if (!context.NodeData.TryGetValue(topologyNode.Id, out var data))
            {
                throw new StateQueryException(500, $"Data for node '{topologyNode.Id}' was not loaded. Available nodes: {string.Join(", ", context.NodeData.Keys)}");
            }

            nodes[topologyNode.Id] = BuildNodeState(topologyNode, data, context, binIndex);
        }

        return new StateResponse
        {
            RunId = context.Manifest.RunId,
            Mode = MapRunSourceToMode(context.Manifest.Source),
            Window = new StateWindowInfo
            {
                Start = context.Window.StartTime,
                Timezone = context.Manifest.Grid.Timezone
            },
            Grid = new StateGridInfo
            {
                Bins = context.Window.Bins,
                BinSize = context.Window.BinSize,
                BinUnit = context.Window.BinUnit.ToString().ToLowerInvariant(),
                BinMinutes = context.Window.BinDuration.TotalMinutes
            },
            Bin = new StateBinInfo
            {
                Index = binIndex,
                StartUtc = binStart,
                EndUtc = binEnd
            },
            Nodes = nodes
        };
    }

    public async Task<StateWindowResponse> GetStateWindowAsync(string runId, int startBin, int endBin, CancellationToken cancellationToken = default)
    {
        var context = await LoadContextAsync(runId, cancellationToken);

        if (startBin < 0 || startBin >= context.Window.Bins)
        {
            throw new StateQueryException(400, $"startBin must be between 0 and {context.Window.Bins - 1}.");
        }

        if (endBin < 0 || endBin >= context.Window.Bins)
        {
            throw new StateQueryException(400, $"endBin must be between 0 and {context.Window.Bins - 1}.");
        }

        if (endBin < startBin)
        {
            throw new StateQueryException(400, "endBin must be greater than or equal to startBin.");
        }

        var count = endBin - startBin + 1;
        if (count > maxWindowBins)
        {
            throw new StateQueryException(413, $"Requested bin range {count} exceeds maximum supported window size of {maxWindowBins}.");
        }

        var timestamps = new List<DateTime?>(count);
        for (var idx = startBin; idx <= endBin; idx++)
        {
            timestamps.Add(context.Window.GetBinStartTime(idx));
        }

        var nodes = new Dictionary<string, NodeWindowSeries>(StringComparer.Ordinal);
        foreach (var topologyNode in context.Topology.Nodes)
        {
            if (!context.NodeData.TryGetValue(topologyNode.Id, out var data))
            {
                throw new StateQueryException(500, $"Data for node '{topologyNode.Id}' was not loaded. Available nodes: {string.Join(", ", context.NodeData.Keys)}");
            }

            var series = BuildNodeSeries(topologyNode, data, context, startBin, count);
            nodes[topologyNode.Id] = series;
        }

        return new StateWindowResponse
        {
            RunId = context.Manifest.RunId,
            Mode = MapRunSourceToMode(context.Manifest.Source),
            Window = new StateWindowInfo
            {
                Start = context.Window.StartTime,
                Timezone = context.Manifest.Grid.Timezone
            },
            Grid = new StateGridInfo
            {
                Bins = context.Window.Bins,
                BinSize = context.Window.BinSize,
                BinUnit = context.Window.BinUnit.ToString().ToLowerInvariant(),
                BinMinutes = context.Window.BinDuration.TotalMinutes
            },
            Slice = new StateSliceInfo
            {
                StartBin = startBin,
                EndBin = endBin,
                Bins = count
            },
            Timestamps = timestamps,
            Nodes = nodes
        };
    }

    private async Task<StateRunContext> LoadContextAsync(string runId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(runId))
        {
            throw new StateQueryException(400, "runId must be provided.");
        }

        var artifactsDirectory = Program.GetArtifactsDirectory(configuration);
        var runDirectory = Path.Combine(artifactsDirectory, runId);

        if (!Directory.Exists(runDirectory))
        {
            throw new StateQueryException(404, $"Run '{runId}' not found.");
        }

        try
        {
            var reader = new FileSeriesReader();
            var adapter = new RunArtifactAdapter(reader, runDirectory);
            var manifest = await adapter.GetManifestAsync();

            var modelPath = ResolveModelPath(runDirectory);
            var modelYaml = await File.ReadAllTextAsync(modelPath, cancellationToken);

            ModelDefinition modelDefinition;
            try
            {
                modelDefinition = ModelService.ParseAndConvert(modelYaml);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to parse model for run {RunId}", runId);
                throw new StateQueryException(409, $"Model for run '{runId}' could not be parsed: {ex.Message}");
            }

            ModelMetadata metadata;
            try
            {
                metadata = ModelParser.ParseMetadata(modelDefinition, Path.GetDirectoryName(modelPath));
            }
            catch (ModelParseException ex)
            {
                logger.LogError(ex, "Invalid model metadata for run {RunId}", runId);
                throw new StateQueryException(409, $"Model metadata for run '{runId}' is invalid: {ex.Message}");
            }

            if (metadata.Topology is null)
            {
                throw new StateQueryException(412, $"Run '{runId}' does not include topology information required for state queries.");
            }

            var modelDirectory = Path.GetDirectoryName(modelPath);
            var loader = new SemanticLoader(modelDirectory);
            var nodeData = new Dictionary<string, NodeData>(StringComparer.Ordinal);

            foreach (var node in metadata.Topology.Nodes)
            {
                try
                {
                    var data = loader.LoadNodeData(node, metadata.Window.Bins);
                    nodeData[node.Id] = data;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to load series for node {NodeId} in run {RunId}", node.Id, runId);
                    throw new StateQueryException(500, $"Failed to load data for node '{node.Id}' in run '{runId}': {ex.Message}");
                }
            }

            return new StateRunContext(manifest, metadata.Window, metadata.Topology, nodeData);
        }
        catch (StateQueryException)
        {
            throw;
        }
        catch (FileNotFoundException ex)
        {
            logger.LogError(ex, "Missing artifact for run {RunId}", runId);
            throw new StateQueryException(404, $"Required artifact missing for run '{runId}': {ex.Message}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error loading run {RunId}", runId);
            throw new StateQueryException(500, $"Unexpected error loading run '{runId}': {ex.Message}");
        }
    }

    private static NodeState BuildNodeState(Node node, NodeData data, StateRunContext context, int binIndex)
    {
        var kind = NormalizeKind(node.Kind);
        var arrivals = GetValue(data.Arrivals, binIndex);
        var served = GetValue(data.Served, binIndex);
        var errors = GetValue(data.Errors, binIndex);
        var externalDemand = GetOptionalValue(data.ExternalDemand, binIndex);
        var queue = GetOptionalValue(data.QueueDepth, binIndex);
        var capacity = GetOptionalValue(data.Capacity, binIndex);

        double? utilization = null;
        double? latencyMinutes = null;

        if (served.HasValue)
        {
            utilization = UtilizationComputer.Calculate(served.Value, capacity);

            if (string.Equals(kind, "queue", StringComparison.OrdinalIgnoreCase))
            {
                if (queue.HasValue)
                {
                    latencyMinutes = LatencyComputer.Calculate(queue.Value, served.Value, context.Window.BinDuration.TotalMinutes);
                }
            }
        }

        string color = string.Equals(kind, "queue", StringComparison.OrdinalIgnoreCase)
            ? ColoringRules.PickQueueColor(latencyMinutes, node.Semantics.SlaMinutes)
            : ColoringRules.PickServiceColor(utilization);

        return new NodeState
        {
            Kind = kind,
            Arrivals = arrivals,
            Served = served,
            Errors = errors,
            Queue = queue,
            ExternalDemand = externalDemand,
            Capacity = capacity,
            Utilization = utilization,
            LatencyMinutes = latencyMinutes,
            SlaMinutes = node.Semantics.SlaMinutes,
            Color = color
        };
    }

    private static NodeWindowSeries BuildNodeSeries(Node node, NodeData data, StateRunContext context, int startBin, int count)
    {
        var kind = NormalizeKind(node.Kind);
        var series = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["arrivals"] = ExtractSlice(data.Arrivals, startBin, count),
            ["served"] = ExtractSlice(data.Served, startBin, count),
            ["errors"] = ExtractSlice(data.Errors, startBin, count)
        };

        if (data.ExternalDemand != null)
        {
            series["externalDemand"] = ExtractSlice(data.ExternalDemand, startBin, count);
        }

        if (data.QueueDepth != null)
        {
            series["queue"] = ExtractSlice(data.QueueDepth, startBin, count);
        }

        if (data.Capacity != null)
        {
            series["capacity"] = ExtractSlice(data.Capacity, startBin, count);
            var utilizationSeries = ComputeUtilizationSeries(data, startBin, count);
            if (utilizationSeries != null)
            {
                series["utilization"] = utilizationSeries;
            }
        }

        if (string.Equals(kind, "queue", StringComparison.OrdinalIgnoreCase) && data.QueueDepth != null)
        {
            var latency = ComputeLatencySeries(data, context.Window, startBin, count);
            if (latency != null)
            {
                series["latencyMinutes"] = latency;
            }
        }

        return new NodeWindowSeries
        {
            Kind = kind,
            Series = series
        };
    }

    private static double?[] ExtractSlice(double[] source, int start, int count)
    {
        var result = new double?[count];
        for (var i = 0; i < count; i++)
        {
            result[i] = source[start + i];
        }
        return result;
    }

    private static double?[]? ComputeUtilizationSeries(NodeData data, int start, int count)
    {
        if (data.Capacity == null)
        {
            return null;
        }

        var result = new double?[count];
        for (var i = 0; i < count; i++)
        {
            var served = data.Served[start + i];
            var capacity = data.Capacity[start + i];
            result[i] = UtilizationComputer.Calculate(served, capacity);
        }

        return result;
    }

    private static double?[]? ComputeLatencySeries(NodeData data, Window window, int start, int count)
    {
        if (data.QueueDepth == null)
        {
            return null;
        }

        var result = new double?[count];
        var binMinutes = window.BinDuration.TotalMinutes;
        for (var i = 0; i < count; i++)
        {
            var queue = data.QueueDepth[start + i];
            var served = data.Served[start + i];
            result[i] = LatencyComputer.Calculate(queue, served, binMinutes);
        }

        return result;
    }

    private static string ResolveModelPath(string runDirectory)
    {
        var explicitModelPath = Path.Combine(runDirectory, "model", "model.yaml");
        if (File.Exists(explicitModelPath))
        {
            return explicitModelPath;
        }

        var specPath = Path.Combine(runDirectory, "spec.yaml");
        if (File.Exists(specPath))
        {
            return specPath;
        }

        throw new FileNotFoundException("Run is missing model/model.yaml or spec.yaml");
    }

    private static string NormalizeKind(string? kind)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            return "service";
        }

        return kind.Trim();
    }

    private static double? GetValue(double[] source, int index)
    {
        if (index < 0 || index >= source.Length)
        {
            return null;
        }

        return source[index];
    }

    private static double? GetOptionalValue(double[]? source, int index)
    {
        if (source == null)
        {
            return null;
        }

        if (index < 0 || index >= source.Length)
        {
            return null;
        }

        return source[index];
    }

    private static string? MapRunSourceToMode(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        if (source.Equals("simulation", StringComparison.OrdinalIgnoreCase))
        {
            return "simulation";
        }

        if (source.Equals("engine", StringComparison.OrdinalIgnoreCase) || source.Equals("telemetry", StringComparison.OrdinalIgnoreCase))
        {
            return "telemetry";
        }

        return source.ToLowerInvariant();
    }

    private sealed record StateRunContext(RunManifest Manifest, Window Window, Topology Topology, IReadOnlyDictionary<string, NodeData> NodeData)
    {
        public double BinMinutes => Window.BinDuration.TotalMinutes;
    }
}

public sealed class StateQueryException : Exception
{
    public int StatusCode { get; }

    public StateQueryException(int statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }
}
