# CL-M-04.03 Implementation Tracking

**Milestone:** CL-M-04.03 — UI Class-Aware Visualization  
**Started:** 2025-11-24  
**Status:** 🔄 In Progress  
**Branch:** `milestone/m4.3`

---

## Quick Links

- **Milestone Document:** `docs/milestones/CL-M-04.03.md`
- **Release Notes (prior):** `docs/releases/CL-M-04.02.md`
- **Milestone Guide:** `docs/development/milestone-documentation-guide.md`

---

## Current Status

### Overall Progress
- [x] Phase 1: Data Plumbing & Contracts (3/3 tasks)
- [x] Phase 2: UI Selector & KPIs (3/3 tasks)
- [ ] Phase 3: Diagnostics & Docs (3/4 tasks — class coverage verification pending)

### Test Status
- ✅ `dotnet build` (UI warnings unchanged: SimulationResults nullability, async `ArtifactDetail`, Topology inspector warnings).
- ✅ `dotnet test --nologo` — all suites green; perf + example conformance tests skipped by design in FlowTime.Tests/FlowTime.Sim.Tests.
- ➖ E2E browser automation deferred for Milestone 4 epic wrap; component + unit coverage expanded this milestone.

---

## Progress Log

### 2025-11-24 - Setup

**Preparation:**
- [x] Read CL-M-04.03 milestone.
- [x] Created branch `milestone/m4.3`.
- [x] Generate RED tests plan for UI class selector and run metadata ingestion.

**Next Steps:**
- Add RED tests for UI service/model ingestion of `classes`/`classCoverage`.
- Add RED tests for class selector behavior (All/single/multi) and KPI filtering.

### 2025-11-24 - Data plumbing

**TDD Notes:**
- RED: added `ClassStateIngestionTests` covering snapshot/window byClass + classCoverage fields (UI DTOs).
- GREEN: extended `TimeTravelApiModels` to surface `classCoverage`, `byClass`, and `TimeTravelClassMetricsDto`; reran filtered UI tests.

**Commands:**
- `dotnet test --nologo --filter ClassStateIngestionTests`
- `dotnet build FlowTime.sln`
- `dotnet test --nologo`

**Outcome:**
- Class-aware state DTO ingestion now green; remaining tasks focus on selector/filtering logic. Full test sweep clean (perf tests skipped by design).

### 2025-11-24 - Class selector state prep

**TDD Notes:**
- RED: added `ClassSelectorStateTests` targeting default/all/single/multi selection rules + deterministic ordering.
- GREEN: introduced `ClassSelectionState` helper with canonicalized class lists, max-three multi-select guard, and deterministic query serialization.

**Commands:**
- `dotnet test --nologo --filter ClassSelectorStateTests`

**Outcome:**
- Selector state machine ready for UI integration; next step is wiring component + URL sync using this helper.

### 2025-11-24 - Class selector component

**TDD Notes:**
- RED: added `ClassSelectorRenderTests` (Bunit) validating All/Single/Multi≤3 modes and query sync with the dashboard route.
- GREEN: implemented `ClassSelector` Razor component backed by `ClassSelectionState`, custom query parsing, and accessible chip buttons.

**Commands:**
- `dotnet test --nologo --filter ClassSelectorRenderTests`

**Outcome:**
- Selector widget now enforces max-three selections, persists URL state, and exposes hooks for downstream KPIs. Integration into dashboard/topology pages remains.

### 2025-11-24 - Class plumbing through artifacts/topology/dashboard

**TDD Notes:**
- RED→GREEN: `ArtifactListRenderTests.RunCardsSurfaceClassCoverage` verifies run cards show coverage text + class chips fed by discovery service metadata.
- RED→GREEN: `TopologyClassFilterTests` asserts dimming when selected class lacks volume and validates per-class success rate.
- `ClassSelectorRenderTests` rerun after wiring display names / auto-hydration.

**Implementation highlights:**
- Surfaced class inventory across discovery DTOs (`RunListEntry`), cards, and new CSS.
- Added `ClassSelector` + coverage banner to Topology/Dashboard headers; added class metadata parsing from `series/index.json`.
- Topology now aggregates `byClass` data for sparklines, inspector metrics, dimming, and CSV exports; dashboard re-computes SLA tiles from class-filtered state windows.

