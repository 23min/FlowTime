using FlowTime.Core.Execution;
using FlowTime.Core.Models;

namespace FlowTime.Core.Nodes;

public sealed class RouterNode : INode
{
    private readonly NodeId queueId;

    public NodeId Id { get; }
    public IEnumerable<NodeId> Inputs { get; }

    public RouterNode(string id, NodeId queue)
    {
        Id = new NodeId(id);
        queueId = queue;
        Inputs = new[] { queue };
    }

    public Series Evaluate(TimeGrid grid, Func<NodeId, Series> getInput)
    {
        return getInput(queueId);
    }
}
