# UI-M-03.23 Implementation Tracking

> **Note:** This tracking document is created when work begins on the feature branch.  
> Do not create this until you're ready to start implementation.  
> See [Milestone Rules](milestone-rules-quick-ref.md) for workflow.

**Milestone:** UI-M-03.23 — Node Detail Panel Refresh  
**Started:** 2025-11-02  
**Status:** 🔄 In Progress  
**Branch:** `feature/ui-m-0323-node-detail-panel`  
**Assignee:** Codex

---

## Quick Links

- **Milestone Document:** [`docs/milestones/UI-M-03.23.md`](../milestones/UI-M-03.23.md)
- **Related Analysis:** _(link once available)_
- **Milestone Guide:** [`docs/development/milestone-documentation-guide.md`](../development/milestone-documentation-guide.md)

---

## Current Status

### Overall Progress
- [ ] Phase 1: Inspector State Persistence (1/3 tasks)
- [ ] Phase 2: Metric Stack Rendering (0/3 tasks)
- [ ] Phase 3: Missing-Series Messaging (0/3 tasks)

### Test Status
- **Unit Tests:** 0 passing / 0 total
- **Integration Tests:** 0 passing / 0 total
- **E2E Tests:** 0 passing / 0 planned

---

## Progress Log

### 2025-11-02 - Session Start

**Preparation:**
- [x] Read milestone document
- [x] Read related documentation
- [x] Create feature branch
- [ ] Verify dependencies (services, tools, etc.)

**Next Steps:**
- [ ] Begin Phase 1
- [ ] Start with first task (persistence test RED)

### 2025-11-02 - Inspector Persistence RED pass

**Changes:**
- Drafted `TopologyInspectorTests` covering drawer persistence, metric stacks, and missing-series placeholders (Phase 1 RED + Phase 2/3 scaffolding).
- Added test hooks to `Topology` for inspector state inspection and logger injection.

**Tests:**
- ✅ `dotnet test tests/FlowTime.UI.Tests/FlowTime.UI.Tests.csproj -c Release`

**Next Steps:**
- [ ] Implement inspector pinning and canvas blur handling (Phase 1 Task 1.2).
- [ ] Replace MudChart stack with sparkline blocks per node type (Phase 2 Task 2.2).
- [ ] Surface placeholder messaging with single warning per node kind (Phase 3 Task 3.2).

### 2025-11-02 - Inspector persistence GREEN wiring

**Changes:**
- Introduced inspector pinning state, background-click blur handling, and stacked sparkline blocks with per-node metric selection.
- Replaced the modal drawer with a side-by-side inspector panel so the canvas/scrubber stay interactive.
- Added placeholder messaging with once-per-kind logging and ensured sparklines reuse color-basis strokes.

**Tests:**
- ✅ `dotnet build FlowTime.sln -c Release`
- ⚠️ `dotnet test FlowTime.sln -c Release` *(fails: FlowTime.Api.Tests.RunOrchestrationGoldenTests.{CreateSimulationRun_ResponseMatchesGolden, CreateRun_ResponseMatchesGolden} due to golden RNG diff)*

**Next Steps:**
- [ ] Investigate golden RNG divergence or coordinate with API owners.
- [ ] Continue Phase 2/3 GREEN+REFACTOR clean-up once API differences resolved.

---

## Phase 1: Inspector State Persistence

**Goal:** Keep the detail drawer open across canvas and scrubber interactions.

### Task 1.1: Persistence Test (RED)
**File(s):** `tests/FlowTime.UI.Tests/TimeTravel/TopologyInspectorTests.cs`

**Checklist (TDD Order - Tests FIRST):**
- [x] Write unit tests covering inspector pinning and explicit dismiss (`InspectorRemainsOpenDuringMetricsUpdate`, `InspectorClosesWhenFocusClears`)
- [ ] Commit: `test(ui): add inspector persistence regression`

**Status:** 🔄 In Progress

### Task 1.2: State Management Update (GREEN)
**File(s):** `src/FlowTime.UI/Pages/TimeTravel/Topology.razor`

**Checklist:**
- [ ] Implement explicit inspector state flag
- [ ] Ensure canvas callbacks respect the pinned state
- [ ] Commit: `feat(ui): persist inspector drawer across scrub`

**Status:** ⏳ Not Started

### Task 1.3: Cleanup & Refactor (REFACTOR)
**File(s):** `Topology.razor`, related helpers

**Checklist:**
- [ ] Remove obsolete dismissal hooks
- [ ] Confirm Escape/close button behaviour
- [ ] Commit: `refactor(ui): streamline inspector dismiss logic`

**Status:** ⏳ Not Started

### Phase 1 Validation

**Smoke Tests:**
- [ ] Build solution (no compilation errors)
- [ ] Run new persistence test (passing)

