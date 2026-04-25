# m-E24-01 Inventory and Design Decisions — Tracking

**Started:** 2026-04-24
**Completed:** 2026-04-25
**Branch:** `milestone/m-E24-01-inventory-and-design-decisions` (from `epic/E-24-schema-alignment`)
**Spec:** `work/epics/E-24-schema-alignment/m-E24-01-inventory-and-design-decisions.md`
**Commits:** `c43e8c0` (single doc-only commit)

<!-- Status is not carried here. The milestone spec's frontmatter `status:` field is
     canonical. `**Completed:**` is filled iff the spec is `complete`. -->

## Acceptance Criteria

<!-- Mirror ACs from the spec. Check each when its Work Log entry lands. -->

- [x] **AC1 — Full-field inventory complete.** Row-per-field table covering every public member of `SimModelArtifact` and `ModelDefinition` plus every distinct shape the m-E23-01 survey identified. Each row names: field path, emitter / producer (Sim or Engine), consumer (`file:line` or "no consumer" with grep verification note), current schema declaration, classification (keep-as-is / rename / move-to-provenance / drop / open), decision column with chosen side. **Landed 2026-04-24** — see "Full-field Inventory" section: root-level fields + per-sub-type rows (`grid.*`, `nodes[].*`, `outputs[].*`, `traffic.arrivals[].*`), four surprises surfaced as m-E24-02 investigation items.
- [x] **AC2 — The six epic-spec open questions resolved.** For each question in the epic spec's Open Questions table, the tracking doc records the decision, the rationale, and the rejected alternative(s). See "Design Decisions — Six Open Questions" below. **Landed 2026-04-24** — all six questions ratified with easy/right options, rationale, rejected alternatives, and consequences; five sub-decisions (A1–A5) recorded under "Additional Decisions."
- [x] **AC3 — Leaked-state fields have drop plans.** `window`, `generator`, top-level `metadata`, and top-level `mode` each have a decision row. Default disposition "drop from emission" with grep evidence. If any row deviates (e.g. "move into `provenance`"), the new location and reader are named. **Landed 2026-04-24** — covered inside "Full-field Inventory → Root-level fields": each of the four leaked-state fields has an explicit row with "no consumer at root (grep: …)" evidence and "remove from Sim emission" disposition. `mode` and `generator` relocate into the unified `provenance` block per Q5/A4 (where they survive as load-bearing provenance fields); `window` and top-level `metadata` drop entirely (the load-bearing `window.start` survives as `grid.start`; the load-bearing template metadata subset survives as `provenance.templateId` / `templateVersion`).
- [x] **AC4 — `SimModelArtifact` satellite types have disposition plans.** `SimNode`, `SimOutput`, `SimProvenance`, `SimTraffic`, `SimArrival`, `SimArrivalPattern` each have a row naming whether they merge into the unified type's equivalents as-is, with changes, or get deleted. Any field asymmetries flagged with keep / drop decision. **Landed 2026-04-24** — see "Satellite-type Disposition" section: all 6 satellites plus `SimModelArtifact` itself slated for deletion; each names its Contracts equivalent and the consumer sites needing update. Template-authoring-side types (outside E-24 scope) explicitly excluded.
- [x] **AC5 — `provenance` block is named in full.** Final list of fields on the unified `provenance` block written out with camelCase keys. Each field has a consumer citation and a rationale. `schemaVersion` duplicate decided. `mode` decided. **Landed 2026-04-24** — see "Unified `provenance` Block — Final Field List": 7 fields (`generator`, `generatedAt`, `templateId`, `templateVersion`, `mode`, `modelId`, `parameters`), `source` and `schemaVersion` explicitly dropped, consumer-citation table for today-vs-post-E-24, casing closure note referencing the 72-error m-E23-01 violation.
- [x] **AC6 — Uncommitted m-E23-01 schema edits re-examined.** `grid.start`, `nodes[].metadata`, `nodes[].source` each have explicit keep / modify / revert decisions with rationale. **Landed 2026-04-24** — see "Uncommitted m-E23-01 Schema Edits — Re-examined under Unification": `grid.start` KEEP (carry to m-E24-03); `nodes[].metadata` KEEP (carry to m-E24-03); `nodes[].source` REVERSE (Q4 drops it from schema and emission). Each verdict cites the ratified framework (Q1–Q6, ADR-E-24-03, Truth Discipline guards).
- [x] **AC7 — Forward-only disposition confirmed.** Every code path that might read the old two-type YAML shape (fixtures, sample bundles, test helpers, any bundle reader) named with disposition: regenerate, delete, or (rarely) document as historical reference. No compatibility reader survives. **Landed 2026-04-24** — see "Forward-only Disposition — Old Two-type YAML Shape Consumers": 10 production call sites + 5 test files enumerated with per-site dispositions; pre-E-24 bundles discarded on disk (forward-only regeneration); no compat reader.
- [x] **AC8 — No production change.** Diff against `epic/E-24-schema-alignment` touches only this tracking doc, this milestone spec, and (optionally) reference-pointer updates in the epic spec. No edits to `src/`, `tests/`, `docs/schemas/`, or other code-bearing surfaces. Full `.NET` test suite green throughout (untouched baseline). **Verified 2026-04-24** — `git diff --stat HEAD` enumerates only doc files; see "Validation → Final no-production-change verification."

## Decisions made during implementation

<!-- Populated one-by-one as the user drives through the six open questions and
     any other decisions that surface during the per-field inventory. -->

- **2026-04-24 — Q1:** Unified type home is `FlowTime.Contracts`. See "Design Decisions — Q1."
- **2026-04-24 — Q2:** `ModelDto` becomes the unified wire type; `SimModelArtifact` and satellites are deleted. See "Design Decisions — Q2."
- **2026-04-24 — Q3:** `outputs[].as` is optional; presence means "also export as CSV." See "Design Decisions — Q3."
- **2026-04-24 — Q4:** `nodes[].source` is dropped from Sim emission and not declared on the unified schema; E-15 owns the forward contract end-to-end. See "Design Decisions — Q4."
- **2026-04-24 — Q5:** Provenance is a nested block; `parameters` is a nested map; keys are camelCase; `source` collapses into `generator`; duplicate `schemaVersion` drops. See "Design Decisions — Q5."
- **2026-04-24 — Q6:** Canary is an integration test using `WebApplicationFactory<Program>`; no external port dependency; no graceful skip. See "Design Decisions — Q6."
- **2026-04-24 — A1:** Delete `GridDto.LegacyStart` compat shim during m-E24-02.
- **2026-04-24 — A2:** Rename `GridDto.StartTimeUtc` → `GridDto.Start` (wire name stays `start`).
- **2026-04-24 — A3:** Canonical grid start field name is `start` across schema + templates + Sim emission.
- **2026-04-24 — A4:** Canonical provenance field set is `{generator, generatedAt, templateId, templateVersion, mode, modelId, parameters}`.
- **2026-04-24 — A5:** camelCase everywhere — no snake_case keys on the unified schema or its emitters.

## Design Decisions — Six Open Questions

<!-- Each question follows the spec-mandated format: question text, rejected
     "easy option," chosen "foundationally-right option," rationale, consequences.
     Placeholders ready for the user to drive answers in subsequent turns. -->

### Q1 — Unified type home

**Question:** Where does the unified type live? `FlowTime.Core`, `FlowTime.Contracts`, or a new `FlowTime.Contracts.Model` namespace?

**Blocking:** Yes — gates m-E24-02.

**Easy option (rejected):**
- Keep the unified type in `FlowTime.Sim.Core` and have Engine reach into Sim; OR put it in `FlowTime.Core` and have Sim depend on Engine. Either option avoids touching `FlowTime.Contracts`, but both flip the dependency direction between surfaces.

**Foundationally-right option (chosen):**
- `FlowTime.Contracts` — the project already exists for exactly this purpose. Both Sim and Engine already reference it. Existing `ModelDto`, `ModelService`, and `TimeGrid` already live there.

**Rationale:**
- `FlowTime.Contracts` is purpose-built as the neutral wire-contract home and is already referenced by both Sim and Engine. Placing the unified type there avoids any dependency-direction flip between surfaces and keeps the contract co-located with the types that already live there (`ModelDto`, `ModelService`, `TimeGrid`). No new project or namespace is introduced.

**Rejected alternatives:**
- `FlowTime.Core` — would require Sim to depend on Engine core, flipping the dependency direction.
- `FlowTime.Sim.Core` — would require Engine to depend on Sim, also inverting the intended layering.
- New `FlowTime.Contracts.Model` project — over-engineering for a single type family; a new csproj buys nothing that a namespace inside the existing project doesn't already provide.

**Decision:** Unified type lives in `FlowTime.Contracts`.

---

