using FlowTime.Core.Execution;
using FlowTime.Core.Models;
using FlowTime.Core.Nodes;

namespace FlowTime.Core.Tests.Nodes;

/// <summary>
/// Tests for AC-1 and AC-5 of m-ec-p3b WIP Limits.
/// Verifies that ServiceWithBufferNode clamps queue depth at the WIP limit
/// and tracks overflow.
/// </summary>
public sealed class WipLimitTests
{
    /// <summary>
    /// AC-5 scenario 1: Queue capped at WIP limit.
    /// Inflow=10/bin, outflow=2/bin, no loss, wipLimit=20.
    /// Without limit: Q[0]=8, Q[1]=16, Q[2]=24, Q[3]=32.
    /// With limit=20: Q[0]=8, Q[1]=16, Q[2]=20, Q[3]=20.
    /// Overflow at t=2: 24-20=4, t=3: 8+20-2=26→20, overflow=6.
    /// </summary>
    [Fact]
    public void WipLimit_ClampQueue_QueueCappedAtLimit()
    {
        var grid = new TimeGrid(4, 1, TimeUnit.Hours);
        var node = new ServiceWithBufferNode(
            "queue", inflow: new NodeId("inflow"), outflow: new NodeId("outflow"),
            loss: null, initialDepth: 0, dispatchSchedule: null,
            wipLimit: 20.0);

        var inflow = new Series(new double[] { 10, 10, 10, 10 });
        var outflow = new Series(new double[] { 2, 2, 2, 2 });

        Series GetInput(NodeId id) => id.Value switch
        {
            "inflow" => inflow,
            "outflow" => outflow,
            _ => throw new InvalidOperationException($"Unknown node: {id}")
        };

        var result = node.Evaluate(grid, GetInput);

        // Q[0] = max(0, 0 + 10 - 2) = 8  (< 20, no clamp)
        // Q[1] = max(0, 8 + 10 - 2) = 16  (< 20, no clamp)
        // Q[2] = max(0, 16 + 10 - 2) = 24 → clamped to 20, overflow = 4
        // Q[3] = max(0, 20 + 10 - 2) = 28 → clamped to 20, overflow = 8
        Assert.Equal(8.0, result[0], precision: 10);
        Assert.Equal(16.0, result[1], precision: 10);
        Assert.Equal(20.0, result[2], precision: 10);
        Assert.Equal(20.0, result[3], precision: 10);
    }

    /// <summary>
    /// AC-5 scenario 1 continued: Overflow series is tracked.
    /// </summary>
    [Fact]
    public void WipLimit_ClampQueue_OverflowTracked()
    {
        var grid = new TimeGrid(4, 1, TimeUnit.Hours);
        var node = new ServiceWithBufferNode(
            "queue", inflow: new NodeId("inflow"), outflow: new NodeId("outflow"),
            loss: null, initialDepth: 0, dispatchSchedule: null,
            wipLimit: 20.0);

        var inflow = new Series(new double[] { 10, 10, 10, 10 });
        var outflow = new Series(new double[] { 2, 2, 2, 2 });

        Series GetInput(NodeId id) => id.Value switch
        {
            "inflow" => inflow,
            "outflow" => outflow,
            _ => throw new InvalidOperationException($"Unknown node: {id}")
        };

        node.Evaluate(grid, GetInput);

        Assert.NotNull(node.LastOverflow);
        Assert.Equal(4, node.LastOverflow!.Length);
        Assert.Equal(0.0, node.LastOverflow[0], precision: 10); // no overflow
        Assert.Equal(0.0, node.LastOverflow[1], precision: 10); // no overflow
        Assert.Equal(4.0, node.LastOverflow[2], precision: 10); // 24 - 20
        Assert.Equal(8.0, node.LastOverflow[3], precision: 10); // 28 - 20
    }

