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
- [x] Sparklines + edge share rendering
- [ ] Full DAG mode (non-service nodes + filters)
  - [x] API spec drafted for `GET /v1/runs/{runId}/graph?mode=full&kinds=...`
  - [x] API implementation + UI requery on toggle (mode switch, dependency edge toggles, JS gating)
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
- [x] Render service sparklines (metrics mini arrays) above nodes
- [x] Compute edge shares and display labels with LOD gating
- [ ] Implement Full DAG mode & kind filters (service/expr/const)

### Session: Polish & Tests
- [x] Persist feature bar state + overlay preferences to localStorage
- [x] Add keyboard shortcut (Alt+T) to toggle bar
- [ ] Update render tests for overlay state
- [ ] Manual/perf verification (scrub ≤200ms, pan FPS)

### Session: Overlay Refinement
- [x] Switched Mud toggles to explicit handlers to ensure state changes fire reliably
- [x] Restored clean `TopologyFeatureBar` UI (removed debug controls/logging)
- [x] Simplified persistence path; verified overlay booleans sync with `localStorage`

### Session: Sparklines & Edge Shares
- [x] Emit per-node success-rate sparklines from the state window and pass to canvas
- [x] Compute normalized edge shares and surface in render payload
- [x] JS render pipeline now honors overlay toggles for labels/arrows/shares/sparklines
- [x] Canvas now uses elbowed edges with port markers and refined node styling

### Session: Edge Routing Polish
- [x] Reworked elbow routing to add lane separation and approach stubs for dense graphs
- [x] Ports now respect node bounds + extra clearance so badges no longer overlap node edges
- [x] Simplified routing points and rounded corners to avoid overlapping segments
- [x] Reduced elbows to two per diagonal leg and keep aligned edges straight for clarity

### Session: Canvas Zoom Controls
- [x] Disabled scroll-wheel zoom to avoid accidental scale changes
- [x] Added a dedicated zoom slider with live value feedback in the feature bar
- [x] Initial zoom anchors at 100% while nodes render smaller for crisp output
- [x] Shrunk default node size so the 100% view remains sharp without GPU scaling

### Session: Routing Modes + Controls
- [x] Added toggle for orthogonal vs. bezier edge rendering with ports centered on node sides
- [x] Swapped numeric inputs for sliders across zoom/threshold controls for faster tuning
- [x] Canvas now uses resize observer so window resize adjusts bounds without implicit zoom
- [x] Removed unused LOD sliders and added section dividers/dense radio styling for clarity
- [x] Replaced scrubber slider with “radio dial” timeline + playback controls (incident markers spec still pending)

### Session: Threshold Controls & Timeline Dial
- [x] Wired SLA/util/error sliders through to canvas + sparklines for per-bin color thresholds
- [x] Styled timeline dial with tick marks, tuner thumb, play/pause, loop, and speed selector
- [x] Synced mouse-wheel canvas zoom back into feature-bar slider so values stay aligned
- [ ] TODO: Add incident markers + auto-loop presets once timeline UX spec lands

### Session: Full DAG API Wiring
- [x] Extended `/v1/runs/{runId}/graph` to support `mode`, `kinds`, `dependencyFields`, and dependency edge metadata.
- [x] Added Feature Bar dependency toggles (arrivals/served/errors/queue/capacity/expr) with JS gating.
- [x] UI now re-fetches graph when Full DAG mode toggles on/off; dependency toggles persist client-side and cut server payload when subset selected.

### Session: Layout & Visual Updates
- [x] Switched layout to Top→Bottom (stable ranks; x=index, y=layer).
- [x] Increased zoom max to 400% and added label font scaling for legibility.
- [x] Styled non-service nodes: expr (diamond), const/pmf (capsule); queues show an inner depth bar.
- [x] Dashed styling for dependency edges; labels remain crisp at higher zoom.

---

## Notes / Blockers
- Tooltips bug tracked in this milestone; fix once overlay payload is wired.
- Need to ensure JS interop remains performant as overlay options expand (watch redraw time).
- Follow-up specs captured in `docs/milestones/UI-M-03.22.1.md` for incident markers on the dial and the node inspector panel (see `topography.md` / `image3.png`).
- Full DAG now depends on API `mode=full`; ensure run bundles include const/expr/pmf definitions (legacy runs without them still render operational topology only).

---

## Commands
```bash
# Topology-focused tests
dotnet test tests/FlowTime.UI.Tests/FlowTime.UI.Tests.csproj \
  --filter FullyQualifiedName~Topology

# Rebuild UI after JS/CSS changes
dotnet build src/FlowTime.UI/FlowTime.UI.csproj
```
