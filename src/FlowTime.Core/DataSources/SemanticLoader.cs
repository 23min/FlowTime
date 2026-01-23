using System;
using System.Globalization;
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
        double[]? errors = IsFileUri(semantics.Errors)
            ? LoadSeries(semantics.Errors!, bins)
            : null;

        double[]? attempts = IsFileUri(semantics.Attempts)
            ? LoadSeries(semantics.Attempts!, bins)
            : null;

        double[]? failures = IsFileUri(semantics.Failures)
            ? LoadSeries(semantics.Failures!, bins)
            : null;

        double[]? exhaustedFailures = IsFileUri(semantics.ExhaustedFailures)
            ? LoadSeries(semantics.ExhaustedFailures!, bins)
            : null;

        double[]? retryEcho = IsFileUri(semantics.RetryEcho)
            ? LoadSeries(semantics.RetryEcho!, bins)
            : null;

        double[]? retryBudgetRemaining = IsFileUri(semantics.RetryBudgetRemaining)
            ? LoadSeries(semantics.RetryBudgetRemaining!, bins)
            : null;

        var retryKernel = semantics.RetryKernel?.ToArray();

        double[]? externalDemand = IsFileUri(semantics.ExternalDemand)
            ? LoadSeries(semantics.ExternalDemand!, bins)
            : null;

        double[]? queueDepth = IsFileUri(semantics.QueueDepth)
            ? LoadSeries(semantics.QueueDepth!, bins)
            : null;

        double[]? capacity = IsFileUri(semantics.Capacity)
            ? LoadSeries(semantics.Capacity!, bins)
            : null;

        var parallelism = ResolveParallelism(semantics.Parallelism, bins);

        double[]? processingTimeMsSum = IsFileUri(semantics.ProcessingTimeMsSum)
            ? LoadSeries(semantics.ProcessingTimeMsSum!, bins)
            : null;

        double[]? servedCount = IsFileUri(semantics.ServedCount)
            ? LoadSeries(semantics.ServedCount!, bins)
            : null;

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
        var arrivals = IsFileUri(semantics.Arrivals)
            ? LoadSeries(semantics.Arrivals!, bins)
            : CreateConstantSeries(double.NaN, bins);
        var served = IsFileUri(semantics.Served)
            ? LoadSeries(semantics.Served!, bins)
            : CreateConstantSeries(double.NaN, bins);
        double[]? errors = IsFileUri(semantics.Errors)
            ? LoadSeries(semantics.Errors!, bins)
            : null;
        double[]? latencyMinutes = IsFileUri(semantics.LatencyMinutes)
            ? LoadSeries(semantics.LatencyMinutes!, bins)
            : null;

        return new ConstraintData
        {
            Id = constraint.Id,
            Arrivals = arrivals,
            Served = served,
            Errors = errors,
            LatencyMinutes = latencyMinutes
        };
    }

    private double[]? ResolveParallelism(object? parallelism, int bins)
    {
        if (parallelism is null)
        {
            return null;
        }

        if (parallelism is string seriesId)
        {
            if (IsFileUri(seriesId))
            {
                return LoadSeries(seriesId, bins);
            }

            if (double.TryParse(seriesId, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) &&
                double.IsFinite(parsed) &&
                parsed > 0d)
            {
                return CreateConstantSeries(parsed, bins);
            }

            return null;
        }

        if (parallelism is IConvertible)
        {
            var value = Convert.ToDouble(parallelism, CultureInfo.InvariantCulture);
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

    private double[] LoadSeries(string uri, int bins)
    {
        var path = UriResolver.ResolveFilePath(uri, modelDirectory);
        return CsvReader.ReadTimeSeries(path, bins);
    }

    private static bool IsFileUri(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().StartsWith("file:", StringComparison.OrdinalIgnoreCase);
}
