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

- [ ] **AC1 — Unified type exists at its ratified home.** `ModelDto` (the unified type per m-E24-01 Q1+Q2) lives in `FlowTime.Contracts`. A reader who opens `POST /v1/run`'s handler can follow the type reference to a single definition that represents the full post-substitution model.
- [ ] **AC2 — `SimModelBuilder` emits the unified type directly.** `SimModelBuilder.Build(...)` returns `ModelDto` (or an immutable value carrying it). No intermediate `SimModelArtifact` instance is constructed as a bridge. The serialization path produces YAML matching the unified schema shape.
- [ ] **AC3 — Engine intake parses the unified type directly.** `ModelParser.ParseFromCoreModel` (or its replacement) deserializes into `ModelDto` then converts to `ModelDefinition`. `RunOrchestrationService.cs:627` and sibling Engine-side deserialization sites operate on `ModelDto`.
- [ ] **AC4 — `SimModelArtifact` is deleted.** `src/FlowTime.Sim.Core/Templates/SimModelArtifact.cs` is removed from the repo. `grep -rn "SimModelArtifact" --include='*.cs'` returns zero hits.
- [ ] **AC5 — Satellite Sim-side types are deleted.** `SimNode`, `SimOutput`, `SimProvenance`, `SimTraffic`, `SimArrival`, `SimArrivalPattern` are removed. Each satellite either merged into the unified type's equivalent (per m-E24-01 satellite-disposition table) or was deleted because it had no consumer. `grep -rn "SimNode\b\|SimOutput\b\|SimProvenance\b\|SimTraffic\b\|SimArrival\b\|SimArrivalPattern\b" --include='*.cs'` returns zero hits.
- [ ] **AC6 — Leaked-state fields dropped from emission.** Per m-E24-01's decisions, `window`, `generator`, top-level `metadata`, and top-level `mode` no longer appear in emitted YAML. Whatever traceability content was meaningful has been moved into `provenance` (per Q5/A4 — `mode` and `generator` survive there).
- [ ] **AC7 — `POST /v1/run` byte-identical success.** For every template in `templates/*.yaml` with default parameters, `POST /v1/run` returns the same response body pre- and post-m-E24-02. The pre-/post-comparison is captured in this tracking doc as explicit evidence (JSON response diff on at least three representative templates: one minimal, one with PMF nodes, one with classes).
- [ ] **AC8 — `POST /v1/validate` at Analyse.** The canary `TemplateWarningSurveyTests` is run pre- and post-m-E24-02. The non-`ParseScalar` portion of `val-err` (the four top-level leaked-state shapes, the provenance snake_case shapes, the outputs shapes, the empty-classes shape) drops to zero post-m-E24-02. The `ParseScalar` residual (~231 errors) remains until m-E24-04 lands. The full residual histogram is captured here.
- [ ] **AC9 — Fixtures and samples regenerated.** Every test fixture, sample bundle, and reference YAML under `tests/` and `docs/samples/` (or equivalent paths) is regenerated under the unified shape in this milestone. No compatibility reader survives. Any fixture that cannot be regenerated is deleted with a tracking-doc note explaining why.
- [ ] **AC10 — `SimModelBuilder` tests updated in place.** Tests in `tests/FlowTime.Sim.Tests` that asserted the presence of `SimModelArtifact` fields (e.g. `window.start`, top-level `metadata`, provenance snake_case keys) are updated to assert against the unified shape or deleted if the test's only purpose was asserting drift.
- [ ] **AC11 — Engine tests updated in place.** Tests in `tests/FlowTime.Core.Tests`, `tests/FlowTime.Api.Tests`, `tests/FlowTime.TimeMachine.Tests`, and `tests/FlowTime.Integration.Tests` that author `ModelDefinition` instances directly are updated to author the unified type.
- [ ] **AC12 — Forward-only guard.** No compatibility reader for the old two-type YAML shape exists at epic-branch tip after this milestone. Any legacy-shape detection code that appeared during refactor is deleted in the same change.
- [ ] **AC13 — Full `.NET` test suite green.** `dotnet test FlowTime.sln` passes. No new regressions beyond the known validator residuals (tracked in AC8) which close in m-E24-03 and m-E24-04.
- [ ] **AC14 — Branch coverage complete.** Every reachable branch added or modified in `SimModelBuilder`, `ModelDto`'s serializer hooks, and the Engine's parser is exercised by at least one test. Node-kind variants (value / expr / pmf / inflow / outflow), empty-collection cases (no classes, no outputs, no provenance), and optional-field absence (`grid.start` omitted, `nodes[].source` omitted per m-E24-01 decision) each have coverage.

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
- Step 2 (`GridDto.LegacyStart` delete + `StartTimeUtc` → `Start` rename) still pending.
- Pre-existing dead `ShouldSerialize*` shims in `Template.cs` are tracked under the new gap entry, not deleted in this commit.

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
