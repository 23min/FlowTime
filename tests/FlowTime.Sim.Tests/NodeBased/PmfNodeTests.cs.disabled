using FlowTime.Sim.Core.Templates;
using Xunit;

namespace FlowTime.Sim.Tests.NodeBased;

[Trait("Category", "NodeBased")]
public class PmfNodeTests
{
    [Fact]
    public void Valid_PMF_Specification_Validates_Successfully()
    {
        var pmfSpec = new PmfSpec
        {
            Values = new[] { 10.0, 20.0, 30.0 },
            Probabilities = new[] { 0.2, 0.5, 0.3 }
        };

        var result = PmfValidator.Validate(pmfSpec);
        
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void PMF_Values_And_Probabilities_Length_Mismatch_Fails_Validation()
    {
        var pmfSpec = new PmfSpec
        {
            Values = new[] { 10.0, 20.0, 30.0 }, // 3 values
            Probabilities = new[] { 0.5, 0.5 }    // 2 probabilities
        };

        var result = PmfValidator.Validate(pmfSpec);
        
        Assert.False(result.IsValid);
        Assert.Contains("Values and probabilities arrays must have the same length", result.Errors);
    }

    [Fact]
    public void PMF_Probabilities_Sum_Not_Equal_To_One_Fails_Validation()
    {
        var pmfSpec = new PmfSpec
        {
            Values = new[] { 10.0, 20.0 },
            Probabilities = new[] { 0.3, 0.4 } // Sum = 0.7
        };

        var result = PmfValidator.Validate(pmfSpec);
        
        Assert.False(result.IsValid);
        Assert.Contains("Probabilities must sum to 1.0", result.Errors);
    }

    [Fact]
    public void PMF_With_Negative_Probabilities_Fails_Validation()
    {
        var pmfSpec = new PmfSpec
        {
            Values = new[] { 10.0, 20.0 },
            Probabilities = new[] { -0.2, 1.2 } // Negative probability
        };

        var result = PmfValidator.Validate(pmfSpec);
        
        Assert.False(result.IsValid);
        Assert.Contains("All probabilities must be non-negative", result.Errors);
    }

    [Fact]
    public void PMF_With_Zero_Values_Is_Valid()
    {
        var pmfSpec = new PmfSpec
        {
            Values = new[] { 0.0, 10.0, 20.0 },
            Probabilities = new[] { 0.1, 0.4, 0.5 }
        };

        var result = PmfValidator.Validate(pmfSpec);
        
        Assert.True(result.IsValid);
    }

    [Fact]
    public void PMF_Node_Preserves_Original_Specification_For_Engine()
    {
        var pmfNode = new TemplateNode
        {
            Id = "preserved_pmf",
            Kind = "pmf",
            Pmf = new PmfSpec
            {
                Values = new[] { 5.0, 15.0, 25.0 },
                Probabilities = new[] { 0.3, 0.4, 0.3 }
            }
        };

        // PMF should be preserved exactly as authored for Engine compilation
        Assert.Equal(new[] { 5.0, 15.0, 25.0 }, pmfNode.Pmf.Values);
        Assert.Equal(new[] { 0.3, 0.4, 0.3 }, pmfNode.Pmf.Probabilities);
        
        // Verify probabilities sum to 1.0 (within tolerance)
        var sum = pmfNode.Pmf.Probabilities.Sum();
        Assert.True(Math.Abs(sum - 1.0) < 1e-10);
    }

    [Fact]
    public void PMF_Node_Metadata_Includes_Hash_For_Provenance()
    {
        var pmfSpec = new PmfSpec
        {
            Values = new[] { 100.0, 200.0 },
            Probabilities = new[] { 0.6, 0.4 }
        };

        var hash1 = PmfHasher.ComputeHash(pmfSpec);
        var hash2 = PmfHasher.ComputeHash(pmfSpec);
        
        // Same PMF should produce same hash
        Assert.Equal(hash1, hash2);
        Assert.NotEmpty(hash1);
        
        // Different PMF should produce different hash
        var differentPmf = new PmfSpec
        {
            Values = new[] { 100.0, 200.0 },
            Probabilities = new[] { 0.5, 0.5 } // Different probabilities
        };
        
        var differentHash = PmfHasher.ComputeHash(differentPmf);
        Assert.NotEqual(hash1, differentHash);
    }

    [Fact]
    public void PMF_Grid_Length_Alignment_Validated()
    {
        var pmfNode = new TemplateNode
        {
            Id = "grid_aligned_pmf",
            Kind = "pmf",
            Pmf = new PmfSpec
            {
                Values = new[] { 10.0, 20.0, 30.0 },
                Probabilities = new[] { 0.2, 0.5, 0.3 }
            }
        };

        var grid = new TemplateGrid
        {
            Bins = 5,
            BinSize = 60,
            BinUnit = "minutes"
        };

        // PMF nodes should have grid alignment policy
        var compilationContext = new NodeCompilationContext
        {
            Grid = grid,
            PmfPolicy = PmfGridPolicy.Repeat // or Error
        };

        // This should not throw - PMF grid alignment is handled by Engine
        var compiled = NodeCompiler.CompileNode(pmfNode, grid, compilationContext);
        Assert.NotNull(compiled.PmfSpecification);
        Assert.Equal(PmfGridPolicy.Repeat, compiled.GridAlignmentPolicy);
    }
}