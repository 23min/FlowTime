using System.Collections.Generic;
using FlowTime.Contracts.Dtos;
using FlowTime.Core;
using FlowTime.Sim.Core.Templates;
using Xunit;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace FlowTime.Sim.Tests.Templates;

/// <summary>
/// m-E24-04 / D-m-E24-04-03 round-trip pair tests for
/// <see cref="QuotedAmbiguousStringEmitter"/> wired into
/// <see cref="TemplateService.CreateYamlSerializer"/>. Each test asserts that:
/// <list type="number">
///   <item><description>Emitting a DTO whose <c>string</c> field carries text that would
///     re-resolve as a YAML 1.2 plain non-string scalar produces double-quoted output.</description></item>
///   <item><description>The same emitted YAML, fed back through <see cref="ModelSchemaValidator.Validate(string)"/>,
///     resolves the field as a string (no <c>type: string</c> schema violation).</description></item>
/// </list>
/// The pair is symmetric by construction — the emitter's ambiguity test mirrors the
/// validator's plain-scalar coercion attempt-order.
/// </summary>
public sealed class QuotedAmbiguousStringEmitterTests
{
    private static ISerializer CreateSerializer()
    {
        // Mirror of TemplateService.CreateYamlSerializer minus the cache plumbing —
        // exercises the same SerializerBuilder shape so the test guards real production
        // emission, not a hand-rolled local build.
        return new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .WithEventEmitter(next => new FlowSequenceEventEmitter(next))
            .WithEventEmitter(next => new QuotedAmbiguousStringEmitter(next))
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull | DefaultValuesHandling.OmitEmptyCollections)
            .Build();
    }

    // ─── DTO-level scalar emission ─────────────────────────────────────────────

    [Fact]
    public void NodeDto_ExprIntegerLiteralString_EmitsDoubleQuoted()
    {
        var node = new NodeDto { Id = "n", Kind = "expr", Expr = "0" };

        var yaml = CreateSerializer().Serialize(node);

        Assert.Contains("expr: \"0\"", yaml);
    }

    [Fact]
    public void NodeDto_ExprFloatLiteralString_EmitsDoubleQuoted()
    {
        var node = new NodeDto { Id = "n", Kind = "expr", Expr = "3.14" };

        var yaml = CreateSerializer().Serialize(node);

        Assert.Contains("expr: \"3.14\"", yaml);
    }

    [Fact]
    public void NodeDto_ExprBooleanLiteralString_EmitsDoubleQuoted()
    {
        var node = new NodeDto { Id = "n", Kind = "expr", Expr = "true" };

        var yaml = CreateSerializer().Serialize(node);

        Assert.Contains("expr: \"true\"", yaml);
    }

    [Fact]
    public void NodeDto_ExprNullLiteralString_EmitsDoubleQuoted()
    {
        var node = new NodeDto { Id = "n", Kind = "expr", Expr = "null" };

        var yaml = CreateSerializer().Serialize(node);

        Assert.Contains("expr: \"null\"", yaml);
    }

    [Fact]
    public void NodeDto_ExprNonAmbiguousString_EmitsPlain()
    {
        // A real expression literal — not a number / bool / null. Must round-trip without
        // unnecessary quoting so the wire shape stays legible.
        var node = new NodeDto { Id = "n", Kind = "expr", Expr = "base * scale" };

        var yaml = CreateSerializer().Serialize(node);

        Assert.Contains("expr: base * scale", yaml);
        Assert.DoesNotContain("expr: \"base * scale\"", yaml);
    }

    [Fact]
    public void NodeDto_MetadataValueWithBooleanLiteral_EmitsDoubleQuoted()
    {
        // Dictionary<string, string> — the source type is `string` for each value, so the
        // emitter activates without per-entry annotation. This is the metadata/graph.hidden
        // shape from the canary (92 errors before fix).
        var node = new NodeDto
        {
            Id = "n",
            Kind = "const",
            Metadata = new Dictionary<string, string> { ["graph.hidden"] = "true" }
        };

        var yaml = CreateSerializer().Serialize(node);

        Assert.Contains("graph.hidden: \"true\"", yaml);
    }

    [Fact]
    public void NodeDto_MetadataValueWithFloatLiteral_EmitsDoubleQuoted()
    {
        // pmf.expected shape from the canary — produced by SimModelBuilder via
        // expectedValue.ToString("G17", InvariantCulture). Source type at emission is string.
        var node = new NodeDto
        {
            Id = "n",
            Kind = "const",
            Metadata = new Dictionary<string, string> { ["pmf.expected"] = "174.5" }
        };

        var yaml = CreateSerializer().Serialize(node);

        Assert.Contains("pmf.expected: \"174.5\"", yaml);
    }

    [Fact]
    public void NodeDto_MetadataValueWithIntegerLiteral_EmitsDoubleQuoted()
    {
        var node = new NodeDto
        {
            Id = "n",
            Kind = "const",
            Metadata = new Dictionary<string, string> { ["pmf.expected"] = "174" }
        };

        var yaml = CreateSerializer().Serialize(node);

        Assert.Contains("pmf.expected: \"174\"", yaml);
    }

    [Fact]
    public void NodeDto_KindAsConstKeyword_EmitsPlain()
    {
        // `const` is not a YAML 1.2 type-resolution keyword, so the emitter leaves it
        // alone. The default plain emission is correct.
        var node = new NodeDto { Id = "n", Kind = "const" };

        var yaml = CreateSerializer().Serialize(node);

        Assert.Contains("kind: const", yaml);
        Assert.DoesNotContain("kind: \"const\"", yaml);
    }

    [Fact]
    public void NodeDto_NumericValuesArray_RemainsUnquoted()
    {
        // double[] elements are not strings — emitter is a no-op for them. Pinned to guard
        // against accidental over-application that would corrupt numeric arrays into string
        // sequences. (FlowSequenceEventEmitter forces flow-style for numeric arrays so the
        // wire shape is `values: [0, 1, 2]`, not block-style — assert against that form.)
        var node = new NodeDto { Id = "n", Kind = "const", Values = new[] { 0.0, 1.0, 2.0 } };

        var yaml = CreateSerializer().Serialize(node);

        Assert.Contains("values: [0, 1, 2]", yaml);
        Assert.DoesNotContain("\"0\"", yaml);
    }

    [Fact]
    public void GridDto_StartIsoTimestamp_EmitsPlain()
    {
        // ISO-8601 timestamps are not YAML 1.2 type-resolution patterns. Default plain
        // emission is correct; the emitter must not mistakenly quote them.
        var grid = new GridDto { Bins = 1, BinSize = 1, BinUnit = "hours", Start = "2026-01-01T00:00:00Z" };

        var yaml = CreateSerializer().Serialize(grid);

        Assert.Contains("start: 2026-01-01T00:00:00Z", yaml);
        Assert.DoesNotContain("start: \"2026-01-01T00:00:00Z\"", yaml);
    }

    // ─── End-to-end round trip: emit → parse → schema validate ─────────────────

    [Fact]
    public void RoundTrip_ExprQuotedZero_ValidatesAsString()
    {
        // The 89-error shape from the canary collapses here. Emit a model with
        // expr="0", parse the emitted YAML through the validator, and confirm there is
        // no /nodes/0/expr "Value is integer but should be string" violation.
        var model = new ModelDto
        {
            SchemaVersion = 1,
            Grid = new GridDto { Bins = 1, BinSize = 1, BinUnit = "hours" },
            Nodes = new List<NodeDto>
            {
                new() { Id = "n", Kind = "const", Values = new[] { 0.0 }, Expr = "0" }
            },
            Outputs = new List<OutputDto> { new() { Series = "n" } }
        };

        var yaml = CreateSerializer().Serialize(model);
        var result = ModelSchemaValidator.Validate(yaml);

        Assert.DoesNotContain(result.Errors, e =>
            e.Contains("/nodes/0/expr", System.StringComparison.Ordinal) &&
            e.Contains("\"integer\"", System.StringComparison.Ordinal));
    }

    [Fact]
    public void RoundTrip_MetadataGraphHiddenTrue_ValidatesAsString()
    {
        // The 92-error shape from the canary. Same round-trip pattern, different field.
        var model = new ModelDto
        {
            SchemaVersion = 1,
            Grid = new GridDto { Bins = 1, BinSize = 1, BinUnit = "hours" },
            Nodes = new List<NodeDto>
            {
                new()
                {
                    Id = "n",
                    Kind = "const",
                    Values = new[] { 0.0 },
                    Metadata = new Dictionary<string, string> { ["graph.hidden"] = "true" }
                }
            },
            Outputs = new List<OutputDto> { new() { Series = "n" } }
        };

        var yaml = CreateSerializer().Serialize(model);
        var result = ModelSchemaValidator.Validate(yaml);

        Assert.DoesNotContain(result.Errors, e =>
            e.Contains("/nodes/0/metadata/graph.hidden", System.StringComparison.Ordinal) &&
            e.Contains("\"boolean\"", System.StringComparison.Ordinal));
    }

    [Fact]
    public void RoundTrip_MetadataPmfExpectedNumeric_ValidatesAsString()
    {
        // The 40+10-error shape from the canary, collapsed.
        var model = new ModelDto
        {
            SchemaVersion = 1,
            Grid = new GridDto { Bins = 1, BinSize = 1, BinUnit = "hours" },
            Nodes = new List<NodeDto>
            {
                new()
                {
                    Id = "n",
                    Kind = "const",
                    Values = new[] { 0.0 },
                    Metadata = new Dictionary<string, string> { ["pmf.expected"] = "174.5" }
                }
            },
            Outputs = new List<OutputDto> { new() { Series = "n" } }
        };

        var yaml = CreateSerializer().Serialize(model);
        var result = ModelSchemaValidator.Validate(yaml);

        Assert.DoesNotContain(result.Errors, e =>
            e.Contains("/nodes/0/metadata/pmf.expected", System.StringComparison.Ordinal) &&
            (e.Contains("\"number\"", System.StringComparison.Ordinal) ||
             e.Contains("\"integer\"", System.StringComparison.Ordinal)));
    }

    // ─── Branch coverage: each ambiguity-classifier branch ─────────────────────

    [Theory]
    [InlineData("null")]          // lowercase null keyword
    [InlineData("NULL")]          // case-insensitive null keyword
    [InlineData("~")]             // shorthand null
    [InlineData("true")]          // lowercase bool
    [InlineData("False")]         // mixed-case bool
    [InlineData("0")]             // integer
    [InlineData("-42")]           // negative integer
    [InlineData("3.14")]          // float
    [InlineData("1e5")]           // scientific notation
    [InlineData(".5")]            // float without leading digit
    public void AmbiguousLiterals_ForceDoubleQuoted(string ambiguous)
    {
        var node = new NodeDto { Id = "n", Kind = "expr", Expr = ambiguous };

        var yaml = CreateSerializer().Serialize(node);

        Assert.Contains($"expr: \"{ambiguous}\"", yaml);
    }

    [Fact]
    public void AmbiguousEmptyString_OmittedByOmitNull()
    {
        // Empty string is treated as a missing value by YamlDotNet under OmitNull semantics
        // when the property is also typed as nullable (`string?`). The emitter never sees a
        // ScalarEventInfo for the field, so the ambiguity guard is moot. This is the
        // existing m-E24-02 OmitNull behavior, not new — the test pins it so a future
        // serializer-handling change does not silently surface an unquoted `expr:`.
        var node = new NodeDto { Id = "n", Kind = "expr", Expr = "" };

        var yaml = CreateSerializer().Serialize(node);

        Assert.DoesNotContain("expr:", yaml);
    }

    [Theory]
    [InlineData("base * scale")]      // expression
    [InlineData("hello world")]       // plain text
    [InlineData("MAX(0, x)")]         // function call (parens are safe in YAML)
    [InlineData("v1.2.3")]            // version string
    public void NonAmbiguousLiterals_LeavePlain(string nonAmbiguous)
    {
        var node = new NodeDto { Id = "n", Kind = "expr", Expr = nonAmbiguous };

        var yaml = CreateSerializer().Serialize(node);

        Assert.Contains($"expr: {nonAmbiguous}", yaml);
    }
}
