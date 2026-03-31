using FlowTime.Core.Analysis;
using FlowTime.Core.Dispatching;
using FlowTime.Core.Execution;
using FlowTime.Core.Models;
using FlowTime.Core.Nodes;

namespace FlowTime.Core.Tests.Bugs;

/// <summary>
/// Regression tests for Phase 0 correctness bugs found in the March 2026 engine deep review.
/// Each test reproduces the bug and should FAIL before the fix, PASS after.
/// </summary>
public class Phase0BugTests
{
    /// <summary>
    /// BUG-1 [CRITICAL]: ServiceWithBufferNode mutates shared memoized series.
    ///
    /// When a ServiceWithBufferNode has a dispatch schedule, it calls
    /// DispatchScheduleProcessor.ApplySchedule on the memoized outflow series,
    /// zeroing non-dispatch bins in place. Any other node that reads the same
    /// outflow series sees corrupted (zeroed) values.
    ///
    /// This test creates two ServiceWithBufferNodes that share the same outflow
    /// series, where one has a dispatch schedule. The node WITHOUT the dispatch
    /// schedule should see the original (uncorrupted) outflow values.
    /// </summary>
    [Fact]
    public void Bug1_SharedSeriesMutation_DispatchDoesNotCorruptSharedOutflow()
    {
        var grid = new TimeGrid(10, 1, TimeUnit.Minutes);

        // Shared outflow: constant 5.0 per bin
        var sharedOutflow = new Series(new double[] { 5, 5, 5, 5, 5, 5, 5, 5, 5, 5 });
        var inflow = new Series(new double[] { 8, 8, 8, 8, 8, 8, 8, 8, 8, 8 });

        // Memo dictionary simulating Graph's internal cache
        var memo = new Dictionary<NodeId, Series>
        {
            [new NodeId("inflow")] = inflow,
            [new NodeId("shared_outflow")] = sharedOutflow,
        };

        // Node A: has a dispatch schedule (period=3, phase=0 → dispatches at bins 0,3,6,9)
        var nodeA = new ServiceWithBufferNode(
            "queue_a",
            new NodeId("inflow"),
            new NodeId("shared_outflow"),
            loss: null,
            initialDepth: 0,
            dispatchSchedule: new DispatchScheduleConfig(3, 0, null));

        // Node B: no dispatch schedule, same outflow
        var nodeB = new ServiceWithBufferNode(
            "queue_b",
            new NodeId("inflow"),
            new NodeId("shared_outflow"),
            loss: null,
            initialDepth: 0,
            dispatchSchedule: null);

        // Evaluate A first (it has the dispatch schedule)
        var resultA = nodeA.Evaluate(grid, id => memo[id]);

        // Now evaluate B — it should see the ORIGINAL outflow values (5.0 per bin),
        // not the dispatch-zeroed values
        var resultB = nodeB.Evaluate(grid, id => memo[id]);

        // Node B's queue should be: Q[t] = max(0, Q[t-1] + 8 - 5)
        // = 3, 6, 9, 12, 15, 18, 21, 24, 27, 30
        // If BUG-1 exists, the shared outflow is corrupted (zeroed on non-dispatch bins),
        // so Node B would compute Q[t] = max(0, Q[t-1] + 8 - 0) on non-dispatch bins.
        Assert.Equal(3.0, resultB[0], precision: 6);
        Assert.Equal(6.0, resultB[1], precision: 6);
        Assert.Equal(9.0, resultB[2], precision: 6);
        Assert.Equal(12.0, resultB[3], precision: 6);
    }

    /// <summary>
    /// BUG-2 [HIGH]: ServiceWithBufferNode.Inputs omits CapacitySeriesId.
    ///
    /// When a dispatch schedule specifies a CapacitySeriesId, the Inputs property
    /// must include it. Otherwise the topological sort won't see the dependency,
    /// and getInput(capacityId) may fail with KeyNotFoundException.
    /// </summary>
    [Fact]
    public void Bug2_InputsIncludesCapacitySeriesId()
    {
        var capacityId = new NodeId("capacity_series");
        var node = new ServiceWithBufferNode(
            "queue",
            new NodeId("inflow"),
            new NodeId("outflow"),
            loss: null,
            initialDepth: 0,
            dispatchSchedule: new DispatchScheduleConfig(3, 0, capacityId));

        var inputs = node.Inputs.ToList();

        Assert.Contains(capacityId, inputs);
    }

