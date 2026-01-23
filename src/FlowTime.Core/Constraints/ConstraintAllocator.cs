using System;
using System.Collections.Generic;

namespace FlowTime.Core.Constraints;

public static class ConstraintAllocator
{
    public static IReadOnlyDictionary<string, double> AllocateProportional(
        IReadOnlyDictionary<string, double> demandByNode,
        double capacity)
    {
        ArgumentNullException.ThrowIfNull(demandByNode);

        var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        if (demandByNode.Count == 0 || capacity <= 0d)
        {
            foreach (var key in demandByNode.Keys)
            {
                result[key] = 0d;
            }

            return result;
        }

        var totalDemand = 0d;
        foreach (var demand in demandByNode.Values)
        {
            if (demand > 0d && double.IsFinite(demand))
            {
                totalDemand += demand;
            }
        }

        if (totalDemand <= 0d)
        {
            foreach (var key in demandByNode.Keys)
            {
                result[key] = 0d;
            }

            return result;
        }

        if (totalDemand <= capacity)
        {
            foreach (var (key, demand) in demandByNode)
            {
                result[key] = demand > 0d && double.IsFinite(demand) ? demand : 0d;
            }

            return result;
        }

        foreach (var (key, demand) in demandByNode)
        {
            if (demand <= 0d || !double.IsFinite(demand))
            {
                result[key] = 0d;
                continue;
            }

            result[key] = capacity * (demand / totalDemand);
        }

        return result;
    }
}
