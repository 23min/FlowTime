---
id: E-23-model-validation-consolidation
status: ready-to-resume
depends_on: E-24-schema-alignment
completed:
---

# E-23: Model Validation Consolidation

## Amendment 2026-04-25 (E-24 closed; ready to resume)

**E-24 Schema Alignment closed 2026-04-25.** All five E-24 milestones landed on `epic/E-24-schema-alignment`: `SimModelArtifact` deleted; the unified `ModelDto`+`ProvenanceDto` lives in `FlowTime.Contracts`; `docs/schemas/model.schema.yaml` was rewritten top-to-bottom against the unified type with nested 7-field camelCase provenance; the mirrored `ParseScalar` `ScalarStyle.Plain` guard plus the sibling `QuotedAmbiguousStringEmitter` closed the round-trip pair end-to-end; and `TemplateWarningSurveyTests.Survey_Templates_For_Warnings` is now a hard-asserting build-time gate (`val-err == 0` across every shipped template at `ValidationTier.Analyse`).

E-23 is now ready to resume. The original m-E23-02 (call-site migration to `ModelSchemaValidator`) and m-E23-03 (`ModelValidator` deletion) are byte-trivial mechanical cleanup at this point — `ModelSchemaValidator`, the schema, Sim's emitter, and the Engine's reader literally share a single unified type definition. Reentry order: re-examine the `milestone/m-E23-01-schema-alignment` branch's stashed input material (most of it has been absorbed by E-24 milestones — the `grid.start` schema edit, the `nodes[].metadata` schema edit, the rule-audit tracking doc); reconcile the m-E23-01 spec status with the E-24 close; then start m-E23-02 from the new `epic/E-23-model-validation-consolidation` branch tip after merging the closed E-24 work into `main` (or rebasing onto `epic/E-24-schema-alignment` post-merge).

Decision: `D-2026-04-25-038` logs E-24's close and E-23's ready-to-resume flip.

## Amendment 2026-04-24 (pause)

**Paused pending E-24 Schema Alignment.** m-E23-01's bisection survey surfaced 16 distinct schema-vs-reality divergences across five classifications (architectural seam, systematic rule violation, validator defect, emitter drift, emitter redundancy) — far beyond the single `grid.start` gap the E-23 context anticipated. Applying the "right, not easy" discipline per-field is broader than E-23's consolidation thesis. **E-24 Schema Alignment** (`work/epics/completed/E-24-schema-alignment/spec.md`) takes over the convergence work. E-23 resumes when E-24 closes; at that point m-E23-02 and m-E23-03 become byte-trivial because `ModelSchemaValidator`, the schema, Sim's emitter, and the Engine's reader will actually agree on every currently-valid template.

The uncommitted m-E23-01 artefacts (three schema edits on `milestone/m-E23-01-schema-alignment`, plus the rule-audit tracking doc and two `work/gaps.md` entries) are preserved as input material for E-24 m-E24-01. They are not committed; they are not discarded. See `D-2026-04-24-036` for the full context.

**Amendment 2026-04-24 (Option E ratified within E-24):** Within E-24 planning, **Option E (unify `SimModelArtifact` and `ModelDefinition` into a single type, forward-only)** was ratified over Option A (preserve two types with a projection layer). E-24's milestone count collapsed from six to five. At E-24 close, `ModelSchemaValidator`, the schema, Sim's emitter, and the Engine's reader will share a single unified type definition — m-E23-02 and m-E23-03 remain mechanical cleanup, with the simplification that `ModelSchemaValidator` already validates the unified type used by every call site. See `D-2026-04-24-037` for the ratification context.

Pointers:
- E-24 epic spec: `work/epics/completed/E-24-schema-alignment/spec.md`
- m-E23-01 tracking (input to m-E24-01): held on branch `milestone/m-E23-01-schema-alignment` (stashed)
- Decisions: `work/decisions.md` → `D-2026-04-24-036` (E-23 paused, E-24 created) · `D-2026-04-24-037` (Option E ratified) · `D-2026-04-25-038` (E-24 closed; E-23 ready to resume)

## Goal

Replace FlowTime's two silently-disagreeing model validators with a single schema-driven validator (`ModelSchemaValidator`) used everywhere a model YAML is checked. Finish a migration that has been half-done since schema-based validation landed: the pre-schema hand-rolled `ModelValidator` is deleted outright, every rule it enforced is expressed declaratively in `docs/schemas/model.schema.yaml`, and every call site flows through the single consolidated entry point.

