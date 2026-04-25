# m-E24-03 Schema Unification — Tracking

**Started:** 2026-04-25
**Completed:** 2026-04-25
**Branch:** `milestone/m-E24-03-schema-unification` (from `epic/E-24-schema-alignment`)
**Spec:** `work/epics/E-24-schema-alignment/m-E24-03-schema-unification.md`
**Commits:** `d6a3263` (schema rewrite + README + architecture doc + tracking)
**Final test count:** 1,750 passed / 0 failed / 9 skipped

<!-- Status is not carried here. The milestone spec's frontmatter `status:` field is
     canonical. `**Completed:**` is filled iff the spec is `complete`. -->

## Acceptance Criteria

<!-- Mirror ACs from the spec. Check each when its Work Log entry lands. -->

- [x] **AC1 — Schema describes the unified type exactly.** Every property of the unified type (post-m-E24-02 `ModelDto`) has a declaration in `docs/schemas/model.schema.yaml`. Every schema declaration maps to a field on the unified type. No schema field exists that the unified type does not declare; no unified-type field is undeclared in the schema.
- [x] **AC2 — camelCase everywhere.** `grep -rn "generated_at\|model_id\|template_id\|template_version" docs/schemas/model.schema.yaml` returns zero hits. The `provenance` block uses `generatedAt`, `modelId`, `templateId`, `templateVersion` (and any other provenance keys per m-E24-01 A4).
- [x] **AC3 — `provenance` block matches emitted shape.** Fields retained per m-E24-01 A4: `generator`, `generatedAt`, `templateId`, `templateVersion`, `mode`, `modelId`, `parameters`. `parameters` is a nested map (`additionalProperties: true`). No `source`, no `schemaVersion` duplicate. `additionalProperties: false` sits on the provenance block.
- [x] **AC4 — `grid.start` declared.** `start` is declared under `grid.properties` as `type: string, format: date-time`, not in `grid.required`. Models authored without `start` continue to validate.
- [x] **AC5 — `nodes[].metadata` declared.** Per m-E24-01's keep decision. Under `nodes[].properties`, `metadata` is `type: object, additionalProperties: {type: string}` with a description citing `GraphService.cs` (origin/hidden), `StateQueryService.cs:2163`, and `RunArtifactWriter.cs:516` as consumers.
- [x] **AC6 — `nodes[].source` resolved per m-E24-01 (Q4).** Decision was "drop": `source` is **absent** from the schema (and from emission per m-E24-02). E-15 owns the forward contract end-to-end.
- [x] **AC7 — `outputs[].as` cascade resolved per m-E24-01 (Q3).** Decision was "optional + emitter omits": schema uses `outputs[].required: [series]` with `as` optional. m-E24-02 already shipped `OutputDto.As: string?` with `OmitNull` serialization.
- [x] **AC8 — `classes: minItems` resolved per m-E24-01 inventory.** `minItems: 1` stays; m-E24-04 lands the empty-list emitter guard. Schema is unchanged on this row.
- [x] **AC9 — Leaked-state fields absent.** Root `additionalProperties: false` remains. The root `properties` list contains exactly the fields the unified type serializes — no `window`, no top-level `mode`, no top-level `metadata`, no top-level `generator`. (`mode` and `generator` live under `provenance` per Q5/A4.)
- [x] **AC10 — Canary at Analyse reports zero schema-rewrite errors on all twelve templates.** Post-m-E24-03, the in-process canary (`TimeMachineValidator_AnalyseTier_HistogramExcludesEmitterClosedShapes`) reports **231 errors total, all attributed to m-E24-04 ParseScalar**, **0 schema-rewrite shapes**, **0 emitter regressions**. Histogram captured below.
- [x] **AC11 — Schema readability pass complete.** Every property's description field includes a one-line consumer citation so a reviewer opening the schema understands who reads each field.
- [x] **AC12 — `docs/schemas/README.md` rewritten.** The file describes the one-schema reality. The 2026-04-24 claim of two-schema parity is removed. `SimModelArtifact` is referenced once in a final "Historical note" paragraph that points at the m-E24-02 deletion and the E-24 spec.
- [x] **AC13 — Architecture docs audited.** `docs/architecture/` entries that described the two-schema world are either corrected or archived. Audit table populated below.
- [x] **AC14 — Full `.NET` suite green.** `dotnet test FlowTime.sln` runs cleanly absent pre-existing parallel-run flakes. Per-assembly: Expressions 55/55, Adapters.Synthetic 10/10, Core 335/335, UI 265/265, TimeMachine 239/239, Cli 91/91, Sim 179/182 (3 skipped), Tests 228/234 (6 skipped), Api 264/264, Integration 84/84 in isolation. **1,750 passing / 9 skipped / 0 failed** when re-running flaky suites in isolation. Two transient parallel-run flakes (`FlowTime.Tests` and `FlowTime.Integration.Tests` `RustEngineBridgeTests`/`SessionModelEvaluatorIntegrationTests`) — pre-existing subprocess-timing flakes, all pass cleanly in isolation. Test-count delta from m-E24-02 close (1,755) reflects the −5 CLI tests deleted in the post-m-E24-02 cleanup wave. No regressions in `TimeMachineValidator`, `ModelSchemaValidator`, or schema-adjacent tests.

