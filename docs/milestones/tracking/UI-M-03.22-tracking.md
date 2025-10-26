# UI‑M‑03.22 Implementation Tracking — Topology Canvas (Graph + Coloring)

**Milestone:** UI‑M‑03.22 — Topology Canvas (Graph + Coloring)  
**Status:** 📋 Planned  
**Branch:** `feature/ui-m-0322-topology-canvas`  

---

## Quick Links
- Milestone: `docs/milestones/UI-M-03.22.md`
- Roadmap: `docs/architecture/time-travel/ui-m3-roadmap.md`

---

## Current Status

### Overall Progress
- [ ] Phase 1: Test Harness + Contracts
- [ ] Phase 2: Canvas Component + Static Render
- [ ] Phase 3: Coloring + Scrubber Integration
- [ ] Phase 4: Hover/Focus Tooltips + Keyboard

### Test Status
- Unit: 0 passing / 0 total (TBD)
- Render: 0 passing / 0 total (TBD)

---

## Progress Log

### Session: Milestone Setup
- [x] Read milestone document
- [x] Create feature branch name
- [x] Create tracking document

**Next:** Begin Phase 1 (write unit tests first).

---

## Phase 1: Test Harness + Contracts

**Goal:** Verified helpers for graph mapping, color scale, tooltip formatting.

### Tasks
- [ ] `GraphMapperTests` — API graph → internal model
- [ ] `ColorScaleTests` — thresholds → palette
- [ ] `TooltipFormatterTests` — compact metrics text

### Deliverables
- `GraphMapper.cs`, `ColorScale.cs`, tooltip helpers + tests.

---

## Phase 2: Canvas Component + Static Render

**Goal:** Mount canvas, draw nodes/edges, pan/zoom.

### Tasks
- [ ] Render test: node count drawn
- [ ] Implement draw loop + transforms
- [ ] Extract layout math (refactor)

---

## Phase 3: Coloring + Scrubber Integration

**Goal:** Recolor on bin change within budget.

### Tasks
- [ ] Render test: recolor on stubbed bin change
- [ ] Hook global scrubber; fast update path
- [ ] Memoization/refactor

---

## Phase 4: Hover/Focus Tooltips + Keyboard

**Goal:** Accessible info on hover/focus; navigation via keyboard.

### Tasks
- [ ] Render test: tooltip show/hide
- [ ] Hit‑test + placement
- [ ] Keyboard nav + focus ring

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

