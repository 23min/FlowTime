# FT-M-05.12 Implementation Tracking

**Milestone:** FT-M-05.12 — Metric Provenance & Audit Trail  
**Started:** 2026-01-12  
**Status:** 📋 Planned  
**Branch:** `milestone/ft-m-05.12`

---

## Quick Links

- **Milestone Document:** `docs/milestones/FT-M-05.12-metric-provenance.md`
- **Milestone Guide:** `docs/development/milestone-documentation-guide.md`

---

## Current Status

### Overall Progress
- [ ] Phase 1: Provenance Catalog + API Shape (0/2 tasks)
- [ ] Phase 2: Inspector UX (0/2 tasks)
- [ ] Phase 3: Bin Dump Enhancements (0/2 tasks)
- [ ] Phase 4: Docs + Validation (0/2 tasks)

### Test Status
- **Unit Tests:** 0 passing / 0 total
- **UI Tests:** 0 passing / 0 total

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

## Phase 1: Provenance Catalog + API Shape

**Goal:** Define the metric provenance catalog and map metric inputs deterministically.

### Task 1.1: Metric Provenance Catalog
**File(s):** `src/FlowTime.UI/Services/TimeTravelApiModels.cs`

**Checklist (TDD Order - Tests FIRST):**
- [ ] Write unit test: `MetricProvenanceCatalog_KindsHaveRequiredEntries` (RED)
- [ ] Implement catalog for nodeKind → metric mappings (GREEN)
- [ ] Refactor for clarity and reuse (REFACTOR)

**Status:** ⏳ Not Started

---

### Task 1.2: Missing Input Reporting
**File(s):** `src/FlowTime.UI/Services/TimeTravelApiModels.cs`

**Checklist (TDD Order - Tests FIRST):**
- [ ] Write unit test: `MetricProvenance_ReportsMissingInputs` (RED)
- [ ] Implement missing-input provenance reporting (GREEN)
- [ ] Refactor catalog lookup helpers (REFACTOR)

**Status:** ⏳ Not Started

---

## Phase 2: Inspector UX

**Goal:** Add expandable provenance details to inspector metric rows.

### Task 2.1: Inspector Expand/Collapse
**File(s):** `src/FlowTime.UI/Pages/TimeTravel/Topology.razor`

**Checklist (TDD Order - Tests FIRST):**
- [ ] Write UI test: `Inspector_ExpandsMetricProvenance` (RED)
- [ ] Implement expand/collapse affordance (GREEN)
- [ ] Refactor layout and styling (REFACTOR)

**Status:** ⏳ Not Started

---

### Task 2.2: Render Provenance Details
**File(s):** `src/FlowTime.UI/Pages/TimeTravel/Topology.razor`

**Checklist (TDD Order - Tests FIRST):**
- [ ] Extend `Inspector_ExpandsMetricProvenance` to assert formula/sources/units (RED)
- [ ] Render formula, inputs, gating rules, units (GREEN)
- [ ] Refactor UI helpers/components (REFACTOR)

**Status:** ⏳ Not Started

---

## Phase 3: Bin Dump Enhancements

**Goal:** Provide provenance in bin dumps and add modifier-key tab behavior.

### Task 3.1: Bin Dump Provenance Payload
**File(s):** `src/FlowTime.UI/Services/TimeTravelApiModels.cs`

**Checklist (TDD Order - Tests FIRST):**
- [ ] Write unit test to validate dump provenance payload shape (RED)
- [ ] Add provenance catalog slice to dump model (GREEN)
- [ ] Refactor serialization helpers (REFACTOR)

**Status:** ⏳ Not Started

---

### Task 3.2: Modifier Key Opens Tab
**File(s):** `src/FlowTime.UI/wwwroot/js/topologyCanvas.js`

**Checklist (TDD Order - Tests FIRST):**
- [ ] Write UI test: `BinDump_AltKeyOpensTab` (RED)
- [ ] Implement ALT/CTRL new-tab behavior (GREEN)
- [ ] Refactor event handling (REFACTOR)

**Status:** ⏳ Not Started

---

## Phase 4: Docs + Validation

**Goal:** Document provenance and validate end-to-end behavior.

### Task 4.1: Documentation
**File(s):** `docs/architecture/ui/metric-provenance.md`

**Checklist (TDD Order - Tests FIRST):**
- [ ] Document provenance UI and dump behavior
- [ ] Note gating rules and units semantics

**Status:** ⏳ Not Started

---

### Task 4.2: Build and Test

**Checklist:**
- [ ] `dotnet build` (no errors)
- [ ] `dotnet test --nologo` (all tests pass, perf benchmark skip warning expected)

**Status:** ⏳ Not Started
