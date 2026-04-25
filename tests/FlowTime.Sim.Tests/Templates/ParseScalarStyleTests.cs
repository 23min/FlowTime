using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using FlowTime.Sim.Core.Templates;
using Xunit;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace FlowTime.Sim.Tests.Templates;

/// <summary>
/// m-E24-04 — <see cref="TemplateSchemaValidator.ParseScalar"/> must honor
/// <see cref="ScalarStyle"/>. Mirrors <c>tests/FlowTime.Core.Tests/Validation/ParseScalarStyleTests.cs</c>
/// against the Sim-side validator. The two validators must agree by construction; if the
/// Engine validator's guard ever drifts from the Sim validator's guard, the test-class
/// pair surfaces it.
/// </summary>
public sealed class ParseScalarStyleTests
{
    private static YamlScalarNode LoadScalar(string yaml)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));
        var root = (YamlMappingNode)stream.Documents[0].RootNode;
        return (YamlScalarNode)root.Children[new YamlScalarNode("v")];
    }

    // ─── Plain scalars: existing coercion order preserved ─────────────────────

    [Fact]
    public void Plain_UnquotedTrue_ResolvesAsBool()
    {
        var scalar = LoadScalar("v: true\n");
        Assert.Equal(ScalarStyle.Plain, scalar.Style);

        var node = TemplateSchemaValidator.ParseScalar(scalar);

        Assert.Equal(JsonValueKind.True, node.GetValueKind());
        Assert.True(node.GetValue<bool>());
    }

    [Fact]
    public void Plain_UnquotedFalse_ResolvesAsBool()
    {
        var scalar = LoadScalar("v: false\n");
        Assert.Equal(ScalarStyle.Plain, scalar.Style);

        var node = TemplateSchemaValidator.ParseScalar(scalar);

        Assert.Equal(JsonValueKind.False, node.GetValueKind());
        Assert.False(node.GetValue<bool>());
    }

    [Fact]
    public void Plain_UnquotedInteger_ResolvesAsInteger()
    {
        var scalar = LoadScalar("v: 42\n");
        Assert.Equal(ScalarStyle.Plain, scalar.Style);

        var node = TemplateSchemaValidator.ParseScalar(scalar);

        Assert.Equal(JsonValueKind.Number, node.GetValueKind());
        Assert.Equal(42, node.GetValue<int>());
    }

    [Fact]
    public void Plain_UnquotedDouble_ResolvesAsDouble()
    {
        var scalar = LoadScalar("v: 3.14\n");
        Assert.Equal(ScalarStyle.Plain, scalar.Style);

        var node = TemplateSchemaValidator.ParseScalar(scalar);

        Assert.Equal(JsonValueKind.Number, node.GetValueKind());
        Assert.Equal(3.14, node.GetValue<double>(), 5);
    }

    [Fact]
    public void Plain_UnquotedText_ResolvesAsString()
    {
        var scalar = LoadScalar("v: hello\n");
        Assert.Equal(ScalarStyle.Plain, scalar.Style);

        var node = TemplateSchemaValidator.ParseScalar(scalar);

        Assert.Equal(JsonValueKind.String, node.GetValueKind());
        Assert.Equal("hello", node.GetValue<string>());
    }

    // ─── Quoted scalars: must resolve as string regardless of literal text ────

    [Fact]
    public void DoubleQuoted_BooleanLiteral_ResolvesAsString()
    {
        var scalar = LoadScalar("v: \"true\"\n");
        Assert.Equal(ScalarStyle.DoubleQuoted, scalar.Style);

        var node = TemplateSchemaValidator.ParseScalar(scalar);

        Assert.Equal(JsonValueKind.String, node.GetValueKind());
        Assert.Equal("true", node.GetValue<string>());
    }

    [Fact]
    public void DoubleQuoted_IntegerLiteral_ResolvesAsString()
    {
        var scalar = LoadScalar("v: \"42\"\n");
        Assert.Equal(ScalarStyle.DoubleQuoted, scalar.Style);

        var node = TemplateSchemaValidator.ParseScalar(scalar);

        Assert.Equal(JsonValueKind.String, node.GetValueKind());
        Assert.Equal("42", node.GetValue<string>());
    }

    [Fact]
    public void DoubleQuoted_DoubleLiteral_ResolvesAsString()
    {
        var scalar = LoadScalar("v: \"3.14\"\n");
        Assert.Equal(ScalarStyle.DoubleQuoted, scalar.Style);

        var node = TemplateSchemaValidator.ParseScalar(scalar);

        Assert.Equal(JsonValueKind.String, node.GetValueKind());
        Assert.Equal("3.14", node.GetValue<string>());
    }

    [Fact]
    public void DoubleQuoted_NullLiteral_ResolvesAsFourCharacterString()
    {
        var scalar = LoadScalar("v: \"null\"\n");
        Assert.Equal(ScalarStyle.DoubleQuoted, scalar.Style);

        var node = TemplateSchemaValidator.ParseScalar(scalar);

        Assert.Equal(JsonValueKind.String, node.GetValueKind());
        Assert.Equal("null", node.GetValue<string>());
    }

    [Fact]
    public void SingleQuoted_BooleanLiteral_ResolvesAsString()
    {
        var scalar = LoadScalar("v: 'false'\n");
        Assert.Equal(ScalarStyle.SingleQuoted, scalar.Style);

        var node = TemplateSchemaValidator.ParseScalar(scalar);

        Assert.Equal(JsonValueKind.String, node.GetValueKind());
        Assert.Equal("false", node.GetValue<string>());
    }

    [Fact]
    public void SingleQuoted_IntegerLiteral_ResolvesAsString()
    {
        var scalar = LoadScalar("v: '0'\n");
        Assert.Equal(ScalarStyle.SingleQuoted, scalar.Style);

        var node = TemplateSchemaValidator.ParseScalar(scalar);

        Assert.Equal(JsonValueKind.String, node.GetValueKind());
        Assert.Equal("0", node.GetValue<string>());
    }

    [Fact]
    public void SingleQuoted_DoubleLiteral_ResolvesAsString()
    {
        var scalar = LoadScalar("v: '2.5'\n");
        Assert.Equal(ScalarStyle.SingleQuoted, scalar.Style);

        var node = TemplateSchemaValidator.ParseScalar(scalar);

        Assert.Equal(JsonValueKind.String, node.GetValueKind());
        Assert.Equal("2.5", node.GetValue<string>());
    }

    // ─── Block scalars: literal | and folded > resolve as string ──────────────

    [Fact]
    public void Literal_BlockScalar_BooleanLiteral_ResolvesAsString()
    {
        var scalar = LoadScalar("v: |-\n  true\n");
        Assert.Equal(ScalarStyle.Literal, scalar.Style);

        var node = TemplateSchemaValidator.ParseScalar(scalar);

        Assert.Equal(JsonValueKind.String, node.GetValueKind());
        Assert.Equal("true", node.GetValue<string>());
    }

    [Fact]
    public void Folded_BlockScalar_IntegerLiteral_ResolvesAsString()
    {
        var scalar = LoadScalar("v: >-\n  42\n");
        Assert.Equal(ScalarStyle.Folded, scalar.Style);

        var node = TemplateSchemaValidator.ParseScalar(scalar);

        Assert.Equal(JsonValueKind.String, node.GetValueKind());
        Assert.Equal("42", node.GetValue<string>());
    }

    // ─── Null handling ────────────────────────────────────────────────────────
    //
    // Mirrors the Engine-side null tests. m-E24-04 deliberately does not recognize the
    // YAML null keyword (D-m-E24-04-01) — that would be a second inference layer.

    [Fact]
    public void Plain_NullKeyword_ResolvesAsLiteralString()
    {
        var scalar = LoadScalar("v: null\n");
        Assert.Equal(ScalarStyle.Plain, scalar.Style);

        var node = TemplateSchemaValidator.ParseScalar(scalar);

        Assert.Equal(JsonValueKind.String, node.GetValueKind());
        Assert.Equal("null", node.GetValue<string>());
    }

    [Fact]
    public void Plain_TildeShorthand_ResolvesAsLiteralString()
    {
        var scalar = LoadScalar("v: ~\n");
        Assert.Equal(ScalarStyle.Plain, scalar.Style);

        var node = TemplateSchemaValidator.ParseScalar(scalar);

        Assert.Equal(JsonValueKind.String, node.GetValueKind());
        Assert.Equal("~", node.GetValue<string>());
    }

    [Fact]
    public void EmptyMappingValue_ResolvesAsEmptyString()
    {
        // YamlDotNet returns `Value = ""` for an empty mapping value; the `value is null`
        // short-circuit at the top of ParseScalar is defensive, not exercised by the public
        // surface today. Pin observable behavior here.
        var scalar = LoadScalar("v:\n");
        Assert.Equal(string.Empty, scalar.Value);

        var node = TemplateSchemaValidator.ParseScalar(scalar);

        Assert.Equal(JsonValueKind.String, node.GetValueKind());
        Assert.Equal(string.Empty, node.GetValue<string>());
    }

    // Note: the `value is null` short-circuit at the top of ParseScalar is a pre-existing
    // defensive branch never exercised by normal YamlDotNet parsing. Mirrors the Engine-side
    // commentary; latent JsonNode-null gotcha is out of m-E24-04 scope (D-m-E24-04-02).
}
