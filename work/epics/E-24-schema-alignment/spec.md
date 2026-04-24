---
id: E-24-schema-alignment
status: planning
depends_on:
completed:
---

# E-24: Schema Alignment

## Goal

Unify FlowTime's post-substitution model representation. One C# type. One YAML schema. One validator. `SimModelArtifact` is **deleted**. Sim builds the unified model type directly; the Engine accepts and parses the same type. Every field has exactly one declaration site. `TemplateWarningSurveyTests` reports `val-err=0` across all twelve templates at `ValidationTier.Analyse`, promoted to a hard build-time assertion. `ModelValidator` deletion (E-23) then becomes a mechanical cleanup.

## Context

### The split is accidental drift, not a designed interface

An investigation into `SimModelArtifact`'s purpose (agent `a5aa3dfe26394aff5`) established that the current two-type split was never a deliberate architectural decision:

- **`SimModelArtifact` was born in commit `ce9ec9e` (Oct 2025)** shaped to Sim's authoring-time template schema (`template.schema.json`) — not to `docs/schemas/model.schema.yaml`, which predated it by a month and already described what the Engine consumes. The two types never shared a contract.
- **`docs/schemas/README.md:34` claims parity that the code does not deliver.** The file asserts that `SimModelArtifact` and `ModelDefinition` describe the same shape. Reality: the only boundary between them is a YAML string, and that YAML string fails the Engine's own schema validation in every one of the twelve shipped templates today.
- **No C# type boundary exists.** `SimModelArtifact` is serialized to YAML in Sim; the Engine parses that YAML into `ModelDefinition`. No shared interface, no projection method, no round-trip contract. Drift is the default; alignment has never been enforced.
- **Each feature addition paid a duplication tax.** Commit `51a99b9` added classes to both types in the same commit (+23 lines to `SimModelArtifact`, +28 to `ModelDefinition`). `ModelDefinition` acquired `wipLimit` / `wipLimitSeries` / `wipOverflow` that `SimModelArtifact` lacks. `SimModelArtifact` acquired `window` / `generator` / `mode` / top-level `metadata` that no Engine consumer reads.
- **Only `provenance` has genuine dual concern** — both sides already agree on the keys, they just disagree on casing (snake_case in the schema, camelCase in `SimProvenance` emission and in Sim's Engine `ProvenanceMetadata` reader).

The m-E23-01 bisection survey (agent `a07d52c12dcaf3538`, evidence at `work/epics/E-23-model-validation-consolidation/m-E23-01-schema-alignment-tracking.md` → "AC4 canary re-run, full-shape audit (2026-04-24)") surfaced 16 distinct divergence shapes and 726 validator errors across the twelve templates. Most of those divergences dissolve under unification; a few (the `ParseScalar` defect, the `outputs[].as` semantic, the `nodes[].source` forward contract) are substantive decisions that survive.

### Option A was rejected; Option E was ratified

The initial epic draft proposed **Option A**: keep the two types, introduce a projection layer (`SimModelArtifact.ToEngineSubmission()`), let the schema describe only the submission payload. Option A preserves the accidental split — it makes the leak a designed feature and accepts the ongoing duplication tax for every future field addition. The user rejected Option A in favor of **Option E: unify**. One type. One schema. One validator. `SimModelArtifact` deleted outright. Sim builds the unified type directly; the Engine parses it.

Unification is the foundationally-right answer because:

1. **The split has no defender.** No code is protected by `SimModelArtifact`'s existence; no external consumer has a reason to care which type shape is on the wire. The only artefact the split produces is drift.
2. **Forward-only is cheap.** The biggest historical cost of unification (migration of stored bundles) is removed by the user's forward-only stance: existing bundle YAML is obsolete; Sim regenerates from templates going forward.
3. **`Template` stays distinct.** Authoring-time templates (pre-substitution, parameters, Liquid expressions) are genuinely a different contract. `Template` remains its own type with its own schema (`template.schema.json`). The merge target is `SimModelArtifact → unified model`, not `Template → unified model`.

### Truth Discipline precedent

The 2026-04-23 Truth Discipline guard (*"'API stability' does not mean 'keep old functions around.'"*) already rejected delegation shims in E-23. Option E extends the same discipline to types: once `SimModelArtifact` has no callers outside its own graph, it is deleted in the same change. No coexistence window, no "temporary" dual-type state, no projection layer that becomes permanent.

## Scope

### In Scope

- **Unify the post-substitution model type.** A single unified type replaces `SimModelArtifact` entirely. Exact home (`FlowTime.Core`, `FlowTime.Contracts`, or a new `FlowTime.Contracts.Model` namespace) is decided by m-E24-01; the constraint is "one type, discoverable from both Sim and the Engine."
- **`Template` remains distinct.** Authoring templates (pre-substitution) stay at their current shape and schema (`template.schema.json`). Unification applies only to the post-substitution model.
- **Sim builds the unified type directly.** `SimModelBuilder` is rewritten to emit the unified type. `SimNode`, `SimOutput`, `SimProvenance`, `SimTraffic`, `SimArrival`, `SimArrivalPattern` are deleted in the same change — Truth Discipline: no callers left, delete.
- **Engine intake reads the unified type.** `ModelDefinition` either becomes the unified type or is replaced by it. Whichever path, exactly one type is used on both sides.
- **One schema: `docs/schemas/model.schema.yaml`.** camelCase throughout. `provenance` is declared as a first-class block inside the unified schema with camelCase keys. Fields emitted by Sim today with no Engine consumer (`window`, `generator`, top-level `metadata`, top-level `mode`) are **dropped from emission** — they are leaked authoring state, not schema pass-through candidates. Any genuinely traceability-worthy field moves into the `provenance` block.
- **One validator: `ModelSchemaValidator`.** Validates both Sim's emitted output and the Engine's intake. Sim's `TemplateSchemaValidator` continues to validate the *authoring template* (pre-substitution) — that is genuinely a different contract — but the Sim→Engine model output uses the same validator the Engine uses.
- **`ParseScalar` scalar-style fix** in both `ModelSchemaValidator` and `TemplateSchemaValidator`. Unification does not eliminate this defect; it is an independent validator bug that lands in the same epic for convergence. Mirrored fix, mirrored tests. The test-side coercion helper in `tests/FlowTime.TimeMachine.Tests/TemplateSchemaValidationTests.cs:134-169` is updated or replaced so tests do not silently mask the fix.
- **Canary promoted to hard assertion** at epic close. `TemplateWarningSurveyTests` fails the build on non-zero `val-err`.
- **Forward-only discipline.** No migration of stored bundles. No compatibility reader for the old two-type YAML shape. Any code path that reads the old shape is identified and either regenerates from template or is deleted if nothing current needs it.
- **Docs alignment.** `docs/schemas/README.md` rewritten to reflect the unified reality. `docs/architecture/` entries that describe the "two schemas" world are corrected or archived.

### Out of Scope (firm)

- **`ModelValidator` deletion.** Stays with E-23 (m-E23-03, post-E-24 resume). E-24 makes the deletion mechanical; it does not perform it.
- **Bundle / run migration.** Forward-only per user direction. Existing stored bundles are obsolete. Sim regenerates from templates. No migration code is written; the forward-only stance is documented in the spec and any residual migration helper that surfaces during m-E24-02 is deleted rather than kept.
- **UI work.** No Blazor changes. No Svelte changes. `nodes[].metadata` remains a load-bearing `GraphService` concern — its schema declaration is already correct on the Engine side and stays.
- **New validator features.** No line/column mapping, no LSP integration, no compile-time rule extraction, no per-field suggestions.
- **`Template` redesign.** Authoring-time template schema and type remain unchanged.
- **E-15 Telemetry Ingestion runtime.** E-24 decides the `nodes[].source` contract under unification so E-15 starts aligned; E-15's actual implementation is not part of this epic.
- **Deprecated schema fields.** No reintroduction of `binMinutes` or other previously-retired shapes. Forward-only.
- **External-consumer compatibility windows.** No camelCase aliases in provenance. No dual-field acceptance. No migration mode. The schema changes in one cut.

## Constraints

- **Exactly one type, one schema, one validator.** Any deviation from "exactly one" needs a written design justification in m-E24-01's tracking doc.
- **Forward-only.** No compatibility reader, no "accept either shape" branch, no version-detection logic. If something changes shape, runs and fixtures are regenerated.
- **"Right, not easy" applied per field.** For every field currently on `SimModelArtifact`, m-E24-01 records: current emission site, whether any Engine consumer reads it, the decision (keep + declare, drop entirely, or move into `provenance`), and the rationale. The "easy option" is named as rejected; the "foundationally-right option" is chosen.
- **Truth Discipline guard at the type layer.** No compatibility shim. No temporary accommodation. When a type or function has no callers after the refactor, it is deleted in the same change — not retained as a dead alternative entry point.
- **Mirrored fixes.** The `ParseScalar` defect has two homes (`ModelSchemaValidator`, `TemplateSchemaValidator`). Fix both in the same milestone or drift re-opens. Same constraint applies to the test-side coercion helper.
- **camelCase everywhere.** No snake_case survives in `docs/schemas/model.schema.yaml` at epic close. `grep -rn "generated_at\|model_id\|template_id\|template_version" docs/schemas/model.schema.yaml` returns zero hits.
- **Byte-identical `POST /v1/run` success** for every currently-valid template (after Sim regenerates its template outputs under the unified type). Error responses may have different phrasing; HTTP status codes and JSON shape remain unchanged.

## Success Criteria

- [ ] `SimModelArtifact` is deleted. `grep -rn "SimModelArtifact" --include="*.cs"` returns zero hits.
- [ ] `SimNode`, `SimOutput`, `SimProvenance`, `SimTraffic`, `SimArrival`, `SimArrivalPattern` are deleted. A sibling `grep` confirms.
- [ ] One C# type represents the post-substitution model. `SimModelBuilder` emits it directly; the Engine parses it directly. `ModelDefinition` either *is* the unified type or has been replaced by it.
- [ ] `docs/schemas/model.schema.yaml` declares exactly one model shape including its `provenance` block. camelCase throughout. `grep -rn "generated_at\|model_id\|template_id\|template_version" docs/schemas/model.schema.yaml` returns zero hits.
- [ ] `ModelSchemaValidator` is the single runtime validator for post-substitution model YAML. `TemplateSchemaValidator` is limited to pre-substitution templates (`template.schema.json`).
- [ ] `TemplateWarningSurveyTests.Survey_Templates_For_Warnings` reports `val-err=0` across all twelve templates at `ValidationTier.Analyse`. The test is a hard `Assert` — non-zero `val-err` fails the build.
- [ ] `POST /v1/run` returns byte-identical success responses for every currently-valid template in `templates/*.yaml` with default parameters. Error responses may have different phrasing for currently-invalid models; HTTP status codes and response JSON shape remain unchanged.
- [ ] `ModelSchemaValidator.ParseScalar` and `TemplateSchemaValidator.ParseScalar` both honor `YamlScalarNode.Style`. Dedicated scalar-style tests cover `Plain` / `SingleQuoted` / `DoubleQuoted` variants for scalars that look like bool / int / double. The test-side coercion helper in `tests/FlowTime.TimeMachine.Tests/TemplateSchemaValidationTests.cs` is updated or replaced so tests do not mask the fix.
- [ ] Full `.NET` test suite passes: `dotnet test FlowTime.sln` is green with zero regressions. Any pre-existing flakes remain the same flakes — E-24 introduces no new timing sensitivity.
- [ ] `docs/schemas/README.md` accurately describes one-schema reality. Any `docs/architecture/` entry that described a two-schema world is corrected or archived.
- [ ] E-23 can resume cleanly. `ModelSchemaValidator` and the unified type agree by construction; m-E23-02 (call-site migration) and m-E23-03 (`ModelValidator` delete) become byte-trivial mechanical cleanup.

## Open Questions

The six design questions below are resolved by m-E24-01 as written design decisions before any other milestone starts. Each answer goes into the m-E24-01 tracking doc with its rejected alternative named.

| Question | Blocking? | Resolution path |
|----------|-----------|-----------------|
| Where does the unified type live? `FlowTime.Core`, `FlowTime.Contracts`, or a new `FlowTime.Contracts.Model` namespace? | Yes — gates m-E24-02 | m-E24-01 decides. Default leaning: `FlowTime.Contracts` — it is the shared-contracts project and Sim already references it. Decision is recorded with the rejected options. |
| Does `ModelDefinition` become the unified type, or is it replaced by a new type? `ModelDefinition` has `wipLimit` fields `SimModelArtifact` lacks; `SimModelArtifact` has Sim-only fields that are dropping. The unified type is their union minus leaked state. | Yes — gates m-E24-02 | m-E24-01 decides. Default leaning: extend `ModelDefinition` in place and rename if the expanded scope warrants. Decision captures the name, the namespace, and any members that need introduction or rename. |
| `outputs[].as` semantics under unification: optional (reflecting that auto-added outputs need no filename) or required (every output declares a filename)? Today `EnsureSemanticsOutputs` auto-adds entries without `as`, producing 366 of 495 non-defect validator errors. | Yes — gates m-E24-02 and m-E24-03 | m-E24-01 decides. Two clean options: (a) `as` is optional and auto-added outputs omit it; (b) `as` is required and the emitter synthesizes a default (e.g. `as = "{id}.csv"`) for auto-added outputs. The decision cites the Engine consumer (if any) that reads `as`. |
| `nodes[].source` forward contract: drop entirely until E-15 lands and defines it, or declare as optional in the unified schema now? Sim currently emits empty-string defaults that YamlDotNet does not auto-omit. | No — scoped to m-E24-01 | m-E24-01 decides. Default leaning: drop — E-24's discipline is "schema declares what has a consumer." E-15 declares the field when it builds the consumer. If dropped, Sim omits emission via `OmitDefaults` or `ShouldSerialize` guard. |
| Provenance shape in the unified schema: flat or nested? `SimProvenance` nests `parameters` as a sub-dictionary. Engine's `ProvenanceMetadata` is flat. Unification requires one shape. | Yes — gates m-E24-03 | m-E24-01 decides. Default leaning: nested `parameters` sub-map (matches Sim's current shape and preserves author-intent grouping). Decision cites both sides' current shape and the reader adjustments required. |
| Canary: integration test against live Engine API (current shape) or fast unit-style check (`ModelSchemaValidator.Validate(...)` in-process)? Under unification, the unit-level variant becomes attractive because render-and-validate is fully in-process. | No — scoped to m-E24-05 | m-E24-05 decides. Default: keep the integration test; add a parallel fast unit variant if CI cost becomes visible. The hard-assertion gate in m-E24-05 is whichever variant runs in CI. |

