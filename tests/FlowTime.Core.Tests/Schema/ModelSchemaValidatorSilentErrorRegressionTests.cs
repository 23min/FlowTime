using System.Linq;
using System.Text.Json.Nodes;
using FlowTime.Core;
using Json.Schema;

namespace FlowTime.Core.Tests.Schema;

/// <summary>
/// m-E23-01 / D3 regression — closes the silent-error blind spot in the schema-eval pipeline.
///
/// Background: <see cref="ModelSchemaValidator.Validate(string)"/> calls JsonEverything's
/// <c>Evaluate(...)</c> with <see cref="OutputFormat.Hierarchical"/> and walks the resulting
/// <see cref="EvaluationResults"/> tree, collecting <c>InstanceLocation: error.Value</c> strings
/// from any node whose <c>Errors</c> dictionary is populated. Several JsonEverything keywords —
/// <c>not</c>, <c>oneOf</c>, deep <c>allOf</c> — fail by setting <c>IsValid == false</c> on the
/// relevant subtree without populating any leaf <c>Errors</c> entry. Pre-fix, those failures
/// became silent: the validator returned <see cref="ValidationResult.IsValid"/> == <c>false</c>
/// with an empty <see cref="ValidationResult.Errors"/> list, so callers (notably the canary
/// <c>Survey_Templates_For_Warnings</c>) saw "invalid model, zero errors" and could not tell
/// what failed. Worse, when no other validator produced a textual diagnostic on the same
/// instance, the silent failure could be invisible end-to-end.
///
/// Fix 1: <see cref="ModelSchemaValidator"/> synthesizes a path-only fallback message
/// (<c>{instance}: schema rule failed at {path}</c>) when the evaluation tree is invalid but
/// no leaf yielded a textual diagnostic. The synthesizer walks to the deepest invalid node
/// in the <see cref="EvaluationResults.Details"/> tree and uses that node's
/// <c>InstanceLocation</c>/<c>EvaluationPath</c> as the message coordinates.
///
/// These tests pin the synthesizer directly (it's <c>internal</c> for test access via
/// <c>InternalsVisibleTo("FlowTime.Core.Tests")</c>). Driving the synthesizer directly with
/// a hand-crafted <see cref="EvaluationResults"/> gives a sharp regression: pre-fix the
/// helpers don't exist, post-fix they produce a deterministic path-only message in exactly
/// the silent-error shape D3 found.
/// </summary>
public sealed class ModelSchemaValidatorSilentErrorRegressionTests
{
    /// <summary>
    /// Sanity check: confirms the silent-error shape we're guarding actually occurs in
    /// JsonEverything 5.x for the <c>not</c> keyword. If a future JsonEverything bump
    /// starts populating leaf errors for <c>not</c>, this test fails noisily and we can
    /// re-evaluate whether the synthesizer is still needed.
    /// </summary>
    [Fact]
    public void NotKeyword_FailsWithoutPopulatingAnyLeafErrors_TodayInJsonEverything()
    {
        var evaluation = EvaluateNotKeywordFailure();

        Assert.False(evaluation.IsValid);
        Assert.True(IsTreeFreeOfTextualErrors(evaluation),
            "JsonEverything's `not` keyword must fail silently (no leaf Errors). " +
            "If this assertion fails, the silent-error class has shrunk and the synthesizer's " +
            "responsibility may have changed.");
    }

    /// <summary>
    /// Core regression: when an evaluation is invalid and <see cref="ModelSchemaValidator.CollectErrorsForTests"/>
    /// yields zero strings, <see cref="ModelSchemaValidator.SynthesizePathOnlyErrorForTests"/>
    /// must produce a non-empty path-only diagnostic.
    /// </summary>
    [Fact]
    public void Synthesizer_OnSilentInvalidEvaluation_ProducesPathOnlyDiagnostic()
    {
        var evaluation = EvaluateNotKeywordFailure();
        Assert.False(evaluation.IsValid);

        var collected = ModelSchemaValidator.CollectErrorsForTests(evaluation).ToList();
        Assert.Empty(collected);

        var synthesized = ModelSchemaValidator.SynthesizePathOnlyErrorForTests(evaluation);

        Assert.False(string.IsNullOrWhiteSpace(synthesized));
        Assert.Contains("schema rule failed at", synthesized);
    }

