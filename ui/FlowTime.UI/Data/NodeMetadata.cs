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
    public static readonly NodeMetadata[] All =
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
            Title: "Expression Engine",
            Description: "Evaluates mathematical expressions with operators, functions, and node references. Supports SHIFT, MIN, MAX, CLAMP functions.",
            Inputs: "Variable (based on expression)",
            Output: "Series<double>",
            Status: NodeStatus.Implemented,
            ExampleExpr: "demand * 1.2 + SHIFT(demand, 1)",
            Notes: "M1.5 complete: arithmetic (+,-,*,/), functions (SHIFT, MIN, MAX, CLAMP), node references."
        ),
        new(
            Kind: "pmf",
            Title: "Probability Mass Function",
            Description: "Converts discrete probability distributions into expected value time series.",
            Inputs: "(none - PMF definition)",
            Output: "Series<double>",
            Status: NodeStatus.Implemented,
            ExampleExpr: "pmf: {values: [0,1,2], probabilities: [0.2,0.5,0.3]}",
            Notes: "M2 complete: Expected value calculation from discrete PMF. Full distribution propagation in M15."
        ),
        new(
            Kind: "shift",
            Title: "SHIFT Function",
            Description: "Temporal lag operation with stateful history buffers. SHIFT(series, k) returns series lagged by k bins.",
            Inputs: "1 series + lag parameter",
            Output: "Series<double>",
            Status: NodeStatus.Implemented,
            ExampleExpr: "SHIFT(demand, 2) → [0, 0, demand[0], demand[1], ...]",
            Notes: "M1.5 stateful node: maintains history, causal evaluation (k ≥ 0), efficient circular buffers."
        ),
        new(
            Kind: "min_max",
            Title: "MIN/MAX Functions",
            Description: "Element-wise minimum and maximum operations between two series or series-scalar combinations.",
            Inputs: "2 operands (series or scalars)",
            Output: "Series<double>",
            Status: NodeStatus.Implemented,
            ExampleExpr: "MIN(demand, capacity), MAX(baseline, 0)",
            Notes: "M1.5 complete: Handles series-series and series-scalar broadcasting."
        ),
        new(
            Kind: "clamp",
            Title: "CLAMP Function",
            Description: "Value constraining operation. CLAMP(value, min, max) bounds each element to range [min, max].",
            Inputs: "3 operands (value, min, max)",
            Output: "Series<double>",
            Status: NodeStatus.Implemented,
            ExampleExpr: "CLAMP(utilization, 0.0, 1.0)",
            Notes: "M1.5 complete: Ensures values stay within specified bounds, supports series/scalar mixing."
        )
    };
}