**Success Criteria:**
- [ ] Inspector stays open while interacting with scrubber/graph
- [ ] Manual check confirms expected close triggers

---

## Phase 2: Metric Stack Rendering

**Goal:** Render the updated sparkline stack with category-specific metrics and padding.

### Task 2.1: Metric Stack Tests (RED)
**File(s):** `tests/FlowTime.UI.Tests/TimeTravel/TopologyInspectorTests.cs`

**Checklist:**
- [x] Add tests for service/queue/computed metric sections (`BuildInspectorMetrics_ServiceNode_ReturnsExpectedStack`, `BuildInspectorMetrics_PmfNode_IncludesDistribution`)
- [ ] Commit: `test(ui): cover node metric stack layout`

**Status:** 🔄 In Progress

### Task 2.2: Panel Layout Implementation (GREEN)
**File(s):** `Topology.razor`, `TopologyInspectorSparkline.razor`

**Checklist:**
- [ ] Extend helper to provide required series slices
- [ ] Update inspector markup with new stack and padding
- [ ] Commit: `feat(ui): add stacked inspector sparklines`

**Status:** ⏳ Not Started

### Task 2.3: Helper Refactor (REFACTOR)
**File(s):** `Topology.razor` helpers

**Checklist:**
- [ ] Consolidate metric ordering logic
- [ ] Share placeholder rendering function
- [ ] Commit: `refactor(ui): unify inspector metric helpers`

**Status:** ⏳ Not Started

### Phase 2 Validation

**Smoke Tests:**
- [ ] Build solution
- [ ] Run updated unit tests
- [ ] Manual visual check (service/queue/computed nodes)

**Success Criteria:**
- [ ] All expected sparklines visible with correct titles
- [ ] Selected bin highlight and stroke colors accurate

---

## Phase 3: Missing-Series Messaging

**Goal:** Display “Model does not include series data” placeholders and deduplicated logs.

### Task 3.1: Placeholder Test (RED)
**File(s):** `tests/FlowTime.UI.Tests/TimeTravel/TopologyInspectorTests.cs`

**Checklist:**
- [x] Write test verifying placeholder text when series absent (`BuildInspectorMetrics_QueueNodeWithMissingSeries_UsesPlaceholderAndLogsOnce`)
- [ ] Commit: `test(ui): assert missing series placeholder`

**Status:** 🔄 In Progress

### Task 3.2: Placeholder Implementation (GREEN)
**File(s):** `Topology.razor`

**Checklist:**
- [ ] Render placeholder text within metric stack
- [ ] Hook into logger for once-per-node warning
- [ ] Commit: `feat(ui): show missing series placeholder`

**Status:** ⏳ Not Started

### Task 3.3: Refactor Logging (REFACTOR)
**File(s):** `Topology.razor`

**Checklist:**
- [ ] Deduplicate warning tracking with existing structures
- [ ] Commit: `refactor(ui): dedupe missing series warnings`

**Status:** ⏳ Not Started

### Phase 3 Validation

**Smoke Tests:**
- [ ] Build solution
- [ ] Run new placeholder tests
- [ ] Manual check on run lacking series

**Success Criteria:**
- [ ] Placeholder text appears as specified
- [ ] Logs contain single warning per missing metric

---

## Testing & Validation

### Test Case 1: Inspector Persistence
**Status:** ⏳ Not Started

**Steps:**
1. Open topology, focus a node.
2. Adjust scrubber position.
3. Click canvas background.

**Expected:** Inspector remains open until manually dismissed.  
**Result:** _(TBD)_

### Test Case 2: Queue Metric Stack
**Status:** ⏳ Not Started

**Steps:**
1. Focus a queue node with full data.
2. Verify four sparklines render simultaneously.
3. Scrub to confirm highlight moves across all charts.

**Expected:** Queue depth, latency, arrivals, served visible with highlights.  
**Result:** _(TBD)_

### Test Case 3: Missing Series Placeholder
**Status:** ⏳ Not Started

**Steps:**
1. Load run lacking queue series.
2. Focus queue node.

**Expected:** Placeholder text “Model does not include series data” replaces missing sparkline(s).  
**Result:** _(TBD)_

---

## Issues Encountered

_(None yet)_

---

## Final Checklist

### Code Complete
- [ ] All phases complete
- [ ] All tests passing
- [ ] No compilation errors
- [ ] Console free of new warnings
- [ ] Code review complete (if applicable)

### Documentation
- [ ] Milestone document updated (status → ✅ Complete)
- [ ] Roadmap updated (if required)
- [ ] Release notes entry drafted
- [ ] Related docs refreshed

### Quality Gates
- [ ] Unit tests passing
- [ ] Integration tests passing
- [ ] Manual validation complete
- [ ] Performance confirmed
- [ ] No regressions detected