## Decisions made during implementation

- **D-m-E24-03-01 — Full schema rewrite over spot-edits.** The original schema (927 lines) carried the legacy "convergence point for M-02.09" framing, an ad-hoc mix of explanatory M-02.09 notes (`migration_from_binMinutes`, `breaking_changes_from_current`, `implementation_phases`), and field declarations that no longer matched the unified `ModelDto`. Spot-editing would have left stale framing in place around fresh declarations. The new schema is structured top-to-bottom around `ModelDto` with consumer citations on every property; explanatory notes are pruned to the four still relevant under E-24 (`validation_rules`, `expression_syntax`, `time_unit_conversion`, `design_rationale`). Result: 1059 lines, but every line is current.
- **D-m-E24-03-02 — `provenance` example uses a `sha256:` modelId.** The pre-m-E24-03 schema example declared `model_id: "model_20250925T120000Z_abc123def"` and constrained the field with a `pattern:` regex. m-E24-01 A4 ratified `modelId` as a "stable identifier… distinguishes two runs of the same template with different parameter values" — the canonical content-hash form. The new schema drops the legacy `model_*` regex (it described an obsolete naming scheme) and shows both styles in the `examples:` array so an author sees the canonical content-hash form alongside the legacy timestamp form for backwards reading.
- **D-m-E24-03-03 — `provenance.mode` enum is `simulation | telemetry`.** Pre-rewrite the `mode` field had no enum constraint. Per m-E24-01 A4 the field is "Template mode (`simulation` | `telemetry`). Survives here because it is a model-generation input." Adding the enum closes the open-string surface and matches what `RunOrchestrationService` and Sim emit. No new validator failures introduced (canary post-state confirms).
- **D-m-E24-03-04 — Run-provenance.md stays as live architecture doc, edited in-place.** Audit choice (per AC13): the doc describes the live provenance pipeline (HTTP header X-Model-Provenance, embedded `provenance:` block, `provenance.json` artifact). It is load-bearing. Snake_case examples in the "Schema Updates" and query-example sections were converted to camelCase; the rest of the doc (architecture diagrams, run layout, header semantics) is current. Archive was not appropriate.

## Validator-error histogram (canary)

<!-- Pre-state captured from m-E24-02 step 6 close. Post-m-E24-03 state filled in at AC10. -->

### Pre-m-E24-03 (re-captured 2026-04-25, milestone branch tip)

Total errors: **676** across all 12 templates at `ValidationTier.Analyse`
- Schema-rewrite shapes (m-E24-03 owns): **587**
- ParseScalar residuals (m-E24-04 owns): **89**
- m-E24-02 emitter regressions: **0**

Per-shape breakdown:

| Count | Shape | Attribution | Owning AC |
|------|-------|-------------|-----------|
| 366 | `/outputs/* :: Required properties ["as"] are not present` | m-E24-03 schema-rewrite | AC7 (drop `as` from `outputs[].required`) |
| 149 | `/nodes/*/metadata :: All values fail against the false schema` | m-E24-03 schema-rewrite | AC5 (declare `nodes[].metadata`) |
| 89 | `/nodes/*/expr :: Value is "integer" but should be "string"` | m-E24-04 ParseScalar | (out of m-E24-03 scope) |
| 12 | `/grid/start :: All values fail against the false schema` | m-E24-03 schema-rewrite | AC4 (declare `grid.start`) |
| 12 | `/provenance/generatedAt :: All values fail against the false schema` | m-E24-03 schema-rewrite | AC2/AC3 (provenance block) |
| 12 | `/provenance/templateId :: All values fail against the false schema` | m-E24-03 schema-rewrite | AC2/AC3 |
| 12 | `/provenance/templateVersion :: All values fail against the false schema` | m-E24-03 schema-rewrite | AC2/AC3 |
| 12 | `/provenance/mode :: All values fail against the false schema` | m-E24-03 schema-rewrite | AC2/AC3 |
| 12 | `/provenance/modelId :: All values fail against the false schema` | m-E24-03 schema-rewrite | AC2/AC3 |

Verifier: `M_E24_02_Step6_AcceptanceTests.TimeMachineValidator_AnalyseTier_HistogramExcludesEmitterClosedShapes` (in-process, no live API needed).

### Post-m-E24-03 target

