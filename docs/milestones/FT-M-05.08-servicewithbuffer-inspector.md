# FT-M-05.08 - ServiceWithBuffer Inspector Consistency and Class Coverage

**Status:** 📋 Planned  
**Epic Reference:** `docs/architecture/service-with-buffer/README.md`  
**Owner:** FlowTime Engine + UI  
**Scope:** FlowTime.Core, FlowTime.API, FlowTime.UI, templates  
**Dependencies:** ✅ SB-M-05.01, ✅ SB-M-05.02, ✅ SB-M-05.03  
**Target:** Ensure ServiceWithBuffer nodes surface the same inspector metrics and class coverage guarantees as service nodes, with explicit clarity on what is model-driven vs. code-driven.

---

## Overview

ServiceWithBuffer was introduced as the canonical "service with queue" abstraction, and the UI renders these nodes with a queue badge. In practice, the inspector and sparklines show inconsistent metrics between service nodes and ServiceWithBuffer nodes, and class coverage is uneven across nodes in the same topology (notably in `transportation-basic-classes`). We need to determine whether missing metrics are caused by model semantics, engine output, or UI mapping, then align the inspector and class coverage so the output is consistent and predictable.

### Strategic Context
- **Motivation:** Users cannot trust inspector comparisons when ServiceWithBuffer nodes show fewer metrics or missing class chips relative to service nodes.
- **Impact:** Consistent inspector output, clearer model authoring expectations, and reduced confusion for class-driven runs.
- **Dependencies:** Uses the ServiceWithBuffer epic outputs and class-routing support already introduced in SB-M-05.01..05.03.

## Scope

### In Scope ✅
1. Audit Service vs. ServiceWithBuffer inspector metrics and sparklines against the actual series emitted in `/state_window`.
2. Define a clear metric contract for service-like nodes (service + ServiceWithBuffer), including queue-centric metrics.
3. Ensure class coverage is consistent for ServiceWithBuffer nodes in classed templates (e.g., `transportation-basic-classes`).
4. Update UI inspector logic so ServiceWithBuffer nodes display the expected metrics and class chips when data exists.
5. Document model-driven vs. code-driven gaps and update authoring guidance if needed.

### Out of Scope ❌
- Broad performance work (this is not a perf milestone).
- Router feature expansions beyond fixing class propagation gaps.
- Schema changes unrelated to ServiceWithBuffer inspector and class coverage.

### Future Work (Optional)
- Align router inspector UX with class coverage warnings if router class leakage persists.

## Requirements

### Functional Requirements

#### FR1: ServiceWithBuffer Metric Contract
**Description:** ServiceWithBuffer nodes must expose a predictable baseline metric set in the inspector, aligned with service nodes and queue ownership.

**Acceptance Criteria:**
- [ ] For service nodes, inspector shows: arrivals, served, errors, success rate, utilization, service time, flow latency, error rate, and optional retry metrics when series exist.
- [ ] For ServiceWithBuffer nodes, inspector shows: arrivals, served, errors, queue depth, queue latency status (when available), success rate, utilization, service time, flow latency, error rate, and optional retry metrics when series exist.
- [ ] When a metric series is missing, the inspector renders a consistent placeholder ("Model does not include series data") without silently dropping the metric.

**Examples:**
- HubQueue (ServiceWithBuffer) should display queue depth and queue latency alongside standard service metrics.
- LineAirport (service with retry semantics) should display attempts, failed retries, and retry echo.

**Error Cases:**
- Missing series should not crash the inspector or mislabel other metrics.

#### FR2: Class Coverage Parity
**Description:** Class chips and classed sparklines must appear consistently for ServiceWithBuffer nodes when the run has class data.

**Acceptance Criteria:**
- [ ] In `transportation-basic-classes`, HubQueue, AirportDispatchQueue, DowntownDispatchQueue, and IndustrialDispatchQueue all show class chips for Airport, Downtown, Industrial.
- [ ] Class totals are present for arrivals/served/errors/queue where the base node series exists.
- [ ] If class coverage is partial, the inspector displays a visible warning (or logs a warning) explaining the limitation.

**Error Cases:**
- Router outputs should not silently drop class data for downstream ServiceWithBuffer nodes.

#### FR3: Inspector Sparkline Consistency
**Description:** The inspector uses consistent series keys and alias resolution across service and ServiceWithBuffer nodes.

**Acceptance Criteria:**
- [ ] Alias resolution (`aliases` in topology semantics) applies equally to service and ServiceWithBuffer nodes.
- [ ] `queueDepth` and `queue` are treated as equivalent series for ServiceWithBuffer nodes.
- [ ] Retry metrics show only when the series is present, and are never fabricated from unrelated series.

### Non-Functional Requirements

#### NFR1: No Regression in Runtime Output
**Target:** Class coverage and inspector updates do not change the numerical totals in run artifacts.
**Validation:** Compare before/after totals for `transportation-basic-classes` and ensure only by-class visibility changes.

