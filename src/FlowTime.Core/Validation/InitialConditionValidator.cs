using System;
using FlowTime.Core.Models;

namespace FlowTime.Core.Validation;

public sealed class InitialConditionValidator
{
    private const double DefaultTolerance = 0.01d;

    public void Validate(NodeData nodeData, InitialCondition? initialCondition, double tolerance = DefaultTolerance)
    {
        ArgumentNullException.ThrowIfNull(nodeData);
        if (nodeData.Arrivals.Length == 0 || nodeData.Served.Length == 0 || nodeData.Errors.Length == 0)
            throw new ArgumentException("Node data must contain arrivals, served, and errors with at least one bin.", nameof(nodeData));
        if (tolerance < 0)
            throw new ArgumentOutOfRangeException(nameof(tolerance), tolerance, "tolerance must be non-negative");

        var expectedQueueDepth = (initialCondition?.QueueDepth ?? 0d)
            + nodeData.Arrivals[0]
            - nodeData.Served[0]
            - nodeData.Errors[0];

        if (nodeData.QueueDepth is null)
            return;

        var actual = nodeData.QueueDepth[0];
        if (Math.Abs(actual - expectedQueueDepth) > tolerance)
        {
            throw new InvalidOperationException($"Initial condition violation for node {nodeData.NodeId}: expected queueDepth[0] = {expectedQueueDepth:F2}, actual = {actual:F2}");
        }
    }
}
