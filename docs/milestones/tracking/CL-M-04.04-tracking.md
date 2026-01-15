# CL-M-04.04 Implementation Tracking

**Milestone:** CL-M-04.04 — Telemetry Contract & Loop Validation for Classes  
**Started:** 2025-11-25  
**Status:** 🔄 In Progress  
**Branch:** `milestone/m4.4`

---

## Quick Links

- **Milestone Document:** `docs/milestones/CL-M-04.04.md`
- **Latest Release:** `docs/releases/CL-M-04.03.md`
- **Milestone Guide:** `docs/development/milestone-documentation-guide.md`

---

## Current Status

### Overall Progress
- [x] Phase 1: Contract & Docs (3/3 tasks)
- [x] Phase 2: Engine + Telemetry Plumbing (3/3 tasks)
- [x] Phase 3: Capture Endpoint & Loop Tests (3/3 tasks)

### Test Status
- ✅ Targeted: `dotnet test --filter WriteArtifacts_ClassSeriesScaleThroughScalarMultipliers`
- ✅ Full `dotnet test --nologo`
- ✅ Telemetry/Loop integration suites (Phase 3 parity tests)

---

## Progress Log

### 2025-11-26 - Schema cross-links + manifest docs

**Changes:**
- Updated reference docs (`docs/reference/contracts.md`, `docs/reference/engine-capabilities.md`, `docs/reference/data-formats.md`) so telemetry manifest schema v2 + `supportsClassMetrics` requirements are linked outside the operations guide.
- Marked milestone spec as in progress; ensured tracking reflects completion of Phase 1 doc tasks.

**Tests:**
- (Docs only)

**Next Steps:**
- [ ] Start Phase 2 Task 2 by adding TelemetryLoader ingestion RED tests.
- [ ] Keep CLI/API references aligned as loader work progresses.

### 2025-11-26 - Telemetry loader RED coverage

**Changes:**
- Added `TelemetryLoaderByClassTests.BuildAsync_WithClassAwareBundle_WritesPerClassSeries` to pin expected by-class behavior for telemetry bundles.
- Created synthetic capture fixtures with manifest v2 (`supportsClassMetrics=true`) to drive ingestion work.

**Tests:**
- 🔴 `dotnet test tests/FlowTime.Generator.Tests/FlowTime.Generator.Tests.csproj --filter TelemetryLoaderByClassTests --nologo` (fails as expected; loader does not yet persist per-class series)

**Next Steps:**
- Implement loader + writer changes so per-class CSVs are ingested and class series files appear in run artifacts.

### 2025-11-26 - Telemetry loader ingestion (GREEN)

**Changes:**
- Extended `TelemetryManifest` + capture pipeline to emit schema v2 manifests with `supportsClassMetrics`.
- `TelemetryBundleBuilder` now preserves per-class CSV data and passes it to `RunArtifactWriter`, which learned to accept override class series.
- `TelemetryLoaderByClassTests` now GREEN, confirming telemetry ingests write class-specific CSVs into `series/index.json`.

**Tests:**
- ✅ `dotnet test tests/FlowTime.Generator.Tests/FlowTime.Generator.Tests.csproj --filter TelemetryLoaderByClassTests --nologo`

**Next Steps:**
- Surface CLI/log summaries for class coverage warnings (Phase 2 Task 2 REFACTOR).

### 2025-11-27 - Loop parity fixture + integration tests

**Changes:**
- Added the deterministic `loop-parity-template` fixture plus supporting telemetry capture helpers to drive two-class workloads during loop validation.
- Introduced `ClassesLoopTests` that orchestrate both simulation and telemetry runs, compare `/state_window` totals/byClass series, and ensure missing-class telemetry emits the correct warnings.
- Updated the telemetry bundler to stop overwriting topology semantics with capture URIs and to ignore class assignments for nodes without captured series, enabling canonical CSV emission (and `classCoverage` metadata) for telemetry runs.

**Tests:**
- ✅ `dotnet test tests/FlowTime.Integration.Tests/FlowTime.Integration.Tests.csproj --filter ClassesLoopTests --nologo`

**Next Steps:**
- Fold the new fixture into the milestone document + test plan and proceed with capture endpoint validations.

### 2025-11-27 - Telemetry capture endpoint validations

**Changes:**
- Fixed `RunArtifactReader` so telemetry capture bindings look through file-based semantics, normalize `file://...csv` references, and emit per-class bindings for every `(node, classId)` combination.
- `/v1/telemetry/captures` now ships class-aware bundles end-to-end; added `GenerateTelemetry_FromClassAwareRun_ReturnsClassMetadata` to assert that capture summaries and stored manifests report `supportsClassMetrics=true`/`classCoverage=full` with the expected class list.
- Kept legacy captures working by falling back to totals-only bindings while still writing schema v2 manifests.

**Tests:**
- ✅ `dotnet test tests/FlowTime.Api.Tests/FlowTime.Api.Tests.csproj --filter TelemetryCaptureEndpointsTests --nologo`
- ✅ `dotnet test --nologo`

**Next Steps:**
- Close out the milestone ceremony and sync docs once release notes are drafted.

### 2025-11-26 - Class coverage warnings + CLI summaries

**Changes:**
- Added ingestion validations that compare totals vs per-class sums and emit warnings when manifests declare classes but data is missing or mismatched.
- Both `flowtime telemetry bundle` and `flowtime telemetry run` now print class coverage summaries and merged warning lists, so operators see the contract status immediately.
- Updated capture guide to describe the new CLI output.

