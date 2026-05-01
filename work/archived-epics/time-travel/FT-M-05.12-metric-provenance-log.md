# FT-M-05.12 Implementation Tracking

**Milestone:** FT-M-05.12 — Metric Provenance & Audit Trail  
**Started:** 2026-01-12  
**Status:** ✅ Complete  
**Branch:** `milestone/ft-m-05.12`

---

## Quick Links

- **Milestone Document:** `work/epics/completed/time-travel/FT-M-05.12-metric-provenance.md`
- **Milestone Guide:** `docs/development/milestone-documentation-guide.md`

---

## Current Status

### Overall Progress
- [x] Phase 1: Provenance Catalog + API Shape (2/2 tasks)
- [x] Phase 2: Inspector UX (2/2 tasks)
- [x] Phase 3: Bin Dump Enhancements (2/2 tasks)
- [x] Phase 4: Docs + Validation (2/2 tasks)

### Test Status
- ✅ `dotnet build`
- ✅ `dotnet test --nologo` (perf benchmark skips expected)

---

## Progress Log

### 2026-01-12 - Session Start

**Preparation:**
- [x] Read milestone document
- [x] Create milestone branch
- [x] Create tracking document

**Next Steps:**
- [ ] Begin Phase 1: Provenance Catalog + API Shape
- [ ] Start with RED tests in the Test Plan

---

### 2026-01-12 - Phase 1-3 Implementation

**Changes:**
- Added metric provenance catalog and missing-input evaluation.
- Added inspector provenance expanders and bin dump provenance bundle.
- Added modifier-key bin dump new-tab behavior.

**Tests:**
- ✅ `dotnet build`
- ✅ `dotnet test --nologo` (perf benchmark skip warnings expected)

**Next Steps:**
- [ ] Phase 4: Docs + Validation

---

### 2026-01-12 - Docs Update

**Changes:**
- Updated metric provenance architecture notes for inspector and bin dump behavior.

**Next Steps:**
- [ ] Ready for review and commit guidance

---

### 2026-01-12 - Inspector Tooltip Refinement

**Changes:**
- Replaced inspector provenance expanders with tooltips on metric labels.
- Updated tests and hooks for tooltip strings.

---

### 2026-01-12 - Properties Tooltip Coverage

**Changes:**
- Added provenance tooltips to inspector Properties rows.
- Added properties tooltip test coverage and updated docs.

**Tests:**
- ✅ `dotnet build`
- ❌ `dotnet test --nologo` (failed: `FlowTime.Tests.Performance.M2PerformanceTests.Test_PMF_Mixed_Workload_Performance`)

---

### 2026-01-12 - Provenance Popup Styling

**Changes:**
- Styled provenance popups to match the topology tooltip look for properties and chart labels.

**Tests:**
- ✅ `dotnet build`
- ✅ `dotnet test --nologo` (perf benchmark skips expected)

---

### 2026-01-12 - Popup Outline Alignment

**Changes:**
- Updated provenance popups to match node tooltip outline, colors, and two-column layout.

**Tests:**
- ✅ `dotnet build`
- ❌ `dotnet test --nologo` (failed: `FlowTime.Tests.Performance.M2PerformanceTests.Test_PMF_Mixed_Workload_Performance`)

---

### 2026-01-12 - Tooltip Visual Match

**Changes:**
- Forced provenance popups to use the same dark outline, colors, and grid layout as canvas node tooltips.

**Tests:**
- ✅ `dotnet build`
- ✅ `dotnet test --nologo` (perf benchmark skips expected)

---

### 2026-01-12 - Metric Meaning Definitions

**Changes:**
- Added metric meaning definitions to provenance catalog and surfaced meaning in tooltips.
- Removed duplicate title/subtitle from provenance popups.
- Updated metric provenance UI documentation to mention meanings.

**Tests:**
- ✅ `dotnet build`
- ⚠️ `dotnet test --nologo` (timed out after 240s; output reported passing tests with perf skips)
- ⚠️ `dotnet test --nologo --no-build` (timed out after 180s; output reported passing tests with perf skips)
- ⚠️ `dotnet test --nologo --no-build tests/FlowTime.Api.Tests/FlowTime.Api.Tests.csproj` (timed out after 120s; no completion summary)

