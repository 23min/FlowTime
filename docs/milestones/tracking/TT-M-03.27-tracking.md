# TT‑M‑03.27 Implementation Tracking

> Note: This tracking document is created as work begins and is updated throughout implementation. See docs/development/milestone-rules-quick-ref.md for workflow and docs/development/milestone-documentation-guide.md for structure.

**Milestone:** TT‑M‑03.27 — Queues First‑Class (Backlog + Latency; No Retries)  
**Started:** 2025-11-04  
**Status:** 🚧 In Progress  
**Branch:** `feature/tt-m-03-27/queues-first-class`  
**Assignee:** TBA

---

## Quick Links

- Milestone Document: `docs/milestones/TT-M-03.27.md`
- Milestone Guide: `docs/development/milestone-documentation-guide.md`
- Tracking Template: `docs/development/TEMPLATE-tracking.md`

---

## Current Status

### Overall Progress
- [x] Phase 1: Templates + Topology (latency expr deferred by design)
- [x] Phase 2: API Latency Derivation
- [x] Phase 3: UI Canvas + Inspector
- [x] Phase 4: Docs + Tests + Roadmap

### Test Status
- Build: ✅ `dotnet build FlowTime.sln -c Release`
- API Tests: ✅ `dotnet test tests/FlowTime.Api.Tests -c Release --no-build`
- UI Tests: ✅ `dotnet test tests/FlowTime.UI.Tests -c Release --no-build`
- Full Suite / Perf: ✅ `dotnet test tests/FlowTime.Tests -c Release --no-build` (see `docs/performance/perf-log.md`)

---

## Progress Log

### 2025-11-04 — Kickoff & Template/UI foundations

**Preparation:**
- [x] Read milestone document
- [x] Review warehouse 1d/5m template
- [x] Identify `/state_window` builder for queue latency derivation
- [x] Review canvas node rendering path and inspector bindings

**Updates:**
- Inserted `DistributorQueue` between Warehouse and Distributor with `queue_inflow/outflow/depth` series and rerouted edges.
- Artifact writer now precomputes true SHIFT-based queue depth and normalizes `semantics.queueDepth` to emitted CSV URIs.
- Added persisted overlay toggle for queue scalar badge; queue chips now render left-aligned ahead of errors on queue nodes; feature panel scrollability fixed.
- Confirmed API already emits `latencyMinutes` (Little’s Law) and added coverage for zero-served bins.
- Authored architecture note: `docs/architecture/time-travel/queues-shift-depth-and-initial-conditions.md` (shift, initial conditions, telemetry expectations).

**Next Steps:**
- ✅ Milestone complete; follow-ups tracked in roadmap (telemetry fallback, retries/service-time deferrals).

---

## Phase 1: Templates + Topology

**Goal:** Insert explicit queue node in warehouse 1d/5m template with arrivals/served/queue series, SHIFT initial, and rerouted edges.

### Task 1.1: Add queue series
**File(s):** `templates/supply-chain-multi-tier-warehouse-1d5m.yaml`

Checklist:
- [x] Define `queue_inflow`, `queue_outflow`
- [x] Define `queue_depth` series (proxy authored, precomputed to CSV at artifact time)

### Task 1.2: Add queue node and reroute edges
**File(s):** `templates/supply-chain-multi-tier-warehouse-1d5m.yaml`

Checklist:
- [x] Add topology node `DistributorQueue` (kind=queue)
- [x] Route `Warehouse:out → DistributorQueue:in → Distributor:in`

### Task 1.3: Optional latency expr (until API computes)
**File(s):** `templates/supply-chain-multi-tier-warehouse-1d5m.yaml`

Checklist:
- [ ] `queue_latency_minutes := IF(queue_outflow > 0, (queue_depth / queue_outflow) * 5, null)` *(Deferred in favor of API derivation)*

---

## Phase 2: API Latency Derivation

**Goal:** Include `latencyMinutes` for queue nodes in `/state_window` (compute via Little’s Law; null when served==0).

### Task 2.1: Identify builder and extend payload
**File(s):** `src/FlowTime.API/**` (state_window assembler)

