using System.Globalization;

namespace FlowTime.Core.Models;

/// <summary>
/// Service for parsing FlowTime model definitions into executable node graphs.
/// Shared between CLI and API to avoid code duplication.
/// </summary>
public static class ModelParser
{
    /// <summary>
    /// Parse a model definition into a Graph ready for evaluation.
    /// </summary>
    /// <param name="model">The model definition with grid and nodes</param>
    /// <returns>Parsed TimeGrid and Graph ready for evaluation</returns>
    /// <exception cref="ModelParseException">Thrown when parsing fails</exception>
    public static (TimeGrid Grid, Graph Graph) ParseModel(ModelDefinition model)
    {
        if (model.Grid == null)
            throw new ModelParseException("Model must have a grid definition");
            
        var grid = new TimeGrid(model.Grid.Bins, model.Grid.BinMinutes);
        var nodes = ParseNodes(model.Nodes);
        var graph = new Graph(nodes);
        
        return (grid, graph);
    }
    
    /// <summary>
    /// Parse node definitions into INode objects.
    /// </summary>
    public static List<INode> ParseNodes(IEnumerable<NodeDefinition> nodeDefinitions)
    {
        var nodes = new List<INode>();
        
        foreach (var nodeDef in nodeDefinitions)
        {
            var node = ParseSingleNode(nodeDef);
            nodes.Add(node);
        }
        
        return nodes;
    }
    
    /// <summary>
    /// Parse a single node definition into an INode.
    /// </summary>
    public static INode ParseSingleNode(NodeDefinition nodeDef)
    {
        if (string.IsNullOrWhiteSpace(nodeDef.Id))
            throw new ModelParseException("Node must have an id");
            
        return nodeDef.Kind switch
        {
            "const" => ParseConstNode(nodeDef),
            "expr" => ParseExprNode(nodeDef),
            "pmf" => ParsePmfNode(nodeDef),
            _ => throw new ModelParseException($"Unknown node kind: {nodeDef.Kind}")
        };
    }
    
    private static INode ParseConstNode(NodeDefinition nodeDef)
    {
        if (nodeDef.Values == null || nodeDef.Values.Length == 0)
            throw new ModelParseException($"Node {nodeDef.Id}: const nodes require values array");
            
        return new ConstSeriesNode(nodeDef.Id, nodeDef.Values);
    }
    
    private static INode ParseExprNode(NodeDefinition nodeDef)
    {
        if (string.IsNullOrWhiteSpace(nodeDef.Expr))
            throw new ModelParseException($"Node {nodeDef.Id}: expr nodes require expr property");
            
        try
        {
            var parser = new Expressions.ExpressionParser(nodeDef.Expr);
            var ast = parser.Parse();
            var exprNode = Expressions.ExpressionCompiler.Compile(ast, nodeDef.Id);
            return exprNode;
        }
        catch (Exception ex)
        {
            throw new ModelParseException($"Node {nodeDef.Id}: error parsing expression '{nodeDef.Expr}': {ex.Message}", ex);
        }
    }

    private static INode ParsePmfNode(NodeDefinition nodeDef)
    {
        if (nodeDef.Pmf == null || nodeDef.Pmf.Count == 0)
            throw new ModelParseException($"Node {nodeDef.Id}: pmf nodes require pmf property with at least one value");

        try
        {
            // Parse PMF dictionary from string keys to double values
            var distribution = new Dictionary<double, double>();
            foreach (var kvp in nodeDef.Pmf)
            {
                if (!double.TryParse(kvp.Key, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                    throw new ModelParseException($"Node {nodeDef.Id}: invalid PMF value '{kvp.Key}' - must be a valid number");
                
                if (distribution.ContainsKey(value))
                    throw new ModelParseException($"Node {nodeDef.Id}: duplicate PMF value '{value}'");
                    
                distribution[value] = kvp.Value;
            }

            var pmf = new Pmf.Pmf(distribution);
            return new Pmf.PmfNode(new NodeId(nodeDef.Id), pmf);
        }
        catch (ArgumentException ex)
        {
            throw new ModelParseException($"Node {nodeDef.Id}: error creating PMF: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            throw new ModelParseException($"Node {nodeDef.Id}: unexpected error parsing PMF: {ex.Message}", ex);
        }
    }
}

/// <summary>
/// Exception thrown when model parsing fails.
/// </summary>
public class ModelParseException : Exception
{
    public ModelParseException(string message) : base(message) { }
    public ModelParseException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Model definition structure - can be deserialized from YAML/JSON.
/// </summary>
public class ModelDefinition
{
    public GridDefinition? Grid { get; set; }
    public List<NodeDefinition> Nodes { get; set; } = new();
    public List<OutputDefinition> Outputs { get; set; } = new();
}

public class GridDefinition
{
    public int Bins { get; set; }
    public int BinMinutes { get; set; }
}

public class NodeDefinition
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "const";
    public double[]? Values { get; set; }
    public string? Expr { get; set; }
    public Dictionary<string, double>? Pmf { get; set; }
}

public class OutputDefinition
{
    public string Series { get; set; } = "";
    public string As { get; set; } = "";
}
