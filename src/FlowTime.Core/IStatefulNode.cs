namespace FlowTime.Core;

/// <summary>
/// Interface for nodes that maintain state across time bins during evaluation.
/// Stateful nodes can store history and implement temporal operations like SHIFT, EMA, etc.
/// </summary>
public interface IStatefulNode : INode
{
    /// <summary>
    /// Initialize the node's state for the given time grid.
    /// Called once before evaluation begins.
    /// </summary>
    /// <param name="grid">The time grid for the current evaluation</param>
    void InitializeState(TimeGrid grid);
    
    /// <summary>
    /// Update the node's internal state after computing a value for the current bin.
    /// Called after Evaluate() for each bin during the evaluation process.
    /// </summary>
    /// <param name="currentBin">The bin index that was just evaluated (0-based)</param>
    /// <param name="value">The computed value for this bin</param>
    void UpdateState(int currentBin, double value);
}
