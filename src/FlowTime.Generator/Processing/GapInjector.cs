using FlowTime.Generator.Models;

namespace FlowTime.Generator.Processing;

/// <summary>
/// Handles gap detection / filling for telemetry series.
/// </summary>
public sealed class GapInjector
{
    private readonly GapInjectorOptions options;

    public GapInjector(GapInjectorOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Inspects the series and optionally fills NaN values. Returns the (possibly modified) series plus generated warnings.
    /// </summary>
    public GapInjectionResult Process(string nodeId, TelemetryMetricKind metric, IReadOnlyList<double?> series)
    {
        if (series is null)
        {
            throw new ArgumentNullException(nameof(series));
        }

        var data = new double?[series.Count];
        var warnings = new List<CaptureWarning>();

        for (var i = 0; i < series.Count; i++)
        {
            var value = series[i];

            if (!value.HasValue)
            {
                if (options.MissingValueHandling != GapHandlingMode.Ignore)
                {
                    warnings.Add(new CaptureWarning(
                        Code: "data_gap",
                        Message: $"Missing value at bin {i} for {nodeId}:{metric}.",
                        NodeId: nodeId,
                        Bins: new[] { i }));
                }

                data[i] = options.MissingValueHandling == GapHandlingMode.FillWithZero ? 0d : null;
                continue;
            }

            if (double.IsNaN(value.Value) || double.IsInfinity(value.Value))
            {
                if (options.FillNaNWithZero)
                {
                    data[i] = 0d;
                    warnings.Add(new CaptureWarning(
                        Code: "nan_fill",
                        Message: $"Filled NaN/∞ at bin {i} for {nodeId}:{metric}.",
                        NodeId: nodeId,
                        Bins: new[] { i }));
                }
                else
                {
                    data[i] = value;
                    warnings.Add(new CaptureWarning(
                        Code: "nan_detected",
                        Message: $"Detected NaN/∞ at bin {i} for {nodeId}:{metric}.",
                        NodeId: nodeId,
                        Bins: new[] { i }));
                }
            }
            else
            {
                data[i] = value;
            }
        }

        return new GapInjectionResult(data, warnings);
    }
}

public sealed record GapInjectionResult(
    IReadOnlyList<double?> Series,
    IReadOnlyList<CaptureWarning> Warnings);