## Risks (optional)

| Risk | Impact | Mitigation |
|------|--------|------------|
| The unified type touches more Engine-side code than anticipated (every consumer of `ModelDefinition` + every producer of `SimModelArtifact`). | Medium | m-E24-01's inventory enumerates every call site before m-E24-02 plans the type-unification change. If the scope is large, m-E24-02 subdivides deliberately — the split plan is written before work begins, not after. |
| Forward-only means existing run fixtures in `tests/` and sample bundles in `docs/samples/` produce the obsolete two-type YAML shape and stop validating. | Medium | Forward-only regeneration: m-E24-02 regenerates every fixture and sample under the unified shape in the same milestone. No compatibility reader survives. Anything that cannot be regenerated is deleted rather than patched. |
| `ParseScalar` fix regresses a test that was accidentally relying on coercion (e.g. a quoted integer). | Low | m-E24-04 runs the full solution suite immediately after the fix. Any regression is investigated case-by-case — correct response is to update the test to author the scalar correctly. `tests/FlowTime.TimeMachine.Tests/TemplateSchemaValidationTests.cs` is the known hotspot; its coercion helper is updated in the same milestone as the validator fix. |
| `outputs[].as` decision forces a cascade: if optional, every Engine consumer that reads `as` is audited; if required with synthesized default, the filename convention must be stable and discoverable. | Medium | m-E24-01 decides before any code lands. The chosen side names the convention (if synthesize) or the consumer audit (if drop required). m-E24-03 lands the schema edit; m-E24-02 lands any emitter synthesis. |
| External consumers (VSCode YAML plugin, docs tooling) read `docs/schemas/model.schema.yaml` directly and break on the snake_case → camelCase rename. | Low | The schema is consumed internally. Sibling checkouts are read-only per project rule; a quick `grep` in `/workspaces/flowtime-sim-vnext` for `generated_at` / `model_id` surfaces any drift before m-E24-03 lands the rename. |

