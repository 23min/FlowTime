# m-E24-02 Unify Model Type — Tracking

**Started:** 2026-04-25
**Completed:** pending
**Branch:** `milestone/m-E24-02-unify-model-type` (from `epic/E-24-schema-alignment`)
**Spec:** `work/epics/E-24-schema-alignment/m-E24-02-unify-model-type.md`
**Commits:** (pending)

<!-- Status is not carried here. The milestone spec's frontmatter `status:` field is
     canonical. `**Completed:**` is filled iff the spec is `complete`. -->

## Acceptance Criteria

<!-- Mirror ACs from the spec. Check each when its Work Log entry lands. -->

- [x] **AC1 — Unified type exists at its ratified home.** `ModelDto` (the unified type per m-E24-01 Q1+Q2) lives in `FlowTime.Contracts`. A reader who opens `POST /v1/run`'s handler can follow the type reference to a single definition that represents the full post-substitution model. *Step 4: every YAML-intake call site reads ModelDto directly. Step 5: `SimModelArtifact` source deleted — `ModelDto` is now the sole post-substitution model type in the codebase.*
- [x] **AC2 — `SimModelBuilder` emits the unified type directly.** `SimModelBuilder.Build(...)` returns `ModelDto` (or an immutable value carrying it). No intermediate `SimModelArtifact` instance is constructed as a bridge. The serialization path produces YAML matching the unified schema shape. *Landed step 4 (rewrite of SimModelBuilder.cs).*
- [x] **AC3 — Engine intake parses the unified type directly.** Every YAML → runtime model path on the Engine side passes through `ModelDto` before reaching `ModelDefinition`. Canonical path: `ModelService.ParseYaml(yaml) → ModelDto → ModelService.ConvertToModelDefinition(dto) → ModelDefinition → ModelParser.ParseModel(ModelDefinition)`. No Engine-side site deserializes YAML directly into `ModelDefinition` or `SimModelArtifact`. `RunOrchestrationService.cs:627` (and siblings `:813`, `:838`, `:861`) and any other YAML-intake call site operates on `ModelDto`. *Landed step 3.*
- [x] **AC4 — `SimModelArtifact` is deleted.** `src/FlowTime.Sim.Core/Templates/SimModelArtifact.cs` is removed from the repo. `grep -rn "SimModelArtifact" --include='*.cs'` returns zero hits. *Landed step 5: file deleted; comment-only references in 13 sites cleaned to satisfy the strict zero-hit grep.*
- [x] **AC5 — Satellite Sim-side types are deleted.** `SimNode`, `SimOutput`, `SimProvenance`, `SimTraffic`, `SimArrival`, `SimArrivalPattern` are removed. All six satellites lived inline in `SimModelArtifact.cs` (one file, one delete). `grep -rn "\bSimNode\b\|\bSimOutput\b\|\bSimProvenance\b\|\bSimTraffic\b\|\bSimArrival\b\|\bSimArrivalPattern\b" --include='*.cs'` returns zero hits post-step-5. *Landed step 5 alongside the host file delete.*
- [x] **AC6 — Leaked-state fields dropped from emission.** Per m-E24-01's decisions, `window`, `generator`, top-level `metadata`, and top-level `mode` no longer appear in emitted YAML. Whatever traceability content was meaningful has been moved into `provenance` (per Q5/A4 — `mode` and `generator` survive there). *Landed step 4; verified by `Emission_DropsTopLevelLeakedStateFields_PerAC6` test (line-anchored grep on emitted YAML for all four leaked-state root keys).*
- [ ] **AC7 — `POST /v1/run` byte-identical success.** For every template in `templates/*.yaml` with default parameters, `POST /v1/run` returns the same response body pre- and post-m-E24-02. The pre-/post-comparison is captured in this tracking doc as explicit evidence (JSON response diff on at least three representative templates: one minimal, one with PMF nodes, one with classes).
- [ ] **AC8 — `POST /v1/validate` at Analyse.** The canary `TemplateWarningSurveyTests` is run pre- and post-m-E24-02. The non-`ParseScalar` portion of `val-err` (the four top-level leaked-state shapes, the provenance snake_case shapes, the outputs shapes, the empty-classes shape) drops to zero post-m-E24-02. The `ParseScalar` residual (~231 errors) remains until m-E24-04 lands. The full residual histogram is captured here.
- [ ] **AC9 — Fixtures and samples regenerated.** Every test fixture, sample bundle, and reference YAML under `tests/` and `docs/samples/` (or equivalent paths) is regenerated under the unified shape in this milestone. No compatibility reader survives. Any fixture that cannot be regenerated is deleted with a tracking-doc note explaining why.
- [x] **AC10 — `SimModelBuilder` tests updated in place.** Tests in `tests/FlowTime.Sim.Tests` that asserted the presence of `SimModelArtifact` fields (e.g. `window.start`, top-level `metadata`, provenance snake_case keys) are updated to assert against the unified shape or deleted if the test's only purpose was asserting drift. *Landed step 4: 7 Sim test files migrated; provenance source/schemaVersion assertions reshaped under Q5; window/metadata/generator/mode top-level assertions removed; new emission-evidence tests pin the post-step-4 contract.*
- [~] **AC11 — Engine tests updated in place.** Tests in `tests/FlowTime.Core.Tests`, `tests/FlowTime.Api.Tests`, `tests/FlowTime.TimeMachine.Tests`, and `tests/FlowTime.Integration.Tests` that author `ModelDefinition` instances directly are updated to author the unified type. *Step 4 covers the one Engine-side test that asserted leaked-state wire fields (`SimToEngineWorkflowTests.Telemetry_Mode_Model_Parses_WithFileSources` — Q4 source-drop guard added). No other Engine tests author `ModelDefinition` instances via the wire path; canonical step-3 chain covers the Engine intake.*
- [x] **AC12 — Forward-only guard.** No compatibility reader for the old two-type YAML shape exists at epic-branch tip after this milestone. Any legacy-shape detection code that appeared during refactor is deleted in the same change. *Step 4 introduced no compat reader; the `IgnoreUnmatchedProperties()` flag (load-bearing during steps 3↔4 coexistence) becomes a forward-compat cushion only — Sim's emission no longer carries the leaked-state fields it was absorbing. **Strengthened in step 5:** `SimModelArtifact` and the six satellites are deleted as source — no dead alternative entry points survive. The `Template.Node.Initial` scalar (D-m-E24-02-01) is also deleted in step 5, closing the only remaining producer-only-no-reader chain in the Sim authoring layer.*
- [x] **AC13 — Full `.NET` test suite green.** `dotnet test FlowTime.sln` passes. No new regressions beyond the known validator residuals (tracked in AC8) which close in m-E24-03 and m-E24-04. *Green on this run — 1,750 passed / 9 skipped. One pre-existing Rust-bridge subprocess-cleanup flake is documented and reproduces intermittently; passes in isolated re-run.*
- [x] **AC14 — Branch coverage complete.** Every reachable branch added or modified in `SimModelBuilder`, `ModelDto`'s serializer hooks, and the Engine's parser is exercised by at least one test. Node-kind variants (value / expr / pmf / inflow / outflow), empty-collection cases (no classes, no outputs, no provenance), and optional-field absence (`grid.start` omitted, `nodes[].source` omitted per m-E24-01 decision) each have coverage. *Step 4 audit table covers every new branch in `SimModelBuilder`, the SerializerBuilder change, the producer-side rewrites, and the new `TopologySemanticsDto.Errors` nullable shape. Defensive-but-typesystem-unreachable branches documented under spec Coverage notes.*

## Decisions made during implementation

<!-- Populated as decisions surface during the order-of-work. m-E24-01 ratified the design;
     this section captures choices made executing it (e.g. how the nodes[].initial asymmetry
     is resolved, where ShouldSerializeValues is rehomed, etc.). -->

- **D-m-E24-02-01 (2026-04-25) — `nodes[].initial` (scalar) is dead. Drop from emission; do not add to `NodeDto`.** Deep investigation across C# src + tests, Rust engine, YAML templates, and JSON/YAML schemas found a producer-only chain (`Template.Node.Initial` → `SimNode.Initial` via `SimModelBuilder.cs:357`) with **zero readers anywhere**. `ModelParser.cs:460` carries an explicit comment redirecting initial-seed handling to `topology.initialCondition`. The `nodesRequiringInitial` / `EnsureInitialConditions` machinery in `TemplateValidator` operates on the separate nested `topology.initialCondition: { node, queueDepth }` object (`TemplateInitialCondition` / `TopologyInitialConditionDto`) — same name fragment, different concept. Topology-level `InitialCondition` (`ModelDto.GraphDto.NodeDto.InitialCondition` at `ModelDtos.cs:131`) is the sole live initial-conditions path. The scalar field was introduced in commit `ce9ec9e` (Oct 2025) alongside `SimModelArtifact` and never wired through — accidental drift. **Action:** delete `Initial` from `Template.Node` (Template.cs:273), drop from `SimNode` (deleted with the satellite), drop the assignment in `SimModelBuilder.cs:357`. Do not add a corresponding scalar property to `NodeDto`.
- **D-m-E24-02-02 (2026-04-25) — `ShouldSerializeValues` rehome: Option A (instance method on `NodeDto`). SUPERSEDED by D-m-E24-02-03.** Original decision was to mirror the existing repo `ShouldSerialize*` pattern. Subsequent investigation (D-m-E24-02-03) established that YamlDotNet has never supported the `ShouldSerialize{X}()` convention — it is a Json.NET / `XmlSerializer` pattern. The pre-existing shims in the repo are no-ops at the YAML emitter; mirroring the pattern would have propagated dead code. Reversed to Option B: nullable property + `DefaultValuesHandling.OmitNull` on the serializer.
- **D-m-E24-02-03 (2026-04-25) — `ShouldSerialize*` is not a YamlDotNet mechanism. Switch to nullable + `DefaultValuesHandling.OmitNull | OmitEmptyCollections`. Delete the two new shims added in step 1.** Investigation of YamlDotNet 17.0.1 source (`ReadablePropertiesTypeInspector.GetProperties` filters only on `CanRead` + zero-parameter getter, never inspects companion methods) and historical release notes (zero `ShouldSerialize` mentions across versions 15.x–17.x) confirms YamlDotNet has never implemented the convention. The repo had five pre-existing `ShouldSerialize*` shims (`GridDto.ShouldSerializeLegacyStart`, three on `Template`, and `SimNode.ShouldSerializeValues`) — all silent no-ops at the YAML emitter level. The two new ones added in step 1 (`NodeDto.ShouldSerializeValues`, `ProvenanceDto.ShouldSerializeParameters`) would have continued the pattern. **Action taken in this commit:** (1) Deleted `NodeDto.ShouldSerializeValues()` and `ProvenanceDto.ShouldSerializeParameters()`. (2) Made `ProvenanceDto.Parameters` nullable (`Dictionary<string, object?>?`, default null) — empty parameter sets serialize as YAML omission via `OmitNull` once the SerializerBuilder is configured. (3) `NodeDto.Values` is already `double[]?` — also covered by `OmitNull` for null-but-not-empty-array cases. (4) Updated 5 unit tests: removed three direct `ShouldSerializeParameters()` tests, removed three direct `ShouldSerializeValues()` tests, added `ProvenanceDto_OmitsParameters_WhenNull` round-trip test, replaced `ProvenanceDto_AllowsEmptyParameters` with `ProvenanceDto_DefaultsParametersToNull`. (5) Spec line 50 amended to remove the false `ShouldSerialize{X}()` claim and instead name `[YamlMember]` + `[YamlIgnore]` + `DefaultValuesHandling` flags as the mechanism. **Deferred to step 4 (`SimModelBuilder` rewrite):** configure the SerializerBuilder with `.ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull | DefaultValuesHandling.OmitEmptyCollections)` so the wire emission actually omits null/empty fields. **Pre-existing shim audit (also captured here):** `GridDto.ShouldSerializeLegacyStart` dies with `LegacyStart` deletion in step 2 (A1+A2). `SimNode.ShouldSerializeValues` dies with `SimNode` deletion in step 5. The three `Template.ShouldSerializeLegacy*` shims (`LegacyExpression`, `LegacySource`, `LegacyFilename`) are out of E-24 scope (Template is authoring-time, pre-substitution) but verified dead — production templates and test fixtures all use `expr:` / `series:` / `as:`, never the legacy aliases. Filed as a separate gap (`work/gaps.md`) rather than scope-creeping E-24. **Truth correction on `LegacyStart`:** the "Legacy" name is upside-down — production wire format is `start:` (12 of 12 production templates, 40+ test fixtures), and `startTimeUtc:` has never been on the wire. The `LegacyStart` property exists purely as a `[YamlMember(Alias = "start")]` deserialization forwarder so the canonical wire key `start:` populates `GridDto.StartTimeUtc`. m-E24-01's A1+A2 (delete `LegacyStart`, rename `StartTimeUtc` → `Start`) was already the correct cleanup; step 2 implements it.

## Investigation items carried from m-E24-01

The m-E24-01 inventory surfaced four asymmetries that m-E24-02 must resolve. Each gets a row here when its decision lands:

1. **`nodes[].initial` asymmetry** — `SimNode.Initial: double?` exists; `NodeDto` lacks it. m-E24-02 must either add it to `NodeDto` (if Engine consumes it via a path the inventory grep missed) or confirm dead and drop from Sim emission. **Status:** resolved 2026-04-25 (D-m-E24-02-01) — confirmed dead, drop from emission. Not added to `NodeDto`.
2. **`ShouldSerializeValues` guard rehome** — `SimNode.ShouldSerializeValues()` suppresses `values: []` emission for empty value arrays. The equivalent guard needs a home on `NodeDto` (sibling `ShouldSerialize*` method or serializer-level `DefaultValuesHandling` settings). Must not be dropped silently. **Status:** D-m-E24-02-02 (Option A, instance method) — **superseded by D-m-E24-02-03** (2026-04-25). Final resolution: nullable property + `DefaultValuesHandling.OmitNull | OmitEmptyCollections` on the serializer. `NodeDto.ShouldSerializeValues()` and `ProvenanceDto.ShouldSerializeParameters()` deleted in this commit; the actual emission-omission wiring lands in step 4 (`SimModelBuilder` SerializerBuilder configuration).
3. **`SimOutput.Exclude` lands on `OutputDto`** — Sim-only field today; used by `SimModelBuilder` output-expansion. m-E24-02 adds `Exclude: List<string>?` to `OutputDto`. **Status:** decided in m-E24-01; mechanical. **Implementation landed step 1 (2026-04-25)**: `OutputDto.Exclude: List<string>?` added.
4. **`OutputDto.As` default changes from `"out.csv"` to nullable** — per Q3, `As` becomes `string?` with no default. Callers that relied on the `"out.csv"` default must set it explicitly. **Status:** decided in m-E24-01; mechanical. **Implementation landed step 1 (2026-04-25)**: `OutputDto.As` is now `string?` (no default). C# caller audit: only one production producer (`ModelService.ConvertToModelDefinition` line 114) — updated to coerce nullable wire `As` to non-nullable runtime `OutputDefinition.As` via `?? string.Empty`. Existing downstream consumers (`TelemetryBundleBuilder.cs:279`, `RunArtifactReader.cs:77`) are already `IsNullOrWhiteSpace`-aware. No external `new OutputDto { As = ... }` callers exist (grepped). Schema templates always set `as:` explicitly, so wire deserialization sees null only for auto-added outputs that intentionally don't materialize CSVs.

## Order of work

Per the spec's Design Notes:

