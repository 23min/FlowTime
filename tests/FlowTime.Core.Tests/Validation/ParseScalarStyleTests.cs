using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using FlowTime.Core;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace FlowTime.Core.Tests.Validation;

/// <summary>
/// m-E24-04 — <see cref="ModelSchemaValidator.ParseScalar"/> must honor
/// <see cref="ScalarStyle"/>. YAML 1.2: quoted scalars (<see cref="ScalarStyle.SingleQuoted"/>,
/// <see cref="ScalarStyle.DoubleQuoted"/>) and block scalars (<see cref="ScalarStyle.Literal"/>,
/// <see cref="ScalarStyle.Folded"/>) are explicitly typed as strings; only
/// <see cref="ScalarStyle.Plain"/> scalars are candidates for bool/int/double coercion.
///
/// Each test exercises a single scalar style × candidate-coercion-target combination by
/// authoring a tiny YAML stream, parsing it via <see cref="YamlStream"/>, extracting the
/// representative <see cref="YamlScalarNode"/>, and asserting the resolved JSON node kind.
/// The branch coverage matrix below is dense by design — every reachable conditional in
/// the post-fix <c>ParseScalar</c> body must be exercised by at least one test.
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

        var node = ModelSchemaValidator.ParseScalar(scalar);

        Assert.Equal(JsonValueKind.True, node.GetValueKind());
        Assert.True(node.GetValue<bool>());
    }

    [Fact]
    public void Plain_UnquotedFalse_ResolvesAsBool()
    {
        var scalar = LoadScalar("v: false\n");
        Assert.Equal(ScalarStyle.Plain, scalar.Style);

        var node = ModelSchemaValidator.ParseScalar(scalar);

        Assert.Equal(JsonValueKind.False, node.GetValueKind());
        Assert.False(node.GetValue<bool>());
    }

    [Fact]
    public void Plain_UnquotedInteger_ResolvesAsInteger()
    {
        var scalar = LoadScalar("v: 42\n");
        Assert.Equal(ScalarStyle.Plain, scalar.Style);

        var node = ModelSchemaValidator.ParseScalar(scalar);

        Assert.Equal(JsonValueKind.Number, node.GetValueKind());
        Assert.Equal(42, node.GetValue<int>());
    }

    [Fact]
    public void Plain_UnquotedDouble_ResolvesAsDouble()
    {
        var scalar = LoadScalar("v: 3.14\n");
        Assert.Equal(ScalarStyle.Plain, scalar.Style);

        var node = ModelSchemaValidator.ParseScalar(scalar);

        Assert.Equal(JsonValueKind.Number, node.GetValueKind());
        Assert.Equal(3.14, node.GetValue<double>(), 5);
    }

    [Fact]
    public void Plain_UnquotedText_ResolvesAsString()
    {
        var scalar = LoadScalar("v: hello\n");
        Assert.Equal(ScalarStyle.Plain, scalar.Style);

        var node = ModelSchemaValidator.ParseScalar(scalar);

        Assert.Equal(JsonValueKind.String, node.GetValueKind());
        Assert.Equal("hello", node.GetValue<string>());
    }

    // ─── Quoted scalars: must resolve as string regardless of literal text ────

    [Fact]
    public void DoubleQuoted_BooleanLiteral_ResolvesAsString()
    {
        var scalar = LoadScalar("v: \"true\"\n");
        Assert.Equal(ScalarStyle.DoubleQuoted, scalar.Style);

        var node = ModelSchemaValidator.ParseScalar(scalar);

        Assert.Equal(JsonValueKind.String, node.GetValueKind());
        Assert.Equal("true", node.GetValue<string>());
    }

    [Fact]
    public void DoubleQuoted_IntegerLiteral_ResolvesAsString()
    {
        var scalar = LoadScalar("v: \"42\"\n");
        Assert.Equal(ScalarStyle.DoubleQuoted, scalar.Style);

        var node = ModelSchemaValidator.ParseScalar(scalar);

        Assert.Equal(JsonValueKind.String, node.GetValueKind());
        Assert.Equal("42", node.GetValue<string>());
    }

    [Fact]
    public void DoubleQuoted_DoubleLiteral_ResolvesAsString()
    {
        var scalar = LoadScalar("v: \"3.14\"\n");
        Assert.Equal(ScalarStyle.DoubleQuoted, scalar.Style);

        var node = ModelSchemaValidator.ParseScalar(scalar);

        Assert.Equal(JsonValueKind.String, node.GetValueKind());
        Assert.Equal("3.14", node.GetValue<string>());
    }

    [Fact]
    public void DoubleQuoted_NullLiteral_ResolvesAsFourCharacterString()
    {
        var scalar = LoadScalar("v: \"null\"\n");
        Assert.Equal(ScalarStyle.DoubleQuoted, scalar.Style);

        var node = ModelSchemaValidator.ParseScalar(scalar);

        Assert.Equal(JsonValueKind.String, node.GetValueKind());
        Assert.Equal("null", node.GetValue<string>());
    }

    [Fact]
    public void SingleQuoted_BooleanLiteral_ResolvesAsString()
    {
        var scalar = LoadScalar("v: 'false'\n");
        Assert.Equal(ScalarStyle.SingleQuoted, scalar.Style);

        var node = ModelSchemaValidator.ParseScalar(scalar);

        Assert.Equal(JsonValueKind.String, node.GetValueKind());
        Assert.Equal("false", node.GetValue<string>());
    }

    [Fact]
    public void SingleQuoted_IntegerLiteral_ResolvesAsString()
    {
        var scalar = LoadScalar("v: '0'\n");
        Assert.Equal(ScalarStyle.SingleQuoted, scalar.Style);

        var node = ModelSchemaValidator.ParseScalar(scalar);

        Assert.Equal(JsonValueKind.String, node.GetValueKind());
        Assert.Equal("0", node.GetValue<string>());
    }

    [Fact]
    public void SingleQuoted_DoubleLiteral_ResolvesAsString()
    {
        var scalar = LoadScalar("v: '2.5'\n");
        Assert.Equal(ScalarStyle.SingleQuoted, scalar.Style);

        var node = ModelSchemaValidator.ParseScalar(scalar);

        Assert.Equal(JsonValueKind.String, node.GetValueKind());
        Assert.Equal("2.5", node.GetValue<string>());
    }

    // ─── Block scalars: literal | and folded > resolve as string ──────────────

    [Fact]
    public void Literal_BlockScalar_BooleanLiteral_ResolvesAsString()
    {
        // |- strips trailing newline so the resolved string is exactly "true".
        var scalar = LoadScalar("v: |-\n  true\n");
        Assert.Equal(ScalarStyle.Literal, scalar.Style);

        var node = ModelSchemaValidator.ParseScalar(scalar);

        Assert.Equal(JsonValueKind.String, node.GetValueKind());
        Assert.Equal("true", node.GetValue<string>());
    }

    [Fact]
    public void Folded_BlockScalar_IntegerLiteral_ResolvesAsString()
    {
        var scalar = LoadScalar("v: >-\n  42\n");
        Assert.Equal(ScalarStyle.Folded, scalar.Style);

        var node = ModelSchemaValidator.ParseScalar(scalar);

        Assert.Equal(JsonValueKind.String, node.GetValueKind());
        Assert.Equal("42", node.GetValue<string>());
    }

    // ─── Null handling ────────────────────────────────────────────────────────
    //
    // YamlDotNet's RepresentationModel does not apply YAML's tag resolver; literal `~` and
    // `null` keywords surface as plain scalars whose `Value` is the literal text. The
    // `value is null` guard at the top of ParseScalar fires only when YamlDotNet hands back
    // a YamlScalarNode whose `Value` is C# null — this happens for an empty mapping value.
    // m-E24-04 deliberately does NOT widen the validator to recognize the YAML null keyword
    // (D-m-E24-04-01) — that would be a second inference layer, explicitly rejected by the
    // spec's Constraints section.

    [Fact]
    public void Plain_NullKeyword_ResolvesAsLiteralString()
    {
        var scalar = LoadScalar("v: null\n");
        Assert.Equal(ScalarStyle.Plain, scalar.Style);

        var node = ModelSchemaValidator.ParseScalar(scalar);

        // Pinning behavior: `null` keyword surfaces as a plain scalar with literal text "null"
        // and resolves through the string fall-through branch. Not widened by m-E24-04.
        Assert.Equal(JsonValueKind.String, node.GetValueKind());
        Assert.Equal("null", node.GetValue<string>());
    }

    [Fact]
    public void Plain_TildeShorthand_ResolvesAsLiteralString()
    {
        var scalar = LoadScalar("v: ~\n");
        Assert.Equal(ScalarStyle.Plain, scalar.Style);

        var node = ModelSchemaValidator.ParseScalar(scalar);

        // Pinning behavior: `~` (YAML's null shorthand) surfaces with `Value="~"`. Not widened.
        Assert.Equal(JsonValueKind.String, node.GetValueKind());
        Assert.Equal("~", node.GetValue<string>());
    }

    [Fact]
    public void EmptyMappingValue_ResolvesAsEmptyString()
    {
        // YamlDotNet's RepresentationModel returns `Value = ""` for an empty mapping value
        // (not C# null). The `value is null` short-circuit at the top of ParseScalar is
        // therefore defensive — kept against future YamlDotNet shape changes but not
        // exercised by the public surface today. This test pins the observable behavior.
        var scalar = LoadScalar("v:\n");
        Assert.Equal(string.Empty, scalar.Value);

        var node = ModelSchemaValidator.ParseScalar(scalar);

        Assert.Equal(JsonValueKind.String, node.GetValueKind());
        Assert.Equal(string.Empty, node.GetValue<string>());
    }

    // Note: the `value is null` short-circuit at the top of ParseScalar is a pre-existing
    // defensive branch that YamlDotNet's RepresentationModel never exercises through normal
    // parsing (empty mapping values surface with Value=""; see EmptyMappingValue_ResolvesAsEmptyString).
    // Calling it directly returns a C# null JsonNode (`JsonValue.Create((string?)null)!` is a
    // null-forgiving wrap on a value that JsonValue.Create resolves to C# null) — that latent
    // gotcha is pre-existing behavior, not introduced or modified by m-E24-04, and is outside
    // this milestone's scope. Recorded as D-m-E24-04-02.

    // ─── End-to-end behavior: defect-shape via Validate(...) ──────────────────

    /// <summary>
    /// End-to-end sanity for AC1: a model authored with a quoted integer in <c>nodes[].expr</c>
    /// must validate cleanly under the post-fix validator (the schema declares <c>expr</c> as
    /// <c>type: string</c>; a quoted "0" is a string).
    /// </summary>
    [Fact]
    public void Validate_NodeExprAsQuotedInteger_PassesSchemaTypeCheck()
    {
        const string yaml = """
            schemaVersion: 1
            grid:
              bins: 1
              binSize: 1
              binUnit: hours
            nodes:
              - id: n
                kind: const
                values: [0]
                expr: "0"
            outputs:
              - series: n
            """;

        var result = ModelSchemaValidator.Validate(yaml);

        // The /nodes/*/expr "Value is integer but should be string" defect is the
        // 89-error shape in the m-E24-03 canary; after the m-E24-04 fix, this shape
        // does not appear.
        Assert.DoesNotContain(result.Errors, e =>
            e.Contains("/nodes/0/expr", System.StringComparison.Ordinal) &&
            e.Contains("\"integer\"", System.StringComparison.Ordinal));
    }
}
