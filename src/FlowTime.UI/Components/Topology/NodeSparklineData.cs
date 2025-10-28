using System;
using System.Collections.Generic;
using System.Linq;

namespace FlowTime.UI.Components.Topology;

public sealed record NodeSparklineData(
    IReadOnlyList<double?> Values,
    double Min,
    double Max,
    string Metric,
    bool IsFlat)
{
    public static NodeSparklineData? Create(
        IReadOnlyList<double?> sourceValues,
        string metric,
        double? explicitMin = null,
        double? explicitMax = null)
    {
        if (sourceValues is null || sourceValues.Count == 0)
        {
            return null;
        }

        var cloned = sourceValues.ToArray();

        double min = explicitMin ?? double.PositiveInfinity;
        double max = explicitMax ?? double.NegativeInfinity;

        if (explicitMin is null || explicitMax is null)
        {
            foreach (var value in cloned)
            {
                if (!value.HasValue)
                {
                    continue;
                }

                if (value.Value < min)
                {
                    min = value.Value;
                }

                if (value.Value > max)
                {
                    max = value.Value;
                }
            }
        }

        if (!cloned.Any(v => v.HasValue))
        {
            return null;
        }

        var finiteMin = double.IsFinite(min) ? min : 0d;
        var finiteMax = double.IsFinite(max) ? max : finiteMin;
        var isFlat = Math.Abs(finiteMax - finiteMin) < 1e-9;

        return new NodeSparklineData(cloned, finiteMin, finiteMax, metric, isFlat);
    }
}
