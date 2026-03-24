# FT-M-05.09 — ServiceWithBuffer SLA + Backlog Health Signals

**Status:** ✅ Complete  
**Dependencies:** ✅ FT-M-05.08  
**Target:** Implement batch-safe SLA semantics and backlog health signals for ServiceWithBuffer so SLA and queue warnings remain meaningful across batch and continuous systems.

---

## Overview

This milestone implements the SLA taxonomy and backlog health signals defined in `work/epics/completed/service-with-buffer/service-with-buffer-architecture-part2.md`. The goal is to prevent misleading SLA (for example 0% between batch releases), expose sustained backlog risk, and ensure queue invariants are enforced for both template-driven and telemetry-driven runs. A new continuous, classed template will validate that ServiceWithBuffer works beyond batch release patterns.

### Strategic Context
- **Motivation:** Batch and gated-release systems currently produce misleading SLA/queue signals and invariant warnings. Continuous systems lack a classed ServiceWithBuffer reference template.
- **Impact:** Clearer SLA interpretation, actionable backlog warnings, and improved template coverage for continuous processing.
- **Dependencies:** Builds on FT-M-05.08 (ServiceWithBuffer inspector, class coverage, and narrative metadata).

## Scope

### In Scope ✅
1. Add SLA taxonomy to the API contract (completion SLA, backlog age SLA, schedule adherence).
2. Implement batch-safe SLA semantics (carry-forward or "no data" rules between releases).
3. Add backlog health warnings (growth streak, overload ratio, age risk) in the API output.
4. Enforce queue depth invariant checks for ServiceWithBuffer in templates and fixtures; address the dispatch-queue mismatch cause.
5. Add a continuous, classed ServiceWithBuffer template for validation.
6. Document SLA/backlog behavior and telemetry gaps (queue age missing) in modeling docs.

### Out of Scope ❌
- ❌ Full anomaly detection (beyond node-local warnings).
- ❌ EdgeTimeBin or edge-level analytics.
- ❌ New sink node kind (metadata-only sink remains future work).
- ❌ UI redesign unrelated to SLA/backlog warnings.

### Future Work
- Sink node metadata (render-only) once SLA semantics stabilize.
- Cross-node incident clustering (anomaly detection epic).

## Requirements

### Functional Requirements

#### FR1: SLA Taxonomy in API Contract
**Description:** The API must expose SLA results with explicit semantic type so UI and downstream tooling can interpret them correctly.

**Acceptance Criteria:**
- [ ] API returns SLA metrics with a `kind` value: `completion`, `backlogAge`, or `scheduleAdherence`.
- [ ] Each SLA metric exposes its calculation inputs (threshold, window/bin alignment).
- [ ] Missing inputs produce `status: unavailable` rather than a fabricated value.

**Error Cases:**
- Missing queue age distribution must not yield a backlog SLA; return `unavailable`.

#### FR2: Batch-Safe SLA Semantics
**Description:** SLA must remain meaningful between gated releases.

**Acceptance Criteria:**
- [ ] Completion SLA evaluated on release bins and carried forward until next release (or explicitly marked `noEvents`).
- [ ] Schedule adherence computed independently of backlog depth.
- [ ] Backlog age SLA remains continuous when queue age distribution exists.

**Examples:**
- In batch dispatch systems, SLA remains stable between dispatches (no 0% plateau).

#### FR3: Backlog Health Warnings
**Description:** API exposes node-local backlog warnings for sustained risk.

**Acceptance Criteria:**
- [ ] Growth streak warning: queueDepth increases for N consecutive bins.
- [ ] Overload warning: arrivals / capacity > 1 for N consecutive bins.
- [ ] Age risk warning: queueAgeP95 exceeds threshold for M bins.
- [ ] Warning payload includes nodeId, window start/end bins, and primary signal.

#### FR4: Queue Depth Invariant Alignment
**Description:** Queue depth must satisfy the invariant: prior depth + arrivals - served - errors (plus optional attrition).

**Acceptance Criteria:**
- [ ] Dispatch queues in templates are updated so queue depth is consistent with arrivals/served/errors.
- [ ] Invariant warnings are only emitted when the invariant is truly violated (not due to batch modeling inconsistencies).

#### FR5: Continuous Classed ServiceWithBuffer Template
**Description:** Add a continuous ServiceWithBuffer template with class coverage and external dependencies.

**Acceptance Criteria:**
- [ ] Template models continuous arrivals (no dispatch schedule).
- [ ] Includes at least three classes and class-based routing.
- [ ] All processing stages are ServiceWithBuffer nodes with queue depth, capacity, and service time series.
- [ ] Includes retry + DLQ semantics.
- [ ] Used in tests to validate SLA/backlog outputs for continuous systems.