**Commands:**
- `dotnet test --nologo --filter "RunCardsSurfaceClassCoverage"`
- `dotnet test --nologo --filter "ClassSelectorRenderTests|TopologyClassFilterTests"`

**Outcome:**
- Class selection now flows from artifacts → dashboard → topology with deterministic query sync; per-class KPIs recalc client-side while nodes without class volume are dimmed.

### 2025-11-25 - Final polish, docs, and release prep

**TDD Notes:**
- Full `dotnet build` and `dotnet test --nologo` to validate UI/service changes; perf/conformance suites skipped (expected) per harness rules.

**Implementation highlights:**
- Added shared `.class-selector`/`.class-chip` styles plus JS helper `FlowTime.downloadText` wiring for CSV exports, ensuring selectors remain keyboard-accessible and downloads respect class filters.
- Updated milestone + UI docs to capture selector behavior, created release notes, and closed tracker tasks; ClassSelector now signals coverage text and inspector chips reference selected classes.

**Outcome:**
- CL-M-04.03 marked complete with documentation + release artifacts ready for handoff; clean build/test sweep recorded with known warnings noted.

### 2025-11-25 - Class Coverage Revalidation (RE-OPENED)

**Changes:**
- Removed premature release note (`docs/releases/CL-M-04.03.md`) and reset tracker/milestone status to 🔄 while we verify real runs.
- Regenerated class-enabled templates via Sim CLI + Engine CLI (post `ClassContributionBuilder` fix):
  - `data/run_20251125T155445Z_74e60979` ← `supply-chain-multi-tier-classes`.
  - `data/run_20251125T155501Z_0cc3f7e6` ← `transportation-basic-classes`.
- Confirmed `run.json` + `series/index.json` both report `classCoverage: "full"` for the regenerated runs (no per-component offenders via analyzer harness).
- Spot-checked downstream nodes called out as problematic:
  - Supply chain: `RETURNS_PROCESSED`, `RESTOCK_BACKLOG`, `RECOVER_BACKLOG`, `SCRAP_BACKLOG`, and `SUPPLIER_SHORTFALL_DEPTH` each emit Retail/Wholesale/Subscription CSVs.
  - Transportation: `HUB_QUEUE_DEPTH`, `HUB_LOSS_DEPTH`, `HUB_QUEUE_CARRY_RAW`, and `AIRPORT_DLQ_DEPTH` include Airport/Industrial/Downtown series with sums matching totals (post add/sub normalization).
- Added instrumentation inside `StateQueryService` to log node-level class coverage diagnostics so we can see which nodes (if any) still surface `class_totals_mismatch` once the API runs through the new artifacts.

**Next Steps:**
- [ ] Wire these new runs into manual UI validation (Topology banner must show “Class coverage: Full” for both templates).
- [ ] Run the class-coverage analyzer harness against each run and capture the output in this tracker (pass/fail + offending nodes, if any). ✅ Analyzer PASS recorded for runs above.
- [ ] Monitor API logs (new instrumentation) while loading the runs to pinpoint any nodes still reporting `class_totals_mismatch`, then address root cause.
- [ ] Keep class coverage task (3.4) open until stakeholder confirms the UI matches expectations.

---

### 2025-11-25 - Run discovery metadata fix

**TDD Notes:**
- RED: tightened `FileSeriesReaderTests` to expect `classCoverage` + `classes` from both `run.json` and `series/index.json`.
- GREEN: updated `FileSeriesReader` to parse class coverage + manifest classes so `/v1/runs/{id}/index` responses include metadata, allowing run cards to display real class counts.

**Commands:**
- `dotnet test --nologo --filter FileSeriesReaderTests`

**Outcome:**
- API index endpoint now emits class lists + coverage, so `RunDiscoveryService` populates `RunListEntry.Classes` and run cards show the correct “Classes: N” value.

---

## Phase 1: Data Plumbing & Contracts

**Goal:** Surface class inventory/coverage to UI services and DTOs.

### Task 1.1: Run Metadata Ingestion (RED)
**Checklist (Tests First):**
- [x] Add failing tests ensuring UI client models capture `classes` and `classCoverage` from run metadata.
- [x] Run `dotnet test --nologo` filtered to UI service tests to capture RED.