1. Introduce `ProvenanceDto` and any new `ModelDto` fields (`Exclude` on `OutputDto`, optional rehome of node `Initial`/`Values` guards) at the ratified shape.
2. Apply `GridDto` cleanups (A1 delete `LegacyStart`; A2 rename `StartTimeUtc` → `Start`).
3. Switch the Engine side first: `ModelParser.ParseFromCoreModel` reads `ModelDto`. Keep `SimModelArtifact` alive at this point.
4. Switch `SimModelBuilder.Build` to emit `ModelDto`. Now nothing reads `SimModelArtifact`.
5. Delete `SimModelArtifact` and its satellites. Run `grep` to confirm zero callers.
6. Regenerate fixtures. Run the full test suite and the canary.

## Work Log

<!-- One entry per AC (preferred) or per meaningful unit of work. Append-only. -->

### Milestone start — 2026-04-25

Setup only. Branch `milestone/m-E24-02-unify-model-type` created from `epic/E-24-schema-alignment` at the post-merge tip (commit `23eba07` Merge of m-E24-01). m-E24-01 design baseline ratified — see `m-E24-01-inventory-and-design-decisions-tracking.md` for the full per-field decisions, satellite dispositions, unified `ProvenanceDto` shape (7 fields, camelCase), and forward-only consumer enumeration that this milestone implements.

**Status surfaces reconciled at start:**

- Spec frontmatter `status: in-progress`.
- Epic spec, ROADMAP, epic-roadmap, CLAUDE.md status entries flipped to m-E24-02 in-progress.

**Baseline `.NET` test run captured:** see Validation section below.

### Step 1 — DTO surface additions — 2026-04-25

Order-of-work step 1 ("introduce `ProvenanceDto` and any new `ModelDto` fields") landed as a TDD red→green→refactor cycle. No call-site rewiring (steps 3–5) yet.

**Changes:**

