# TT-M-03.30.3 Implementation Tracking

> **Note:** This tracking document is created when work begins on the feature branch.  
> Do not create this until you're ready to start implementation.  
> See [Milestone Rules](../development/milestone-rules-quick-ref.md) for workflow.

**Milestone:** TT-M-03.30.3 — PMF Time-of-Day Profiles  
**Started:** 2025-11-17  
**Status:** 🔄 In Progress  
**Branch:** `feature/tt-m-0330-1-domain-aliases`  
**Assignee:** [Unassigned]

---

## Quick Links

- **Milestone Document:** [`docs/milestones/TT-M-03.30.3.md`](../TT-M-03.30.3.md)
- **Related Analysis:** [None recorded]
- **Milestone Guide:** [`docs/development/milestone-documentation-guide.md`](../development/milestone-documentation-guide.md)

---

## Current Status

### Overall Progress
- [x] Phase 1: Engine & Profiles (2/2 tasks)
- [x] Phase 2: Templates & Docs (3/3 tasks)
- [ ] Phase 3: UI & Analyzer polish (2/3 tasks)

### Test Status
- **Unit/UI Tests:** Failing: UI Topology inspector (metric stack ordering after flow latency). Dashboard sparkline passes.
- **API Tests:** Failing: state/schema goldens (`flowLatencyMs` + bin metadata) and orchestration goldens (`plan`/timestamps). Queue-latency-null test needs update.
- **Integration Tests:** Existing suites pass (perf flakes when running full PMF perf tests)
- **E2E Tests:** Manual UI verification pending

---

## Progress Log

### 2025-11-17 - Session Start

**Preparation:**
- [x] Read milestone document
- [ ] Read related documentation
- [x] Create feature branch
- [x] Verify dependencies (services, tools, etc.)

**Next Steps:**
- [x] Begin Phase 1
- [x] Start with first task

---

### 2025-11-18 - Profiles, Analyzer Infos, UI surfacing

**Changes:**
- Implemented profile expansion and kept deterministic PMF outputs.
- Analyzer now emits info-level warnings for missing capacity/served/queue metrics; severity flows to telemetry manifest and state window.
- UI renders info chips (blue) at scrubber and nodes; PMF tooltips/inspector show profile metadata.
- Styled info chip/tooltip; improved dark-mode node chip fills.

**Tests:**
- ✅ `dotnet build FlowTime.sln`
- ⚠️ `dotnet test FlowTime.sln` — perf benchmarks flaky; focused PMF perf tests pass (`FlowTime.Tests.Performance.M2PerformanceTests.*` filters).
- ✅ Added generator test to ensure run warnings propagate into telemetry manifest.

**Commits:**
- [multiple] (severity propagation, info chip rendering, tooltip styling, generator test)

**Next Steps:**
- [ ] Finalize scrubber tooltip border to match node tooltips visually.
- [ ] Re-verify node info chips after UI restart/run regeneration.
- [ ] Update API goldens and schema for flow latency; fix queue-latency-null regression.
- [ ] Update inspector metric stack expectation to include flow latency before error rate.

**Blockers:**
- Perf benchmark tests flaky when run in full suite.

### 2025-11-19 - Template capacity/utilization wiring

**Changes:**
- Added capacity semantics for all service nodes across templates so utilization is computable: wiring supplier/warehouse/load balancer/auth/database/origin/support/etc. to their capacity series and introducing edge/support analytics capacity PMFs where missing.

**Tests:**
- ✅ `dotnet build FlowTime.sln`
- ⚠️ `dotnet test FlowTime.sln` — API golden baselines now need updates for added info warnings and flow latency; UI inspector stack expectation failing.

**Next Steps:**
- [ ] Refresh API golden files to account for info-level warnings and flow latency.
- [ ] Visual QA: confirm utilization now shows for all operational nodes.

**Blockers:**
- None.

---

## Phase 1: Engine & Profiles

**Goal:** PMF profile support and deterministic time-of-day scaling.

### Task 1.1: Profile Expansion in Sim
**File(s):** `src/FlowTime.Sim.Core/Templates/SimModelBuilder.cs`, `src/FlowTime.Core/Pmf/PmfNode.cs`

**Checklist (TDD Order - Tests FIRST):**
- [ ] Write unit test: `Test_Profile_Expansion_Generates_Profiled_Series`
- [x] Implement profile multiplication of PMF expectations
- [x] Commit

**Commits:**
- [x] Implemented in prior sessions (hashes in git log)

**Tests:**
- [ ] Add targeted unit test

**Status:** ✅ Complete (test backfill pending)

### Task 1.2: Analyzer Severity Propagation
**File(s):** `src/FlowTime.Core/Analysis/InvariantAnalyzer.cs`, `src/FlowTime.Core/Artifacts/RunArtifactWriter.cs`, `src/FlowTime.API/Services/StateQueryService.cs`

**Checklist:**
- [x] Emit info-level warnings for missing capacity/served/queue
- [x] Preserve severity into run.json and telemetry manifest
- [x] Expose severity to state window/UI

