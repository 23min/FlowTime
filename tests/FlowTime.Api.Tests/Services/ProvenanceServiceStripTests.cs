using FlowTime.API.Services;
using Xunit;

namespace FlowTime.Api.Tests.Services;

/// <summary>
/// Branch-coverage tests for <see cref="ProvenanceService.StripProvenance"/>.
///
/// <para>m-E23-02 AC8 (Category I).</para>
/// The previous implementation deserialized to <c>Dictionary&lt;string, object&gt;</c> and
/// re-serialized, which discards every original scalar's <see cref="YamlDotNet.Core.ScalarStyle"/>.
/// Strings whose literal text was YAML-1.2-ambiguous (e.g. <c>pmf.expected</c> emitted by
/// <c>SimModelBuilder</c> as a <c>G17</c>-formatted string like <c>"3.5"</c>) were re-emitted
/// as plain scalars, which the canonical schema's
/// <c>nodes[].metadata.additionalProperties.type: string</c> rule then rejected.
///
/// The new implementation uses a <see cref="YamlDotNet.RepresentationModel.YamlStream"/> walk
/// + <see cref="YamlDotNet.RepresentationModel.YamlStream.Save(System.IO.TextWriter, bool)"/>
/// to remove the <c>provenance:</c> key while preserving every other scalar's style.
/// </summary>
public sealed class ProvenanceServiceStripTests
{
    // Branch 1 — null/empty/whitespace input is returned as-is (early-return guard).
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\n")]
    public void StripProvenance_NullOrWhitespace_ReturnsInputUnchanged(string? input)
    {
        var result = ProvenanceService.StripProvenance(input!);
        Assert.Equal(input, result);
    }

    // Branch 2 — malformed YAML falls back to original text via the catch.
    [Fact]
    public void StripProvenance_MalformedYaml_ReturnsInputUnchanged()
    {
        // Unbalanced bracket/brace produces a YamlException on Load.
        const string malformed = "schemaVersion: 1\ngrid: {bins: 4, binSize: 1, binUnit: hours\n";

        var result = ProvenanceService.StripProvenance(malformed);

        Assert.Equal(malformed, result);
    }

    // Branch 3 — empty document set (e.g., a document containing only "---" with nothing else)
    // returns the original input. Hard to construct without YamlDotNet treating "---" as a
    // valid empty doc; instead use a comment-only file which produces zero documents.
    [Fact]
    public void StripProvenance_NoDocuments_ReturnsInputUnchanged()
    {
        const string commentsOnly = "# just a comment\n# nothing else\n";

        var result = ProvenanceService.StripProvenance(commentsOnly);

        Assert.Equal(commentsOnly, result);
    }

    // Branch 4 — root is a sequence (not a mapping) — returns original input.
    [Fact]
    public void StripProvenance_RootIsSequence_ReturnsInputUnchanged()
    {
        const string yamlSequence = "- item1\n- item2\n";

        var result = ProvenanceService.StripProvenance(yamlSequence);

        Assert.Equal(yamlSequence, result);
    }

    // Branch 5 — provenance key absent — returns original input (no rewrite needed).
    [Fact]
    public void StripProvenance_NoProvenanceKey_ReturnsInputUnchanged()
    {
        const string yaml = """
            schemaVersion: 1
            grid:
              bins: 1
              binSize: 1
              binUnit: hours
            nodes:
              - id: demand
                kind: const
                values: [100]
            """;

        var result = ProvenanceService.StripProvenance(yaml);

        Assert.Equal(yaml, result);
    }

    // Branch 6 (happy path) — provenance key present, removed, output no longer contains it.
    [Fact]
    public void StripProvenance_ProvenancePresent_RemovesKey()
    {
        const string yaml = """
            schemaVersion: 1
            provenance:
              modelId: model_test
              generatedAt: "2026-04-26T00:00:00Z"
            grid:
              bins: 1
              binSize: 1
              binUnit: hours
            nodes:
              - id: demand
                kind: const
                values: [100]
            """;

        var result = ProvenanceService.StripProvenance(yaml);

        Assert.DoesNotContain("provenance", result);
        Assert.DoesNotContain("model_test", result);
        Assert.Contains("schemaVersion", result);
        Assert.Contains("grid", result);
        Assert.Contains("nodes", result);
    }

    // Branch 6 (regression for the specific m-E23-02 bug) — string scalars that look like
    // numbers (e.g., G17-formatted "3.5", "0", "true") survive the round-trip as quoted
    // strings, not as plain scalars that YAML 1.2 would re-resolve as numbers/bools.
    //
    // The previous Dictionary<string, object>-based implementation failed this because the
    // generic deserializer parsed "3.5" → double 3.5 → re-emitted as the plain scalar `3.5`.
    // Schema validators that declare `type: string` then rejected the post-strip wire form.
    [Fact]
    public void StripProvenance_PreservesAmbiguousStringScalars()
    {
        // pmf.expected mirrors what SimModelBuilder emits (G17-formatted string in
        // a Dictionary<string, string> metadata bag). The double-quoted form on the wire
        // is the contract that ModelSchemaValidator's metadata type-check depends on.
        const string yaml = """
            schemaVersion: 1
            provenance:
              modelId: model_test
            grid:
              bins: 1
              binSize: 1
              binUnit: hours
            nodes:
              - id: demand
                kind: const
                values: [100]
                metadata:
                  origin.kind: "pmf"
                  pmf.expected: "3.5"
                  graph.hidden: "true"
            """;

        var result = ProvenanceService.StripProvenance(yaml);

        // Provenance gone.
        Assert.DoesNotContain("modelId", result);

        // Ambiguous string scalars must remain quoted (i.e., the literal text "3.5" must
        // appear with surrounding quotes, not as the plain scalar `pmf.expected: 3.5`).
        // We assert on the wire shape directly.
        Assert.Contains("\"3.5\"", result);
        Assert.Contains("\"true\"", result);
        Assert.Contains("\"pmf\"", result);
    }