## Context

FlowTime currently ships two validators in `FlowTime.Core`:

- **`ModelValidator.Validate`** (`src/FlowTime.Core/Models/ModelValidator.cs`) — hand-rolled, imperative, uses YamlDotNet `IgnoreUnmatchedProperties()`. Does not consult the JSON schema. Used by `POST /v1/run` (`src/FlowTime.API/Program.cs:657`) and the Engine CLI (`src/FlowTime.Cli/Program.cs:76`).
- **`ModelSchemaValidator.Validate`** (`src/FlowTime.Core/Models/ModelSchemaValidator.cs`) — reads `docs/schemas/model.schema.yaml` and performs full JSON-schema evaluation. Used inside `TimeMachineValidator.ValidateSchema` (`src/FlowTime.TimeMachine/Validation/TimeMachineValidator.cs:46`), reachable from `POST /v1/validate` and the `flowtime validate` CLI command.

The two validators disagree today. An evidence-gathering survey across all twelve templates under `templates/*.yaml` (test file `tests/FlowTime.Integration.Tests/TemplateWarningSurveyTests.cs`, currently uncommitted) shows:

- Every template runs successfully through `POST /v1/run` (which validates via `ModelValidator`).
- Every template emits at least one tier-3 `ModelSchemaValidator` error, all of the form `/grid/start: All values fail against the false schema`.

Root cause: `docs/schemas/model.schema.yaml` declares `additionalProperties: false` on `grid` (line 71) and never adds `grid.start`, but Sim's `SimModelBuilder.PopulateGridStartFromWindow` deliberately copies `window.start` into `grid.start`, and the Engine's `ModelParser.ParseFromCoreModel` reads it into `StartTime` (`src/FlowTime.Core/Models/ModelParser.cs:62`). That start timestamp is load-bearing — Blazor's Time-Travel UX (`Topology.razor`, `Dashboard.razor.cs`, `TimeTravelMetricsClient.cs`) uses it for window math, and Svelte's m-E21-06 heatmap consumes it indirectly via `state_window`'s `timestampsUtc`. The fix is not "stop Sim emitting the field" — the fix is "recognize the field in the schema, collapse to the one validator that reads the schema, delete the one that does not."

Git history (`git log -- src/FlowTime.Core/Models/ModelValidator.cs`) shows `ModelValidator` landed in `6432057 feat(core): implement schema validation (M2.9 Phase 2 Step 2)` as the pre-schema home-rolled checker. `ModelSchemaValidator` was added later, once the JSON schema existed. They have coexisted ever since. This is an unfinished migration, not a deliberate two-validator architecture.

This epic exists in direct service of the 2026-04-23 addition to Truth Discipline Guards (`.ai-repo/rules/project.md`): *"'API stability' does not mean 'keep old functions around.' When a function has no production callers after a refactor, delete it and its tests in the same change — do not retain it as a dead alternative entry point under the banner of keeping the existing surface stable."* `ModelValidator` is that exact shape: a leftover entry point that has stopped earning its keep now that `ModelSchemaValidator` reads schema truth directly.

## Scope

### In Scope

- Update `docs/schemas/model.schema.yaml` so every rule currently asserted by `ModelValidator` is expressed declaratively in the schema, including `grid.start` as a permitted (optional) ISO-8601 timestamp property, `schemaVersion` as `const: 1`, legacy top-level `arrivals`/`route` rejection via `additionalProperties: false` on the root (already present — confirm), and legacy node-level `expression` rejection if any node currently tolerates it.
- Migrate every production call site from `ModelValidator.Validate` to `ModelSchemaValidator.Validate`:
  - `src/FlowTime.API/Program.cs:657` (`POST /v1/run`)
  - `src/FlowTime.Cli/Program.cs:76` (Engine CLI `run` entry)
  - `src/FlowTime.TimeMachine/Validation/TimeMachineValidator.cs:50` (remove the `ModelValidator` delegation — `ModelSchemaValidator` already runs on line 46, the second call is the vestigial layer)