---

### 2026-01-12 - Latency Meaning Clarifications

**Changes:**
- Updated latency/service meanings to call out per-bin averages.
- Ensured Queue latency row always renders with "-" when unavailable.
- Documented percentile guidance for telemetry latency series.

**Tests:**
- ✅ `dotnet build`
- ✅ `dotnet test --nologo` (perf benchmark skips expected)

---

### 2026-01-12 - Router Latency Suppression

**Changes:**
- Suppressed latency/service/flow rows for router nodes in the inspector.
- Clarified service latency meaning as source-defined and documented service-node semantics.

**Tests:**
- Not run (not requested).

---

### 2026-01-12 - Service Latency Suppression

**Changes:**
- Suppressed latency rows for service nodes in the inspector (queue latency only shown for queue/serviceWithBuffer).

**Tests:**
- Not run (not requested).

---

### 2026-01-12 - Focus Label Clearing

**Changes:**
- Cleared focus labels when selected basis has no data to avoid stale values.
- Added canvas render test for focus label clearing on basis change.

**Tests:**
- Not run (not requested).

---

### 2026-01-12 - Inspector Dark Mode Styling

**Changes:**
- Ensured expanded inspector charts use dark-mode background in all theme selectors.
- Matched property-row provenance popups to the node tooltip style in light mode.

**Tests:**
- Not run (not requested).

---

### 2026-01-12 - Inspector Popup Alignment

**Changes:**
- Reverted provenance tooltip styling to the original property-row look and removed Mud wrapper padding so chart-title popups match.

**Tests:**
- Not run (not requested).

---

### 2026-01-12 - Arrivals Focus + SLA Sparkline Alignment

**Tests (RED):**
- Added `BuildInspectorMetrics_RouterNode_IncludesArrivalsWhenSeriesPresent` in `tests/FlowTime.UI.Tests/TimeTravel/TopologyInspectorTests.cs`.

**Changes:**
- Added Arrivals focus chip and legend entry.
- Made SLA mini-sparklines prefer `successRate` and added Arrivals basis handling.
- Added Arrivals chart support for router nodes in the inspector.

**Tests:**
- Not run (not requested).

---

### 2026-01-12 - Chart Title Popup Matching

**Changes:**
- Replaced chart-title and horizon label MudTooltips with the same hover flyout used by property rows.
- Ensured chart-title popups now match property-row styling exactly.

**Tests:**
- Not run (not requested).

---

### 2026-01-12 - Sparkline Flat Baseline

**Changes:**
- Fixed node sparklines to keep flat series at the baseline (0% error rate no longer renders at the top).

**Tests:**
- Not run (not requested).

---

### 2026-01-12 - Node Tooltip Width + Router Badge

**Changes:**
- Added minimum width for node tooltips to avoid inspector toggle overlap, with a canvas-safe max width cap.
- Added a router chip aligned with arrivals to distinguish router kinds at a glance.

**Tests:**
- Not run (not requested).

---

### 2026-01-12 - Canvas Bin Stabilization

**Changes:**
- Decoupled canvas render bin from immediate scrub selection to prevent color flicker when metrics lag bin changes.

**Tests:**
- Not run (not requested).

---

## Phase 1: Provenance Catalog + API Shape

**Goal:** Define the metric provenance catalog and map metric inputs deterministically.

### Task 1.1: Metric Provenance Catalog
**File(s):** `src/FlowTime.UI/Services/MetricProvenanceCatalog.cs`

**Checklist (TDD Order - Tests FIRST):**
- [x] Write unit test: `MetricProvenanceCatalog_KindsHaveRequiredEntries` (RED)
- [x] Implement catalog for nodeKind → metric mappings (GREEN)
- [x] Refactor for clarity and reuse (REFACTOR)

**Status:** ✅ Complete

---

### 2026-01-12 - Milestone Wrap

**Ready to Wrap:**
- [x] Documentation sweep complete (milestone spec + UI provenance doc updated).
- [x] Release note drafted (`docs/releases/FT-M-05.12.md`).
- [x] Tests captured in tracking log.