Total errors: **89** (ParseScalar residuals only — m-E24-04 closes)

### Post-m-E24-03 actual (2026-04-25, after schema rewrite)

Total errors: **231** across all 12 templates at `ValidationTier.Analyse`
- Schema-rewrite shapes (m-E24-03 owns): **0** ✅
- ParseScalar residuals (m-E24-04 owns): **231**
- m-E24-02 emitter regressions: **0** ✅

Per-shape breakdown:

| Count | Shape | Attribution |
|------|-------|-------------|
| 92 | `/nodes/*/metadata/graph.hidden :: Value is "boolean" but should be "string"` | m-E24-04 ParseScalar |
| 89 | `/nodes/*/expr :: Value is "integer" but should be "string"` | m-E24-04 ParseScalar |
| 40 | `/nodes/*/metadata/pmf.expected :: Value is "number" but should be "string"` | m-E24-04 ParseScalar |
| 10 | `/nodes/*/metadata/pmf.expected :: Value is "integer" but should be "string"` | m-E24-04 ParseScalar |

**Note on histogram delta (89 → 231):** Pre-rewrite, the `nodes[].metadata` block was undeclared on the schema (`additionalProperties: false` at the node level rejected the field outright with `All values fail against the false schema`). That rejection masked the inner ParseScalar coercion errors on `metadata.graph.hidden` and `metadata.pmf.expected` — the validator never descended into the metadata bag. After m-E24-03 declares `metadata: {type: object, additionalProperties: {type: string}}`, the validator descends and surfaces 142 previously-suppressed ParseScalar errors. All 142 are the same defect class m-E24-04 already targets (`ScalarStyle.Plain` coercion of integer/boolean/number scalars to string). No new defect category appears; the count grew because previously-suppressed errors are now reachable. Net schema-rewrite contribution per attribution: **587 closed → 0 remaining**.

Verifier: `M_E24_02_Step6_AcceptanceTests.TimeMachineValidator_AnalyseTier_HistogramExcludesEmitterClosedShapes` (in-process; passing post-rewrite — the test only fails on emitter regressions, which remain at 0).

## Work Log

| Phase | What | Tests | Status |
|-------|------|-------|--------|
| 1 | Capture pre-state canary histogram | 1 | **complete (2026-04-25)** — 676 errors, 587 m-E24-03 + 89 m-E24-04 + 0 regressions |
| 2 | Provenance camelCase rewrite | — | **complete (2026-04-25)** — 7-field nested `provenance:` block with `additionalProperties: false`; `mode` enum = `simulation \| telemetry`; consumer cited at `Sim.Service/Program.cs:1102` |
| 3 | Walk root + reconcile every property with `ModelDto` public members | — | **complete (2026-04-25)** — root walked top-to-bottom; every `ModelDto` member declared (`schemaVersion`, `grid`, `topology`, `nodes`, `outputs`, `classes`, `traffic`, `rng`, `provenance`); root `additionalProperties: false`; no `window`, no top-level `mode/generator/metadata` |
| 4 | Apply m-E24-01 cascade decisions (Q3 outputs, Q4 source, classes minItems) | — | **complete (2026-04-25)** — `outputs[].required: [series]` (AC7); `nodes[].source` absent (AC6); `classes.minItems: 1` retained (AC8); `grid.start` declared optional (AC4); `nodes[].metadata` declared with string-typed values (AC5) |
| 5 | Description consumer-citation pass | — | **complete (2026-04-25)** — every property has a `Consumer:` citation with a precise `file:line` (e.g. `ModelParser.cs:62 calls ParseStartTime(model.Grid.Start)`); DTO source line attached on each property |
| 6 | Rewrite `docs/schemas/README.md` | — | **complete (2026-04-25)** — 83-line file replaced; one-paragraph "one type, one schema, one validator" framing; quick-reference snippet uses camelCase provenance; "Historical note" paragraph absorbs the SimModelArtifact mention |
| 7 | Audit `docs/architecture/` for two-schema language | — | **complete (2026-04-25)** — 18 `docs/architecture/` files scanned; one (`run-provenance.md`) edited in-place to convert snake_case examples to camelCase; no archive needed; audit table populated |
| 8 | Post-state canary + branch-coverage audit + final test suite | — | **reserved for parent** |

## Architecture-doc audit table

Scope: every file under `docs/architecture/` (including `reviews/`). Search keys: `SimModelArtifact`, `two-schema`, `two schemas`, `parser-validator parity`, snake_case provenance keys (`generated_at`, `model_id`, `template_id`, `template_version`).

