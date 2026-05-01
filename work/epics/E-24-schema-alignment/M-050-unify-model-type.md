---
id: M-050
title: Unify Model Type
status: done
parent: E-24
acs:
  - id: AC-1
    title: "**Unified type exists at its ratified home.** The type named in M-049 lives at its ratified namespace and name.
      A reader who opens `POST /v1/run`'s handler can follow the type reference to a single definition that represents the
      full post-substitution model."
    status: met
  - id: AC-2
    title: '**`SimModelBuilder` emits the unified type directly.** `SimModelBuilder.Build(...)` returns the unified type (or
      an immutable value carrying it). No intermediate `SimModelArtifact` instance is constructed as a bridge. The serialization
      path produces YAML matching the unified schema shape.'
    status: met
  - id: AC-3
    title: '**Engine intake parses the unified type directly.** Every YAML → runtime model path on the Engine side passes
      through the unified type before reaching `ModelDefinition`. The canonical path is `ModelService.ParseYaml(yaml) → ModelDto
      → ModelService.ConvertToModelDefinition(dto) → ModelDefinition → ModelParser.ParseModel(ModelDefinition)`. No Engine-side
      site deserializes YAML directly into `ModelDefinition` or `SimModelArtifact`. `RunOrchestrationService.cs:627` (and
      siblings `:813`, `:838`, `:861`) and any other YAML-intake call site operates on `ModelDto`.'
    status: met
  - id: AC-4
    title: "**`SimModelArtifact` is deleted.** `src/FlowTime.Sim.Core/Templates/SimModelArtifact.cs` is removed from the repo.
      `grep -rn \"SimModelArtifact\" --include='*.cs'` returns zero hits."
    status: met
  - id: AC-5
    title: "**Satellite Sim-side types are deleted.** `SimNode`, `SimOutput`, `SimProvenance`, `SimTraffic`, `SimArrival`,
      `SimArrivalPattern` are removed from the repo. Each satellite either merged into the unified type's equivalent (per
      M-049) or was deleted because it had no consumer. `grep -rn \"SimNode\\b\\|SimOutput\\b\\|SimProvenance\\b\\|SimTraffic\\\
      b\\|SimArrival\\b\\|SimArrivalPattern\\b\" --include='*.cs'` returns zero hits."
    status: met
  - id: AC-6
    title: "**Leaked-state fields dropped from emission.** Per M-049's decisions, `window`, `generator`, top-level `metadata`,
      and top-level `mode` no longer appear in emitted YAML. Whatever traceability content was meaningful has been moved into
      `provenance`."
    status: met
  - id: AC-7
    title: '**`POST /v1/run` byte-identical success.** For every template in `templates/*.yaml` with default parameters, `POST
      /v1/run` returns the same response body pre- and post-m-E24-02. The pre-/post-comparison is captured in the tracking
      doc as explicit evidence (JSON response diff on at least three representative templates: one minimal, one with PMF nodes,
      one with classes).'
    status: met
  - id: AC-8
    title: '**`POST /v1/validate` at Analyse.** The canary `TemplateWarningSurveyTests` is run pre- and post-m-E24-02. The
      non-`ParseScalar` portion of `val-err` (the four top-level leaked-state shapes, the provenance snake_case shapes, the
      outputs shapes, the empty-classes shape) drops to zero post-m-E24-02. The `ParseScalar` residual (~231 errors) remains
      until M-052 lands. The tracking doc captures the full residual histogram.'
    status: met
  - id: AC-9
    title: '**Fixtures and samples regenerated.** Every test fixture, sample bundle, and reference YAML under `tests/` and
      `docs/samples/` (or equivalent paths) is regenerated under the unified shape in this milestone. No compatibility reader
      survives. Any fixture that cannot be regenerated is deleted with a tracking-doc note explaining why.'
    status: met
  - id: AC-10
    title: "**`SimModelBuilder` tests updated in place.** Tests in `tests/FlowTime.Sim.Tests` that asserted the presence of
      `SimModelArtifact` fields (e.g. `window.start`, top-level `metadata`, provenance snake_case keys) are updated to assert
      against the unified shape or deleted if the test's only purpose was asserting drift."
    status: met
  - id: AC-11
    title: '**Engine tests updated in place.** Tests in `tests/FlowTime.Core.Tests`, `tests/FlowTime.Api.Tests`, `tests/FlowTime.TimeMachine.Tests`,
      and `tests/FlowTime.Integration.Tests` that author `ModelDefinition` instances directly are updated to author the unified
      type.'
    status: met
  - id: AC-12
    title: '**Forward-only guard.** No compatibility reader for the old two-type YAML shape exists at epic-branch tip after
      this milestone. Any legacy-shape detection code that appeared during refactor is deleted in the same change.'
    status: met
  - id: AC-13
    title: '**Full `.NET` test suite green.** `dotnet test FlowTime.sln` passes. No new regressions beyond the known validator
      residuals (tracked in AC8) which close in M-051 and M-052.'
    status: met
  - id: AC-14
    title: "**Branch coverage complete.** Every reachable branch added or modified in `SimModelBuilder`, the unified type's
      serializer hooks, and the Engine's parser is exercised by at least one test. Node-kind variants (value / expr / pmf
      / inflow / outflow), empty-collection cases (no classes, no outputs, no provenance), and optional-field absence (`grid.start`
      omitted, `nodes[].source` omitted per M-049 decision) each have coverage."
    status: met
---

## Goal

Collapse `SimModelArtifact` and `ModelDefinition` into a single unified post-substitution model type. `SimModelBuilder` emits the unified type directly; the Engine parses and consumes the unified type directly; no projection layer, no second type, no compatibility reader for the old two-type shape. `SimModelArtifact` and its satellite types (`SimNode`, `SimOutput`, `SimProvenance`, `SimTraffic`, `SimArrival`, `SimArrivalPattern`) are **deleted** in this milestone — Truth Discipline: no callers left, delete. Leaked-state fields identified in M-049 (`window`, `generator`, top-level `metadata`, top-level `mode`) are dropped from emission. Test fixtures and sample bundles are regenerated under the unified shape. This is the largest architectural change in E-24.

## Context

M-049 ratified Option E and produced the per-field decision baseline:

- The unified type's home and name are named.
- Every field has a keep / rename / drop / move-to-provenance decision.
- Every satellite Sim-side type has a disposition (merge into existing Engine-side equivalent, or delete).
- `provenance` block's final field list is named with camelCase keys.
- Forward-only disposition is confirmed for every code path that currently reads the old two-type YAML shape.

This milestone implements those decisions. It is the single milestone where code shape changes — schema (M-051), validator (M-052), and canary (M-053) follow what this milestone builds.

## Acceptance criteria

### AC-1 — **Unified type exists at its ratified home.** The type named in M-049 lives at its ratified namespace and name. A reader who opens `POST /v1/run`'s handler can follow the type reference to a single definition that represents the full post-substitution model.

### AC-2 — **`SimModelBuilder` emits the unified type directly.** `SimModelBuilder.Build(...)` returns the unified type (or an immutable value carrying it). No intermediate `SimModelArtifact` instance is constructed as a bridge. The serialization path produces YAML matching the unified schema shape.

### AC-3 — **Engine intake parses the unified type directly.** Every YAML → runtime model path on the Engine side passes through the unified type before reaching `ModelDefinition`. The canonical path is `ModelService.ParseYaml(yaml) → ModelDto → ModelService.ConvertToModelDefinition(dto) → ModelDefinition → ModelParser.ParseModel(ModelDefinition)`. No Engine-side site deserializes YAML directly into `ModelDefinition` or `SimModelArtifact`. `RunOrchestrationService.cs:627` (and siblings `:813`, `:838`, `:861`) and any other YAML-intake call site operates on `ModelDto`.

### AC-4 — **`SimModelArtifact` is deleted.** `src/FlowTime.Sim.Core/Templates/SimModelArtifact.cs` is removed from the repo. `grep -rn "SimModelArtifact" --include='*.cs'` returns zero hits.

### AC-5 — **Satellite Sim-side types are deleted.** `SimNode`, `SimOutput`, `SimProvenance`, `SimTraffic`, `SimArrival`, `SimArrivalPattern` are removed from the repo. Each satellite either merged into the unified type's equivalent (per M-049) or was deleted because it had no consumer. `grep -rn "SimNode\b\|SimOutput\b\|SimProvenance\b\|SimTraffic\b\|SimArrival\b\|SimArrivalPattern\b" --include='*.cs'` returns zero hits.

### AC-6 — **Leaked-state fields dropped from emission.** Per M-049's decisions, `window`, `generator`, top-level `metadata`, and top-level `mode` no longer appear in emitted YAML. Whatever traceability content was meaningful has been moved into `provenance`.

### AC-7 — **`POST /v1/run` byte-identical success.** For every template in `templates/*.yaml` with default parameters, `POST /v1/run` returns the same response body pre- and post-m-E24-02. The pre-/post-comparison is captured in the tracking doc as explicit evidence (JSON response diff on at least three representative templates: one minimal, one with PMF nodes, one with classes).

### AC-8 — **`POST /v1/validate` at Analyse.** The canary `TemplateWarningSurveyTests` is run pre- and post-m-E24-02. The non-`ParseScalar` portion of `val-err` (the four top-level leaked-state shapes, the provenance snake_case shapes, the outputs shapes, the empty-classes shape) drops to zero post-m-E24-02. The `ParseScalar` residual (~231 errors) remains until M-052 lands. The tracking doc captures the full residual histogram.

### AC-9 — **Fixtures and samples regenerated.** Every test fixture, sample bundle, and reference YAML under `tests/` and `docs/samples/` (or equivalent paths) is regenerated under the unified shape in this milestone. No compatibility reader survives. Any fixture that cannot be regenerated is deleted with a tracking-doc note explaining why.

### AC-10 — **`SimModelBuilder` tests updated in place.** Tests in `tests/FlowTime.Sim.Tests` that asserted the presence of `SimModelArtifact` fields (e.g. `window.start`, top-level `metadata`, provenance snake_case keys) are updated to assert against the unified shape or deleted if the test's only purpose was asserting drift.

### AC-11 — **Engine tests updated in place.** Tests in `tests/FlowTime.Core.Tests`, `tests/FlowTime.Api.Tests`, `tests/FlowTime.TimeMachine.Tests`, and `tests/FlowTime.Integration.Tests` that author `ModelDefinition` instances directly are updated to author the unified type.

### AC-12 — **Forward-only guard.** No compatibility reader for the old two-type YAML shape exists at epic-branch tip after this milestone. Any legacy-shape detection code that appeared during refactor is deleted in the same change.

### AC-13 — **Full `.NET` test suite green.** `dotnet test FlowTime.sln` passes. No new regressions beyond the known validator residuals (tracked in AC8) which close in M-051 and M-052.

### AC-14 — **Branch coverage complete.** Every reachable branch added or modified in `SimModelBuilder`, the unified type's serializer hooks, and the Engine's parser is exercised by at least one test. Node-kind variants (value / expr / pmf / inflow / outflow), empty-collection cases (no classes, no outputs, no provenance), and optional-field absence (`grid.start` omitted, `nodes[].source` omitted per M-049 decision) each have coverage.
## Constraints

- **One type, one change.** The unified type exists after this milestone. No transitional phase where both `SimModelArtifact` and the unified type coexist. The delete happens in the same change that introduces the unified type.
- **Forward-only is non-negotiable.** No compatibility reader for the old two-type YAML. No "accept either shape" branch. If a test or fixture authored the old shape, it is regenerated or deleted here.
- **Byte-identical `/v1/run` success is non-negotiable.** The diff evidence in AC7 must be zero for the representative templates. A byte-level diff indicates a defect in the unification and blocks milestone wrap.
- **Test-fixture churn is acceptable.** Tests that authored `SimModelArtifact` directly are rewritten. No compatibility reader in test helpers.
- **Emission stays declarative.** YamlDotNet serialization attributes (`[YamlMember]`, `[YamlIgnore]`) plus `DefaultValuesHandling.OmitNull | OmitEmptyCollections` configured on the `SerializerBuilder` are the mechanism for omitting leaked-state and absent fields — not custom string manipulation. The emitter's behavior is inspectable from the type definition. **Note:** YamlDotNet does not honor a `ShouldSerialize{X}()` convention — that is a Json.NET / `XmlSerializer` pattern. Per D-m-E24-02-03, all `ShouldSerialize*` shims that survive into M-050 either die with their host type (`SimNode`, `LegacyStart`) or are replaced by nullable-property + `OmitNull` semantics.
- **No Sim→Engine dependency inversion.** The unified type lives at its M-049 home. If that home is `FlowTime.Contracts`, both Sim and the Engine reference it — Sim already references `FlowTime.Contracts`, and the Engine likewise. If the home is `FlowTime.Core`, Sim adds the reference. The Engine never references Sim.

## Design Notes

- **Order of work:**
  1. Introduce the unified type at its ratified home with all fields from M-049's final list.
  2. Switch the Engine side first: make `ModelParser.ParseFromCoreModel` read the unified type. Keep `SimModelArtifact` alive at this point.
  3. Switch `SimModelBuilder.Build` to emit the unified type. Now nothing reads `SimModelArtifact`.
  4. Delete `SimModelArtifact` and its satellites. Run `grep` to confirm zero callers.
  5. Regenerate fixtures. Run the full test suite and the canary.
- **Emitter attributes.** YamlDotNet's camelCase naming convention (`CamelCaseNamingConvention.Instance`) is already the project default. `DefaultValuesHandling.OmitNull | OmitDefaults` on optional fields ensures that fields like `nodes[].source` (if M-049 decided "drop from emission when absent") do not appear in serialized output when unset. `ShouldSerialize{PropertyName}()` is available for empty-collection guards (e.g. omit `classes: []`).
- **Provenance.** Per M-049, the `provenance` block is declared as a nested type on the unified model (or as a property on a flat unified model — the shape is M-049's decision). Either way, its camelCase keys match `SimProvenance`'s existing emission and `ProvenanceMetadata`'s existing reader.
- **`nodes[].metadata`.** Stays as a load-bearing `Dictionary<string, string>` per M-049's keep decision. The unified node type exposes it as a declared property with the schema-description consumers (`GraphService`, `StateQueryService`, `RunArtifactWriter`).
- **Fixture regeneration strategy.** For each test project, enumerate fixtures that author YAML literals. For each, rewrite by hand (forward-only) against the unified shape. For fixtures that use `SimModelBuilder` to produce YAML, re-run the builder and capture the new output. For reference samples in `docs/samples/`, regenerate by running the relevant CLI command against the new builder.

## Surfaces touched

- The unified type's source file (new; location per M-049 — likely `src/FlowTime.Contracts/Models/` or extending `src/FlowTime.Core/Models/ModelDefinition.cs`)
- `src/FlowTime.Sim.Core/Templates/SimModelBuilder.cs` (rewritten to emit the unified type)
- `src/FlowTime.Sim.Core/Templates/SimModelArtifact.cs` (deleted)
- Sim satellite types: `SimNode.cs`, `SimOutput.cs`, `SimProvenance.cs`, `SimTraffic.cs`, `SimArrival.cs`, `SimArrivalPattern.cs` (deleted per M-049 disposition)
- `src/FlowTime.Core/Models/ModelParser.cs` (switched to unified type)
- `src/FlowTime.Core/Models/ModelDefinition.cs` (becomes the unified type or is replaced — per M-049)
- `src/FlowTime.Sim.Service/Program.cs:1079` (wire-serialization call site)
- `src/FlowTime.TimeMachine/Orchestration/RunOrchestrationService.cs:627` (deserialization call site)
- `src/FlowTime.Sim.Cli/Program.cs:418` (CLI call site — audit per M-049)
- `tests/FlowTime.Sim.Tests`, `tests/FlowTime.Core.Tests`, `tests/FlowTime.Api.Tests`, `tests/FlowTime.TimeMachine.Tests`, `tests/FlowTime.Integration.Tests` (fixtures and tests regenerated)
- `docs/samples/` (reference YAML regenerated if applicable)
- `work/epics/E-24-schema-alignment/m-E24-02-unify-model-type-tracking.md` (new)

## Coverage notes

Defensive-but-typesystem-unreachable branches (per `wf-tdd-cycle.md` audit
guidance) introduced or carried forward by this milestone:

- **`RunOrchestrationService.ParseYaml(...) ?? throw`** — `ModelService.ParseYaml`
  returns `Deserialize<ModelDto>(yaml)`; YamlDotNet's `Deserialize<T>` returns
  non-null for any non-empty YAML document. The `?? throw` mirrors the
  pre-step-3 defensive null guard that wrapped `Deserialize<SimModelArtifact>`
  for the same reason. Reachable only by literal `null` YAML documents,
  which the upstream `TemplateService.GenerateEngineModelAsync` does not
  produce.
- **`RunOrchestrationService.ValidateSimulationModel`'s `model.Grid is null`
  guard** — `ModelDto.Grid` is non-nullable (`public GridDto Grid { get; set; }
  = new();`); the typesystem makes this branch unreachable for a constructed
  `ModelDto` instance. Kept for structural symmetry with the previous
  `artifact.Window is null` defensive check.
- **`RunOrchestrationService.TryComputeDurationMinutes`'s `grid is null` and
  `OverflowException` catch** — same typesystem reasoning for the null path;
  the overflow path requires `Bins × BinSize > int.MaxValue` which
  `ValidateSimulationModel` rejects upstream and which no realistic model
  constructs. Kept for forward-compatibility with future high-resolution grids.

## Out of Scope

- Schema edits — M-051 owns.
- `ParseScalar` validator fix — M-052 owns.
- Canary hard-assertion promotion — M-053 owns.
- Any change to Blazor or Svelte UI.
- `Template` (authoring-time, pre-substitution) redesign.
- `ModelValidator` deletion — stays with E-23 M-048.

## Dependencies

- M-049 landed. Unified type home, name, provenance shape, field dispositions, and satellite-type dispositions are all named in M-049's tracking doc before this milestone starts.

## References

- Epic spec: `work/epics/E-24-schema-alignment/spec.md`
- M-049 tracking doc: `work/epics/E-24-schema-alignment/m-E24-01-inventory-and-design-decisions-tracking.md`
- ADR-E-24-01 (unify), ADR-E-24-02 (forward-only), ADR-E-24-03 (schema declares only consumed fields): epic spec "ADRs" section — ratified in M-049, implemented here
- Type sources:
  - `src/FlowTime.Sim.Core/Templates/SimModelArtifact.cs` (slated for deletion)
  - `src/FlowTime.Core/Models/ModelDefinition.cs` (unification target)
- Emitter: `src/FlowTime.Sim.Core/Templates/SimModelBuilder.cs`
- Parser: `src/FlowTime.Core/Models/ModelParser.cs`
- Wire site: `src/FlowTime.Sim.Service/Program.cs:1079`
- Engine intake: `src/FlowTime.TimeMachine/Orchestration/RunOrchestrationService.cs:627,813,838,861`