    /// <summary>
    /// Format spec: the synthesized message is <c>{instance}: schema rule failed at {path}</c>
    /// where <c>{instance}</c> and <c>{path}</c> are JsonPointer renderings (empty string for
    /// the document/schema root, otherwise <c>/segment/segment...</c>). Anchors the contract
    /// callers depend on. Uses a nested silent-error keyword so the deepest invalid node has
    /// non-trivial pointers — proves the walker descends into
    /// <see cref="EvaluationResults.Details"/> rather than just returning the root.
    /// </summary>
    [Fact]
    public void Synthesizer_MessageFormat_IsInstanceColonSchemaRuleFailedAtPath()
    {
        // Schema: nodes is an array; each item must NOT have property `bad`.
        // Instance: nodes[0] = { bad: 1 }. Failure is silent-error nested at /nodes/0.
        var schemaJson = """
        {
          "type": "object",
          "properties": {
            "nodes": {
              "type": "array",
              "items": {
                "type": "object",
                "not": { "required": ["bad"] }
              }
            }
          }
        }
        """;
        var instance = JsonNode.Parse("""{"nodes":[{"bad":1}]}""")!;
        var schema = JsonSchema.FromText(schemaJson);
        var evaluation = schema.Evaluate(instance, new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical });
        Assert.False(evaluation.IsValid);
        Assert.True(IsTreeFreeOfTextualErrors(evaluation));

        var synthesized = ModelSchemaValidator.SynthesizePathOnlyErrorForTests(evaluation);

        // Format: <instance>: schema rule failed at <path>
        // JsonPointer renders the root as the empty string, so the regex must allow either
        // empty or `/segment/segment...` for both fields.
        var match = System.Text.RegularExpressions.Regex.Match(
            synthesized,
            @"^(?<instance>(/[^:\s]*)*): schema rule failed at (?<path>(/[^:\s]*)*)$");
        Assert.True(match.Success,
            $"Synthesized message must match `<instance>: schema rule failed at <path>` — got: {synthesized}");

