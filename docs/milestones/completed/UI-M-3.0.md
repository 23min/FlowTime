# UI-M-3.0 — Time-Travel UI (Minimal M3)

**Status:** 📋 Planned  
**Dependencies:** ✅ M-03.00 (Time-Travel foundations), ✅ M-03.02 (Telemetry capture + bundle), ✅ M-03.04 (Run packaging, state window)  
**Target:** Deliver a minimal, reliable time‑travel UI: SLA Dashboard, Topology with scrubber, and Node Detail lines, operating on run bundles.

---

## Overview

This milestone delivers the first usable Time‑Travel UI on top of run bundles (formerly called "gold" data). It restores core UI surfaces broken by M3 refactors, rationalizes navigation, and introduces three focused visualizations: (1) SLA Dashboard tiles; (2) Flow Topology (entire graph) with global scrubber and heat‑map node coloring; (3) Node Detail Panel with simple line charts. The goal is clarity and responsiveness without transactional drilldown.

### Strategic Context
- Motivation: Executives and operators need at‑a‑glance SLA and topology health during time‑scrub playback while the API layer stabilizes.
- Impact: Establishes a stable navigation area “Time‑Travel”, restores Artifacts, and creates reproducible analysis over run bundles.
- Dependencies: Engine/sim telemetry bundling and run packaging must be available (M‑03.x line). UI consumes the generated artifacts directly.

---

## Milestone Tree

Phase 1 — Align With M3 (Refactor and Restore)
- [ ] UI-M-03.10 — UI Baseline & Build Health (see: docs/milestones/completed/UI-M-03.10.md)
- [ ] UI-M-03.11 — Artifacts Page Restoration (see: docs/milestones/completed/UI-M-03.11.md)
- [ ] UI-M-03.12 — Simulate → Run Bundle Integration (see: docs/milestones/completed/UI-M-03.12.md)
- [x] UI-M-03.13 — Analyze Section Decision (complete — nav hide documented)
- [x] UI-M-03.14 — Time‑Travel Nav & Routes (skeleton) (complete — nav group + placeholders live)
- [x] UI-M-03.15 — Run Bundle Data Access Service (REST) (complete — REST client & data service)
- [ ] UI-M-03.16 — Run Orchestration Page (skeleton) (planned)
- [ ] TT-M-03.17 — Telemetry Auto-Capture Orchestration (planned)
- [ ] TT-M-03.18 — Telemetry Auto-Capture (Seeded RNG Support) (planned)
- [ ] UI-M-03.18 — QA & Docs Pass (planned)

Phase 2 — Minimal Time‑Travel Visualizations
- [ ] UI-M-03.20 — SLA Dashboard (tiles + mini bars) (planned)
- [ ] UI-M-03.21 — Global Top Bar + Range + Scrubber (planned)
- [ ] UI-M-03.22 — Topology Canvas (graph + coloring) (planned)
- [ ] UI-M-03.23 — Node Detail Panel (simple lines) (planned)
- [ ] UI-M-03.24 — SLA ↔ Topology Linking (planned)
- [ ] UI-M-03.25 — Performance + A11y Pass (planned)
- [ ] UI-M-03.26 — Documentation & Stabilization (planned)

Refer to the roadmap for phase scope and sequencing: docs/architecture/time-travel/ui-m3-roadmap.md

---

## Scope

### In Scope ✅
1. New “Time‑Travel” nav group with pages: Dashboard, Topology, Run Orchestration, and link to Artifacts.
2. SLA Dashboard tiles with mini bar sparklines (no forecasting).
3. Topology canvas rendering for entire graph with heat‑map node coloring at current bin.
4. Global top bar with range presets and scrubber; keyboard controls.
5. Node Detail Panel with simple line charts; defaults by node type.
6. Time-Travel data service that consumes the `/v1/runs` REST endpoints and shapes responses for UI use.
7. Run Orchestration page (skeleton) to drive telemetry capture + bundling and register runs.
8. Artifacts page restoration (list bundles, open actions, link to Time‑Travel).

### Out of Scope ❌
- ❌ Forecasting, anomaly detection, wave propagation heatmaps, path analysis boards.
- ❌ Transactional/customer drilldown and per‑class segmentation.
- ❌ Full API endpoint implementation for `/flow` and `/metrics` (handled by backend milestones).

### Future Work (Follow‑ups)
- Compare runs overlays; advanced charts (heatmaps); alert markers.
- REST endpoints for `graph`, `state_window`, `metrics` and UI adapter switch.