## Milestones

Sequencing: design decisions first (m-E24-01 is doc-only), then type unification (m-E24-02 is the largest architectural change), then schema consolidation (m-E24-03 describes what m-E24-02 built), then validator defect (m-E24-04 is independent and mirrored), then canary close (m-E24-05 makes the zero assertion permanent and flips E-23 to ready-to-resume). m-E24-04 can be parallelized with m-E24-02 / m-E24-03 if desired, but the conservative default is serial after m-E24-03 so the canary run in m-E24-05 operates on a fully-converged stack.

- [m-E24-01-inventory-and-design-decisions](./m-E24-01-inventory-and-design-decisions.md) — **in-progress (branch `milestone/m-E24-01-inventory-and-design-decisions`, started 2026-04-24).** Doc-only milestone. Every field on `SimModelArtifact` and `ModelDefinition` gets a recorded decision (keep / drop / move). The six open questions above are answered. Unified type home, name, and provenance shape are named. No code, no schema changes. · depends on: —
- [m-E24-02-unify-model-type](./m-E24-02-unify-model-type.md) — Introduce the unified type. Rewrite `SimModelBuilder` to emit it directly. Switch Engine intake to parse it directly. Delete `SimModelArtifact` and its satellite types (`SimNode`, `SimOutput`, `SimProvenance`, `SimTraffic`, `SimArrival`, `SimArrivalPattern`). Drop leaked-state fields (`window`, `generator`, top-level `metadata`, top-level `mode`) from emission. Regenerate fixtures and samples under the unified shape. · depends on: m-E24-01
- [m-E24-03-schema-unification](./m-E24-03-schema-unification.md) — Rewrite `docs/schemas/model.schema.yaml` to describe the unified type. camelCase provenance block. Apply every m-E24-01 decision: `outputs[].as`, `classes: minItems`, `nodes[].source`, provenance shape. Update `docs/schemas/README.md` and any architecture doc that still describes the two-schema world. · depends on: m-E24-02
- [m-E24-04-parser-validator-scalar-style-fix](./m-E24-04-parser-validator-scalar-style-fix.md) — `ParseScalar` honors `YamlScalarNode.Style` in both `ModelSchemaValidator` and `TemplateSchemaValidator`. Mirror the fix. Dedicated tests cover Plain / SingleQuoted / DoubleQuoted / Literal / Folded styles for scalars that look like bool / int / double. Test-side coercion helper updated or replaced. · depends on: m-E24-03 (conservative; can run parallel to m-E24-02 / m-E24-03 if desired)
- [m-E24-05-canary-green-hard-assertion](./m-E24-05-canary-green-hard-assertion.md) — Canary promoted to hard assertion: `TemplateWarningSurveyTests` fails the build on non-zero `val-err`. Full `.NET` suite green. Docs audit complete. E-23 status flips to ready-to-resume. Decision entry logs E-24 close. · depends on: m-E24-04

