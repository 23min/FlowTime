# Milestone: Live Topology and Charts

**ID:** m-E17-03
**Epic:** E-17 Interactive What-If Mode
**Status:** in-progress
**Branch:** `milestone/m-E17-03-live-topology-and-charts` (off `main`)
**Depends on:** m-E17-02 (Svelte parameter panel)

## Goal

The What-If page shows a **topology graph** and **time-series charts** that both update reactively when parameters change. Topology node colors reflect current metrics (heatmap); charts show full time series with axes and hover tooltips. This is the "wow" moment: the user drags a slider, watches the graph recolor, and sees the chart curves animate in real time.

## Context

After m-E17-02, the What-If page has auto-generated parameter controls, reactive state, debounced eval, and sparklines that show series shapes as small SVG paths. The data pipeline is fully working and sub-millisecond fast. What's missing is the **visual richness** that makes the interactivity compelling:

- **Topology graph**: no visualization yet. The user sees series names and values as text. For topology-heavy models (queue, class decomposition), understanding which node feeds which is impossible without a graph.
- **Charts**: sparklines are 140×28 pixels. They convey shape but not exact values, no axes, no hover, no legend. For longer series or comparison across metrics, they're insufficient.

m-E17-03 replaces both: a proper topology graph (using the existing `dag-map-view` component with metric overlays) and a richer chart component (SVG-based, no external library) with axes, hover tooltips, and multi-series support.

**Layout stability is critical.** The graph must re-layout only on *structural* changes (model switch, new parameters). On *value* changes (slider drag), only colors and chart data update — nodes don't move, edges don't rewire. This preserves the user's mental map and makes the experience feel alive rather than jittery.

## Acceptance Criteria

1. **AC-1: Topology graph renders.** When a compiled model has 2+ nodes, the What-If page shows a DAG-style graph alongside the parameter panel and series list. Uses the existing `dag-map-view.svelte` component. Graph structure is derived from the compiled model's nodes + dependencies (not from a separate HTTP call).

2. **AC-2: Graph structure from compile response.** The engine session `compile` command's response includes a `graph` field: `{ nodes: [{id, kind}], edges: [{from, to}] }`. The server derives this from the parsed model definition. The Svelte client stores it in state and passes it to the graph renderer.

3. **AC-3: Layout stability on value changes.** Dragging a slider does NOT trigger a graph re-layout. The dag-map-view's computed positions are preserved across evals. This is verified by a Playwright test that checks node positions are identical before and after a parameter tweak.

4. **AC-4: Reactive heatmap.** Each topology node is colored by its current series value (e.g., `served` for expression nodes, `queue_depth` for queue nodes). Colors update when the series store updates. A Svelte-derived store maps series → `Map<nodeId, NodeMetric>` for the graph overlay.

5. **AC-5: Metric selection.** A small dropdown above the graph lets the user pick which metric drives the heatmap (e.g., "served", "queue_depth", "utilization"). The dropdown lists all non-internal series. Default selection is the first primary output series.

6. **AC-6: Time-series chart component.** A new `<Chart>` component replaces the inline sparkline usage in the series panel:
   - SVG-based, no external library
   - Configurable width/height, default 300×120
   - X-axis: bin index labels
   - Y-axis: min/max labels (auto-scaled)
   - Hover: shows bin index + exact value at mouse position
   - Smooth line with optional point markers

7. **AC-7: Chart updates are reactive.** When series data changes, the chart re-renders in place. The axis auto-scales to the new range. No flicker, no re-mount.

8. **AC-8: Multi-series overlay (optional per chart).** A chart can render multiple series as overlapping lines with different colors and a legend. Default: one series per chart. Overlay mode is used for related metrics (e.g., arrivals vs served).

9. **AC-9: Chart hover tooltip.** On mouseover, a crosshair line shows the current bin index, and a tooltip displays `{name}: {value}` for each series rendered in that chart. The tooltip follows the cursor and does not cause layout shift.

10. **AC-10: Pure unit tests.** Vitest unit tests cover:
    - Graph derivation from compile response (node/edge mapping)
    - Metric map computation (series → heatmap metric)
    - Chart path computation (extending sparkline-path tests to include axis-aware layout)
    - Hover bin index resolution (cursor x → nearest bin)
    No DOM-level component tests required; pure functions only.

