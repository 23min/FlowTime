using FlowTime.Core.Compiler;
using FlowTime.Core.Execution;
using FlowTime.Core.Models;
using FlowTime.Core.Nodes;

namespace FlowTime.Core.Tests.Nodes;

/// <summary>
/// AC-4 + AC-5: SHIFT-based backpressure tests.
/// Tests both signal-driven throttling (no cycle) and true cross-node
/// feedback (cycle broken by SHIFT lag, evaluated bin-by-bin).
/// </summary>
public sealed class BackpressureShiftTests
{
    /// <summary>
    /// AC-4: SHIFT-based throttle reduces upstream effective arrivals
    /// based on a lagged capacity signal.
    ///
    /// Model (no cycle — linear dependency chain):
    ///   capacity_signal = const [1, 1, 0.5, 0.2, 0.2, 0.2]
    ///   effective_arrivals = raw_arrivals * SHIFT(capacity_signal, 1)
    ///   queue_depth = serviceWithBuffer(effective_arrivals, served)
    ///
    /// SHIFT(capacity_signal, 1) reads t-1's capacity signal.
    /// At t=0: no history → SHIFT returns 0 → effective=0 (ramp-up lag)
    /// At t=1: reads t=0 signal=1 → effective=100
    /// At t=2: reads t=1 signal=1 → effective=100
    /// At t=3: reads t=2 signal=0.5 → effective=50
    /// This demonstrates how a downstream pressure signal throttles upstream.
    /// </summary>
    [Fact]
    public void Backpressure_ShiftThrottlesUpstream_EffectiveArrivalsRespond()
    {
        var model = new ModelDefinition
        {
            Grid = new GridDefinition { Bins = 6, BinSize = 1, BinUnit = "hours" },
            Nodes =
            {
                new NodeDefinition { Id = "raw_arrivals", Kind = "const",
                    Values = new[] { 100.0, 100.0, 100.0, 100.0, 100.0, 100.0 } },
                new NodeDefinition { Id = "served_capacity", Kind = "const",
                    Values = new[] { 20.0, 20.0, 20.0, 20.0, 20.0, 20.0 } },
                // Simulates a downstream pressure signal (e.g., from a monitoring system)
                // 1.0 = healthy, 0.5 = degraded, 0.2 = overloaded
                new NodeDefinition { Id = "capacity_signal", Kind = "const",
                    Values = new[] { 1.0, 1.0, 0.5, 0.2, 0.2, 0.2 } },
                // Upstream reads the PREVIOUS bin's signal — one-bin propagation delay
                new NodeDefinition { Id = "effective_arrivals", Kind = "expr",
                    Expr = "raw_arrivals * SHIFT(capacity_signal, 1)" },
                new NodeDefinition { Id = "downstream_served", Kind = "expr",
                    Expr = "served_capacity" },
            },
            Topology = new TopologyDefinition
            {
                Nodes =
                {
                    new TopologyNodeDefinition
                    {
                        Id = "Downstream",
                        Kind = "serviceWithBuffer",
                        Semantics = new TopologyNodeSemanticsDefinition
                        {
                            Arrivals = "effective_arrivals",
                            Served = "downstream_served",
                            QueueDepth = "queue_depth"
                        }
                    }
                }
            }
        };

        var compiled = ModelCompiler.Compile(model);
        var (grid, graph) = ModelParser.ParseModel(compiled);
        var result = graph.Evaluate(grid);

        var effective = result[new NodeId("effective_arrivals")].ToArray();
        var queue = result[new NodeId("queue_depth")].ToArray();

        // t=0: SHIFT(capacity_signal, 1) = 0 (no prior bin), effective = 100*0 = 0
        Assert.Equal(0.0, effective[0], precision: 10);
        Assert.Equal(0.0, queue[0], precision: 10); // Q = 0 - 20 → max(0, -20) = 0

        // t=1: SHIFT reads t=0 signal=1.0, effective = 100
        Assert.Equal(100.0, effective[1], precision: 10);
        Assert.Equal(80.0, queue[1], precision: 10); // Q = 0 + 100 - 20 = 80

        // t=2: SHIFT reads t=1 signal=1.0, effective = 100
        Assert.Equal(100.0, effective[2], precision: 10);

        // t=3: SHIFT reads t=2 signal=0.5, effective = 50 (throttled!)
        Assert.Equal(50.0, effective[3], precision: 10);

        // t=4: SHIFT reads t=3 signal=0.2, effective = 20
        Assert.Equal(20.0, effective[4], precision: 10);

        // t=5: SHIFT reads t=4 signal=0.2, effective = 20
        Assert.Equal(20.0, effective[5], precision: 10);

        // Key assertion: arrivals are NOT constant — they respond to the signal
        Assert.True(effective[1] > effective[3],
            "Throttle should reduce effective arrivals when signal drops");
    }

