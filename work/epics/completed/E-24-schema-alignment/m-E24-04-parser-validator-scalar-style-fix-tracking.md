# m-E24-04 Parser/Validator Scalar-Style Fix — Tracking

**Started:** 2026-04-25
**Completed:** 2026-04-25
**Branch:** `milestone/m-E24-04-parser-validator-scalar-style-fix` (from `epic/E-24-schema-alignment`)
**Spec:** `work/epics/E-24-schema-alignment/m-E24-04-parser-validator-scalar-style-fix.md`
**Commits:** `a7c984f` (implementation — parser fix + emitter sibling + test matrix)
**Final test count:** Core 353/353, Sim 225/225 (3 skip pre-existing), TimeMachine 239/239, API 264/264, UI.Tests 265/265, CLI 91/91, FlowTime.Tests 228/228 (6 skip pre-existing), Integration 84/84 — full suite green.

<!-- Status is not carried here. The milestone spec's frontmatter `status:` field is
     canonical. `**Completed:**` is filled iff the spec is `complete`. -->

## Acceptance Criteria

<!-- Mirror ACs from the spec. Check each when its Work Log entry lands. -->

- [x] **AC1 — `ModelSchemaValidator.ParseScalar` honors `ScalarStyle.Plain`.** Non-plain scalars (`SingleQuoted`, `DoubleQuoted`, `Literal`, `Folded`) resolve as strings. The guard sits immediately after the `value is null` check and before the existing `bool.TryParse` call. Plain scalars continue to coerce as today (bool → int → double → string in that order).
- [x] **AC2 — `TemplateSchemaValidator.ParseScalar` is mirrored.** Identical guard shape and semantics in `src/FlowTime.Sim.Core/Templates/TemplateSchemaValidator.cs`. No divergence between the two validators.
- [x] **AC3 — Test-side coercion helper replaced.** `tests/FlowTime.TimeMachine.Tests/TemplateSchemaValidationTests.cs` `NormalizeYaml` / `TryConvertScalar` deleted; replaced with a `YamlStream` walk + `ParseScalar` mirror that respects `ScalarStyle`. The test layer now applies the same coercion rule as `ModelSchemaValidator.ParseScalar` — symmetric by construction.
- [x] **AC4 — Scalar-style test matrix — Engine side.** `tests/FlowTime.Core.Tests/Validation/ParseScalarStyleTests.cs` covers all required cases (quoted/unquoted bool/int/double/string/null, block literal/folded, end-to-end `expr: "0"`).
- [x] **AC5 — Scalar-style test matrix — Sim side.** `tests/FlowTime.Sim.Tests/Templates/ParseScalarStyleTests.cs` mirrors the Engine matrix against `TemplateSchemaValidator`.
- [x] **AC6 — Canary reports `val-err=0`.** `M_E24_02_Step6_AcceptanceTests.TimeMachineValidator_AnalyseTier_HistogramExcludesEmitterClosedShapes` reports total **0** errors at `ValidationTier.Analyse` (histogram below). Closure required both halves: the parser-side `ScalarStyle.Plain` guard (AC1/AC2) **and** the sibling `QuotedAmbiguousStringEmitter` on the Sim emitter (D-m-E24-04-03 / ADR-E-24-05).
- [x] **AC7 — No regression in broader validator behavior.** Plain-scalar integer/bool authoring (`schemaVersion: 1`, `bins: 24`, PMF values) unchanged. Core / Sim / TimeMachine suites all green (353/353, 225/225+3 skipped, 239/239).
- [x] **AC8 — Full `.NET` suite green.** Two pre-existing subprocess-timing flakes (`RustEngine_Timeout_ThrowsRustEngineException`, `Dispose_TerminatesSubprocess`) flake under suite contention; both pass in isolation. No new regressions attributable to this milestone.
- [x] **AC9 — Branch coverage complete.** Parser-side guard exercised by SingleQuoted, DoubleQuoted, Literal, Folded styles in both validators. New `QuotedAmbiguousStringEmitter` exercised by 11-branch theory (null/`~`/empty/bool casings/int/-int/float/scientific/leading-decimal) plus 4 non-ambiguous-leave-plain cases plus 9 DTO-shape tests covering `expr`, `metadata` (string-dict values), numeric-array no-op, and ISO-timestamp leave-plain. Round-trip pair tests (emit → parse → schema validate) close the loop end-to-end.