11. **AC-11: Playwright E2E.** Extend `tests/ui/specs/svelte-what-if.spec.ts` with:
    - Topology graph renders when model is loaded
    - Drag slider → graph colors change (verify a specific node's fill attribute changes)
    - Drag slider → node positions don't change (layout stability)
    - Model switch → graph re-layouts (new structure)
    - Chart hover → tooltip appears with correct values

12. **AC-12: Visual latency unchanged.** Tweaking a parameter to seeing the graph/chart update should still feel instant. Reuse the existing `Last eval: N µs` badge — it should stay under 1000 µs for the simple-pipeline model even with the new rendering.

## Technical Notes

### Graph in compile response

Extend `engine/cli/src/protocol.rs` `CompileResult`:

```rust
pub struct CompileResult {
    pub params: Vec<ParamInfo>,
    pub series: HashMap<String, Vec<f64>>,
    pub bins: usize,
    pub grid: GridInfo,
    pub graph: GraphInfo,  // NEW
}

pub struct GraphInfo {
    pub nodes: Vec<GraphNodeInfo>,
    pub edges: Vec<GraphEdgeInfo>,
}

pub struct GraphNodeInfo {
    pub id: String,
    pub kind: String,  // "const", "expr", "pmf", "queue", "router"
}

pub struct GraphEdgeInfo {
    pub from: String,
    pub to: String,
}
```

Graph derivation in session:
- Iterate model.nodes → collect (id, kind) for `nodes`
- For expr nodes → parse the expression to find referenced nodes → edges
- For topology.nodes (queue/service) → add node with kind="queue", edges from semantics.arrivals source to the queue, from queue to semantics.served target
- For router nodes → edges from `inputs.queue` source to each route target

This is a one-shot derivation — cached per compile, not recomputed on eval.

### Client-side graph type mapping

In `ui/src/lib/api/engine-session.ts`:

```typescript
export interface CompileResult {
    params: ParamInfo[];
    series: Record<string, number[]>;
    bins: number;
    grid: GridInfo;
    graph: EngineGraph;
}

export interface EngineGraph {
    nodes: { id: string; kind: string }[];
    edges: { from: string; to: string }[];
}
```

In the what-if page, adapt to the `GraphResponse` shape that `dag-map-view` expects:

```typescript
function adaptGraph(eg: EngineGraph): GraphResponse {
    return {
        nodes: eg.nodes.map(n => ({ id: n.id, kind: n.kind })),
        edges: eg.edges.map((e, i) => ({ id: `edge-${i}`, from: e.from, to: e.to })),
        order: [],  // dag-map-view computes its own order
    };
}
```

### Metric map

Pure function in `ui/src/lib/api/topology-metrics.ts`:

```typescript
export function buildMetricMap(
    series: Record<string, number[]>,
    metricName: string,
    bins: number,
): Map<string, { value: number; label?: string }> {
    const map = new Map();
    for (const [seriesName, values] of Object.entries(series)) {
        if (isInternalSeries(seriesName)) continue;
        // Use the mean over all bins as the heatmap value
        const mean = values.reduce((a, b) => a + b, 0) / values.length;
        map.set(seriesName, { value: mean, label: seriesName });
    }
    return map;
}
```

Initially: heatmap colors every node by its series' mean. Future enhancement: let the user pick a bin slider (scrub through time).

### Chart component

New `ui/src/lib/components/chart.svelte`:
- SVG element sized to viewport
- Pure derivation: `computeChartGeometry(values, { width, height, padding })` → `{ path, xTicks, yTicks, xScale, yScale }`
- Hover: listen to `mousemove`, compute `binFromX(mouseX)`, render crosshair line + tooltip
- Multi-series: accept `series: { name, values, color }[]` as prop

Keep the pure geometry in `chart-geometry.ts`, testable without DOM. Follow the sparkline-path.ts pattern.

### Layout stability

`dag-map-view.svelte` uses `$derived.by(() => ...)` to compute the DAG + layout when `graph` changes. As long as the parent passes the *same* graph object reference across evals (not a new one each time), Svelte's reactivity won't retrigger layout. Use `let graph = $state<GraphResponse | null>(null)` and only assign it in `compileModel`, never in `runEval`.

Verify in Playwright by reading `cx`/`cy` (or `transform`) attributes of node elements before and after a slider drag — they should be identical.

### Playwright selectors

Add `data-testid` to the graph container and tooltip in the component. For the dag-map-view, the nodes render as SVG elements; use `[data-node-id="..."]` or similar. Check what dag-map-view already exposes.

## Out of Scope

- Animated transitions between values (ease-in color changes) — the page already feels instant at 42µs/eval; animation is pure polish and can come later
- Zooming/panning the graph — future
- Time scrubber (pick a specific bin for the heatmap) — future
- Export chart as image — future
- Comparison mode (multiple parameter snapshots side-by-side) — future
- Editing graph structure in the UI — out of scope forever; models are YAML-authored
- Replacing the chart component library later — we'll add uPlot or ECharts if the custom SVG chart proves insufficient, but that's a future concern

## Success Indicator

Load the **queue-with-wip** model → see the topology graph with queue node and its inputs → drag the WIP limit slider → watch the queue node's color shift from green to orange as the queue depth increases → watch the `queue_queue` chart curve flatten at the new WIP cap. All in under 100ms from drag to visual update.

## Key References

- `ui/src/lib/components/dag-map-view.svelte` — existing topology renderer
- `ui/src/lib/api/types.ts` — `GraphResponse`, `GraphNode`, `GraphEdge` types
- `ui/src/routes/what-if/+page.svelte` — target page for new additions
- `ui/src/lib/components/sparkline-path.ts` — pattern for pure geometry functions
- `ui/src/lib/api/engine-session.ts` — WebSocket client to extend with graph type
- `engine/cli/src/protocol.rs` — session protocol types to extend
- `engine/cli/src/session.rs` — session compile handler to extend
- `work/epics/E-17-interactive-what-if-mode/m-E17-02-svelte-parameter-panel.md` — prior milestone
