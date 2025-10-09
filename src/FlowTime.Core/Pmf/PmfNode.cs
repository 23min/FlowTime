using FlowTime.Core.Execution;
using FlowTime.Core.Models;
using FlowTime.Core.Nodes;

namespace FlowTime.Core.Pmf;

/// <summary>
/// A node that emits a constant expected value series based on a Probability Mass Function (PMF).
/// PMF nodes convert uncertainty into deterministic expected values for downstream analysis.
/// </summary>
public class PmfNode : INode
{
    /// <summary>
    /// The unique identifier for this node.
    /// </summary>
    public NodeId Id { get; }

    /// <summary>
    /// The PMF that defines the probability distribution for this node.
    /// </summary>
    public Pmf Pmf { get; }

    /// <summary>
    /// PMF nodes have no dependencies on other nodes.
    /// </summary>
    public IEnumerable<NodeId> Inputs => Enumerable.Empty<NodeId>();

    /// <summary>
    /// Create a new PMF node.
    /// </summary>
    /// <param name="id">Unique node identifier</param>
    /// <param name="pmf">The probability mass function</param>
    public PmfNode(NodeId id, Pmf pmf)
    {
        Id = id;
        Pmf = pmf ?? throw new ArgumentNullException(nameof(pmf));
    }

    /// <summary>
    /// Evaluate the PMF node to produce a time series.
    /// Returns a constant series with the PMF's expected value in all time bins.
    /// </summary>
    /// <param name="grid">The time grid for evaluation</param>
    /// <param name="getInput">Function to get input series from other nodes (unused for PMF nodes)</param>
    /// <returns>A series with constant expected value across all time bins</returns>
    public Series Evaluate(TimeGrid grid, Func<NodeId, Series> getInput)
    {
        // PMF nodes produce constant expected value across all time bins
        var values = new double[grid.Bins];
        Array.Fill(values, Pmf.ExpectedValue);
        
        return new Series(values);
    }

    /// <summary>
    /// Get a string representation of this PMF node for debugging.
    /// </summary>
    public override string ToString()
    {
        return $"PmfNode({Id}, {Pmf})";
    }

    /// <summary>
    /// Check equality based on ID and PMF content.
    /// </summary>
    public override bool Equals(object? obj)
    {
        if (obj is not PmfNode other) return false;
        return Id.Equals(other.Id) && Pmf.Equals(other.Pmf);
    }

    /// <summary>
    /// Get hash code based on ID and PMF content.
    /// </summary>
    public override int GetHashCode()
    {
        return HashCode.Combine(Id, Pmf.ExpectedValue);
    }
}