#### NFR2: UI Stability
**Target:** Inspector rendering remains stable with no crashes when series are missing.
**Validation:** Manual run through missing-series nodes plus UI tests.

## Technical Design (Summary)

### Architecture Decisions
**Decision:** Treat ServiceWithBuffer as "service-like plus queue" for inspector metrics, using `node.Kind` + `nodeLogicalType` mapping and series aliases.
**Rationale:** Aligns with the ServiceWithBuffer epic expectations and reduces user confusion.
**Alternatives Considered:** Hide queue metrics for ServiceWithBuffer (rejected as it contradicts queue ownership semantics).

## Implementation Plan

### Phase 1: Evidence and Gap Inventory
**Goal:** Separate model gaps from code gaps.

**Tasks:**
1. Capture `/graph` + `/state_window` series keys for:
   - `transportation-basic-classes` (CentralHub, HubQueue, AirportDispatchQueue).
   - `transportation-basic` (non-class baseline).
2. Build a matrix of expected vs. actual series per node kind.
3. Record which gaps are model-driven (template semantics missing) vs. code-driven (API/UI mapping).

**Deliverables:**
- Gap matrix table in this milestone doc or tracking doc.
- Clear classification of each missing metric.

**Success Criteria:**
- [ ] Each missing metric is tagged as model or code gap.

### Phase 2: Engine/API Alignment for Class Coverage
**Goal:** Ensure class data propagates to ServiceWithBuffer nodes in classed runs.

**Tasks:**
1. Verify `ClassContributionBuilder` output for router targets feeding ServiceWithBuffer nodes.
2. Confirm `StateQueryService` includes `ByClass` for those nodes when available.
3. Add or update tests for class propagation across router -> ServiceWithBuffer.

**Deliverables:**
- Tests covering router class propagation and ServiceWithBuffer class outputs.

**Success Criteria:**
- [ ] `ByClass` is present for ServiceWithBuffer nodes downstream of routers when class data exists.

### Phase 3: UI Inspector Alignment
**Goal:** Display consistent metric blocks and class chips for ServiceWithBuffer nodes.

**Tasks:**
1. Update inspector metric block construction to include queue depth for ServiceWithBuffer.
2. Normalize alias handling for queue series (`queue` vs `queueDepth`).
3. Ensure retry metrics are only shown when series exist.
4. Add UI tests for inspector metric block presence across service vs ServiceWithBuffer nodes.

**Deliverables:**
- Updated inspector logic with tests and UI snapshot coverage.

**Success Criteria:**
- [ ] Inspector shows the expected metric set for service and ServiceWithBuffer nodes.

### Phase 4: Documentation and Validation
**Goal:** Document what is required in templates and how class coverage behaves.

**Tasks:**
1. Update `docs/templates/template-authoring.md` with ServiceWithBuffer inspector expectations.
2. Add a short validation checklist to the tracking doc.

**Deliverables:**
- Updated authoring docs and validation checklist.

**Success Criteria:**
- [ ] Documentation clearly explains required series for ServiceWithBuffer inspector parity.

## Test Plan

### Test-Driven Development Approach
**Strategy:** RED -> GREEN -> REFACTOR for each phase.

### Unit Tests
**Focus:** Class contribution propagation and series mapping.

**Key Test Cases:**
1. `ClassContributionBuilder_ServiceWithBuffer_UsesRouterOutputs()`
2. `StateQueryService_IncludesByClass_ForServiceWithBufferNodes()`
3. `TopologyInspector_MetricBlocks_IncludeQueueDepth_ForServiceWithBuffer()`

### Integration Tests
1. `TimeTravelStateWindow_ServiceWithBuffer_ByClass_Exists()`
2. `TopologyInspector_ShowsRetryMetrics_WhenSeriesPresent()`

### UI Tests
1. `TopologyInspector_ServiceWithBuffer_MetricSet_Parity()`
2. `TopologyInspector_ClassChips_Appear_ForDispatchQueues()`

## Success Criteria

### Milestone Complete When:
- [ ] ServiceWithBuffer inspector metrics match the documented contract.
- [ ] Class chips appear consistently for ServiceWithBuffer nodes in classed templates.
- [ ] Missing series are explicitly reported, not silently skipped.
- [ ] Tests added and passing.
- [ ] Docs updated with model requirements and inspector expectations.

## File Impact Summary

### Files to Modify (Likely)
- `src/FlowTime.Core/Artifacts/ClassContributionBuilder.cs` - class propagation for ServiceWithBuffer/router outputs.
- `src/FlowTime.API/Services/StateQueryService.cs` - ensure ByClass is surfaced for ServiceWithBuffer nodes.
- `src/FlowTime.UI/Pages/TimeTravel/Topology.razor` - inspector metric block mapping and alias handling.
- `docs/templates/template-authoring.md` - clarify required series and class behavior.

