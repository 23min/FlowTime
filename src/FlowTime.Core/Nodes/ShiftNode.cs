namespace FlowTime.Core.Nodes;

/// <summary>
/// Node that implements the SHIFT(series, k) operation.
/// Returns the input series shifted k bins into the future (lagged by k bins).
/// SHIFT(x, 0) = identity, SHIFT(x, 1) = [0, x[0], x[1], ...], etc.
/// </summary>
public class ShiftNode : IStatefulNode
{
    private readonly INode sourceNode;
    private readonly int lag;
    private readonly Queue<double> history;
    
    public NodeId Id { get; }
    public IEnumerable<NodeId> Inputs => [sourceNode.Id];
    
    /// <summary>
    /// Creates a new SHIFT node.
    /// </summary>
    /// <param name="id">The node identifier</param>
    /// <param name="sourceNode">The source node to shift</param>
    /// <param name="lag">The number of bins to lag (must be >= 0)</param>
    public ShiftNode(string id, INode sourceNode, int lag)
    {
        if (lag < 0)
            throw new ArgumentException("Lag must be non-negative for causal evaluation", nameof(lag));
        
        Id = new NodeId(id ?? throw new ArgumentNullException(nameof(id)));
        this.sourceNode = sourceNode ?? throw new ArgumentNullException(nameof(sourceNode));
        this.lag = lag;
        history = new Queue<double>();
    }
    
    public void InitializeState(TimeGrid grid)
    {
        history.Clear();
        
        // Pre-fill history with zeros for the lag period
        for (int i = 0; i < lag; i++)
        {
            history.Enqueue(0.0);
        }
    }
    
    public Series Evaluate(TimeGrid grid, Func<NodeId, Series> getInput)
    {
        var sourceValues = getInput(sourceNode.Id);
        var shiftedValues = new double[grid.Bins];
        
        // For lag=0, just return the source series directly
        if (lag == 0)
        {
            return sourceValues;
        }
        
        // For lag>0, shift the series
        for (int i = 0; i < grid.Bins; i++)
        {
            if (i < lag)
            {
                // First lag bins are zero
                shiftedValues[i] = 0.0;
            }
            else
            {
                // Subsequent bins get values from lag bins earlier
                shiftedValues[i] = sourceValues[i - lag];
            }
        }
        
        return new Series(shiftedValues);
    }
    
    public void UpdateState(int currentBin, double value)
    {
        // For the Series-based evaluation, no state update is needed
        // This method is for future bin-by-bin evaluation support
    }
}