**Status:** ✅ Complete

### Phase 1 Validation

**Smoke Tests:**
- [x] Build solution (no compilation errors)
- [ ] Run unit tests (all passing; perf flakes noted)

**Success Criteria:**
- [x] Profiles applied deterministically across 288-bin grid
- [x] Analyzer surfaces missing metric infos

---

## Phase 2: Templates & Docs

**Goal:** Apply profiles to templates and document feature.

### Task 2.1: Template Updates
**File(s):** `templates/*.yaml`

**Checklist:**
- [x] Apply profile refs to PMF nodes per domain
- [x] Ensure 24h×5m grids consistent

**Status:** ✅ Complete

### Task 2.2: Docs
**File(s):** `docs/milestones/TT-M-03.30.3.md`, `docs/templates/profiles.md`

**Checklist:**
- [x] Document profile library and usage

**Status:** ✅ Complete

### Task 2.3: Tests
**File(s):** `tests/FlowTime.Generator.Tests/RunOrchestrationServiceTests.cs`

**Checklist:**
- [x] Add test to ensure run warnings propagate to telemetry manifest

**Status:** ✅ Complete

### Phase 2 Validation

**Smoke Tests:**
- [x] Build solution
- [ ] Manual review of template curves (pending visual QA)

**Success Criteria:**
- [x] Templates produce profiled series
- [x] Docs updated

---

## Phase 3: UI & Analyzer Polish

**Goal:** Surface profiles/infos in UI; polish visuals.

### Task 3.1: UI Warning/Info Chips
**File(s):** `src/FlowTime.UI/wwwroot/js/topologyCanvas.js`, `src/FlowTime.UI/Components/Topology/TopologyCanvasModels.cs`

**Checklist:**
- [x] Render info severity at scrubber and nodes
- [x] Use blue palette for info chips

**Status:** ✅ Complete

### Task 3.2: Tooltip Styling
**File(s):** `src/FlowTime.UI/wwwroot/css/app.css`, `src/FlowTime.UI/Pages/TimeTravel/Topology.razor`

**Checklist:**
- [x] Align scrubber tooltip styling with node tooltips (thin outline, no shadow, no arrow)
- [ ] Final visual QA (border thickness)

**Status:** 🔄 In Progress

### Task 3.3: Profile Surfacing in UI
**File(s):** PMF node rendering/tooltips

**Checklist:**
- [x] Show profile name/kind in PMF tooltips/inspector

**Status:** ✅ Complete

### Phase 3 Validation

**Smoke Tests:**
- [ ] Visual QA: info chips appear beside nodes; tooltips consistent
- [ ] Manual check: scrubber tooltip border matches node tooltip

**Success Criteria:**
- [ ] Infos visibly distinct from warnings across UI
- [ ] Tooltip styling consistent light/dark

---

## Testing & Validation

### Test Case 1: Info Warning Surfacing
**Status:** 🔄 In Progress

**Steps:**
1. Generate new run in simulation mode with missing capacity on a service node.
2. Load topology page; hover scrubber chip.
3. Hover affected node.

**Expected:**
- Scrubber shows blue Info chip + thin-outline tooltip.
- Node shows blue Info chip; tooltip shows warning text.

**Actual:**
- Scrubber Info chip OK; tooltip border still visually thick (needs CSS polish).
- Node info chips render after severity propagation (verify visually).

**Result:** 🔄 Pending final visual QA

### Test Case 2: Manifest Warning Propagation
**Status:** ✅ Pass

**Steps:**
1. Run generator test `CreateRunAsync_SimulationMode_CarriesRunWarningsIntoTelemetryManifest`.
2. Inspect manifest warnings.

**Expected:** Warning present with severity=info.

**Actual:** Pass.

**Result:** ✅ Pass

---

## Issues Encountered

### Issue 1: Perf Benchmark Flakiness
**Encountered:** 2025-11-18  
**Severity:** Medium

**Description:** PMF performance benchmarks occasionally exceed thresholds in full `dotnet test` runs.

**Impact:** Full suite red; focused perf tests pass.

**Resolution:** Run perf tests with filters when validating milestone work; monitor for future tuning.

**Status:** Open

### Issue 2: Scrubber Tooltip Border Thickness
**Encountered:** 2025-11-18  
**Severity:** Low

**Description:** Scrubber Info tooltip still appears thicker than node tooltips.

**Impact:** Visual inconsistency.

**Resolution:** Ongoing CSS adjustments; further QA required.

**Status:** Open

---

## Final Checklist

### Code Complete
- [ ] All phase tasks complete
- [ ] All tests passing
- [ ] No compilation errors
- [ ] No console warnings
- [ ] Code reviewed (if applicable)

### Documentation
- [ ] Milestone document updated (status → ✅ Complete)
- [ ] ROADMAP.md updated
- [ ] Release notes entry created
- [ ] Related docs updated

### Quality Gates
- [ ] All unit tests passing
- [ ] All integration tests passing
- [ ] Manual E2E tests passing
- [ ] Performance acceptable
- [ ] No regressions
