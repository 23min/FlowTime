using FlowTime.Core;

namespace FlowTime.Core.Tests.Schema;

/// <summary>
/// m-E23-01 / AC4 + AC6 — negative-case catalogue covering every adjunct method on
/// <see cref="ModelSchemaValidator"/>. Each adjunct gets at least one deliberately-invalid
/// model snippet that asserts (a) <see cref="ValidationResult.IsValid"/> is <c>false</c>
/// and (b) the error list contains a substring matching the rule's identifying phrase.
///
/// <para>
/// Adjuncts complement the JSON-schema evaluator: they cover cross-reference and
/// cross-array rules JSON Schema draft-07 cannot express. The shape is mirrored after
/// the prior-art <c>ValidateClassReferences</c> (already wired into <c>Validate(yaml)</c>).
/// </para>
///
/// <para>
/// <b>Convention:</b> every snippet starts from a minimal valid skeleton and breaks
/// exactly one rule, so the asserted error is unambiguously attributable. Snippets
/// avoid sibling-kind fields, use <c>kind: const</c> with bin-aligned <c>values</c>
/// where possible, and keep dependencies between fields explicit. The rule-coverage
/// canary at <c>tests/FlowTime.Integration.Tests/TemplateWarningSurveyTests.cs</c>
/// continues to require zero validator errors across the shipped templates — these
/// tests therefore exercise shapes that the templates never produce.
/// </para>
/// </summary>
public sealed class RuleCoverageRegressionTests
{
    // ─── ValidateNodeIdUniqueness (cross-array; node-id duplicates within nodes[]) ───