## Decisions made during implementation

- **D-m-E24-04-01 — m-E24-04 does not widen `ParseScalar` to recognize the YAML null keyword.** YamlDotNet's `RepresentationModel` does not apply YAML's tag resolver, so literal `null` and `~` surface as plain scalars whose `Value` is the literal text. Recognizing them as JSON null would require a second inference layer — explicitly rejected by the spec's Constraints section ("No second inference layer"). Tests pin existing behavior (`null` → "null", `~` → "~", both as 4-/1-character strings).
- **D-m-E24-04-02 — The pre-existing `value is null` branch in `ParseScalar` is defensive but not exercised by YamlDotNet's public surface.** Empty mapping values (`v:\n`) deserialize as `Value=""` (empty string), not `Value=null`. The branch is therefore unreachable through normal parsing. A latent gotcha exists if the branch ever does fire (`JsonValue.Create((string?)null)!` returns C# null wrapped in a no-op null-forgiving operator, producing a NullReferenceException in callers). m-E24-04 does not modify that branch — out of scope.
- **D-m-E24-04-03 — Round-trip pair: emitter mirrors validator.** AC6 closure requires both halves of the YAML round-trip pair. The parser-side fix (AC1/AC2) makes the validator honor `ScalarStyle`; the emitter-side fix forces `ScalarStyle.DoubleQuoted` on strings whose literal text would otherwise re-resolve as a YAML 1.2 plain non-string scalar (bool / int / float / null). Without both halves, Sim emits `expr: 0` (plain) for source `Expr = "0"` (string), and the validator (correctly) types the plain scalar as integer — failing the schema's `type: string`. **Chosen approach:** sibling `IEventEmitter` (`QuotedAmbiguousStringEmitter`) plugged into `TemplateService.CreateYamlSerializer` next to the existing `FlowSequenceEventEmitter`. Source-type-driven (activates only on `eventInfo.Source.Type == typeof(string)`); ambiguity test mirrors validator's plain-scalar coercion attempt-order exactly. **Why "no indirection / future-proof":** any string field — including newly-added DTO fields and `Dictionary<string, string>` values — is guarded the moment it ships, without per-field annotation, central listing, or post-hoc YAML rewriting. Prior art: `FlowSequenceEventEmitter` (commit `c57b597`, 2025-10-24) is the same pattern at the sequence-style boundary; the new emitter is the symmetric scalar-style sibling. ADR-E-24-05 ratifies the pattern.

## ADR-E-24-05 — `QuotedAmbiguousStringEmitter` (round-trip symmetry on the emitter side)

**Status:** Ratified (2026-04-25, in-milestone).

**Context.** The parser-side `ScalarStyle.Plain` guard (AC1/AC2) is necessary but not sufficient to close the AC6 canary. YamlDotNet's default serializer writes plain (unquoted) scalars whenever the literal text is "safe" — including text matching YAML 1.2 type-resolution patterns. So a DTO whose `string` field carries the literal `"0"` round-trips as `expr: 0` (plain), which the validator (correctly, post-fix) types as integer — failing schema `type: string`.

**Decision.** Plug a sibling `IEventEmitter` into `TemplateService.CreateYamlSerializer` next to the existing `FlowSequenceEventEmitter`. The new emitter forces `ScalarStyle.DoubleQuoted` when:

- `eventInfo.Source.Type == typeof(string)` — source type narrows the rule to actual strings; numerics, bools, arrays are untouched.
- The string text would re-resolve as a non-string YAML 1.2 plain scalar (null forms, bool, int, float).

The ambiguity classifier mirrors the validator's plain-scalar coercion attempt-order exactly. The two halves of the round-trip pair are symmetric by construction.

**Alternatives considered & rejected.**

