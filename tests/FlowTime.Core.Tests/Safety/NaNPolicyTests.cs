using FlowTime.Core.Execution;
using FlowTime.Core.Expressions;
using FlowTime.Core.Metrics;
using FlowTime.Core.Models;
using FlowTime.Core.Nodes;
using FlowTime.Expressions;

namespace FlowTime.Core.Tests.Safety;

/// <summary>
/// Tests verifying the engine's three-tier NaN/Infinity/division-by-zero policy.
/// See docs/architecture/nan-policy.md for the formal policy.
/// </summary>
public class NaNPolicyTests
{
    private readonly TimeGrid grid = new(bins: 4, binSize: 60, binUnit: TimeUnit.Minutes);

    // Tier 1: Return 0.0

    [Fact]
    public void Tier1_ExpressionDivision_ZeroDivisor_ReturnsZero()
    {
        var result = EvalExpression("a / b",
            ("a", new double[] { 10, 20, 30, 40 }),
            ("b", new double[] { 2, 0, 5, 0 }));

        Assert.Equal(new double[] { 5, 0, 6, 0 }, result);
    }

    [Fact]
    public void Tier1_ExpressionDivision_NaNDivisor_ReturnsNaN()
    {
        var result = EvalExpression("a / b",
            ("a", new double[] { 10, 20 }),
            ("b", new double[] { 2, double.NaN }));

        Assert.Equal(5.0, result[0]);
        Assert.True(double.IsNaN(result[1]));
    }

    [Fact]
    public void Tier1_ExpressionDivision_InfinityDivisor_ReturnsZero()
    {
        var result = EvalExpression("a / b",
            ("a", new double[] { 10 }),
            ("b", new double[] { double.PositiveInfinity }));

        Assert.Equal(0.0, result[0]);
    }

    [Fact]
    public void Tier1_ExpressionMod_ZeroDivisor_ReturnsZero()
    {
        var result = EvalExpression("MOD(a, b)",
            ("a", new double[] { 10, 20, 30, 40 }),
            ("b", new double[] { 3, 0, 7, 0 }));

        Assert.Equal(1.0, result[0]);
        Assert.Equal(0.0, result[1]);
        Assert.Equal(2.0, result[2]);
        Assert.Equal(0.0, result[3]);
    }

    [Fact]
    public void Tier1_Safe_NaN_ReturnsZero()
    {
        var nanInflow = new ConstSeriesNode("inflow", new double[] { double.NaN, 5, double.NaN, 3 });
        var outflow = new ConstSeriesNode("outflow", new double[] { 0, 0, 0, 0 });
        var swb = new ServiceWithBufferNode("queue", new NodeId("inflow"), new NodeId("outflow"),
            loss: null, initialDepth: 0, dispatchSchedule: null);
        var graph = new Graph(new INode[] { nanInflow, outflow, swb });

        var results = graph.Evaluate(grid);
        var queue = results[new NodeId("queue")].ToArray();

        Assert.Equal(0.0, queue[0]);
        Assert.Equal(5.0, queue[1]);
        Assert.Equal(5.0, queue[2]);
        Assert.Equal(8.0, queue[3]);
    }

    [Fact]
    public void Tier1_Safe_Infinity_ReturnsZero()
    {
        var infInflow = new ConstSeriesNode("inflow", new double[] { double.PositiveInfinity, 5, double.NegativeInfinity, 3 });
        var outflow = new ConstSeriesNode("outflow", new double[] { 0, 0, 0, 0 });
        var swb = new ServiceWithBufferNode("queue", new NodeId("inflow"), new NodeId("outflow"),
            loss: null, initialDepth: 0, dispatchSchedule: null);
        var graph = new Graph(new INode[] { infInflow, outflow, swb });

        var results = graph.Evaluate(grid);
        var queue = results[new NodeId("queue")].ToArray();

        Assert.Equal(0.0, queue[0]);
        Assert.Equal(5.0, queue[1]);
        Assert.Equal(5.0, queue[2]);
        Assert.Equal(8.0, queue[3]);
    }

    // Tier 2: Return null

    [Fact]
    public void Tier2_Utilization_ZeroCapacity_ReturnsNull()
    {
        Assert.Null(UtilizationComputer.Calculate(served: 10, capacity: 0));
    }

    [Fact]
    public void Tier2_Utilization_NegativeCapacity_ReturnsNull()
    {
        Assert.Null(UtilizationComputer.Calculate(served: 10, capacity: -5));
    }

    [Fact]
    public void Tier2_Utilization_NullCapacity_ReturnsNull()
    {
        Assert.Null(UtilizationComputer.Calculate(served: 10, capacity: null));
    }

    [Fact]
    public void Tier2_Utilization_PositiveCapacity_ReturnsValue()
    {
        Assert.Equal(0.5, UtilizationComputer.Calculate(served: 5, capacity: 10));
    }

    [Fact]
    public void Tier2_Latency_ZeroServed_ReturnsNull()
    {
        Assert.Null(ComputeLatencyMinutes(queue: 10, served: 0, binMinutes: 60));
    }

    [Fact]
    public void Tier2_Latency_NegativeServed_ReturnsNull()
    {
        Assert.Null(ComputeLatencyMinutes(queue: 10, served: -1, binMinutes: 60));
    }

    [Fact]
    public void Tier2_Latency_ZeroBinMinutes_ReturnsNull()
    {
        Assert.Null(ComputeLatencyMinutes(queue: 10, served: 5, binMinutes: 0));
    }

    [Fact]
    public void Tier2_Latency_ValidInputs_ReturnsValue()
    {
        Assert.Equal(120.0, ComputeLatencyMinutes(queue: 10, served: 5, binMinutes: 60));
    }