    [Fact]
    public void NodeIdUniqueness_DuplicateIdsAcrossNodes_ProducesError()
    {
        var yaml = """
        schemaVersion: 1
        grid:
          bins: 3
          binSize: 1
          binUnit: minutes
        nodes:
          - id: dup
            kind: const
            values: [1, 2, 3]
          - id: dup
            kind: const
            values: [4, 5, 6]
        """;

        var result = ModelSchemaValidator.Validate(yaml);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("duplicate node id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NodeIdUniqueness_AllUniqueIds_DoesNotProduceUniquenessError()
    {
        // Sanity / branch coverage: ensures the adjunct's "no duplicates" branch is exercised.
        var yaml = """
        schemaVersion: 1
        grid:
          bins: 3
          binSize: 1
          binUnit: minutes
        nodes:
          - id: a
            kind: const
            values: [1, 2, 3]
          - id: b
            kind: const
            values: [4, 5, 6]
        """;

        var result = ModelSchemaValidator.Validate(yaml);

        Assert.True(result.IsValid, "Two distinct node ids must not trip the uniqueness adjunct.");
    }

    // ─── ValidateOutputSeriesReferences (cross-reference; outputs.series → node id or `*`) ───

    [Fact]
    public void OutputSeriesReferences_UnknownSeriesId_ProducesError()
    {
        var yaml = """
        schemaVersion: 1
        grid:
          bins: 3
          binSize: 1
          binUnit: minutes
        nodes:
          - id: demand
            kind: const
            values: [1, 2, 3]
        outputs:
          - series: not_a_node
        """;

        var result = ModelSchemaValidator.Validate(yaml);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.Contains("output series 'not_a_node'", StringComparison.OrdinalIgnoreCase) ||
            e.Contains("not_a_node", StringComparison.OrdinalIgnoreCase) && e.Contains("does not match", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void OutputSeriesReferences_WildcardStar_DoesNotProduceError()
    {
        // Branch coverage: the `*` wildcard short-circuits the lookup. Sanity that the
        // adjunct does not falsely flag the wildcard as an unresolved id.
        var yaml = """
        schemaVersion: 1
        grid:
          bins: 3
          binSize: 1
          binUnit: minutes
        nodes:
          - id: demand
            kind: const
            values: [1, 2, 3]
        outputs:
          - series: "*"
        """;

        var result = ModelSchemaValidator.Validate(yaml);

        Assert.True(result.IsValid, "The `*` wildcard must be accepted as an outputs.series value.");
    }

    // ─── ValidateExpressionNodeReferences (cross-reference; expr → node ids in scope) ───

    [Fact]
    public void ExpressionNodeReferences_UnknownIdInExpr_ProducesError()
    {
        var yaml = """
        schemaVersion: 1
        grid:
          bins: 3
          binSize: 1
          binUnit: minutes
        nodes:
          - id: a
            kind: const
            values: [1, 2, 3]
          - id: derived
            kind: expr
            expr: "missing_node + a"
        """;

        var result = ModelSchemaValidator.Validate(yaml);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("missing_node", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ExpressionNodeReferences_UnparseableExpr_DoesNotCrashAdjunct()
    {
        // Branch coverage: parser-fail path. The adjunct must not throw on a syntactically
        // broken expression — the broken-syntax error is owned by the schema/compile step.
        // The schema's `expr.minLength: 1` gate already lets non-empty garbage through, so
        // the adjunct must guard the parse attempt.
        var yaml = """
        schemaVersion: 1
        grid:
          bins: 3
          binSize: 1
          binUnit: minutes
        nodes:
          - id: a
            kind: const
            values: [1, 2, 3]
          - id: derived
            kind: expr
            expr: "((("
        """;

        // No throw. Validity is irrelevant here — we are pinning that the adjunct does
        // not blow up on parse failure.
        var result = ModelSchemaValidator.Validate(yaml);
        Assert.NotNull(result);
    }

    // ─── ValidateConstNodeValueCount (cross-reference; values.length == grid.bins) ───

    [Fact]
    public void ConstNodeValueCount_MismatchedLength_ProducesError()
    {
        var yaml = """
        schemaVersion: 1
        grid:
          bins: 3
          binSize: 1
          binUnit: minutes
        nodes:
          - id: demand
            kind: const
            values: [1, 2]
        """;

        var result = ModelSchemaValidator.Validate(yaml);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.Contains("'demand'", StringComparison.OrdinalIgnoreCase) &&
            e.Contains("values", StringComparison.OrdinalIgnoreCase) &&
            (e.Contains("bins", StringComparison.OrdinalIgnoreCase) || e.Contains("length", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void ConstNodeValueCount_ZeroBins_DoesNotProduceMismatchError()
    {
        // Branch coverage: when grid.bins is invalid (the schema's minimum:1 catches it)
        // the adjunct must not double-flag the same problem under a different message.
        // Schema rejects bins:0; we just confirm the adjunct doesn't add a confusing
        // length-mismatch message in that case.
        var yaml = """
        schemaVersion: 1
        grid:
          bins: 0
          binSize: 1
          binUnit: minutes
        nodes:
          - id: demand
            kind: const
            values: [1, 2, 3]
        """;

        var result = ModelSchemaValidator.Validate(yaml);

        // Schema rejects bins:0 (minimum:1). The point is just that the const adjunct
        // doesn't synthesize a noisy "values length mismatch" error in this case.
        Assert.False(result.IsValid);
        Assert.DoesNotContain(result.Errors, e =>
            e.Contains("'demand'", StringComparison.OrdinalIgnoreCase) &&
            e.Contains("values count", StringComparison.OrdinalIgnoreCase));
    }

    // ─── ValidatePmfProbabilitySum (cross-array; sum to 1.0 ± tolerance) ───

    [Fact]
    public void PmfProbabilitySum_NotOne_ProducesError()
    {
        var yaml = """
        schemaVersion: 1
        grid:
          bins: 3
          binSize: 1
          binUnit: minutes
        nodes:
          - id: roll
            kind: pmf
            pmf:
              values: [1, 2, 3]
              probabilities: [0.1, 0.1, 0.1]
        """;

        var result = ModelSchemaValidator.Validate(yaml);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.Contains("'roll'", StringComparison.OrdinalIgnoreCase) &&
            e.Contains("sum", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PmfProbabilitySum_WithinTolerance_DoesNotProduceError()
    {
        // Branch coverage: a sum of 1.00005 (within 1e-4) must not trip the adjunct.
        var yaml = """
        schemaVersion: 1
        grid:
          bins: 3
          binSize: 1
          binUnit: minutes
        nodes:
          - id: roll
            kind: pmf
            pmf:
              values: [1, 2, 3]
              probabilities: [0.50005, 0.3, 0.2]
        """;

        var result = ModelSchemaValidator.Validate(yaml);

        Assert.True(result.IsValid, "PMF sum within tolerance must validate. Errors: " + string.Join(" | ", result.Errors));
    }

    // ─── ValidatePmfValueUniqueness (cross-array; unique values) ───

    [Fact]
    public void PmfValueUniqueness_DuplicateValues_ProducesError()
    {
        var yaml = """
        schemaVersion: 1
        grid:
          bins: 3
          binSize: 1
          binUnit: minutes
        nodes:
          - id: roll
            kind: pmf
            pmf:
              values: [1, 1, 3]
              probabilities: [0.3, 0.3, 0.4]
        """;

        var result = ModelSchemaValidator.Validate(yaml);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.Contains("'roll'", StringComparison.OrdinalIgnoreCase) &&
            e.Contains("duplicate", StringComparison.OrdinalIgnoreCase));
    }

    // ─── ValidatePmfArrayLengths (cross-array; values.length == probabilities.length) ───

    [Fact]
    public void PmfArrayLengths_Mismatched_ProducesError()
    {
        var yaml = """
        schemaVersion: 1
        grid:
          bins: 3
          binSize: 1
          binUnit: minutes
        nodes:
          - id: roll
            kind: pmf
            pmf:
              values: [1, 2, 3]
              probabilities: [0.5, 0.5]
        """;

        var result = ModelSchemaValidator.Validate(yaml);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.Contains("'roll'", StringComparison.OrdinalIgnoreCase) &&
            e.Contains("probabilities", StringComparison.OrdinalIgnoreCase) &&
            e.Contains("length", StringComparison.OrdinalIgnoreCase));
    }

    // ─── ValidateSelfShiftRequiresInitialCondition (cross-reference; expr-AST → topology) ───

    [Fact]
    public void SelfShiftRequiresInitialCondition_NoInitialCondition_ProducesError()
    {
        var yaml = """
        schemaVersion: 1
        grid:
          bins: 3
          binSize: 1
          binUnit: minutes
        nodes:
          - id: depth
            kind: expr
            expr: "SHIFT(depth, 1) + 1"
        """;

        var result = ModelSchemaValidator.Validate(yaml);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.Contains("'depth'", StringComparison.OrdinalIgnoreCase) &&
            e.Contains("SHIFT", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SelfShiftRequiresInitialCondition_WithInitialCondition_DoesNotProduceError()
    {
        // Branch coverage: when the topology supplies an initialCondition.queueDepth for
        // the same node id, the adjunct must accept the self-shift. Note the topology
        // node and the `nodes[]` entry share the same id `depth` — that is the protocol.
        var yaml = """
        schemaVersion: 1
        grid:
          bins: 3
          binSize: 1
          binUnit: minutes
        topology:
          nodes:
            - id: depth
              kind: service
              semantics:
                arrivals: depth
                served: depth
              initialCondition:
                queueDepth: 0
          edges: []
        nodes:
          - id: depth
            kind: expr
            expr: "SHIFT(depth, 1) + 1"
        """;

        var result = ModelSchemaValidator.Validate(yaml);

        Assert.DoesNotContain(result.Errors, e =>
            e.Contains("SHIFT", StringComparison.OrdinalIgnoreCase) &&
            e.Contains("initial", StringComparison.OrdinalIgnoreCase));
    }

    // ─── ValidateTopologySeriesReferences (cross-reference; topology.semantics → node id) ───

    [Fact]
    public void TopologySeriesReferences_UnknownArrivalsId_ProducesError()
    {
        var yaml = """
        schemaVersion: 1
        grid:
          bins: 3
          binSize: 1
          binUnit: minutes
        topology:
          nodes:
            - id: svc
              kind: service
              semantics:
                arrivals: nonexistent_node
                served: served_node
          edges: []
        nodes:
          - id: served_node
            kind: const
            values: [1, 2, 3]
        """;

        var result = ModelSchemaValidator.Validate(yaml);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.Contains("nonexistent_node", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TopologySeriesReferences_SelfKeyword_DoesNotProduceError()
    {
        // Branch coverage: `self` is a reserved binding meaning "this topology node".
        // It must not be looked up against the node id set.
        var yaml = """
        schemaVersion: 1
        grid:
          bins: 3
          binSize: 1
          binUnit: minutes
        topology:
          nodes:
            - id: svc
              kind: service
              semantics:
                arrivals: self
                served: served_node
          edges: []
        nodes:
          - id: served_node
            kind: const
            values: [1, 2, 3]
        """;

        var result = ModelSchemaValidator.Validate(yaml);

        Assert.DoesNotContain(result.Errors, e =>
            e.Contains("'self'", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TopologySeriesReferences_FilePrefix_DoesNotProduceError()
    {
        // Branch coverage: the `file:` prefix is an external-data reference. The adjunct
        // must not look it up against the node id set.
        var yaml = """
        schemaVersion: 1
        grid:
          bins: 3
          binSize: 1
          binUnit: minutes
        topology:
          nodes:
            - id: svc
              kind: service
              semantics:
                arrivals: "file:arrivals.csv"
                served: served_node
          edges: []
        nodes:
          - id: served_node
            kind: const
            values: [1, 2, 3]
        """;

        var result = ModelSchemaValidator.Validate(yaml);

        Assert.DoesNotContain(result.Errors, e => e.Contains("file:arrivals.csv", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TopologySeriesReferences_NodeAtClassReference_ResolvesNodeIdOnly()
    {
        // Branch coverage: `nodeId@classId` syntax — only the node id portion is checked
        // against the node set. Class id is independent (covered by ValidateClassReferences).
        var yaml = """
        schemaVersion: 1
        grid:
          bins: 3
          binSize: 1
          binUnit: minutes
        topology:
          nodes:
            - id: svc
              kind: service
              semantics:
                arrivals: "no_such_node@some_class"
                served: served_node
          edges: []
        nodes:
          - id: served_node
            kind: const
            values: [1, 2, 3]
        """;

        var result = ModelSchemaValidator.Validate(yaml);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("no_such_node", StringComparison.OrdinalIgnoreCase));
    }

    // ─── ValidateWipOverflowTarget (cross-reference; wipOverflow → topology node with queueDepth) ───

    [Fact]
    public void WipOverflowTarget_UnknownTarget_ProducesError()
    {
        var yaml = """
        schemaVersion: 1
        grid:
          bins: 3
          binSize: 1
          binUnit: minutes
        topology:
          nodes:
            - id: svc
              kind: serviceWithBuffer
              semantics:
                arrivals: arr
                served: srv
                queueDepth: svc_q
              wipOverflow: ghost_node
          edges: []
        nodes:
          - id: arr
            kind: const
            values: [1, 2, 3]
          - id: srv
            kind: const
            values: [1, 2, 3]
        """;

        var result = ModelSchemaValidator.Validate(yaml);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.Contains("ghost_node", StringComparison.OrdinalIgnoreCase) &&
            e.Contains("wipOverflow", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WipOverflowTarget_LossKeyword_DoesNotProduceError()
    {
        // Branch coverage: `loss` is the reserved sink. Must not be looked up.
        var yaml = """
        schemaVersion: 1
        grid:
          bins: 3
          binSize: 1
          binUnit: minutes
        topology:
          nodes:
            - id: svc
              kind: serviceWithBuffer
              semantics:
                arrivals: arr
                served: srv
                queueDepth: svc_q
              wipOverflow: loss
          edges: []
        nodes:
          - id: arr
            kind: const
            values: [1, 2, 3]
          - id: srv
            kind: const
            values: [1, 2, 3]
        """;

        var result = ModelSchemaValidator.Validate(yaml);

        Assert.DoesNotContain(result.Errors, e =>
            e.Contains("'loss'", StringComparison.OrdinalIgnoreCase) &&
            e.Contains("wipOverflow", StringComparison.OrdinalIgnoreCase));
    }

    // ─── ValidateWipOverflowAcyclic (graph invariant; no cycles) ───

    [Fact]
    public void WipOverflowAcyclic_TwoNodeCycle_ProducesError()
    {
        var yaml = """
        schemaVersion: 1
        grid:
          bins: 3
          binSize: 1
          binUnit: minutes
        topology:
          nodes:
            - id: a
              kind: serviceWithBuffer
              semantics:
                arrivals: ax
                served: ay
                queueDepth: a_q
              wipOverflow: b
            - id: b
              kind: serviceWithBuffer
              semantics:
                arrivals: bx
                served: by
                queueDepth: b_q
              wipOverflow: a
          edges: []
        nodes:
          - id: ax
            kind: const
            values: [1, 2, 3]
          - id: ay
            kind: const
            values: [1, 2, 3]
          - id: bx
            kind: const
            values: [1, 2, 3]
          - id: by
            kind: const
            values: [1, 2, 3]
        """;

        var result = ModelSchemaValidator.Validate(yaml);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("cycle", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WipOverflowAcyclic_LinearChain_DoesNotProduceCycleError()
    {
        // Branch coverage: a → b → loss is acyclic and must validate.
        var yaml = """
        schemaVersion: 1
        grid:
          bins: 3
          binSize: 1
          binUnit: minutes
        topology:
          nodes:
            - id: a
              kind: serviceWithBuffer
              semantics:
                arrivals: ax
                served: ay
                queueDepth: a_q
              wipOverflow: b
            - id: b
              kind: serviceWithBuffer
              semantics:
                arrivals: bx
                served: by
                queueDepth: b_q
              wipOverflow: loss
          edges: []
        nodes:
          - id: ax
            kind: const
            values: [1, 2, 3]
          - id: ay
            kind: const
            values: [1, 2, 3]
          - id: bx
            kind: const
            values: [1, 2, 3]
          - id: by
            kind: const
            values: [1, 2, 3]
        """;

        var result = ModelSchemaValidator.Validate(yaml);

        Assert.DoesNotContain(result.Errors, e => e.Contains("cycle", StringComparison.OrdinalIgnoreCase));
    }

    // ─── ValidateDateTimeFormats (annotation-only `format: date-time` made enforceable) ───

    [Fact]
    public void DateTimeFormats_UnparseableGridStart_ProducesError()
    {
        var yaml = """
        schemaVersion: 1
        grid:
          bins: 3
          binSize: 1
          binUnit: minutes
          start: not-a-timestamp
        nodes:
          - id: demand
            kind: const
            values: [1, 2, 3]
        """;

        var result = ModelSchemaValidator.Validate(yaml);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.Contains("/grid/start", StringComparison.OrdinalIgnoreCase) &&
            e.Contains("date-time", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DateTimeFormats_AbsentGridStart_DoesNotProduceError()
    {
        // Branch coverage: grid.start is optional. Adjunct must skip the check when absent.
        var yaml = """
        schemaVersion: 1
        grid:
          bins: 3
          binSize: 1
          binUnit: minutes
        nodes:
          - id: demand
            kind: const
            values: [1, 2, 3]
        """;

        var result = ModelSchemaValidator.Validate(yaml);

        Assert.True(result.IsValid, "Absent grid.start must not trip the date-time adjunct. Errors: " + string.Join(" | ", result.Errors));
    }

    [Fact]
    public void DateTimeFormats_ValidIsoUtcGridStart_DoesNotProduceError()
    {
        // Branch coverage: a parseable ISO-8601 UTC timestamp is accepted.
        var yaml = """
        schemaVersion: 1
        grid:
          bins: 3
          binSize: 1
          binUnit: minutes
          start: 2025-01-01T00:00:00Z
        nodes:
          - id: demand
            kind: const
            values: [1, 2, 3]
        """;

        var result = ModelSchemaValidator.Validate(yaml);

        Assert.True(result.IsValid, "Valid ISO-8601 UTC grid.start must validate. Errors: " + string.Join(" | ", result.Errors));
    }

    [Fact]
    public void DateTimeFormats_UnparseableProvenanceGeneratedAt_ProducesError()
    {
        // Branch coverage: provenance.generatedAt is the second `format: date-time` field.
        var yaml = """
        schemaVersion: 1
        grid:
          bins: 3
          binSize: 1
          binUnit: minutes
        nodes:
          - id: demand
            kind: const
            values: [1, 2, 3]
        provenance:
          generator: test
          generatedAt: nope
          templateId: t
          templateVersion: v
          mode: simulation
          modelId: m
        """;

        var result = ModelSchemaValidator.Validate(yaml);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.Contains("/provenance/generatedAt", StringComparison.OrdinalIgnoreCase) &&
            e.Contains("date-time", StringComparison.OrdinalIgnoreCase));
    }
}
