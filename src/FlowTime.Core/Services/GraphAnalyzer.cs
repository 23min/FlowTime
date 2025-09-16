using FlowTime.Core.Models;

namespace FlowTime.Core.Services;

/// <summary>
/// Provides analysis capabilities for FlowTime model graphs, including dependency extraction and relationship mapping.
/// </summary>
public static class GraphAnalyzer
{
    /// <summary>
    /// Extracts the input dependencies for a given node by analyzing its definition and expression references.
    /// </summary>
    /// <param name="node">The node to analyze for input dependencies</param>
    /// <param name="model">The complete model containing all node definitions</param>
    /// <returns>Array of node IDs that this node depends on as inputs</returns>
    public static string[] GetNodeInputs(NodeDefinition node, ModelDefinition model)
    {
        if (node.Kind == "const")
        {
            return Array.Empty<string>(); // const nodes have no inputs
        }
        else if (node.Kind == "expr")
        {
            // Simple extraction of referenced node names from expressions
            // This is a simplified approach - for a complete solution, we'd parse the expression AST
            var expr = node.Expr ?? "";
            var inputs = new List<string>();
            
            // Look for node references (simple word patterns that match existing node IDs)
            foreach (var otherNode in model.Nodes)
            {
                if (otherNode.Id != node.Id && expr.Contains(otherNode.Id))
                {
                    inputs.Add(otherNode.Id);
                }
            }
            
            return inputs.ToArray();
        }
        else if (node.Kind == "pmf")
        {
            return Array.Empty<string>(); // PMF nodes typically don't have runtime inputs
        }
        
        return Array.Empty<string>();
    }
}