### Q2 — Unified type name

**Question:** Does `ModelDefinition` become the unified type, or is it replaced by a new type? `ModelDefinition` has `wipLimit` fields `SimModelArtifact` lacks; `SimModelArtifact` has Sim-only fields that are dropping.

**Blocking:** Yes — gates m-E24-02.

**Easy option (rejected):**
- Create a new unified type and delete both `SimModelArtifact` and `ModelDto`. More upheaval: re-points every existing consumer of `ModelDto` (already in production use) for no material gain over adapting the existing type.

**Foundationally-right option (chosen):**
- Use the existing `ModelDto` as the unified wire type. It is already designed for this role, has no leaked-state fields, and already lives in `FlowTime.Contracts`. Delete `SimModelArtifact` and its satellite types.

**Rationale:**
- `ModelDto` is already the neutral wire representation: it is emitted and consumed across surfaces, it has no Sim-only bundle-traceability fields, and it lives in the project the unification converges on. Adopting it avoids inventing a third type, preserves existing Engine consumers, and localizes the churn to Sim's emitter plus a small set of additive changes (`ProvenanceDto`, `GridDto` rename). `ModelDefinition` stays as Engine's runtime type — wire-vs-runtime separation is load-bearing; `ModelService.ParseAndConvert` continues to go `YAML → ModelDto → ModelDefinition`.

**Rejected alternatives:**
- Create a new unified type and delete both `SimModelArtifact` and `ModelDto` — maximum churn for zero architectural benefit; `ModelDto` already plays the role this new type would play.
- Collapse `ModelDto` and `ModelDefinition` into a single type — breaks the wire-vs-runtime separation: `ModelDefinition` carries evaluator-oriented fields (e.g. `wipLimit`) that do not belong on the wire; `ModelDto` carries wire-only shape the evaluator does not want. Merging conflates concerns the parse-and-convert boundary exists to separate.

**Decision:** `ModelDto` becomes the unified wire type; `SimModelArtifact` and all its satellites are deleted.

**Consequences (input to m-E24-02):**

- Delete `SimModelArtifact`, `SimNode`, `SimOutput`, `SimProvenance`, `SimTraffic`, `SimArrival`, `SimArrivalPattern`.
- `SimModelBuilder` rewrites to emit `ModelDto` directly.
- Add a new `ProvenanceDto` to `ModelDto` (shape defined by Q5).
- `ModelDefinition` remains as Engine runtime type; `ModelService.ParseAndConvert` continues `YAML → ModelDto → ModelDefinition`.
- Delete `GridDto.LegacyStart` compat shim (lines 42-49 of `src/FlowTime.Contracts/Dtos/ModelDtos.cs`) — it is precisely the "temporary compatibility shim without explicit deletion criteria" the 2026-04-23 Truth Discipline guard forbids.
- Sub-decision: canonical grid start field name is `start` (schema + templates + Sim emission all use it). `GridDto.StartTimeUtc` renames to `Start` on the C# side; the YAML wire name stays `start`.

---

### Q3 — `outputs[].as` semantics under unification

**Question:** Optional (reflecting that auto-added outputs need no filename) or required (every output declares a filename)? Today `EnsureSemanticsOutputs` auto-adds entries without `as`, producing 366 of 495 non-defect validator errors.

**Blocking:** Yes — gates m-E24-02 and m-E24-03.

**Easy option (rejected):**
- Make `as` required and have the emitter synthesize `"{series}.csv"` defaults for auto-added outputs. Silences the validator errors without changing the validator, but invents filenames for outputs that never get written — the classic cargo accommodation the "don't describe a target contract in present tense unless it is live" guard forbids.

**Foundationally-right option (chosen):**
- `as` is optional. "Output" means "pin this series in the run result"; CSV export is a subset — applied only when `as` is set. This matches what auto-added outputs actually are (result pins, not CSV file declarations).

**Rationale:**
- The 366 validator errors are signal, not noise: the schema demanded a filename the emitter had no reason to supply. Making `as` optional aligns the contract with actual semantics — the output list records which series to retain; the `as` field opts a given series in for CSV materialization. Auto-added outputs stay filename-less; user-authored outputs with `as` still produce CSVs. The validator stops complaining because the contract now matches reality, not because we manufactured a filename to satisfy the validator.

**Rejected alternatives:**
- `as` required with synthesized defaults — invents filenames for outputs that never get written; violates the "no target contract in present tense unless it is live" guard.
- Introduce a separate `export: true/false` flag — over-engineers the split. `as` presence is already a natural on/off signal for "produce a CSV"; a second flag just duplicates that information.

**Decision:** `outputs[].as` is optional; presence means "also export this series as CSV."

**Consequences (input to m-E24-02 / m-E24-03):**

- Schema: `as` moves out of `outputs[].required`.
- `ModelDto.OutputDto.As` becomes nullable `string?` (not `string` with default `"out.csv"`).
- `SimOutput.As` is already nullable — no Sim-side type change.
- `EnsureSemanticsOutputs` continues to emit outputs without `as`; user-authored outputs with `as` continue to produce CSVs.

---

### Q4 — `nodes[].source` forward contract

**Question:** Drop entirely until E-15 lands and defines it, or declare as optional in the unified schema now? Sim currently emits empty-string defaults that YamlDotNet does not auto-omit.

**Blocking:** No — scoped to m-E24-01.

**Easy option (rejected):**
- Declare `source` as an optional string on the unified schema now, as forward-compatibility for E-15. Ships a contract line nothing honors — Truth Discipline calls this "describe a target contract in present tense unless it is live," and the 2026-04-23 "no temporary accommodation" guard forbids blessing schema fields that have no reader.

**Foundationally-right option (chosen):**
- Drop `source` from Sim emission. `Template` keeps `source:` as an authoring field, and `TemplateValidator`'s "values or source" gate stays. `SimModelBuilder` does NOT copy `Source` into the output model. `ModelDto.NodeDto` does NOT get a `Source` field. The schema does NOT declare it. When E-15 Telemetry Ingestion lands, E-15 adds all four pieces (schema declaration, `NodeDto.Source`, Engine reader, Sim emission) coherently as one contract.

**Rationale:**
- Schema declares what is consumed. With no Engine reader for `source`, declaring it is blessing a contract nobody honors — exactly the "do not describe a target contract in present tense unless it is live" guard. Keeping `source` on `Template` preserves the existing 8-template authoring pattern (templates use `source:` as input to `SimModelBuilder`); dropping it from the emitted model means the output stops carrying a field no consumer will read. E-15 gets a clean slate: when the consumer exists, the schema declaration, DTO field, reader, and emission all land together.

**Rejected alternatives:**
- Declare optional now — violates the 2026-04-23 "no temporary accommodation" guard; the field has no reader.
- Drop from `Template` entirely — breaks the existing 8-template authoring pattern, including the `TemplateValidator` "values or source" gate.

**Decision:** `nodes[].source` is dropped from Sim emission and not declared on the unified schema. `Template` retains `source:` as an authoring-only field. E-15 owns the forward contract end-to-end when it lands.

---

### Q5 — Provenance shape in the unified schema

**Question:** Flat or nested? `SimProvenance` nests `parameters` as a sub-dictionary. Engine's `ProvenanceMetadata` is flat. Unification requires one shape.

**Blocking:** Yes — gates m-E24-03.

**Easy option (rejected):**
- Represent `parameters` as a nested list of objects (`[{name, value}, …]`) — schema-stricter but awkward: it forces a list shape over a natural name→value lookup and diverges from the map shape Sim already emits.

**Foundationally-right option (chosen):**
- Nested map: `parameters: { name: value, … }`. Values are permissive (`additionalProperties: true`) — parameter values are forensic data, not a contract Engine consumes. Provenance stays on the unified schema (load-bearing for reproducibility). Duplicate `schemaVersion` drops (root already carries it). `source` and `generator` collapse — keep `generator`, drop `source`. All keys are camelCase per the project rule.