- Delete `src/FlowTime.Core/Models/ModelValidator.cs`. Delete tests that exercise it in isolation (`tests/FlowTime.Tests/Schema/TargetSchemaValidationTests.cs`, `tests/FlowTime.Tests/Schema/SchemaVersionTests.cs`, `tests/FlowTime.Tests/Schema/SchemaErrorHandlingTests.cs`). Rewrite any rule whose coverage would otherwise be lost against `ModelSchemaValidator` so the covered behavior survives the delete.
- Update `tests/FlowTime.Integration.Tests/SimToEngineWorkflowTests.cs:38,120` to use `ModelSchemaValidator.Validate` directly.
- Audit error-message phrasing: any test that asserts on a specific `ModelValidator` error string must be updated to match `ModelSchemaValidator`'s JSON-schema-shaped messages, or relaxed to assert on semantic properties (`IsValid == false`, presence of a matching error code) rather than exact phrasing.
- Commit `tests/FlowTime.Integration.Tests/TemplateWarningSurveyTests.cs` (currently uncommitted on the m-E21-06 branch) as a regression canary: after the schema update it must report zero `val-err` across all twelve templates when run at `ValidationTier.Analyse`.
- Rerun the full solution test suite and the Svelte UI suites. Zero regressions in Blazor, Svelte, CLI, or API behavior.

### Out of Scope

- Any change to `SimModelBuilder`'s emission of `grid.start` from `window.start`. Sim is correct. This epic aligns the schema to Sim's live output; it does not modify Sim.
- Any change to Blazor UI or Svelte UI code. Both remain untouched. `TimeTravelMetricsClient`, `Topology.razor`, `Dashboard.razor.cs`, and Svelte `state_window` consumers stay on their current contracts.
- The active-validation UI deferred from m-E21-06. That work continues to live in m-E21-07 Validation Surface once this epic lands; E-23 does not absorb it.
- New validator features: line/column mapping, LSP integration, incremental validation, per-field suggestions, compile-time rule extraction, or any other expansion of the validation *product*. The goal here is consolidation, not expansion.
- Reintroducing any deprecated schema fields (e.g., `binMinutes`). Per project rule.
- Tiered validation parity across external consumers (Sim UI, MCP, agents) beyond the two existing validators. The `TimeMachineValidator` tier model (`Schema` / `Compile` / `Analyse`) is preserved as-is; only its internal dependency on `ModelValidator` is removed.

## Constraints

- `ModelValidator` is **deleted**, not delegated and not wrapped. No compatibility shim. No "temporary alias." The single consolidation endpoint after this epic is `ModelSchemaValidator.Validate`.
- The schema must remain the source of truth for all structural rules. If a rule currently enforced only by `ModelValidator` cannot be expressed declaratively in JSON Schema draft-07, the epic surfaces that before milestone work and decides case-by-case (move rule into `ModelSchemaValidator` as an adjunct like `ValidateClassReferences` already is, or accept the rule as redundant with a compile-time check). Do not silently drop rules.
- `grid.start`, once added to the schema, must be **optional** (templates without `start` must continue to validate). Its format must be expressed as `type: string` with `format: date-time` or a pattern compatible with `DateTime.TryParse` in invariant culture, matching what `ModelParser.ParseStartTime` already accepts.
- Byte-for-byte parity on the `/v1/run` success path. Any caller that submits a currently-valid model must continue to receive the same response, including error response shapes on currently-invalid models (phrasing may differ; status codes and JSON shape must not).
- Survey test `TemplateWarningSurveyTests` is the canary. At epic close, it must report zero `val-err` across all templates when run at `ValidationTier.Analyse`. Non-zero `val-warn` or `run-warn` are informational, not regressions against this epic's acceptance.
- No reintroduction of `FlowTime.Generator` nor any variant of the deleted provenance pipeline. Per D-2026-04-07-019 Path B.

## Success Criteria

