using FlowTime.Core.Compiler;
using FlowTime.Core.Execution;
using FlowTime.Core.Models;
using FlowTime.Core.Nodes;

namespace FlowTime.Core.Tests.Nodes;

/// <summary>
/// Tests for m-ec-p3b WIP Limits (AC-1, AC-2, AC-5).
/// Verifies WIP clamping, overflow tracking, and overflow routing.
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
    /// AC-1 + AC-3: WIP limit defined on topology node flows through ModelCompiler
    /// and ModelParser into the evaluated graph. End-to-end pipeline test.
    /// </summary>
    [Fact]
    public void WipLimit_EndToEnd_TopologyThroughCompiler()
    {
        var model = new ModelDefinition
        {
            Grid = new GridDefinition { Bins = 4, BinSize = 1, BinUnit = "hours" },
            Nodes =
            {
                new NodeDefinition { Id = "arrivals", Kind = "const", Values = new[] { 10.0, 10.0, 10.0, 10.0 } },
                new NodeDefinition { Id = "capacity", Kind = "const", Values = new[] { 2.0, 2.0, 2.0, 2.0 } },
                new NodeDefinition { Id = "served", Kind = "expr", Expr = "capacity" },
                new NodeDefinition { Id = "errors", Kind = "const", Values = new[] { 0.0, 0.0, 0.0, 0.0 } },
            },
            Topology = new TopologyDefinition
            {
                Nodes =
                {
                    new TopologyNodeDefinition
                    {
                        Id = "Queue",
                        Kind = "serviceWithBuffer",
                        WipLimit = 15.0,
                        Semantics = new TopologyNodeSemanticsDefinition
                        {
                            Arrivals = "arrivals",
                            Served = "served",
                            Errors = "errors",
                            QueueDepth = "queue_depth"
                        }
                    }
                }
            }
        };

        var compiled = ModelCompiler.Compile(model);
        var (grid, graph) = ModelParser.ParseModel(compiled);
        var result = graph.Evaluate(grid);

        var queueId = new NodeId("queue_depth");
        Assert.True(result.ContainsKey(queueId));

        var queue = result[queueId].ToArray();
        // inflow=10, outflow=2, net=8 per bin
        // Q[0]=8, Q[1]=16→capped at 15, Q[2]=15+8=23→15, Q[3]=15+8=23→15
        Assert.Equal(8.0, queue[0], precision: 10);
        Assert.Equal(15.0, queue[1], precision: 10);
        Assert.Equal(15.0, queue[2], precision: 10);
        Assert.Equal(15.0, queue[3], precision: 10);
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

    // ──────────────────────────────────────────────────────────────
    // AC-2: WIP overflow routing
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// AC-2 + AC-5: Overflow routed to a DLQ node (target nodeId).
    /// Main queue has wipLimit=10, wipOverflow="DLQ". Overflow items
    /// become additional inflow to the DLQ queue.
    /// </summary>
    [Fact]
    public void WipOverflow_ToDlqNode_OverflowBecomesTargetInflow()
    {
        var model = new ModelDefinition
        {
            Grid = new GridDefinition { Bins = 3, BinSize = 1, BinUnit = "hours" },
            Nodes =
            {
                new NodeDefinition { Id = "main_arrivals", Kind = "const", Values = new[] { 15.0, 15.0, 15.0 } },
                new NodeDefinition { Id = "main_capacity", Kind = "const", Values = new[] { 5.0, 5.0, 5.0 } },
                new NodeDefinition { Id = "main_served", Kind = "expr", Expr = "main_capacity" },
                new NodeDefinition { Id = "main_errors", Kind = "const", Values = new[] { 0.0, 0.0, 0.0 } },
                new NodeDefinition { Id = "dlq_arrivals", Kind = "const", Values = new[] { 0.0, 0.0, 0.0 } },
                new NodeDefinition { Id = "dlq_capacity", Kind = "const", Values = new[] { 0.0, 0.0, 0.0 } },
                new NodeDefinition { Id = "dlq_served", Kind = "expr", Expr = "dlq_capacity" },
                new NodeDefinition { Id = "dlq_errors", Kind = "const", Values = new[] { 0.0, 0.0, 0.0 } },
            },
            Topology = new TopologyDefinition
            {
                Nodes =
                {
                    new TopologyNodeDefinition
                    {
                        Id = "Main",
                        Kind = "serviceWithBuffer",
                        WipLimit = 10.0,
                        WipOverflow = "DLQ",
                        Semantics = new TopologyNodeSemanticsDefinition
                        {
                            Arrivals = "main_arrivals",
                            Served = "main_served",
                            Errors = "main_errors",
                            QueueDepth = "main_queue"
                        }
                    },
                    new TopologyNodeDefinition
                    {
                        Id = "DLQ",
                        Kind = "serviceWithBuffer",
                        Semantics = new TopologyNodeSemanticsDefinition
                        {
                            Arrivals = "dlq_arrivals",
                            Served = "dlq_served",
                            Errors = "dlq_errors",
                            QueueDepth = "dlq_queue"
                        }
                    }
                }
            }
        };

        var compiled = ModelCompiler.Compile(model);
        var (grid, graph) = ModelParser.ParseModel(compiled);
        var result = WipOverflowEvaluator.Evaluate(graph, grid);

        var mainQueue = result[new NodeId("main_queue")].ToArray();
        var dlqQueue = result[new NodeId("dlq_queue")].ToArray();

        // Main: inflow=15, outflow=5, wipLimit=10
        // Q[0]=10 (at limit), overflow=0
        // Q[1]=10+15-5=20 → clamped to 10, overflow=10
        // Q[2]=10+15-5=20 → clamped to 10, overflow=10
        Assert.Equal(10.0, mainQueue[0], precision: 10);
        Assert.Equal(10.0, mainQueue[1], precision: 10);
        Assert.Equal(10.0, mainQueue[2], precision: 10);

        // DLQ receives overflow as inflow: [0, 10, 10], outflow=0
        // Q[0]=0, Q[1]=10, Q[2]=20
        Assert.Equal(0.0, dlqQueue[0], precision: 10);
        Assert.Equal(10.0, dlqQueue[1], precision: 10);
        Assert.Equal(20.0, dlqQueue[2], precision: 10);
    }

    /// <summary>
    /// AC-2 + AC-5: Cascading overflow. A→B→C.
    /// A overflows to B (wipLimit=5), B overflows to C (no limit).
    /// </summary>
    [Fact]
    public void WipOverflow_Cascading_OverflowChainsToFinalTarget()
    {
        var model = new ModelDefinition
        {
            Grid = new GridDefinition { Bins = 3, BinSize = 1, BinUnit = "hours" },
            Nodes =
            {
                new NodeDefinition { Id = "a_arrivals", Kind = "const", Values = new[] { 20.0, 20.0, 20.0 } },
                new NodeDefinition { Id = "a_capacity", Kind = "const", Values = new[] { 5.0, 5.0, 5.0 } },
                new NodeDefinition { Id = "a_served", Kind = "expr", Expr = "a_capacity" },
                new NodeDefinition { Id = "a_errors", Kind = "const", Values = new[] { 0.0, 0.0, 0.0 } },
                new NodeDefinition { Id = "b_arrivals", Kind = "const", Values = new[] { 0.0, 0.0, 0.0 } },
                new NodeDefinition { Id = "b_capacity", Kind = "const", Values = new[] { 0.0, 0.0, 0.0 } },
                new NodeDefinition { Id = "b_served", Kind = "expr", Expr = "b_capacity" },
                new NodeDefinition { Id = "b_errors", Kind = "const", Values = new[] { 0.0, 0.0, 0.0 } },
                new NodeDefinition { Id = "c_arrivals", Kind = "const", Values = new[] { 0.0, 0.0, 0.0 } },
                new NodeDefinition { Id = "c_capacity", Kind = "const", Values = new[] { 0.0, 0.0, 0.0 } },
                new NodeDefinition { Id = "c_served", Kind = "expr", Expr = "c_capacity" },
                new NodeDefinition { Id = "c_errors", Kind = "const", Values = new[] { 0.0, 0.0, 0.0 } },
            },
            Topology = new TopologyDefinition
            {
                Nodes =
                {
                    new TopologyNodeDefinition
                    {
                        Id = "NodeA",
                        Kind = "serviceWithBuffer",
                        WipLimit = 10.0,
                        WipOverflow = "NodeB",
                        Semantics = new TopologyNodeSemanticsDefinition
                        {
                            Arrivals = "a_arrivals",
                            Served = "a_served",
                            Errors = "a_errors",
                            QueueDepth = "a_queue"
                        }
                    },
                    new TopologyNodeDefinition
                    {
                        Id = "NodeB",
                        Kind = "serviceWithBuffer",
                        WipLimit = 5.0,
                        WipOverflow = "NodeC",
                        Semantics = new TopologyNodeSemanticsDefinition
                        {
                            Arrivals = "b_arrivals",
                            Served = "b_served",
                            Errors = "b_errors",
                            QueueDepth = "b_queue"
                        }
                    },
                    new TopologyNodeDefinition
                    {
                        Id = "NodeC",
                        Kind = "serviceWithBuffer",
                        Semantics = new TopologyNodeSemanticsDefinition
                        {
                            Arrivals = "c_arrivals",
                            Served = "c_served",
                            Errors = "c_errors",
                            QueueDepth = "c_queue"
                        }
                    }
                }
            }
        };

        var compiled = ModelCompiler.Compile(model);
        var (grid, graph) = ModelParser.ParseModel(compiled);
        var result = WipOverflowEvaluator.Evaluate(graph, grid);

        var aQueue = result[new NodeId("a_queue")].ToArray();
        var bQueue = result[new NodeId("b_queue")].ToArray();
        var cQueue = result[new NodeId("c_queue")].ToArray();

        // A: inflow=20, outflow=5, wipLimit=10
        // Q[0]=15→10, overflow=5; Q[1]=10+15=25→10, overflow=15; Q[2]=25→10, overflow=15
        Assert.Equal(10.0, aQueue[0], precision: 10);
        Assert.Equal(10.0, aQueue[1], precision: 10);
        Assert.Equal(10.0, aQueue[2], precision: 10);

        // B: receives A overflow [5,15,15] as inflow, outflow=0, wipLimit=5
        // Q[0]=5 (at limit), overflow=0; Q[1]=5+15=20→5, overflow=15; Q[2]=5+15=20→5, overflow=15
        Assert.Equal(5.0, bQueue[0], precision: 10);
        Assert.Equal(5.0, bQueue[1], precision: 10);
        Assert.Equal(5.0, bQueue[2], precision: 10);

        // C: receives B overflow [0,15,15] as inflow, outflow=0, no limit
        // Q[0]=0; Q[1]=15; Q[2]=15+15=30
        Assert.Equal(0.0, cQueue[0], precision: 10);
        Assert.Equal(15.0, cQueue[1], precision: 10);
        Assert.Equal(30.0, cQueue[2], precision: 10);
    }

    /// <summary>
    /// AC-2: Cycle in overflow routing detected at compile time.
    /// A→B and B→A creates a cycle — compiler must reject.
    /// </summary>
    [Fact]
    public void WipOverflow_CycleDetection_ThrowsOnCycle()
    {
        var model = new ModelDefinition
        {
            Grid = new GridDefinition { Bins = 3, BinSize = 1, BinUnit = "hours" },
            Nodes =
            {
                new NodeDefinition { Id = "a_arrivals", Kind = "const", Values = new[] { 10.0, 10.0, 10.0 } },
                new NodeDefinition { Id = "a_capacity", Kind = "const", Values = new[] { 5.0, 5.0, 5.0 } },
                new NodeDefinition { Id = "a_served", Kind = "expr", Expr = "a_capacity" },
                new NodeDefinition { Id = "a_errors", Kind = "const", Values = new[] { 0.0, 0.0, 0.0 } },
                new NodeDefinition { Id = "b_arrivals", Kind = "const", Values = new[] { 10.0, 10.0, 10.0 } },
                new NodeDefinition { Id = "b_capacity", Kind = "const", Values = new[] { 5.0, 5.0, 5.0 } },
                new NodeDefinition { Id = "b_served", Kind = "expr", Expr = "b_capacity" },
                new NodeDefinition { Id = "b_errors", Kind = "const", Values = new[] { 0.0, 0.0, 0.0 } },
            },
            Topology = new TopologyDefinition
            {
                Nodes =
                {
                    new TopologyNodeDefinition
                    {
                        Id = "NodeA",
                        Kind = "serviceWithBuffer",
                        WipLimit = 5.0,
                        WipOverflow = "NodeB",
                        Semantics = new TopologyNodeSemanticsDefinition
                        {
                            Arrivals = "a_arrivals",
                            Served = "a_served",
                            Errors = "a_errors",
                            QueueDepth = "a_queue"
                        }
                    },
                    new TopologyNodeDefinition
                    {
                        Id = "NodeB",
                        Kind = "serviceWithBuffer",
                        WipLimit = 5.0,
                        WipOverflow = "NodeA",
                        Semantics = new TopologyNodeSemanticsDefinition
                        {
                            Arrivals = "b_arrivals",
                            Served = "b_served",
                            Errors = "b_errors",
                            QueueDepth = "b_queue"
                        }
                    }
                }
            }
        };

        var ex = Assert.Throws<InvalidOperationException>(() => ModelCompiler.Compile(model));
        Assert.Contains("cycle", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────────────────
    // AC-5: Time-varying wipLimit (series reference)
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// AC-5: wipLimit as a series reference — limit changes per bin.
    /// Limit series = [20, 10, 5], inflow=15, outflow=2.
    /// Q[0]=13 (&lt;20, no clamp), Q[1]=13+13=26→10 overflow=16, Q[2]=10+13=23→5 overflow=18.
    /// </summary>
    [Fact]
    public void WipLimit_TimeVarying_LimitChangesPerBin()
    {
        var grid = new TimeGrid(3, 1, TimeUnit.Hours);
        var node = new ServiceWithBufferNode(
            "queue", inflow: new NodeId("inflow"), outflow: new NodeId("outflow"),
            loss: null, initialDepth: 0, dispatchSchedule: null,
            wipLimit: null, wipOverflowTarget: null,
            wipLimitSeriesId: new NodeId("wip_limit_series"));

        var inflow = new Series(new double[] { 15, 15, 15 });
        var outflow = new Series(new double[] { 2, 2, 2 });
        var wipLimitSeries = new Series(new double[] { 20, 10, 5 });

        Series GetInput(NodeId id) => id.Value switch
        {
            "inflow" => inflow,
            "outflow" => outflow,
            "wip_limit_series" => wipLimitSeries,
            _ => throw new InvalidOperationException($"Unknown node: {id}")
        };

        var result = node.Evaluate(grid, GetInput);

        // Q[0] = max(0, 0 + 15 - 2) = 13 (< 20, no clamp)
        // Q[1] = max(0, 13 + 15 - 2) = 26 → clamped to 10, overflow = 16
        // Q[2] = max(0, 10 + 15 - 2) = 23 → clamped to 5, overflow = 18
        Assert.Equal(13.0, result[0], precision: 10);
        Assert.Equal(10.0, result[1], precision: 10);
        Assert.Equal(5.0, result[2], precision: 10);

        Assert.NotNull(node.LastOverflow);
        Assert.Equal(0.0, node.LastOverflow![0], precision: 10);
        Assert.Equal(16.0, node.LastOverflow[1], precision: 10);
        Assert.Equal(18.0, node.LastOverflow[2], precision: 10);
    }
}