    /// <summary>
    /// AC-4 + AC-5: WIP limit combined with SHIFT-based backpressure.
    /// The WIP limit catches any burst that the one-bin-lagged signal
    /// doesn't prevent, while SHIFT throttling provides steady-state control.
    /// </summary>
    [Fact]
    public void WipLimit_PlusShiftBackpressure_QueueNeverExceedsLimit()
    {
        var model = new ModelDefinition
        {
            Grid = new GridDefinition { Bins = 6, BinSize = 1, BinUnit = "hours" },
            Nodes =
            {
                new NodeDefinition { Id = "raw_arrivals", Kind = "const",
                    Values = new[] { 100.0, 100.0, 100.0, 100.0, 100.0, 100.0 } },
                new NodeDefinition { Id = "served_capacity", Kind = "const",
                    Values = new[] { 10.0, 10.0, 10.0, 10.0, 10.0, 10.0 } },
                // Pressure signal degrades over time
                new NodeDefinition { Id = "pressure_signal", Kind = "const",
                    Values = new[] { 1.0, 0.5, 0.3, 0.2, 0.1, 0.1 } },
                new NodeDefinition { Id = "effective_arrivals", Kind = "expr",
                    Expr = "raw_arrivals * SHIFT(pressure_signal, 1)" },
                new NodeDefinition { Id = "served", Kind = "expr",
                    Expr = "served_capacity" },
            },
            Topology = new TopologyDefinition
            {
                Nodes =
                {
                    new TopologyNodeDefinition
                    {
                        Id = "Queue",
                        Kind = "serviceWithBuffer",
                        WipLimit = 50.0,
                        Semantics = new TopologyNodeSemanticsDefinition
                        {
                            Arrivals = "effective_arrivals",
                            Served = "served",
                            QueueDepth = "queue_depth"
                        }
                    }
                }
            }
        };

        var compiled = ModelCompiler.Compile(model);
        var (grid, graph) = ModelParser.ParseModel(compiled);
        var result = graph.Evaluate(grid);

        var queue = result[new NodeId("queue_depth")].ToArray();

        // Queue should never exceed wipLimit of 50
        for (int t = 0; t < queue.Length; t++)
        {
            Assert.True(queue[t] <= 50.0, $"Queue[{t}]={queue[t]:F2} exceeds wipLimit=50");
        }

        // Effective arrivals should decrease as signal drops
        var effective = result[new NodeId("effective_arrivals")].ToArray();
        Assert.True(effective[4] < effective[2],
            "Later bins should have lower effective arrivals due to decreasing pressure signal");
    }

