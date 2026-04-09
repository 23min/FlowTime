using FlowTime.Core.Compiler;
using FlowTime.Core.Constraints;
using FlowTime.Core.Execution;
using FlowTime.Core.Models;
using FlowTime.Core.Nodes;
using FlowTime.Core.Routing;

namespace FlowTime.Core.Tests.Constraints;

/// <summary>
/// Tests for AC-1 through AC-4 of m-ec-p3d Constraint Enforcement.
/// Verifies that ConstraintAllocator is wired into the evaluation pipeline
/// so declared constraints actually cap served throughput per bin and
/// propagate capped values downstream.
/// </summary>
public sealed class ConstraintEnforcementTests
{
    /// <summary>
    /// AC-4 scenario 1: Two nodes share a constraint with capacity less than
    /// total demand. Each node's served should be proportionally capped.
    ///
    /// Setup: 4 bins, two services each wanting 60/bin, shared constraint
    /// capacity of 80/bin. Expected allocation: 40 each (proportional split).
    /// </summary>
    [Fact]
    public void SingleConstraint_TwoNodes_ProportionalSplit()
    {
        var model = BuildTwoNodeConstraintModel(
            nodeADemand: 60, nodeBDemand: 60, constraintCapacity: 80, bins: 4);
        var (result, metadata) = EvaluateWithConstraints(model);

        // Before enforcement, served == demand for both nodes (60 each, totaling 120 > 80 capacity).
        // After enforcement: proportional allocation = 80 * (60/120) = 40 each.
        var servedA = GetServedSeries(result, metadata, "ServiceA");
        var servedB = GetServedSeries(result, metadata, "ServiceB");

        for (int t = 0; t < 4; t++)
        {
            Assert.Equal(40.0, servedA[t], precision: 6);
            Assert.Equal(40.0, servedB[t], precision: 6);
        }
    }

    /// <summary>
    /// AC-4 scenario 2: Demand ≤ capacity — no capping needed.
    /// Each node gets its full demand.
    /// </summary>
    [Fact]
    public void Unconstrained_DemandBelowCapacity_NoCapping()
    {
        var model = BuildTwoNodeConstraintModel(
            nodeADemand: 30, nodeBDemand: 40, constraintCapacity: 100, bins: 4);
        var (result, metadata) = EvaluateWithConstraints(model);

        var servedA = GetServedSeries(result, metadata, "ServiceA");
        var servedB = GetServedSeries(result, metadata, "ServiceB");

        for (int t = 0; t < 4; t++)
        {
            Assert.Equal(30.0, servedA[t], precision: 6);
            Assert.Equal(40.0, servedB[t], precision: 6);
        }
    }

    /// <summary>
    /// AC-4 scenario 3: Constraint with zero capacity — all nodes get zero.
    /// </summary>
    [Fact]
    public void ZeroCapacity_AllNodesGetZero()
    {
        var model = BuildTwoNodeConstraintModel(
            nodeADemand: 50, nodeBDemand: 50, constraintCapacity: 0, bins: 4);
        var (result, metadata) = EvaluateWithConstraints(model);

        var servedA = GetServedSeries(result, metadata, "ServiceA");
        var servedB = GetServedSeries(result, metadata, "ServiceB");

        for (int t = 0; t < 4; t++)
        {
            Assert.Equal(0.0, servedA[t], precision: 6);
            Assert.Equal(0.0, servedB[t], precision: 6);
        }
    }