    [Fact]
    public void Tier2_CycleTime_QueueTime_ZeroServed_ReturnsNull()
    {
        Assert.Null(CycleTimeComputer.CalculateQueueTime(queueDepth: 10, served: 0, binMs: 60000));
    }

    [Fact]
    public void Tier2_CycleTime_QueueTime_NegativeServed_ReturnsNull()
    {
        Assert.Null(CycleTimeComputer.CalculateQueueTime(queueDepth: 10, served: -1, binMs: 60000));
    }

    [Fact]
    public void Tier2_CycleTime_ServiceTime_NullInputs_ReturnsNull()
    {
        Assert.Null(CycleTimeComputer.CalculateServiceTime(processingTimeMsSum: null, servedCount: null));
    }

    [Fact]
    public void Tier2_CycleTime_ServiceTime_ZeroServedCount_ReturnsNull()
    {
        Assert.Null(CycleTimeComputer.CalculateServiceTime(processingTimeMsSum: 500, servedCount: 0));
    }

    [Fact]
    public void Tier2_CycleTime_CycleTime_NullQueueTime_ReturnsServiceTime()
    {
        // Pure service node: queue time null, service time available → returns service time
        Assert.Equal(50.0, CycleTimeComputer.CalculateCycleTime(queueTimeMs: null, serviceTimeMs: 50));
    }

    [Fact]
    public void Tier2_FlowEfficiency_NullServiceTime_ReturnsNull()
    {
        Assert.Null(CycleTimeComputer.CalculateFlowEfficiency(serviceTimeMs: null, cycleTimeMs: 100));
    }

    [Fact]
    public void Tier2_FlowEfficiency_ZeroCycleTime_ReturnsNull()
    {
        Assert.Null(CycleTimeComputer.CalculateFlowEfficiency(serviceTimeMs: 50, cycleTimeMs: 0));
    }

    [Fact]
    public void Tier2_FlowEfficiency_ValidInputs_ReturnsValue()
    {
        Assert.Equal(0.5, CycleTimeComputer.CalculateFlowEfficiency(serviceTimeMs: 50, cycleTimeMs: 100));
    }

    // Exception: Invalid input

    [Fact]
    public void Exception_Pmf_ZeroSumProbabilities_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new FlowTime.Core.Pmf.Pmf(new[] { 1.0, 2.0, 3.0 }, new[] { 0.0, 0.0, 0.0 }));
    }

    // NaN inputs to expression functions

    [Fact]
    public void NaN_Floor_NaNInput_ReturnsNaN()
    {
        var result = EvalExpression("FLOOR(x)",
            ("x", new double[] { 1.5, double.NaN, 3.7, double.NaN }));

        Assert.Equal(1.0, result[0]);
        Assert.True(double.IsNaN(result[1]));
        Assert.Equal(3.0, result[2]);
        Assert.True(double.IsNaN(result[3]));
    }

    [Fact]
    public void NaN_Ceil_NaNInput_ReturnsNaN()
    {
        var result = EvalExpression("CEIL(x)",
            ("x", new double[] { 1.5, double.NaN }));

        Assert.Equal(2.0, result[0]);
        Assert.True(double.IsNaN(result[1]));
    }

    [Fact]
    public void NaN_Round_NaNInput_ReturnsNaN()
    {
        var result = EvalExpression("ROUND(x)",
            ("x", new double[] { 1.5, double.NaN }));

        Assert.Equal(2.0, result[0]);
        Assert.True(double.IsNaN(result[1]));
    }

    [Fact]
    public void NaN_Step_NaNValue_ReturnsZero()
    {
        var result = EvalExpression("STEP(x, 0.5)",
            ("x", new double[] { 1.0, double.NaN, 0.3, double.NaN }));

        Assert.Equal(1.0, result[0]);
        Assert.Equal(0.0, result[1]);
        Assert.Equal(0.0, result[2]);
        Assert.Equal(0.0, result[3]);
    }

    [Fact]
    public void NaN_Mod_NaNValue_ReturnsNaN()
    {
        var result = EvalExpression("MOD(x, 3)",
            ("x", new double[] { 10, double.NaN }));

        Assert.Equal(1.0, result[0]);
        Assert.True(double.IsNaN(result[1]));
    }

    // Helper

    private double[] EvalExpression(string expr, params (string name, double[] values)[] inputs)
    {
        var grid = new TimeGrid(inputs[0].values.Length, 60, TimeUnit.Minutes);
        var parser = new ExpressionParser(expr);
        var ast = parser.Parse();
        var exprNode = ExpressionCompiler.Compile(ast, "result");

        Series GetInput(NodeId id)
        {
            foreach (var (name, values) in inputs)
            {
                if (id.Value == name) return new Series(values);
            }
            throw new KeyNotFoundException($"Unknown input: {id.Value}");
        }

        return exprNode.Evaluate(grid, GetInput).ToArray();
    }

    private static double? ComputeLatencyMinutes(double queue, double served, double binMinutes)
    {
        var result = RuntimeAnalyticalEvaluator.ComputeBin(
            new RuntimeAnalyticalDescriptor
            {
                Identity = RuntimeAnalyticalIdentity.Queue,
                Category = RuntimeAnalyticalNodeCategory.Queue,
                HasQueueSemantics = true,
                HasServiceSemantics = false,
                HasCycleTimeDecomposition = false,
                StationarityWarningApplicable = true
            },
            queueDepth: queue,
            served: served,
            processingTimeMsSum: null,
            servedCount: null,
            binMs: binMinutes * 60_000);

        return result.LatencyMinutes;
    }
}
