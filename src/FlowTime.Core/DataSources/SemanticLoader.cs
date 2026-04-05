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
        var arrivals = LoadRequiredSeries(semantics.ArrivalsRef, bins, "arrivals");
        var served = LoadRequiredSeries(semantics.ServedRef, bins, "served");
        double[]? errors = LoadOptionalSeries(semantics.ErrorsRef, bins);

        double[]? attempts = LoadOptionalSeries(semantics.AttemptsRef, bins);

        double[]? failures = LoadOptionalSeries(semantics.FailuresRef, bins);

        double[]? exhaustedFailures = LoadOptionalSeries(semantics.ExhaustedFailuresRef, bins);

        double[]? retryEcho = LoadOptionalSeries(semantics.RetryEchoRef, bins);

        double[]? retryBudgetRemaining = LoadOptionalSeries(semantics.RetryBudgetRemainingRef, bins);

        var retryKernel = semantics.RetryKernel?.ToArray();

        double[]? externalDemand = LoadOptionalSeries(semantics.ExternalDemandRef, bins);

        double[]? queueDepth = LoadOptionalSeries(semantics.QueueDepthRef, bins);

        double[]? capacity = LoadOptionalSeries(semantics.CapacityRef, bins);

        var parallelism = ResolveParallelism(semantics.ParallelismRef, bins);

        double[]? processingTimeMsSum = LoadOptionalSeries(semantics.ProcessingTimeMsSumRef, bins);

        double[]? servedCount = LoadOptionalSeries(semantics.ServedCountRef, bins);

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
        var arrivals = LoadOptionalSeries(semantics.ArrivalsRef, bins)
            ?? CreateConstantSeries(double.NaN, bins);
        var served = LoadOptionalSeries(semantics.ServedRef, bins)
            ?? CreateConstantSeries(double.NaN, bins);
        double[]? errors = LoadOptionalSeries(semantics.ErrorsRef, bins);
        double[]? latencyMinutes = LoadOptionalSeries(semantics.LatencyMinutesRef, bins);

        return new ConstraintData
        {
            Id = constraint.Id,
            Arrivals = arrivals,
            Served = served,
            Errors = errors,
            LatencyMinutes = latencyMinutes
        };
    }

    private double[]? ResolveParallelism(CompiledParallelismReference? parallelism, int bins)
    {
        if (parallelism is null)
        {
            return null;
        }

        if (parallelism.Constant.HasValue)
        {
            return CreateConstantSeries(parallelism.Constant.Value, bins);
        }

        return LoadOptionalSeries(parallelism.Series, bins);
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

    private double[] LoadSeries(string uri, int bins)
    {
        var path = UriResolver.ResolveFilePath(uri, modelDirectory);
        return CsvReader.ReadTimeSeries(path, bins);
    }

    private double[] LoadRequiredSeries(CompiledSeriesReference? reference, int bins, string fieldName)
    {
        var series = LoadOptionalSeries(reference, bins);
        return series ?? throw new InvalidOperationException(
            $"SemanticLoader requires a file-backed compiled reference for '{fieldName}'.");
    }

    private double[]? LoadOptionalSeries(CompiledSeriesReference? reference, int bins)
    {
        if (reference is null || reference.Kind != CompiledSeriesReferenceKind.File)
        {
            return null;
        }

        return LoadSeries(reference.RawText, bins);
    }
}
