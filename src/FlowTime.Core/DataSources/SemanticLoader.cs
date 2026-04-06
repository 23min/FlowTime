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
        double[]? errors = TryLoadSeries(semantics.Errors, bins);

        double[]? attempts = TryLoadSeries(semantics.Attempts, bins);

        double[]? failures = TryLoadSeries(semantics.Failures, bins);

        double[]? exhaustedFailures = TryLoadSeries(semantics.ExhaustedFailures, bins);

        double[]? retryEcho = TryLoadSeries(semantics.RetryEcho, bins);

        double[]? retryBudgetRemaining = TryLoadSeries(semantics.RetryBudgetRemaining, bins);

        var retryKernel = semantics.RetryKernel?.ToArray();

        double[]? externalDemand = TryLoadSeries(semantics.ExternalDemand, bins);

        double[]? queueDepth = TryLoadSeries(semantics.QueueDepth, bins);

        double[]? capacity = TryLoadSeries(semantics.Capacity, bins);

        var parallelism = ResolveParallelism(semantics.Parallelism, bins);

        double[]? processingTimeMsSum = TryLoadSeries(semantics.ProcessingTimeMsSum, bins);

        double[]? servedCount = TryLoadSeries(semantics.ServedCount, bins);

        return new NodeData
        {
            NodeId = node.Id,
            Arrivals = arrivals,
            Served = served,
            Errors = errors,
            Attempts = attempts,
            Failures = failures ?? errors,
            ExhaustedFailures = exhaustedFailures,
            RetryEcho = retryEcho,
            RetryKernel = retryKernel,
            ExternalDemand = externalDemand,
            QueueDepth = queueDepth,
            Capacity = capacity,
            Parallelism = parallelism,
            ProcessingTimeMsSum = processingTimeMsSum,
            ServedCount = servedCount,
            RetryBudgetRemaining = retryBudgetRemaining,
            Values = null
        };
    }

    public ConstraintData LoadConstraintData(Constraint constraint, int bins)
    {
        ArgumentNullException.ThrowIfNull(constraint);
        ArgumentNullException.ThrowIfNull(constraint.Semantics);

        if (bins <= 0)
            throw new ArgumentOutOfRangeException(nameof(bins), bins, "bins must be positive");

        var semantics = constraint.Semantics;
        var arrivals = semantics.Arrivals.Kind == CompiledSeriesReferenceKind.File
            ? LoadSeries(semantics.Arrivals, bins)
            : CreateConstantSeries(double.NaN, bins);
        var served = semantics.Served.Kind == CompiledSeriesReferenceKind.File
            ? LoadSeries(semantics.Served, bins)
            : CreateConstantSeries(double.NaN, bins);
        double[]? errors = TryLoadSeries(semantics.Errors, bins);
        double[]? latencyMinutes = TryLoadSeries(semantics.LatencyMinutes, bins);

        return new ConstraintData
        {
            Id = constraint.Id,
            Arrivals = arrivals,
            Served = served,
            Errors = errors,
            LatencyMinutes = latencyMinutes
        };
    }

    private double[]? ResolveParallelism(ParallelismReference? parallelism, int bins)
    {
        if (parallelism is null)
        {
            return null;
        }

        if (parallelism.SeriesReference is { Kind: CompiledSeriesReferenceKind.File } seriesReference)
        {
            return LoadSeries(seriesReference, bins);
        }

        if (parallelism.Constant.HasValue)
        {
            var value = parallelism.Constant.Value;
            if (!double.IsFinite(value) || value <= 0d)
            {
                return null;
            }

            return CreateConstantSeries(value, bins);
        }

        return null;
    }

    private static double[] CreateConstantSeries(double value, int bins)
    {
        var series = new double[bins];
        for (var i = 0; i < bins; i++)
        {
            series[i] = value;
        }

        return series;
    }

    private double[]? TryLoadSeries(CompiledSeriesReference? reference, int bins)
    {
        return reference is { Kind: CompiledSeriesReferenceKind.File }
            ? LoadSeries(reference, bins)
            : null;
    }

    private double[] LoadSeries(CompiledSeriesReference reference, int bins)
    {
        var path = UriResolver.ResolveFilePath(reference.RawText, modelDirectory);
        return CsvReader.ReadTimeSeries(path, bins);
    }
}