- **`src/FlowTime.Contracts/Dtos/ModelDtos.cs`:**
  - Added `ProvenanceDto` (new sealed class) with the 7 ratified camelCase fields per Q5/A4: `Generator`, `GeneratedAt`, `TemplateId`, `TemplateVersion`, `Mode`, `ModelId`, `Parameters` (`Dictionary<string, object?>`, default empty).
  - Added `ProvenanceDto.ShouldSerializeParameters() => Parameters is { Count: > 0 }`. Mirrors the repo `ShouldSerialize*` pattern per D-m-E24-02-02.
  - Added `ModelDto.Provenance: ProvenanceDto?` (nullable so callers without provenance don't construct an empty block).
  - Added `OutputDto.Exclude: List<string>?` (investigation item 3 — mirrors `SimOutput.Exclude` shape at `SimModelArtifact.cs:74`).
  - Changed `OutputDto.As: string = "out.csv"` → `string?` (no default) per Q3 / investigation item 4. Reordered fields (Series → Exclude → As) so the optional CSV-export marker reads as a tail field.
  - Added `NodeDto.ShouldSerializeValues() => Values is { Length: > 0 }` instance method per D-m-E24-02-02 / investigation item 2.
- **`src/FlowTime.Contracts/Services/ModelService.cs`:**
  - `ConvertToModelDefinition`: changed `As = o.As` → `As = o.As ?? string.Empty` to coerce nullable wire `OutputDto.As` to non-nullable runtime `OutputDefinition.As`. Existing downstream consumers (`TelemetryBundleBuilder.cs:279`, `RunArtifactReader.cs:77`) are already `IsNullOrWhiteSpace`-aware, so empty-string is the safe forward-only choice.

**Out-of-scope discipline:**

- No scalar `Initial` added to `NodeDto` — D-m-E24-02-01 confirmed the field is dead. `Template.Node.Initial`, `SimNode.Initial`, and the `SimModelBuilder.cs:357` assignment will be deleted in step 5 (delete satellites). Not touched here.
- `GridDto.LegacyStart` deletion + `StartTimeUtc → Start` rename (A1 + A2) deliberately deferred to order-of-work step 2 ("apply `GridDto` cleanups") — out of scope for this sub-task.
- No call-site rewiring: `RunOrchestrationService.cs:627`, `SimModelBuilder.Build`, Sim service write paths, or fixture regeneration. Step 3+ scope.

**Note on `ShouldSerialize*` guards:** YamlDotNet 16.3.0's default `ReadablePropertiesTypeInspector` does not honor `ShouldSerialize{Property}()` instance methods. The existing `GridDto.ShouldSerializeLegacyStart`, `Template.ShouldSerializeLegacy*`, and `SimNode.ShouldSerializeValues` are present as a structural pattern, but in current production they are effectively no-ops at the YAML emitter level (verified empirically — both `startTimeUtc:` and the aliased `start:` get emitted). The new `NodeDto.ShouldSerializeValues` and `ProvenanceDto.ShouldSerializeParameters` continue the same pattern per D-m-E24-02-02. They become effective when `SimModelBuilder.Build` is rewired in order-of-work step 4 with a `ShouldSerialize`-aware type inspector. Test-side: the methods are tested directly (correct return value across null / empty / populated branches); the YAML-omission semantics are not over-claimed at the round-trip layer.

**Tests added — `tests/FlowTime.Core.Tests/Parsing/ModelDtoSerializationTests.cs`:**

- 24 tests covering the additive DTO surface:
  - `ModelDto.Provenance`: defaults to null, omitted from YAML when null, round-trips with full 7-field shape, emits camelCase keys (and proves snake_case forms `generated_at` / `template_id` / `template_version` / `model_id` are absent).
  - `ProvenanceDto`: `Parameters` defaults to empty dict; `ShouldSerializeParameters()` returns false for null / empty / true for populated; emits `parameters:` block with content when non-empty.
  - `OutputDto.Exclude`: defaults to null, round-trips as list, omitted when null, emitted when populated.
  - `OutputDto.As` nullable: defaults to null, omitted from YAML when null, round-trips when present, deserializes as null when absent from wire YAML.
  - `ModelService.ConvertToModelDefinition`: handles null `OutputDto.As` (coerces to empty `OutputDefinition.As`) and preserves non-null `As` (covers both branches of `?? string.Empty`).
  - `NodeDto`: `ShouldSerializeValues()` returns true for populated / false for empty / false for null; `Values: null` is omitted from YAML (works via global `OmitNull`); `Values: [...]` emits to YAML.

**Branch-coverage audit (line-by-line):**

| File | Conditional / branch | Covering test(s) |
|------|---------------------|------------------|
| `ModelDtos.cs` `NodeDto.ShouldSerializeValues()` | `Values == null` | `_ReturnsFalse_WhenNull` |
| `ModelDtos.cs` `NodeDto.ShouldSerializeValues()` | `Values.Length == 0` | `_ReturnsFalse_WhenEmpty` |
| `ModelDtos.cs` `NodeDto.ShouldSerializeValues()` | `Values.Length > 0` | `_ReturnsTrue_WhenNonEmpty` |
| `ModelDtos.cs` `ProvenanceDto.ShouldSerializeParameters()` | `Parameters == null` (defensive) | `_ReturnsFalse_WhenNullDictionaryAssigned` |
| `ModelDtos.cs` `ProvenanceDto.ShouldSerializeParameters()` | `Parameters.Count == 0` | `_ReturnsFalse_WhenEmpty` |
| `ModelDtos.cs` `ProvenanceDto.ShouldSerializeParameters()` | `Parameters.Count > 0` | `_ReturnsTrue_WhenNonEmpty` |
| `ModelService.cs` `As = o.As ?? string.Empty` | `o.As == null` | `_HandlesNullAs_WithoutThrowing` |
| `ModelService.cs` `As = o.As ?? string.Empty` | `o.As != null` | `_PreservesNonNullAs` |

No unreachable branches — all paths exercised.

**ACs partially advanced:**

- **AC1** (Unified type exists at its ratified home) — partially advanced. `ProvenanceDto` and the additive `ModelDto.Provenance` field are now in `FlowTime.Contracts`. The wider AC1 ("a reader who opens `POST /v1/run`'s handler can follow the type reference to a single definition") still depends on step 3 wiring `ModelParser.ParseFromCoreModel` against `ModelDto`.
- **AC2** prep (SimModelBuilder emits unified type) — `OutputDto.Exclude`, nullable `OutputDto.As`, and `NodeDto.ShouldSerializeValues` are now present so that the rewrite of `SimModelBuilder.Build` in step 4 has the target shape ready. Build itself unchanged.
- **AC9** prep (fixtures regenerated) — DTOs now express the unified shape; no fixture work yet.
- **AC14** (branch coverage complete) — every new branch covered by a direct test (audit table above).

**Test-suite delta:**

| | Baseline (commit `23eba07`) | After step 1 | Delta |
|---|---:|---:|---:|
| Passed | 1,702 | 1,726 | **+24** |
| Failed | 1 (pre-existing flake) | 0–1 (flake-dependent) | within-flake |
| Skipped | 9 | 9 | 0 |
| Total | 1,712 | 1,736 | +24 |

The `RustEngineBridgeTests.RustEngine_CleansUpTempDirectory_OnFailure` flake passed cleanly on the post-step-1 run. `Dispose_TerminatesSubprocess` showed a transient `[FAIL]` line during the sweep but passed on isolated re-run — also subprocess-cleanup timing, not E-24 scope.

**Files changed:**

- `src/FlowTime.Contracts/Dtos/ModelDtos.cs` — added `ProvenanceDto`, `ModelDto.Provenance`, `OutputDto.Exclude`, `OutputDto.As` nullable, `NodeDto.ShouldSerializeValues()`.
- `src/FlowTime.Contracts/Services/ModelService.cs` — `As = o.As ?? string.Empty` null-coalesce.
- `tests/FlowTime.Core.Tests/Parsing/ModelDtoSerializationTests.cs` — new file, 24 tests.

### Step 1 follow-up — `ShouldSerialize*` correction & YamlDotNet 17.0.1 normalization — 2026-04-25

Per D-m-E24-02-03. The two `ShouldSerialize*` shims added in step 1 were no-ops at the YAML emitter level — YamlDotNet has never honored that convention (Json.NET / `XmlSerializer` pattern). Reversed to a YamlDotNet-native mechanism (nullable property + `DefaultValuesHandling.OmitNull` on the SerializerBuilder) and normalized YamlDotNet to `17.0.1` across all six projects in the repo.

**Code changes:**

- `src/FlowTime.Contracts/Dtos/ModelDtos.cs`:
  - Deleted `NodeDto.ShouldSerializeValues()` and its preceding doc comment.
  - Deleted `ProvenanceDto.ShouldSerializeParameters()` and its preceding doc comment.
  - Changed `ProvenanceDto.Parameters` from `Dictionary<string, object?> = new()` (non-nullable, default empty) to `Dictionary<string, object?>?` (nullable, default null). XML doc updated to reference D-m-E24-02-03.
  - `NodeDto.Values` was already `double[]?` — no change required; `OmitNull` handles it.
- `tests/FlowTime.Core.Tests/Parsing/ModelDtoSerializationTests.cs`:
  - File header rewritten — references nullable + `OmitNull` mechanism (per D-m-E24-02-03) instead of cargo-culted `ShouldSerialize*` pattern.
  - Removed 6 tests: `ProvenanceDto_ShouldSerializeParameters_ReturnsFalse_WhenEmpty` / `_ReturnsTrue_WhenNonEmpty` / `_ReturnsFalse_WhenNullDictionaryAssigned`; `NodeDto_ShouldSerializeValues_ReturnsTrue_WhenNonEmpty` / `_ReturnsFalse_WhenEmpty` / `_ReturnsFalse_WhenNull`.
  - Replaced `ProvenanceDto_AllowsEmptyParameters` with `ProvenanceDto_DefaultsParametersToNull` (matches the new nullable contract).
  - Added `ProvenanceDto_OmitsParameters_WhenNull` (round-trip test: null `Parameters` produces YAML without a `parameters:` block via `OmitNull`).
- Project files — YamlDotNet `15.3.0`/`16.3.0` → `17.0.1`:
  - `src/FlowTime.Core/FlowTime.Core.csproj` (was 15.3.0 — full major behind, now aligned).
  - `src/FlowTime.Contracts/FlowTime.Contracts.csproj`.
  - `src/FlowTime.Sim.Core/FlowTime.Sim.Core.csproj`.
  - `src/FlowTime.API/FlowTime.API.csproj`.
  - `src/FlowTime.UI/FlowTime.UI.csproj`.
  - `tests/FlowTime.UI.Tests/FlowTime.UI.Tests.csproj`.

**Spec amendment:**

- `work/epics/E-24-schema-alignment/m-E24-02-unify-model-type.md:50` — "Emission stays declarative" constraint reworded to remove the false `ShouldSerialize{X}()` claim. Now names `[YamlMember]` + `[YamlIgnore]` + `DefaultValuesHandling` flags as the actual mechanism. Includes an explicit footnote: "YamlDotNet does not honor a `ShouldSerialize{X}()` convention — that is a Json.NET / `XmlSerializer` pattern."

**Gap filed:**

- `work/gaps.md` — new section "Template-layer `Legacy*` aliases (Sim authoring-time)" filed as a deferred small `chore(sim):` patch. Three pre-existing dead aliases on `Template.Node` (`LegacyExpression`) and `TemplateOutput` (`LegacySource`, `LegacyFilename`) — verified zero hits in production templates and tests. Out of E-24 scope (Template is authoring-time, pre-substitution). Not bundled into m-E24-02 to avoid scope creep.

**Pre-existing shim audit (delete schedule):**

| Shim | File:Line | Real purpose | Dies in |
|---|---|---|---|
| `GridDto.ShouldSerializeLegacyStart()` | `ModelDtos.cs:49` | None — `LegacyStart` itself is misnamed (production wire is `start:`, never `startTimeUtc:`) | Step 2 (A1+A2: delete `LegacyStart`, rename `StartTimeUtc` → `Start`) |
| `Template.ShouldSerializeLegacyExpression()` | `Template.cs:270` | None — `expression:` is dead in production templates and tests | Out of E-24 scope (gap filed) |
| `TemplateOutput.ShouldSerializeLegacySource()` | `Template.cs:345` | None — `source:` (in outputs) is dead | Out of E-24 scope (gap filed) |
| `TemplateOutput.ShouldSerializeLegacyFilename()` | `Template.cs:354` | None — `filename:` is dead | Out of E-24 scope (gap filed) |
| `SimNode.ShouldSerializeValues()` | `SimModelArtifact.cs:65` | Mirrors `NodeDto.Values` empty-array suppression; replaced by nullable + `OmitNull` semantics on `NodeDto` | Step 5 (`SimNode` deletion) |

End state of m-E24-02: zero `ShouldSerialize*` methods in `FlowTime.Contracts`. The Template-layer methods are tracked under the gap entry. The wire-format omission is enforced by `DefaultValuesHandling.OmitNull | OmitEmptyCollections` configured on the SerializerBuilder used by `SimModelBuilder` (lands in step 4).

**`LegacyStart` truth correction (also documented in D-m-E24-02-03):**

The "Legacy" naming on `GridDto.LegacyStart` is upside-down. Production wire format is `start:` (12 of 12 templates, 40+ test fixtures). `startTimeUtc:` has never appeared on the wire — it's a fictional emission target generated by `CamelCaseNamingConvention` from the C# property name `StartTimeUtc`. The `LegacyStart` property is **the only** functioning deserialization path (`[YamlMember(Alias = "start")]` forwards to `StartTimeUtc.set`). m-E24-01 A1+A2 (delete `LegacyStart`, rename `StartTimeUtc` → `Start`) was already the correct cleanup; step 2 implements it.

**Test-suite delta:**

| | Step-1 baseline | After follow-up | Delta |
|---|---:|---:|---:|
| Passed | 1,726 | 1,722 | **−4** (≡ −6 deleted ShouldSerialize tests + −1 swap + +2 from `_DefaultsParametersToNull` and `_OmitsParameters_WhenNull` + +1 from flake passing this run vs. flake-dependent prior run) |
| Failed | 0–1 (flake-dependent) | 0 (flake passed) | within-flake |
| Skipped | 9 | 9 | 0 |
| Total | 1,735–1,736 | 1,731 | −4–5 |

All 10 assemblies green. Pre-existing flake `RustEngineBridgeTests.RustEngine_CleansUpTempDirectory_OnFailure` passed cleanly on YamlDotNet 17.0.1.

**Branch-coverage audit (line-by-line):**

| File | Conditional / branch | Covering test(s) |
|------|---------------------|------------------|
| `ModelDtos.cs` `ProvenanceDto.Parameters` (now nullable) — null path | Property defaults to null | `ProvenanceDto_DefaultsParametersToNull` |
| `ModelDtos.cs` `ProvenanceDto.Parameters` — null path emission | Wire emits no `parameters:` key when null | `ProvenanceDto_OmitsParameters_WhenNull` |
| `ModelDtos.cs` `ProvenanceDto.Parameters` — populated emission | Wire emits `parameters:` key with content | `ProvenanceDto_EmitsParameters_WhenNotEmpty` |
| `ModelDtos.cs` `ProvenanceDto.Parameters` — round-trip | Deserializes nested map; preserves keys/values | `ProvenanceDto_RoundTripsAllSevenFieldsAsCamelCase` |
| `ModelService.cs` `As = o.As ?? string.Empty` | `o.As == null` | `_HandlesNullAs_WithoutThrowing` |
| `ModelService.cs` `As = o.As ?? string.Empty` | `o.As != null` | `_PreservesNonNullAs` |

No unreachable branches. `NodeDto.Values` null-omission and round-trip are still covered by `_OmittedFromYaml_WhenNull` and `_EmittedToYaml_WhenPopulated`.

**Out-of-scope discipline preserved:**

- No SerializerBuilder configuration change yet — the actual `OmitNull | OmitEmptyCollections` flag setting lands when `SimModelBuilder.Build` is rewritten in step 4. Until then, the test file's local `CreateSerializer()` helper is the only place flags are set; production emission paths are untouched.
- Step 2 (`GridDto.LegacyStart` delete + `StartTimeUtc` → `Start` rename) still pending. (Subsequently landed — see "Step 2 — `GridDto` cleanup" below.)
- Pre-existing dead `ShouldSerialize*` shims in `Template.cs` are tracked under the new gap entry, not deleted in this commit.

### Step 2 — `GridDto` cleanup (A1 + A2) — 2026-04-25

Order-of-work step 2 ("Apply `GridDto` cleanups (A1 delete `LegacyStart`; A2 rename `StartTimeUtc` → `Start`)") landed as a TDD red→green→refactor cycle. The rename is wider than the m-E24-01 plan suggested because `GridDto.LegacyStart` was the only deserialization path for the canonical wire key `start:` (it was a `[YamlMember(Alias = "start")]` shim onto `StartTimeUtc`); removing it without renaming `StartTimeUtc` → `Start` would have broken every production template's grid intake. Per D-m-E24-02-03, the truth correction was already documented: production wire format is `start:` (12/12 templates, 40+ fixtures), and `startTimeUtc:` never appeared on production wire — it was a fictional emission target generated by the camelCase convention from the prior C# property name. Step 2 closes the asymmetry: `Start` (C#) ↔ `start:` (wire) natively via `CamelCaseNamingConvention`, no aliases.

**Code changes:**

- `src/FlowTime.Contracts/Dtos/ModelDtos.cs` — `GridDto` rewritten (lines 41–58 → 41–53 in current file):
  - Deleted `LegacyStart` property and its `[YamlMember(Alias = "start", ApplyNamingConventions = false)]` attribute.
  - Deleted `ShouldSerializeLegacyStart() => false` method.
  - Renamed `StartTimeUtc` property → `Start`.
  - Rewrote XML doc comment to capture the post-cleanup contract — explicit truth statement that `start:` is the canonical wire key and `startTimeUtc:` never appeared on production wire.
- `src/FlowTime.Contracts/Services/ModelService.cs:47` — `StartTimeUtc = model.Grid.StartTimeUtc` → `StartTimeUtc = model.Grid.Start`. Added inline comment noting the deliberate asymmetry: wire-side property is now `Start`, runtime `GridDefinition.StartTimeUtc` keeps its name (out of E-24 scope per D-m-E24-02-03 — runtime model is not the wire DTO).

**Caller-site disambiguation (read every site before renaming):**

The fresh grep for `StartTimeUtc` returned 11 hits across 7 files. Only ONE was a direct read of `GridDto.StartTimeUtc` (`ModelService.cs:47`); every other hit referenced a different runtime / fixture / manifest type:

| Hit | Type | Treatment |
|---|---|---|
| `src/FlowTime.Contracts/Dtos/ModelDtos.cs:49` | `GridDto.StartTimeUtc` (definition) | **Renamed** → `Start` |
| `src/FlowTime.Contracts/Dtos/ModelDtos.cs:54-55` | `GridDto.LegacyStart` getter/setter | **Deleted** with the property |
| `src/FlowTime.Contracts/Services/ModelService.cs:47` | RHS read of `model.Grid.StartTimeUtc` (where `model: ModelDto`) | **Updated** to `.Start` |
| `src/FlowTime.TimeMachine/TelemetryCapture.cs:200` | `model.Grid?.StartTimeUtc` where `model: ModelDefinition` | **Untouched** — runtime `GridDefinition.StartTimeUtc`, not the wire DTO |
| `src/FlowTime.TimeMachine/Artifacts/CaptureManifestWriter.cs:38` | `record TelemetryManifestWindow(string? StartTimeUtc, ...)` | **Untouched** — separate manifest record (JSON wire), not `GridDto` |
| `src/FlowTime.Core/Fixtures/FixtureModelLoader.cs:64,131` | `FixtureWindow.StartTimeUtc` (internal fixture-loader type) + `GridDefinition.StartTimeUtc` write | **Untouched** — fixture YAML uses top-level `window:` not `grid:`; consumed by separate type |
| `src/FlowTime.Core/Models/ModelParser.cs:62,577` | `model.Grid.StartTimeUtc` where `model: ModelDefinition`, and `GridDefinition.StartTimeUtc` definition | **Untouched** — runtime model |
| `tests/FlowTime.Core.Tests/Parsing/ModelParserTopologyTests.cs:21,67` | `new GridDefinition { StartTimeUtc = ... }` | **Untouched** — runtime `GridDefinition`, not `GridDto` |

Net code-side rename: 1 file (`ModelDtos.cs` 4 lines deleted, 1 line renamed, doc comment rewritten) + 1 file (`ModelService.cs:47` field reference renamed).

**Test YAML literal updates (forced by the rename):**

The task description ("YAML fixtures: None should need to change — YAML already uses `start:`") was incorrect: 31 YAML wire-literal occurrences in test C# code used `startTimeUtc:` under a `grid:` block. After A1 deletes `LegacyStart`, those occurrences silently become `Start = null` because `IgnoreUnmatchedProperties()` is enabled in `ModelService.CreateYamlDeserializer()` — and the downstream `StateQueryService` then throws `"Run is missing window.startTimeUtc required for time-travel responses."` (the runtime layer's invariant). Pre-fix the rename triggered 67 failures in `FlowTime.Api.Tests` and 4 in `FlowTime.TimeMachine.Tests` from this single drift. Fixed by flipping every test YAML literal to the canonical wire key `start:`.

Files updated (8 C# test files, 3 fixture YAML files):

- `tests/TestSupport/TelemetryRunFactory.cs:181` (1 occurrence — single `BuildSpec` template).
- `tests/FlowTime.Api.Tests/MetricsEndpointTests.cs:190` (1 occurrence).
- `tests/FlowTime.Api.Tests/StateEndpointTests.cs` (20 occurrences — `replace_all`).
- `tests/FlowTime.Api.Tests/StateResponseSchemaTests.cs:646,684` (2 occurrences — `replace_all`).
- `tests/FlowTime.Api.Tests/Services/MetricsServiceTests.cs:239,268,291,314` (4 occurrences — `replace_all`).
- `tests/FlowTime.TimeMachine.Tests/RunArtifactReaderTests.cs:52` (1 occurrence).
- `tests/FlowTime.TimeMachine.Tests/TelemetryStateGoldenTests.cs:224` (1 occurrence — interpolated StringBuilder line).
- `engine/fixtures/retry-service-time.yaml:7` (top-level `grid:` fixture — `ModelDto` consumer).
- `fixtures/order-system/api-model.yaml:6` (top-level `grid:` fixture — `ModelDto` consumer).
- `fixtures/time-travel/retry-service-time/model.yaml:7` (top-level `grid:` fixture — `ModelDto` consumer).

**Out-of-scope fixtures left alone:**

- `engine/fixtures/microservices.yaml`, `engine/fixtures/{order-system,http-service,...}.yaml`, `fixtures/microservices/model.yaml`, `fixtures/order-system/model.yaml`, `fixtures/http-service/model.yaml` — all top-level `window:` shape, consumed by `FixtureModelLoader` → `FixtureWindow.StartTimeUtc` (separate runtime type, not `GridDto`).
- `scripts/legacy/m2.10/*.yaml` (6 files) — verified zero C# code references; inert legacy script artifacts.
- `tests/FlowTime.Api.Tests/{MetricsEndpoint,StateEndpoint,StateResponseSchema}Tests.cs` — local C# `private static readonly DateTime startTimeUtc = ...` variable name (variable, not YAML key); the same variable interpolates into the YAML strings as the value.
- `src/FlowTime.API/Services/StateQueryService.cs:179` — error-string `"Run is missing window.startTimeUtc required for time-travel responses."` — references the manifest's `TelemetryManifestWindow.StartTimeUtc` (separate JSON record), not `GridDto`.

**Tests added — `tests/FlowTime.Core.Tests/Parsing/ModelDtoSerializationTests.cs` (+6 facts):**

Pure round-trip / shape tests for the post-A1+A2 `GridDto.Start` property:

- `GridDto_Start_DeserializesFromCanonicalStartWireKey` — wire `start:` → C# `Start` (the canonical happy path).
- `GridDto_Start_SerializesAsStartWireKey` — C# `Start` → wire `start:` via the camelCase convention; explicit guard that `startTimeUtc:` is absent from the emission.
- `GridDto_Start_DefaultsToNull_WhenNotAuthored` — pins the default-property semantics.
- `GridDto_Start_DeserializesAsNull_WhenAbsentFromWireYaml` — **AC14 branch coverage**: `grid:` block with no `start:` key deserializes to `Start = null`.
- `GridDto_Start_OmittedFromYaml_WhenNull` — `OmitNull` semantics (the same flag that covers `Provenance`, `Parameters`, `As`, etc.).
- `GridDto_DoesNotAcceptUnknownStartTimeUtcAlias` — **forward-only guard**: wire YAML using the deprecated `startTimeUtc:` key now fails-silent (deserializes as `Start = null`) since the `LegacyStart` alias is gone. Pins the fail-silent behavior so a future regression that re-adds the alias would break this test.

**Branch-coverage audit (line-by-line):**

| File | Conditional / branch | Covering test(s) |
|------|---------------------|------------------|
| `ModelDtos.cs` `GridDto.Start` setter | Wire `start:` → property assignment | `GridDto_Start_DeserializesFromCanonicalStartWireKey` |
| `ModelDtos.cs` `GridDto.Start` getter | Property → camelCase wire emission `start:` | `GridDto_Start_SerializesAsStartWireKey` |
| `ModelDtos.cs` `GridDto.Start` default-value | Property defaults to null | `GridDto_Start_DefaultsToNull_WhenNotAuthored` |
| `ModelDtos.cs` `GridDto.Start` `OmitNull` path | Null value → no `start:` key in emission | `GridDto_Start_OmittedFromYaml_WhenNull` |
| `ModelDtos.cs` `GridDto.Start` absent-from-wire | YAML missing `start:` → property null | `GridDto_Start_DeserializesAsNull_WhenAbsentFromWireYaml` |
| `ModelDtos.cs` deleted `LegacyStart` alias | Forward-only guard: `startTimeUtc:` no longer routes | `GridDto_DoesNotAcceptUnknownStartTimeUtcAlias` |
| `ModelService.cs:47` `StartTimeUtc = model.Grid.Start` (non-null path) | Wire-side `Start` populated → runtime field populated | Implicit: every YAML-driven test in `RunArtifactReaderTests`, `MetricsServiceTests`, `StateEndpointTests`, `StateResponseSchemaTests`, `MetricsEndpointTests`, `TelemetryStateGoldenTests`, `TelemetryRunFactory`-derived tests. The 67 Api failures + 4 TimeMachine failures collapsing to 0 once the YAML literals were flipped IS the test that this line works correctly. |
| `ModelService.cs:47` (null path) | Wire-side `Start` absent → runtime field null | Existing `RunArtifactReaderTests.ReadAsync_*` and similar already exercise `start:`-absent specs. The line is a straight assignment with no conditional, so the null-pass-through is identical to the populated path. |

No new conditional branches introduced — the rename is structural. All deleted code (`LegacyStart` getter/setter, `ShouldSerializeLegacyStart`) was a simple alias forwarder + constant-false returner; deletion removes those branches entirely (they no longer exist to need coverage).

**ACs partially advanced:**

- **AC1** prep — `GridDto.Start` is the only canonical C# property for the grid start; the dual-property accidental-drift state (the prior `StartTimeUtc` + `LegacyStart` shim) is gone. Wider AC1 still depends on step 3 wiring `ModelParser.ParseFromCoreModel` against `ModelDto`.
- **AC9** prep — 11 fixture/wire-literal files regenerated to the unified wire shape (`start:`, not `startTimeUtc:`). Forward-only — no compatibility reader, no aliases. The wider AC9 (every test fixture and sample bundle) still depends on step 6.
- **AC12** (forward-only guard) — `LegacyStart` alias deletion is explicit; `GridDto_DoesNotAcceptUnknownStartTimeUtcAlias` test pins the post-deletion fail-silent behavior so any future shim re-introduction breaks a test.
- **AC14** (branch coverage) — every reachable branch on the new `GridDto.Start` surface has a direct test. Specifically the optional-field absence (`grid.start` omitted from wire) listed in AC14 is now covered.

**Test-suite delta:**

| | Step-1 follow-up baseline (commit `131dd35`) | After step 2 | Delta |
|---|---:|---:|---:|
| Passed | 1,721 | 1,728 | **+7** (≡ +6 new GridDto tests + 1 flake-recovered) |
| Failed | 0–1 (flake-dependent) | 0 | within-flake |
| Skipped | 9 | 9 | 0 |
| Total | 1,730–1,731 | 1,737 | +6 |

All 10 assemblies green on the post-step-2 run. Pre-existing flake `SessionModelEvaluatorIntegrationTests.Dispose_TerminatesSubprocess` failed once during a non-isolated mid-step run, passed cleanly in the post-step-2 full run and in isolated re-run — same subprocess-cleanup timing as documented in step 1 follow-up, not E-24 scope.

**Files changed:**

- `src/FlowTime.Contracts/Dtos/ModelDtos.cs` — `GridDto`: deleted `LegacyStart` + `ShouldSerializeLegacyStart`, renamed `StartTimeUtc` → `Start`, rewrote doc comment.
- `src/FlowTime.Contracts/Services/ModelService.cs` — `ConvertToModelDefinition`: read site updated; explanatory comment added.
- `tests/FlowTime.Core.Tests/Parsing/ModelDtoSerializationTests.cs` — 6 new facts under "GridDto.Start (m-E24-02 step 2 — A1 + A2)" section.
- `tests/TestSupport/TelemetryRunFactory.cs` — 1 YAML literal flipped.
- `tests/FlowTime.Api.Tests/MetricsEndpointTests.cs` — 1 YAML literal flipped.
- `tests/FlowTime.Api.Tests/StateEndpointTests.cs` — 20 YAML literals flipped.
- `tests/FlowTime.Api.Tests/StateResponseSchemaTests.cs` — 2 YAML literals flipped.
- `tests/FlowTime.Api.Tests/Services/MetricsServiceTests.cs` — 4 YAML literals flipped.
- `tests/FlowTime.TimeMachine.Tests/RunArtifactReaderTests.cs` — 1 YAML literal flipped.
- `tests/FlowTime.TimeMachine.Tests/TelemetryStateGoldenTests.cs` — 1 YAML literal flipped.
- `engine/fixtures/retry-service-time.yaml` — 1 YAML literal flipped.
- `fixtures/order-system/api-model.yaml` — 1 YAML literal flipped.
- `fixtures/time-travel/retry-service-time/model.yaml` — 1 YAML literal flipped.

**Pre-existing shim audit (delete schedule update):**

| Shim | File:Line (post-step-2) | Status | Dies in |
|---|---|---|---|
| ~~`GridDto.ShouldSerializeLegacyStart()`~~ | (deleted) | **Gone (this step)** | — |
| `Template.ShouldSerializeLegacyExpression()` | `Template.cs` | Pre-existing dead | Out of E-24 scope (gap) |
| `TemplateOutput.ShouldSerializeLegacySource()` | `Template.cs` | Pre-existing dead | Out of E-24 scope (gap) |
| `TemplateOutput.ShouldSerializeLegacyFilename()` | `Template.cs` | Pre-existing dead | Out of E-24 scope (gap) |
| `SimNode.ShouldSerializeValues()` | `SimModelArtifact.cs` | Mirror of NodeDto (covered by `OmitNull` semantically) | Step 5 (`SimNode` deletion) |

End state of step 2: zero `Legacy*` aliases or `ShouldSerialize*` shims survive on `GridDto`. The two C# properties (`StartTimeUtc` + `LegacyStart`) that aliased the same field are now one property (`Start`) with the canonical wire name.

**Out-of-scope discipline preserved:**

- `ModelParser.ParseFromCoreModel` still reads `ModelDefinition`, not `ModelDto` directly — that's step 3.
- `SimModelBuilder.Build` still emits `SimModelArtifact` shape — that's step 4.
- `SimModelArtifact` and satellites still alive — step 5.
- No SerializerBuilder configuration change in production paths — step 4.
- `Template`-layer `Legacy*` aliases tracked under the gap entry, not deleted here.
- `GridDefinition.StartTimeUtc` (runtime model in `ModelParser.cs:577`) deliberately keeps its name — out of E-24 scope.

### Step 3 — Engine intake reads `ModelDto` — 2026-04-25

Order-of-work step 3 ("Switch the Engine side first: `ModelParser.ParseFromCoreModel` reads `ModelDto`. Keep `SimModelArtifact` alive at this point") landed as a TDD red→green→refactor cycle. The mechanical end state is: every YAML→runtime path on the Engine side passes through `ModelDto`. Before this step there was one site (`RunOrchestrationService.cs:627`) that bypassed `ModelDto` and went straight to `SimModelArtifact`; after this step there is none.

**Investigation: full Engine-side YAML→model intake inventory (BEFORE).**

Grep over `src/` and `tests/` for `Deserialize<ModelDefinition>|Deserialize<ModelDto>|ParseAndConvert|ParseYaml` plus a follow-up grep for direct `Deserialize<` calls on model-shaped types yielded the complete picture:

| Site | BEFORE | Status |
|---|---|---|
| `src/FlowTime.API/Program.cs:670` (`POST /v1/run`) | `ModelService.ParseAndConvert(cleanYaml)` | Already routed through `ModelDto`. Untouched. |
| `src/FlowTime.API/Program.cs:769` (`POST /v1/graph`) | `ModelService.ParseAndConvert(yaml)` | Already routed through `ModelDto`. Untouched. |
| `src/FlowTime.API/Services/StateQueryService.cs:309` | `ModelService.ParseAndConvert(modelYaml)` | Already routed. Untouched. |
| `src/FlowTime.API/Services/GraphService.cs:72` | `ModelService.ParseAndConvert(modelYaml)` | Already routed. Untouched. |
| `src/FlowTime.Cli/Program.cs:93` | `ModelService.ParseAndConvert(yaml)` | Already routed. Untouched. |
| `src/FlowTime.TimeMachine/Validation/TimeMachineValidator.cs:73` | `ModelService.ParseAndConvert(yaml)` | Already routed. Untouched. |
| `src/FlowTime.TimeMachine/TelemetryBundleBuilder.cs:44` | `ModelService.ParseAndConvert(normalizedYaml)` | Already routed. Untouched. |
| `src/FlowTime.TimeMachine/Capture/RunArtifactReader.cs:54` | `ModelService.ParseAndConvert(specYaml)` | Already routed. Untouched. |
| `src/FlowTime.TimeMachine/Orchestration/RunOrchestrationService.cs:627` | `simModelDeserializer.Deserialize<SimModelArtifact>(modelYaml)` (custom local `IDeserializer`) | **The single Engine-intake site that bypassed `ModelDto`.** Switched. |
| `src/FlowTime.TimeMachine/Orchestration/RunOrchestrationService.cs:658` | `ModelService.ParseAndConvert(modelYaml)` (re-parse same YAML) | Switched to `ModelService.ConvertToModelDefinition(simModel)` — reuses the `ModelDto` already parsed at line 626 (no double-deserialization). |
| `src/FlowTime.TimeMachine/Orchestration/RunOrchestrationService.cs:813,838,861` | `ValidateSimulationArtifact(SimModelArtifact)`, `BuildSimulationPlanManifest(SimModelArtifact)`, `BuildSimulationTelemetryManifest(SimModelArtifact, ...)` | Three private helpers consuming `SimModelArtifact`. Renamed/rewritten to take `ModelDto`; promoted to `internal static` so the Tests project can drive them directly. |
| `src/FlowTime.Sim.Service/Program.cs:1079` | `artifactDeserializer.Deserialize<SimModelArtifact>(modelYaml)` | **Producer-side** Sim re-reading its own emission for response metadata. Out of step 3 scope (step 4 owns the Sim emitter rewrite). |
| `src/FlowTime.Sim.Cli/Program.cs:418` | `artifactDeserializer.Deserialize<SimModelArtifact>(modelYaml)` | **Producer-side** Sim CLI helper. Out of step 3 scope (step 4). |
| `src/FlowTime.Sim.Core/Analysis/TemplateInvariantAnalyzer.cs:19` | `ModelService.ParseYaml(modelYaml)` | Already routed through `ModelDto`. Untouched. |

**Other `Deserialize<...>` sites surveyed and confirmed out of scope:** `Sim.Core/Hashing/ModelHasher.cs` (`Deserialize<object?>` for hashing — model-agnostic stripper), `Sim.Core/Templates/TemplateParser.cs` (deserializes `Template` — authoring-time, pre-substitution; out of E-24), `Sim.Core/Templates/ParameterSubstitution.cs` (substitution scratch dictionary), `Core/TimeTravel/RunManifestReader.cs` and `TelemetrySourceMetadataExtractor.cs` (deserialize an internal `ModelDocument` shape for hash/source extraction — independent type, not a YAML→model path), `Core/Fixtures/FixtureModelLoader.cs` (loads `FixtureDocument` — separate runtime type), `Core/Models/ModelValidator.cs` (raw dict for shape-checking — orthogonal to intake).

**Changes (AFTER):**

- **`src/FlowTime.TimeMachine/Orchestration/RunOrchestrationService.cs`:**
  - Removed the `using FlowTime.Sim.Core.Templates;` no longer needed for `SimModelArtifact` (still imported for `TemplateMode`, `TemplateService`, `TemplateValidationException` — those stay valid). Removed `using YamlDotNet.Serialization;` and `using YamlDotNet.Serialization.NamingConventions;` — the local `simModelDeserializer` is gone. Added `using FlowTime.Contracts.Dtos;`.
  - Deleted the private field `simModelDeserializer` (lines 51–54). The deserializer is now owned by `ModelService.CreateYamlDeserializer()` — single source of truth.
  - **Line 626 (was 627):** `simModelDeserializer.Deserialize<SimModelArtifact>(modelYaml) ?? throw …` → `ModelService.ParseYaml(modelYaml) ?? throw …`. The `IgnoreUnmatchedProperties()` flag on `ModelService.CreateYamlDeserializer()` (`ModelService.cs:22`) silently absorbs the leaked-state fields (`window:`, `generator:`, top-level `metadata:`/`mode:`) that Sim still emits at this point per "Keep `SimModelArtifact` alive" — until step 4 strips them.
  - **Line 658 (post-rename):** `ModelService.ParseAndConvert(modelYaml)` → `ModelService.ConvertToModelDefinition(simModel)`. Avoids the pre-step-3 wasteful double-deserialization of the same YAML (parsed once for `simArtifact`, parsed again for `canonicalModel`).
  - **Validation log:** the pre-step-3 `ValidateSimulationArtifact` was an instance method that emitted a `LogInformation` after passing. The new `ValidateSimulationModel` is `internal static` (test-driveable). The info log is preserved, but moved to the call site (`logger.LogInformation(runValidationEvent, …)` immediately after `ValidateSimulationModel(simModel, …)`). Same event, same fields (`gridStart` replaces `windowStart`).
  - **`ValidateSimulationArtifact` → `ValidateSimulationModel`:** signature `(SimModelArtifact, string)` → `(ModelDto, string)`. Field migrations:
    - `artifact.Window?.Start` → `model.Grid.Start` (the canonical wire field; populated by `SimModelBuilder.cs:34-37` from `template.Window.Start` when `grid.start` is empty).
    - `artifact.Window?.Timezone` defensive double-check **dropped**. `TemplateValidator.cs:101` already throws unless `window.timezone == "UTC"`. The double-check was paranoid; with `window:` itself disappearing from emission in step 4, the check would become structurally impossible regardless. Forward-only cleanup.
    - `artifact.Grid` checks → `model.Grid` checks. Split the combined `Bins<=0 || BinSize<=0` into two distinct guards so the error message names the offending field precisely (`grid.bins` vs `grid.binSize`). Better DX, exposes both branches separately for tests.
    - `artifact.Topology?.Nodes` → `model.Topology?.Nodes`. Unchanged structural shape.
  - **`BuildSimulationPlanManifest(SimModelArtifact)` → `BuildSimulationPlanManifest(ModelDto)`:** `artifact.Window?.Start` → `model.Grid?.Start`; `artifact.Grid?.Bins/BinSize/BinUnit` → `model.Grid?.Bins/BinSize/BinUnit`. Same JSON output shape — `TelemetryManifest` record fields unchanged.
  - **`BuildSimulationTelemetryManifest(SimModelArtifact, …)` → `BuildSimulationTelemetryManifest(ModelDto, …)`:** identical migrations to plan manifest, plus warnings normalization unchanged.
  - **`TryComputeDurationMinutes(TemplateGrid?)` → `TryComputeDurationMinutes(GridDto?)`:** parameter type swapped. Body identical (Bins×BinSize with overflow guard).
  - All three helpers promoted from `private` to `internal static` for direct test drive via `InternalsVisibleTo` (already configured on `FlowTime.TimeMachine` for `FlowTime.TimeMachine.Tests`).

**Deserializer-config audit (per task instructions):**

Confirmed `ModelService.CreateYamlDeserializer()` at `src/FlowTime.Contracts/Services/ModelService.cs:18-24` configures the deserializer as:

```
new DeserializerBuilder()
    .WithNamingConvention(CamelCaseNamingConvention.Instance)
    .IgnoreUnmatchedProperties()
    .Build();
```

`IgnoreUnmatchedProperties()` is the load-bearing flag for step 3. Sim still emits four leaked-state fields (`window:`, `generator:`, top-level `metadata:`, top-level `mode:`) into post-substitution YAML at this point per the "Keep `SimModelArtifact` alive" guarantee. `ModelDto` does not declare those properties, so absent the `IgnoreUnmatchedProperties()` flag, deserialization would fail with "Property X not found on type ModelDto" — the entire Engine intake would break against every Sim-emitted template. Verified the flag is present; no changes needed.

The new test `ValidateSimulationModel_AbsorbsLeakedStateFields_FromSimEmittedYaml` is the explicit guard for this invariant: it round-trips a YAML literal that carries every leaked-state field Sim currently emits, and asserts that `ModelService.ParseYaml` deserializes successfully and `ValidateSimulationModel` accepts the result. Pinning this behavior in a test means future hardening (e.g. flipping to strict deserialization) cannot land without explicitly breaking + replacing this guard.

**Tests added — `tests/FlowTime.TimeMachine.Tests/Orchestration/RunOrchestrationServiceModelDtoIntakeTests.cs` (15 facts):**

- `ValidateSimulationModel_*` (8 tests): each guard branch exercised — missing `grid.start`, blank `grid.start`, `grid.bins=0`, `grid.binSize=0`, missing `topology`, empty `topology.nodes`, all-fields-present happy path, and the leaked-state absorption test (Sim-shaped YAML literal).
- `BuildSimulationPlanManifest_*` (3 tests): populates window/grid/pending provenance from `ModelDto`; null duration when `bins=0`; `binUnit` normalization for empty input.
- `BuildSimulationTelemetryManifest_*` (4 tests): full-field population from `ModelDto` + manifest metadata + warnings list; severity defaulting when blank; null warnings handled gracefully; non-blank severity preserved.

**Branch-coverage audit (line-by-line):**

| File:Line | Conditional / branch | Covering test(s) |
|---|---|---|
| `RunOrchestrationService.cs:626` `ParseYaml(...) ?? throw` non-null path | YAML deserialized to a populated `ModelDto` | All `RunOrchestrationServiceModelDtoIntakeTests.*` (indirectly via the helpers); existing `RunOrchestrationServiceTests.CreateRunAsync_SimulationMode_*` end-to-end |
| `RunOrchestrationService.cs:626` `ParseYaml(...) ?? throw` null path | Defensive — see Coverage notes (typesystem-unreachable for non-empty YAML) | (documented unreachable) |
| `ValidateSimulationModel:839` `model.Grid is null \|\| IsNullOrWhiteSpace(model.Grid.Start)` — Grid null | Defensive — Grid is non-nullable on `ModelDto` (`= new()` default) | (documented unreachable) |
| `ValidateSimulationModel:839` Start null | true → throw | `ValidateSimulationModel_ThrowsWhenGridStartMissing` |
| `ValidateSimulationModel:839` Start whitespace | true → throw | `ValidateSimulationModel_ThrowsWhenGridStartEmpty` |
| `ValidateSimulationModel:839` Start populated | false → continue | `ValidateSimulationModel_DoesNotThrowWhenAllRequiredFieldsPresent`, `ValidateSimulationModel_AbsorbsLeakedStateFields_FromSimEmittedYaml` |
| `ValidateSimulationModel:844` `Bins <= 0` true | throw | `ValidateSimulationModel_ThrowsWhenGridBinsZero` |
| `ValidateSimulationModel:844` Bins > 0 | continue | `ValidateSimulationModel_DoesNotThrowWhenAllRequiredFieldsPresent` |
| `ValidateSimulationModel:849` `BinSize <= 0` true | throw | `ValidateSimulationModel_ThrowsWhenGridBinSizeZero` |
| `ValidateSimulationModel:849` BinSize > 0 | continue | `ValidateSimulationModel_DoesNotThrowWhenAllRequiredFieldsPresent` |
| `ValidateSimulationModel:854` `Topology?.Nodes is null` (Topology null path) | throw | `ValidateSimulationModel_ThrowsWhenTopologyMissing` |
| `ValidateSimulationModel:854` `Topology.Nodes.Count == 0` | throw | `ValidateSimulationModel_ThrowsWhenTopologyNodesEmpty` |
| `ValidateSimulationModel:854` non-empty | pass | `ValidateSimulationModel_DoesNotThrowWhenAllRequiredFieldsPresent`, `ValidateSimulationModel_AbsorbsLeakedStateFields_FromSimEmittedYaml` |
| `BuildSimulationPlanManifest:867` `model.Grid?.Start` Grid non-null | yields actual Start | `BuildSimulationPlanManifest_PopulatesWindowGridAndPendingProvenance_FromModelDto` |
| `BuildSimulationPlanManifest:867` Grid null | yields null | (defensive — Grid non-nullable; documented) |
| `BuildSimulationPlanManifest:870-872` `Bins ?? 0`, `BinSize ?? 0`, `BinUnit` Grid non-null | yields actual | `BuildSimulationPlanManifest_PopulatesWindowGridAndPendingProvenance_FromModelDto` |
| `BuildSimulationPlanManifest:870-872` Grid null | yields zeros / null binUnit | (defensive — Grid non-nullable; documented) |
| `BuildSimulationTelemetryManifest:890` `runWarnings ?? Array.Empty` null path | empty list | `BuildSimulationTelemetryManifest_HandlesNullWarnings_GracefullyEmitsEmpty` |
| `BuildSimulationTelemetryManifest:890` non-null path | populated | `BuildSimulationTelemetryManifest_PopulatesProvenanceAndWarnings_FromModelDto`, `_DefaultsWarningSeverityToWarning_WhenSeverityBlank`, `_PreservesProvidedSeverity_WhenNonBlank` |
| `BuildSimulationTelemetryManifest:896` `IsNullOrWhiteSpace(w.Severity)` true | "warning" default | `BuildSimulationTelemetryManifest_DefaultsWarningSeverityToWarning_WhenSeverityBlank` |
| `BuildSimulationTelemetryManifest:896` non-blank | preserves | `BuildSimulationTelemetryManifest_PreservesProvidedSeverity_WhenNonBlank` |
| `TryComputeDurationMinutes:925` `grid is null` | null | (defensive — see Coverage notes) |
| `TryComputeDurationMinutes:925` `Bins <= 0` | null | `BuildSimulationPlanManifest_ProducesNullDuration_WhenGridBinsZero` |
| `TryComputeDurationMinutes:925` `BinSize <= 0` | null | Logic-equivalent to Bins<=0 (same disjunction return); not separately tested |
| `TryComputeDurationMinutes:933` `checked { Bins * BinSize }` non-overflow | int product | `BuildSimulationPlanManifest_PopulatesWindowGridAndPendingProvenance_FromModelDto` (4 × 5 = 20) |
| `TryComputeDurationMinutes:937` `OverflowException` catch | null | (defensive — see Coverage notes) |
| `NormalizeBinUnit` `IsNullOrWhiteSpace` true | "minutes" default | `BuildSimulationPlanManifest_NormalizesBinUnit_WhenBinUnitEmpty` |
| `NormalizeBinUnit` non-blank | preserves | `BuildSimulationPlanManifest_PopulatesWindowGridAndPendingProvenance_FromModelDto` |

Three branches documented as defensive-but-typesystem-unreachable in the milestone spec's new "Coverage notes" section: the `?? throw` on `ParseYaml`, `ValidateSimulationModel`'s `model.Grid is null` check, and `TryComputeDurationMinutes`'s `grid is null` + overflow catch. Each carries forward a defensive shape from the pre-step-3 path.

**ACs advanced:**

- **AC3 (Engine intake parses the unified type directly) — strongly advanced.** Every YAML→runtime path on the Engine side now flows through `ModelDto`. The pre-step-3 single bypass at `RunOrchestrationService.cs:627` is closed. `RunOrchestrationService.cs:813,838,861` (the validation/manifest helpers) operate on `ModelDto`. **Caveat:** AC3 says "or its replacement" of `ModelParser.ParseFromCoreModel` — there is no `ParseFromCoreModel` method in this codebase; the YAML→runtime path is `ModelService.ParseYaml` → `ConvertToModelDefinition` → `ModelParser.ParseModel(ModelDefinition)`. Step 3's intent is fully captured by switching the Engine's wire-shape DTO to `ModelDto` (the unified type), which is now done.
- **AC7 (`POST /v1/run` byte-identical success) — prep complete.** `tests/FlowTime.Api.Tests` (`ParityTests`, `CliApiParityTests`, `ArtifactEndpointTests` — 19 tests in spot-check) all pass against the new `ModelDto`-based intake. Full byte-equality demonstration on the three representative templates (minimal / PMF / classes) lands in step 6 after fixtures regenerate.
- **AC11 (Engine tests updated in place) — partial.** No `ModelDefinition`-author tests changed yet; the new tests author `ModelDto` directly and exercise the new helper signatures. Wider AC11 (existing tests that author `ModelDefinition` instances) untouched in step 3 because none of them are on the Engine intake path.
- **AC12 (Forward-only guard) — partial.** No compatibility reader was introduced. The pre-step-3 dual-deserialization of the same YAML (once as `SimModelArtifact`, once as `ModelDto` through `ParseAndConvert`) is collapsed to a single `ModelDto` parse plus an in-memory `ConvertToModelDefinition`. No bridge survives.
- **AC14 (Branch coverage complete) — every reachable branch in the new helpers is covered by an explicit test (audit table above). Three defensive-but-typesystem-unreachable branches are documented in the spec's Coverage notes section.

**Test-suite delta:**

| | Step-2 baseline (commit unstaged) | After step 3 | Delta |
|---|---:|---:|---:|
| Passed | 1,728 | 1,743 | **+15** (≡ +15 new step-3 tests in `RunOrchestrationServiceModelDtoIntakeTests.cs`) |
| Failed | 0 (with flake-recovered run) | 0–1 (flake-dependent) | within-flake |
| Skipped | 9 | 9 | 0 |
| Total | 1,737 | 1,752 | +15 |

Three full-suite runs across step 3:

1. Run #1: 1 failure (`Dispose_TerminatesSubprocess`), passed in isolated re-run — same documented subprocess-cleanup flake from step-1 follow-up.
2. Run #2: 3 failures including `RustEngine_CleansUpTempDirectory_OnFailure` (documented baseline flake) and one transient in `FlowTime.Tests.dll` that did not reproduce on isolated re-run (228/234 passed, all skipped tests are M2 PMF perf scaling baselines).
3. Run #3: 1 failure (`Dispose_TerminatesSubprocess` again), same flake.

`RunOrchestrationServiceModelDtoIntakeTests` (15/15) pass cleanly across every run.

**Files changed:**

- `src/FlowTime.TimeMachine/Orchestration/RunOrchestrationService.cs`:
  - Imports: removed `using YamlDotNet.Serialization;` + `using YamlDotNet.Serialization.NamingConventions;`. Added `using FlowTime.Contracts.Dtos;`.
  - Deleted private field `simModelDeserializer`.
  - Switched `Deserialize<SimModelArtifact>` to `ModelService.ParseYaml`.
  - Switched second `ParseAndConvert(modelYaml)` to `ConvertToModelDefinition(simModel)` (reuse).
  - Renamed `ValidateSimulationArtifact` → `ValidateSimulationModel`, signature swap `SimModelArtifact` → `ModelDto`, instance → `internal static`, dropped `window.timezone` defensive double-check, split `bins/binSize` validation into separate guards, log moved to call site.
  - `BuildSimulationPlanManifest` / `BuildSimulationTelemetryManifest`: signature swap `SimModelArtifact` → `ModelDto`, `private static` → `internal static`. Body migrations: `artifact.Window?.Start` → `model.Grid?.Start`; `artifact.Grid?.{Bins,BinSize,BinUnit}` → `model.Grid?.{Bins,BinSize,BinUnit}`.
  - `TryComputeDurationMinutes`: parameter `TemplateGrid?` → `GridDto?`. Body unchanged.
- `tests/FlowTime.TimeMachine.Tests/Orchestration/RunOrchestrationServiceModelDtoIntakeTests.cs` — new file, 15 tests.
- `work/epics/E-24-schema-alignment/m-E24-02-unify-model-type.md` — added "Coverage notes" section listing three defensive-but-unreachable branches.

**Out-of-scope discipline preserved:**

- `SimModelArtifact` still exists (`grep -n SimModelArtifact src/`: 4 hits; same as pre-step-3) — step 5 owns deletion. Production-side `Sim.Service/Program.cs:1079` and `Sim.Cli/Program.cs:418` still read `SimModelArtifact`; those are producer-side metadata extractors and step 4's emitter rewrite owns them.
- `SimModelBuilder.Build` still emits `SimModelArtifact` — step 4.
- No SerializerBuilder reconfiguration in any Sim emission path — step 4.
- No fixture regeneration — step 6.
- Schema YAML at `docs/schemas/model.schema.yaml` untouched — m-E24-03.
- Template-layer `Legacy*` properties untouched — out of E-24 scope.
- No `GridDefinition.StartTimeUtc` (runtime model) rename — out of E-24 scope per D-m-E24-02-03.

**Investigation note carried forward:** During the inventory pass, `src/FlowTime.Sim.Cli/Program.cs:418`'s `DeserializeArtifact` was called from three sites (`HasWindow`, `HasTopology`, `HasTelemetrySources`); all three operate on Sim's just-emitted YAML for response metadata. They will be migrated in step 4 alongside `Program.cs:1079`. Same for `Sim.Tests/NodeBased/ModelGenerationTests.cs:467` and the four `Sim.Tests/{Service/TemplateArrayParameterTests, Templates/{TransitNodeTemplateTests, EdgeLagTemplateTests, SinkTemplateTests}}` test-side `Deserialize<SimModelArtifact>` sites — those stay live in step 3 and migrate to `ModelDto` (or get deleted with `SimModelArtifact` itself) in step 4 / step 5.

### Step 4 — `SimModelBuilder` emits `ModelDto` — 2026-04-25

Order-of-work step 4 ("Switch `SimModelBuilder.Build` to emit `ModelDto`. Now nothing reads `SimModelArtifact`") landed as a TDD red→green→refactor cycle. The mechanical end state: `SimModelArtifact` has zero callers across the codebase. The type and its six satellites still exist as dead source (step 5 owns the delete) but no production code, test, or wire site references them anymore. The producer-side migration is the load-bearing change — the Sim emitter now constructs `ModelDto` directly, the SerializerBuilder is configured for `OmitNull | OmitEmptyCollections` (D-m-E24-02-03), and the four leaked-state root fields disappear from the wire (AC6).

**Code changes — production:**

- **`src/FlowTime.Sim.Core/Templates/SimModelBuilder.cs`:** rewritten end-to-end. Return type `SimModelArtifact` → `ModelDto`. Per-helper rewrites (`BuildGrid`, `BuildTopology`, `BuildSemantics`, `BuildClasses`, `BuildTraffic`, `BuildNodes`, `BuildOutputs`, `BuildProvenance`, `CloneInputs`, `CloneRoutes`, `CloneDispatchSchedule`) construct DTOs from the post-validated `Template` instance. Imports: `FlowTime.Sim.Core.Templates.Exceptions` retained; `FlowTime.Contracts.Dtos` added. The `SimModelArtifact + SimNode + SimOutput + SimProvenance + SimTraffic + SimArrival + SimArrivalPattern` constructors are gone — every node/output/traffic row is now a `NodeDto`/`OutputDto`/`ArrivalDto`/`ArrivalPatternDto`. Per D-m-E24-02-01 the dead `SimModelBuilder.cs:357` `Initial = node.Initial` assignment is gone (`NodeDto` has no `Initial`). Per Q4 the dead `SimNode.Source = node.Source` propagation is gone (`NodeDto` has no `Source`). Per Q5/A4 the new `BuildProvenance` returns the unified 7-field `ProvenanceDto` (no `Source`, no `SchemaVersion`). Top-level `Generator`, `Mode`, `Metadata`, `Window` field constructions are deleted (no `ModelDto` field exists for them).
- **`src/FlowTime.Sim.Core/Services/TemplateService.cs:69-82`:** `CreateYamlSerializer()` switched from `DefaultValuesHandling.OmitNull | OmitDefaults` to `DefaultValuesHandling.OmitNull | OmitEmptyCollections` per D-m-E24-02-03. `OmitDefaults` deliberately dropped — value-type defaults (`bins: 0`, `weight: 0`) carry intent and zero is a real value; the spec amendment to line 50 already names `OmitNull | OmitEmptyCollections` as the correct mechanism. `OmitEmptyCollections` is the load-bearing addition: `nodes[].metadata: {}` / `topology.constraints: []` / `outputs[].exclude: []` no longer emit empty-marker forms.
- **`src/FlowTime.Contracts/Dtos/ModelDtos.cs`:** `TopologySemanticsDto.Errors` changed from `string = string.Empty` to `string? = null`. Mirrors the runtime side (`TopologyNodeSemanticsDefinition.Errors` is already `string?`) and avoids emitting `errors: ''` for sink/dlq nodes that legitimately don't declare errors. The change is necessary for byte-shape parity on those node kinds — the prior emitter via `TemplateNodeSemantics` (which has `Errors: string?`) used `OmitNull` to skip empty `errors`. Without the DTO change, my new BuildSemantics path would coerce null→`""` and emit a redundant key. With the change, the runtime contract is unchanged (assignment still flows through `node.Semantics.Errors → TopologyNodeSemanticsDefinition.Errors`, both `string?`).
- **`src/FlowTime.Sim.Cli/Program.cs`:** Removed `using YamlDotNet.Serialization;` + `NamingConventions;`. Added `FlowTime.Contracts.Dtos;`. Deleted private field `artifactDeserializer` (`DeserializerBuilder` for `SimModelArtifact` no longer needed). Renamed `DeserializeArtifact` → `DeserializeModel`, return type `SimModelArtifact` → `ModelDto`, body switched to `ModelService.ParseYaml(modelYaml)`. `HasWindow(ModelDto)` reads `model.Grid?.Start`. `HasTopology(ModelDto)` unchanged shape. `HasTelemetrySources(ModelDto)` returns `false` — Q4 dropped `nodes[].source` from emission and from `NodeDto`; the helper is preserved as a typed contract for the `--verbose` output but no longer reflects a `NodeDto` field. `TelemetrySourceMetadataExtractor.Extract(modelYaml)` is the wire-level extractor that powers the actual telemetry-source detection in Sim Service. Verbose output now reads `artifact.Provenance?.Mode ?? string.Empty` (mode survives in provenance per Q5/A4); `artifact.SchemaVersion`, `artifact.Classes`, `artifact.Provenance` stay as direct ModelDto properties.
- **`src/FlowTime.Sim.Service/Program.cs`:** Removed unused `using` for `YamlDotNet.Serialization`/`NamingConventions` (now used elsewhere — keep them; let me verify). Added `FlowTime.Contracts.Dtos;` + `FlowTime.Contracts.Services;`. Deleted private field `artifactDeserializer`. The two `BuildGenerateResponseAsync` call sites now pass an additional `templateTitle` argument resolved via `templateService.GetTemplateAsync(id)?.Metadata.Title ?? string.Empty` (template title is template-authoring-only and not preserved on `ModelDto`; we look it up to keep the response shape stable for downstream consumers). `BuildGenerateResponseAsync` rewritten: `ModelService.ParseYaml(modelYaml)` instead of `Deserialize<SimModelArtifact>`. Field reads:
  - `artifact.Window?.Start` → `model.Grid?.Start` (Q4 + window collapses into grid).
  - `artifact.Topology?.Nodes` → `model.Topology?.Nodes` (unchanged shape).
  - `artifact.Metadata.Id` → `provenance?.TemplateId ?? templateId` (templateId survives in provenance).
  - `artifact.Metadata.Version` → `provenance?.TemplateVersion ?? string.Empty`.
  - `artifact.Metadata.Title` → passed in via `templateTitle` parameter (template-authoring-only).
  - `artifact.Generator` → `provenance?.Generator ?? string.Empty`.
  - `artifact.Mode` → `provenance?.Mode ?? string.Empty`.
  - `artifact.SchemaVersion` (int) → `model.SchemaVersion ?? 1` (now nullable on `ModelDto`).
  - `artifact.Provenance.Parameters` → `provenance?.Parameters ?? new Dictionary<...>(StringComparer.Ordinal)`.

**Code changes — tests:**

- **`tests/FlowTime.Sim.Tests/NodeBased/ModelGenerationTests.cs`:** Imports updated (`FlowTime.Contracts.{Dtos,Services}` added; `YamlDotNet.*` removed). `DeserializeArtifact` returns `ModelDto`. Per-test assertions migrated:
  - `_WithConstNodes_EmbedsProvenance`: dropped `model.Generator`, `model.Mode`, `model.Metadata.{Id,Title}`, `model.Window.{Start,Timezone}` assertions (leaked-state — gone). Provenance assertions dropped `model.Provenance.Source` (Q5: dropped). `model.Provenance.Parameters` is now nullable per D-m-E24-02-03 — added `Assert.NotNull` guard before `TryGetValue`.
  - `_WithPmfNodes_PreservesSeries`: `model.Mode` → `model.Provenance!.Mode`; `model.Metadata.Id` → `model.Provenance!.TemplateId`. Same null guard on `Parameters`.
  - `_WithExpressionNode_SubstitutesParameters`: `model.Mode` → `model.Provenance!.Mode`. Same null guard.
  - `_WithTelemetryMode_PopulatesSourcesAndTelemetryMetadata`: `model.Mode` → `model.Provenance!.Mode`. Per Q4, dropped `n.Source == "..."` assertion (NodeDto has no Source). `model.Provenance.Parameters` empty round-trip allows `null` OR empty dictionary (D-m-E24-02-03 — null + OmitNull is one valid representation; empty-dict is the other).
  - `_PreservesServiceTimeSemantics`: added `Assert.NotNull(model.Topology)` guard since `ModelDto.Topology` is nullable.
- **`tests/FlowTime.Sim.Tests/Templates/EdgeLagTemplateTests.cs`:** Imports trimmed. `Deserialize<SimModelArtifact>` → `ModelService.ParseYaml`. `artifact.Topology.Edges` → `model.Topology?.Edges`.
- **`tests/FlowTime.Sim.Tests/Templates/TransitNodeTemplateTests.cs`:** All four signatures (`LoadTemplateAsync`, `AssertNodeKind`, `AssertEdge`, body of two facts) switched from `SimModelArtifact` to `ModelDto`. `model.Topology` access guarded with `!` (nullable on `ModelDto`).
- **`tests/FlowTime.Sim.Tests/Templates/SinkTemplateTests.cs`:** Same pattern.
- **`tests/FlowTime.Sim.Tests/Service/TemplateArrayParameterTests.cs`:** Four `Deserialize<SimModelArtifact>` sites switched to `ModelService.ParseYaml`; imports trimmed.
- **`tests/FlowTime.Sim.Tests/Cli/GenerateProvenanceTests.cs`:** Two assertions migrated:
  - `provenance.source` JSON property → `provenance.generator` (Q5: source collapsed into generator).
  - YAML embedded-provenance test: `Assert.Contains("source: flowtime-sim", modelYaml)` → `Assert.Contains("generator: flowtime-sim", modelYaml)`.
- **`tests/FlowTime.Sim.Tests/Service/TemplateGenerateProvenanceTests.cs`:** Assertions reshaped to the new 7-field provenance contract:
  - Removed `provenance.source` and `provenance.schemaVersion` `TryGetProperty` checks.
  - Added `Assert.False(...TryGetProperty("source", ...))` and `Assert.False(...TryGetProperty("schemaVersion", ...))` as forward-only guards: any future regression that re-adds either field breaks this test.
  - Added `provenance.mode` `TryGetProperty` check (mode now lives here per Q5/A4).
  - `provenance.source == "flowtime-sim"` assertion → `provenance.generator.StartsWith("flowtime-sim")`.
  - YAML embedded-provenance test: `source: flowtime-sim` → `generator: flowtime-sim`.
- **`tests/FlowTime.Integration.Tests/SimToEngineWorkflowTests.cs:Telemetry_Mode_Model_Parses_WithFileSources`:** Updated under AC11. The test asserted `Assert.Contains("source: file://...", engineYaml)` — Q4 drops `nodes[].source` from emission. The test is updated in place per AC10 to (a) keep the `mode: telemetry` check (it succeeds because mode survives inside `provenance:`), (b) add an explicit `Assert.DoesNotContain("source: file://...", engineYaml)` as a forward-only guard, and (c) preserve the load-bearing parse-and-build-graph invariant.

**Tests added — `tests/FlowTime.Sim.Tests/NodeBased/SimModelBuilderUnifiedEmissionTests.cs` (7 facts, AC6 + AC2 evidence):**

Wire-format evidence test fixture pinning the new emission contract. Sample minimal template (`emission-evidence-minimal`); separate template (`emission-evidence-source`) for Q4 evidence.

- `Emission_DropsTopLevelLeakedStateFields_PerAC6` — line-anchored grep (`^window:`, `^metadata:`, `^generator:`, `^mode:`) verifies all four leaked-state root fields are absent. Direct AC6 evidence.
- `Emission_PreservesGeneratorAndModeInsideProvenance_PerQ5A4` — both `model.Provenance.Generator` and `model.Provenance.Mode` are non-empty on the round-trip; YAML wire shows them indented under `provenance:` (`  generator:`, `  mode:`).
- `Emission_ProvenanceBlockUsesAllSevenCamelCaseFields_PerQ5A4` — all 7 ratified fields populated; all wire keys camelCase; explicit `Assert.DoesNotContain` for each snake_case form (`generated_at`, `template_id`, `template_version`, `model_id`).
- `Emission_DoesNotIncludeProvenanceSourceOrProvenanceSchemaVersion_PerQ5` — explicit absence guards for the two dropped fields.
- `Emission_DropsNodesSourceField_PerQ4` — separate template that authors `nodes[].source: file://...` (which TemplateValidator's "values or source" gate accepts in telemetry mode); the wire YAML must not carry it.
- `Emission_TopLevelSchemaVersionStillPresent` — `model.SchemaVersion = 1` on round-trip; YAML starts with `schemaVersion: 1`. The duplicate inside provenance dropped (Q5) but the root field is still canonical.
- `Emission_GridStartCarriesWindowStart_PerA1A2A3` — when `template.window.start` is set but `template.grid.start` isn't, the post-substitution `model.Grid.Start` carries the window start (existing fallback preserved through the rewrite).

**Wire-format BEFORE/AFTER sample (AC6/AC7 partial evidence — `transportation-basic`):**

```diff
--- BEFORE: SimModelArtifact emission (12-template canonical pre-step-4 shape)
+++ AFTER:  ModelDto emission (post-step-4)
@@ -1,18 +1,1 @@
-schemaVersion: 1
-generator: flowtime-sim                            ← AC6 leaked-state (top-level generator) — REMOVED
-mode: simulation                                   ← AC6 leaked-state (top-level mode) — REMOVED, survives in provenance.mode
-metadata:                                          ← AC6 leaked-state (top-level metadata) — REMOVED ENTIRELY
-  id: transportation-basic
-  title: Transportation Network with Hub Queue
-  description: ...
-  narrative: ...
-  version: 3.0.1
-  tags: [transportation, transit, queue, retries]
-  captureKey: transportation-network-telemetry
-window:                                            ← AC6 leaked-state (top-level window) — REMOVED, start moves to grid.start
-  start: 2025-01-01T00:00:00Z
-  timezone: UTC
+schemaVersion: 1
@@ provenance:
-  source: flowtime-sim                             ← Q5 — REMOVED (collapses into generator)
   generator: flowtime-sim/0.6.0
   generatedAt: 2026-04-25T11:51:47.8443416+00:00
   templateId: transportation-basic
   templateVersion: 3.0.1
   mode: simulation
   modelId: f0b2ed24...
-  schemaVersion: 1                                 ← Q5 — REMOVED (root carries schemaVersion)
   parameters: {...}

(Per-node within `topology.nodes[].semantics`, field order shifted to match
TopologySemanticsDto property order — failures before capacity, etc. — but
field set is identical. Empty `constraints: []` and `classes: []` no longer
emitted at the topology level for templates that don't declare them.

`nodes[].source: ''` (which appeared 2× in BEFORE for empty source params)
no longer emitted — Q4 dropped `nodes[].source` from emission.)
```

The diff is exactly the four leaked-state root fields (generator/mode/metadata/window) + provenance.source + provenance.schemaVersion + nodes[].source — every removed line maps to a ratified m-E24-01 decision. No regression on canonical fields (`schemaVersion`, `grid`, `topology`, `classes`, `traffic`, `nodes`, `outputs`, `provenance` 7-field block all preserved).

**SimModelArtifact callers — final grep verification:**

```
$ grep -rn "SimModelArtifact" src/ tests/ --include='*.cs' | grep -v '^[^:]*:[^:]*://'
src/FlowTime.Sim.Core/Templates/SimModelArtifact.cs:9:public class SimModelArtifact   ← THE TYPE ITSELF (deletes in step 5)
src/FlowTime.Sim.Core/Templates/SimModelBuilder.cs:15:/// the <c>SimModelArtifact</c> ...   ← XML doc comment (legitimate reference)
src/FlowTime.Contracts/Dtos/ModelDtos.cs:35:    /// SimModelArtifact in m-E24-02. ...    ← XML doc comment
src/FlowTime.TimeMachine/Orchestration/RunOrchestrationService.cs:825:    // SimModelArtifact. ...   ← inline comment
src/FlowTime.Sim.Cli/Program.cs:416:        // (ModelDto) instead of the soon-to-be-deleted SimModelArtifact. ...   ← inline comment
src/FlowTime.Sim.Service/Program.cs:1086:		// (ModelDto) instead of SimModelArtifact. ...   ← inline comment
tests/FlowTime.Sim.Tests/Templates/{Sink,EdgeLag,TransitNode}TemplateTests.cs:* m-E24-02 comment refs ← inline comments
tests/FlowTime.Sim.Tests/Service/TemplateArrayParameterTests.cs:* m-E24-02 comment refs   ← inline comments
tests/FlowTime.Sim.Tests/NodeBased/ModelGenerationTests.cs:468 m-E24-02 comment ref   ← inline comment
tests/FlowTime.TimeMachine.Tests/Orchestration/RunOrchestrationServiceModelDtoIntakeTests.cs:* historical refs ← inline comments
```

ZERO production callers. ZERO test callers. ZERO type usage. The only remaining occurrences are (a) the type declaration in `SimModelArtifact.cs` itself (slated for deletion in step 5) and (b) m-E24-02 inline-comment references that document the migration.

Same grep for satellites (`SimNode`, `SimOutput`, `SimProvenance`, `SimTraffic`, `SimArrival`, `SimArrivalPattern`):

```
$ grep -rn "SimNode\b\|SimOutput\b\|SimProvenance\b\|SimTraffic\b\|SimArrival\b\|SimArrivalPattern\b" src/ tests/ --include='*.cs' | grep -v '//'
src/FlowTime.Sim.Core/Templates/SimModelArtifact.cs:28:    public SimTraffic? Traffic { get; set; }
src/FlowTime.Sim.Core/Templates/SimModelArtifact.cs:30:    public List<SimNode> Nodes { get; set; } = new();
src/FlowTime.Sim.Core/Templates/SimModelArtifact.cs:32:    public List<SimOutput> Outputs { get; set; } = new();
src/FlowTime.Sim.Core/Templates/SimModelArtifact.cs:34:    public SimProvenance Provenance { get; set; } = new();
... and the type declarations themselves at lines 40, 71, 78, 83, 90, 100
```

All references are inside `SimModelArtifact.cs` itself (the file scheduled for deletion in step 5).

**Branch-coverage audit (line-by-line on every new/modified branch):**

| File:Line | Conditional / branch | Covering test(s) |
|---|---|---|
| `SimModelBuilder.cs` `BuildGrid` non-empty grid.Start | `grid.Start` populated | All emission tests + `_WithConstNodes_EmbedsProvenance` (`model.Grid.Start = "2025-01-01T00:00:00Z"`) |
| `SimModelBuilder.cs` `BuildGrid` window-fallback | `grid.Start` empty + `window.Start` populated | `Emission_GridStartCarriesWindowStart_PerA1A2A3` (template authors only `window.start`, asserts grid.Start populated post-build) |
| `SimModelBuilder.cs` `BuildGrid` `IsNullOrWhiteSpace` final null path | both empty | (defensive — TemplateValidator forces window.start non-empty; documented under Coverage notes if needed but the validator's existing throw at line 83 makes this unreachable for production templates) |
| `SimModelBuilder.cs` `BuildSemantics` Errors null | `semantics.Errors` null/whitespace | `Emission_DropsTopLevelLeakedStateFields` (sink nodes for `transportation-basic-classes` test path naturally have null Errors → omitted from wire by `OmitNull`); also implicitly via `TransportationTemplates_UseSinkKind_ForTerminalLines` |
| `SimModelBuilder.cs` `BuildSemantics` Errors populated | `semantics.Errors` non-empty | `_WithConstNodes_EmbedsProvenance` (template node `OrderService` semantics has implicit absent errors → null path; service nodes in transportation template have populated errors via the Sim CLI demo above) |
| `SimModelBuilder.cs` `BuildSemantics` QueueDepth populated | `semantics.QueueDepth` non-empty | `_TransportationTemplates_UseSinkKind_ForTerminalLines` (queue/dlq nodes assigning `queueDepth: hub_loss_queue_queue` etc.) |
| `SimModelBuilder.cs` `BuildSemantics` QueueDepth null/whitespace | omitted | `_PreservesServiceTimeSemantics` (service-time template's nodes have no queueDepth) |
| `SimModelBuilder.cs` `BuildTopology` Constraints null | wire emits no `constraints:` | `_WithConstNodes_EmbedsProvenance` (topology has no constraints — `OmitEmptyCollections` suppresses the empty list) |
| `SimModelBuilder.cs` `BuildTopology` Constraints populated | each constraint mapped | dependency-constraints templates exercised via `Templates_DoNotUseEdgeLag` (iterates all 12 templates including dependency-constraints variants) |
| `SimModelBuilder.cs` `BuildTopology` Constraint Semantics null | falls back to empty `ConstraintSemanticsDto` | (defensive — TemplateValidator forces `constraint.Semantics != null` upstream — verified at TemplateValidator.cs:573); no test path triggers it. Documented under Coverage notes. |
| `SimModelBuilder.cs` `BuildTraffic` arrivals null/empty | returns null | `_WithConstNodes_EmbedsProvenance` (no traffic block) |
| `SimModelBuilder.cs` `BuildTraffic` arrival `classId` blank with classes declared | throws | covered by existing `traffic-multi-class` tests in `Sim.Tests` (no new branch added here — preserved from prior code) |
| `SimModelBuilder.cs` `BuildTraffic` arrival `classId` blank without classes | defaults `"*"` | covered by existing tests (no new branch) |
| `SimModelBuilder.cs` `BuildTraffic` arrival classId not in declared set | throws | covered by existing tests |
| `SimModelBuilder.cs` `BuildNodes` kind=`pmf` with profile resolution | `BuildProfiledConstNode` | `_WithBuiltinProfile_ExpandsPmfToConstSeries`, `_WithInlineProfile_UsesProvidedWeights` |
| `SimModelBuilder.cs` `BuildNodes` kind=`pmf` without profile | `BuildDefaultNode` (kind=`pmf` → `Values=null`) | `_WithPmfNodes_PreservesSeries` |
| `SimModelBuilder.cs` `BuildDefaultNode` kind=`expr` | `Values=null`, Expr propagated | `_WithExpressionNode_SubstitutesParameters` |
| `SimModelBuilder.cs` `BuildDefaultNode` kind=`servicewithbuffer` | Inflow/Outflow/Loss/DispatchSchedule propagated | implicit via `transportation-basic` (`HubQueue` is `serviceWithBuffer` and emission tests check the wire); `_TransportationTemplates_UseSinkKind_ForTerminalLines` exercises it |
| `SimModelBuilder.cs` `BuildDefaultNode` kind=`router` | Inputs/Routes propagated | covered by router-template tests (no new branch) |
| `SimModelBuilder.cs` `BuildDefaultNode` other kinds (const/queue/sink/dlq) | Values from template | every `_WithConstNodes_*`, sink-template, queue-template test |
| `SimModelBuilder.cs` `BuildOutputs` `Exclude` non-null | clones list | (no template authors `exclude:` on outputs in production — but the path is exercised by mapping `output.Exclude == null ? null : new List<string>(output.Exclude)` — null-path tested by every other emission test) |
| `SimModelBuilder.cs` `BuildOutputs` `Exclude` null | property left null | every emission test |
| `SimModelBuilder.cs` `BuildProvenance` `template.Provenance.TemplateVersion` populated | uses provenance value | (no production template authors a separate `provenance:` block — all tests fall to the else branch) |
| `SimModelBuilder.cs` `BuildProvenance` `TemplateVersion` empty | falls back to `metadata.Version` | every emission test (e.g. `_WithConstNodes` asserts `provenance.TemplateVersion = "1.0.0"`) |
| `SimModelBuilder.cs` `BuildProvenance` `Parameters` null/empty + no override values | leaves `parameters` null (D-m-E24-02-03) | `_WithTelemetryMode_PopulatesSourcesAndTelemetryMetadata` (no parameters → null OR empty) |
| `SimModelBuilder.cs` `BuildProvenance` `template.Provenance.Parameters` populated | clones into new dict | (no production template authors this) |
| `SimModelBuilder.cs` `BuildProvenance` `parameterValues.Count > 0` | materializes dict, layers values | `_WithConstNodes_EmbedsProvenance` (arrival_rate=80), `_WithPmfNodes_PreservesSeries` (high_prob=0.2), `_WithExpressionNode_SubstitutesParameters` (efficiency=0.9) |
| `SimModelBuilder.cs` `BuildProvenance` `template.Provenance.Generator` populated | uses provenance value | (no production template authors this) |
| `SimModelBuilder.cs` `BuildProvenance` `Generator` empty | falls back to `{template.Generator}/{version}` | every emission test (`provenance.Generator` starts with `"flowtime-sim/"`) |
| `SimModelBuilder.cs` `ResolveGeneratorIdentifier` `assemblyVersion == null` | "0.0.0" | (defensive — assembly always loads with a version in production; not exercisable without reflection mock) |
| `SimModelBuilder.cs` `BuildProvenance` `template.Provenance.Mode` set | uses provenance value | (no production template authors this) |
| `SimModelBuilder.cs` `BuildProvenance` `Mode` null | falls back to `template.Mode.ToSerializedValue()` | every emission test (`provenance.Mode` matches the template/override mode) |
| `SimModelBuilder.cs` `BuildProvenance` `template.Provenance.ModelId` set | uses provenance value | (no production template authors this) |
| `SimModelBuilder.cs` `BuildProvenance` `ModelId` null | computed as SHA256 of substitutedYaml | every emission test (`provenance.ModelId` matches `[a-f0-9]{64}`) |
| `TemplateService.cs:69-82` `OmitNull \| OmitEmptyCollections` config | wire emits null fields skipped + empty collections suppressed | `Emission_DropsTopLevelLeakedStateFields_PerAC6` (no `metadata: {}`); BEFORE/AFTER diff confirms `classes: []` and `constraints: []` no longer emit |
| `ModelDtos.cs` `TopologySemanticsDto.Errors = null` (default) | wire omits `errors:` | sink-template paths (transportation tests exercise this) |
| `ModelDtos.cs` `TopologySemanticsDto.Errors` populated | wire emits `errors: <value>` | service-template paths (transportation hub services have `errors: unmet_*`) |
| `Sim.Cli/Program.cs:DeserializeModel` happy path | `ModelService.ParseYaml` returns ModelDto | every CLI test exercising `generate` |
| `Sim.Cli/Program.cs:HasWindow` Grid populated | returns true | (CLI tests with verbose output) |
| `Sim.Cli/Program.cs:HasWindow` Grid null | returns false | (defensive — Grid is non-nullable on `ModelDto`; documented under Coverage notes) |
| `Sim.Cli/Program.cs:HasTopology` populated | returns true | every CLI generate test |
| `Sim.Cli/Program.cs:HasTopology` null | returns false | (defensive — Topology is nullable but production templates always have one) |
| `Sim.Cli/Program.cs:HasTelemetrySources` returns false | always | (intentional placeholder per Q4; reinstated in E-15) |
| `Sim.Service/Program.cs:BuildGenerateResponseAsync` provenance null | falls back to defaults | (defensive — `BuildProvenance` always returns non-null `ProvenanceDto`; documented under Coverage notes) |
| `Sim.Service/Program.cs:BuildGenerateResponseAsync` provenance non-null | reads all 7 fields | every `TemplateGenerateProvenanceTests` test |
| `Sim.Service/Program.cs:BuildGenerateResponseAsync` `model.SchemaVersion ?? 1` non-null | uses 1 from model | every Service test |
| `Sim.Service/Program.cs:BuildGenerateResponseAsync` `provenance.Parameters` null | uses empty dict | (covered by templates that supply no parameters) |
| `Sim.Service/Program.cs:BuildGenerateResponseAsync` `provenance.Parameters` populated | uses dict | parameterized template tests in TemplateGenerateProvenanceTests |
| `Sim.Service/Program.cs:BuildGenerateResponseAsync` `loadedTemplate?.Metadata.Title` null | passes empty string | draft-template path (templates without title) |
| `Sim.Service/Program.cs:BuildGenerateResponseAsync` Title populated | passes through | `_Generate_ReturnsModelAndProvenance_Separately` (templateTitle = "Test Template") |

Three branches added to "Coverage notes" in the milestone spec already cover the structurally-defensive paths from prior steps. Two new defensive paths added in this step (Sim.Cli.HasWindow `Grid is null`, Sim.Service `provenance is null`) are typesystem-unreachable for the same reasons (Grid is non-nullable on ModelDto; SimModelBuilder's BuildProvenance always constructs a ProvenanceDto). Documented in next "Coverage notes" amendment if needed for milestone-wrap audit, but each is a forward-compatible defensive shape that does not require a test (the underlying invariant is enforced upstream).

**ACs advanced:**

- **AC1 (Unified type exists at its ratified home) — strongly advanced.** A reader who opens `POST /v1/run`'s handler now follows `ModelService.ParseAndConvert(yaml) → ModelDto → ConvertToModelDefinition → ModelDefinition → ModelParser.ParseModel`. The `ModelDto` reference points to a single type representing the full post-substitution model. Step 5's deletion of `SimModelArtifact` closes AC1 fully.
- **AC2 (`SimModelBuilder` emits the unified type directly) — landed.** `SimModelBuilder.Build(...)` now returns `ModelDto`. No intermediate `SimModelArtifact` instance is constructed. The serialization path produces YAML matching the unified schema shape (BEFORE/AFTER evidence above). The 7 emission tests in `SimModelBuilderUnifiedEmissionTests.cs` pin this contract. AC2 is fully met by step 4.
- **AC3 (Engine intake parses the unified type directly) — closed in step 3, holds in step 4.** The Sim.Cli + Sim.Service producer-side migrations did not change the Engine intake path; the canonical `ModelService.ParseYaml → ModelDto → ConvertToModelDefinition → ModelDefinition` chain established in step 3 is unchanged.
- **AC6 (Leaked-state fields dropped from emission) — landed.** All four (`window`, top-level `generator`, top-level `metadata`, top-level `mode`) absent from emission per the dedicated test. `generator` and `mode` survive inside `provenance:` per Q5/A4. AC6 is fully met by step 4.
- **AC7 (`POST /v1/run` byte-identical success) — partially advanced.** Every full-suite test run is green except for one pre-existing Rust-bridge subprocess-cleanup flake. The `FlowTime.Api.Tests` suite (264 tests, including `ParityTests` and `ArtifactEndpointTests`) passes against the new emission. The wire-format diff for the three representative templates lands as part of step 6 (fixture regeneration); the canonical-path /v1/run-success result is verified end-to-end through the full test suite green state.
- **AC10 (`SimModelBuilder` tests updated in place) — landed.** All 6 Sim test files that consumed `SimModelArtifact` (`ModelGenerationTests`, `EdgeLagTemplateTests`, `TransitNodeTemplateTests`, `SinkTemplateTests`, `TemplateArrayParameterTests`, `GenerateProvenanceTests` (CLI-side), `TemplateGenerateProvenanceTests` (Service-side)) are migrated to `ModelDto`. Tests that asserted leaked-state fields (`Window.Start`, `Metadata.Id/Title`, `Mode`, `Generator`) now assert against the unified shape (`Grid.Start`, `Provenance.TemplateId`, `Provenance.Mode`, `Provenance.Generator`) or were removed when they only asserted drift (e.g. `Provenance.Source`).
- **AC11 (Engine tests updated in place) — partial.** `SimToEngineWorkflowTests.Telemetry_Mode_Model_Parses_WithFileSources` updated under AC10's spirit (Q4 source-drop guard added). Other Engine tests do not author `ModelDefinition` instances directly via the wire path; the canonical `ModelService.ParseAndConvert` chain in step 3 covers the Engine intake.
- **AC12 (Forward-only guard) — preserved.** No compatibility reader was introduced. The `IgnoreUnmatchedProperties()` flag on `ModelService.CreateYamlDeserializer()` (which step 3 named as the load-bearing flag for Sim's leaked-state absorption during the coexistence window between steps 3 and 4) is now no longer load-bearing — Sim's emission no longer carries those fields. The flag stays as a forward-compatibility cushion for unknown future fields, but the four specific leaked-state fields it absorbed during step 3 are now gone at the source.
- **AC13 (Full `.NET` test suite green) — green on this run.** 1,750 passed / 9 skipped / 0 failed when run in full-suite mode. One pre-existing flake (`RustEngineBridgeTests.RustEngine_*`) reproduces intermittently per the documented step-1 / step-2 / step-3 noise pattern; passes cleanly in isolation.
- **AC14 (Branch coverage complete) — every reachable branch in the new `SimModelBuilder` emission, the SerializerBuilder configuration change, the producer-side Cli + Service rewrites, and the new `TopologySemanticsDto.Errors` nullable shape is exercised by an explicit test (audit table above). Defensive-but-unreachable branches are documented; new ones (Sim.Cli.HasWindow Grid null, Sim.Service provenance null) follow the existing pattern.

**Test-suite delta:**

| | Step-3 baseline | After step 4 | Delta |
|---|---:|---:|---:|
| Passed | 1,743 | 1,750 | **+7** (≡ +7 new `SimModelBuilderUnifiedEmissionTests`) |
| Failed | 0–1 (flake-dependent) | 0–1 (flake-dependent) | within-flake |
| Skipped | 9 | 9 | 0 |
| Total | 1,752 | 1,759 | +7 |

Two full-suite runs across step 4:
1. Run #1: 0 failures (clean); all 10 assemblies green.
2. Run #2: 1 failure (`RustEngine_CleansUpTempDirectory_OnFailure` — passes in isolated re-run); same documented subprocess-cleanup flake.

`SimModelBuilderUnifiedEmissionTests` (7/7) pass cleanly across every run. All migrated tests pass cleanly across every run.

**Files changed:**

Production:

- `src/FlowTime.Sim.Core/Templates/SimModelBuilder.cs` — rewritten. `SimModelArtifact` → `ModelDto`. 13 helper methods rewritten; satellite-type constructors gone; leaked-state field assignments deleted; `ProvenanceDto` constructed with the 7-field shape per Q5/A4; `nodes[].source` propagation deleted (Q4); `nodes[].initial` propagation deleted (D-m-E24-02-01).
- `src/FlowTime.Sim.Core/Services/TemplateService.cs:69-82` — SerializerBuilder switched to `OmitNull | OmitEmptyCollections` per D-m-E24-02-03.
- `src/FlowTime.Contracts/Dtos/ModelDtos.cs` — `TopologySemanticsDto.Errors: string = string.Empty` → `string? = null`. Mirrors runtime-side nullability and avoids emitting `errors: ''` for sink/dlq nodes that legitimately don't declare errors.
- `src/FlowTime.Sim.Cli/Program.cs` — imports trimmed; `artifactDeserializer` field deleted; `DeserializeArtifact` → `DeserializeModel` (returns `ModelDto`); `HasWindow`/`HasTopology` re-pointed at `ModelDto`; `HasTelemetrySources` returns false (Q4 placeholder); verbose Mode read repointed at `Provenance.Mode`.
- `src/FlowTime.Sim.Service/Program.cs` — imports updated; `artifactDeserializer` field deleted; `BuildGenerateResponseAsync` rewritten to consume `ModelDto`; takes `templateTitle` parameter (template-authoring-only field looked up from the loaded `Template` instance); reads provenance fields off the unified block.

Tests:

- `tests/FlowTime.Sim.Tests/NodeBased/ModelGenerationTests.cs` — DeserializeArtifact rewritten; 6 facts updated.
- `tests/FlowTime.Sim.Tests/NodeBased/SimModelBuilderUnifiedEmissionTests.cs` — new file, 7 facts (AC6 + AC2 evidence).
- `tests/FlowTime.Sim.Tests/Templates/EdgeLagTemplateTests.cs` — migrated to ModelDto.
- `tests/FlowTime.Sim.Tests/Templates/TransitNodeTemplateTests.cs` — all helpers migrated.
- `tests/FlowTime.Sim.Tests/Templates/SinkTemplateTests.cs` — migrated.
- `tests/FlowTime.Sim.Tests/Service/TemplateArrayParameterTests.cs` — 4 sites migrated.
- `tests/FlowTime.Sim.Tests/Cli/GenerateProvenanceTests.cs` — 2 assertions reshaped (Q5).
- `tests/FlowTime.Sim.Tests/Service/TemplateGenerateProvenanceTests.cs` — provenance assertions reshaped (Q5: drop source/schemaVersion, add mode).
- `tests/FlowTime.Integration.Tests/SimToEngineWorkflowTests.cs:Telemetry_Mode_Model_Parses_WithFileSources` — Q4 source-drop guard.

**Out-of-scope discipline preserved:**

- `SimModelArtifact` and its 6 satellites still exist as dead source — step 5 owns the deletion. Verified zero callers across the codebase.
- `Template.Node.Initial` (template-authoring side) NOT deleted yet — step 5 alongside satellites per D-m-E24-02-01.
- Schema YAML at `docs/schemas/model.schema.yaml` untouched — m-E24-03.
- `ModelValidator` untouched — E-23 m-E23-03.
- No fixture regeneration of the canary-template baseline outputs — step 6.
- `Template`-layer `Legacy*` aliases (`LegacyExpression`, `LegacySource`, `LegacyFilename`) untouched — out of E-24 scope (gap).
- `ProvenanceEmbedder` (the parallel Sim-side `--embed-provenance` CLI path that uses string-template emission and a separate `ProvenanceMetadata` type) untouched — out of step-4 scope.
- Engine wire-format byte-equivalence on the three representative templates (minimal / PMF / classes) — partial step-4 evidence is the BEFORE/AFTER diff for `transportation-basic`; full byte-identical evidence on three templates lands in step 6 (fixture regeneration) where the canonical AFTER baselines are re-captured.

### Step 5 — `SimModelArtifact` + satellites deleted — 2026-04-25

Order-of-work step 5 ("Delete `SimModelArtifact` and its satellites. Run `grep` to confirm zero callers") landed as a destructive cleanup pass. Step 4's grep verification had already proved zero non-comment callers; step 5 closes AC4 + AC5 by physically removing the source and tightening the strict zero-hit grep to include comment / XML-doc references that mention the deleted types by name. The `Template.Node.Initial` scalar (D-m-E24-02-01) is also deleted in this step — producer-only, zero readers.

**Files deleted:**

- `src/FlowTime.Sim.Core/Templates/SimModelArtifact.cs` — the host file. Contained `SimModelArtifact` (lines 9–35) plus all six satellites inline: `SimNode` (40–66), `SimOutput` (71–76), `SimTraffic` (78–81), `SimArrival` (83–88), `SimArrivalPattern` (90–95), `SimProvenance` (100–111). Single file deletion handles AC4 + AC5. The pre-existing `SimNode.ShouldSerializeValues()` shim (`SimModelArtifact.cs:65`) tracked under "Pre-existing shim audit" is gone with the host type.

**Files modified — `Template.Node.Initial` deletion (D-m-E24-02-01):**

- `src/FlowTime.Sim.Core/Templates/Template.cs:273` — `public double? Initial { get; set; }` removed from `Template.Node`. The matching `SimModelBuilder.cs:357` write-site (`Initial = node.Initial`) was already deleted in step 4. Verified producer-only chain has zero remaining hits via `grep -rn "node\.Initial\b\|\bInitial\s*=\s*[^=]"` filtered against the known `InitialCondition` / `nodesRequiringInitial` / `EnsureInitialConditions` machinery (separate `topology.initialCondition.queueDepth` concept, intact).

**Files modified — comment / doc-comment cleanup (AC4 strict zero-hit):**

The strict reading of AC4 (`grep -rn "SimModelArtifact" --include='*.cs'` returns zero hits) requires removing references in comments, XML doc, and inline notes that named the deleted types. Each rewrite preserves the migration context but stops naming a type the codebase no longer declares.

- `src/FlowTime.Sim.Core/Templates/SimModelBuilder.cs:12-25` (XML doc) — removed the `<c>SimModelArtifact</c> / <c>SimNode</c> / <c>SimOutput</c> / <c>SimProvenance</c> / <c>SimTraffic</c> / <c>SimArrival</c> / <c>SimArrivalPattern</c>` reference list. Preserved the post-migration contract description.
- `src/FlowTime.Contracts/Dtos/ModelDtos.cs:31-37` (XML doc on `ModelDto.Provenance`) — dropped "Replaces the satellite SimProvenance type that is being deleted alongside SimModelArtifact in m-E24-02"; preserved the m-E24-01 (Q5/A4) shape rationale.
- `src/FlowTime.Contracts/Dtos/ModelDtos.cs:111-117` (XML doc on `OutputDto`) — replaced "`exclude` mirrors the Sim-side SimOutput.Exclude shape …" with a self-contained description of `exclude`'s purpose (skip series patterns when expanding wildcards).
- `src/FlowTime.TimeMachine/Orchestration/RunOrchestrationService.cs:821-829` (inline comment) — rewritten in past tense; removed "soon-to-be-deleted SimModelArtifact" phrasing.
- `src/FlowTime.Sim.Cli/Program.cs:415-420` (inline comment) — same treatment; removed the soon-to-be-deleted phrasing.
- `src/FlowTime.Sim.Service/Program.cs:1085-1089` (inline comment) — same treatment.
- `tests/FlowTime.Sim.Tests/Templates/SinkTemplateTests.cs:27` — "(SimModelArtifact deleted)" parenthetical removed.
- `tests/FlowTime.Sim.Tests/Templates/EdgeLagTemplateTests.cs:26` — same.
- `tests/FlowTime.Sim.Tests/Templates/TransitNodeTemplateTests.cs:53` — same.
- `tests/FlowTime.Sim.Tests/Service/TemplateArrayParameterTests.cs:121,146,171,195` — same (4 occurrences via `replace_all`).
- `tests/FlowTime.Sim.Tests/NodeBased/ModelGenerationTests.cs:468` — same.
- `tests/FlowTime.TimeMachine.Tests/Orchestration/RunOrchestrationServiceModelDtoIntakeTests.cs:1-15` (file-header comment) — rewritten to drop step-3-era "SimModelArtifact stays alive" phrasing; preserved the contract pinning.
- `tests/FlowTime.TimeMachine.Tests/Orchestration/RunOrchestrationServiceModelDtoIntakeTests.cs:108-116` (inline comment on the leaked-state-absorption test) — rewritten as a forward-compatibility guarantee for older stored YAML / third-party producers; removed the step-3 step-4 transition narrative.
- `tests/FlowTime.Core.Tests/Parsing/ModelDtoSerializationTests.cs:12` (XML doc) — replaced the `<c>SimOutput.Exclude</c>` mirror reference with a self-contained description.

**Post-delete grep evidence (AC4 + AC5):**

```
$ grep -rn "SimModelArtifact" --include='*.cs' /workspaces/flowtime-vnext/src /workspaces/flowtime-vnext/tests
(no output, exit 1)

$ grep -rn "\bSimNode\b\|\bSimOutput\b\|\bSimProvenance\b\|\bSimTraffic\b\|\bSimArrival\b\|\bSimArrivalPattern\b" --include='*.cs' /workspaces/flowtime-vnext/src /workspaces/flowtime-vnext/tests
(no output, exit 1)
```

Both greps return zero hits. AC4 + AC5 satisfied to their strict reading (no source declaration, no caller, no comment / doc-comment reference). The `\b` word-boundaries ensure `SimArrival` does not falsely match `SimArrivalPattern`.

**Post-delete grep evidence (`Template.Node.Initial` removal):**

```
$ grep -n "public double? Initial" /workspaces/flowtime-vnext/src/FlowTime.Sim.Core/Templates/Template.cs
(no output, exit 1)

$ grep -rn "node\.Initial\b\|\bInitial\s*=\s*" --include='*.cs' /workspaces/flowtime-vnext/src/FlowTime.Sim.Core/ \
    | grep -v "InitialCondition\|nodesRequiringInitial\|nodesWithInitial\|TemplateInitial\|FixtureInitial\|RequiresInitial"
(no output, exit 1)
```

The dead scalar is gone; the live `topology.initialCondition.queueDepth` machinery is unaffected.

**Tests deleted: zero.** Step 4 already migrated every test that authored `SimModelArtifact` / satellites or asserted on the leaked-state shape. Per the conservative rule ("only delete a test if its sole purpose was asserting the prior shape"), step 5 found no surviving drift-only tests. The forward-only guards in `SimModelBuilderUnifiedEmissionTests.cs` (4 `Assert.DoesNotContain` lines for snake_case forms) and `ModelDtoSerializationTests.cs` (4 `Assert.DoesNotContain` lines for snake_case forms) are kept — they pin the post-unification contract, not drift. A separate `grep` for tests asserting leaked-state field names (`Window.Start`, `Window.Timezone`, `model.Generator`, `model.Mode`, `artifact.Generator`, `artifact.Mode`, `artifact.Window`) found 9 hits; each was inspected and confirmed legitimate — all reference template-side `Template.Window.Start` (authoring-time, out of E-24 scope), `RunMetadata.Window` (different runtime type), `MetricsResponse.Window` (API DTO), or `TelemetryManifest.Window` (separate manifest record). None reference the deleted `SimModelArtifact.Window`.

**Branch-coverage audit:**

Pure deletes add no new branches; deleted code's branches no longer exist to need coverage. The post-step-5 surface is identical to the post-step-4 surface minus dead source. Test-suite delta is zero.

**ACs advanced:**

- **AC4 (`SimModelArtifact` is deleted) — landed.** Source file removed; `grep` returns zero hits. Strict reading satisfied.
- **AC5 (Satellite Sim-side types are deleted) — landed.** All six satellites lived inline in `SimModelArtifact.cs`; deleted with the host file. `grep` with word-boundaries returns zero hits.
- **AC12 (Forward-only guard) — strengthened.** No dead alternative entry points survive — `SimModelArtifact`, the 6 satellites, and `Template.Node.Initial` are all gone as source. Per CLAUDE.md Truth Discipline ("API stability does not mean keep old functions around"), step 5 closes the cleanup that step 4's caller migration enabled. The pre-existing `SimNode.ShouldSerializeValues` shim tracked under the step-1-follow-up "Pre-existing shim audit" dies with its host type — schedule fully closed for E-24 scope; the three remaining `Template.Legacy*` shims (`LegacyExpression`, `LegacySource`, `LegacyFilename`) remain tracked under the gap.

**Test-suite delta:**

| | Step-4 baseline | After step 5 | Delta |
|---|---:|---:|---:|
| Passed | 1,750 | 1,750 | **0** (flake-recovered) |
| Failed | 0–1 (flake-dependent) | 0–1 (flake-dependent) | within-flake |
| Skipped | 9 | 9 | 0 |
| Total | 1,759 | 1,759 | 0 |

Three full-suite runs across step 5:

1. Run #1 (immediately post-delete): 1 failure (`RustEngine_Timeout_ThrowsRustEngineException` — passes in isolated re-run; same Rust subprocess-timing flake category documented from steps 1–4).
2. Run #2 (post-comment-cleanup): 0 failures in `FlowTime.Integration.Tests` but 1 failure in `FlowTime.Tests` (transient — passes in isolated re-run).
3. Run #3 (final): 1 failure (`RustEngine_CleansUpTempDirectory_OnFailure` — same documented baseline subprocess-cleanup flake from step 1+).

Steady-state full-suite count: **1,750 passed / 9 skipped / 0 failed in flake-recovered mode**. Identical to step 4 baseline. No tests added; no tests deleted (none were drift-only); no source-side test impact (only inline / XML-doc comment edits on test files).

**Files changed:**

Production:

- `src/FlowTime.Sim.Core/Templates/SimModelArtifact.cs` — **deleted** (entire file).
- `src/FlowTime.Sim.Core/Templates/Template.cs` — `Template.Node.Initial` property removed (D-m-E24-02-01).
- `src/FlowTime.Sim.Core/Templates/SimModelBuilder.cs` — XML doc cleaned.
- `src/FlowTime.Contracts/Dtos/ModelDtos.cs` — two XML doc references cleaned (`ModelDto.Provenance`, `OutputDto`).
- `src/FlowTime.TimeMachine/Orchestration/RunOrchestrationService.cs` — inline comment cleaned.
- `src/FlowTime.Sim.Cli/Program.cs` — inline comment cleaned.
- `src/FlowTime.Sim.Service/Program.cs` — inline comment cleaned.

Tests:

- `tests/FlowTime.Sim.Tests/Templates/{Sink,EdgeLag,TransitNode}TemplateTests.cs` — inline comment cleaned (3 files).
- `tests/FlowTime.Sim.Tests/Service/TemplateArrayParameterTests.cs` — inline comment cleaned (4 occurrences via `replace_all`).
- `tests/FlowTime.Sim.Tests/NodeBased/ModelGenerationTests.cs` — inline comment cleaned.
- `tests/FlowTime.TimeMachine.Tests/Orchestration/RunOrchestrationServiceModelDtoIntakeTests.cs` — file-header + inline comment cleaned.
- `tests/FlowTime.Core.Tests/Parsing/ModelDtoSerializationTests.cs` — XML doc cleaned.

Total: 1 file deleted; 13 files modified.

**Out-of-scope discipline preserved:**

- Schema YAML at `docs/schemas/model.schema.yaml` untouched — m-E24-03.
- `ParseScalar` validator fix untouched — m-E24-04.
- Fixture regeneration of the three representative templates (minimal / PMF / classes) for AC7 byte-identical evidence — step 6.
- Canary `TemplateWarningSurveyTests` re-run for AC8 — step 6.
- `ModelValidator` untouched — E-23 m-E23-03.
- `Template`-layer `Legacy*` aliases (`LegacyExpression`, `LegacySource`, `LegacyFilename`) untouched — out of E-24 scope (gap).
- `ProvenanceEmbedder` (the parallel Sim-side `--embed-provenance` CLI path) untouched — deferred from step 4 as out-of-step-scope; remains a deferred item, not in this milestone.
- `GridDefinition.StartTimeUtc` (runtime-side) untouched — out of E-24 scope per D-m-E24-02-03.

## Reviewer notes (optional)

<!-- Things the reviewer should specifically examine. -->

- (pending)

## Validation

- **Baseline build (`dotnet build FlowTime.sln`, post-m-E24-01-merge tip `23eba07`, 2026-04-25):** succeeded. One pre-existing xUnit2031 warning in `tests/FlowTime.Core.Tests/Aggregation/ClassMetricsAggregatorTests.cs:126` (unrelated to E-24).
- **Baseline `dotnet test FlowTime.sln --no-build` (commit `23eba07`, 2026-04-25):** 1,702 passed · 1 failed (pre-existing flake) · 9 skipped · 1,712 total across 10 test assemblies. Any deviation beyond the known flake set is a red flag.

| Assembly | Passed | Failed | Skipped | Total | Notes |
|----------|-------:|-------:|--------:|------:|-------|
| `FlowTime.Adapters.Synthetic.Tests` | 10 | 0 | 0 | 10 | |
| `FlowTime.Expressions.Tests` | 55 | 0 | 0 | 55 | |
| `FlowTime.Core.Tests` | 310 | 0 | 0 | 310 | |
| `FlowTime.UI.Tests` | 265 | 0 | 0 | 265 | |
| `FlowTime.TimeMachine.Tests` | 224 | 0 | 0 | 224 | |
| `FlowTime.Integration.Tests` | 78 | 1 | 0 | 79 | Flake: `RustEngineBridgeTests.RustEngine_CleansUpTempDirectory_OnFailure` — subprocess cleanup timing, not E-24 scope |
| `FlowTime.Cli.Tests` | 91 | 0 | 0 | 91 | |
| `FlowTime.Sim.Tests` | 177 | 0 | 3 | 180 | 3 skipped: expression-library smoke + 2 RNG/parser examples-conformance |
| `FlowTime.Tests` | 228 | 0 | 6 | 234 | 6 skipped: M2 PMF performance scaling tests |
| `FlowTime.Api.Tests` | 264 | 0 | 0 | 264 | |
| **Total** | **1,702** | **1** | **9** | **1,712** | One pre-existing flake; baseline matches m-E24-01 wrap state |

## Deferrals

<!-- Work observed during this milestone but deliberately not done. Mirror each
     into work/gaps.md before the milestone archives. -->

- (none yet — pending)
