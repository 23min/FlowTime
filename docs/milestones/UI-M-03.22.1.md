# UI‑M‑03.22.1 — Topology LOD + Feature Bar

**Status:** In Progress  
**Depends on:** UI‑M‑03.22 (Topology Canvas baseline)  
**Goal:** Add Level‑of‑Detail (LOD) rendering and a “Topology Features” bar (secondary Mud bar) to control overlays/filters without cluttering the default operator view.

---

## Overview

This milestone refines the topology canvas with a layered, zoom‑aware presentation and a compact control surface. By default the view stays clean and focused on operational nodes; the Features bar enables power users to turn on overlays (labels, edge shares, sparklines, full DAG) and adjust filter/threshold knobs.

A known bug with tooltips is also tracked and fixed here so keyboard/pointer inspection aligns with LOD behaviors.

---

## UX Design

- Component naming & scope
  - Introduce a dedicated `TopologyFeatureBar` component scoped to the topology page.
  - Other areas (dashboards, SLA views, etc.) may adopt their own feature bars later; keep the foundation adaptable but avoid premature generalization.
  - Initial pass focuses solely on topology overlays; cross-page reuse is deferred until requirements surface.

- Feature Bar (secondary Mud bar)
  - Vertical bar that appears immediately to the right of the primary left menu bar; it spans the same height as the main menu.
  - Component: `MudDrawer` (left‑side, clipped under app layout). Fixed width ~320 px.
  - Open/close: “X” in the bar header and keyboard shortcut `Alt+T`. Persist state in `localStorage`.
  - Sections (in order):
    1. Overlays (labels, arrows, edge shares, sparklines)
    2. Filters (by kind; curated topology vs. full DAG)
    3. LOD (Auto/On/Off per overlay; zoom thresholds)
    4. Color Basis (SLA/util/errors/queue) and thresholds
    5. Help/Legend (what colors/arrows/shares mean)

- Canvas behaviors (visual)
  - Directed edges (arrowheads), thinner strokes, compact labels (10px), rounded rectangles.
  - Sparklines render above nodes when enabled and at appropriate zoom levels.
  - Edge share labels appear near the edge midpoint when enabled.
  - Neighbor emphasis on focus (fade non‑neighbors).

**Validation plan:** Finalize the `TopologyFeatureBar` layout (sections, toggles, defaults) before implementing LOD logic. Stakeholders sign off on UX (labels, grouping, persistence) prior to wiring Auto/On/Off behaviors.

### Progress (Mar 2025)
- Feature bar drawer is live on the topology page with overlay, filter, LOD, and color-basis controls.
- Overlay settings now serialize to `localStorage` and restore on load (including Alt+T panel toggle).
- Canvas payload honors label/edge-arrow overlay modes with the zoom-aware defaults defined here.
- Overlay toggles simplified to switches (Auto removed) with a line vs. bar sparkline selector.
- Success-rate sparklines and edge-share labels render with overlay gating in Blazor + JS.
- Debug scaffolding trimmed; layout matches the target UX.

### Next Focus
- Implement full DAG node filters and neighbor emphasis behaviors.
- Restore tooltips and add render coverage for overlay payload changes.

---

## LOD Rules (Zoom‑aware)

- Scale ≤ 0.5 (zoomed out)
  - Show: service nodes, edges with arrows, color fills.
  - Hide: labels, sparklines, edge shares.
- 0.5 < Scale ≤ 1.0 (mid zoom)
  - Show: labels, optional single sparkline for services.
  - Hide: edge shares (unless explicitly forced On).
- Scale > 1.0 (zoomed in)
  - Show: labels, sparklines, optional edge shares.
  - Emphasize focused node and neighbors.
- Full DAG Mode (toggle)
  - Include const/expr nodes in draw + filters by kind. Default Off.

Each overlay supports Auto/On/Off. Auto follows LOD rules above; On forces display; Off hides regardless of zoom.

---

## Features and Toggles

- Labels: Show/hide node labels; truncate with ellipsis.
- Edge Arrows: Show/hide arrowheads.
- Edge Shares: Show normalized weight % along edges.
- Sparklines:
  - Services: SLA or arrivals/served mini‑chart from metrics.
  - Non‑services: arrivals/served/errors/queue fetched on demand.
