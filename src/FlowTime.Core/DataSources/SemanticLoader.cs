using System;
using System.Linq;
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

        double[]? attempts = semantics.Attempts != null
            ? LoadSeries(semantics.Attempts, bins)
            : null;

        double[]? failures = semantics.Failures != null
            ? LoadSeries(semantics.Failures, bins)
            : null;

        double[]? retryEcho = semantics.RetryEcho != null
            ? LoadSeries(semantics.RetryEcho, bins)
            : null;

        var retryKernel = semantics.RetryKernel?.ToArray();

        double[]? externalDemand = semantics.ExternalDemand != null
            ? LoadSeries(semantics.ExternalDemand, bins)
            : null;

        double[]? queueDepth = semantics.QueueDepth != null
            ? LoadSeries(semantics.QueueDepth, bins)
            : null;

        double[]? capacity = semantics.Capacity != null
            ? LoadSeries(semantics.Capacity, bins)
            : null;

        return new NodeData
        {
            NodeId = node.Id,
            Arrivals = arrivals,
            Served = served,
            Errors = errors,
            Attempts = attempts,
            Failures = failures ?? errors,
            RetryEcho = retryEcho,
            RetryKernel = retryKernel,
            ExternalDemand = externalDemand,
            QueueDepth = queueDepth,
            Capacity = capacity,
            Values = null
        };
    }

    private double[] LoadSeries(string uri, int bins)
    {
        var path = UriResolver.ResolveFilePath(uri, modelDirectory);
        return CsvReader.ReadTimeSeries(path, bins);
    }
}