    /// <summary>
    /// AC-5: No WIP limit (default) → queue grows unbounded, no overflow.
    /// </summary>
    [Fact]
    public void NoWipLimit_QueueGrowsUnbounded()
    {
        var grid = new TimeGrid(4, 1, TimeUnit.Hours);
        var node = new ServiceWithBufferNode(
            "queue", inflow: new NodeId("inflow"), outflow: new NodeId("outflow"),
            loss: null, initialDepth: 0, dispatchSchedule: null);

        var inflow = new Series(new double[] { 10, 10, 10, 10 });
        var outflow = new Series(new double[] { 2, 2, 2, 2 });

        Series GetInput(NodeId id) => id.Value switch
        {
            "inflow" => inflow,
            "outflow" => outflow,
            _ => throw new InvalidOperationException($"Unknown node: {id}")
        };

        var result = node.Evaluate(grid, GetInput);

        Assert.Equal(8.0, result[0], precision: 10);
        Assert.Equal(16.0, result[1], precision: 10);
        Assert.Equal(24.0, result[2], precision: 10);
        Assert.Equal(32.0, result[3], precision: 10);
        Assert.Null(node.LastOverflow);
    }

    /// <summary>
    /// AC-5: Overflow to loss (default behavior). When wipOverflow is not
    /// specified, overflow items are simply removed from the system. The
    /// loss series is unaffected — overflow is a separate tracking concern.
    /// </summary>
    [Fact]
    public void WipLimit_OverflowToLoss_Default()
    {
        var grid = new TimeGrid(4, 1, TimeUnit.Hours);
        var node = new ServiceWithBufferNode(
            "queue", inflow: new NodeId("inflow"), outflow: new NodeId("outflow"),
            loss: new NodeId("loss"), initialDepth: 0, dispatchSchedule: null,
            wipLimit: 15.0);

        var inflow = new Series(new double[] { 20, 20, 20, 20 });
        var outflow = new Series(new double[] { 5, 5, 5, 5 });
        var loss = new Series(new double[] { 1, 1, 1, 1 });

        Series GetInput(NodeId id) => id.Value switch
        {
            "inflow" => inflow,
            "outflow" => outflow,
            "loss" => loss,
            _ => throw new InvalidOperationException($"Unknown node: {id}")
        };

        var result = node.Evaluate(grid, GetInput);

        // Q[0] = max(0, 0 + 20 - 5 - 1) = 14 (< 15, no clamp)
        // Q[1] = max(0, 14 + 20 - 5 - 1) = 28 → 15, overflow = 13
        // Q[2] = max(0, 15 + 20 - 5 - 1) = 29 → 15, overflow = 14
        // Q[3] = max(0, 15 + 20 - 5 - 1) = 29 → 15, overflow = 14
        Assert.Equal(14.0, result[0], precision: 10);
        Assert.Equal(15.0, result[1], precision: 10);
        Assert.Equal(15.0, result[2], precision: 10);
        Assert.Equal(15.0, result[3], precision: 10);

        Assert.NotNull(node.LastOverflow);
        Assert.Equal(0.0, node.LastOverflow![0], precision: 10);
        Assert.Equal(13.0, node.LastOverflow[1], precision: 10);
        Assert.Equal(14.0, node.LastOverflow[2], precision: 10);
        Assert.Equal(14.0, node.LastOverflow[3], precision: 10);
    }

    /// <summary>
    /// AC-5: WIP limit of zero → all items overflow immediately.
    /// </summary>
    [Fact]
    public void WipLimit_Zero_AllOverflow()
    {
        var grid = new TimeGrid(3, 1, TimeUnit.Hours);
        var node = new ServiceWithBufferNode(
            "queue", inflow: new NodeId("inflow"), outflow: new NodeId("outflow"),
            loss: null, initialDepth: 0, dispatchSchedule: null,
            wipLimit: 0.0);

        var inflow = new Series(new double[] { 10, 10, 10 });
        var outflow = new Series(new double[] { 5, 5, 5 });

        Series GetInput(NodeId id) => id.Value switch
        {
            "inflow" => inflow,
            "outflow" => outflow,
            _ => throw new InvalidOperationException($"Unknown node: {id}")
        };

        var result = node.Evaluate(grid, GetInput);

        Assert.Equal(0.0, result[0], precision: 10);
        Assert.Equal(0.0, result[1], precision: 10);
        Assert.Equal(0.0, result[2], precision: 10);

        Assert.NotNull(node.LastOverflow);
        Assert.Equal(5.0, node.LastOverflow![0], precision: 10); // 10-5=5, all overflow
        Assert.Equal(5.0, node.LastOverflow[1], precision: 10);
        Assert.Equal(5.0, node.LastOverflow[2], precision: 10);
    }
}
