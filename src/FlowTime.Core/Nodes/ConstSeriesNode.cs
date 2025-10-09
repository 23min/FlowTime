using FlowTime.Core.Execution;
using FlowTime.Core.Models;

namespace FlowTime.Core.Nodes;

public sealed class ConstSeriesNode : INode
{
    public NodeId Id { get; }
    public IEnumerable<NodeId> Inputs { get; } = Array.Empty<NodeId>();
    private readonly double[] values;

    public ConstSeriesNode(string id, double[] values)
    {
        Id = new NodeId(id);
        this.values = values;
    }

    public Series Evaluate(TimeGrid grid, Func<NodeId, Series> getInput)
    {
        if (values.Length != grid.Length)
            throw new ArgumentException($"ConstSeriesNode {Id}: values length {values.Length} != grid length {grid.Length}");
        return new Series((double[])values.Clone());
    }
}