**Wrap Actions:**
- [x] Moved milestone spec to `work/epics/completed/time-travel/`.
- [x] Updated milestone references and quick links.
- [x] Marked milestone status ✅ Complete.

---

### 2026-01-12 - UI/Engine Parity Test

**Changes:**
- Added inspector parity test that compares selected-bin values against window series.
- Documented the validation loop in the provenance UI architecture notes.

**Tests:**
- ✅ `dotnet build`
- ✅ `dotnet test --nologo` (perf benchmark skips expected)

---

### 2026-01-12 - Expanded UI/Engine Parity Coverage

**Changes:**
- Added parity tests across service, serviceWithBuffer, queue, router, sink, dlq, expr/const/pmf, and class selection.
- Added SLA status + missing-series placeholder checks.

**Tests:**
- ✅ `dotnet build`
- ✅ `dotnet test --nologo` (perf benchmark skips expected)

---

### Task 1.2: Missing Input Reporting
**File(s):** `src/FlowTime.UI/Services/MetricProvenanceCatalog.cs`

**Checklist (TDD Order - Tests FIRST):**
- [x] Write unit test: `MetricProvenance_ReportsMissingInputs` (RED)
- [x] Implement missing-input provenance reporting (GREEN)
- [x] Refactor catalog lookup helpers (REFACTOR)

**Status:** ✅ Complete

---

## Phase 2: Inspector UX

**Goal:** Add expandable provenance details to inspector metric rows.

### Task 2.1: Inspector Expand/Collapse
**File(s):** `src/FlowTime.UI/Pages/TimeTravel/Topology.razor`

**Checklist (TDD Order - Tests FIRST):**
- [x] Write UI test: `Inspector_ExpandsMetricProvenance` (RED)
- [x] Implement expand/collapse affordance (GREEN)
- [x] Refactor layout and styling (REFACTOR)

**Status:** ✅ Complete

---

### Task 2.2: Render Provenance Details
**File(s):** `src/FlowTime.UI/Pages/TimeTravel/Topology.razor`

**Checklist (TDD Order - Tests FIRST):**
- [x] Extend `Inspector_ExpandsMetricProvenance` to assert formula/sources/units (RED)
- [x] Render formula, inputs, gating rules, units (GREEN)
- [x] Refactor UI helpers/components (REFACTOR)

**Status:** ✅ Complete

---

## Phase 3: Bin Dump Enhancements

**Goal:** Provide provenance in bin dumps and add modifier-key tab behavior.

### Task 3.1: Bin Dump Provenance Payload
**File(s):** `src/FlowTime.UI/Pages/TimeTravel/Topology.razor`

**Checklist (TDD Order - Tests FIRST):**
- [x] Write unit test to validate dump provenance payload shape (RED)
- [x] Add provenance catalog slice to dump model (GREEN)
- [x] Refactor serialization helpers (REFACTOR)

**Status:** ✅ Complete

---

### Task 3.2: Modifier Key Opens Tab
**File(s):** `src/FlowTime.UI/wwwroot/js/topologyCanvas.js`, `src/FlowTime.UI/wwwroot/js/theme.js`

**Checklist (TDD Order - Tests FIRST):**
- [x] Write UI test: `BinDump_AltKeyOpensTab` (RED)
- [x] Implement ALT/CTRL new-tab behavior (GREEN)
- [x] Refactor event handling (REFACTOR)

**Status:** ✅ Complete

---

## Phase 4: Docs + Validation

**Goal:** Document provenance and validate end-to-end behavior.

### Task 4.1: Documentation
**File(s):** `work/epics/ui/metric-provenance.md`

**Checklist (TDD Order - Tests FIRST):**
- [x] Document provenance UI and dump behavior
- [x] Note gating rules and units semantics

**Status:** ✅ Complete

---

### Task 4.2: Build and Test

**Checklist:**
- [x] `dotnet build` (no errors)
- [x] `dotnet test --nologo` (all tests pass, perf benchmark skip warning expected)

**Status:** ✅ Complete
