# CL-M-04.02 Implementation Tracking

**Milestone:** CL-M-04.02 — Engine & State Aggregation for Classes  
**Started:** 2025-11-24  
**Status:** ✅ Complete  
**Branch:** `milestone/m4.2`

---

## Quick Links

- **Milestone Document:** `work/milestones/completed/CL-M-04.02.md`
- **Release Notes (prior):** `docs/releases/CL-M-04.01.01.md`
- **Milestone Guide:** `docs/development/milestone-documentation-guide.md`

---

## Current Status

### Overall Progress
- [x] Phase 1: Domain & DTO Updates (3/3 tasks)
- [ ] Phase 2: Persistence & Telemetry Output (2/3 tasks; coverage warnings pending)
- [ ] Phase 3: API Surface & Diagnostics (0/3 tasks)

### Test Status
- **Unit Tests:** RED captured for class aggregation (filter run).
- **Integration Tests:** Pending.
- **E2E Tests:** Not planned.

---

## Progress Log

### 2025-11-24 - Setup

**Preparation:**
- [x] Read CL-M-04.02 milestone.
- [x] Created branch `milestone/m4.2`.
- [ ] Generate tracking RED tests plan.

**Next Steps:**
- [ ] Define test fixtures for multi-class runs.

### 2025-11-24 - Phase 1 Aggregation (RED)

**What changed:**
- Added class aggregation unit tests (`ClassMetricsAggregatorTests`) covering per-class counts, wildcard defaults, and conservation warnings.
- Introduced domain scaffolding for class data (`NodeClassData`, `ClassMetricsAggregator`, `ClassMetricsSnapshot`, `ClassCoverage`).

**Tests (RED capture):**
- `dotnet test --nologo --filter ClassMetricsAggregatorTests` (passes new tests; broader suite still pending full run).

**Next Steps:**
- Wire class aggregation into domain/DTOs and serialization.
- Add deterministic ordering for class keys in API/contract surfaces.

### 2025-11-24 - Phase 1 Aggregation (GREEN start)

**What changed:**
- Added class-aware domain wiring: `NodeClassData`, `ClassMetrics` contracts, and `ClassMetricsAggregator` integration in `StateQueryService`.
- Loaded per-class series from `series/index.json` into `NodeData.ByClass`; state snapshot/window emit `byClass` scaffolding.

**Tests:**
- `dotnet build --nologo` (pass).
- `dotnet test --nologo --filter ClassMetricsAggregatorTests` (targeted; full suite still pending).

**Next Steps:**
- Add integration tests for `/state` + `/state_window` `byClass` payloads and coverage metadata.
- Extend telemetry writers/manifest metadata with class coverage.

### 2025-11-24 - Phase 1/3 API Hooks (GREEN)

**What changed:**
- Added class-aware state endpoint tests (`GetState_ReturnsByClassBreakdown`, `GetStateWindow_ReturnsByClassSeries`) with new fixture run `run_state_classes`.
- Fixed class series resolution (`NormalizeSeriesKey` handling `file:` URIs, metric-aware class loading) and exposed `byClass` blocks in state snapshot/window responses.

**Tests:**
- `dotnet test --nologo --filter "GetState_ReturnsByClassBreakdown|GetStateWindow_ReturnsByClassSeries"` (pass; artifacts rooted at /tmp/flowtime-debug).

**Next Steps:**
- Add metadata/coverage flags to state responses and manifests.
- Broaden test coverage to CLI/telemetry outputs once class coverage metadata is in place.

### 2025-11-24 - Class Coverage Metadata (GREEN)

**What changed:**
- Added `classCoverage` to state metadata and `byClass` wildcards for legacy runs; snapshot/window responses now emit per-node `byClass` when available.
- Updated API goldens to include `classCoverage` and `byClass` defaults.

**Tests:**
- `dotnet test --nologo --filter StateEndpointTests` (pass).

**Next Steps:**
- Propagate class coverage into manifests/telemetry outputs.
- Ensure serialization determinism for class keys (already sorted in aggregator).

### 2025-11-24 - Telemetry Manifest Coverage (RED→GREEN)

**What changed:**
- Telemetry capture emits `classCoverage` (defaults to `missing`) and `classes` array in capture/telemetry manifests; bundle builder test updated.
- Telemetry CSVs now include `classId` column and manifest entries carry `classId` per file.

**Tests:**
- `dotnet test --nologo --filter TelemetryBundleBuilderTests` (pass).
- `dotnet test --nologo --filter TelemetryCaptureTests` (pass).