---

## Requirements

### Functional Requirements

#### FR1: Time‑Travel Navigation
- Description: A top‑level “Time‑Travel” menu appears in the left nav with Dashboard, Topology, Run Orchestration, and Artifacts.
- Acceptance Criteria:
  - [ ] Clicking each item routes to a corresponding page in the main content area.
  - [ ] The current `runId` (if selected) and scrubber state are visible from these pages.

#### FR2: SLA Dashboard (Tiles)
- Description: Show per‑flow SLA % for the selected range; each tile includes binsMet/total and a mini bar sparkline.
- Acceptance Criteria:
  - [ ] Tiles render from `metrics.json.flows[]` with `name`, `slaPct`, `binsMet`, `binsTotal`, `mini`.
  - [ ] Status icons reflect thresholds (Green ≥95, Yellow [90–95), Red <90).
  - [ ] Clicking a tile navigates to Topology with the current `runId`.

#### FR3: Topology Canvas (Entire Graph)
- Description: Render nodes/edges from `graph.json`; color nodes by SLA or utilization threshold using the current bin from `state_window`.
- Acceptance Criteria:
  - [ ] Canvas supports pan/zoom; node hover tooltips show current bin metrics.
  - [ ] Node colors follow rules: SLA if available; otherwise utilization; Gray if no bin data.
  - [ ] Clicking a node opens Node Detail Panel; panel remains open while scrubbing.

#### FR4: Global Scrubber + Range
- Description: A single top bar hosts range presets and a scrubber controlling all views.
- Acceptance Criteria:
  - [ ] Range presets (1h, 6h, 24h, 7d) and absolute start/end constrain visible bins.
  - [ ] Keyboard: ←/→ moves one bin; Space toggles play/pause.
  - [ ] Scrub updates reflect across tiles, topology, and Node Detail Panel within target performance.

#### FR5: Node Detail Panel (Lines)
- Description: Right‑side panel shows line charts based on node type.
- Acceptance Criteria:
  - [ ] Queue node: Queue depth and Latency lines; Arrival vs Service dual line.
  - [ ] Service node: Latency and Utilization lines; Errors line (when available).
  - [ ] Series derived from `state_window.json` sliced to the selected range; highlight current bin.

#### FR6: Run Orchestration (Skeleton)
- Description: Page to execute telemetry capture + bundling and register a new run.
- Acceptance Criteria:
  - [ ] User can start orchestration, view progress/logs, and end with a new `runId`.
  - [ ] New run appears in Artifacts and is openable in Time‑Travel pages.

#### FR7: Artifacts Restoration
- Description: Page lists available run bundles with basic metadata.
- Acceptance Criteria:
  - [ ] Shows run id, time range, bundle location; open action navigates into Time‑Travel.

### Non‑Functional Requirements

#### NFR1: Performance
- Target: Scrub update (recolor + small UI updates) ≤ 200 ms on typical graphs (≤20 nodes).
- Validation: Manual profiling and logs; ensure redraw throttling for the canvas.

#### NFR2: Accessibility
- Target: Keyboard navigation (Tab, ←/→, Space) and colorblind‑aware indicators.
- Validation: Manual checks; ensure labels and aria attributes exist.

---

## Data Requirements (Gold Bundles)

- `runs/{runId}/graph.json`
```json
{
  "nodes": [ { "id": "OrderSvc", "type": "service" }, { "id": "OrderQueue", "type": "queue" } ],
  "edges": [ { "from": "OrderSvc", "to": "OrderQueue" } ],
  "sla": { "latencyThresholdMin": 2.0 }
}
```

- `runs/{runId}/state_window.json`
```json
{
  "binMinutes": 5,
  "bins": [ { "t": "2025-10-07T13:55:00Z", "nodes": { "OrderSvc": {"arr":150, "srv":145, "lat":0.5, "util":0.72, "err":2}, "OrderQueue": {"q":25, "lat":10.7, "util":0.93} } } ]
}
```

- `runs/{runId}/metrics.json`
```json
{
  "flows": [ { "name": "Orders", "slaPct": 95.8, "binsMet": 23, "binsTotal": 24, "mini": [0.1,0.2,0.35,0.6,0.8,0.6,0.3,0.2] } ]
}
```

Mapping to future endpoints (later): `/v1/runs/{id}/graph`, `/state_window`, `/metrics`.

---

## Implementation Plan