**Status:** ✅ Completed

### Task 1.2: State API Consumption (GREEN)
**Checklist (Tests First):**
- [x] Re-run new tests to confirm RED baseline.
- [x] Update UI API client DTOs to include `byClass` and `classCoverage`.
- [x] Ensure null-safe defaults for legacy runs.

**Status:** ✅ Completed

### Task 1.3: Ordering/Determinism
**Checklist (Tests First):**
- [x] Add tests to ensure class lists are sorted deterministically for UI selectors.
- [x] Wire helper to reuse ordering across UI components.

**Status:** ✅ Completed

### Phase 1 Validation
- [x] UI models expose classes + coverage; legacy runs remain compatible.
- [x] Deterministic ordering of class options.

---

## Phase 2: UI Selector & KPIs

**Goal:** Enable class selection and class-aware KPIs/graphs in UI.

### Task 2.1: Selector & Navigation (RED)
**Checklist (Tests First):**
- [x] Add failing Blazor component tests for class selector (All/single/multi≤3) with URL query sync.
- [x] Run targeted UI tests to confirm RED.

**Status:** ✅ Completed

### Task 2.2: KPI/Graph Filtering (GREEN)
**Checklist (Tests First):**
- [x] Re-run selector tests to confirm RED baseline.
- [x] Apply class filter to node KPIs, sparklines, and overlays; fallback to totals when no class selected.

**Status:** ✅ Completed

### Task 2.3: Inspector Chips & Accessibility
**Checklist (Tests First):**
- [x] Add tests for inspector chips showing per-class metrics (`TopologyClassFilterTests` extended).
- [x] Verify keyboard navigation/ARIA labels for selector (`ClassSelectorRenderTests` covers aria-pressed + query sync).

**Status:** ✅ Completed

### Phase 2 Validation
- [x] Selector works with URL sync and accessibility.
- [x] KPIs/graphs respect selected classes; defaults to totals when none.

---

## Phase 3: Diagnostics & Docs

**Goal:** Document class-aware UI behavior and ensure diagnostics surface coverage state.

### Task 3.1: Diagnostics Hooks (RED)
**Checklist (Tests First):**
- [x] Add failing tests/log hooks for class coverage display in run summary card (`ArtifactListRenderTests.RunCardsSurfaceClassCoverage`).
- [x] Confirm warning surfacing when class data is missing/partial (Run cards + Topology coverage text fallback).

**Status:** ✅ Completed

### Task 3.2: Documentation (GREEN)
**Checklist (Tests First):**
- [x] Update UI docs (run summary, selector usage, coverage meanings).
- [x] Add release note snippet via `docs/releases/CL-M-04.03.md`.

**Status:** ✅ Completed

### Task 3.3: Final Validation
**Checklist (Tests First):**
- [x] Run full `dotnet test --nologo` (perf skips acceptable; logged above).
- [x] Update tracking doc to Done.

**Status:** ✅ Completed

### Task 3.4: Template Class Coverage Verification (RE-OPENED)
**Checklist (TDD Order - Tests/Validations FIRST):**
- [x] Supply-chain template (`supply-chain-multi-tier-classes.yaml`) run produces `classCoverage: "full"` and downstream nodes (ReturnsProcessing, SupplierShortfallQueue, etc.) expose per-class CSVs.
- [x] Transportation template (`transportation-basic-classes.yaml`) run produces `classCoverage: "full"` with per-class metrics beyond HubQueue (Airport DLQ, HubLossQueue, etc.).
- [ ] Update tracker + docs once both runs verified in UI (Topology banner shows “Class coverage: Full”).

**Status:** ⏳ Not Started

### Phase 3 Validation
- [x] Coverage state visible in UI summary (Run cards + Topology coverage text render even when selector disabled).
- [x] Docs updated for class-aware UI (`docs/ui/time-travel-visualizations-3.md`, milestone + release notes).

---

## Final Checklist
- [x] `dotnet build FlowTime.sln`
- [x] `dotnet test --nologo` (perf skips acceptable)
- [ ] Docs updated + release note ready for GA (pending class coverage verification; release doc removed until approval).
- [ ] Milestone status updated to ✅ Complete (will flip once class coverage confirmed by stakeholder).