    /// <summary>
    /// AC-4 scenario 4: Downstream propagation. A consumer node sums the
    /// served output of two constrained producers. The consumer must see
    /// the capped (constrained) values, not the unconstrained originals.
    ///
    /// Setup: ServiceA wants 60, ServiceB wants 60, constraint capacity 80
    /// → each gets 40. Consumer = ServiceA_served + ServiceB_served should
    /// be 80, not 120.
    /// </summary>
    [Fact]
    public void DownstreamPropagation_ConsumerSeesConstrainedValues()
    {
        var model = BuildTwoNodeWithDownstreamModel(
            nodeADemand: 60, nodeBDemand: 60, constraintCapacity: 80, bins: 4);
        var (result, metadata) = EvaluateWithConstraints(model);

        // The consumer's arrivals = sum of constrained served from A and B
        var consumerArrivals = GetArrivalsSeries(result, metadata, "Consumer");

        for (int t = 0; t < 4; t++)
        {
            // 40 + 40 = 80, not 60 + 60 = 120
            Assert.Equal(80.0, consumerArrivals[t], precision: 6);
        }
    }

    /// <summary>
    /// AC-4 scenario 5: Multiple constraints on different node groups.
    /// Constraint1 caps {ServiceA, ServiceB} with capacity 80.
    /// Constraint2 caps {ServiceC} with capacity 25.
    /// ServiceA/B each want 60, ServiceC wants 50.
    /// </summary>
    [Fact]
    public void MultipleConstraints_DifferentGroups()
    {
        var model = BuildMultipleConstraintModel(bins: 4);
        var (result, metadata) = EvaluateWithConstraints(model);

        // Constraint1: A(60) + B(60) = 120 > 80 → proportional: A=40, B=40
        var servedA = GetServedSeries(result, metadata, "ServiceA");
        var servedB = GetServedSeries(result, metadata, "ServiceB");

        // Constraint2: C(50) > 25 → capped to 25
        var servedC = GetServedSeries(result, metadata, "ServiceC");

        for (int t = 0; t < 4; t++)
        {
            Assert.Equal(40.0, servedA[t], precision: 6);
            Assert.Equal(40.0, servedB[t], precision: 6);
            Assert.Equal(25.0, servedC[t], precision: 6);
        }
    }

    // -----------------------------------------------------------------------
    // Helpers: build models, evaluate, extract series
    // -----------------------------------------------------------------------

    /// <summary>
    /// Compile, parse, and evaluate a model with constraint enforcement.
    /// Returns the evaluation result and the parsed metadata (topology + constraints).
    /// </summary>
    private static (IReadOnlyDictionary<NodeId, Series> Evaluation, ModelMetadata Metadata) EvaluateWithConstraints(ModelDefinition model)
    {
        var compiled = ModelCompiler.Compile(model);
        var (grid, graph) = ModelParser.ParseModel(compiled);
        var metadata = ModelParser.ParseMetadata(compiled);

        // This is the method p3d introduces — constraint-aware evaluation.
        var result = ConstraintAwareEvaluator.Evaluate(compiled, graph, grid, metadata.Topology!);
        return (result.Evaluation, metadata);
    }

    private static double[] GetServedSeries(
        IReadOnlyDictionary<NodeId, Series> evaluation,
        ModelMetadata metadata,
        string topologyNodeId)
    {
        var node = metadata.Topology!.Nodes.Single(n => n.Id == topologyNodeId);
        var servedRef = node.Semantics.Served;
        var servedNodeId = new NodeId(servedRef.LookupKey);
        return evaluation[servedNodeId].ToArray();
    }

    private static double[] GetArrivalsSeries(
        IReadOnlyDictionary<NodeId, Series> evaluation,
        ModelMetadata metadata,
        string topologyNodeId)
    {
        var node = metadata.Topology!.Nodes.Single(n => n.Id == topologyNodeId);
        var arrivalsRef = node.Semantics.Arrivals;
        var arrivalsNodeId = new NodeId(arrivalsRef.LookupKey);
        return evaluation[arrivalsNodeId].ToArray();
    }

    // -----------------------------------------------------------------------
    // Model builders
    // -----------------------------------------------------------------------