**Tests:**
- ✅ `dotnet test tests/FlowTime.Generator.Tests/FlowTime.Generator.Tests.csproj --filter TelemetryLoaderByClassTests --nologo`

### 2025-11-26 - Telemetry capture endpoint metadata

**Changes:**
- Extended `TelemetryGenerationService` to surface manifest summaries (supportsClassMetrics, classCoverage, classes) and propagate them through `/v1/telemetry/captures`.
- `TelemetryCaptureSummary` + UI DTOs now include class metadata so operators can confirm what the capture pipeline produced.
- Added API test coverage ensuring the capture response includes the new fields even for legacy single-class templates.

**Tests:**
- ✅ `dotnet test tests/FlowTime.Generator.Tests/FlowTime.Generator.Tests.csproj --filter TelemetryLoaderByClassTests --nologo`
- ✅ `dotnet test tests/FlowTime.Api.Tests/FlowTime.Api.Tests.csproj --filter TelemetryCaptureEndpointsTests --nologo`

### 2025-11-25 - Kickoff & Engine Backfill

**Changes:**
- Added RED regression (`WriteArtifacts_ClassSeriesScaleThroughScalarMultipliers`) covering scalar multiply/subtract class propagation.
- Updated `ClassContributionBuilder` multiply/min/max logic so per-class series survive capacity clamps and zeroed queues, keeping `classCoverage` truly `full`.
- Regenerated class-aware demo runs for supply chain (`run_20251125T130751Z_21597334`) and transportation (`run_20251125T130822Z_91deaced`).
- Documented progress + run IDs in `docs/milestones/CL-M-04.04.md`.

**Tests:**
- ✅ `dotnet test --filter WriteArtifacts_ClassSeriesScaleThroughScalarMultipliers`
- ⏳ Full build + test sweep (run after remaining changes).

**Next Steps:**
- [ ] Expand schema/docs for telemetry manifest updates (Phase 1 RED tests).
- [ ] Begin TelemetryLoader ingestion work (Phase 2 Task 2).
- [ ] Draft CLI/API validation plan for Phase 3.

**Blockers:**
- None — engine/class artifacts now emit data needed for telemetry loop work.

---

## Phase 1: Contract & Docs

**Goal:** Formalize manifest/schema/doc updates for class-aware telemetry.

**TDD Checklist:**
- [x] RED: `TelemetryManifestSchemaTests.ManifestSchema_Requires_ClassColumns_When_SupportsTrue`
- [x] RED: `TelemetryManifestSchemaTests.ManifestSchema_LegacyMode_AllowsTotalsOnly`
- [x] GREEN: Update manifest schema + `docs/operations/telemetry-capture-guide.md`
- [x] GREEN: Versioning + examples (`docs/schemas/telemetry-manifest.schema.json`, schema index)
- [x] REFACTOR: Ensure docs/reference pages link to the new schema

_Status:_ Schema + docs landed; final doc sweep (reference cross-links) still pending.

### 2025-12-01 - Telemetry Manifest Schema v2

**Changes:**
- Added RED tests (`TelemetryManifestSchemaTests`) asserting that manifests must declare `supportsClassMetrics`, require per-file `classId` entries when the flag is true, and still allow totals-only bundles when false.
- Bumped `docs/schemas/telemetry-manifest.schema.json` to schemaVersion **2** with the new `supportsClassMetrics` boolean, conditional `classes`/`classCoverage` requirements, and file-level `classId` enforcement.
- Updated `docs/operations/telemetry-capture-guide.md` and `docs/schemas/README.md` to describe the new manifest contract and provide sample JSON.

**Tests:**
- ✅ `dotnet test --filter FullyQualifiedName~TelemetryManifestSchemaTests --nologo`

---

## Phase 2: Engine + Telemetry Plumbing

**Goal:** Ensure engine artifacts + telemetry ingestion stay aligned on per-class data.

### Task 2.1: Engine artifact parity
- ✅ Regression test + `ClassContributionBuilder` fix ensure per-class CSVs exist even when values clamp to zero.
- ✅ Demo runs regenerated for downstream validation.

### Task 2.2: TelemetryLoader ingestion
- [x] RED: `TelemetryLoaderByClassTests.BuildAsync_WithClassAwareBundle_WritesPerClassSeries` (fails: loader drops per-class CSVs)
- [x] GREEN: Update loader + writer to ingest per-class CSVs, emit byClass series, and satisfy the new test.
- [x] REFACTOR: CLI/log summaries for class coverage after ingestion changes.

### Task 2.3: CLI summary/logging
- ⏳ Planned — update CLI output once ingestion work lands.

---

## Phase 3: Capture Endpoint & Loop Tests

**Goal:** Enforce telemetry contract server-side and validate loop parity.

_Status:_ Pending — will start after loader work is stable._

---

## Testing & Validation Checklist

- [x] Targeted regression for class propagation
- [ ] `dotnet build`
- [ ] `dotnet test --nologo`
- [ ] Telemetry ingestion unit tests
- [ ] API/Loop integration tests

---

## Notes

- Engine side fix unblocks telemetry ingestion by guaranteeing every node publishes per-class CSVs (no more `DEFAULT`-only queues).
- Keep regenerated runs handy for UI + telemetry loop comparisons; both labeled in `data/` for easy discovery.
