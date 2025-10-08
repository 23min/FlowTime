using System;
using FlowTime.Core.Models;

namespace FlowTime.Core.DataSources;

public sealed class SemanticLoader
{
    private readonly string? modelDirectory;

    public SemanticLoader(string? modelDirectory)
    {
        this.modelDirectory = modelDirectory;
    }

    public NodeData LoadNodeData(Node node, int bins)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(node.Semantics);

        if (bins <= 0)
            throw new ArgumentOutOfRangeException(nameof(bins), bins, "bins must be positive");

        var semantics = node.Semantics;
        var arrivals = LoadSeries(semantics.Arrivals, bins);
        var served = LoadSeries(semantics.Served, bins);
        var errors = LoadSeries(semantics.Errors, bins);

        double[]? externalDemand = semantics.ExternalDemand != null
            ? LoadSeries(semantics.ExternalDemand, bins)
            : null;

        double[]? queueDepth = semantics.QueueDepth != null
            ? LoadSeries(semantics.QueueDepth, bins)
            : null;

        return new NodeData
        {
            NodeId = node.Id,
            Arrivals = arrivals,
            Served = served,
            Errors = errors,
            ExternalDemand = externalDemand,
            QueueDepth = queueDepth
        };
    }

    private double[] LoadSeries(string uri, int bins)
    {
        var path = UriResolver.ResolveFilePath(uri, modelDirectory);
        return CsvReader.ReadTimeSeries(path, bins);
    }
}