    /// <summary>
    /// Two services (ServiceA, ServiceB) sharing one constraint (shared_pool).
    /// Each has a const demand series and a served series = demand (unconstrained).
    /// The constraint's capacity is a const series.
    /// </summary>
    private static ModelDefinition BuildTwoNodeConstraintModel(
        double nodeADemand, double nodeBDemand, double constraintCapacity, int bins)
    {
        var aDemand = ConstValues(nodeADemand, bins);
        var bDemand = ConstValues(nodeBDemand, bins);
        var capacity = ConstValues(constraintCapacity, bins);

        return new ModelDefinition
        {
            Grid = new GridDefinition { Bins = bins, BinSize = 1, BinUnit = "hours" },
            Nodes =
            {
                new NodeDefinition { Id = "a_arrivals", Kind = "const", Values = aDemand },
                new NodeDefinition { Id = "a_served", Kind = "expr", Expr = "a_arrivals" },
                new NodeDefinition { Id = "b_arrivals", Kind = "const", Values = bDemand },
                new NodeDefinition { Id = "b_served", Kind = "expr", Expr = "b_arrivals" },
                new NodeDefinition { Id = "pool_capacity", Kind = "const", Values = capacity },
                new NodeDefinition { Id = "pool_arrivals", Kind = "expr", Expr = "a_arrivals + b_arrivals" },
                new NodeDefinition { Id = "pool_served", Kind = "expr", Expr = "pool_capacity" },
                // Dummy error nodes required by topology semantics
                new NodeDefinition { Id = "a_errors", Kind = "const", Values = ConstValues(0, bins) },
                new NodeDefinition { Id = "b_errors", Kind = "const", Values = ConstValues(0, bins) },
            },
            Topology = new TopologyDefinition
            {
                Nodes =
                {
                    new TopologyNodeDefinition
                    {
                        Id = "ServiceA", Kind = "service",
                        Constraints = new List<string> { "shared_pool" },
                        Semantics = new TopologyNodeSemanticsDefinition
                        {
                            Arrivals = "a_arrivals", Served = "a_served", Errors = "a_errors"
                        }
                    },
                    new TopologyNodeDefinition
                    {
                        Id = "ServiceB", Kind = "service",
                        Constraints = new List<string> { "shared_pool" },
                        Semantics = new TopologyNodeSemanticsDefinition
                        {
                            Arrivals = "b_arrivals", Served = "b_served", Errors = "b_errors"
                        }
                    }
                },
                Constraints =
                {
                    new ConstraintDefinition
                    {
                        Id = "shared_pool",
                        Semantics = new ConstraintSemanticsDefinition
                        {
                            Arrivals = "pool_arrivals",
                            Served = "pool_served"
                        }
                    }
                }
            }
        };
    }

    /// <summary>
    /// Same as BuildTwoNodeConstraintModel but adds a Consumer node downstream
    /// whose arrivals = a_served + b_served. This tests AC-2 downstream propagation.
    /// </summary>
    private static ModelDefinition BuildTwoNodeWithDownstreamModel(
        double nodeADemand, double nodeBDemand, double constraintCapacity, int bins)
    {
        var model = BuildTwoNodeConstraintModel(nodeADemand, nodeBDemand, constraintCapacity, bins);

        model.Nodes.Add(new NodeDefinition { Id = "consumer_arrivals", Kind = "expr", Expr = "a_served + b_served" });
        model.Nodes.Add(new NodeDefinition { Id = "consumer_served", Kind = "expr", Expr = "consumer_arrivals" });
        model.Nodes.Add(new NodeDefinition { Id = "consumer_errors", Kind = "const", Values = ConstValues(0, bins) });

        model.Topology!.Nodes.Add(new TopologyNodeDefinition
        {
            Id = "Consumer", Kind = "service",
            Semantics = new TopologyNodeSemanticsDefinition
            {
                Arrivals = "consumer_arrivals", Served = "consumer_served", Errors = "consumer_errors"
            }
        });

        return model;
    }