- [ ] `src/FlowTime.Core/Models/ModelValidator.cs` is deleted from the repository at epic close. `grep -rn "ModelValidator\b" --include="*.cs"` returns zero results outside of `ValidationResult` (which remains in `FlowTime.Core` as the shared result type).
- [ ] `ModelSchemaValidator.Validate` is the single model-YAML validator called from `POST /v1/run`, `POST /v1/validate`, `flowtime` Engine CLI, `flowtime validate` Time Machine CLI, `TimeMachineValidator`, and all test paths.
- [ ] All twelve templates under `templates/*.yaml`, when rendered with default parameters and submitted to `TimeMachineValidator.Validate(..., ValidationTier.Analyse)`, report zero errors. The survey test `TemplateWarningSurveyTests.Survey_Templates_For_Warnings` is the observable guard.
- [ ] `docs/schemas/model.schema.yaml` declares `grid.start` as an optional string with an ISO-8601-compatible format. `schemaVersion: const: 1` remains; legacy top-level fields `arrivals` and `route` remain rejected via root-level `additionalProperties: false`; legacy node-level `expression` continues to fail validation.
- [ ] `POST /v1/run` produces byte-identical success responses for all currently-valid models post-migration. For currently-invalid models, error responses preserve HTTP status and JSON shape; error phrasing may change and is covered by an explicit phrasing audit in m-E23-02.
- [ ] Full `.NET` test suite passes: `dotnet test FlowTime.sln` is green. UI vitest and Playwright remain green (Svelte and Blazor surfaces untouched — any red there is a regression).
- [ ] `tests/FlowTime.Integration.Tests/TemplateWarningSurveyTests.cs` is committed to the repository and is wired into the integration test project so the twelve-template canary is a reproducible check, not a hand-run diagnostic.

## Open Questions

| Question | Blocking? | Resolution path |
|----------|-----------|-----------------|
| Does every rule in `ModelValidator` have an equivalent JSON-Schema expression, or are some rules (e.g., integer-like strings for `bins`, legacy `expression` detection inside node dictionaries) not cleanly expressible? | Yes — gates m-E23-01 scope | m-E23-01 begins with a rule-by-rule audit. If a rule cannot be expressed declaratively, it moves into `ModelSchemaValidator` as an adjunct alongside the existing `ValidateClassReferences`. Audit output is the first deliverable of m-E23-01. |
| Should the survey test be committed directly to `main` as a chore bootstrap commit, or as the first deliverable of m-E23-01? | No — sequencing detail | Recommended path: commit as first deliverable of m-E23-01 on the epic integration branch. Rationale: the test belongs to this epic's regression story, and committing it standalone to `main` risks a red canary until the schema fix lands. User to confirm. |
| Milestones 02 and 03 may be cleanly combinable (migrate call sites and delete the type in a single change) since the migration is mechanical once the schema supports every current input. Keep separate for rollback safety or merge for velocity? | No — sequencing detail | Default: keep separate (m-E23-02 = migrate and leave `ModelValidator` dead-reachable; m-E23-03 = delete). If the m-E23-02 audit shows the migration is byte-trivial, merge into a single milestone. User to confirm preference before m-E23-01 starts. |
| Do any non-test callers of `ModelValidator` exist in sibling repositories (MCP, external tools) that would break on deletion? | Unknown — worth a quick sweep | m-E23-01 includes a cross-repo grep against sibling checkouts visible to this workspace (treated read-only per project rule). If callers exist, this epic either absorbs them or coordinates deletion. |
| What is `ModelSchemaValidator`'s cost at hot paths like `POST /v1/run`? It loads and compiles the schema lazily once (`Lazy<JsonSchema?>`), but per-request JSON-schema evaluation is heavier than the hand-rolled check. Is that measurably meaningful? | No — informational | m-E23-02 includes a before/after benchmark (`dotnet run` timing on a representative template). If latency grows by more than ~2 ms per request we document it; if it grows materially more we escalate. No work planned to mitigate until we have a number. |

## Risks (optional)

