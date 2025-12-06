using System.Collections.Generic;
using System.Linq;
using FlowTime.Core.Models;
using FlowTime.Core.Nodes;

namespace FlowTime.Core.Routing;

internal static class RouterSpecificationBuilder
{
    public static IReadOnlyDictionary<NodeId, RouterSpec> Build(ModelDefinition model)
    {
        var specs = new Dictionary<NodeId, RouterSpec>(new NodeIdComparer());
        foreach (var node in model.Nodes.Where(n => string.Equals(n.Kind, "router", StringComparison.OrdinalIgnoreCase)))
        {
            if (node.Router?.Inputs?.Queue is null || node.Router.Routes is null)
            {
                continue;
            }

            var routes = new List<RouterRouteSpec>();
            foreach (var route in node.Router.Routes)
            {
                if (string.IsNullOrWhiteSpace(route.Target))
                {
                    continue;
                }

                var classes = route.Classes?
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .Select(c => c.Trim())
                    .ToArray() ?? Array.Empty<string>();

                var weight = route.Weight ?? 1d;
                routes.Add(new RouterRouteSpec(new NodeId(route.Target), classes, weight));
            }

            if (routes.Count == 0)
            {
                continue;
            }

            specs[new NodeId(node.Id)] = new RouterSpec(
                new NodeId(node.Id),
                new NodeId(node.Router.Inputs.Queue),
                routes);
        }

        return specs;
    }
}

internal sealed record RouterSpec(NodeId RouterId, NodeId SourceId, IReadOnlyList<RouterRouteSpec> Routes);

internal sealed record RouterRouteSpec(NodeId TargetId, IReadOnlyList<string> Classes, double Weight);

internal sealed class NodeIdComparer : IEqualityComparer<NodeId>
{
    public bool Equals(NodeId x, NodeId y) =>
        string.Equals(x.Value, y.Value, StringComparison.OrdinalIgnoreCase);

    public int GetHashCode(NodeId obj) =>
        obj.Value?.ToLowerInvariant().GetHashCode() ?? 0;
}