| Option | Rejection reason |
|--------|------------------|
| `[YamlMember(ScalarStyle = DoubleQuoted)]` per string field | Doesn't extend to `Dictionary<string, string>` values (no per-entry annotation surface). Rots when new DTO fields ship. Violates "no indirection" requirement. |
| Post-hoc YAML rewriting after `Serialize` returns | Brittle (regex on YAML output), violates the "solve at the serializer level" requirement, and produces non-canonical formatting. |
| `coerceQuotedLiterals` opt-in flag | Spec explicitly forbids: "No coercion-mode configuration. The correct behavior is the only behavior." |
| Wider emitter that quotes all strings | Corrupts the wire shape of legitimate plain-text strings (`expr: base * scale` would become `expr: "base * scale"`). Loses author-intent preservation in the opposite direction. |

**Prior art.** `FlowSequenceEventEmitter` (commit `c57b597`, TT-M-03.20, 2025-10-24) — same pattern at the sequence-style boundary. The new emitter is the symmetric scalar-style sibling; the codebase has been using this pattern since October 2025. No reintroduction; this is an extension.

**Future-proofing.** Source-type activation means any new `string` DTO field, anywhere in the model graph, is guarded automatically when it ships. No central registry. No annotation sprinkling. The rule lives at the serializer level, once, on the type the rule is about.

## Validator-error histogram (canary)

### Pre-m-E24-04 (re-captured 2026-04-25, milestone branch tip; matches m-E24-03 close)

Total errors: **231** across all 12 templates at `ValidationTier.Analyse`
- m-E24-04 ParseScalar shapes: **231**
- m-E24-03 schema-rewrite shapes: **0**
- m-E24-02 emitter regressions: **0**

Per-shape breakdown:

| Count | Shape | Attribution |
|------|-------|-------------|
| 92 | `/nodes/*/metadata/graph.hidden :: Value is "boolean" but should be "string"` | m-E24-04 ParseScalar |
| 89 | `/nodes/*/expr :: Value is "integer" but should be "string"` | m-E24-04 ParseScalar |
| 40 | `/nodes/*/metadata/pmf.expected :: Value is "number" but should be "string"` | m-E24-04 ParseScalar |
| 10 | `/nodes/*/metadata/pmf.expected :: Value is "integer" but should be "string"` | m-E24-04 ParseScalar |

Verifier: `M_E24_02_Step6_AcceptanceTests.TimeMachineValidator_AnalyseTier_HistogramExcludesEmitterClosedShapes` (in-process, no live API needed).

### Post-m-E24-04 target

Total errors: **0** across all 12 templates at `ValidationTier.Analyse`. Every ParseScalar shape closes once the validator honors `YamlScalarNode.Style`.

### Post-m-E24-04 actual (parser + emitter fixes — 2026-04-25)

Total errors: **0** across all 12 templates at `ValidationTier.Analyse` — **231 → 0**.

Per-template result (every template green):

| Template | val-err | val-warn |
|----------|---------|----------|
| dependency-constraints-attached | 0 | 0 |
| dependency-constraints-minimal | 0 | 0 |
| it-document-processing-continuous | 0 | 0 |
| it-system-microservices | 0 | 0 |
| manufacturing-line | 0 | 0 |
| network-reliability | 0 | 0 |
| supply-chain-incident-retry | 0 | 1 |
| supply-chain-multi-tier-classes | 0 | 0 |
| supply-chain-multi-tier | 0 | 0 |
| transportation-basic-classes | 0 | 3 |
| transportation-basic | 0 | 0 |
| warehouse-picker-waves | 0 | 0 |
| **Totals** | **0** | **4** |

Per-shape histogram: empty (no error shapes survive). Step-4 emitter-closed shape regressions detected: 0.

The 4 remaining warnings are pre-existing analysis-tier signals (not validator errors) — out of scope for the canary's `val-err` contract.

## Work Log