    // ──────────────────────────────────────────────────────────────
    // Cross-node feedback: true backpressure via SHIFT
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// AC-4: True cross-node backpressure feedback.
    ///
    /// Cycle (broken by SHIFT lag):
    ///   effective_arrivals = raw_arrivals * (1 - SHIFT(pressure, 1))
    ///   queue_depth = serviceWithBuffer(effective_arrivals, served)
    ///   pressure = CLAMP(queue_depth / 50, 0, 1)
    ///
    /// Same-bin chain: effective_arrivals → queue_depth → pressure
    /// Lagged back-edge: pressure →(SHIFT lag=1)→ effective_arrivals
    ///
    /// The feedback subgraph evaluates bin-by-bin. At each bin t,
    /// SHIFT(pressure, 1) reads pressure[t-1] which was already computed.
    ///
    /// t=0: no prior pressure → SHIFT=0 → effective=100, Q=80, pressure=1.0
    /// t=1: SHIFT reads pressure[0]=1.0 → effective=100*(1-1)=0, Q=60, pressure=1.0
    /// t=2: SHIFT reads pressure[1]=1.0 → effective=0, Q=40, pressure=0.8
    /// t=3: SHIFT reads pressure[2]=0.8 → effective=20, Q=40, pressure=0.8
    /// t=4+: stabilizes around Q≈40, effective≈20
    /// </summary>
    [Fact]
    public void Backpressure_CrossNodeFeedback_QueueStabilizes()
    {
        var model = new ModelDefinition
        {
            Grid = new GridDefinition { Bins = 8, BinSize = 1, BinUnit = "hours" },
            Nodes =
            {
                new NodeDefinition { Id = "raw_arrivals", Kind = "const",
                    Values = new[] { 100.0, 100.0, 100.0, 100.0, 100.0, 100.0, 100.0, 100.0 } },
                new NodeDefinition { Id = "served_capacity", Kind = "const",
                    Values = new[] { 20.0, 20.0, 20.0, 20.0, 20.0, 20.0, 20.0, 20.0 } },
                // Upstream throttles based on PREVIOUS bin's pressure (feedback)
                new NodeDefinition { Id = "effective_arrivals", Kind = "expr",
                    Expr = "raw_arrivals * (1 - SHIFT(pressure, 1))" },
                new NodeDefinition { Id = "downstream_served", Kind = "expr",
                    Expr = "served_capacity" },
                // Pressure signal derived from queue state (0..1)
                new NodeDefinition { Id = "pressure", Kind = "expr",
                    Expr = "CLAMP(queue_depth / 50, 0, 1)" },
            },
            Topology = new TopologyDefinition
            {
                Nodes =
                {
                    new TopologyNodeDefinition
                    {
                        Id = "Downstream",
                        Kind = "serviceWithBuffer",
                        Semantics = new TopologyNodeSemanticsDefinition
                        {
                            Arrivals = "effective_arrivals",
                            Served = "downstream_served",
                            QueueDepth = "queue_depth"
                        },
                        InitialCondition = new InitialConditionDefinition { QueueDepth = 0 }
                    }
                }
            }
        };

        var compiled = ModelCompiler.Compile(model);
        var (grid, graph) = ModelParser.ParseModel(compiled);
        var result = graph.Evaluate(grid);

        var queue = result[new NodeId("queue_depth")].ToArray();
        var effective = result[new NodeId("effective_arrivals")].ToArray();
        var pressure = result[new NodeId("pressure")].ToArray();

        // t=0: SHIFT(pressure, 1) = 0 → effective = 100, Q = 80, pressure = 1.0 (clamped)
        Assert.Equal(100.0, effective[0], precision: 10);
        Assert.Equal(80.0, queue[0], precision: 10);
        Assert.Equal(1.0, pressure[0], precision: 10);

        // t=1: SHIFT reads pressure[0]=1.0 → effective = 0, Q = 60
        Assert.Equal(0.0, effective[1], precision: 10);
        Assert.Equal(60.0, queue[1], precision: 10);

        // t=2: SHIFT reads pressure[1]=1.0 → effective = 0, Q = 40
        Assert.Equal(0.0, effective[2], precision: 10);
        Assert.Equal(40.0, queue[2], precision: 10);

        // t=3: pressure[2] = CLAMP(40/50, 0, 1) = 0.8 → effective = 100*(1-0.8) = 20
        // Q = 40 + 20 - 20 = 40
        Assert.Equal(20.0, effective[3], precision: 10);
        Assert.Equal(40.0, queue[3], precision: 10);

        // t=4: pressure[3] = 0.8 → effective = 20, Q = 40 (stabilized)
        Assert.Equal(20.0, effective[4], precision: 10);
        Assert.Equal(40.0, queue[4], precision: 10);

        // Queue stabilizes — the feedback loop found its equilibrium
        Assert.Equal(queue[4], queue[5], precision: 10);
        Assert.Equal(queue[5], queue[6], precision: 10);
    }
}