## ADRs

- **ADR-E-24-01 — Unify the post-substitution model type.** `SimModelArtifact` and `ModelDefinition` collapse to one type. `Template` (authoring-time, pre-substitution) stays distinct. Rationale: the split is accidental drift per commit history, has no type boundary, and has compounded a duplication tax since October 2025. Rejected alternative (Option A): keep both types with a projection layer — preserves the accidental leak as a designed feature and accepts the ongoing duplication tax. Ratified in m-E24-01.
- **ADR-E-24-02 — Forward-only regeneration.** No compatibility reader for the old two-type YAML shape. Existing stored bundles are obsolete; Sim regenerates from templates going forward. Rationale: the biggest historical cost of unification (bundle migration) is removed by forward-only; compatibility readers are the shape the Truth Discipline guard explicitly rejects. Rejected alternative: migration window with dual-shape acceptance. Ratified in m-E24-01.
- **ADR-E-24-03 — Schema declares only consumed fields.** Fields emitted by Sim with no Engine consumer are dropped from emission, not declared as schema pass-through. Rationale: Truth Discipline — the schema is the contract; the contract declares what exists, not what is tolerated. Rejected alternative: declare `window` / `mode` / `generator` / top-level `metadata` in the schema as optional pass-through. Ratified in m-E24-01.
- **ADR-E-24-04 (candidate) — `ScalarStyle.Plain` gates numeric / boolean coercion in `ParseScalar`.** YAML 1.2 resolution requires honoring scalar style; quoted strings must short-circuit to string. Rejected alternative: tag-based inference (`!!str`, `!!int`) — YAML's resolver already embeds the distinction in `Style`; a second inference layer is redundant. Ratified in m-E24-04.

