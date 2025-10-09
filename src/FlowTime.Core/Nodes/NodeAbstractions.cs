using FlowTime.Core.Execution;
using FlowTime.Core.Models;

namespace FlowTime.Core.Nodes;

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

public interface IStatefulNode : INode
{
    void InitializeState(TimeGrid grid);
    void UpdateState(int currentBin, double value);
}