    /// <summary>
    /// BUG-2 additional: Inputs without dispatch schedule should be unchanged.
    /// </summary>
    [Fact]
    public void Bug2_InputsWithoutDispatchSchedule_NoCapacityId()
    {
        var node = new ServiceWithBufferNode(
            "queue",
            new NodeId("inflow"),
            new NodeId("outflow"),
            loss: null,
            initialDepth: 0,
            dispatchSchedule: null);

        var inputs = node.Inputs.ToList();

        Assert.Equal(2, inputs.Count);
        Assert.Contains(new NodeId("inflow"), inputs);
        Assert.Contains(new NodeId("outflow"), inputs);
    }

    /// <summary>
    /// BUG-2 additional: Inputs with dispatch schedule but no CapacitySeriesId
    /// should still work (no capacity dependency added).
    /// </summary>
    [Fact]
    public void Bug2_InputsWithDispatchButNoCapacity_NoExtraInput()
    {
        var node = new ServiceWithBufferNode(
            "queue",
            new NodeId("inflow"),
            new NodeId("outflow"),
            loss: null,
            initialDepth: 0,
            dispatchSchedule: new DispatchScheduleConfig(3, 0, null));

        var inputs = node.Inputs.ToList();

        Assert.Equal(2, inputs.Count);
    }

    /// <summary>
    /// BUG-3 [MEDIUM]: InvariantAnalyzer.ValidateQueue ignores dispatch schedules.
    ///
    /// ServiceWithBufferNode zeros outflow on non-dispatch bins via ApplySchedule.
    /// The invariant analyzer recomputes expected queue depth from raw outflow,
    /// producing false positive "queue_depth_mismatch" warnings.
    /// </summary>
    [Fact]
    public void Bug3_InvariantAnalyzer_NoFalsePositiveWithDispatchSchedule()
    {
        // Model: serviceWithBuffer node with dispatch schedule (period=2, phase=0)
        // Dispatches at bins 0, 2, 4 — outflow zeroed at bins 1, 3
        var model = new ModelDefinition
        {
            Grid = new GridDefinition { Bins = 6, BinSize = 1, BinUnit = "hours" },
            Nodes = new List<NodeDefinition>
            {
                new NodeDefinition
                {
                    Id = "Buffer",
                    Kind = "serviceWithBuffer",
                    Inflow = "buf_inflow",
                    Outflow = "buf_outflow",
                    DispatchSchedule = new DispatchScheduleDefinition
                    {
                        Kind = "time-based",
                        PeriodBins = 2,
                        PhaseOffset = 0
                    }
                }
            },
            Topology = new TopologyDefinition
            {
                Nodes =
                {
                    new TopologyNodeDefinition
                    {
                        Id = "Source",
                        Kind = "service",
                        Semantics = new TopologyNodeSemanticsDefinition
                        {
                            Arrivals = "src_arrivals",
                            Served = "src_served"
                        }
                    },
                    new TopologyNodeDefinition
                    {
                        Id = "Buffer",
                        Kind = "serviceWithBuffer",
                        Semantics = new TopologyNodeSemanticsDefinition
                        {
                            Arrivals = "buf_inflow",
                            Served = "buf_outflow",
                            QueueDepth = "buf_queue"
                        },
                        DispatchSchedule = new DispatchScheduleDefinition
                        {
                            Kind = "time-based",
                            PeriodBins = 2,
                            PhaseOffset = 0
                        }
                    }
                },
                Edges =
                {
                    new TopologyEdgeDefinition
                    {
                        Source = "Source",
                        Target = "Buffer",
                        Measure = "served"
                    }
                }
            }
        };

        // Simulate: inflow=3/bin, outflow=5/bin (raw), dispatch zeros bins 1,3,5
        // Effective outflow: 5, 0, 5, 0, 5, 0
        // Queue: Q[0]=max(0, 0+3-5)=0, Q[1]=max(0, 0+3-0)=3,
        //        Q[2]=max(0, 3+3-5)=1, Q[3]=max(0, 1+3-0)=4,
        //        Q[4]=max(0, 4+3-5)=2, Q[5]=max(0, 2+3-0)=5
        var evaluated = new Dictionary<NodeId, double[]>
        {
            [new NodeId("src_arrivals")] = new[] { 3d, 3, 3, 3, 3, 3 },
            [new NodeId("src_served")] = new[] { 3d, 3, 3, 3, 3, 3 },
            [new NodeId("buf_inflow")] = new[] { 3d, 3, 3, 3, 3, 3 },
            [new NodeId("buf_outflow")] = new[] { 5d, 5, 5, 5, 5, 5 },  // raw (pre-dispatch)
            [new NodeId("buf_queue")] = new[] { 0d, 3, 1, 4, 2, 5 },     // correct (post-dispatch)
        };

        var result = InvariantAnalyzer.Analyze(model, evaluated);

        // Should NOT have queue_depth_mismatch warning
        Assert.DoesNotContain(result.Warnings,
            w => w.Code == "queue_depth_mismatch" && w.NodeId == "Buffer");
    }
}