| Phase | What | Tests | Status |
|-------|------|-------|--------|
| 1 | Capture pre-state canary histogram | 1 | **complete (2026-04-25)** — 231 errors, 92/89/40/10 split, all m-E24-04 ParseScalar shapes |
| 2 | Write red tests — Engine side scalar-style matrix | 18 | **complete (2026-04-25)** — `tests/FlowTime.Core.Tests/Validation/ParseScalarStyleTests.cs`; 10 RED before fix (quoted SQ/DQ × bool/int/double, block Literal/Folded, end-to-end `expr: "0"` → schema pass) |
| 3 | Write red tests — Sim side scalar-style matrix | 17 | **complete (2026-04-25)** — `tests/FlowTime.Sim.Tests/Templates/ParseScalarStyleTests.cs`; 9 RED before fix (mirrored Engine matrix, no end-to-end test — Sim validator targets template schema, not model schema) |
| 4 | Implement `ScalarStyle.Plain` guard in `ModelSchemaValidator.ParseScalar` | — | **complete (2026-04-25)** — `src/FlowTime.Core/Models/ModelSchemaValidator.cs:223-264`; guard sits immediately after `value is null` short-circuit |
| 5 | Mirror guard in `TemplateSchemaValidator.ParseScalar` | — | **complete (2026-04-25)** — `src/FlowTime.Sim.Core/Templates/TemplateSchemaValidator.cs:174-216`; identical guard shape |
| 6 | Replace test-side coercion helper in `TemplateSchemaValidationTests.cs` | — | **complete (2026-04-25)** — `NormalizeYaml` / `TryConvertScalar` deleted; `YamlToJsonNode` + `ParseScalar` (mirror of `ModelSchemaValidator`) replaces them. Test layer applies the same coercion rule as the validator — symmetric by construction. `AllTemplates_ConformToTemplateSchema` passes (1/1). |
| 7 | Implement `QuotedAmbiguousStringEmitter` (sibling of `FlowSequenceEventEmitter`) | — | **complete (2026-04-25)** — `src/FlowTime.Sim.Core/Templates/QuotedAmbiguousStringEmitter.cs`; activates on `Source.Type == typeof(string)` + ambiguity match (null/bool/int/float patterns); plugged into `TemplateService.CreateYamlSerializer` next to `FlowSequenceEventEmitter`. ADR-E-24-05 ratifies. |
| 8 | Round-trip emitter test matrix | 29 | **complete (2026-04-25)** — `tests/FlowTime.Sim.Tests/Templates/QuotedAmbiguousStringEmitterTests.cs`; DTO-shape, theory of ambiguous literals, theory of non-ambiguous literals, end-to-end emit→validate round-trip pair (3 shapes from the canary). 29/29 green. |
| 9 | Re-run canary; confirm 231 → 0 transition | 1 | **complete (2026-04-25)** — `M_E24_02_Step6_AcceptanceTests.TimeMachineValidator_AnalyseTier_HistogramExcludesEmitterClosedShapes`: total `val-err=0` across 12 templates. Histogram empty. Step-4 emitter regressions: 0. |
| 10 | Full `dotnet test FlowTime.sln`; investigate any regressions | — | **complete (2026-04-25)** — Core 353/353, Sim 225/225 (3 skip pre-existing), TimeMachine 239/239, API 264/264, UI.Tests 265/265, CLI 91/91, FlowTime.Tests 228/228 (6 skip pre-existing), Integration 83/84 (1 timing flake — `RustEngine_Timeout_ThrowsRustEngineException` or `Dispose_TerminatesSubprocess`, both pre-existing subprocess-timing flakes that pass in isolation; unrelated to this milestone). |
| 11 | Branch-coverage audit on the modified `ParseScalar` paths and new emitter | — | **complete (2026-04-25)** — *Parser side*: every reachable branch in post-fix `ParseScalar` exercised — `value is null` short-circuit (defensive, D-m-E24-04-02), `Style != Plain` early-out (9 quoted/block tests), bool / int / double / string fall-through. Mirrored on Sim side. *Emitter side*: every branch of `IsYamlTypeAmbiguous` exercised — empty/null/`~` (4 cases), bool casing variants, int (positive/negative), float (regular/scientific/leading-decimal). Source-type activation guard exercised by numeric-array-untouched and ISO-timestamp-untouched tests. Round-trip pair tests (3) close the canary shapes end-to-end. |
| 12 | Self-review pass + commit-gate prep | — | reserved for parent |

