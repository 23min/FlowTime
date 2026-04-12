# Milestone: Edge Heatmap

**ID:** m-E17-05
**Epic:** E-17 Interactive What-If Mode
**Status:** in-progress
**Branch:** `milestone/m-E17-05-edge-heatmap` (off `epic/E-17-interactive-what-if-mode`)
**Depends on:** m-E17-04 (Warnings Surface)

## Goal

Color the edges in the What-If topology graph by the throughput they carry, so the user can immediately see where flow is heavy, where it is throttled, and how it redistributes when parameters change.

## Context

After m-E17-04, the topology graph colors **nodes** by their series mean (utilization, queue depth, served rate). The edges between nodes are currently monochrome — a missed opportunity to show the *flow* on each connection.

The `dag-map-view` component already accepts an `edgeMetrics?: Map<string, NodeMetric>` prop — the rendering support exists but has never been wired up. This milestone computes the edge metric map from the series data and passes it in.

**What "flow on an edge" means here.** For an edge from node A to node B, the flow is the output series of A — the rate at which A produces values that travel toward B. This is the same series used to color A as a node, applied to its outgoing edges. The interpretation:

- `const` node A → expr node B: A's series (the constant value) is the flow on the edge.
- `expr` node A → topology node B (as `semantics.arrivals`): A's series is the arrival rate flowing into B.
- Topology node Q → whatever consumes Q's served output: Q's `served` series (the `{snake(Q)}_served` column, or Q's own primary series if that's what the heatmap uses) is the edge flow.

This interpretation is consistent and symmetric: a node that feeds into multiple downstream nodes will color all its outgoing edges with its own output series. The user sees "this value leaves A, flows along each edge to downstream nodes."

## Acceptance Criteria

1. **AC-1: Edge metric map computation.** A new pure function `buildEdgeMetricMap` in `ui/src/lib/api/topology-metrics.ts`:

   ```typescript
   export function buildEdgeMetricMap(
       graph: EngineGraph,
       series: Record<string, number[]>,
   ): MetricMap;
   ```

   For each edge `{ from, to }` in `graph.edges`:
   - Look up the `from` node's primary series using the same `findNodeSeries` logic used for nodes.
   - Compute the mean across all bins.
   - Insert into the map with key `${from}->${to}` (see Technical Notes for key format).
   - If no series is found for `from`, omit the edge from the map (edge renders with default styling).

2. **AC-2: Edge metric map normalization.** `normalizeMetricMap` (already exists, normalizes to `[0, 1]`) is applied to the raw edge metric map before passing to `dag-map-view`. The function is shared — no duplication.

3. **AC-3: Edge metrics wired into `dag-map-view`.** In `+page.svelte`, a new `$derived.by` computes the normalized edge metric map and passes it as `edgeMetrics` to `<DagMapView>`. The map recomputes when `series` or `engineGraph` changes.

4. **AC-4: Layout unaffected.** Adding `edgeMetrics` does not trigger a DAG re-layout. The `edgeMetrics` prop is consumed only in `renderSVG`, not in `layoutMetro`. This is the existing separation in `dag-map-view.svelte`. Verify: the `layout` derived store does not read `edgeMetrics`.

5. **AC-5: Visual result.** Load the `capacity-constrained` model. The edge from `arrivals` to `Service` is colored (not gray/default). Dragging the `arrivals` slider shifts the edge color intensity. This is a visual smoke-test via Playwright.

6. **AC-6: Edge key format confirmed.** The `dag-map` library's `edgeMetrics` key format is `${fromId}\u2192${toId}` — the Unicode right-arrow character `→` (`\u2192`) between node IDs (confirmed in `dag-map/src/render.js:151`). The `buildEdgeMetricMap` function uses this exact format. A comment in the source records this.

