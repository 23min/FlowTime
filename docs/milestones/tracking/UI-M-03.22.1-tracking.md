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
- [ ] Overlay/LOD plumbing into canvas payload + JS
- [ ] Sparklines + edge share rendering
- [ ] Full DAG mode (non-service nodes + filters)
- [ ] Persistence & shortcuts (localStorage overlays, Alt+T toggle)

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
- [ ] Persist overlay choices + Alt+T shortcut (deferred)

### Session: LOD & Overlays
- [x] Extend settings to Auto/On/Off tri-state per overlay (+ filters, color basis controls)
- [ ] Update `TopologyCanvas` payload/JS to honor toggles + zoom scale
- [ ] Render service sparklines (metrics mini arrays) above nodes
- [ ] Compute edge shares and display labels with LOD gating
- [ ] Implement Full DAG mode & kind filters (service/expr/const)

### Session: Polish & Tests
- [ ] Persist feature bar state + overlay preferences to localStorage
- [ ] Add keyboard shortcut (Alt+T) to toggle bar
- [ ] Update render tests for overlay state
- [ ] Manual/perf verification (scrub ≤200ms, pan FPS)

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
