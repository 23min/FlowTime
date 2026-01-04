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
6. Prefer API-side derivation/aliasing of ServiceWithBuffer metrics when data exists, rather than requiring template-only fixes.

### Out of Scope ❌
- Broad performance work (this is not a perf milestone).
- Router feature expansions beyond fixing class propagation gaps.
- Schema changes unrelated to ServiceWithBuffer inspector and class coverage.

### Known Regression (Carry-over)
- **Topology chip hover tooltip**: Hovering over on-node metric chips no longer shows the tooltip popup. Likely introduced during FT-M-05.07 UI perf work.
  - **Probable cause:** Chip hitboxes are only populated during static scene rebuilds. Overlay updates (e.g., timeline scrub/selected bin changes) do not mark the scene dirty, so `chipHitboxes` can be stale or empty and hover hit-testing returns `null`.
  - **Symptoms:** Chips still render, but hover detection never sets `hoveredChipId`, so `drawChipTooltip` never fires.
  - **Diagnostics:** Inspect `chipHitboxes.length` after an overlay-only update; if `0`, hover tooltips will never appear.
  - **Fix direction:** Refresh chip hitboxes on overlay updates (or add a lightweight hitbox-only pass) when overlay metrics change.
  - **Provenance/analysis:** During FT-M-05.07, canvas rendering was split into static scene rebuilds vs. overlay-only updates. The hover tooltip relies on chip hitboxes that are currently rebuilt only when `rebuildStaticScene` is true. When the timeline or overlay changes without a full scene rebuild, `chipHitboxes` remains empty and the hover path exits early. This is most likely in `src/FlowTime.UI/wwwroot/js/topologyCanvas.js` around the draw path and overlay refresh flow.

### Future Work (Optional)
- Align router inspector UX with class coverage warnings if router class leakage persists.

## Requirements

### Functional Requirements

#### FR1: ServiceWithBuffer Metric Contract
**Description:** ServiceWithBuffer nodes must expose a predictable baseline metric set in the inspector, aligned with service nodes and queue ownership.

**Acceptance Criteria:**
- [ ] For service nodes, inspector shows: arrivals, served, errors, success rate, utilization, service time, flow latency, error rate, and optional retry metrics when series exist.
- [ ] For ServiceWithBuffer nodes, inspector shows: arrivals, served, errors, queue depth, queue latency status (when available), success rate, utilization, service time, flow latency, error rate, and optional retry metrics when series exist.
- [ ] When ServiceWithBuffer semantics are incomplete but series exist in `/state_window`, the API derives/aliases queue latency, utilization, and service time rather than requiring template edits.
- [ ] When a metric series is missing, the inspector renders a consistent placeholder ("Model does not include series data") without silently dropping the metric.

#### FR1.1: Metric Derivation Rules (API)
**Description:** Define explicit derivation/alias rules so ServiceWithBuffer metrics are computed consistently when source series exist.

**Rules:**
- **Queue latency**: derived from queue depth + served count (or equivalent) over the same time bin. If either input is missing, queue latency remains unavailable.
- **Utilization**: derived from served count + capacity (or capacitySeries). If capacity is missing, utilization remains unavailable.
- **Service time**: derived from processing time sum + served count (or equivalent). If processing time or served count is missing, service time remains unavailable.
- **Flow latency**: only shown when the run provides it directly; it is not inferred from queue latency + service time.

**Acceptance Criteria:**
- [ ] Derivation rules are implemented in the API (not in the UI).
- [ ] The API never invents metrics without source series; missing inputs yield "No data".

#### FR1.2: Missing Data Behavior
**Description:** Missing series should be explicit and consistent across service and ServiceWithBuffer nodes.

**Acceptance Criteria:**
- [ ] Missing metrics show a consistent "No data" placeholder.
- [ ] Missing inputs are logged or surfaced in diagnostics (non-blocking).
- [ ] The UI does not hide metrics entirely when a series is missing.

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
**Decision:** Treat ServiceWithBuffer as "service-like plus queue" for inspector metrics, using `node.Kind` + `nodeLogicalType` mapping and series aliases. Prefer API-derived metrics from existing series before requesting template changes.
**Rationale:** Aligns with the ServiceWithBuffer epic expectations and reduces user confusion.
**Alternatives Considered:** Hide queue metrics for ServiceWithBuffer (rejected as it contradicts queue ownership semantics).

## Implementation Plan

### Phase 1: Evidence and Gap Inventory
**Goal:** Separate model gaps from code gaps.

**Tasks:**
1. Capture `/graph` + `/state_window` series keys for:
   - `transportation-basic-classes` (CentralHub, HubQueue, AirportDispatchQueue).
   - `transportation-basic` (non-class baseline).
2. Capture ClassContributionBuilder routing outputs for the same runs and nodes to verify class series propagation.
3. Build a matrix of expected vs. actual series per node kind.
4. Record which gaps are model-driven (template semantics missing) vs. code-driven (API/UI mapping).

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
3. Update modeling documentation to clarify which series must be emitted (and which are derived) for ServiceWithBuffer parity.
4. Update reference docs to reflect API-derived metrics and required inputs.

**Deliverables:**
- Updated authoring docs and validation checklist.

**Success Criteria:**
- [ ] Documentation clearly explains required series for ServiceWithBuffer inspector parity.
 - [ ] Modeling/templates/telemetry docs stay consistent with API derivation rules.

## Documentation Impact

The API derivation approach affects multiple docs and must remain consistent:
- `docs/templates/template-authoring.md`: required series inputs for ServiceWithBuffer.
- `docs/modeling.md`: modeling guidance for queues, services, and buffers.
- `docs/reference/*`: inspector metrics and semantics expectations.
- `docs/schemas/*` (if schemas are updated): ensure telemetry/engine artifacts list the required series.

## Future-Proofing Guard

The UI must remain agnostic to data origin. Whether series come from templates, simulation artifacts, or telemetry ingestion, the inspector uses the same API contract and derivation rules. Any changes must preserve that contract so telemetry-driven runs behave identically to template-driven runs.

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