| Risk | Impact | Mitigation |
|------|--------|------------|
| Error-message phrasing change breaks downstream consumers (e.g., Blazor surfacing validation messages verbatim, CLI scripts grep'ing stdout for specific text) | Medium | m-E23-02 includes an error-phrasing audit: diff `ModelValidator` vs. `ModelSchemaValidator` error output on a representative invalid-model corpus; document the deltas; update tests; scan UI code for any string-match consumers. |
| Hidden rule in `ModelValidator` that `ModelSchemaValidator` does not already cover gets dropped silently when `ModelValidator` is deleted | Medium | m-E23-01 rule audit is the primary defense. Second defense: the survey test + full .NET suite run after delete. Third defense: keep the `ModelValidator` source file in a draft commit on the milestone branch until the m-E23-02 migration has green CI, so rollback is one revert. |
| `docs/schemas/model.schema.yaml` is also consumed by external editors/validators (VSCode YAML plugin, documentation tooling). Adding `grid.start` changes their reported valid-shape | Low | The addition is permissive (new optional property), not restrictive. External consumers will accept more, not less. Document the schema bump in the milestone tracking doc. |
| Error-response JSON shape diverges between validators more than expected (e.g., `ModelSchemaValidator` concatenates instance-location + message; `ModelValidator` returns flat strings) | Low | The shape change is one level of concatenation. `POST /v1/run` already collapses errors to `string.Join("; ", ...)`. Verify API contract tests pass with the new shape and adjust if required. |

## Milestones

Sequencing: schema alignment first (no code changes, establishes the regression canary), then call-site migration (mechanical switch-over with phrasing audit), then the `ModelValidator` delete (cleanup + assertion that nothing calls it anymore). The default is three milestones; the Open Questions list above captures a collapse option.

- [m-E23-01-schema-alignment](./m-E23-01-schema-alignment.md) — Update `model.schema.yaml` so every current template validates cleanly under `ModelSchemaValidator`; commit the template-warning survey as a regression canary. · **largely absorbed by E-24 (closed 2026-04-25); `model.schema.yaml` rewritten by E-24 m-E24-03 against the unified `ModelDto`; canary committed and now hard-asserts under E-24 m-E24-05.** Reentry needs a status reconciliation pass on the `milestone/m-E23-01-schema-alignment` branch's stashed input material. · depends on: — (E-24 prerequisite cleared 2026-04-25)
- [m-E23-02-call-site-migration](./m-E23-02-call-site-migration.md) — Switch every production call site and test from `ModelValidator.Validate` to `ModelSchemaValidator.Validate`; audit error-message phrasing. · **ready (E-24 closed 2026-04-25 — `D-2026-04-25-038`)** · depends on: m-E23-01, E-24 (cleared)
- [m-E23-03-delete-model-validator](./m-E23-03-delete-model-validator.md) — Delete `ModelValidator.cs` and its dedicated tests; assert `grep` returns zero production callers. · **ready (waits on m-E23-02)** · depends on: m-E23-02

## ADRs

- **ADR-E-23-01 — Delete, do not delegate.** The target state is a single validator (`ModelSchemaValidator`). `ModelValidator` is **deleted** rather than rewired to forward to `ModelSchemaValidator`, because: (1) a forwarding shim is a new compatibility layer that the Truth Discipline guard added 2026-04-23 explicitly forbids, (2) there are no external-surface users of `ModelValidator` that would justify a shim, (3) a delete is cheaper to maintain than a forward for all future readers of the code. Recorded as a decision in `work/decisions.md` before m-E23-01 begins.
- **ADR-E-23-02 (candidate) — Schema as the single structural contract.** `ModelSchemaValidator` reads `docs/schemas/model.schema.yaml` and performs JSON-schema evaluation plus the `ValidateClassReferences` adjunct. Any structural rule not expressible in JSON Schema draft-07 is added as an adjunct method alongside `ValidateClassReferences` rather than as a second parallel validator. Deferred pending the m-E23-01 rule audit — if every rule expresses cleanly, this ADR is not needed; if not, record it with the rationale and the enumerated adjunct rules.

## References

- `src/FlowTime.Core/Models/ModelValidator.cs` — slated for deletion
- `src/FlowTime.Core/Models/ModelSchemaValidator.cs` — the consolidation target
- `src/FlowTime.TimeMachine/Validation/TimeMachineValidator.cs` — the tiered validator that transitively uses both today
- `src/FlowTime.API/Program.cs:657` — `POST /v1/run` call site
- `src/FlowTime.Cli/Program.cs:76` — Engine CLI call site
- `docs/schemas/model.schema.yaml` — schema to update in m-E23-01
- `src/FlowTime.Core/Models/ModelParser.cs:62` — where `grid.start` is consumed
- `src/FlowTime.Sim.Core/Templates/SimModelBuilder.cs:34-36` — where `grid.start` is emitted
- `tests/FlowTime.Integration.Tests/TemplateWarningSurveyTests.cs` — regression canary (to be committed in m-E23-01)
- Project rule addition (2026-04-23): `.ai-repo/rules/project.md` → Truth Discipline Guards → *"'API stability' does not mean 'keep old functions around.'"*
- Related epic: `work/epics/E-21-svelte-workbench-and-analysis/spec.md` m-E21-07 Validation Surface — consumes the consolidated validator once this epic lands.