Checklist:
- [x] Detect `node.kind == queue`
- [x] Derive `latencyMinutes = (queue/served) * binMinutes` with guards
- [x] Include in response shape

### Task 2.2: Unit + golden tests
**File(s):** `tests/FlowTime.Api.Tests/**`

Checklist:
- [x] Unit tests for derivation and nulling (telemetry run with zero served bin)
- [x] Golden contract update for queue nodes (`state-window-queue-null-approved.json`)

### Task 2.3: Docs
**File(s):** `docs/milestones/TT-M-03.27.md`

Checklist:
- [x] Confirm Telemetry Contract section reflects API behavior

---

## Phase 3: UI Canvas + Inspector

**Goal:** Render queue nodes as rectangles with optional scalar badge; inspector shows Queue, Latency, Arrivals, Served with horizons.

### Task 3.1: Queue glyph and hit testing
**File(s):** `src/FlowTime.UI/wwwroot/js/topologyCanvas.js`

Checklist:
- [x] Branch on `meta.kind === 'queue'` for rectangle glyph
- [x] Ensure tooltip/hitbox matches shape

### Task 3.2: Scalar badge feature toggle
**File(s):** `src/FlowTime.UI/**`

Checklist:
- [x] Add overlay toggle (persisted via overlay settings)
- [x] Read current bin’s `queue` and draw scalar badge (with queue-node layout adjustments)

### Task 3.3: Inspector bindings
**File(s):** `src/FlowTime.UI/Pages/TimeTravel/Topology.razor`

Checklist:
- [x] Bind 4 series for queue nodes (Queue, Latency, Arrivals, Served)
- [x] Horizons show highlight window (verified via inspector tests)

### Task 3.4: A11y and performance
**File(s):** `src/FlowTime.UI/**`

Checklist:
- [x] Aria labels for charts/badge (existing components reused; verified during review)
- [x] Redraw budget ≤ 8ms on scrub (no regression observed; canvas profiling unchanged)

---

## Phase 4: Docs + Tests + Roadmap

**Goal:** Update docs, add tests, and record deferred work in the roadmap.

### Task 4.1: Docs updates
**File(s):** `docs/milestones/TT-M-03.27.md`, `docs/architecture/time-travel/time-travel-planning-roadmap.md`

Checklist:
- [x] Add example YAML snippet / narrative updates
- [x] Update roadmap with “Deferred” section (retries, service time S, oldest_age, edge overlays, queue depth fallback)

### Task 4.2: UI tests
**File(s):** `tests/FlowTime.UI.Tests/**`

Checklist:
- [x] Inspector renders queue stack (4 series)
- [x] Color-basis stroke responds to `Queue` basis

### Task 4.3: API tests
**File(s):** `tests/FlowTime.Api.Tests/**`

Checklist:
- [x] `latencyMinutes` inclusion for queues in `/state_window`

### Task 4.4: Build and validate
**File(s):** Solution

Checklist:
- [x] `dotnet build FlowTime.sln -c Release`
- [x] `dotnet test tests/FlowTime.Tests -c Release --no-build`

---

## Testing & Validation

### Test Case: Queue latency derivation
Status: ✅ Completed

Steps:
1. Create/run warehouse 1d/5m with queue node
2. Request `/state_window`
3. Inspect `latencyMinutes` series for queue node

Expected:
- `latencyMinutes = (queue/served) * binMinutes` when `served > 0`, else null

---

## Issues Encountered

- Template normalization initially emitted `semantics.queueDepth: queue_depth`, which the API rejected (non-URI); resolved by teaching the artifact writer to normalize the queue mapping and regenerating runs.

---

## Final Checklist

### Code Complete
- [x] All phases complete
- [x] No compilation errors
- [x] No console warnings (no new warnings introduced)

### Documentation
- [x] Milestone status updated (→ ✅ Complete)
- [x] Roadmap updated with Deferred section
- [ ] Release notes entry *(pending release planning)*

### Quality Gates
- [x] All unit tests passing
- [x] All integration/golden tests passing
- [x] Performance acceptable (see `docs/performance/perf-log.md`)
- [x] No regressions observed
