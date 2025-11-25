using System.Collections.Generic;
using FlowTime.Core.Artifacts;
using FlowTime.Core.Models;
using FlowTime.Core.Nodes;

namespace FlowTime.Core.Tests.Aggregation;

public class RouterClassContributionTests
{
    [Fact]
    public void RouterRoutesClassesToTargets()
    {
        var model = new ModelDefinition
        {
            Grid = new GridDefinition { Bins = 2, BinSize = 1, BinUnit = "hours" },
            Nodes =
            {
                new NodeDefinition { Id = "alpha_source", Kind = "const", Values = new[] { 4d, 6d } },
                new NodeDefinition { Id = "beta_source", Kind = "const", Values = new[] { 6d, 4d } },
                new NodeDefinition { Id = "source_total", Kind = "expr", Expr = "alpha_source + beta_source" },
                new NodeDefinition { Id = "airport_flow", Kind = "expr", Expr = "source_total * 0.4" },
                new NodeDefinition { Id = "general_flow", Kind = "expr", Expr = "source_total * 0.6" },
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
            }
        };

        var grid = new TimeGrid(2, 1, TimeUnit.Hours);
        var totals = new Dictionary<NodeId, double[]>
        {
            [new NodeId("alpha_source")] = new[] { 4d, 6d },
            [new NodeId("beta_source")] = new[] { 6d, 4d },
            [new NodeId("source_total")] = new[] { 10d, 10d },
            [new NodeId("airport_flow")] = new[] { 4d, 6d },
            [new NodeId("general_flow")] = new[] { 6d, 4d },
            [new NodeId("hub_router")] = new[] { 10d, 10d }
        };
        var classAssignments = new Dictionary<NodeId, string>
        {
            [new NodeId("alpha_source")] = "Alpha",
            [new NodeId("beta_source")] = "Beta"
        };

        var contributions = ClassContributionBuilder.Build(model, grid, totals, classAssignments);

        var airport = Assert.Contains(new NodeId("airport_flow"), contributions);
        Assert.Equal(new[] { 4d, 6d }, airport["Alpha"]);
        Assert.False(airport.ContainsKey("Beta"));

        var general = Assert.Contains(new NodeId("general_flow"), contributions);
        Assert.Equal(new[] { 6d, 4d }, general["Beta"]);
        Assert.False(general.ContainsKey("Alpha"));
    }

    [Fact]
    public void RouterWeightedRoutesSplitRemainder()
    {
        var model = new ModelDefinition
        {
            Grid = new GridDefinition { Bins = 2, BinSize = 1, BinUnit = "hours" },
            Nodes =
            {
                new NodeDefinition { Id = "alpha_source", Kind = "const", Values = new[] { 8d, 4d } },
                new NodeDefinition { Id = "beta_source", Kind = "const", Values = new[] { 2d, 6d } },
                new NodeDefinition { Id = "source_total", Kind = "expr", Expr = "alpha_source + beta_source" },
                new NodeDefinition { Id = "route_a", Kind = "expr", Expr = "source_total * 0.7" },
                new NodeDefinition { Id = "route_b", Kind = "expr", Expr = "source_total * 0.3" },
                new NodeDefinition
                {
                    Id = "hub_router",
                    Kind = "router",
                    Router = new RouterDefinition
                    {
                        Inputs = new RouterInputsDefinition { Queue = "source_total" },
                        Routes = new List<RouterRouteDefinition>
                        {
                            new() { Target = "route_a", Weight = 2d },
                            new() { Target = "route_b", Weight = 1d }
                        }
                    }
                }
            }
        };

        var grid = new TimeGrid(2, 1, TimeUnit.Hours);
        var totals = new Dictionary<NodeId, double[]>
        {
            [new NodeId("alpha_source")] = new[] { 8d, 4d },
            [new NodeId("beta_source")] = new[] { 2d, 6d },
            [new NodeId("source_total")] = new[] { 10d, 10d },
            [new NodeId("route_a")] = new[] { 20d / 3d, 20d / 3d },
            [new NodeId("route_b")] = new[] { 10d / 3d, 10d / 3d },
            [new NodeId("hub_router")] = new[] { 10d, 10d }
        };
        var classAssignments = new Dictionary<NodeId, string>
        {
            [new NodeId("alpha_source")] = "Alpha",
            [new NodeId("beta_source")] = "Beta"
        };

        var contributions = ClassContributionBuilder.Build(model, grid, totals, classAssignments);

        var routeA = Assert.Contains(new NodeId("route_a"), contributions);
        Assert.Equal(new[] { 16d / 3d, 8d / 3d }, routeA["Alpha"], new DoubleArrayComparer(1e-9));
        Assert.Equal(new[] { 4d / 3d, 4d }, routeA["Beta"], new DoubleArrayComparer(1e-9));

        var routeB = Assert.Contains(new NodeId("route_b"), contributions);
        Assert.Equal(new[] { 8d / 3d, 4d / 3d }, routeB["Alpha"], new DoubleArrayComparer(1e-9));
        Assert.Equal(new[] { 2d / 3d, 2d }, routeB["Beta"], new DoubleArrayComparer(1e-9));
    }

    private sealed class DoubleArrayComparer : IEqualityComparer<double[]>
    {
        private readonly double tolerance;

        public DoubleArrayComparer(double tolerance)
        {
            this.tolerance = tolerance;
        }

        public bool Equals(double[]? x, double[]? y)
        {
            if (x is null || y is null || x.Length != y.Length)
            {
                return false;
            }

            for (var i = 0; i < x.Length; i++)
            {
                if (Math.Abs(x[i] - y[i]) > tolerance)
                {
                    return false;
                }
            }

            return true;
        }

        public int GetHashCode(double[] obj) => obj.Length;
    }
}
