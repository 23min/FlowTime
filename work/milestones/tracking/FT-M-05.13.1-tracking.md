# FT-M-05.13.1 Implementation Tracking

> **Note:** This tracking document is created when work begins on the feature branch.  
> Do not create this until you're ready to start implementation.  
> See [Milestone Rules](milestone-rules-quick-ref.md) for workflow.

**Milestone:** FT-M-05.13.1 — Class Filter Dimming Gap (Transportation Classes)  
**Started:** 2026-01-15  
**Status:** ✅ Complete  
**Branch:** `milestone/ft-m-05.13.1`  
**Assignee:** Codex

---

## Quick Links

- **Milestone Document:** `work/milestones/completed/FT-M-05.13.1-class-filter-dimming-gap.md`
- **Related Analysis:** N/A
- **Milestone Guide:** `docs/development/milestone-documentation-guide.md`

---

## Current Status

### Overall Progress
- [x] Phase 1: Diagnosis + Tests (2/2 tasks)
- [x] Phase 2: Fix + Validation (2/2 tasks)
- [x] Phase 3: Regression + Docs (2/2 tasks)

### Test Status
- **Unit Tests:** `dotnet test --nologo` (pass; perf tests skipped)
- **Integration Tests:** Included in full run (pass)
- **E2E Tests:** N/A

---

## Progress Log

### 2026-01-15 - Session Start

**Preparation:**
- [x] Read milestone document
- [x] Read related documentation
- [x] Create milestone branch
- [x] Verify class propagation path for serviceWithBuffer semantics

**Next Steps:**
- [x] Begin Phase 1
- [x] Start Task 1.1 (failing core tests)

### 2026-01-15 - Wrap

**Validation:**
- [x] `dotnet build`
- [x] `dotnet test --nologo` (perf tests skipped as expected)

**Notes:**
- Class coverage warnings appear for DLQ outflow/loss as expected; LineAirport dimming was due to current-bin zero values.

---

## Phase 1: Diagnosis + Tests

**Goal:** Reproduce the dimming gap and identify where class series coverage is missing.

### Task 1.1: Add failing core tests
**File(s):** `tests/FlowTime.Core.Tests/`

**Checklist (TDD Order - Tests FIRST):**
- [x] Write test: `ClassContributionBuilder_PropagatesServiceWithBufferTopologyClasses` (RED)
- [x] Write test: `InvariantAnalyzer_WarnsOnTopologyClassCoverageGaps` (RED)
- [x] Commit: `fix(core): propagate class series via topology semantics` (`3c2c2f8`)

**Status:** ✅ Complete

---

### Task 1.2: Trace class series coverage
**File(s):** `src/FlowTime.Core/Artifacts/ClassContributionBuilder.cs`, `src/FlowTime.Core/Analysis/InvariantAnalyzer.cs`

**Checklist (TDD Order - Tests FIRST):**
- [x] Identify class propagation gaps for topology semantics
- [x] Confirm analyzer warning codes and targets
- [x] Commit: `docs: align epic roadmap and milestone status` (`7b4676c`)

**Status:** ✅ Complete

---

## Phase 2: Fix + Validation

**Goal:** Ensure class filtering respects class-specific series for derived airport legs.

### Task 2.1: Implement class series fix
**File(s):** `src/FlowTime.Core/Artifacts/ClassContributionBuilder.cs`

**Checklist (TDD Order - Tests FIRST):**
- [x] Implement topology-based propagation for served/errors (GREEN)
- [x] Commit: `fix(core): propagate class series for topology serviceWithBuffer` (`3c2c2f8`)

**Status:** ✅ Complete

---

### Task 2.2: Add analyzer validation
**File(s):** `src/FlowTime.Core/Analysis/InvariantAnalyzer.cs`

**Checklist (TDD Order - Tests FIRST):**
- [x] Emit warnings for missing/partial served/errors class series (GREEN)
- [x] Commit: `fix(core): warn on topology class coverage gaps` (`3c2c2f8`)

**Status:** ✅ Complete

---

## Phase 3: Regression + Docs

**Goal:** Record the fix and confirm no regressions.

### Task 3.1: Docs alignment
**File(s):** `docs/templates/template-authoring.md` (if updated)

**Checklist (TDD Order - Tests FIRST):**
- [x] Update docs to reflect class series guidance (not required)
- [x] Commit: `docs: align epic roadmap and milestone status` (`7b4676c`)

**Status:** ✅ Complete

---

### Task 3.2: Final validation
**Checklist (TDD Order - Tests FIRST):**
- [x] `dotnet build`
- [x] `dotnet test --nologo`
- [x] Update tracking doc test status

**Status:** ✅ Complete

---

## Testing & Validation

### Test Case 1: Topology serviceWithBuffer propagation
**Status:** ✅ Complete

**Steps:**
1. [x] Build a minimal model with classed inflow and topology serviceWithBuffer semantics.
2. [x] Run class contribution builder.
3. [x] Verify served/errors series include class data.

**Expected:** Served/errors class series are emitted when arrivals are classed.

### Test Case 2: Analyzer warning for missing class series
**Status:** ✅ Complete

**Steps:**
1. [x] Provide contributions where arrivals are classed but served/errors are missing.
2. [x] Run topology class coverage analyzer.
3. [x] Verify warnings for missing/partial class series.

**Expected:** Analyzer reports missing class coverage for served/errors.
