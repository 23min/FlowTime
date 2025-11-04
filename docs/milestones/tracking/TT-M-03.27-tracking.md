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
- [x] Phase 1: Templates + Topology (2/3 tasks complete; latency expr deferred)
- [ ] Phase 2: API Latency Derivation (0/3 tasks)
- [ ] Phase 3: UI Canvas + Inspector (1/4 tasks complete; others pending validation)
- [ ] Phase 4: Docs + Tests + Roadmap (0/4 tasks)

### Test Status
- Build: ✅ `dotnet build FlowTime.sln -c Release`
- API Tests: ✅ `dotnet test tests/FlowTime.Api.Tests -c Release --no-build`
- UI Tests: ✅ `dotnet test tests/FlowTime.UI.Tests -c Release --no-build`
- Remaining Suites: 🚫 Full solution test sweep still pending (perf/long-running suites)

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
- Extended template outputs for queue series; adjusted depth expr to avoid cyclic SHIFT while keeping non-negative depth proxy.
- Added persisted overlay toggle for queue scalar badge; queue chips now render left-aligned ahead of errors on queue nodes; feature panel scrollability fixed.
- Confirmed API already emits `latencyMinutes`; validated via run regeneration after artifact normalization fix.

**Next Steps:**
- [ ] Phase 2 — Add explicit `latencyMinutes` tests and contract coverage
- [ ] Phase 3 — Wire inspector horizons + UI tests for queue stack
- [ ] Phase 4 — Documentation + roadmap updates

---

## Phase 1: Templates + Topology

**Goal:** Insert explicit queue node in warehouse 1d/5m template with arrivals/served/queue series, SHIFT initial, and rerouted edges.

### Task 1.1: Add queue series
**File(s):** `templates/supply-chain-multi-tier-warehouse-1d5m.yaml`

Checklist:
- [x] Define `queue_inflow`, `queue_outflow`
- [x] Define `queue_depth` proxy (SHIFT-free backlog approximation with non-negative clamp)

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
- [ ] Detect `node.kind == queue`
- [ ] Derive `latencyMinutes = (queue/served) * binMinutes` with guards
- [ ] Include in response shape

### Task 2.2: Unit + golden tests
**File(s):** `tests/FlowTime.Api.Tests/**`

Checklist:
- [ ] Unit tests for derivation and nulling
- [ ] Golden contract update for queue nodes

### Task 2.3: Docs
**File(s):** `docs/milestones/TT-M-03.27.md`

Checklist:
- [ ] Confirm Telemetry Contract section reflects API behavior

---

## Phase 3: UI Canvas + Inspector

**Goal:** Render queue nodes as rectangles with optional scalar badge; inspector shows Queue, Latency, Arrivals, Served with horizons.

### Task 3.1: Queue glyph and hit testing
**File(s):** `src/FlowTime.UI/wwwroot/js/topologyCanvas.js`

Checklist:
- [ ] Branch on `meta.kind === 'queue'` for rectangle glyph
- [ ] Ensure tooltip/hitbox matches shape

### Task 3.2: Scalar badge feature toggle
**File(s):** `src/FlowTime.UI/**`

Checklist:
- [x] Add overlay toggle (persisted via overlay settings)
- [x] Read current bin’s `queue` and draw scalar badge (with queue-node layout adjustments)

### Task 3.3: Inspector bindings
**File(s):** `src/FlowTime.UI/Pages/TimeTravel/Topology.razor`

Checklist:
- [ ] Bind 4 series for queue nodes (Queue, Latency, Arrivals, Served)
- [ ] Horizons show highlight window

### Task 3.4: A11y and performance
**File(s):** `src/FlowTime.UI/**`

Checklist:
- [ ] Aria labels for charts/badge
- [ ] Redraw budget ≤ 8ms on scrub

---

## Phase 4: Docs + Tests + Roadmap

**Goal:** Update docs, add tests, and record deferred work in the roadmap.

### Task 4.1: Docs updates
**File(s):** `docs/milestones/TT-M-03.27.md`, `docs/architecture/time-travel/time-travel-planning-roadmap.md`

Checklist:
- [ ] Add example YAML snippet
- [ ] Update roadmap with “Deferred” section (retries, service time S, oldest_age, edge overlays)

### Task 4.2: UI tests
**File(s):** `tests/FlowTime.UI.Tests/**`

Checklist:
- [ ] Inspector renders queue stack (4 series)
- [ ] Color-basis stroke responds to `Queue` basis

### Task 4.3: API tests
**File(s):** `tests/FlowTime.Api.Tests/**`

Checklist:
- [ ] `latencyMinutes` inclusion for queues in `/state_window`

### Task 4.4: Build and validate
**File(s):** Solution

Checklist:
- [ ] `dotnet build FlowTime.sln -c Release`
- [ ] `dotnet test FlowTime.sln -c Release`

---

## Testing & Validation

### Test Case: Queue latency derivation
Status: ⏳ Not Started

Steps:
1. Create/run warehouse 1d/5m with queue node
2. Request `/state_window`
3. Inspect `latencyMinutes` series for queue node

Expected:
- `latencyMinutes = (queue/served) * binMinutes` when `served > 0`, else null

---

## Issues Encountered

- Template normalization initially emitted `semantics.queue: queue_depth`, which the API rejected (non-URI); resolved by teaching the artifact writer to normalize the queue mapping and regenerating runs.

---

## Final Checklist

### Code Complete
- [ ] All phases complete
- [ ] No compilation errors
- [ ] No console warnings

### Documentation
- [ ] Milestone status updated (→ ✅ Complete)
- [ ] Roadmap updated with Deferred section
- [ ] Release notes entry

### Quality Gates
- [ ] All unit tests passing
- [ ] All integration/golden tests passing
- [ ] Performance acceptable
- [ ] No regressions
