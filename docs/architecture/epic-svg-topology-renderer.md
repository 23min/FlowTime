# Epic: SVG Topology Renderer (dag-map Integration)

> **Date:** 2026-03-23
> **Status:** Proposal
> **Depends on:** UI Layout Motors epic (conceptually); can be done incrementally ahead of it
> **Related:** `docs/architecture/ui-layout/README.md`, `engine-deep-review-2026-03.md`

---

## 1. Motivation

The current FlowTime topology view is a **10,000-line Canvas 2D renderer** (`topologyCanvas.js`) with a **2,100-line Blazor component** (`TopologyCanvas.razor.cs`). It is feature-rich but:

- **Hard to maintain**: Every visual feature (chips, sparklines, edges, tooltips) is imperative Canvas draw calls
- **Hard to extend**: Adding a new chip type or overlay requires touching the JS rendering engine
- **Layout is coupled to rendering**: `GraphMapper.cs` (827 lines) computes positions that are tightly bound to Canvas pixel assumptions
- **Accessibility is bolted on**: Invisible proxy buttons are overlaid on Canvas for keyboard nav
- **No DOM**: Canvas pixels are opaque to the browser — no CSS styling, no DOM inspection, no native text selection

[dag-map](https://github.com/23min/dag-map) is an SVG-based DAG visualizer with:

- **Clean metro-map aesthetic** that's visually appealing for flow topology
- **Pure-function architecture**: `(dag, options) => svg` — zero side effects, framework-agnostic
- **Two layout engines**: Metro (route-decomposition) and Hasse (Sugiyama-style)
- **1,467 lines total** — an order of magnitude simpler than the Canvas renderer
- **SVG output**: DOM-native, CSS-styleable, accessible, inspectable

The goal: run dag-map as an **alternative topology renderer** behind a feature flag, progressively adding FlowTime-specific features (data chips, coloring, interactivity) until it reaches parity.

---

## 2. Current Canvas Architecture (What We're Wrapping)

### Data Flow (Current)
```
API (/state, /graph)
  --> TimeTravelDataService (C#)
    --> TopologyCanvas.razor (Blazor component)
      --> GraphMapper.cs (layout: nodes -> positions)
      --> BuildCanvasPayloads() -> CanvasScenePayload + CanvasOverlayPayload
        --> JSInterop -> topologyCanvas.js (Canvas 2D rendering)
```

### Key Abstractions Already in Place
- **CanvasScenePayload**: Geometry — node positions, edge paths, labels. Changes only when topology changes.
- **CanvasOverlayPayload**: Appearance — colors, sparkline data, chip values. Changes every bin.
- **TopologyOverlaySettings**: Feature flags for what to show (labels, sparklines, edge shares, etc.)
- **ColorScale.cs**: Pure function: `(metrics, basis, thresholds) -> hex color`
- **GraphMapper.cs**: Pure function: `(graph, options) -> positioned nodes`
- **FeatureFlagService.cs**: Existing feature flag mechanism (localStorage-backed)

### What TopologyCanvas Renders
- **Nodes**: Rectangles (services), circles (leaves), with color fill based on ColorScale
- **Data chips**: 12 chip types (arrivals, served, queue, DLQ, retry, parallelism, capacity, constraints, etc.)
- **Edges**: Orthogonal or Bezier paths with optional share labels, multiplier badges, retry indicators
- **Sparklines**: Per-node mini charts (line or bar) showing time-series data
- **Tooltips**: Rich hover tooltips with metric breakdowns
- **Interactivity**: Click to select, hover for tooltips, arrow keys for navigation, zoom/pan

---

## 3. dag-map Capabilities & Gaps

### What dag-map Provides

| Capability | Status | Notes |
|---|---|---|
| SVG rendering | Yes | Pure SVG string output, viewBox-based |
| DAG layout (metro) | Yes | Greedy longest-path route decomposition |
| DAG layout (Sugiyama) | Yes | Via `layoutHasse` (virtual nodes, barycenter crossing reduction) |
| Node rendering | Basic | Circles with labels. No rectangles, no custom content. |
| Edge routing | Yes | Bezier and angular (progressive curve) routing |
| Theming | Yes | 6 built-in themes + custom theme objects. CSS variables optional. |
| Node classes/colors | Yes | `cls` field maps to theme colors |
| LTR and TTB direction | Yes | Coordinate swapping for both orientations |
| Legend | Yes | Auto-generated from node classes |

### What dag-map Does NOT Provide (Gaps for FlowTime)

| FlowTime Requirement | dag-map Status | Gap Severity | Notes |
|---|---|---|---|
| Rectangular nodes (services) | Not supported | **High** | Only renders circles. Need to extend or replace node rendering. |
| Data chips (12 types) | Not supported | **High** | No concept of node decorations or badges. Must be added. |
| Per-node coloring from metrics | Partial | **Medium** | Has class-based coloring, but not dynamic per-bin metric-driven coloring. |
| Sparklines on/near nodes | Not supported | **High** | No embedded SVG charts. Must be added. |
| Edge labels (share %, multiplier) | Not supported | **High** | Edges are plain paths. No label support. |
| Edge overlays (retry rate, heat map) | Not supported | **High** | No edge decorations. |
| Click/hover interactivity | Not supported | **High** | SVG is currently a static string. Must add event listeners. |
| Zoom/pan | Not supported | **Medium** | Must implement viewBox manipulation or SVG transform. |
| Keyboard navigation | Not supported | **Medium** | Must add focusable elements + arrow key handling. |
| Tooltips | Not supported | **Medium** | Must add hover detection + tooltip rendering. |
| Focus view (subgraph) | Not supported | **Medium** | Must filter DAG before passing to layout. |
| Inspector panel integration | Not supported | **Medium** | Must emit selection events back to Blazor. |
| Timeline scrubber sync | N/A | **Low** | Scrubber is external to the renderer; just re-render on bin change. |
| Large graph performance | Unknown | **Medium** | Metro layout is greedy but untested >100 nodes. |

### Architecture Compatibility

**Good news:** dag-map's pure-function design (`dag -> layout -> svg`) maps cleanly to FlowTime's existing payload separation pattern (`graph -> scene payload -> render`). The key insight: we don't need dag-map to render everything. We need dag-map for **layout** and can render the SVG ourselves using dag-map's computed positions.

---

## 4. Proposed Architecture

### Key Design Decision: Use dag-map for Layout, Custom SVG for Rendering

Rather than trying to make dag-map render data chips and sparklines (which would require deep modification of its 192-line renderer), we should:

1. **Use dag-map's layout engine** to compute node positions and edge routes
2. **Render our own SVG** using those positions, with FlowTime-specific node shapes, chips, and decorations
3. **Keep dag-map's edge routing** (bezier/angular) which is already high quality

This gives us the best of both worlds: dag-map's excellent layout algorithm + FlowTime's rich visual vocabulary.

### Proposed Data Flow (SVG Path)
```
API (/state, /graph)
  --> TimeTravelDataService (C#)                    [unchanged]
    --> TopologySvg.razor (NEW Blazor component)
      --> GraphMapper.cs (or new SvgGraphMapper)    [layout adapter]
        --> dag-map layoutMetro()                   [JS: compute positions + edge routes]
      --> SvgSceneBuilder.cs (NEW)                  [C#: build SVG markup from positions + data]
      --> Render as Blazor MarkupString or @((MarkupString)svg)
      --> DOM event handlers via @onclick, @onmouseover [native Blazor, no JSInterop needed]
```

### Why This Is Better Than Canvas for This Use Case

| Concern | Canvas (current) | SVG (proposed) |
|---|---|---|
| Node decorations | Imperative draw calls | Declarative SVG groups — add `<g>` with child elements |
| Data chips | Complex positioning math in JS | `<text>` or `<foreignObject>` with CSS styling |
| Interactivity | Hit-testing + JSInterop callbacks | Native DOM events (@onclick on SVG elements) |
| Accessibility | Proxy buttons overlaid on Canvas | Native SVG `role`, `aria-label`, `tabindex` |
| Styling | Hardcoded colors in JS | CSS classes, potentially CSS transitions |
| Debugging | Canvas inspector (opaque) | DOM inspector (full SVG tree visible) |
| Sparklines | Canvas mini-charts | SVG `<polyline>` or `<path>` — native, scalable |
| Text rendering | Canvas `fillText` (no wrapping) | SVG `<text>` with CSS font properties |

---

## 5. Feature Flag Design

### Flag Mechanism

Extend the existing `FeatureFlagService.cs`:

```csharp
// New flag
public bool UseSvgTopology => GetFlag("ft.useSvgTopology", defaultValue: false);
```

Activated via:
- Query param: `?svg=1`
- localStorage: `ft.useSvgTopology = true`
- (Future) Settings UI toggle in TopologyFeatureBar

### Component Swap

In the Topology page (`Pages/TimeTravel/Topology.razor`):

```razor
@if (FeatureFlags.UseSvgTopology)
{
    <TopologySvg Graph="@graph" NodeMetrics="@nodeMetrics" ... />
}
else
{
    <TopologyCanvas Graph="@graph" NodeMetrics="@nodeMetrics" ... />
}
```

Both components accept the same parameters (same data contract). The SVG version can implement a subset initially and grow toward parity.

### Shared Contract

Both renderers consume:
- `TopologyGraph` (nodes + edges with coordinates)
- `Dictionary<NodeId, NodeBinMetrics>` (current bin metrics)
- `Dictionary<NodeId, NodeSparklineData>` (time-series data)
- `TopologyOverlaySettings` (feature flags, thresholds)
- `ColorScale` (pure function, renderer-agnostic)

This contract is already mostly defined by `TopologyCanvas.razor.cs` parameters. Extract to an interface or shared parameter set.

---

## 6. Milestone Plan

### M-SVG-01: Layout Integration (Foundation)

**Goal:** dag-map computes node positions and edge routes; FlowTime renders minimal SVG with correct topology.

**Tasks:**
1. Add dag-map as a JS dependency (npm package or vendored copy)
2. Create `DagMapInterop.cs` — JSInterop wrapper that calls `layoutMetro(dag, options)` and returns positions + edge routes
3. Create `TopologySvg.razor` component with feature flag swap
4. Map `TopologyGraph` -> dag-map input format (`{ nodes, edges }`)
5. Render basic SVG: rectangles for service nodes, circles for leaf nodes, `<path>` for edges
6. Basic coloring via `ColorScale.cs` (fill color on nodes)
7. Node labels as SVG `<text>`
8. ViewBox computed from layout bounds

**Acceptance:** Toggle `?svg=1`, see correct topology with colored nodes and routed edges. No chips, no sparklines, no interactivity.

**Effort:** 3-5 days

### M-SVG-02: Data Chips & Edge Labels

**Goal:** Nodes display contextual metric badges; edges show flow share labels.

**Tasks:**
1. Implement chip rendering as SVG `<g>` groups positioned relative to node centers
2. Port chip logic from `topologyCanvas.js` `drawChip()` / `drawServiceDecorations()` to C# SVG builder
3. Chip types (progressive): arrivals, served, queue depth, utilization, errors, retry, DLQ, parallelism, capacity, constraints
4. Edge share labels (`<text>` positioned at edge midpoints)
5. Edge multiplier badges
6. Respect `TopologyOverlaySettings` toggles (ShowLabels, ShowEdgeShares, etc.)

**Acceptance:** SVG topology shows the same data chips as Canvas version for common node types. Edge labels visible.

**Effort:** 5-8 days

### M-SVG-03: Interactivity & Scrubbing Performance

**Goal:** Click, hover, keyboard navigation, zoom/pan, inspector integration, smooth timeline scrubbing.

**Tasks:**
1. Node click → `@onclick` on SVG `<g>` → `SelectNode()` → update `focusedNodeId`
2. Node hover → `@onmouseover` → show tooltip (rendered as positioned SVG group or HTML overlay)
3. Keyboard navigation: `tabindex` on node groups, arrow key handling via `@onkeydown`
4. Zoom: SVG viewBox manipulation via mouse wheel
5. Pan: viewBox offset via mouse drag
6. Focus ring on selected node (SVG stroke or filter)
7. Inspector panel integration (same `SelectedNodeId` binding as Canvas)
8. **Timeline scrubbing via scene/overlay split** (see Section 8):
   - Build `nodeId → element` lookup map on scene render (cached until topology changes)
   - Implement `applySvgOverlay()` JS function: targeted `setAttribute` on existing DOM nodes
   - Implement `requestAnimationFrame` coalescing (latest-wins, at most one update per frame)
   - Fixed-width monospace chips to avoid per-frame text measurement
9. **Performance benchmark gate**: 50-node graph, 200 consecutive bin changes, avg < 4ms / p95 < 8ms

**Acceptance:** Full interactive topology equivalent to Canvas for basic workflows. Select node, inspect, navigate, zoom. Timeline scrubbing is visually smooth (no dropped frames at 60fps for 50-node graphs).

**Effort:** 5-8 days

### M-SVG-04: Sparklines & Visual Polish

**Goal:** Per-node sparklines, edge overlays, visual parity with Canvas.

**Tasks:**
1. Sparkline rendering as SVG `<polyline>` or `<path>` positioned below nodes
2. Sparkline modes: line, bar (as SVG `<rect>` bars)
3. Sparkline color coding (SLA basis)
4. Edge overlay: retry rate visualization (opacity or width modulation)
5. Dark mode support via CSS custom properties (leverage dag-map's `cssVars` mode)
6. Node kind-specific rendering (DLQ warning styling, dependency styling, router decorations)
7. Leaf node special styling (circle vs rectangle)
8. Edge types: dashed for dependencies, solid for topology, colored for effort
9. CSS transitions for bin-change updates (optional, if performance allows)

**Acceptance:** Visual parity with Canvas for the standard topology view. Side-by-side comparison should show equivalent information density.

**Effort:** 5-8 days

### M-SVG-05: Feature Parity & Performance

**Goal:** Handle edge cases, large graphs, focus view, class filtering.

**Tasks:**
1. Focus view: filter DAG before passing to dag-map layout (TraverseFocusChain equivalent)
2. Class filtering: filter nodes/edges before layout
3. Node kind filtering (show/hide expression nodes, const nodes, etc.)
4. Layout mode: Layered (metro) vs. HappyPath (filtered DAG)
5. Performance testing with 50-100 node graphs
6. Compare render time Canvas vs SVG for typical models
7. Edge style toggle (Bezier vs Angular from dag-map routing options)
8. Custom node positions (respect `GraphNodeUiModel` X/Y when available)

**Acceptance:** SVG renderer handles all topology filtering/focus modes. Performance acceptable for typical graphs (< 100ms render for 50-node graph).

**Effort:** 3-5 days

---

## 7. Relationship to UI Layout Motors Epic

The existing `docs/architecture/ui-layout/README.md` epic proposes a pluggable `LayoutInput -> LayoutResult` contract. This SVG epic is a **natural first consumer** of that contract:

- **M-SVG-01** implicitly creates the layout abstraction (dag-map input/output mapping)
- **UI Layout Motors** can formalize this into a contract that both Canvas and SVG renderers consume
- If UI Layout Motors ships first, M-SVG-01 becomes simpler (just implement the contract with dag-map)
- If SVG ships first, it provides a concrete implementation to extract the contract from

**Recommendation:** These epics are complementary. The SVG epic can proceed independently and inform the Layout Motors contract design.

---

## 8. Performance Architecture: Timeline Scrubbing

### The Problem

Timeline scrubbing generates 30-60+ bin-change events per second during a fast drag. Each bin changes node colors, chip values, sparkline paths, and edge overlays. This is the single most performance-sensitive operation in the topology view.

### How Canvas Handles It Today

The Canvas renderer already solved this with a **two-tier scene/overlay split**:

1. **Scene payload** — geometry (node positions, edge routes). Rebuilt only when topology changes. Signature-checked; skipped if unchanged.
2. **Overlay payload** — appearance (fill colors, chip text, sparkline data). Rebuilt every bin.

On overlay update, Canvas calls `draw()` which does a **full repaint**: clear the canvas, redraw everything from in-memory state. This is fast because Canvas 2D is immediate-mode — no DOM, no diffing, no reflow. For a 50-node graph, a full repaint is typically **< 2ms**.

### Why Naive SVG Re-rendering Is Too Slow

Three approaches, ranked:

| Approach | Per-frame cost (50 nodes) | Verdict |
|---|---|---|
| **A: Full innerHTML replacement** every bin | 10-20ms (XML parse + DOM create + reflow) | ❌ Will jank |
| **B: Blazor component diffing** (re-render tree) | 8-15ms (C# diff + JSInterop serialization) | ⚠️ Risky, tight budget |
| **C: SVG scene + JS targeted DOM updates** | 1-3ms (setAttribute on existing nodes) | ✅ **Right answer** |

### Approach C: Scene/Overlay Split for SVG

This mirrors the Canvas architecture exactly:

```
TopologySvg.razor
  │
  ├── OnTopologyChanged (rare — graph structure change):
  │     1. Call dag-map layoutMetro() → positions + edge routes
  │     2. Build full SVG string in C# (with data-node-id, CSS classes on all elements)
  │     3. Set container innerHTML via single JSInterop call
  │     4. ≈ 15-30ms — acceptable because it's rare
  │
  └── OnBinChanged (hot path — every scrub tick):
        1. Build overlay delta object in C# (nodeId → {color, chipValues, sparklineD})
        2. Single JSInterop call: applySvgOverlay(container, delta)
        3. JS updates attributes on existing DOM nodes:
           - node fill:     setAttribute('fill', newColor)       → paint only, no reflow
           - chip text:     textContent = '156'                  → paint only, no reflow
           - chip color:    setAttribute('fill', newChipColor)   → paint only, no reflow
           - sparkline:     setAttribute('d', newPathD)          → paint only, no reflow
           - edge opacity:  setAttribute('opacity', newOpacity)  → paint only, no reflow
        4. ≈ 1-3ms for 50 nodes — well within 16ms frame budget
```

**Why this is fast:** Changing `fill`, `textContent`, `opacity`, and `d` on existing SVG elements triggers **repaint only, not reflow**. The browser doesn't recalculate layout — it just repaints affected pixels. No DOM nodes are created or destroyed.

### JS Overlay Updater (Hot Path)

```javascript
// Called every bin during scrubbing — performance-critical
function applySvgOverlay(container, delta) {
  // Node updates: ~5 setAttribute calls per node × 50 nodes = ~250 calls
  for (const [nodeId, m] of Object.entries(delta.nodes)) {
    const g = container.querySelector(`[data-node-id="${nodeId}"]`);
    if (!g) continue;
    g.querySelector('.node-body').setAttribute('fill', m.fill);
    // Chip updates (only for visible chips)
    if (m.chips) {
      for (const [chipCls, chip] of Object.entries(m.chips)) {
        const el = g.querySelector(`.${chipCls}`);
        if (!el) continue;
        el.querySelector('.chip-value').textContent = chip.text;
        if (chip.bg) el.querySelector('.chip-bg').setAttribute('fill', chip.bg);
      }
    }
  }
  // Sparkline updates: single setAttribute('d', ...) per node
  if (delta.sparklines) {
    for (const [nodeId, sp] of Object.entries(delta.sparklines)) {
      const path = container.querySelector(`[data-node-id="${nodeId}"] .sparkline-path`);
      if (path) path.setAttribute('d', sp.d);
    }
  }
  // Edge overlay updates (heat map, retry rate)
  if (delta.edges) {
    for (const [edgeKey, e] of Object.entries(delta.edges)) {
      const path = container.querySelector(`[data-edge-key="${edgeKey}"]`);
      if (path) path.setAttribute('opacity', e.opacity);
    }
  }
}
```

### requestAnimationFrame Coalescing

During fast scrubbing, multiple bin changes may fire before the browser paints. Only the latest matters:

```javascript
let pendingOverlay = null;
let rafId = null;

function scheduleOverlayUpdate(container, delta) {
  pendingOverlay = delta;          // latest wins, older deltas discarded
  if (!rafId) {
    rafId = requestAnimationFrame(() => {
      rafId = null;
      if (pendingOverlay) {
        applySvgOverlay(container, pendingOverlay);
        pendingOverlay = null;
      }
    });
  }
}
```

This guarantees at most **one DOM update per frame** regardless of scrub speed.

### Data Chip Sizing Strategy

When chip text changes length (e.g., "9" → "142"), the background `<rect>` width must adjust. Measuring text at 60fps is expensive. Two options:

**Option A (recommended): Fixed-width chips with monospace font.**
Use a monospace font for chip values. Pre-compute background width for each digit count:
- 1 digit: 16px
- 2 digits: 22px
- 3 digits: 28px
- 4+ digits: 34px

Set the width class via `setAttribute('width', widthForDigitCount(text.length))`. No text measurement needed.

**Option B: CSS-based auto-sizing.**
Use `<foreignObject>` with an HTML `<span>` instead of SVG `<text>`. CSS handles sizing automatically. More flexible but `<foreignObject>` has cross-browser quirks and may not render correctly in exported SVG.

### Performance Estimates

| Graph size | Elements | Overlay updates/frame | Estimated frame time | Headroom |
|---|---|---|---|---|
| 20 nodes | ~200 | ~100 setAttribute | < 1ms | 15ms |
| 50 nodes | ~500 | ~250 setAttribute | 1-3ms | 13ms |
| 100 nodes | ~1000 | ~500 setAttribute | 3-6ms | 10ms |
| 200 nodes | ~2000 | ~1000 setAttribute | 6-12ms | 4ms ⚠️ |

**Conclusion:** Smooth 60fps scrubbing is expected for graphs up to ~100 nodes. For 200+ nodes, performance should be benchmarked and may require optimization (e.g., skip off-screen nodes, reduce chip count, or throttle to 30fps).

### Benchmark Gate (M-SVG-03)

Before M-SVG-03 is considered complete, run this benchmark:

1. Create a 50-node FlowTime topology SVG with full data chips and sparklines
2. Simulate 200 consecutive bin changes at `requestAnimationFrame` speed
3. Measure: **average frame time**, **p95 frame time**, **dropped frame count**

Pass criteria:
- Average frame time < 4ms
- p95 frame time < 8ms
- Zero dropped frames at 60fps

If the benchmark fails, evaluate:
- querySelector caching (build a node-id → element map once, reuse)
- Reducing chip count per node in SVG mode
- Throttling to 30fps during fast scrub (switch to 60fps when scrub stops)

### Comparison: Canvas vs SVG Expected Performance

| Metric | Canvas (current) | SVG (Approach C) |
|---|---|---|
| Scene rebuild (topology change) | 5-10ms | 15-30ms (innerHTML parse) |
| Overlay update (per bin) | 1-2ms (full repaint) | 1-3ms (targeted setAttribute) |
| Memory (50 nodes) | ~2MB (canvas buffer) | ~0.5MB (DOM nodes) |
| Zoom/pan redraw | 1-2ms (full repaint) | 0ms (CSS transform on viewBox) |
| Hit testing (hover) | Custom spatial index | Native browser (pointer events on SVG elements) |
| Accessibility | Proxy buttons overlay | Native DOM (tabindex, aria-label) |

SVG is slightly slower for scene rebuilds but comparable for overlay updates. SVG wins on zoom/pan (CSS transform vs full repaint), hit testing (free via browser), and accessibility (native DOM).

---

## 9. Risks & Mitigations

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| dag-map layout doesn't handle FlowTime's graph shapes well | Medium | High | Test with actual FlowTime topologies early (M-SVG-01). Fall back to GraphMapper positions if needed. |
| SVG scrubbing performance (timeline hot path) | Medium | High | Scene/overlay split architecture (Section 8). Targeted `setAttribute` on existing DOM nodes, not full re-render. Benchmark gate in M-SVG-03: avg < 4ms, p95 < 8ms for 50 nodes. |
| SVG DOM size for large graphs (200+ nodes) | Medium | Medium | ~2000 SVG elements for 200 nodes. Benchmark in M-SVG-05. Mitigations: skip off-screen nodes, reduce chip density, throttle to 30fps. |
| dag-map's route-based layout doesn't match FlowTime's lane-based layout | Medium | Medium | Users will see a different layout. This is acceptable — different aesthetic, same data. Document as intentional. |
| Feature parity takes longer than estimated | High | Low | Feature flag means Canvas remains the default. SVG can ship at any milestone as an opt-in beta. |
| dag-map library maintenance/compatibility | Low | Low | 1,467 lines, zero dependencies, MIT license. Can vendor and maintain internally if needed. |
| querySelector overhead during scrubbing | Low | Medium | Build `nodeId → element` map once on scene render. Reuse during overlay updates. Avoids per-frame DOM queries. |

---

## 9. Success Criteria

1. **M-SVG-01**: A FlowTime user can toggle `?svg=1` and see their topology rendered in SVG with correct structure and colors
2. **M-SVG-02**: The SVG view shows the same data density (chips, labels) as Canvas for common models
3. **M-SVG-03**: A user can interactively explore the SVG topology (click, navigate, zoom) without regression
4. **M-SVG-04**: Visual quality is subjectively "at least as good" as Canvas for demo/presentation use
5. **M-SVG-05**: Performance is acceptable for all current FlowTime models

**Long-term goal:** If the SVG renderer reaches full parity and proves maintainable, consider making it the default and deprecating the Canvas renderer. The feature flag allows a gradual, low-risk transition.

---

## 10. Effort Summary

| Milestone | Effort | Cumulative |
|---|---|---|
| M-SVG-01: Layout Integration | 3-5 days | 1 week |
| M-SVG-02: Data Chips & Labels | 5-8 days | 2-3 weeks |
| M-SVG-03: Interactivity | 5-8 days | 4-5 weeks |
| M-SVG-04: Sparklines & Polish | 5-8 days | 6-7 weeks |
| M-SVG-05: Parity & Performance | 3-5 days | 7-8 weeks |

**Total to feature parity:** ~7-8 weeks (1 developer).
**Total to usable beta (M-SVG-01 + M-SVG-02):** ~2-3 weeks.

The feature flag design means each milestone is independently deployable. Users can opt in at any point and provide feedback.