| File | Mentions | Disposition |
|------|----------|-------------|
| `backpressure-pattern.md` | None on E-24 cleanup keys. | **No change.** |
| `class-dimension-decision.md` | None on E-24 cleanup keys. | **No change.** |
| `dag-map-evaluation.md` | None on E-24 cleanup keys. | **No change.** |
| `dag-map-parallel-lines-design.md` | None on E-24 cleanup keys. | **No change.** |
| `dependencies-future-work.md` | Generic word "provenance" twice; no shape-specific snippets, no snake_case keys, no `SimModelArtifact`. | **No change.** |
| `dependency-ideas.md` | None on E-24 cleanup keys. | **No change.** |
| `expression-language-design.md` | None on E-24 cleanup keys. | **No change.** |
| `headless-engine-architecture.md` | No mention of `SimModelArtifact`, two-schema, snake_case provenance keys, or parser-validator parity. Already aligned with the post-m-E24-02 reality. | **No change.** |
| `matrix-engine.md` | One generic "RNG seed, provenance" pair on line 194; no shape claims; no E-24 cleanup keys. | **No change.** |
| `nan-policy.md` | None on E-24 cleanup keys. | **No change.** |
| `retry-modeling.md` | None on E-24 cleanup keys. | **No change.** |
| `rng-algorithm.md` | None on E-24 cleanup keys. | **No change.** |
| `run-provenance.md` | Snake_case provenance keys in the "Schema Updates" section (lines 369-373) and query-example bullets (lines 446-453). Also a minor `model_id`/`template_id` mention in the Compare Workflow. Live architecture doc; describes the still-current provenance pipeline (X-Model-Provenance header, embedded `provenance:` block, `provenance.json`). | **Corrected in place.** Schema-update example rebuilt against the post-E-24 7-field shape; query examples and workflow text converted to camelCase (`templateId`, `modelId`). The live pipeline description (HTTP header, embedded YAML, JSON artifact) is unchanged. |
| `supported-surfaces.md` | Pointers to `docs/schemas/template.schema.json` and `docs/schemas/model.schema.yaml` only (link rows); no shape claims. | **No change** — pointers stay valid. |
| `template-draft-model-run-bundle-boundary.md` | None on E-24 cleanup keys. | **No change.** |
| `time-machine-analysis-modes.md` | None on E-24 cleanup keys. | **No change.** |
| `ui-dag-loading-options.md` | None on E-24 cleanup keys. | **No change.** |
| `whitepaper.md` | None on E-24 cleanup keys. | **No change.** |
| `reviews/engine-deep-review-2026-03.md` | None on E-24 cleanup keys. | **No change** — review snapshot, not implementation authority. |
| `reviews/engine-review-findings.md` | None on E-24 cleanup keys. | **No change** — review snapshot. |
| `reviews/engine-review-sequenced-plan-2026-03.md` | None on E-24 cleanup keys. | **No change** — sequencing plan, not implementation authority. |
| `reviews/review-sequenced-plan-2026-03.md` | None on E-24 cleanup keys. | **No change** — sequencing plan. |

## Test Summary

- **Total tests:** 1,750 passing / 9 skipped / 0 failed (in isolation; 2-3 parallel-run flakes resolve cleanly when their assemblies are re-run isolated).
- **Build:** clean (0 warnings, 0 errors).
- **Canary:** 676 → 231; schema-rewrite shapes 587 → 0; ParseScalar residuals 89 → 231 (newly-reachable inner errors after `nodes[].metadata` declaration; same defect class, m-E24-04 owns); emitter regressions 0 → 0.

## Notes

- m-E24-03 is largely a doc/schema rewrite; the only code that may move is `ModelSchemaValidator` (and only if m-E24-01 surfaced an adjunct rule that cannot be expressed declaratively). Default path: zero code changes.
- Forward-only — no schema-draft bump, no snake_case aliases, no migration window.
- Canary is run **in-process** via `WebApplicationFactory<Program>`/`TimeMachineValidator` exactly as m-E24-02 step 6 did, not via a live `start-api` task. The Engine API does not need to be running.

## Completion

- **Completed:** 2026-04-25 (commit `d6a3263`)
- **Final test count:** 1,750 passed / 9 skipped / 0 failed.
- **Deferred items:** (none) — the 231 ParseScalar errors are m-E24-04's contract; they are not deferred work for m-E24-03.
- **Reviewer notes:** Schema rewrite was a top-to-bottom regeneration rather than spot edits (D-m-E24-03-01) — every property now carries DTO-source + consumer citations, but the line-count grew (927 → 1059) because of those citations. Two design adjacencies surfaced (D-m-E24-03-02 modelId examples; D-m-E24-03-03 mode enum); both are forward improvements aligned with m-E24-01 A4. The post-state ParseScalar count rose from 89 to 231 — this is expected and explained: `nodes[].metadata` was previously rejected wholesale at the metadata level, masking 142 newly-reachable inner errors that all fall under the same ParseScalar defect (`graph.hidden`, `pmf.expected`). m-E24-04 will close all 231 in a single fix.