- Full DAG Mode: Show const/expr nodes in addition to services.
- Filters: Include/exclude kinds (service, expr, const, queue‑capable).
- Color Basis: SLA (default), Utilization, Errors, Queue depth; adjustable thresholds.
- Neighbor Emphasis: Fade non‑neighbors when a node is focused.
- Reset to Defaults: Clear local preferences.

---

## Data and Interop

- Inputs
  - `/v1/runs/{runId}/graph` (topology, kinds, semantics strings)
  - `/v1/runs/{runId}/metrics` (service metrics and mini arrays)
  - `/v1/runs/{runId}/index` + `/series/{seriesId}` (raw series for non‑service sparklines)
- Mapping
  - Semantics per node reference concrete series (normalized in bundles to `file://telemetry/...`).
  - Edge shares = weight / sum(outgoing weights per source).
- Rendering payload additions (from Blazor → JS)
  - `overlays`: flags + color basis + thresholds + LOD mode
  - `nodeMini`: optional arrays for service sparklines
  - `edgeShares`: optional normalized shares for label rendering
- Performance
  - Lazy fetch raw series only when overlay enabled and nodes are visible.
  - Cache series and mini arrays for reuse across scrubs/pans.

---

## Accessibility

- Feature Bar: focusable controls, Esc to close, `Alt+T` to toggle.
- Canvas: proxy buttons preserve keyboard nav; overlays do not obscure focus.
- Contrast: text uses adaptive color for readability; legend explains color scale.

---

## Implementation Plan (No code in this milestone doc)

1) Feature Bar shell and state
   - Add `MudDrawer` to Topology page; persist panel + toggles in `localStorage`.
2) Overlay state plumbing
   - Extend canvas payload with `overlays`, `nodeMini`, optional `edgeShares`.
3) LOD engine
   - Apply Auto/On/Off per overlay based on `state.scale` in JS.
4) Service sparklines
   - Use metrics mini arrays for quick charts above nodes.
5) Edge shares
   - Compute normalized shares and render labels at edge midpoints.
6) Full DAG mode
   - Toggle to include non‑service nodes; filter by kind; cull at low zoom.
7) Docs + legend
   - Update legend and on‑panel help text; add a quick “What is shown?” section.

---

## Test Plan

- Render tests (bUnit)
  - Feature Bar toggles flip payload overlays; persisted state restores after re‑mount.
  - LOD: scale thresholds hide/show labels/sparklines/edge shares in payload.
- JS smoke tests (manual/visual)
  - Arrowheads on/off; edge shares appear/disappear; sparklines draw at correct zoom.
- Perf sanity
  - Scrub redraw ≤ 200 ms on ~20‑node graphs.
- Accessibility
  - Keyboard toggles; focus ring preserved; adequate contrast.

---

## Risks & Mitigations

- Visual clutter in Full DAG mode → default Off + LOD gating + filters.
- Series I/O overhead → lazy fetch + caching; prefer metrics for services.
- Label overlap → truncation + LOD; future: collision avoidance if needed.

---

## Open Bug (Tracked Here)

- Topology tooltips not working yet (hover/focus do not display or position correctly).  
  Action: Reproduce on focus/hover, fix placement and lifecycle, ensure content includes SLA/util/errors/queue/capacity with timestamp.

---

## Acceptance Criteria

- Feature Bar appears beside the left menu bar, opens/closes, and persists state.
- LOD behaves as specified by scale ranges and Auto/On/Off per overlay.
- Sparklines for services render above nodes when enabled; edge shares render when enabled.
- Full DAG mode includes non‑service nodes and respects kind filters.
- Tooltips work on hover and focus with correct content and placement.

---

## Out of Scope

- Collision‑aware label placement and edge bundling.
- Advanced animations; dynamic edge thickness by measured flow.
- Persisted per‑run overlay presets (future enhancement).

---

## File Impact Summary (expected)

- `src/FlowTime.UI/Pages/TimeTravel/Topology.razor` — add Feature Bar UI and state.
- `src/FlowTime.UI/Components/Topology/TopologyCanvas*.cs` — payload overlays, edge share computation.
- `src/FlowTime.UI/wwwroot/js/topologyCanvas.js` — LOD handling, labels/sparklines/edge shares rendering.
- `src/FlowTime.UI/Services/*` — optional series fetch helpers for non‑service nodes.
- Docs and legend updates under `docs/milestones` and page help.