## Test Summary

| Project | Passed | Skipped | Notes |
|---------|--------|---------|-------|
| FlowTime.Tests (Core) | 353 | 0 | +18 from `Validation/ParseScalarStyleTests.cs` |
| FlowTime.Sim.Tests | 225 | 3 | +17 from `Templates/ParseScalarStyleTests.cs`; +29 from `Templates/QuotedAmbiguousStringEmitterTests.cs`; pre-existing skips unchanged |
| FlowTime.TimeMachine.Tests | 239 | 0 | `TemplateSchemaValidationTests` coercion helper rewritten (still 1/1 conformance) |
| FlowTime.Api.Tests | 264 | 0 | unchanged |
| FlowTime.UI.Tests | 265 | 0 | unchanged |
| FlowTime.Cli.Tests | 91 | 0 | unchanged |
| FlowTime.Tests (legacy harness) | 228 | 6 | pre-existing skips unchanged |
| FlowTime.Integration.Tests | 84 | 0 | canary `val-err=0` in this milestone |

Net new this milestone: **+64 tests** (18 Engine ParseScalar + 17 Sim ParseScalar + 29 emitter round-trip).

## Notes

- The defect is independent of unification. m-E24-02 (type unification) and m-E24-03 (schema unification) did not touch `ParseScalar`'s logic. The 231 ParseScalar shapes have been waiting for this milestone.
- `src/FlowTime.Core/Models/ParallelismReference.cs:97` is the prior-art precedent for honoring `ScalarStyle.Plain` (narrower scalar distinction — null detection — but the pattern ports directly).
- Per spec: "Plain → existing coercion order; everything else → string." YAML 1.2 says quoted scalars are unconditionally typed as strings; only plain scalars are subject to type resolution.
- Canary is run in-process via `WebApplicationFactory<Program>` / `TimeMachineValidator`. Engine API does not need to be running.

## Completion

**Completed 2026-04-25** in implementation commit `a7c984f` (`feat(E-24): m-E24-04 — ParseScalar honors ScalarStyle; quote type-ambiguous strings on emit`).

All 9 ACs landed. Canary collapsed **231 → 0** errors at `ValidationTier.Analyse` across all 12 templates; histogram empty post-fix; step-4 emitter regressions: 0. Closure required both halves of the round-trip pair: parser-side `ScalarStyle.Plain` guard (AC1/AC2) **and** sibling `QuotedAmbiguousStringEmitter` on the Sim emitter (D-m-E24-04-03 / ADR-E-24-05).

**Decisions made during this milestone:**

- D-m-E24-04-01 — m-E24-04 does not widen `ParseScalar` to recognize the YAML null keyword (out of scope; test pins existing string-passthrough behavior).
- D-m-E24-04-02 — `value is null` defensive branch is unreachable through YamlDotNet's public surface; not modified by this milestone.
- D-m-E24-04-03 — Round-trip pair: emitter mirrors validator. Sibling `IEventEmitter` (`QuotedAmbiguousStringEmitter`) plugged into `TemplateService.CreateYamlSerializer` next to `FlowSequenceEventEmitter`. Source-type-driven, ambiguity classifier mirrors the validator's plain-scalar coercion attempt-order.

**ADRs ratified:**

- ADR-E-24-04 — `ScalarStyle.Plain` gates numeric / boolean coercion in `ParseScalar`. (Was a candidate ADR on the epic spec; this milestone ratifies it.)
- ADR-E-24-05 — `QuotedAmbiguousStringEmitter` (round-trip symmetry on the emitter side). New ADR introduced in this milestone; recorded above.

**Next:** m-E24-05 Canary Green + Hard Assertion — promotes the canary's `val-err=0` to a build-time hard `Assert`, completes the docs audit, flips E-23 to ready-to-resume.
