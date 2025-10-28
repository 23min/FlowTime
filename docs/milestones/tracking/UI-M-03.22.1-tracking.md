# UI‑M‑03.22.1 Implementation Tracking — Topology LOD + Feature Bar

**Milestone:** UI‑M‑03.22.1 — Topology LOD + Feature Bar  
**Status:** 🚧 In Progress  
**Branch:** `feature/ui-m-0322-topology-canvas`

---

## Quick Links
- Milestone: `docs/milestones/UI-M-03.22.1.md`
- Parent milestone: `docs/milestones/UI-M-03.22.md`
- Layout reference: `src/FlowTime.UI/Pages/TimeTravel/Topology.razor`

---

## Current Status

### Overall Progress
- [x] UX scaffolding: TopologyFeatureBar component + layout column
- [x] Overlay/LOD plumbing into canvas payload + JS
- [ ] Sparklines + edge share rendering
- [ ] Full DAG mode (non-service nodes + filters)
- [x] Persistence & shortcuts (localStorage overlays, Alt+T toggle)

### Test Status
- [ ] Render tests updated for overlay payload changes
- [ ] JS smoke verified for overlays/LOD
- [ ] Accessibility checks (feature bar focus order, keyboard toggle)

---

## Progress Log

### Session: Feature Bar Scaffolding
- [x] Added `TopologyOverlaySettings` basic flags
- [x] Created `TopologyFeatureBar` with initial toggles (labels, arrows, full DAG placeholder)
- [x] Embedded feature bar into topology layout & saved runId to localStorage
- [x] Persist overlay choices + Alt+T shortcut

### Session: LOD & Overlays
- [x] Extend settings to Auto/On/Off tri-state per overlay (+ filters, color basis controls)
- [x] Update `TopologyCanvas` payload/JS to honor labels/edge arrows with zoom-aware defaults
- [ ] Render service sparklines (metrics mini arrays) above nodes
- [ ] Compute edge shares and display labels with LOD gating
- [ ] Implement Full DAG mode & kind filters (service/expr/const)

### Session: Polish & Tests
- [x] Persist feature bar state + overlay preferences to localStorage
- [x] Add keyboard shortcut (Alt+T) to toggle bar
- [ ] Update render tests for overlay state
- [ ] Manual/perf verification (scrub ≤200ms, pan FPS)

### Session: Overlay Refinement (current)
- [x] Switched Mud toggles to explicit handlers to ensure state changes fire reliably
- [x] Restored clean `TopologyFeatureBar` UI (removed debug controls/logging)
- [x] Simplified persistence path; verified overlay booleans sync with `localStorage`

---

## Notes / Blockers
- Tooltips bug tracked in this milestone; fix once overlay payload is wired.
- Need to ensure JS interop remains performant as overlay options expand (watch redraw time).

---

## Commands
```bash
# Topology-focused tests
dotnet test tests/FlowTime.UI.Tests/FlowTime.UI.Tests.csproj \
  --filter FullyQualifiedName~Topology

# Rebuild UI after JS/CSS changes
dotnet build src/FlowTime.UI/FlowTime.UI.csproj
```
