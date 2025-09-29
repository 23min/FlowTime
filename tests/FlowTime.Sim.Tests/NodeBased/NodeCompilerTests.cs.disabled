using FlowTime.Sim.Core.Templates;
using Xunit;

namespace FlowTime.Sim.Tests.NodeBased;

[Trait("Category", "NodeBased")]
public class NodeCompilerTests
{
    [Fact]
    public void Const_Node_Compiles_To_Value_Series()
    {
        var node = new TemplateNode
        {
            Id = "test_const",
            Kind = "const",
            Values = new[] { 10.0, 20.0, 30.0 }
        };
        
        var grid = new TemplateGrid
        {
            Bins = 3,
            BinSize = 60,
            BinUnit = "minutes"
        };

        var compiled = NodeCompiler.CompileNode(node, grid);
        
        Assert.Equal("test_const", compiled.Id);
        Assert.Equal("const", compiled.Kind);
        Assert.Equal(new[] { 10.0, 20.0, 30.0 }, compiled.CompiledValues);
        Assert.Equal(3, compiled.CompiledValues.Length);
    }

    [Fact]
    public void PMF_Node_Preserves_PMF_Specification_For_Engine()
    {
        var node = new TemplateNode
        {
            Id = "test_pmf",
            Kind = "pmf",
            Pmf = new PmfSpec
            {
                Values = new[] { 5.0, 10.0, 15.0 },
                Probabilities = new[] { 0.2, 0.5, 0.3 }
            }
        };
        
        var grid = new TemplateGrid
        {
            Bins = 4,
            BinSize = 30,
            BinUnit = "minutes"
        };

        var compiled = NodeCompiler.CompileNode(node, grid);
        
        Assert.Equal("test_pmf", compiled.Id);
        Assert.Equal("pmf", compiled.Kind);
        Assert.NotNull(compiled.PmfSpecification);
        Assert.Equal(new[] { 5.0, 10.0, 15.0 }, compiled.PmfSpecification.Values);
        Assert.Equal(new[] { 0.2, 0.5, 0.3 }, compiled.PmfSpecification.Probabilities);
        
        // PMF nodes should not have pre-compiled values - Engine compiles them
        Assert.Null(compiled.CompiledValues);
    }

    [Fact]
    public void Expression_Node_Tracks_Dependencies()
    {
        var node = new TemplateNode
        {
            Id = "test_expr",
            Kind = "expr",
            Expression = "base_rate * 2.0",
            Dependencies = new[] { "base_rate" }
        };
        
        var grid = new TemplateGrid
        {
            Bins = 3,
            BinSize = 60,
            BinUnit = "minutes"
        };

        var compiled = NodeCompiler.CompileNode(node, grid);
        
        Assert.Equal("test_expr", compiled.Id);
        Assert.Equal("expr", compiled.Kind);
        Assert.Equal("base_rate * 2.0", compiled.Expression);
        Assert.Equal(new[] { "base_rate" }, compiled.Dependencies);
        
        // Expression nodes don't have compiled values until dependencies are resolved
        Assert.Null(compiled.CompiledValues);
    }

    [Fact]
    public void Const_Node_With_Single_Value_Repeats_Across_Grid()
    {
        var node = new TemplateNode
        {
            Id = "single_const",
            Kind = "const",
            Values = new[] { 100.0 } // Single value
        };
        
        var grid = new TemplateGrid
        {
            Bins = 5,
            BinSize = 60,
            BinUnit = "minutes"
        };

        var compiled = NodeCompiler.CompileNode(node, grid);
        
        Assert.Equal(new[] { 100.0, 100.0, 100.0, 100.0, 100.0 }, compiled.CompiledValues);
    }

    [Fact]
    public void Node_Values_Length_Mismatch_With_Grid_Throws_Exception()
    {
        var node = new TemplateNode
        {
            Id = "mismatched_const",
            Kind = "const",
            Values = new[] { 10.0, 20.0 } // 2 values
        };
        
        var grid = new TemplateGrid
        {
            Bins = 5, // 5 bins
            BinSize = 60,
            BinUnit = "minutes"
        };

        Assert.Throws<NodeCompilationException>(() => NodeCompiler.CompileNode(node, grid));
    }

    [Fact]
    public void PMF_Probabilities_Must_Sum_To_One()
    {
        var node = new TemplateNode
        {
            Id = "invalid_pmf",
            Kind = "pmf",
            Pmf = new PmfSpec
            {
                Values = new[] { 5.0, 10.0 },
                Probabilities = new[] { 0.3, 0.5 } // Sum = 0.8, not 1.0
            }
        };
        
        var grid = new TemplateGrid
        {
            Bins = 3,
            BinSize = 60,
            BinUnit = "minutes"
        };

        Assert.Throws<NodeValidationException>(() => NodeCompiler.CompileNode(node, grid));
    }
}