## References

### Upstream input

- **`SimModelArtifact` purpose investigation (agent `a5aa3dfe26394aff5`):** established that the split was accidental, not designed. Commit `ce9ec9e` (Oct 2025) introduced `SimModelArtifact` shaped to the template schema, not `model.schema.yaml` which predated it. `docs/schemas/README.md:34` claimed parity the code does not deliver. No C# type boundary exists — the only interface between Sim and Engine is a YAML string, and that YAML string fails the Engine's own schema validation in every shipped template today.
- **Survey agent `a07d52c12dcaf3538`:** produced the 16-row divergence table and the full-shape audit (726 total validator errors). Captured in `work/epics/E-23-model-validation-consolidation/m-E23-01-schema-alignment-tracking.md` → "AC4 canary re-run, full-shape audit (2026-04-24)".
- **m-E23-01 tracking doc:** held on branch `milestone/m-E23-01-schema-alignment` as stashed input material for m-E24-01. Contains the rule audit, the ParseScalar defect prior-art investigation, and the full-shape audit that motivated this epic.
- **Uncommitted schema edits on `milestone/m-E23-01-schema-alignment`:** three additions to `docs/schemas/model.schema.yaml` (`grid.start`, `nodes[].metadata`, `nodes[].source`) — treated as input material for m-E24-01's design-decision review, not as commitments. Under unification, `grid.start` probably stands (Engine consumer confirmed); `nodes[].metadata` stays as a load-bearing `GraphService` concern; `nodes[].source` probably reverts (no consumer until E-15).