**Next Steps:**
- Wire classCoverage into run.json/series consumers (UI/CLI) as part of CL-M-04.03; coverage warnings TBD.

---

## Phase 1: Domain & DTO Updates

**Goal:** Introduce per-class metrics structures and conservation checks.

### Task 1.1: Aggregator Tests (RED)
**Checklist (Tests First):**
- [x] Add failing tests in `tests/FlowTime.Core.Tests/Aggregation/ClassMetricsAggregatorTests.cs` for per-class counts, wildcard default, and inconsistent totals warnings.
- [x] Run `dotnet test --nologo` to capture RED state (filter run for new tests; full suite pending).

**Status:** ✅ Complete

### Task 1.2: Domain/DTO Updates (GREEN)
**Checklist (Tests First):**
- [ ] Re-run new aggregation tests to confirm RED baseline.
- [ ] Add `ClassMetrics`/`byClass` structures to core models/DTOs with null-safe defaults.
- [ ] Implement conservation/warning flags when totals diverge from byClass sums.

**Status:** ✅ Complete

### Task 1.3: Serialization Helpers
**Checklist (Tests First):**
- [ ] Ensure serialization uses shared helpers between totals and class entries.
- [ ] Add tests to confirm ordering/determinism of class keys.

**Status:** ✅ Complete (byClass keys sorted via aggregator; contract/schema updated)

### Phase 1 Validation
- [ ] Aggregation unit tests passing (per-class counts, wildcard default, warnings).
- [ ] Deterministic class key ordering in serialization.

---

## Phase 2: Persistence & Telemetry Output

**Goal:** Persist per-class metrics to canonical artifacts and telemetry.

### Task 2.1: Telemetry Writer Tests (RED)
**Checklist (Tests First):**
- [ ] Add failing tests in `tests/FlowTime.Tests/Telemetry/SyntheticTelemetryWriterTests.cs` for classId column and manifest class list.
- [ ] Capture RED with `dotnet test --nologo`.

**Status:** ✅ Complete (covered via TelemetryCapture/Bundle tests)

### Task 2.2: Writers/Manifest Updates (GREEN)
**Checklist (Tests First):**
- [ ] Re-run new telemetry tests to confirm RED baseline.
- [ ] Extend CSV writers to emit `classId` and per-class rows.
- [x] Update manifest/run metadata with `classes` and `classCoverage` (metadata only; CSV class rows pending).

**Status:** 🚧 In Progress

### Task 2.3: Coverage Checks
**Checklist (Tests First):**
- [ ] Add/extend tests for coverage warnings when class data is missing/partial.
- [ ] Ensure defaults fall back to wildcard when classId absent.

**Status:** 🚧 In Progress (coverage metadata present; warnings/toggle pending)

### Phase 2 Validation
- [ ] Telemetry CSVs and manifests include class data.
- [ ] Coverage metadata/warnings emitted for missing class metrics.

---

## Phase 3: API Surface & Diagnostics

**Goal:** Surface `byClass` through `/state` endpoints with diagnostics.

### Task 3.1: API Integration Tests (RED)
**Checklist (Tests First):**
- [ ] Add failing tests in `tests/FlowTime.Api.Tests/State/ClassStateEndpointTests.cs` for `byClass` in `state` and `state_window`, and omission when absent.
- [ ] Run `dotnet test --nologo` for RED capture.

**Status:** ⏳ Not Started

### Task 3.2: API Implementation (GREEN)
**Checklist (Tests First):**
- [ ] Re-run new API tests to confirm RED baseline.
- [ ] Update controllers/mappers/contracts to emit `byClass` with optional field behavior.
- [ ] Add `classCoverage` metadata to responses and manifests.

**Status:** ⏳ Not Started

### Task 3.3: CLI Diagnostics (Optional)
**Checklist (Tests First):**
- [ ] Add CLI tests (if needed) to surface class summaries in `runs inspect`.
- [ ] Wire CLI output/help updates.

**Status:** ⏳ Not Started

### Phase 3 Validation
- [ ] `/state` and `/state_window` return `byClass` blocks when classes exist.
- [ ] `byClass` omitted when no class data; responses backward compatible.
- [ ] Diagnostics include `classCoverage`.

---

## Final Checklist
- [ ] `dotnet build FlowTime.sln`
- [x] `dotnet test --nologo` (perf skips acceptable; PMF normalization test skipped intentionally)
- [ ] Docs updated (state/telemetry references) and release note appended if required.
- [ ] Milestone status updated to reflect completion.