7. **AC-7: Pure unit tests.** Vitest tests in `ui/src/lib/api/topology-metrics.test.ts` cover:
   - `buildEdgeMetricMap` with a simple two-node graph where `from` has a known series → map entry present with correct mean.
   - `buildEdgeMetricMap` with an edge whose `from` node has no series → edge omitted from map.
   - `buildEdgeMetricMap` with a multi-edge graph → correct keys for all edges.
   - Map has same size as number of edges with known series (no extra entries).
   - At least 8 tests total for the new function.

8. **AC-8: Playwright E2E.** Extend `tests/ui/specs/svelte-what-if.spec.ts`:
   - After model load, the topology graph SVG contains at least one colored (non-default) edge element.
   - Dragging `arrivals` slider on `capacity-constrained` model changes an edge's visual attribute (color or stroke).
   - Layout stability: edge positions (path data or endpoint coords) do not change when a parameter is tweaked — only fill/stroke attributes change.

9. **AC-9: No regression on prior ACs.** All 173 vitest and 19 Playwright tests from m-E17-04 continue to pass.

## Technical Notes

### Edge key format in dag-map

Before implementation, inspect the `dag-map` package to determine the key format for `edgeMetrics`. The two likely candidates are:

- **`${from}->${to}`** — matches the deduplication key used inside `dag-map-view.svelte` (`const key = \`${from}->${to}\``).
- **`e-${index}`** — matches the synthesized edge IDs in `graph-adapter.ts`.

Run a quick spike: pass a known edge metric with both key formats to a rendered `<DagMapView>` in the engine-test page and observe which one colors the edge. Record the confirmed format as a comment near `buildEdgeMetricMap`.

If `dag-map` uses a different format entirely, adapt accordingly and note it.

### `findNodeSeries` reuse

`findNodeSeries` is currently a private function in `topology-metrics.ts`. Export it (or extract the lookup logic into a shared internal helper) so `buildEdgeMetricMap` can call it without duplicating the lookup order:

1. `series[nodeId]` — exact match
2. `series[\`${toSnakeCase(nodeId)}_queue\`]` — topology queue column
3. `undefined` — omit

### Edge metric map key example

For a model with:
- `arrivals` const → `Service` topology node
- `served` expr → `Service.semantics.served`

The `EngineGraph.edges` produced by the Rust session will include `{ from: "arrivals", to: "Service" }`.

The map entry: `"arrivals->Service" → { value: mean(series["arrivals"]), label: "arrivals" }`.

### Page-level derived store

In `+page.svelte`:

```typescript
const edgeMetricMap = $derived.by(() => {
    if (!engineGraph) return new Map();
    return normalizeMetricMap(buildEdgeMetricMap(engineGraph, series));
});
```

Pass to `<DagMapView graph={graphResponse} metrics={metricMap} edgeMetrics={edgeMetricMap} />`.

The `edgeMetricMap` reacts to `series` changes on every eval — colors update in real time without re-layout.

## Out of Scope

- Per-bin edge coloring (time scrubber drives this — m-E17-06).
- Edge labels (flow rate text on edges) — visual noise at current scale.
- Animated edge color transitions.
- Edge click / inspect interaction.
- Separate edge metric selector (use same heatmap metric as nodes for now).

## Success Indicator

Load `queue-with-wip` model → both nodes and edges have heatmap colors. Drag the WIP limit slider — node colors shift AND edge colors shift in tandem, both within 100ms. The topology graph "lights up" the whole flow network.

## Key References

- `ui/src/lib/api/topology-metrics.ts` — `buildMetricMap`, `normalizeMetricMap`, `findNodeSeries` (to export)
- `ui/src/lib/components/dag-map-view.svelte` — `edgeMetrics` prop (already present)
- `ui/src/lib/api/graph-adapter.ts` — `adaptEngineGraph` (edge ID synthesis)
- `ui/src/routes/what-if/+page.svelte` — page to update
- `work/epics/E-17-interactive-what-if-mode/m-E17-04-warnings-surface.md` — prior milestone
