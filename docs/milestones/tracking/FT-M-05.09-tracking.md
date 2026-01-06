# FT-M-05.09 Implementation Tracking

**Milestone:** FT-M-05.09 — ServiceWithBuffer SLA + Backlog Health Signals  
**Started:** 2026-01-06  
**Status:** 📋 Planned  
**Branch:** `milestone/ft-m-05.09`  
**Assignee:** Codex  

---

## Quick Links

- **Milestone Document:** `docs/milestones/FT-M-05.09-servicewithbuffer-sla-backlog.md`
- **Related Analysis:** `docs/architecture/service-with-buffer/service-with-buffer-architecture-part2.md`
- **Milestone Guide:** `docs/development/milestone-documentation-guide.md`

---

## Current Status

### Overall Progress
- [ ] Phase 1: SLA Contract + Batch Semantics (0/3 tasks)
- [ ] Phase 2: Backlog Health Warnings (0/3 tasks)
- [ ] Phase 3: Queue Invariant Alignment (0/3 tasks)
- [ ] Phase 4: Continuous Classed Template (0/3 tasks)
- [ ] Phase 5: Docs + Validation (0/3 tasks)

### Test Status
- **Unit Tests:** 0 passing / 0 total
- **Integration Tests:** 0 passing / 0 total
- **UI Tests:** 0 passing / 0 total

---

## Progress Log

### 2026-01-06 - Session Start

**Preparation:**
- [x] Read milestone document
- [x] Read architecture Part 2 doc
- [x] Create milestone branch
- [x] Create tracking document

**Next Steps:**
- [ ] Begin Phase 1

---

## Phase 1: SLA Contract + Batch Semantics

**Goal:** Add SLA taxonomy and batch-safe completion semantics.

### Task 1.1: SLA payload contract
**File(s):** `src/FlowTime.Contracts/TimeTravel/StateContracts.cs`

**Checklist (TDD Order - Tests FIRST):**
- [ ] Write unit test: `SlaPayload_IncludesKindAndStatus_WhenInputsMissing` (RED)
- [ ] Update SLA contract types (GREEN)
- [ ] Update API serialization tests (GREEN)

**Status:** ⏳ Not Started

### Task 1.2: SLA derivation rules
**File(s):** `src/FlowTime.API/Services/StateQueryService.cs`

**Checklist (TDD Order - Tests FIRST):**
- [ ] Write unit test: `BatchCompletionSla_CarriesForwardUntilNextRelease` (RED)
- [ ] Implement batch carry-forward rules (GREEN)
- [ ] Add backlog-age SLA unavailable rules (GREEN)

**Status:** ⏳ Not Started

### Task 1.3: UI SLA labels
**File(s):** `src/FlowTime.UI/Pages/TimeTravel/Topology.razor`

**Checklist (TDD Order - Tests FIRST):**
- [ ] Write UI test: `Inspector_ShowsSlaKindLabels` (RED)
- [ ] Render SLA kind + unavailable state (GREEN)

**Status:** ⏳ Not Started

### Phase 1 Validation
- [ ] Unit tests pass
- [ ] SLA kinds visible in UI

---

## Phase 2: Backlog Health Warnings

**Goal:** Add backlog growth/overload/age warnings.

### Task 2.1: Warning definitions + tests
**File(s):** `src/FlowTime.API/Services/StateQueryService.cs`

**Checklist (TDD Order - Tests FIRST):**
- [ ] Write unit test: `BacklogWarnings_GrowthOverloadAgeRisk` (RED)
- [ ] Implement warning generation (GREEN)

**Status:** ⏳ Not Started

### Task 2.2: Warning payload plumbing
**File(s):** `src/FlowTime.Contracts/TimeTravel/StateContracts.cs`

**Checklist (TDD Order - Tests FIRST):**
- [ ] Write integration test: `GetStateWindow_ReturnsBacklogWarnings_ForContinuousTemplate` (RED)
- [ ] Add warning payload fields (GREEN)

**Status:** ⏳ Not Started

### Task 2.3: UI warning surfacing
**File(s):** `src/FlowTime.UI/Pages/TimeTravel/Topology.razor`

**Checklist (TDD Order - Tests FIRST):**
- [ ] Write UI test: `Inspector_ShowsBacklogWarnings` (RED)
- [ ] Display warning badges/summary (GREEN)

**Status:** ⏳ Not Started

---

## Phase 3: Queue Invariant Alignment

**Goal:** Remove false invariant warnings by aligning queue depth series.

### Task 3.1: Template invariant tests
**File(s):** `tests/FlowTime.Api.Tests/*`, `templates/*.yaml`

**Checklist (TDD Order - Tests FIRST):**
- [ ] Write integration test: `QueueInvariant_Holds_ForDispatchQueues` (RED)
- [ ] Fix template series to satisfy invariant (GREEN)

**Status:** ⏳ Not Started

### Task 3.2: Dispatch queue alignment
**File(s):** `templates/transportation-basic-classes.yaml`, `templates/transportation-basic.yaml`

**Checklist (TDD Order - Tests FIRST):**
- [ ] Write template fixture test (RED)
- [ ] Update queue depth expressions (GREEN)

**Status:** ⏳ Not Started

### Task 3.3: Validation run
**Checklist (TDD Order - Tests FIRST):**
- [ ] Verify invariant warnings cleared in UI (GREEN)

**Status:** ⏳ Not Started

---

## Phase 4: Continuous Classed Template

**Goal:** Add continuous ServiceWithBuffer classed template.

### Task 4.1: Template spec + schema tests
**File(s):** `templates/it-document-processing-continuous.yaml`

**Checklist (TDD Order - Tests FIRST):**
- [ ] Write template schema test (RED)
- [ ] Add template file + parameters (GREEN)

**Status:** ⏳ Not Started

### Task 4.2: Class coverage validation
**File(s):** `tests/FlowTime.Api.Tests/*`

**Checklist (TDD Order - Tests FIRST):**
- [ ] Write integration test for class coverage (RED)
- [ ] Ensure classed ServiceWithBuffer outputs (GREEN)

**Status:** ⏳ Not Started

### Task 4.3: Template docs
**File(s):** `docs/templates/*`

**Checklist (TDD Order - Tests FIRST):**
- [ ] Write docs update checklist (RED)
- [ ] Document continuous classed template (GREEN)

**Status:** ⏳ Not Started

---

## Phase 5: Docs + Validation

**Goal:** Align documentation and validate test suite.

### Task 5.1: Modeling + telemetry docs
**Checklist (TDD Order - Tests FIRST):**
- [ ] Update `docs/notes/modeling-queues-and-buffers.md` (RED)
- [ ] Update `docs/reference/engine-capabilities.md` (GREEN)

**Status:** ⏳ Not Started

### Task 5.2: Template authoring guidance
**Checklist (TDD Order - Tests FIRST):**
- [ ] Update `docs/templates/template-authoring.md` (RED)
- [ ] Validate examples (GREEN)

**Status:** ⏳ Not Started

### Task 5.3: Full validation
**Checklist (TDD Order - Tests FIRST):**
- [ ] Run `dotnet build`
- [ ] Run `dotnet test --nologo`

**Status:** ⏳ Not Started

---

## Final Checklist

- [ ] All phase tasks complete
- [ ] `dotnet build` passes
- [ ] `dotnet test --nologo` passes
- [ ] Milestone document updated (status → ✅ Complete)
- [ ] Release notes added