### Non-Functional Requirements

#### NFR1: Telemetry-Safe Derivations
**Target:** SLA/backlog logic must not invent signals from missing telemetry inputs.  
**Validation:** Tests verify `unavailable` status when queue age distribution is missing.

#### NFR2: Performance
**Target:** SLA/backlog computations remain O(bins) per node; no cross-node analysis.  
**Validation:** No new cross-node joins; warnings are node-local only.

## Implementation Plan

### Phase 1: SLA Contract + Batch Semantics
**Goal:** Add SLA taxonomy and batch-safe completion semantics.

**Tasks:**
1. Add RED tests for SLA payload shape and batch carry-forward rules.
2. Implement SLA kinds in API contract and derive completion/backlog/schedule SLA.
3. Update UI to display SLA kind labels and `unavailable` status consistently.

**Success Criteria:**
- [ ] SLA payload includes kind + status.
- [ ] Batch models no longer show 0% SLA between releases.

### Phase 2: Backlog Health Warnings
**Goal:** Add backlog health signals and warnings.

**Tasks:**
1. Add RED tests for growth streak, overload ratio, and age risk warnings.
2. Implement warning generation in API.
3. Surface warnings in UI inspector and run summary.

**Success Criteria:**
- [ ] Warnings include nodeId + time window + signal type.

### Phase 3: Queue Invariant Alignment
**Goal:** Remove false invariant warnings by aligning template series.

**Tasks:**
1. Add RED tests that assert invariant holds for dispatch queues.
2. Update affected templates/fixtures to align queue depth with arrivals/served/errors.
3. Verify warnings only appear when true misalignment exists.

**Success Criteria:**
- [ ] Transportation dispatch queues no longer emit invariant warnings.

### Phase 4: Continuous Classed Template
**Goal:** Add a continuous ServiceWithBuffer classed template for validation.

**Tasks:**
1. Add RED tests for template schema + class coverage.
2. Implement the IT document processing template (continuous, classed, retry + DLQ).
3. Validate template in tests and documentation.

**Success Criteria:**
- [ ] Template used in tests to validate SLA/backlog signals.

### Phase 5: Documentation + Validation
**Goal:** Ensure docs and examples stay aligned.

**Tasks:**
1. Update modeling/telemetry docs with SLA taxonomy and backlog warnings.
2. Update template authoring guidance for queue age requirements.
3. Run full build/test and record warnings.

## Test Plan

### Test-Driven Development Approach
Write RED tests for SLA payload shape, batch carry-forward rules, backlog warnings, queue invariant checks, and the new continuous classed template. Implement API/UI changes until tests pass, then refactor.

### Unit Tests
1. `SlaPayload_IncludesKindAndStatus_WhenInputsMissing`
2. `BatchCompletionSla_CarriesForwardUntilNextRelease`
3. `BacklogWarnings_GrowthOverloadAgeRisk`
4. `QueueInvariant_Holds_ForDispatchQueues`

### Integration Tests
1. `GetStateWindow_ReturnsSlaKinds_ForBatchTemplate`
2. `GetStateWindow_ReturnsBacklogWarnings_ForContinuousTemplate`

### UI Tests
1. `RunOrchestration_DisplaysSlaKindLabels`
2. `Inspector_ShowsUnavailableForMissingQueueAge`

## Success Criteria

### Milestone Complete When
- [ ] SLA taxonomy implemented and exposed by API.
- [ ] Backlog warnings emitted and visible in UI.
- [ ] Queue invariant warnings resolved for dispatch queues.
- [ ] Continuous classed template added and validated.
- [ ] All tests passing and docs updated.

## File Impact Summary

### Files to Create
- `work/milestones/tracking/FT-M-05.09-tracking.md`
- `templates/it-document-processing-continuous.yaml` (or equivalent)

### Files to Modify (Major Changes)
- `src/FlowTime.API/Services/StateQueryService.cs` (SLA kinds + warnings)
- `src/FlowTime.Contracts/TimeTravel/StateContracts.cs` (SLA payload contract)
- `src/FlowTime.UI/Pages/TimeTravel/Topology.razor` (SLA display)
- `templates/*.yaml` (queue invariant alignment)

### Files to Modify (Minor Changes)
- `docs/notes/modeling-queues-and-buffers.md`
- `docs/templates/template-authoring.md`
- `docs/reference/engine-capabilities.md`

## Migration Guide

No breaking changes anticipated. New SLA fields are additive and default to `unavailable` when inputs are missing.
