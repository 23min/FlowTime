namespace FlowTime.Core;

public readonly record struct NodeId(string Value)
{
    public override string ToString() => Value;
}

public interface INode
{
    NodeId Id { get; }
    IEnumerable<NodeId> Inputs { get; }
    Series Evaluate(TimeGrid grid, Func<NodeId, Series> getInput);
}