### Epic and decision context

- **E-23 Model Validation Consolidation:** `work/epics/E-23-model-validation-consolidation/spec.md` — paused at m-E23-01 pending E-24. m-E23-02 and m-E23-03 become mechanical once E-24 closes.
- **D-2026-04-24-035:** E-23 ratification — established the "delete, do not delegate" discipline that E-24 extends to types.
- **D-2026-04-24-036:** E-23 pause and E-24 creation. Within E-24 planning, **Option E (unify)** was ratified over **Option A (two types with projection)**.

### Truth Discipline

- `.ai-repo/rules/project.md` → Truth Discipline Guards — precedence hierarchy, camelCase rule, "API stability ≠ keep old functions around" (2026-04-23), "Do not restate a canonical contract in many places from memory", "Do not let adapter/UI projection become the only place where semantics exist", "Do not keep 'temporary' compatibility shims without explicit deletion criteria."

### Source-code pointers

- `src/FlowTime.Sim.Core/Templates/SimModelArtifact.cs` — slated for deletion (and its satellite types `SimNode`, `SimOutput`, `SimProvenance`, `SimTraffic`, `SimArrival`, `SimArrivalPattern`)
- `src/FlowTime.Sim.Core/Templates/SimModelBuilder.cs` — the Sim emitter; rewritten to produce the unified type directly
- `src/FlowTime.Core/Models/ModelDefinition.cs` — Engine-side model type; either becomes the unified type or is replaced
- `src/FlowTime.Core/Models/ModelParser.cs` — Engine intake; switches to parse the unified type directly
- `src/FlowTime.Sim.Service/Program.cs:1079` — Sim wire-serialization site; produces the unified type
- `src/FlowTime.TimeMachine/Orchestration/RunOrchestrationService.cs:627` — Engine-side intake; parses the unified type
- `src/FlowTime.Core/Models/ModelSchemaValidator.cs:222-246` — `ParseScalar` defect site
- `src/FlowTime.Sim.Core/Templates/TemplateSchemaValidator.cs:173-197` — mirrored `ParseScalar` defect
- `src/FlowTime.Core/Models/ParallelismReference.cs:97` — prior art for honoring `ScalarStyle.Plain`
- `tests/FlowTime.TimeMachine.Tests/TemplateSchemaValidationTests.cs:134-169` — test-side coercion helper that masks the defect
- `tests/FlowTime.Integration.Tests/TemplateWarningSurveyTests.cs` — the canary (committed to `main` per D-2026-04-24-035)
- `docs/schemas/model.schema.yaml` — the schema under unification
- `docs/schemas/README.md:34` — the parity claim the code does not deliver; rewritten at m-E24-03

### Downstream impact

- **E-23 resume:** `ModelSchemaValidator` and the unified type agree by construction once E-24 closes. m-E23-02 call-site migration becomes a mechanical `sed`-scale change; m-E23-03 `ModelValidator` delete becomes a one-file removal with its dedicated tests.
- **m-E21-07 Validation Surface (E-21):** consumes the single consolidated `ModelSchemaValidator` once both E-24 and E-23 close.
- **E-15 Telemetry Ingestion:** starts from a ratified `nodes[].source` forward contract (m-E24-01's decision) rather than inheriting a forward-declared placeholder with no consumer.
