# UI‑M‑03.22.1 Implementation Tracking — Topology LOD + Feature Bar

**Milestone:** UI‑M‑03.22.1 — Topology LOD + Feature Bar  
**Status:** ✅ Completed  
**Branch:** `feature/ui-m-0322-topology-canvas`

---

## Quick Links
- Milestone: `docs/milestones/UI-M-03.22.1.md`
- Parent milestone: `docs/milestones/UI-M-03.22.md`
- Layout reference: `src/FlowTime.UI/Pages/TimeTravel/Topology.razor`

---

## Current Status

### Completion Highlights
- Canvas pan/zoom now persists per run (localStorage) and restores without auto-centering; wheel + slider + 100 % chip stay in sync.
- Tooltip alignment fixed (8 px left, vertically centered), blank padding removed, hover outline suppressed.
- Node inspector charts render bar series with tick-only X-axis, min/max Y labels, grey background, and sparkline parity across node kinds.
- Mud chip badges retain numeric values with canvas tooltips for semantics (arrivals/served/errors/queue/capacity); capacity chip repositioned; PMF/const formatting corrected.
- Feature bar persistence + section collapse, scrollable panel, right-aligned reset chip, focus metric spacing, and unified toggle set (compute toggle removed).
- Canvas visuals refreshed: triangular sparkline footer, capsule badge corners, operational nodes share rounded capsule shape, Happy Path layout polished.
- API/UI wiring for Full DAG + dependency filters complete; tests/gateway fixtures updated.

### Test Status
- [x] Updated topology canvas render + helper tests (per-run viewport snapshots, feature bar wiring).
- [x] Golden API fixtures refreshed for dependency payload updates.
- [ ] Accessibility sweep (feature bar keyboard order) deferred to follow-up milestone.

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
- [x] Added dedicated zoom slider with live value feedback in the feature bar + 100 % reset chip
- [x] Wheel zoom reinstated with viewport persistence; slider and chip stay synchronized
- [x] Resize observer keeps canvas bounds current without forcing re-center or scale jumps

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

### Session: Happy Path & Inspector polish
- [x] Happy Path layout now stages expr/const/pmf nodes vertically relative to their consumers with compressed lateral lanes; upstream inputs sit just left of the backbone for readability.
- [x] Badge rack now stays semantics-only (Arrivals top-left, Served bottom-left, Errors/Queue/Capacity arranged outboard) with a brighter palette; compute nodes surface via inspector rather than crowding the main canvas.
- [x] Replaced DOM tooltip with theme-aware canvas card (fixed pixel size, constant offset, auto-dismiss 2s after pointer leave).
- [x] Preserved canvas pan/zoom when toggling feature-bar filters so the viewport stays where the user left it.
- [x] Moved node labels outside the node (left, right-aligned) so the distinct shapes stay visually uncluttered and typography matches edge annotations.
- [x] Mini sparkline now tracks the active color basis (SLA/util/errors/queue), moves above the input port, and shares the node’s fill color so the bar highlight matches the node body.
- [x] Added layout mode radio (Layered vs Happy Path beta) alongside template-position toggle; fallback ensures service nodes remain visible when compute filters adjust.
- [x] Stubbed inspector drawer (right) showing node id + current-bin metrics via new node-focused callback (awaiting extended detail pass).

---

## Notes / Hand-off
- Incident markers for the timeline dial and playback presets are deferred to UI‑M‑03.22.2 (`docs/milestones/UI-M-03.22.1.md`).
- Full DAG rendering depends on new graph payloads that surface const/expr/pmf semantics; legacy runs continue to show operational topology only.
- Inspector deep-dive (lazy-loaded CSV detail for compute nodes) moves to UI‑M‑03.22.2 alongside accessibility verification of the feature bar.

---

## Commands
```bash
# Topology-focused tests
dotnet test tests/FlowTime.UI.Tests/FlowTime.UI.Tests.csproj \
  --filter FullyQualifiedName~Topology

# Rebuild UI after JS/CSS changes
dotnet build src/FlowTime.UI/FlowTime.UI.csproj
```