**Rationale:**
- Parameters are the canonical answer to "why did this run produce these numbers?" — load-bearing for reproducibility and debugging even though Engine does not read them. A nested map matches the natural lookup shape (`parameters["arrivalRate"]`) and matches what Sim already emits. Permissive values reflect that parameter values are forensic, not contract — the unified schema should describe the field, not typecheck every parameter. `schemaVersion` at the root already identifies the contract version; duplicating it inside `provenance` is redundant emission (one of the canary's identified rows). `source` and `generator` both say `"flowtime-sim"` today — keep `generator` (more specific: names the producer), drop `source` (no additional information). All keys camelCase closes the systematic rule violation m-E23-01 identified (72 validator errors across 4 shapes).

**Rejected alternatives:**
- Flat `parameters` (keys directly on `provenance`) — risks key collisions with reserved provenance fields (`mode`, `generator`, etc.) and muddies the "these are the inputs, those are the production metadata" distinction.
- Nested list `[{name, value}, …]` — awkward lookup shape; diverges from Sim's current map emission without architectural benefit.
- Drop `parameters` entirely — loses the primary reproducibility surface; a run without its input parameter values is harder to audit or replay.
- Keep `schemaVersion` inside provenance — redundant with root; one of the canary rows specifically flagged it.
- Keep both `source` and `generator` — duplicate information (both emit `"flowtime-sim"`).
- snake_case keys (`generated_at`, `model_id`, `template_id`, `template_version`) — violates the project-wide camelCase rule and is the exact systematic rule violation m-E23-01 bisected.

**Decision:** Provenance is a nested block on the unified schema; parameters are a nested map; keys are camelCase; redundant fields drop.

**Canonical provenance field set (post-E-24):**

- `generator`
- `generatedAt`
- `templateId`
- `templateVersion`
- `mode`
- `modelId`
- `parameters` (nested map, `additionalProperties: true`)

No `source`. No `schemaVersion` duplicate. All keys camelCase.

---

### Q6 — Canary variant for m-E24-05

**Question:** Integration test against live Engine API (current shape), fast unit-style check (`ModelSchemaValidator.Validate(...)` in-process), or both?

**Blocking:** No — scoped to m-E24-05.

**Easy option (rejected):**
- Keep the current external-port-8081-plus-graceful-skip setup — the silent-skip risk is the exact drift the canary should catch. OR downgrade to a fast static unit test — blind to DTO-conversion and Engine-acceptance drift, the categories the canary exists to catch.

**Foundationally-right option (chosen):**
- Integration test using `WebApplicationFactory<Program>` to spin up the Engine API in-process. No external port dependency, no graceful skip, deterministic in CI and dev. This is the pattern `FlowTime.Api.Tests` already uses for every other API integration test.

**Rationale:**
- A canary only works if it fails loudly when the thing it guards drifts. The current setup probes port 8081 and graceful-skips when the Engine API isn't running — meaning in CI or a fresh dev environment the test silently does nothing, which is the worst possible outcome for a regression canary: green light with zero coverage. `WebApplicationFactory<Program>` runs Engine API in-process as part of the test assembly — same DTO-conversion path, same Engine acceptance checks, no external service dependency, always executed. A fast static unit test on `ModelSchemaValidator` alone is strictly weaker: it exercises validator shape but misses the DTO-conversion boundary (where `SimModelArtifact` currently serializes directly into the POST body) and Engine's acceptance of the produced payload — exactly the drift categories the canary must catch.

**Rejected alternatives:**
- Keep external-API-with-skip — silent-skip defeats the canary's purpose.
- Fast static unit test only — misses DTO-conversion drift and Engine-acceptance drift, the two specific drift categories the canary is designed to detect.
- Run both — premature dual-maintenance; one test that covers the full acceptance path in-process is cheaper and stricter than two tests that split coverage.

**Decision:** Canary is an integration test using `WebApplicationFactory<Program>`; no external port dependency; no graceful skip.

**Consequences (input to m-E24-05):**

- Rewrite `TemplateWarningSurveyTests` to use `WebApplicationFactory<Program>`.
- Delete the port 8081 probe + skip logic.
- Hard-assert `val-err == 0` across all 12 templates at `ValidationTier.Analyse`.

## Additional Decisions

<!-- Sub-decisions ratified alongside the six open questions. Each is load-bearing for
     m-E24-02 / m-E24-03 implementation and must land as part of the decision contract. -->

### A1 — Delete `GridDto.LegacyStart` compat shim

**Scope:** `src/FlowTime.Contracts/Dtos/ModelDtos.cs` lines 42-49.

**Decision:** Delete the `LegacyStart` property during m-E24-02.

**Rationale:** The shim is precisely the "temporary compatibility shim without explicit deletion criteria" the 2026-04-23 Truth Discipline guard forbids. E-24 is the explicit deletion criterion: the unified schema uses `start` as the canonical grid start field, and no consumer should be reaching for a `legacyStart` accommodation.

### A2 — Rename `GridDto.StartTimeUtc` → `GridDto.Start`

**Scope:** `GridDto` in `FlowTime.Contracts`.

**Decision:** C# property name becomes `Start`; YAML wire name stays `start`.

**Rationale:** Canonical grid start field name is `start` across schema + templates + Sim emission. The C# side currently has `StartTimeUtc`, creating needless asymmetry between the wire name and the type name. Renaming to `Start` closes the gap; no wire change (YAML stays `start`).

### A3 — Canonical grid start field name is `start`

**Scope:** Schema, templates, Sim emission.

**Decision:** `start` is the single canonical name across all three surfaces.

**Rationale:** All three surfaces already converged on `start` except for the `StartTimeUtc` C# property (A2 closes that). This records the convergence explicitly so m-E24-02 has an unambiguous target.

### A4 — Canonical provenance field set (post-E-24)

**Scope:** Unified schema provenance block, `ProvenanceDto`, Sim emission.

**Decision:** Fields are `generator`, `generatedAt`, `templateId`, `templateVersion`, `mode`, `modelId`, `parameters`.

**Excluded:**

- `source` — collapsed into `generator` (see Q5).
- `schemaVersion` — root already carries it (see Q5).

**Rationale:** See Q5. This row names the final field list so m-E24-03 has a closed contract.

### A5 — camelCase everywhere

**Scope:** Schema declarations, DTO property JSON/YAML names, Sim emission, test helpers.

**Decision:** All keys camelCase per the project-wide rule. No snake_case anywhere in the unified schema or its emitters.

**Rationale:** Closes the systematic rule violation m-E23-01 bisected (72 validator errors across 4 shapes from snake_case provenance keys). The project rule already forbids snake_case in JSON payloads and schemas; this records that rule's application to every surface touched by E-24.

## Full-field Inventory

Row-per-field union of `SimModelArtifact` (+ satellites) ∪ `ModelDto` (+ satellites). Columns: field path (YAML wire), `SimModelArtifact` shape (Y/N + type), `ModelDto` shape (Y/N + type), current emitter, current consumer (`file:line` or "no consumer"), disposition under unification. References: `src/FlowTime.Sim.Core/Templates/SimModelArtifact.cs`, `src/FlowTime.Contracts/Dtos/ModelDtos.cs`, `src/FlowTime.Core/Models/ModelParser.cs`.

### Root-level fields

| Field path | On `SimModelArtifact` | On `ModelDto` | Emitter | Consumer | Disposition |
|------------|-----------------------|---------------|---------|----------|-------------|
| `schemaVersion` | Y (`int`, default `1`, explicit `[YamlMember]`) | Y (`int?`, nullable) | `SimModelBuilder` | `ModelParser` reads as int; schema root carries it | **keep in ModelDto** — canonical version field; Sim continues to emit `1`. Nullable stays for tolerant intake. |
| `generator` | Y (`string`, `"flowtime-sim"`) | N | `SimModelBuilder` | no consumer at root (grep: Engine never reads `model.generator`) | **remove from Sim emission** — leaked-state field; canonical `generator` lives under `provenance` (Q5). |
| `mode` | Y (`string`, e.g. `"simulation"`) | N | `SimModelBuilder` | no consumer at root (grep: Engine never reads `model.mode`) | **remove from Sim emission** — leaked-state field; `mode` lives under `provenance` (Q5). |
| `metadata` | Y (`TemplateMetadata`: id/title/description/narrative/version/tags/captureKey) | N | `SimModelBuilder` | no consumer at root (grep: Engine never reads top-level `model.metadata`) | **remove from Sim emission** — leaked-state bundle-traceability field; relevant subset survives inside `provenance.templateId` / `templateVersion`. Title/description/narrative/tags/captureKey are template-authoring surface only. |
| `window` | Y (`TemplateWindow`: start/timezone) | N | `SimModelBuilder` | read only on Sim side (`Sim.Cli/Program.cs:420` `HasWindow`) | **remove from Sim emission** — load-bearing data (`start`) already lives under `grid.start`; `timezone` has no consumer. |
| `grid` | Y (`TemplateGrid`: bins/binSize/binUnit/start) | Y (`GridDto`: Bins/BinSize/BinUnit/StartTimeUtc + LegacyStart shim) | both | Engine: `ModelParser.cs:62` reads `Grid.StartTimeUtc`; Grid struct passed through | **keep in ModelDto** — canonical; see field-level rows below. |
| `classes` | Y (`List<TemplateClass>`) | Y (`List<ClassDto>`) | both | `ModelCompiler.cs:131` consumes | **keep in ModelDto** — canonical. Empty-list emission guard lands in m-E24-04. |
| `traffic` | Y (`SimTraffic?`) | Y (`TrafficDto?`) | both | `ModelCompiler.cs:132` consumes | **keep in ModelDto** — canonical. |
| `nodes` | Y (`List<SimNode>`) | Y (`List<NodeDto>`) | both | `ModelCompiler.cs:24` consumes | **keep in ModelDto** — canonical; per-node field delta below. |
| `outputs` | Y (`List<SimOutput>`) | Y (`List<OutputDto>`) | both | `ModelCompiler.cs:134` consumes | **keep in ModelDto** — canonical; `as` becomes optional (Q3). |
| `rng` | N on `SimModelArtifact` | Y (`RngDto?`) | Engine-side only on wire; Sim has `TemplateRng` at template level but does not emit to model artifact | no Engine consumer beyond pass-through (ignored by Engine, used by Sim only) | **keep in ModelDto** — already present; documented Engine-ignored. Sim's `TemplateRng` stays template-authoring. |
| `topology` | Y (`TemplateTopology`) | Y (`TopologyDto?`) | both | `ModelCompiler.cs:19` requires `Topology?.Nodes`; `EdgeFlowMaterializer`, `InvariantAnalyzer` consume | **keep in ModelDto** — canonical, load-bearing. |
| `provenance` | Y (`SimProvenance`: 9 fields) | N | `SimModelBuilder` | `SimProvenance` read by `RunOrchestrationService.cs:627` Sim side only (no Engine consumer) | **add `ProvenanceDto` to ModelDto** — canonical field set per Q5/A4 (7 fields). |

### `grid.*` fields (`TemplateGrid` vs `GridDto`)

| Field path | On `TemplateGrid` | On `GridDto` | Consumer | Disposition |
|------------|-------------------|--------------|----------|-------------|
| `grid.bins` | Y (`int`) | Y (`int`) | `ModelParser.cs` / TimeGrid | **keep** |
| `grid.binSize` | Y (`int`) | Y (`int`) | `ModelParser.cs` / TimeGrid | **keep** |
| `grid.binUnit` | Y (`string`) | Y (`string`, default `"minutes"`) | `ModelParser.cs` / TimeGrid | **keep** |
| `grid.start` | Y (`string?`) | Y via **two** C# properties: `StartTimeUtc` + `LegacyStart` YAML-alias shim | `ModelParser.cs:62` `ParseStartTime(model.Grid.StartTimeUtc)` | **keep (A3)**; rename C# property `StartTimeUtc` → `Start` (A2); delete `LegacyStart` compat shim (A1). Wire name stays `start`. |
| `grid.timezone` | N | N | n/a | **absent** — Sim's `TemplateWindow.Timezone` dies with the `window` root. |

### `nodes[].*` fields (`SimNode` vs `NodeDto`)

| Field path | On `SimNode` | On `NodeDto` | Consumer | Disposition |
|------------|--------------|--------------|----------|-------------|
| `nodes[].id` | Y (`string`) | Y (`string`) | `ModelCompiler`, all node-consumer paths | **keep** |
| `nodes[].kind` | Y (`string`) | Y (`string`, default `"const"`) | `ModelCompiler`, `GraphService`, expression and router paths | **keep** |
| `nodes[].values` | Y (`double[]?` + `ShouldSerializeValues`) | Y (`double[]?`) | const-node evaluator | **keep** — `ShouldSerializeValues` guard pattern may need re-homing on `NodeDto` or via serializer config in m-E24-02. |
| `nodes[].expr` | Y (`string?`, `[YamlMember(Alias="expr")]`) | Y (`string?`) | expression evaluator | **keep** |
| `nodes[].source` | Y (`string?`) | **N** | Sim-side only (`Sim.Cli/Program.cs:424`); no Engine consumer | **remove from Sim emission (Q4)**; do NOT add to `NodeDto`. E-15 owns the forward contract. |
| `nodes[].pmf` | Y (`PmfSpec?`) | Y (`PmfDto?`) | PMF evaluator | **keep** |
| `nodes[].initial` | Y (`double?`) | N | `ModelParser` / stateful-buffer evaluator — **investigate in m-E24-02** (not obviously consumed via `ModelDto`) | **add to NodeDto** if consumed; if not, remove from Sim emission. Action item for m-E24-02. |
| `nodes[].inflow` | Y (`string?`) | Y (`string?`) | `serviceWithBuffer` evaluator | **keep** |
| `nodes[].outflow` | Y (`string?`) | Y (`string?`) | `serviceWithBuffer` evaluator | **keep** |
| `nodes[].loss` | Y (`string?`) | Y (`string?`) | `serviceWithBuffer` evaluator | **keep** |
| `nodes[].metadata` | Y (`Dictionary<string,string>?`) | Y (`Dictionary<string,string>?`) | `GraphService.cs:415` (`origin.kind`), `GraphService.cs:441` (`graph.hidden`), `GraphService.cs:448` (`ui.hidden`); `StateQueryService.cs:2163` (`series.origin`); `RunArtifactWriter.cs:516` preserves dict | **keep** — load-bearing; confirmed consumers. |
| `nodes[].inputs` | Y (`TemplateRouterInputs?`) | Y (`RouterInputsDto?`) | router evaluator | **keep** |
| `nodes[].routes` | Y (`List<TemplateRouterRoute>?`) | Y (`List<RouterRouteDto>?`) | router evaluator | **keep** |
| `nodes[].dispatchSchedule` | Y (`TemplateDispatchSchedule?`) | Y (`DispatchScheduleDto?`) | `serviceWithBuffer` evaluator | **keep** |

### `outputs[].*` fields (`SimOutput` vs `OutputDto`)

| Field path | On `SimOutput` | On `OutputDto` | Consumer | Disposition |
|------------|----------------|----------------|----------|-------------|
| `outputs[].series` | Y (`string`, default `"*"`) | Y (`string`, default `""`) | `ModelCompiler.cs:134`, CSV writer | **keep** — `OutputDto.Series` default becomes `"*"` for parity. |
| `outputs[].exclude` | Y (`List<string>?`) | **N** | filtering in `SimModelBuilder` output-expansion | **add to OutputDto** — lands during m-E24-02. |
| `outputs[].as` | Y (`string?`) | Y (`string`, default `"out.csv"`) | CSV file-naming | **keep, make optional (Q3)** — `OutputDto.As` becomes `string?` (no default); schema drops `as` from `outputs[].required`. |

### `traffic.arrivals[].*` fields (`SimArrival`/`SimArrivalPattern` vs `ArrivalDto`/`ArrivalPatternDto`)

| Field path | On Sim side | On ModelDto side | Consumer | Disposition |
|------------|-------------|------------------|----------|-------------|
| `traffic.arrivals[].nodeId` | Y (`string`) | Y (`string`) | arrivals materializer | **keep** |
| `traffic.arrivals[].classId` | Y (`string`, default `"*"`) | Y (`string?`, nullable) | arrivals materializer | **keep** — align defaulting during m-E24-02 (null-means-`*`). |
| `traffic.arrivals[].pattern.kind` | Y (`string`) | Y (`string`) | arrivals materializer | **keep** |
| `traffic.arrivals[].pattern.ratePerBin` | Y (`double?`) | Y (`double?`) | arrivals materializer | **keep** |
| `traffic.arrivals[].pattern.rate` | Y (`double?`) | Y (`double?`) | arrivals materializer | **keep** |

### `provenance.*` fields (`SimProvenance` vs proposed `ProvenanceDto`)

See Unified `provenance` Block table below for the post-E-24 shape. `SimProvenance` currently emits 9 fields (`source`, `generator`, `generatedAt`, `templateId`, `templateVersion`, `mode`, `modelId`, `schemaVersion`, `parameters`). Under Q5/A4 the canonical shape collapses to 7 (`source` and `schemaVersion` drop).

### Surprises surfaced during inventory

- **`nodes[].initial` asymmetry.** `SimNode` has `Initial: double?` but `NodeDto` does not. Either a live Engine consumer reads it from somewhere else or it was dropped during prior `ModelDto` conversion. Flagged as investigation item for m-E24-02 — do not re-add blindly.
- **`ShouldSerializeValues` on `SimNode`.** YAML-serializer-specific guard that suppresses `values: []` emission. The equivalent guard needs a home on the `NodeDto` side in m-E24-02 (either a sibling `ShouldSerialize*` method or serializer-level `DefaultValuesHandling` settings). Not controversial but must not be dropped.
- **`SimOutput.Exclude` is Sim-only.** Used by `SimModelBuilder` output-expansion logic; `OutputDto` has no equivalent. Needs to land on `OutputDto` in m-E24-02 or the expansion needs to run before DTO materialization.
- **`GridDto.LegacyStart` compat shim.** Two C# properties (`StartTimeUtc` + `LegacyStart`) alias the same field. A1 deletes `LegacyStart`; A2 renames `StartTimeUtc` → `Start`. Net effect: one canonical C# property (`Start`) bound to wire name `start`. No `ShouldSerializeLegacyStart() => false` trickery survives.
- **`OutputDto.As` default is `"out.csv"` (non-null)**. Current behaviour: if caller does not set `As`, YAML emits `"out.csv"`. Under Q3 this changes to `string?` with no default — emission omits the field when null. Callers that relied on the `"out.csv"` default must set it explicitly.

## Satellite-type Disposition

One row per Sim-side satellite type declared in `src/FlowTime.Sim.Core/Templates/SimModelArtifact.cs`. The enclosing file itself dies under Q2 — this table records the fate of each nested type and the Contracts-side type that absorbs its responsibility.

| Satellite type | File:line | Purpose | Contracts equivalent | Disposition | Consumers needing update |
|----------------|-----------|---------|----------------------|-------------|---------------------------|
| `SimModelArtifact` | `src/FlowTime.Sim.Core/Templates/SimModelArtifact.cs:9` | Post-substitution model root emitted by Sim; carries leaked-state fields (`window`, `generator`, top-level `metadata`, top-level `mode`) + canonical model fields. | `ModelDto` (`src/FlowTime.Contracts/Dtos/ModelDtos.cs:20`) — absorbs canonical fields; leaked-state fields drop per Q2/Q5. | **delete** | `SimModelBuilder.cs:16-20` (rewrite to build `ModelDto`); `RunOrchestrationService.cs:627/813/838/861`; `Sim.Service/Program.cs:1079`; `Sim.Cli/Program.cs:418/420/422/424`; 5 test files (`ModelGenerationTests.cs`, `TemplateArrayParameterTests.cs`, `TransitNodeTemplateTests.cs`, `EdgeLagTemplateTests.cs`, `SinkTemplateTests.cs`). |
| `SimNode` | `src/FlowTime.Sim.Core/Templates/SimModelArtifact.cs:40` | Per-node emission shape: id/kind/values/expr/source/pmf/initial/inflow/outflow/loss/metadata/inputs/routes/dispatchSchedule. | `NodeDto` (`src/FlowTime.Contracts/Dtos/ModelDtos.cs:55`) — superset minus `source` (Q4) and `initial` (investigation item for m-E24-02 per inventory surprise). | **delete**; merge into `NodeDto` with field-level additions covered in inventory (`exclude` on outputs, `initial` investigation). | Callers above + any direct `SimNode` references (grep in m-E24-02). |
| `SimOutput` | `src/FlowTime.Sim.Core/Templates/SimModelArtifact.cs:71` | Output declaration: series/exclude/as. | `OutputDto` (`src/FlowTime.Contracts/Dtos/ModelDtos.cs:108`). | **delete**; add `Exclude: List<string>?` to `OutputDto`; change `As` to `string?` per Q3. | `SimModelBuilder` output-expansion path; `RunOrchestrationService` deserializer; tests. |
| `SimTraffic` | `src/FlowTime.Sim.Core/Templates/SimModelArtifact.cs:78` | Traffic wrapper: `List<SimArrival> Arrivals`. | `TrafficDto` (`src/FlowTime.Contracts/Dtos/ModelDtos.cs:86`). | **delete**; `TrafficDto` already has the same shape. | Sim emission path; deserializer callers. |
| `SimArrival` | `src/FlowTime.Sim.Core/Templates/SimModelArtifact.cs:83` | Arrival row: nodeId/classId/pattern. | `ArrivalDto` (`src/FlowTime.Contracts/Dtos/ModelDtos.cs:91`). | **delete**; `ArrivalDto` already has equivalent shape (classId nullable vs default `"*"` — align during m-E24-02). | Sim emission path; deserializer callers. |
| `SimArrivalPattern` | `src/FlowTime.Sim.Core/Templates/SimModelArtifact.cs:90` | Arrival-pattern record: kind/ratePerBin/rate. | `ArrivalPatternDto` (`src/FlowTime.Contracts/Dtos/ModelDtos.cs:98`). | **delete**; `ArrivalPatternDto` already has the same shape. | Sim emission path; deserializer callers. |
| `SimProvenance` | `src/FlowTime.Sim.Core/Templates/SimModelArtifact.cs:100` | Provenance block: source/generator/generatedAt/templateId/templateVersion/mode/modelId/schemaVersion/parameters. 9 fields. | **new `ProvenanceDto`** to be added to `ModelDto` during m-E24-02 (shape per Q5/A4: 7 fields). | **delete**; replace with new `ProvenanceDto`. | `SimModelBuilder` provenance-construction; `RunOrchestrationService.cs:627+` provenance inspection; tests. |

### Template-side types (authoring surface) — NOT satellites of the emission artifact

The following types also live in `src/FlowTime.Sim.Core/Templates/Template.cs` but describe the **template authoring** shape, not the emitted model. They are out of scope for E-24 per the epic spec's explicit "`Template` (authoring-time) redesign" exclusion.

- `Template`, `TemplateMetadata`, `TemplateWindow`, `TemplateParameter`, `TemplateGrid`, `TemplateTopology` (+ nested), `TemplateClass`, `TemplateTraffic` (+ nested), `TemplateNode`, `TemplateDispatchSchedule`, `TemplateRouterInputs`, `TemplateRouterRoute`, `TemplateProfile`, `PmfSpec`, `TemplateOutput`, `TemplateRng`, `TemplateProvenance` — all retained as-is. Specifically: `TemplateNode.Source` stays (authoring-time field used by `TemplateValidator`'s "values or source" gate); `TemplateMetadata` stays (template metadata, not model metadata); `TemplateWindow` stays (template authoring shape).
- `PmfSpec` is referenced from `SimNode.Pmf` today. Under Q2, `NodeDto.Pmf` is typed as `PmfDto` (Contracts-side equivalent) — `PmfSpec` stays with the `Template` authoring layer; the model wire uses `PmfDto`.

## Unified `provenance` Block — Final Field List

Shape of `ProvenanceDto` to be added to `ModelDto` in m-E24-02. All keys camelCase per A5. All fields are nested under the top-level `provenance:` block (nested, not flat — Q5). Seven fields total; `source` and `schemaVersion` are explicitly **not** included (Q5 rationale).

### `ProvenanceDto` — C# definition target (for m-E24-02)

| # | C# property name | YAML wire name (camelCase) | C# type | Required / optional | Description |
|---|------------------|----------------------------|---------|---------------------|-------------|
| 1 | `Generator` | `generator` | `string` | required | Producing system identifier, e.g. `"flowtime-sim"`. Canonical producer field. Collapses Sim's prior `source`+`generator` duplicate pair. |
| 2 | `GeneratedAt` | `generatedAt` | `string` (ISO-8601 UTC) | required | Timestamp at which the model was rendered from its template. Forensic/reproducibility field. |
| 3 | `TemplateId` | `templateId` | `string` | required | Template identifier (e.g. `"transportation-basic"`). Enables template-level regeneration and lookup. |
| 4 | `TemplateVersion` | `templateVersion` | `string` | required | Version of the template at render time. Enables version-pinned regeneration. |
| 5 | `Mode` | `mode` | `string` | required | Template mode (`"simulation"` \| `"telemetry"`). Survives here because it is a model-generation input, not a model-runtime field. Absent from the root. |
| 6 | `ModelId` | `modelId` | `string` | required | Stable identifier for this rendered model. Distinguishes two runs of the same template with different parameter values. |
| 7 | `Parameters` | `parameters` | `Dictionary<string, object?>` | required (may be empty) | Template parameter values at render time. YAML-serialized as a nested map (`parameters: { name: value, … }`). Schema declaration: `additionalProperties: true` — values are forensic data, not a typed contract. |

### Fields explicitly dropped (Q5)

| Field | Reason for exclusion |
|-------|----------------------|
| `source` | Duplicates `generator`. Both emit `"flowtime-sim"`. Keep `generator` (more specific); drop `source`. |
| `schemaVersion` | Root already carries `schemaVersion`. Duplicating it inside `provenance` was one of the m-E23-01 canary rows. |

### Consumer citations (today vs post-E-24)

| Field | Current consumer (today) | Post-E-24 consumer |
|-------|--------------------------|--------------------|
| `generator`, `generatedAt`, `templateId`, `templateVersion`, `mode`, `modelId`, `parameters` | Sim-side only: `RunOrchestrationService.cs:627+` deserializes `SimProvenance` for `TelemetryManifest` construction. | Same call site post-E-24 — but reads `ProvenanceDto` off `ModelDto`. No Engine runtime consumer (provenance is forensic, not load-bearing for evaluation). |
| `source`, `schemaVersion` (dropped) | `SimProvenance.Source` written by `SimModelBuilder`, read by nobody; `SimProvenance.SchemaVersion` likewise redundant. | n/a — dropped. |

### Casing closure (A5)

m-E23-01 bisected 72 validator errors across 4 shapes stemming from snake_case provenance keys (`generated_at`, `model_id`, `template_id`, `template_version`). The A5 decision (camelCase everywhere) plus the field list above closes that systematic violation completely: every field in the post-E-24 provenance block uses camelCase at every layer (schema declaration, DTO property JSON/YAML name, Sim emission, test helpers).

## Uncommitted m-E23-01 Schema Edits — Re-examined under Unification

Three schema edits were stashed from the m-E23-01 context (patch at `/tmp/m-E23-01-input.patch`, stash `stash@{0}`). Each is re-examined below under the E-24 ratified framework (Q1–Q6 + A1–A5). Two carry forward (with their landing point moved from an m-E23-01 schema edit into m-E24-03's schema rewrite); one is reversed by Q4.

### `grid.start`

**Stashed m-E23-01 edit:** declare `grid.start` as an optional ISO-8601 string.

**Decision under unification:** **KEEP — carry forward into m-E24-03.**

**Rationale:** Load-bearing field with a confirmed Engine consumer (`ModelParser.cs:62` reads it via `ParseStartTime(model.Grid.StartTimeUtc)`; the Sim side already writes it; every existing template embeds a timestamp). This satisfies ADR-E-24-03 "Schema declares only consumed fields" — the consumer exists and has existed since before E-23. The schema declaration was correct in m-E23-01 and remains correct under E-24; only the landing point shifts from "m-E23-01 schema edit" to "incorporated into m-E24-03's schema rewrite."

**Landing point:** m-E24-03 Schema Unification. The C# rename `StartTimeUtc` → `Start` (A2) and the `LegacyStart` shim deletion (A1) happen in m-E24-02 as part of the `GridDto` cleanup.

### `nodes[].metadata`

**Stashed m-E23-01 edit:** declare `nodes[].metadata` as an optional `object` with string-typed values.

**Decision under unification:** **KEEP — carry forward into m-E24-03.**

**Rationale:** Multiple live Engine consumers — `GraphService.cs:415` reads `origin.kind` via `TryGetMetadataValue`, `GraphService.cs:441` reads `graph.hidden`, `GraphService.cs:448` reads `ui.hidden`, `StateQueryService.cs:2163` reads `series.origin`, `RunArtifactWriter.cs:516` preserves the dict on node cloning. Both `SimNode.Metadata` (`Dictionary<string,string>?`) and `NodeDto.Metadata` (`Dictionary<string,string>?`) already carry the field; `ModelCompiler.cs:67-68` also emits it (`graph.hidden`, `series.origin` for derived nodes). The schema declaration was correct in m-E23-01 and remains correct under E-24. ADR-E-24-03 satisfied — consumers exist, field is load-bearing.

**Landing point:** m-E24-03 Schema Unification.

### `nodes[].source`

**Stashed m-E23-01 edit:** declare `nodes[].source` as an optional string, documented as forward-compatibility for E-15 Telemetry Ingestion.

**Decision under unification:** **REVERSE — do not declare in the unified schema; drop from Sim emission.**

**Rationale:** This edit is directly overridden by the Q4 decision. The m-E23-01 rationale (forward-compatibility for E-15) was reasonable at the time but does not survive E-24's framework for two converging reasons:

1. **Truth Discipline guard (2026-04-23):** "do not describe a target contract in present tense unless it is live" — declaring `source` in the schema blesses a contract with no consumer today. E-15 has not landed; the Engine reader does not exist; no production code reads the field. Schema-declaring it ships a contract nobody honors.
2. **ADR-E-24-03 (ratified in m-E24-01):** "Schema declares only consumed fields." The m-E23-01 edit predated the ADR; under the ADR the declaration is incoherent.

Q4 resolves this by dropping `source` from Sim emission entirely. `Template` retains `source:` as an authoring-only field (preserves the existing `TemplateValidator` "values or source" gate); `SimModelBuilder` does NOT copy `Source` into the emitted model; `ModelDto.NodeDto` does NOT get a `Source` field; the schema does NOT declare `source`. When E-15 lands, it adds all four pieces (schema declaration, `NodeDto.Source`, Engine reader, Sim emission) coherently as a single designed contract.

**Landing point:** No schema edit in m-E24-03. Sim-emission drop happens in m-E24-02 as part of the `SimModelBuilder` rewrite (the `SimNode.Source` field dies with `SimModelArtifact`, and the builder is instructed not to propagate `TemplateNode.Source` into the new `NodeDto`).

### Summary

| Edit | m-E23-01 default | Decision under E-24 | Landing point |
|------|------------------|---------------------|---------------|
| `grid.start` | keep | **keep** | m-E24-03 schema rewrite |
| `nodes[].metadata` | keep | **keep** | m-E24-03 schema rewrite |
| `nodes[].source` | keep (forward-compat) | **reverse — do not declare; drop from Sim emission** | m-E24-02 (Sim-emission drop); no schema edit |

## Forward-only Disposition — Old Two-type YAML Shape Consumers

Forward-only per user direction: no compatibility reader survives; no `migration-on-read` code; bundles written before E-24 are discarded and regenerated from templates going forward. Table below enumerates every production or test call site that currently reads or writes the old two-type YAML shape (evidence from investigation agent `a5aa3dfe26394aff5` plus fresh grep verification).

### Production code paths

| # | File:line | Purpose | Disposition |
|---|-----------|---------|-------------|
| 1 | `src/FlowTime.TimeMachine/Orchestration/RunOrchestrationService.cs:627` | Deserializes just-emitted YAML into `SimModelArtifact` to build a `TelemetryManifest` (lines 813 `ValidateSimulationArtifact`, 838 `BuildSimulationPlanManifest`, 861 another helper). | **migrate to ModelDto (forward-compatible)** — m-E24-02 changes the deserialize target from `SimModelArtifact` to `ModelDto`; the three helpers read their data from `ModelDto` + `ProvenanceDto` instead. No compat shim. |
| 2 | `src/FlowTime.Sim.Service/Program.cs:1079` | Model-list metadata endpoint deserializes stored `model.yaml` as `SimModelArtifact`. | **migrate to ModelDto (forward-compatible)** — m-E24-02 switches deserialize target. Existing stored bundles become unreadable and must be regenerated (matches the forward-only direction). |
| 3 | `src/FlowTime.Sim.Service/Program.cs:1178` | Writes `model.yaml` into stored bundle archive (Sim Service storage path). | **emits ModelDto directly post-E-24** — no write-time compat; `SimModelBuilder` produces `ModelDto` and the service writes its serialization. |
| 4 | `src/FlowTime.Sim.Cli/Program.cs:389` | CLI writes `model.yaml` from generation output. | **emits ModelDto directly post-E-24** — same as row 3 but the CLI surface. |
| 5 | `src/FlowTime.Sim.Cli/Program.cs:418` | `DeserializeArtifact` helper deserializes YAML → `SimModelArtifact`. | **migrate to ModelDto** — helper becomes `DeserializeModel` returning `ModelDto`. |
| 6 | `src/FlowTime.Sim.Cli/Program.cs:420` | `HasWindow(artifact)` — checks `artifact.Window?.Start`. | **delete** — `window` is a leaked-state root field per Q2; removed from emission so the check becomes obsolete. |
| 7 | `src/FlowTime.Sim.Cli/Program.cs:422` | `HasTopology(artifact)` — checks `artifact.Topology?.Nodes`. | **rewrite onto ModelDto** — the check itself is valid (topology presence is real), just re-point at `ModelDto.Topology?.Nodes`. |
| 8 | `src/FlowTime.Sim.Cli/Program.cs:424` | `HasTelemetrySources(artifact)` — returns `artifact.Nodes.Any(n => !string.IsNullOrWhiteSpace(n.Source))`. | **delete or reinstate later in E-15** — with Q4 dropping `nodes[].source` from emission, every node will lack `Source`; the check becomes a tautology. Either delete the helper in m-E24-02 or mark it as an E-15 resurrection point. |
| 9 | `src/FlowTime.Sim.Core/Templates/SimModelBuilder.cs:16-20` | Builds `SimModelArtifact` from `Template` + parameters + substituted YAML. | **rewrite** — becomes `SimModelBuilder.Build(...)` returning `ModelDto` + a new `ProvenanceDto`. Provenance construction moves to a small helper. |
| 10 | `src/FlowTime.TimeMachine/TelemetryBundleBuilder.cs:102` | Reads `model/model.yaml` from run directory as part of bundle building. | **read as `ModelDto` post-E-24** — same file path, different type. Stored bundles pre-dating E-24 are regenerated. |

### Test files asserting template generation shape

From `rg -l "SimModelArtifact" tests/`:

| # | File | Purpose | Disposition |
|---|------|---------|-------------|
| 11 | `tests/FlowTime.Sim.Tests/NodeBased/ModelGenerationTests.cs:461-467` | `DeserializeArtifact(yaml)` helper + generation-shape assertions. | **migrate to `ModelDto`** — helper re-targets; shape assertions update to the unified root-level fields (no `window`, no top-level `metadata`/`mode`/`generator`). |
| 12 | `tests/FlowTime.Sim.Tests/Service/TemplateArrayParameterTests.cs:127/156/185/213` | Four sites deserializing to `SimModelArtifact` and asserting array-parameter substitution shape. | **migrate to `ModelDto`** — re-target deserializer; preserve the behavioural assertions. |
| 13 | `tests/FlowTime.Sim.Tests/Templates/TransitNodeTemplateTests.cs:52/62/65/73` | `LoadTemplateAsync` + `AssertNodeKind` + `AssertEdge` on `SimModelArtifact`. | **migrate to `ModelDto`** — helper signatures change; assertions preserved. |
| 14 | `tests/FlowTime.Sim.Tests/Templates/EdgeLagTemplateTests.cs:32` | Deserialize for edge-lag shape check. | **migrate to `ModelDto`** — re-target. |
| 15 | `tests/FlowTime.Sim.Tests/Templates/SinkTemplateTests.cs:33` | Deserialize for sink template. | **migrate to `ModelDto`** — re-target. |

### No compat reader survives

No call site listed above reads both shapes conditionally. No bundle-format migration shim is introduced. Stored bundles emitted before m-E24-02 are **discarded** (not migrated); users re-emit by re-running template generation. This is consistent with the 2026-04-23 Truth Discipline guard "When a runtime boundary changes, prefer forward-only regeneration of runs, fixtures, and approved outputs over compatibility readers that recover missing facts."

### `model.yaml` on-disk shape (forward-only)

- Pre-E-24 bundles: `model.yaml` deserializes as `SimModelArtifact`, carries root-level `window`/`generator`/`metadata`/`mode`, snake_case provenance keys, redundant `schemaVersion` inside provenance, `source:` fields on nodes.
- Post-E-24 bundles: `model.yaml` deserializes as `ModelDto`, no leaked-state root fields, camelCase `provenance`, no duplicate `schemaVersion`, no `source:` on nodes.
- No reader accepts both shapes. Attempting to open a pre-E-24 bundle with post-E-24 code fails loudly (deserialization throws). Regenerate.

## Work Log

<!-- One entry per AC (preferred) or per meaningful unit of work. Append-only. -->

### Milestone start — 2026-04-24

Housekeeping setup only. No inventory work or design decisions performed this turn — the six open questions are driven by the user one-by-one in subsequent turns per the milestone start brief.

**Setup executed:**

- Branch `milestone/m-E24-01-inventory-and-design-decisions` created from `epic/E-24-schema-alignment` at commit `94e7f9a` (the E-24 plan commit: `plan(E-24): replan around Option E — unify model type; 5 milestones`).
- Tracking doc scaffolded from `.ai/templates/tracking-doc.md`, extended with placeholders for the six open questions, the full-field inventory table, satellite-type disposition table, unified `provenance` field list, the three uncommitted m-E23-01 schema-edit re-examinations, and the forward-only disposition enumeration.
- Milestone spec frontmatter set to `status: in-progress`.
- Status surfaces reconciled:
  - Epic spec `work/epics/E-24-schema-alignment/spec.md` — milestone bullet annotated with in-progress status and branch.
  - `ROADMAP.md` E-24 section — m-E24-01 annotated.
  - `work/epics/epic-roadmap.md` E-24 entry — m-E24-01 annotated.
  - `CLAUDE.md` Current Work E-24 entry — m-E24-01 annotated.
- Stashed m-E23-01 input material (`stash@{0}`, 8 files: `CLAUDE.md`, `ROADMAP.md`, `docs/schemas/model.schema.yaml`, `work/epics/E-23-model-validation-consolidation/m-E23-01-schema-alignment-tracking.md`, `work/epics/E-23-model-validation-consolidation/m-E23-01-schema-alignment.md`, `work/epics/E-23-model-validation-consolidation/spec.md`, `work/epics/epic-roadmap.md`, `work/gaps.md`) — `git stash pop` attempted and produced merge conflicts on `CLAUDE.md`, `ROADMAP.md`, `work/epics/E-23-model-validation-consolidation/spec.md`, and `work/epics/epic-roadmap.md`. Expected: the E-24 planning commit (`94e7f9a`) rewrote the status-surface entries the stash had edited pre-E-24 pivot. Per the milestone-start brief's pop-fallback instruction, the pop was aborted cleanly (conflicts discarded, stash preserved on the stack) and the stash contents were exported as a patch file at `/tmp/m-E23-01-input.patch` (602 lines, 66 KB, 8 file hunks via `git stash show stash@{0} -p --include-untracked`). Stash `stash@{0}` remains intact and can be re-inspected at any time.
- Baseline `.NET` test run captured — see Validation section.

**Input material available for the inventory work (when the user begins driving Q1):**

- Stashed patch at `/tmp/m-E23-01-input.patch` — includes three schema edits (`grid.start`, `nodes[].metadata`, `nodes[].source`), two `work/gaps.md` entries from the m-E23-01 audit, and the 330-line m-E23-01 tracking doc with the full-shape audit output.
- Stash still in the stack as `stash@{0}` (preserved, not dropped) with subject `"m-E23-01 input material for E-24 m-E24-01 (preserve, do not discard)"`.
- Epic spec `work/epics/E-24-schema-alignment/spec.md` — Option E framing, the six open questions with default leanings, and the `SimModelArtifact` purpose-investigation findings (agent `a5aa3dfe26394aff5`).
- Survey evidence referenced from `work/epics/E-23-model-validation-consolidation/m-E23-01-schema-alignment-tracking.md` (held in stash as uncommitted content — inspect via `git stash show stash@{0} -p --include-untracked` or via the patch file).

### Design decisions landed — 2026-04-24

All six epic-spec open questions ratified in a single user-driven review pass, plus five sub-decisions that surfaced during the discussion. m-E24-02's implementation contract is now closed; the inventory tables (AC1, AC4, AC5, AC6, AC7) are the remaining work for m-E24-01.

**Decisions recorded (full text in "Design Decisions — Six Open Questions" and "Additional Decisions" sections):**

- **Q1** — Unified type home → `FlowTime.Contracts`.
- **Q2** — `ModelDto` becomes the unified wire type; `SimModelArtifact` and its satellites (`SimNode`, `SimOutput`, `SimProvenance`, `SimTraffic`, `SimArrival`, `SimArrivalPattern`) are deleted. `ModelDefinition` stays as Engine runtime type; `ModelService.ParseAndConvert` continues `YAML → ModelDto → ModelDefinition`. A new `ProvenanceDto` is added to `ModelDto` (shape per Q5).
- **Q3** — `outputs[].as` is optional; presence means "also export as CSV." `OutputDto.As` becomes `string?`; schema drops `as` from `outputs[].required`.
- **Q4** — `nodes[].source` is dropped from Sim emission and not declared on the unified schema. `Template` keeps `source:` as authoring-only; E-15 owns the forward contract (schema + DTO + Engine reader + Sim emission) end-to-end when it lands.
- **Q5** — Provenance is a nested block with a nested-map `parameters` (`additionalProperties: true`). Canonical field set: `{generator, generatedAt, templateId, templateVersion, mode, modelId, parameters}`. `source` collapses into `generator`. Duplicate `schemaVersion` drops. All keys camelCase (closes the 72-error systematic snake_case violation m-E23-01 bisected).
- **Q6** — Canary is an integration test using `WebApplicationFactory<Program>`; no external port dependency, no graceful skip; hard-asserts `val-err == 0` across all 12 templates at `ValidationTier.Analyse`.

**Sub-decisions (Additional Decisions section):**

- **A1** — Delete `GridDto.LegacyStart` compat shim during m-E24-02 (the Truth Discipline guard's explicit deletion criterion).
- **A2** — Rename `GridDto.StartTimeUtc` → `GridDto.Start`. YAML wire name stays `start`.
- **A3** — Canonical grid start field name is `start` across schema + templates + Sim emission.
- **A4** — Canonical provenance field set ratified (see Q5).
- **A5** — camelCase everywhere on the unified schema and its emitters.

**Scope confined to this pass:**

- Tracking-doc-only edits. Zero code, schema, or test changes.
- AC2 ticks (all six open questions resolved with format-compliant entries). AC1, AC3, AC4, AC5, AC6, AC7, AC8 remain open — they are m-E24-01's next pass (full-field inventory, satellite-type disposition table, unified provenance field list table, uncommitted m-E23-01 schema-edit re-examinations, forward-only disposition enumeration, final no-production-change verification).
- Baseline `.NET` test run captured at milestone start (1,701 pass / 2 pre-existing flakes / 9 skipped) remains the reference line; this pass does not re-run tests because no code changed.

### Inventory + disposition pass — 2026-04-24

Remaining ACs landed in a single doc-only pass. Tables now written; m-E24-01 is ready to close pending commit approval.

**Tables populated:**

- **Full-field inventory (AC1):** root-level fields + per-sub-type tables for `grid.*`, `nodes[].*`, `outputs[].*`, `traffic.arrivals[].*`, plus a pointer row for `provenance.*` (delegated to the Unified Provenance Block section). Each row names `SimModelArtifact`-side presence/type, `ModelDto`-side presence/type, emitter, consumer (`file:line` where one exists or "no consumer" with grep citation), and disposition. Four surprises flagged as m-E24-02 investigation items: (a) `nodes[].initial` asymmetry between `SimNode` and `NodeDto`; (b) `ShouldSerializeValues` guard rehome; (c) `SimOutput.Exclude` needs to land on `OutputDto`; (d) `OutputDto.As` default changes from `"out.csv"` to `null`.
- **Satellite disposition (AC4):** all 6 satellites + `SimModelArtifact` itself slated for deletion, each with Contracts-side absorbing type named (`SimNode` → `NodeDto`, `SimOutput` → `OutputDto`, `SimTraffic` → `TrafficDto`, `SimArrival` → `ArrivalDto`, `SimArrivalPattern` → `ArrivalPatternDto`, `SimProvenance` → new `ProvenanceDto`). Template-authoring satellites (`TemplateNode`, `TemplateMetadata`, `TemplateWindow`, `PmfSpec`, etc.) explicitly excluded per epic spec scope boundary.
- **Unified `ProvenanceDto` field list (AC5):** 7-field C# contract ready for m-E24-02 to implement, with YAML wire names, types, and required/optional flags. Dropped fields (`source`, `schemaVersion`) documented separately with reason per field. Consumer-citation table distinguishing today's `SimProvenance` reader vs post-E-24 `ProvenanceDto` reader.
- **m-E23-01 schema-edit re-examination (AC6):** three edits → 2 KEEP (`grid.start`, `nodes[].metadata`, carry into m-E24-03) + 1 REVERSE (`nodes[].source`, Q4 decision overrides). Rationale explicit on each.
- **Forward-only disposition (AC7):** 10 production call sites + 5 test files enumerated with per-site disposition. No compat reader survives; pre-E-24 bundles discarded; forward-only regeneration the only path.

**AC3 coverage note:** The four leaked-state fields (`window`, `generator`, top-level `metadata`, top-level `mode`) have their drop plans recorded as rows inside the "Full-field Inventory → Root-level fields" table rather than as a separate section. Two of them (`generator`, `mode`) relocate into the unified `provenance` block rather than dropping entirely, and the table names that new location per row. AC3 ticked accordingly.

**Final no-production-change verification (AC8):**

`git diff --stat HEAD` (run 2026-04-24 before commit):

```
.ai                                                              |   2 +-
.ai-repo/.framework-sync-sha                                     |   1 +
CLAUDE.md                                                        |   4 +-
ROADMAP.md                                                       |   6 +-
...4-01-inventory-and-design-decisions-tracking.md               | 619 +++++
.../m-E24-01-inventory-and-design-decisions.md                   |   2 +-
work/epics/E-24-schema-alignment/spec.md                         |   2 +-
work/epics/epic-roadmap.md                                       |   4 +-
```

Six files are in-scope for the m-E24-01 commit: the new tracking doc, the milestone-spec frontmatter (`status: in-progress`), the epic spec's milestone bullet annotation, `work/epics/epic-roadmap.md` E-24 entry, `ROADMAP.md` E-24 section, and `CLAUDE.md` Current Work entry. The `.ai` submodule pointer bump and `.ai-repo/.framework-sync-sha` are framework-sync artifacts from the milestone-start setup, are independent of m-E24-01's design work, and are **left unstaged** for this commit. `.claude/worktrees/` is environment-local and also left unstaged. No edits to `src/`, `tests/`, `docs/schemas/`, or any other code-bearing surface. Baseline `.NET` test run unchanged — milestone is doc-only; no re-run performed.

## Reviewer notes (optional)

<!-- Things the reviewer should specifically examine. -->

- **Inventory completeness.** The "Full-field Inventory" is scoped to the union of `SimModelArtifact` (+ satellites) ∪ `ModelDto` (+ satellites). It deliberately does **not** enumerate `ModelDefinition` runtime-side fields (e.g. `wipLimit`) because Q2 preserves `ModelDefinition` unchanged as Engine's runtime type; the DTO-to-ModelDefinition boundary is not in E-24's scope. If a reviewer expects a full `ModelDefinition` column, they should reject that expectation and confirm the wire-vs-runtime separation per Q2.
- **AC3 subsumed into AC1.** Rather than a standalone "leaked-state drop plans" section, each of the four leaked-state fields (`window`, `generator`, top-level `metadata`, top-level `mode`) is a row in the root-level inventory table with "remove from Sim emission" (or relocation into `provenance`) as its disposition. This is structurally tidier and avoids duplicating evidence. Flag if reviewer prefers a dedicated section.
- **`nodes[].initial` asymmetry.** Flagged as an m-E24-02 investigation item, not resolved here. `SimNode.Initial: double?` exists on the Sim side; `NodeDto` has no equivalent. m-E24-02 must either add it to `NodeDto` (if Engine consumes it somewhere grep missed) or confirm it is dead and drop it from Sim emission.
- **`SimOutput.Exclude` lands on `OutputDto` in m-E24-02.** Sim-only today; the unified type needs it.
- **`GridDto.LegacyStart` shim deletion criterion is explicit in A1.** The Truth Discipline guard against "temporary compatibility shims without explicit deletion criteria" is satisfied: E-24 is the criterion, m-E24-02 is the commit.
- **No Engine runtime consumer of `provenance`.** Provenance is forensic, not load-bearing for evaluation. Reviewer should verify this is the intended posture (it is per Q5; "parameters are a nested map … values are forensic data, not a contract Engine consumes").

## Validation

- **Baseline build (`dotnet build FlowTime.sln`, commit `94e7f9a`, 2026-04-24):** succeeded. One pre-existing xUnit analyzer warning in `tests/FlowTime.Core.Tests/Aggregation/ClassMetricsAggregatorTests.cs:126` (xUnit2031, unrelated to E-24 scope).
- **Baseline `dotnet test FlowTime.sln --no-build` (commit `94e7f9a`, 2026-04-24):** 1,701 passed · 2 failed (pre-existing flakes) · 9 skipped · 1,712 total across 10 test assemblies. Milestone is doc-only; any deviation from this baseline is a red flag.

| Assembly | Passed | Failed | Skipped | Total | Notes |
|----------|-------:|-------:|--------:|------:|-------|
| `FlowTime.Expressions.Tests` | 55 | 0 | 0 | 55 | |
| `FlowTime.Core.Tests` | 310 | 0 | 0 | 310 | |
| `FlowTime.Adapters.Synthetic.Tests` | 10 | 0 | 0 | 10 | |
| `FlowTime.Integration.Tests` | 78 | 1 | 0 | 79 | Flake: `SessionModelEvaluatorIntegrationTests.Dispose_TerminatesSubprocess` — subprocess-cleanup timing, not E-24 scope |
| `FlowTime.TimeMachine.Tests` | 224 | 0 | 0 | 224 | |
| `FlowTime.UI.Tests` | 265 | 0 | 0 | 265 | |
| `FlowTime.Cli.Tests` | 91 | 0 | 0 | 91 | |
| `FlowTime.Sim.Tests` | 177 | 0 | 3 | 180 | 3 skipped: one expression-library smoke + two RNG/parser examples-conformance |
| `FlowTime.Tests` | 227 | 1 | 6 | 234 | Flake: `M15PerformanceTests.Test_ExpressionType_Performance` — timing-sensitive perf assertion, not E-24 scope |
| `FlowTime.Api.Tests` | 264 | 0 | 0 | 264 | |
| **Total** | **1,701** | **2** | **9** | **1,712** | Two pre-existing flakes carried in as baseline noise |

## Deferrals

<!-- Work observed during this milestone but deliberately not done. Mirror each
     into work/gaps.md before the milestone archives. -->

- (none yet — pending)
