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
- [ ] Phase 1: Diagnosis + Tests (1/2 tasks)
- [ ] Phase 2: Fix + Validation (2/2 tasks)
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
- [ ] Verify class propagation path for serviceWithBuffer semantics

**Next Steps:**
- [ ] Begin Phase 1
- [ ] Start Task 1.1 (failing core tests)

---

## Phase 1: Diagnosis + Tests

**Goal:** Reproduce the dimming gap and identify where class series coverage is missing.

### Task 1.1: Add failing core tests
**File(s):** `tests/FlowTime.Core.Tests/`

**Checklist (TDD Order - Tests FIRST):**
- [x] Write test: `ClassContributionBuilder_PropagatesServiceWithBufferTopologyClasses` (RED)
- [x] Write test: `InvariantAnalyzer_WarnsOnTopologyClassCoverageGaps` (RED)
- [ ] Commit: `test(core): add class propagation/analyzer tests`

**Status:** ⏳ Not Started

---

### Task 1.2: Trace class series coverage
**File(s):** `src/FlowTime.Core/Artifacts/ClassContributionBuilder.cs`, `src/FlowTime.Core/Analysis/InvariantAnalyzer.cs`

**Checklist (TDD Order - Tests FIRST):**
- [ ] Identify class propagation gaps for topology semantics
- [ ] Confirm analyzer warning codes and targets
- [ ] Commit: `docs: capture class propagation analysis`

**Status:** ⏳ Not Started

---

## Phase 2: Fix + Validation

**Goal:** Ensure class filtering respects class-specific series for derived airport legs.

### Task 2.1: Implement class series fix
**File(s):** `src/FlowTime.Core/Artifacts/ClassContributionBuilder.cs`

**Checklist (TDD Order - Tests FIRST):**
- [x] Implement topology-based propagation for served/errors (GREEN)
- [ ] Commit: `fix(core): propagate class series for topology serviceWithBuffer`

**Status:** ⏳ Not Started

---

### Task 2.2: Add analyzer validation
**File(s):** `src/FlowTime.Core/Analysis/InvariantAnalyzer.cs`

**Checklist (TDD Order - Tests FIRST):**
- [x] Emit warnings for missing/partial served/errors class series (GREEN)
- [ ] Commit: `fix(core): warn on topology class coverage gaps`

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

### Test Case 1: Topology serviceWithBuffer propagation
**Status:** ⏳ Not Started

**Steps:**
1. [ ] Build a minimal model with classed inflow and topology serviceWithBuffer semantics.
2. [ ] Run class contribution builder.
3. [ ] Verify served/errors series include class data.

**Expected:** Served/errors class series are emitted when arrivals are classed.

### Test Case 2: Analyzer warning for missing class series
**Status:** ⏳ Not Started

**Steps:**
1. [ ] Provide contributions where arrivals are classed but served/errors are missing.
2. [ ] Run topology class coverage analyzer.
3. [ ] Verify warnings for missing/partial class series.

**Expected:** Analyzer reports missing class coverage for served/errors.
