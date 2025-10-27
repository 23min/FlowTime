# UI‑M‑03.22 Implementation Tracking — Topology Canvas (Graph + Coloring)

**Milestone:** UI‑M‑03.22 — Topology Canvas (Graph + Coloring)  
**Status:** 🚧 In Progress  
**Branch:** `feature/ui-m-0322-topology-canvas`  

---

## Quick Links
- Milestone: `docs/milestones/UI-M-03.22.md`
- Roadmap: `docs/architecture/time-travel/ui-m3-roadmap.md`

---

## Current Status

### Overall Progress
- [x] Phase 1: Test Harness + Contracts
- [x] Phase 2: Canvas Component + Static Render
- [x] Phase 3: Coloring + Scrubber Integration
- [x] Phase 4: Hover/Focus Tooltips + Keyboard

### Test Status
- Unit: 9 passing / 9 total (TopologyHelpers)
- Render: 6 passing / 6 total (TopologyCanvas)

---

## Progress Log

### Session: Milestone Setup
- [x] Read milestone document
- [x] Create feature branch name
- [x] Create tracking document

**Next:** Polish docs + regression run; prep for review.

---

## Phase 1: Test Harness + Contracts

**Goal:** Verified helpers for graph mapping, color scale, tooltip formatting.

### Tasks
- [x] `GraphMapperTests` — API graph → internal model
- [x] `ColorScaleTests` — thresholds → palette
- [x] `TooltipFormatterTests` — compact metrics text

### Deliverables
- `GraphMapper.cs`, `ColorScale.cs`, tooltip helpers + tests.

---

## Phase 2: Canvas Component + Static Render

**Goal:** Mount canvas, draw nodes/edges, pan/zoom.

### Tasks
- [x] Render test: node count drawn
- [x] Implement draw loop + transforms
- [x] Extract layout math (refactor)

---

## Phase 3: Coloring + Scrubber Integration

**Goal:** Recolor on bin change within budget.

### Tasks
- [x] Render test: recolor on stubbed bin change
- [x] Hook global scrubber; fast update path
- [x] Memoization/refactor

---

## Phase 4: Hover/Focus Tooltips + Keyboard

**Goal:** Accessible info on hover/focus; navigation via keyboard.

### Tasks
- [x] Render test: tooltip show/hide
- [x] Hit‑test + placement
- [x] Keyboard nav + focus ring

---

## Notes / Blockers
- N/A

---

## Commands
```bash
# Focused tests
 dotnet test tests/FlowTime.UI.Tests/FlowTime.UI.Tests.csproj \
   --filter FullyQualifiedName~Topology
```
