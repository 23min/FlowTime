---
id: M-052
title: Parser/Validator Scalar-Style Fix
status: done
parent: E-24
acs:
  - id: AC-1
    title: '**`ModelSchemaValidator.ParseScalar` honors `ScalarStyle.Plain`.** Non-plain scalars resolve as strings. The guard
      is placed immediately after the `value is null` check and before the existing `bool.TryParse` call. Plain scalars continue
      to coerce as today (bool → int → double → string in that order).'
    status: met
  - id: AC-2
    title: '**`TemplateSchemaValidator.ParseScalar` is mirrored.** The identical guard lands in the Sim-side validator at
      `src/FlowTime.Sim.Core/Templates/TemplateSchemaValidator.cs:173-197`. Both validators use the same guard shape and semantics
      — no divergence.'
    status: met
  - id: AC-3
    title: '**Test-side coercion helper updated.** `tests/FlowTime.TimeMachine.Tests/TemplateSchemaValidationTests.cs:134-169`
      (`NormalizeYaml` / `TryConvertScalar`) is either updated to match the validator (ScalarStyle-aware) or replaced entirely
      by a delegate call into the fixed `ModelSchemaValidator`. Leaving the helper in its current aggressive-coerce state
      is not acceptable because it silently re-introduces the defect in the test layer.'
    status: met
  - id: AC-4
    title: '**Scalar-style test matrix — Engine side.** New tests in `tests/FlowTime.Core.Tests` (or appropriate home) cover
      at minimum: (a) quoted integer literal `expr: "0"` resolves as string and passes `nodes[].expr: type: string`; (b) unquoted
      integer `schemaVersion: 1` resolves as integer and passes `schemaVersion: type: integer`; (c) quoted boolean `"true"`
      resolves as string; (d) unquoted boolean `true` resolves as bool; (e) quoted null `"null"` resolves as the 4-character
      string; (f) unquoted null (`null` / empty) resolves as JSON null; (g) folded `>` and literal `|` block scalars resolve
      as strings. Each case is a distinct test method with an explicit test name describing the scalar style and expected
      resolution.'
    status: met
  - id: AC-5
    title: '**Scalar-style test matrix — Sim side.** Mirror test matrix in `tests/FlowTime.Sim.Tests` against `TemplateSchemaValidator`.
      Same case-by-case coverage; identical expectations.'
    status: met
  - id: AC-6
    title: '**Canary reports `val-err=0`.** `TemplateWarningSurveyTests.Survey_Templates_For_Warnings` reports `validator-errors=0`
      across all twelve templates at `ValidationTier.Analyse`. The total residual from the M-046 baseline (726) has collapsed
      to 0 after M-050 + M-051 (removed ~495 non-C) and M-052 (removed ~231 C-defect). The tracking doc records the final
      per-template histogram.'
    status: met
  - id: AC-7
    title: '**No regression in the broader validator behavior.** Tests that author plain-scalar integer/bool values (e.g.
      `schemaVersion: 1`, `bins: 24`, PMF values) continue to pass unchanged. The guard adds stricter handling only for non-plain
      scalars.'
    status: met
  - id: AC-8
    title: '**Full `.NET` suite green.** `dotnet test FlowTime.sln` passes. No new regressions. Any test that accidentally
      relied on quoted-literal coercion is updated in-milestone to author the scalar correctly (unquoted if integer was intended,
      quoted if string was intended).'
    status: met
  - id: AC-9
    title: '**Branch coverage complete.** The new `ScalarStyle.Plain` guard in each validator is exercised by at least the
      five non-plain style variants (SingleQuoted, DoubleQuoted, Literal, Folded, and where applicable FlowSingleQuoted /
      FlowDoubleQuoted if present in the YamlDotNet enum the project targets). The existing plain-path coercion has test coverage
      preserved (bool / int / double / string).'
    status: met
---

## Goal

Fix the `ParseScalar` defect in both `src/FlowTime.Core/Models/ModelSchemaValidator.cs:222-246` and `src/FlowTime.Sim.Core/Templates/TemplateSchemaValidator.cs:173-197` so both honor `YamlScalarNode.Style`. Quoted literals (`SingleQuoted`, `DoubleQuoted`) and block scalars (`Literal`, `Folded`) resolve as strings; only `Plain` scalars are candidates for type coercion. Mirror the fix in the test-side coercion helper at `tests/FlowTime.TimeMachine.Tests/TemplateSchemaValidationTests.cs:134-169` so test fixtures do not silently mask the runtime fix. After this milestone, the survey canary reports `val-err=0` across all twelve templates at `ValidationTier.Analyse`.

## Context

