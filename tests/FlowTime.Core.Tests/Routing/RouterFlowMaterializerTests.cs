using FlowTime.Core.Execution;
using FlowTime.Core.Models;
using FlowTime.Core.Routing;
using FlowTime.Core.Nodes;

namespace FlowTime.Core.Tests.Routing;

public class RouterFlowMaterializerTests
{
    [Fact]
    public void ComputeOverrides_DistributesClassRoutes()
    {
        var model = new ModelDefinition
        {
            Grid = new GridDefinition { Bins = 2, BinSize = 1, BinUnit = "hours" },
            Nodes =
            {
                new NodeDefinition { Id = "alpha_source", Kind = "const", Values = new[] { 4d, 6d } },
                new NodeDefinition { Id = "beta_source", Kind = "const", Values = new[] { 6d, 4d } },
                new NodeDefinition { Id = "source_total", Kind = "expr", Expr = "alpha_source + beta_source" },
                new NodeDefinition { Id = "airport_flow", Kind = "expr", Expr = "0" },
                new NodeDefinition { Id = "general_flow", Kind = "expr", Expr = "0" },
                new NodeDefinition
                {
                    Id = "hub_router",
                    Kind = "router",
                    Router = new RouterDefinition
                    {
                        Inputs = new RouterInputsDefinition { Queue = "source_total" },
                        Routes = new List<RouterRouteDefinition>
                        {
                            new() { Target = "airport_flow", Classes = new[] { "Alpha" } },
                            new() { Target = "general_flow", Weight = 1d }
                        }
                    }
                }
            },
            Traffic = new TrafficDefinition
            {
                Arrivals = new List<ArrivalDefinition>
                {
                    new() { NodeId = "alpha_source", ClassId = "Alpha" },
                    new() { NodeId = "beta_source", ClassId = "Beta" }
                }
            }
        };

        var grid = new TimeGrid(2, 1, TimeUnit.Hours);
        var totals = new Dictionary<NodeId, double[]>
        {
            [new NodeId("alpha_source")] = new[] { 4d, 6d },
            [new NodeId("beta_source")] = new[] { 6d, 4d },
            [new NodeId("source_total")] = new[] { 10d, 10d }
        };

        var overrides = RouterFlowMaterializer.ComputeOverrides(
            model,
            grid,
            totals,
            new Dictionary<NodeId, string>
            {
                [new NodeId("alpha_source")] = "Alpha",
                [new NodeId("beta_source")] = "Beta"
            });

        var airport = Assert.Contains(new NodeId("airport_flow"), overrides);
        Assert.Equal(new[] { 4d, 6d }, airport);

        var general = Assert.Contains(new NodeId("general_flow"), overrides);
        Assert.Equal(new[] { 6d, 4d }, general);
    }

    [Fact]
    public void EvaluateWithOverrides_RecomputesDownstreamNodes()
    {
        var model = new ModelDefinition
        {
            Grid = new GridDefinition { Bins = 2, BinSize = 1, BinUnit = "hours" },
            Nodes =
            {
                new NodeDefinition { Id = "alpha_source", Kind = "const", Values = new[] { 4d, 6d } },
                new NodeDefinition { Id = "beta_source", Kind = "const", Values = new[] { 6d, 4d } },
                new NodeDefinition { Id = "source_total", Kind = "expr", Expr = "alpha_source + beta_source" },
                new NodeDefinition { Id = "airport_flow", Kind = "expr", Expr = "0" },
                new NodeDefinition { Id = "general_flow", Kind = "expr", Expr = "0" },
                new NodeDefinition { Id = "downstream_sum", Kind = "expr", Expr = "airport_flow + general_flow" },
                new NodeDefinition
                {
                    Id = "hub_router",
                    Kind = "router",
                    Router = new RouterDefinition
                    {
                        Inputs = new RouterInputsDefinition { Queue = "source_total" },
                        Routes = new List<RouterRouteDefinition>
                        {
                            new() { Target = "airport_flow", Classes = new[] { "Alpha" } },
                            new() { Target = "general_flow", Weight = 1d }
                        }
                    }
                }
            },
            Traffic = new TrafficDefinition
            {
                Arrivals = new List<ArrivalDefinition>
                {
                    new() { NodeId = "alpha_source", ClassId = "Alpha" },
                    new() { NodeId = "beta_source", ClassId = "Beta" }
                }
            }
        };

        var (grid, graph) = ModelParser.ParseModel(model);
        var initial = graph.Evaluate(grid);
        var totals = initial.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray());

        var overrides = RouterFlowMaterializer.ComputeOverrides(
            model,
            grid,
            totals,
            new Dictionary<NodeId, string>
            {
                [new NodeId("alpha_source")] = "Alpha",
                [new NodeId("beta_source")] = "Beta"
            });

        var reevaluated = graph.EvaluateWithOverrides(grid, overrides);

        var airport = Assert.Contains(new NodeId("airport_flow"), reevaluated);
        Assert.Equal(new[] { 4d, 6d }, airport.ToArray());

        var sum = Assert.Contains(new NodeId("downstream_sum"), reevaluated);
        Assert.Equal(new[] { 10d, 10d }, sum.ToArray());
    }
}
