using FlowTime.Core;

namespace FlowTime.Core.Nodes;

public enum BinOp { Add, Mul }

public sealed class BinaryOpNode : INode
{
    public NodeId Id { get; }
    public IEnumerable<NodeId> Inputs { get; }
    private readonly NodeId left;
    private readonly NodeId right;
    private readonly BinOp op;
    private readonly double? scalarRight;

    public BinaryOpNode(string id, NodeId left, NodeId right, BinOp op, double? scalarRight = null)
    {
        Id = new NodeId(id);
    this.left = left;
    this.right = right;
    this.op = op;
    this.scalarRight = scalarRight;
    Inputs = scalarRight.HasValue ? new[] { left } : new[] { left, right };
        // when scalarRight is provided, right is ignored
    }

    // Note: `grid` is not used by BinaryOpNode in M0 because this node performs
    // grid-agnostic, elementwise math over already-aligned Series. The parameter
    // remains part of the uniform INode signature and is required by nodes that
    // depend on bin geometry (e.g., Shift/Resample/Delay) in later milestones.
    public Series Evaluate(TimeGrid grid, Func<NodeId, Series> getInput)
    {
        var a = getInput(left);
        var result = new Series(a.Length);
        if (scalarRight.HasValue)
        {
            var k = scalarRight.Value;
            for (int t = 0; t < a.Length; t++)
            {
                result[t] = op == BinOp.Mul ? a[t] * k : a[t] + k;
            }
            return result;
        }
        var b = getInput(right);
        for (int t = 0; t < a.Length; t++)
        {
            result[t] = op == BinOp.Mul ? a[t] * b[t] : a[t] + b[t];
        }
        return result;
    }
}
