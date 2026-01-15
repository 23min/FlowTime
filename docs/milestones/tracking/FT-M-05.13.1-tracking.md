# FT-M-05.13.1 Implementation Tracking

> **Note:** This tracking document is created when work begins on the feature branch.  
> Do not create this until you're ready to start implementation.  
> See [Milestone Rules](milestone-rules-quick-ref.md) for workflow.

**Milestone:** FT-M-05.13.1 — Class Filter Dimming Gap (Transportation Classes)  
**Started:** 2026-01-15  
**Status:** 📋 Planned  
**Branch:** `milestone/ft-m-05.13.1`  
**Assignee:** Codex

---

## Quick Links

- **Milestone Document:** `docs/milestones/FT-M-05.13.1-class-filter-dimming-gap.md`
- **Related Analysis:** N/A
- **Milestone Guide:** `docs/development/milestone-documentation-guide.md`

---

## Current Status

### Overall Progress
- [ ] Phase 1: Diagnosis + Tests (0/2 tasks)
- [ ] Phase 2: Fix + Validation (0/2 tasks)
- [ ] Phase 3: Regression + Docs (0/2 tasks)

### Test Status
- **Unit Tests:** 0 passing / 0 total
- **Integration Tests:** 0 passing / 0 total
- **E2E Tests:** 0 passing / 0 total

---

## Progress Log

### 2026-01-15 - Session Start

**Preparation:**
- [ ] Read milestone document
- [ ] Read related documentation
- [ ] Create milestone branch
- [ ] Verify templates and class series coverage

**Next Steps:**
- [ ] Begin Phase 1
- [ ] Start Task 1.1 (failing UI tests)

---

## Phase 1: Diagnosis + Tests

**Goal:** Reproduce the dimming gap and identify where class series coverage is missing.

### Task 1.1: Add failing UI tests
**File(s):** `tests/FlowTime.UI.Tests/TimeTravel/`

**Checklist (TDD Order - Tests FIRST):**
- [ ] Write UI test: `Topology_ClassFilter_AirportHighlightsLineAirport` (RED)
- [ ] Write UI test: `Topology_ClassFilter_AirportHighlightsAirport` (RED)
- [ ] Commit: `test(ui): add class filter dimming gap tests`

**Status:** ⏳ Not Started

---

### Task 1.2: Trace class series coverage
**File(s):** `templates/transportation-basic-classes.yaml`, `src/FlowTime.UI/Pages/TimeTravel/Topology.razor`

**Checklist (TDD Order - Tests FIRST):**
- [ ] Identify which derived series are DEFAULT-only
- [ ] Decide fix location (template series vs UI fallback)
- [ ] Commit: `docs: capture class coverage analysis`

**Status:** ⏳ Not Started

---

## Phase 2: Fix + Validation

**Goal:** Ensure class filtering respects class-specific series for derived airport legs.

### Task 2.1: Implement class series fix
**File(s):** `templates/transportation-basic-classes.yaml` and/or UI class filtering logic

**Checklist (TDD Order - Tests FIRST):**
- [ ] Implement class-aware series emission or fallback logic (GREEN)
- [ ] Update template docs if needed
- [ ] Commit: `fix(ui): honor class series for derived airport legs`

**Status:** ⏳ Not Started

---

### Task 2.2: Validate against other classes
**File(s):** `tests/FlowTime.UI.Tests/TimeTravel/`

**Checklist (TDD Order - Tests FIRST):**
- [ ] Add Downtown/Industrial coverage tests (GREEN)
- [ ] Run UI test suite
- [ ] Commit: `test(ui): extend class filter regression coverage`

**Status:** ⏳ Not Started

---

## Phase 3: Regression + Docs

**Goal:** Record the fix and confirm no regressions.

### Task 3.1: Docs alignment
**File(s):** `docs/templates/template-authoring.md` (if updated)

**Checklist (TDD Order - Tests FIRST):**
- [ ] Update docs to reflect class series guidance
- [ ] Commit: `docs: document class-series coverage for derived legs`

**Status:** ⏳ Not Started

---

### Task 3.2: Final validation
**Checklist (TDD Order - Tests FIRST):**
- [ ] `dotnet build`
- [ ] `dotnet test --nologo`
- [ ] Update tracking doc test status

**Status:** ⏳ Not Started

---

## Testing & Validation

### Test Case 1: Airport class filter highlights LineAirport
**Status:** ⏳ Not Started

**Steps:**
1. [ ] Run template `transportation-basic-classes`.
2. [ ] Select Airport class in the UI.
3. [ ] Verify LineAirport node is not dimmed.

**Expected:** LineAirport is highlighted for Airport class.

### Test Case 2: Airport class filter highlights Airport node
**Status:** ⏳ Not Started

**Steps:**
1. [ ] Run template `transportation-basic-classes`.
2. [ ] Select Airport class in the UI.
3. [ ] Verify Airport node is not dimmed.

**Expected:** Airport is highlighted for Airport class.