    // Branch 7 — multi-document YAML: only document 0 is processed; subsequent documents
    // pass through with their own provenance: keys intact. This pins the current contract:
    // StripProvenance is a single-document operation by design (uses stream.Documents[0]).
    // If a future caller needs multi-document semantics, that's a new feature, not a bug
    // fix — this test exists to flag that contract.
    [Fact]
    public void StripProvenance_MultiDocumentYaml_StripsFirstDocumentOnly()
    {
        const string multiDoc = """
            ---
            schemaVersion: 1
            provenance:
              modelId: doc_0_provenance
            grid:
              bins: 1
              binSize: 1
              binUnit: hours
            nodes:
              - id: demand
                kind: const
                values: [100]
            ---
            schemaVersion: 2
            provenance:
              modelId: doc_1_provenance
            other: "carry-through"

            """;

        var result = ProvenanceService.StripProvenance(multiDoc);

        // Document 0's provenance is gone.
        Assert.DoesNotContain("doc_0_provenance", result);
        // Document 1's provenance is preserved (Strip only touches Documents[0]).
        Assert.Contains("doc_1_provenance", result);
        Assert.Contains("schemaVersion: 2", result);
        Assert.Contains("carry-through", result);
    }

    // Branch 8 — provenance value is a scalar (not a mapping). Strip's contract is "remove
    // the root-level provenance key, regardless of value shape" — it doesn't crash on the
    // malformed input, even though ModelSchemaValidator would reject the same payload
    // downstream. ExtractProvenance throws InvalidOperationException for scalar values, but
    // Strip is structurally tolerant. This test pins that tolerance.
    [Fact]
    public void StripProvenance_ProvenanceValueIsScalar_RemovesKeyAnyway()
    {
        const string yaml = """
            schemaVersion: 1
            provenance: "some-string-id"
            grid:
              bins: 1
              binSize: 1
              binUnit: hours
            nodes:
              - id: demand
                kind: const
                values: [100]
            """;

        var result = ProvenanceService.StripProvenance(yaml);

        // The provenance key (and its scalar value) are gone.
        Assert.DoesNotContain("provenance", result);
        Assert.DoesNotContain("some-string-id", result);
        // The rest of the document survives.
        Assert.Contains("schemaVersion", result);
        Assert.Contains("grid", result);
        Assert.Contains("nodes", result);
    }

    // Branch 9 — a provenance: key nested inside grid: (or anywhere other than root) is
    // NOT touched by Strip. This pins the contract that Strip only removes ROOT-level
    // provenance — anything nested is the schema validator's problem (ModelSchemaValidator
    // catches /grid/provenance via additionalProperties: false, per
    // ProvenanceEmbeddedTests.PostRun_ProvenanceAtWrongLevel_ReturnsError).
    [Fact]
    public void StripProvenance_NestedProvenanceLeftAlone()
    {
        const string yaml = """
            schemaVersion: 1
            grid:
              bins: 1
              binSize: 1
              binUnit: hours
              provenance:
                modelId: nested_under_grid
            nodes:
              - id: demand
                kind: const
                values: [100]
            """;

        // Root has no provenance key, so Strip takes the early-return path on line 166
        // and returns the original input verbatim. The nested provenance survives.
        var result = ProvenanceService.StripProvenance(yaml);

        Assert.Equal(yaml, result);
        Assert.Contains("nested_under_grid", result);
    }

    // Branch 10 — false-positive guard. A YAML payload that contains the literal string
    // "provenance" inside a node id or a metadata description — but has NO root-level
    // provenance: key — must round-trip byte-identically. This pins that Strip matches
    // structurally on the root-mapping key, not via string substitution / regex.
    [Fact]
    public void StripProvenance_LiteralProvenanceStringInValue_NotConfused()
    {
        const string yaml = """
            schemaVersion: 1
            grid:
              bins: 1
              binSize: 1
              binUnit: hours
            nodes:
              - id: provenance_node
                kind: const
                values: [100]
                metadata:
                  description: "this is the provenance section"
            """;

        var result = ProvenanceService.StripProvenance(yaml);

        // No removal happened — input round-trips byte-identically through the
        // early-return on line 166 (root.Children does not contain "provenance").
        Assert.Equal(yaml, result);
        Assert.Contains("provenance_node", result);
        Assert.Contains("this is the provenance section", result);
    }
}