        // For this nested-failure scenario the deepest invalid node points at `/nodes/0`
        // (the instance item that violated the schema). The schema-side EvaluationPath
        // depends on how JsonEverything reports `not` failures — empirically it surfaces
        // the parent containing schema (`/properties/nodes/items`) because the `not`
        // keyword's own subtree is reported as valid (the inner schema matched, which is
        // exactly what makes the parent `not` keyword fail). Either way, the path is
        // non-empty and points at the items schema where the violation occurred.
        Assert.Contains("/nodes/0", match.Groups["instance"].Value);
        Assert.Contains("/properties/nodes/items", match.Groups["path"].Value);
    }

    /// <summary>
    /// Idempotency: invoking the synthesizer twice on the same evaluation yields the
    /// same string. Locks the deterministic-deepest-leaf choice — without this, the
    /// synthesizer's output could drift across calls and break log-comparison tooling.
    /// </summary>
    [Fact]
    public void Synthesizer_IsIdempotent_ForTheSameEvaluation()
    {
        var evaluation = EvaluateNotKeywordFailure();

        var first = ModelSchemaValidator.SynthesizePathOnlyErrorForTests(evaluation);
        var second = ModelSchemaValidator.SynthesizePathOnlyErrorForTests(evaluation);

        Assert.Equal(first, second);
    }

    /// <summary>
    /// Walker tiebreaker coverage: when two invalid nodes share the same EvaluationPath
    /// segment count, the walker breaks the tie deterministically by InstanceLocation
    /// segment count (deeper wins). Constructs a schema with sibling silent-error keywords
    /// that both fire on different array items, forcing equal-depth invalid descendants
    /// in the <see cref="EvaluationResults.Details"/> tree.
    /// </summary>
    [Fact]
    public void Synthesizer_WhenMultipleInvalidNodesShareDepth_PicksDeterministically()
    {
        // Two array items both fail the same `not` rule. Their schema-side
        // EvaluationPaths are siblings at equal depth (`/properties/nodes/items` is
        // shared); their instance-side InstanceLocations are `/nodes/0` and `/nodes/1`.
        // The tiebreaker (deeper InstanceLocation segments) is exercised when one
        // candidate's instance pointer is longer; with identical depths the walker
        // keeps the first candidate it found, which is `/nodes/0` (depth-first descent).
        var schemaJson = """
        {
          "type": "object",
          "properties": {
            "nodes": {
              "type": "array",
              "items": { "type": "object", "not": { "required": ["bad"] } }
            }
          }
        }
        """;
        var instance = JsonNode.Parse("""{"nodes":[{"bad":1},{"bad":2}]}""")!;
        var schema = JsonSchema.FromText(schemaJson);
        var evaluation = schema.Evaluate(instance, new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical });

        Assert.False(evaluation.IsValid);
        Assert.True(IsTreeFreeOfTextualErrors(evaluation));

        // The synthesizer must produce a deterministic result — re-invoking it on the
        // same EvaluationResults must yield byte-identical output. The exact picked
        // node is implementation-detail (first-discovered vs last-discovered for ties),
        // but determinism is the contract.
        var first = ModelSchemaValidator.SynthesizePathOnlyErrorForTests(evaluation);
        var second = ModelSchemaValidator.SynthesizePathOnlyErrorForTests(evaluation);
        Assert.Equal(first, second);

        // The picked instance must be one of the failing items, not the root.
        Assert.Matches(@"^/nodes/[01]: schema rule failed at ", first);
    }

    /// <summary>
    /// End-to-end invariant: after the fix, any model the validator marks invalid must
    /// surface at least one error message. This is the contract the canary depends on.
    /// </summary>
    [Fact]
    public void ValidatorContract_InvalidModel_AlwaysSurfacesAtLeastOneError()
    {
        // A model with bins=0 trips the schema's minimum constraint — that's a textual
        // diagnostic, but the assertion below is on the post-fix invariant: invalid implies
        // non-empty errors. Both the textual and synthesized paths satisfy it.
        var invalidYaml = """
        schemaVersion: 1
        grid:
          bins: 0
          binSize: 1
          binUnit: minutes
        nodes: []
        """;
        var result = ModelSchemaValidator.Validate(invalidYaml);

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    /// <summary>
    /// Builds a minimal silent-error <see cref="EvaluationResults"/> using an in-line schema
    /// with a <c>not</c> keyword. This is the canonical D3 silent-error shape and does not
    /// depend on the production model schema, so it survives schema evolution.
    /// </summary>
    private static EvaluationResults EvaluateNotKeywordFailure()
    {
        var schemaJson = """
        {
          "type": "object",
          "properties": { "x": { "type": "integer" } },
          "not": { "required": ["x"] }
        }
        """;
        var instance = JsonNode.Parse("""{"x": 1}""")!;
        var schema = JsonSchema.FromText(schemaJson);
        return schema.Evaluate(instance, new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical });
    }

    /// <summary>
    /// Walks an <see cref="EvaluationResults"/> tree and returns true when no node in the
    /// tree has a populated <c>Errors</c> dictionary. Used by the sanity check above.
    /// </summary>
    private static bool IsTreeFreeOfTextualErrors(EvaluationResults results)
    {
        if (results.Errors is { Count: > 0 })
        {
            return false;
        }
        foreach (var detail in results.Details)
        {
            if (!IsTreeFreeOfTextualErrors(detail))
            {
                return false;
            }
        }
        return true;
    }
}