The M-046 prior-art investigation (see `work/epics/E-23-model-validation-consolidation/m-E23-01-schema-alignment-tracking.md` → "Prior art for C (ParseScalar scalar-style defect)") established: both validators were born with identical unconditionally-coercing `ParseScalar` implementations in commit `51a99b9` / `c1fc49d` (2025-11-24). No later commit touched scalar-style handling. `src/FlowTime.Core/Models/ParallelismReference.cs:97` is the one precedent in the repo for honoring `ScalarStyle.Plain`, guarding a narrower scalar distinction (null detection); the pattern ports directly. No external consumer relies on integer-coerced quoted literals. No prior commit reverts a ScalarStyle-aware fix; this is green-field.

The defect is independent of unification. Unification (M-050) and schema realignment (M-051) do not touch `ParseScalar`'s logic. The defect's blast radius (per M-046's full-shape audit): ~231 residual errors across four shape variants — `nodes/*/expr integer→string` (89), `nodes/*/metadata/graph.hidden boolean→string` (92), `nodes/*/metadata/pmf.expected number→string` (40), `nodes/*/metadata/pmf.expected integer→string` (10). Every residual collapses once `ParseScalar` respects `ScalarStyle`.

## Acceptance criteria

### AC-1 — **`ModelSchemaValidator.ParseScalar` honors `ScalarStyle.Plain`.** Non-plain scalars resolve as strings. The guard is placed immediately after the `value is null` check and before the existing `bool.TryParse` call. Plain scalars continue to coerce as today (bool → int → double → string in that order).

### AC-2 — **`TemplateSchemaValidator.ParseScalar` is mirrored.** The identical guard lands in the Sim-side validator at `src/FlowTime.Sim.Core/Templates/TemplateSchemaValidator.cs:173-197`. Both validators use the same guard shape and semantics — no divergence.

### AC-3 — **Test-side coercion helper updated.** `tests/FlowTime.TimeMachine.Tests/TemplateSchemaValidationTests.cs:134-169` (`NormalizeYaml` / `TryConvertScalar`) is either updated to match the validator (ScalarStyle-aware) or replaced entirely by a delegate call into the fixed `ModelSchemaValidator`. Leaving the helper in its current aggressive-coerce state is not acceptable because it silently re-introduces the defect in the test layer.

### AC-4 — **Scalar-style test matrix — Engine side.** New tests in `tests/FlowTime.Core.Tests` (or appropriate home) cover at minimum: (a) quoted integer literal `expr: "0"` resolves as string and passes `nodes[].expr: type: string`; (b) unquoted integer `schemaVersion: 1` resolves as integer and passes `schemaVersion: type: integer`; (c) quoted boolean `"true"` resolves as string; (d) unquoted boolean `true` resolves as bool; (e) quoted null `"null"` resolves as the 4-character string; (f) unquoted null (`null` / empty) resolves as JSON null; (g) folded `>` and literal `|` block scalars resolve as strings. Each case is a distinct test method with an explicit test name describing the scalar style and expected resolution.

### AC-5 — **Scalar-style test matrix — Sim side.** Mirror test matrix in `tests/FlowTime.Sim.Tests` against `TemplateSchemaValidator`. Same case-by-case coverage; identical expectations.

### AC-6 — **Canary reports `val-err=0`.** `TemplateWarningSurveyTests.Survey_Templates_For_Warnings` reports `validator-errors=0` across all twelve templates at `ValidationTier.Analyse`. The total residual from the M-046 baseline (726) has collapsed to 0 after M-050 + M-051 (removed ~495 non-C) and M-052 (removed ~231 C-defect). The tracking doc records the final per-template histogram.

### AC-7 — **No regression in the broader validator behavior.** Tests that author plain-scalar integer/bool values (e.g. `schemaVersion: 1`, `bins: 24`, PMF values) continue to pass unchanged. The guard adds stricter handling only for non-plain scalars.

### AC-8 — **Full `.NET` suite green.** `dotnet test FlowTime.sln` passes. No new regressions. Any test that accidentally relied on quoted-literal coercion is updated in-milestone to author the scalar correctly (unquoted if integer was intended, quoted if string was intended).

### AC-9 — **Branch coverage complete.** The new `ScalarStyle.Plain` guard in each validator is exercised by at least the five non-plain style variants (SingleQuoted, DoubleQuoted, Literal, Folded, and where applicable FlowSingleQuoted / FlowDoubleQuoted if present in the YamlDotNet enum the project targets). The existing plain-path coercion has test coverage preserved (bool / int / double / string).
## Constraints

- **Exactly one guard shape in both validators.** The check is `if (scalar.Style != ScalarStyle.Plain) return JsonValue.Create(value)!;` placed in the same position in both files. No second inference layer (e.g. tag-based `!!str` detection) — YAML's resolver already embeds the distinction in `Style`.
- **No coercion-mode configuration.** Do not add a `coerceQuotedLiterals` opt-in flag. The correct behavior is the only behavior.
- **Sync the test helper.** If `TemplateSchemaValidationTests.cs`'s `TryConvertScalar` is updated rather than replaced, apply the same `ScalarStyle.Plain` guard. If it is replaced, the replacement delegates to the validator's `ParseScalar` (either directly, via `internal` visibility and `InternalsVisibleTo`, or via a public test hook added in this milestone).
- **Every test case has a named style.** Do not rely on YamlDotNet's automatic style inference in test fixtures. Test YAML strings are authored with explicit quoting so the parser's `ScalarStyle` is deterministic.
- **Mirror is mandatory.** If only one validator is fixed, the survey canary will still show residuals sourced from the unfixed side — M-053 cannot close. The milestone wrap requires both fixed.

## Design Notes

- Reference implementation path (paraphrased from `work/gaps.md` → "`ModelSchemaValidator.ParseScalar` does not honor YAML ScalarStyle" → "Proposed fix"):

  ```csharp
  if (scalar.Style != ScalarStyle.Plain)
  {
      return JsonValue.Create(value)!;
  }
  ```

  Place immediately after the `value is null` check, before the first `bool.TryParse` call. Apply identically to both validators.

- `src/FlowTime.Core/Models/ParallelismReference.cs:97` uses `scalar.Style == ScalarStyle.Plain && string.IsNullOrWhiteSpace(scalar.Value)` — prior art for the guard pattern.
- YamlDotNet's `ScalarStyle` enum values: `Any`, `Plain`, `SingleQuoted`, `DoubleQuoted`, `Literal`, `Folded`. The guard rejects everything non-`Plain`. `Any` is a parse-time sentinel that should not appear on scalars emitted from YamlDotNet's parser.
- Test-fixture shape: use raw YAML string literals in `[Theory]` / `[InlineData]` for each scalar style so the style is visible in the test source. The test asserts `ModelSchemaValidator.Validate(yaml)` returns expected `ValidationResult` (error / no error) rather than inspecting the internal `JsonValue` — tests the observable behavior, not the implementation detail.
- Run the canary post-fix and capture the residual histogram in the tracking doc. Confirm zero errors; every template green.

## Surfaces touched

- `src/FlowTime.Core/Models/ModelSchemaValidator.cs:222-246` (`ParseScalar` fix)
- `src/FlowTime.Sim.Core/Templates/TemplateSchemaValidator.cs:173-197` (mirrored fix)
- `tests/FlowTime.TimeMachine.Tests/TemplateSchemaValidationTests.cs:134-169` (test helper update or replacement)
- `tests/FlowTime.Core.Tests` (or appropriate Engine-side test project — scalar-style test matrix)
- `tests/FlowTime.Sim.Tests` (scalar-style test matrix — Sim side)
- `work/epics/E-24-schema-alignment/m-E24-04-parser-validator-scalar-style-fix-tracking.md` (new)

## Out of Scope

- Schema edits — M-051 owns.
- Unified type or emitter changes — M-050 owns.
- Canary hard-assertion promotion — M-053 owns.
- Any other validator behavior (tier logic, warning vs error, etc.) — out of epic scope.

## Dependencies

- M-051 landed (or at least M-050) so the canary baseline reflects the unified schema + emitter state. The M-052 contribution is cleanly attributable to the ParseScalar fix. M-052 is logically independent of M-050 and M-051 and can be run in parallel; the conservative default is serial.
- Prior-art investigation complete (it is — M-046 tracking doc).

## References

- Epic spec: `work/epics/E-24-schema-alignment/spec.md`
- Gap entry: `work/gaps.md` → "`ModelSchemaValidator.ParseScalar` does not honor YAML ScalarStyle"
- Prior-art investigation: `work/epics/E-23-model-validation-consolidation/m-E23-01-schema-alignment-tracking.md` → "Prior art for C (ParseScalar scalar-style defect)"
- Defect site (Engine): `src/FlowTime.Core/Models/ModelSchemaValidator.cs:222-246`
- Defect site (Sim): `src/FlowTime.Sim.Core/Templates/TemplateSchemaValidator.cs:173-197`
- Prior-art precedent: `src/FlowTime.Core/Models/ParallelismReference.cs:97`
- Test-side coercion helper: `tests/FlowTime.TimeMachine.Tests/TemplateSchemaValidationTests.cs:134-169`
- Canary: `tests/FlowTime.Integration.Tests/TemplateWarningSurveyTests.cs`
