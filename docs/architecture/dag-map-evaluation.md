# dag-map Library Evaluation: What to Extend vs. What to Keep FlowTime-Side

> **Date:** 2026-03-23
> **Context:** Evaluating which extensions to [dag-map](https://github.com/23min/dag-map) would benefit the library as a general-purpose DAG visualizer vs. which should remain FlowTime-specific customization.
> **Principle:** Extend the library only with features any DAG consumer would want. FlowTime-specific semantics stay in FlowTime.

---

## 1. Library Assessment

### What dag-map Is
A pure-function, zero-dependency, 1,467-line ES module that computes metro-map-style DAG layouts and renders them as SVG. Input: `{ nodes, edges }`. Output: `{ layout, svg }`.

### What's Genuinely Good

**The layout engine is the real value.** The metro-map algorithm — greedy longest-path route decomposition with Y-occupancy tracking — is novel and produces distinctively attractive results. It's not Sugiyama, not force-directed, not a simple layered layout. It produces something that looks like a transit map, which is intuitively readable for pipeline/workflow DAGs. This is hard to replicate and worth building on.

**The architecture is clean.** Layout and rendering are separate functions. Layout returns a data structure (positions, routes, edge paths). Rendering consumes it. This means a consumer can use just the layout and do custom rendering — which is exactly what FlowTime should do.

**Edge routing is high quality.** Both bezier and angular routing produce smooth, non-overlapping paths. The angular router with progressive curves and interchange-aware direction detection is particularly well-designed.

**Zero dependencies, pure functions, deterministic.** Ideal for integration into any host framework. No state, no side effects, no framework coupling.

### What's Limiting

**Nodes are circles. Period.** The renderer (render.js:111-166) hardcodes circle rendering with three radius tiers (5.5x interchange, 3.5x depth-0/1, 3x deeper). There is no way to render rectangles, boxes with text, or custom shapes. This is the single biggest limitation for any non-transit-map use case.

**Layout treats all nodes as dimensionless points.** Node positions (layout.js:297-303) are computed as `(layer * layerSpacing, routeY)` without accounting for node width or height. For the current circle aesthetic this works, but for any consumer using boxes/cards, nodes can overlap labels and edges.

**No edge labels.** Edges are bare paths. No way to annotate with text (weight, label, percentage). This is a basic feature for most DAG visualizers.

**No interactivity hooks.** SVG output is a flat string. While `data-id` attributes exist on nodes (render.js:129, good!), edges have no identifying attributes at all. There's no way for a consumer to attach event handlers without parsing the SVG structure.

**Several values are hardcoded that should be options.** Font family, margins, padding, legend position, subtitle text. These limit customization without forking.

---

## 2. Proposed Extensions (General-Purpose — Belong in dag-map)

### Extension 1: Custom Node Renderer ⭐ Most Impactful

**What:** Allow consumers to provide a `renderNode` callback that replaces the default circle rendering.

**Why it's general:** Every DAG tool supports custom node shapes. D3-dag, dagre-d3, Mermaid, Graphviz — all allow rectangles, diamonds, custom SVG. The circle-only constraint limits dag-map to transit-map aesthetics. A custom renderer opens it to workflow diagrams, pipeline visualizers, architecture maps, dependency graphs.

**Suggested API:**
```javascript
renderSVG(dag, layout, {
  renderNode: (node, pos, ctx) => {
    // node: { id, label, cls }
    // pos:  { x, y }
    // ctx:  { theme, scale, isInterchange, depth, inDegree, outDegree }
    // return: SVG string (will be inserted into a <g> with data-node-id)
    return `<rect x="${pos.x - 40}" y="${pos.y - 15}" width="80" height="30"
            rx="4" fill="${ctx.theme.classes[node.cls]}" opacity="0.2"/>
            <text x="${pos.x}" y="${pos.y + 4}" text-anchor="middle"
            font-size="${ctx.scale * 5}">${node.label}</text>`;
  }
});
```

**Default behavior:** When `renderNode` is not provided, current circle rendering is used. Zero breaking change.

**Implementation:** In render.js:111-166, wrap the node rendering block in an `if (options.renderNode) { ... } else { ... }` branch. The `<g>` wrapper and `data-node-id` attribute stay library-controlled; only the inner content is delegated.

**Effort:** ~2 hours

### Extension 2: Node Dimensions in Layout

**What:** Allow nodes to specify `width` and `height`. The layout engine uses these for spacing calculations to prevent overlap.

**Why it's general:** Any consumer rendering boxes instead of circles needs the layout to account for box dimensions. Without this, wide labels or tall node cards overlap adjacent nodes. This is a standard feature in Sugiyama-style and layered layout engines.

**Suggested API:**
```javascript
{
  nodes: [
    { id: 'A', label: 'Process Order', cls: 'service', width: 120, height: 40 },
    { id: 'B', label: 'Queue', cls: 'queue', width: 80, height: 30 },
  ],
  edges: [['A', 'B']]
}
```

**Impact on layout:**
- `layerSpacing` calculation: `Math.max(layerSpacing, maxNodeWidth + gap)` to prevent horizontal overlap
- Y-occupancy gap: `Math.max(spacing * 0.8, maxNodeHeight + gap)` to prevent vertical overlap
- Currently layout.js:253 uses `spacing * 0.8` as the gap — should use `Math.max(spacing * 0.8, nodeHeight)` for the nodes in that route's layer range

**Default behavior:** When width/height not provided, use current point-size behavior.

**Effort:** ~4 hours (layout.js changes in spacing, occupancy, and position calculation)

### Extension 3: Edge Labels

**What:** Allow edges to carry a `label` that's rendered at the midpoint of the edge path.

**Why it's general:** Edge labels are one of the most requested features in any graph visualization library. Weight, percentage, transition name, duration — all are common edge annotations.

**Suggested API:**
```javascript
{
  edges: [
    ['A', 'B'],                         // no label (backward compatible)
    ['B', 'C', { label: '92%' }],       // labeled edge
    { from: 'B', to: 'C', label: '92%' } // alternative: object form
  ]
}
```

The tuple form `[from, to, options]` is backward-compatible (existing `[from, to]` tuples still work). Alternatively, support an object form for richer edge metadata.

**Rendering:** Compute midpoint of the edge path. Render `<text>` with small font, rotated to follow the path direction, with a white background rect for legibility.

**Effort:** ~3-4 hours (edge model change, midpoint calculation, text rendering)

### Extension 4: Data Attributes on All SVG Elements

**What:** Add stable, queryable `data-*` attributes to all rendered SVG elements (nodes, edges, routes, legend items).

**Why it's general:** Essential for any integration where the host application needs to add event listeners, apply dynamic styling, or manipulate specific elements. The library already does this partially — `data-id` on circle nodes (render.js:129). But edges and route paths have no identifying attributes.

**Suggested attributes:**
```html
<!-- Node group -->
<g data-node-id="process_order" data-node-cls="service">
  <circle ... />
  <text>Process Order</text>
</g>

<!-- Edge path -->
<path data-edge-from="A" data-edge-to="B" data-route="0" d="M ..." />

<!-- Extra edge (cross-route) -->
<path data-edge-from="C" data-edge-to="D" data-extra-edge="true" d="M ..." />

<!-- Route group -->
<g data-route-id="0" data-route-class="pure">
  <path ... /> <path ... />
</g>
```

**Implementation notes:**
- Wrap each node's SVG elements in a `<g data-node-id="..." data-node-cls="...">` group
- Add `data-edge-from` and `data-edge-to` to edge `<path>` elements (both route edges and extra edges)
- Add `data-route-id` to route path groups

**Default behavior:** Always present. No opt-out needed — data attributes are invisible to users.

**Effort:** ~1-2 hours

### Extension 5: Font and Margins as Theme/Options

**What:** Move hardcoded values to theme or options:
- Font family (currently `'IBM Plex Mono', 'Courier New', monospace` in render.js:76)
- Margins (currently `{left: 50*s, right: 40*s}` in layout.js:276)
- Padding (currently `{top: 50*s, bottom: 80*s}` in layout.js:294-295)
- Subtitle text (currently hardcoded in render.js:80)

**Why it's general:** Any consumer embedding dag-map in a larger UI needs to control spacing and typography. Hardcoded IBM Plex Mono won't match most host applications.

**Suggested API:**
```javascript
// In theme:
{ font: "'Inter', sans-serif", ... }

// In options:
{
  margin: { top: 20, left: 30, right: 30, bottom: 20 },
  subtitle: 'Custom subtitle text',  // or null to hide
}
```

**Effort:** ~1-2 hours

### Extension 6: Input Validation

**What:** Validate the DAG before layout. Detect and report:
- Cycles (topological sort will silently drop cyclic nodes)
- Edges referencing non-existent nodes
- Duplicate node IDs
- Self-loops

**Why it's general:** Silent failure is the worst behavior for a library. Currently, if a consumer passes a graph with a cycle, the layout silently omits the cyclic nodes with no warning. This produces confusing, incomplete visualizations.

**Suggested behavior:**
```javascript
// Option 1: throw
layoutMetro(dag); // throws: "dag-map: cycle detected involving nodes [A, B, C]"

// Option 2: return warnings in layout result
const layout = layoutMetro(dag);
layout.warnings // ["Cycle detected: A → B → C → A", "Edge references unknown node: Z"]
```

Option 2 is more library-friendly (non-breaking, doesn't change control flow).

**Effort:** ~2 hours

### Extension 7: Custom Edge Renderer

**What:** Allow consumers to provide a `renderEdge` callback, similar to `renderNode`.

**Why it's general:** Same rationale as custom node rendering. Consumers may want dashed edges, colored edges based on status, animated edges, or edges with custom decorations.

**Suggested API:**
```javascript
renderSVG(dag, layout, {
  renderEdge: (edge, segment, ctx) => {
    // edge:    { from, to, label?, ... }
    // segment: { d, color, thickness, opacity, dashed }
    // ctx:     { theme, scale, isExtraEdge, routeIndex }
    // return: SVG string (complete <path> or group)
    return `<path d="${segment.d}" stroke="${segment.color}"
            stroke-width="${segment.thickness}" fill="none"
            marker-end="url(#arrowhead)"/>`;
  }
});
```

**Effort:** ~2 hours

---

## 3. Extensions to NOT Add (FlowTime-Specific)

These features are tempting but would pollute the library with domain-specific concepts:

| Feature | Why It's FlowTime-Specific |
|---|---|
| **Data chips** (metric badges on nodes) | Flow analysis concept. General DAG tools don't show queue depth or utilization on nodes. Implement via `renderNode` callback. |
| **Sparklines** (time-series mini-charts on nodes) | Time-series visualization is orthogonal to DAG layout. Implement via `renderNode` or FlowTime-side SVG overlay. |
| **Dynamic per-bin coloring** (ColorScale) | FlowTime's bin-by-bin metric coloring is domain-specific. The library should support per-node colors (via `renderNode` or a `color` field), but the logic mapping metrics to colors belongs in FlowTime. |
| **Inspector panel integration** | UI framework concern. Library should emit data attributes; FlowTime handles selection and inspection. |
| **Focus view / subgraph traversal** | FlowTime-specific graph filtering. Filter the DAG before passing to dag-map. |
| **Timeline scrubber** | Time-series playback. Completely orthogonal to DAG layout. |
| **Topology overlay settings** | FlowTime's specific configuration surface. Not a library concern. |
| **Zoom/pan** | Host-side SVG viewport management. The library produces a viewBox-based SVG that scales naturally. Zoom/pan is a wrapper concern. |
| **Keyboard navigation** | Accessibility concern for the host application. Library can help by providing `data-*` attributes and `tabindex`; actual key handling is host-side. |
| **Arrowheads on edges** | Borderline. Could be a library option (`options.arrowheads: true`), but the implementation is simple enough that consumers can add it via `renderEdge` or SVG `<defs>` + `marker-end`. |

---

## 4. Implementation Priority

Ranked by impact-to-effort ratio and general utility:

| Priority | Extension | Effort | Impact | Reasoning |
|---|---|---|---|---|
| **1** | Data attributes on all elements | 1-2 hrs | High | Enables all host-side interactivity. No API change. Foundation for everything else. |
| **2** | Custom node renderer | 2 hrs | **Very High** | Unlocks dag-map for any use case beyond transit maps. Single biggest value add. |
| **3** | Font and margins as options | 1-2 hrs | Medium | Low effort, removes the most common reasons to fork. |
| **4** | Node dimensions in layout | 4 hrs | High | Required for correct rendering of rectangular nodes. Without this, custom node renderers produce overlapping output. |
| **5** | Input validation | 2 hrs | Medium | Prevents silent failures. Good library citizenship. |
| **6** | Edge labels | 3-4 hrs | Medium | Common need, clean API extension. |
| **7** | Custom edge renderer | 2 hrs | Medium | Completes the customization story alongside renderNode. |

**Total effort for all 7: ~15-18 hours (2-3 days).**

### Suggested Sequencing

**Batch 1 (Day 1):** Extensions 1, 2, 4 — Data attributes + custom node renderer + font/margins. These are the minimum for FlowTime integration.

**Batch 2 (Day 2):** Extensions 3, 5 — Node dimensions + input validation. These make the library robust for rectangular-node use cases.

**Batch 3 (Day 3):** Extensions 6, 7 — Edge labels + custom edge renderer. Nice-to-haves that complete the customization API.

---

## 5. How FlowTime Would Use the Extended Library

With extensions 1-4 in place, the FlowTime integration pattern becomes:

```javascript
import { layoutMetro, renderSVG } from 'dag-map';

// 1. Map FlowTime topology to dag-map input
const dag = {
  nodes: topologyNodes.map(n => ({
    id: n.id,
    label: n.name,
    cls: mapKindToClass(n.kind),
    width: 100,   // Extension 2
    height: 36,
  })),
  edges: topologyEdges.map(e => [e.from, e.to]),
};

// 2. Compute layout (dag-map does the hard work)
const layout = layoutMetro(dag, {
  routing: 'angular',
  theme: 'dark',
  direction: 'ltr',
  scale: 1.5,
});

// 3. Render with FlowTime-specific node content (Extension 1)
const svg = renderSVG(dag, layout, {
  font: "'Inter', system-ui, sans-serif",  // Extension 5
  showLegend: false,                        // FlowTime has its own legend
  subtitle: null,                           // No generic subtitle
  renderNode: (node, pos, ctx) => {
    // FlowTime renders: colored rect + label + data chips + sparkline
    return buildFlowTimeNodeSvg(node, pos, ctx, currentBinMetrics);
  },
});

// 4. Insert into DOM (Blazor MarkupString)
// 5. Attach event handlers using data-node-id attributes (Extension 4)
```

The key insight: **dag-map owns layout and routing; FlowTime owns node content and interactivity.** This is a clean separation with no library pollution.

---

## 6. Minor Issues to Fix While We're In There

| Issue | Location | Fix |
|---|---|---|
| Function name `dag-map` is invalid JS | index.js:39 | Rename to `dagMap` (add `dag-map` alias for backward compat) |
| `data-id` only on circles, not on text labels | render.js:129 | Wrap each node in `<g data-node-id>` (part of Extension 4) |
| Legend hardcodes exactly 4 classes | render.js:174-177 | Derive legend entries from `Object.keys(theme.classes)` |
| Subtitle text is hardcoded marketing copy | render.js:80 | Make it `options.subtitle` (part of Extension 5) |
| `CLASS_COLOR` and `C` exports are backward-compat dead weight | layout.js:15-25, index.js:13 | Keep for now, mark as deprecated |

---

## 7. Conclusion

dag-map's layout engine is genuinely good and worth building on. The renderer is the weak point — it's tightly coupled to the transit-map aesthetic. The 7 proposed extensions transform dag-map from a niche transit-map visualizer into a general-purpose DAG layout + rendering library, without adding any domain-specific features.

The effort is modest (~2-3 days), the API surface stays clean, and every extension benefits any consumer of the library — not just FlowTime. Most importantly, extensions 1 and 4 (custom node renderer + data attributes) provide exactly the hooks FlowTime needs without the library knowing anything about flow analysis, data chips, or sparklines.

**Recommendation:** Implement Batch 1 (day 1) first, then integrate into FlowTime as a proof of concept. The remaining batches can be done as needed based on what the integration reveals.
