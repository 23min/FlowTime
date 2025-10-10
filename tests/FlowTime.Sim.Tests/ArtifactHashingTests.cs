using FlowTime.Sim.Core;
using Xunit;

namespace FlowTime.Sim.Tests;

public class ArtifactHashingTests
{
    private const string BaseYaml = @"schemaVersion: 1
grid: { bins: 3, binSize: 1, binUnit: hours }
nodes:
  - id: source
    kind: const
    values: [1,2,3]
outputs:
  - id: result
    source: source
    filename: output.csv
";

    [Fact]
    public void SameContentDifferentFormatting_ProducesSameHash()
    {
    // NOTE: Slice 1 canonicalization does not yet reorder keys; test only whitespace & comment insensitivity.
    var variant = "# leading comment\n" + BaseYaml + "\n# trailing comment\n";
    var h1 = ModelHasher.ComputeModelHash(BaseYaml);
    var h2 = ModelHasher.ComputeModelHash(variant);
    Assert.Equal(h1, h2);
    }

    [Fact]
    public void ChangingNumericLiteral_ChangesHash()
    {
        var modified = BaseYaml.Replace("values: [1,2,3]", "values: [1,2,4]");
        var h1 = ModelHasher.ComputeModelHash(BaseYaml);
        var h2 = ModelHasher.ComputeModelHash(modified);
        Assert.NotEqual(h1, h2);
    }

    [Fact]
    public void EmptyYaml_Throws()
    {
        Assert.Throws<ArgumentException>(() => ModelHasher.ComputeModelHash("   \n  \n"));
    }

    [Fact]
    public void InvalidYaml_Throws()
    {
        var invalid = "grid: { bins: 3, binSize: 60  # missing closing brace";
        Assert.Throws<InvalidOperationException>(() => ModelHasher.ComputeModelHash(invalid));
    }
}