### Phase 1: Align With M3 (Refactor and Restore)
- Tasks:
  1. Restore build/boot and rationalize nav placeholders.
  2. Restore Artifacts page (list/open runs from bundle folders).
  3. Rework Simulate to produce gold runs discoverable by Artifacts.
  4. Decide Analyze section (hide or update to a gold data access test).
  5. Introduce a data access adapter for gold files (file‑backed now, API‑ready later).
  6. Add Run Orchestration page (skeleton) invoking telemetry capture + bundling; show `runId`.
- Deliverables: Updated nav; Artifacts restored; Simulate integration; adapter; orchestration skeleton.
- Success Criteria:
  - [ ] App boots; nav routes exist; Artifacts lists and opens runs; Simulate pipeline produces a run; Orchestration returns `runId`.

### Phase 2: Minimal Time‑Travel Visualizations
- Tasks:
  1. SLA Dashboard tiles with mini bars (no forecast).
  2. Global top bar with range presets and scrubber; shared UI state.
  3. Topology canvas (entire graph) with node coloring per current bin.
  4. Node Detail Panel with line charts by node type; series sliced to range.
  5. SLA↔Topology linking; selection persists across pages.
  6. Performance and accessibility pass; documentation.
- Deliverables: Three working views wired to run bundles; minimal documentation.
- Success Criteria:
  - [ ] SLA tiles, Topology, and Node Detail render correctly and respond to scrubber; performance ≤ 200 ms update budget.

---

## Test Plan

### Test‑Driven Development Approach
- Strategy: RED → GREEN → REFACTOR. Write parsing/data binding unit tests first for gold adapter; integration checks for each page reading artifacts; smoke E2E clicks for nav and scrub.

### Test Categories
- Unit Tests
  - Adapter parses `graph.json`, `state_window.json`, `metrics.json` correctly (sample bundles).
  - Color mapping logic based on SLA/util thresholds.
- Integration Tests
  - SLA tiles render expected counts/labels from a canned `metrics.json`.
  - Topology view updates colors when the scrubber advances by one bin.
  - Node Detail Panel loads correct series for queue/service.
- E2E (manual scripted)
  - From Artifacts, open a run, use Dashboard → Topology, click a node, scrub through bins, verify updates.

### Coverage Goals
- Unit: Data binding and threshold logic.
- Integration: Each of the three views with representative sample bundles.
- E2E: Core user journey from Artifacts to Time‑Travel and back.

---

## Success Criteria

### Milestone Complete When:
- [ ] Time‑Travel nav group added with working routes.
- [ ] SLA Dashboard tiles render from `metrics.json` with correct thresholds and mini bars.
- [ ] Topology canvas renders entire graph and recolors on scrub.
- [ ] Node Detail Panel shows correct series and tracks the scrubber.
- [ ] Artifacts restored; Simulate produces runs; Orchestration page returns a `runId` and links to views.
- [ ] Documentation updated (UI roadmap, in‑product help).

---

## File Impact Summary

### Files to Create
- `ui/FlowTime.UI/Pages/TimeTravel/Dashboard.razor` — SLA tiles view (gold adapter backed)
- `ui/FlowTime.UI/Pages/TimeTravel/Topology.razor` — Canvas topology view + node detail panel
- `ui/FlowTime.UI/Pages/TimeTravel/RunOrchestration.razor` — Orchestration skeleton (initiate bundling, show runId)
- `ui/FlowTime.UI/Services/TimeTravelGoldAdapter.cs` — Run bundle reader abstraction
- `ui/FlowTime.UI/State/TimeTravelState.cs` — Range + scrubber shared state

### Files to Modify (Major)
- `ui/FlowTime.UI/Layout/ExpertLayout.razor` — Add “Time‑Travel” nav group (Dashboard, Topology, Orchestration, Artifacts)

### Files to Modify (Minor)
- `ui/FlowTime.UI/Pages/Artifacts/*.razor` — Restore listing and open actions
- `ui/FlowTime.UI/Pages/Simulate/*.razor` — Update to output runs compatible with gold adapter
- `ui/FlowTime.UI/Pages/Analyze/*.razor` — Hide or repurpose to data access test

---

## References
- docs/ui/time-travel-visualizations-3.md
- docs/architecture/time-travel/ui-m3-roadmap.md
- docs/operations/telemetry-capture-guide.md
- docs/architecture/time-travel/time-travel-architecture-ch2-data-contracts.md
