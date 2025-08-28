namespace FlowTime.UI.Data;

public enum NodeStatus { Implemented, Planned }

public sealed record NodeMetadata(
    string Kind,
    string Title,
    string Description,
    string Inputs,
    string Output,
    NodeStatus Status,
    string? ExampleExpr = null,
    string? Notes = null
);

public static class NodeCatalog
{
    // For M0 we hard-code. Later milestones can hydrate from API or manifest file.
    public static IReadOnlyList<NodeMetadata> All { get; } = new List<NodeMetadata>
    {
        new(
            Kind: "const",
            Title: "Constant Series",
            Description: "Provides a fixed sequence of numeric values matching the grid length.",
            Inputs: "(none)",
            Output: "Series<double>",
            Status: NodeStatus.Implemented,
            ExampleExpr: "values: [10,10,10,...]",
            Notes: "Values length must equal grid length."
        ),
        new(
            Kind: "expr",
            Title: "Expression (Binary Op Scalar)",
            Description: "Computes an elementwise expression over another series with a scalar (Add or Multiply).",
            Inputs: "1 series",
            Output: "Series<double>",
            Status: NodeStatus.Implemented,
            ExampleExpr: "demand * 0.8",
            Notes: "In M0 supported ops: +, * (infix)."
        ),
        new(
            Kind: "expr2",
            Title: "Expression (Binary Op Series)",
            Description: "Elementwise binary operation combining two input series (Add or Multiply).",
            Inputs: "2 series",
            Output: "Series<double>",
            Status: NodeStatus.Planned,
            ExampleExpr: "a * b",
            Notes: "Pending parser support for series-series ops in M1."
        )
    };
}