    /// <summary>
    /// Three services in two independent constraint groups:
    /// - shared_pool_1: ServiceA (demand 60) + ServiceB (demand 60), capacity 80
    /// - shared_pool_2: ServiceC (demand 50), capacity 25
    /// </summary>
    private static ModelDefinition BuildMultipleConstraintModel(int bins)
    {
        return new ModelDefinition
        {
            Grid = new GridDefinition { Bins = bins, BinSize = 1, BinUnit = "hours" },
            Nodes =
            {
                new NodeDefinition { Id = "a_arrivals", Kind = "const", Values = ConstValues(60, bins) },
                new NodeDefinition { Id = "a_served", Kind = "expr", Expr = "a_arrivals" },
                new NodeDefinition { Id = "b_arrivals", Kind = "const", Values = ConstValues(60, bins) },
                new NodeDefinition { Id = "b_served", Kind = "expr", Expr = "b_arrivals" },
                new NodeDefinition { Id = "c_arrivals", Kind = "const", Values = ConstValues(50, bins) },
                new NodeDefinition { Id = "c_served", Kind = "expr", Expr = "c_arrivals" },
                new NodeDefinition { Id = "pool1_capacity", Kind = "const", Values = ConstValues(80, bins) },
                new NodeDefinition { Id = "pool1_arrivals", Kind = "expr", Expr = "a_arrivals + b_arrivals" },
                new NodeDefinition { Id = "pool1_served", Kind = "expr", Expr = "pool1_capacity" },
                new NodeDefinition { Id = "pool2_capacity", Kind = "const", Values = ConstValues(25, bins) },
                new NodeDefinition { Id = "pool2_arrivals", Kind = "expr", Expr = "c_arrivals" },
                new NodeDefinition { Id = "pool2_served", Kind = "expr", Expr = "pool2_capacity" },
                // Dummy error nodes
                new NodeDefinition { Id = "a_errors", Kind = "const", Values = ConstValues(0, bins) },
                new NodeDefinition { Id = "b_errors", Kind = "const", Values = ConstValues(0, bins) },
                new NodeDefinition { Id = "c_errors", Kind = "const", Values = ConstValues(0, bins) },
            },
            Topology = new TopologyDefinition
            {
                Nodes =
                {
                    new TopologyNodeDefinition
                    {
                        Id = "ServiceA", Kind = "service",
                        Constraints = new List<string> { "shared_pool_1" },
                        Semantics = new TopologyNodeSemanticsDefinition
                        {
                            Arrivals = "a_arrivals", Served = "a_served", Errors = "a_errors"
                        }
                    },
                    new TopologyNodeDefinition
                    {
                        Id = "ServiceB", Kind = "service",
                        Constraints = new List<string> { "shared_pool_1" },
                        Semantics = new TopologyNodeSemanticsDefinition
                        {
                            Arrivals = "b_arrivals", Served = "b_served", Errors = "b_errors"
                        }
                    },
                    new TopologyNodeDefinition
                    {
                        Id = "ServiceC", Kind = "service",
                        Constraints = new List<string> { "shared_pool_2" },
                        Semantics = new TopologyNodeSemanticsDefinition
                        {
                            Arrivals = "c_arrivals", Served = "c_served", Errors = "c_errors"
                        }
                    }
                },
                Constraints =
                {
                    new ConstraintDefinition
                    {
                        Id = "shared_pool_1",
                        Semantics = new ConstraintSemanticsDefinition
                        {
                            Arrivals = "pool1_arrivals",
                            Served = "pool1_served"
                        }
                    },
                    new ConstraintDefinition
                    {
                        Id = "shared_pool_2",
                        Semantics = new ConstraintSemanticsDefinition
                        {
                            Arrivals = "pool2_arrivals",
                            Served = "pool2_served"
                        }
                    }
                }
            }
        };
    }

    private static double[] ConstValues(double value, int bins)
    {
        var arr = new double[bins];
        Array.Fill(arr, value);
        return arr;
    }
}
