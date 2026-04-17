# Milestone: Metric Selector & Edge Cards

**ID:** m-E21-02-metric-selector-edge-cards
**Epic:** E-21 — Svelte Workbench & Analysis Surfaces
**Status:** in-progress

## Goal

Complete the workbench as a general inspection tool by adding a metric selector chip bar (choose which metric colors the topology), edge click-to-pin with edge cards, and a class filter dropdown.

## Context

m-E21-01 delivered the workbench foundation: density system, dag-map click/hover events, and node cards with utilization-based heatmap coloring. But the topology only colors by utilization (hardcoded), there's no way to inspect edges, and class filtering doesn't exist.

This milestone adds the remaining "what am I looking at?" controls that the Blazor feature bar provided (15+ toggles) — but in the simplified workbench paradigm: one metric selector, one class filter, and edge inspection via pinning.

## Acceptance Criteria

### Metric selector (AC1-AC3)

1. **Chip bar renders below the toolbar.** A horizontal row of metric chips: Utilization, Queue Depth, Arrivals, Served, Errors, Flow Latency. One active at a time (radio behavior). Default: Utilization.

2. **Selecting a metric changes the topology heatmap coloring.** Each chip maps to a specific field in the state API response (`derived.utilization`, `metrics.queueDepth`, `metrics.arrivals`, `metrics.served`, `metrics.errors`, `derived.flowLatencyMs`). Selecting a chip re-extracts metrics from the current state data and passes them to DagMapView. Node metric labels update accordingly (e.g., "85%" for utilization, "14.5" for queue depth).

3. **Workbench card sparklines reflect the selected metric.** When the selected metric changes, the sparkline in each pinned workbench card updates to show that metric's values over the full time window (requires fetching state window data or caching per-bin values).

### Edge cards (AC4-AC6)

4. **Clicking an edge in the topology pins it to the workbench.** Uses the `bindEvents` `onEdgeClick` callback. Pinned edges appear as cards in the workbench alongside node cards. Clicking a pinned edge again unpins it.

5. **Edge card content.** Each edge workbench card shows:
   - Source and target node IDs
   - Flow volume at current bin (from state API edge data if available, or from node-level served/arrivals)
   - Sparkline of flow volume over time (if data available)
   - Compact layout matching node cards

6. **Edge selection indicator in DAG.** Pinned edges get a visual highlight in the topology (e.g., brighter color, thicker stroke, or glow). This uses a CSS class approach since dag-map doesn't have an `selectedEdges` option — the Svelte wrapper applies the class after render.

### Class filter (AC7-AC8)

7. **Class filter dropdown appears when classes exist.** If the current run has per-class data (any node's state includes `byClass` entries), a dropdown/chip filter appears in the toolbar area. Lists all class IDs found in the data. Multi-select: toggle individual classes on/off.

8. **Class filter controls topology visibility.** When classes are filtered, the topology heatmap shows metrics for only the selected classes (using `byClass[classId]` data instead of aggregate). If no class filter is active, show aggregate (default behavior).

### Cross-cutting (AC9-AC10)

9. **Vitest coverage for new helpers.** Metric extraction by selected metric, edge data extraction, class discovery from state data — all have vitest tests.

10. **Existing Playwright specs still pass.** The m-E21-01 workbench specs and E-17 what-if specs continue to work.

## Technical Notes

- The metric selector state lives in the workbench store (or a co-located topology store) — persists across bin scrubs but resets on run change.
- For sparkline data across all bins: the simplest approach is to cache metric values per node as each bin loads. When the user scrubs, accumulate values. Full-window sparklines need either a state_window API call or progressive accumulation. Start with progressive (show what's been visited), upgrade to full-window fetch if it feels incomplete.
- Edge data in the FlowTime state API: check if `/v1/runs/{id}/state?bin=N` returns edge-level metrics. If not, edge cards show source→target label only with node-level served/arrivals as proxy metrics.
- Class discovery: scan `stateNodes[].byClass` keys across all nodes to build the class list.

## Out of Scope

- Analysis tab surfaces (m-E21-03/04)
- Heatmap view (m-E21-05)
- Validation surface (m-E21-06)
- New dag-map layout changes
- Edge metric labels on the DAG itself (edges show color only, detail in workbench)

## Dependencies

- m-E21-01 (complete) — workbench foundation, dag-map events, density system
- FlowTime API state endpoint — already available on port 8081

## Coverage Notes

Branches that are defensive or reachable only under rare runtime conditions; explicitly noted here per the branch-coverage rule.

### Pure logic (unit-tested)

- `metric-defs.ts` — every reachable branch of `extractMetricValue`, `extractMetricValueFiltered`, `buildMetricMapForDef`, `buildMetricMapForDefFiltered`, `buildSparklineSeries`, `extractSeriesValues`, `discoverClasses`, `computeTicks`, `computePointerPct` has at least one explicit vitest test. Includes non-object intermediates (string, number), path-prefix stripping variants, class-filter empty/present/mixed-finite, array-length caps, non-array values, null/NaN/Infinity rejection, and empty-result short-circuits.
- `workbench-metrics.ts` — `extractNodeMetrics`, `extractEdgeMetrics`, `findHighestUtilizationNode` — every null/undefined/non-finite/negative-errors/zero-failures branch covered.
- `sparkline-path.ts` — NaN/Infinity gap handling covered for start/mid/end positions; all-NaN and empty cases short-circuit tested.
- `workbench.svelte.ts` — full state machine tested in `workbench.svelte.test.ts` (pin/unpin/toggle idempotency, edge direction, metric reset on clear).

### Component rendering (Playwright-covered)

- `metric-selector.svelte` — active vs inactive chip styling: covered by the "metric selector changes topology coloring" Playwright test.
- `workbench-card.svelte` + `workbench-edge-card.svelte` — header/metrics/sparkline conditionals: covered by the click-to-pin Playwright tests.
- `timeline-scrubber.svelte` — pointer rendering, controls rendering, drag state: covered by the topology-loads Playwright test (controls present in topology mode) plus the what-if page integration (pointer-hidden + no-controls mode, exercised by the existing what-if Playwright spec).

### Defensive / prop-default branches (documented, not explicitly tested)

- `workbench-card.svelte` and `workbench-edge-card.svelte`: the `onClose === undefined` path (renders no close button). In production the topology page always supplies `onClose`; the optional prop exists so the component can be embedded in a read-only context later. Deleting the prop would require a contract change; keeping it is minimal-cost defense.
- `workbench-card.svelte`: the `currentBin === undefined` path (no vertical indicator on sparkline). Auto-pin always supplies `currentBin`; the branch exists because the prop is optional.
- `timeline-scrubber.svelte`: `binCount <= 1` disables the track. Every caller today only renders the component when `binCount > 0` and typically `> 1`; the tick helper still handles `binCount <= 1` correctly (tested in `computeTicks`).

These branches are intentionally defensive — removing them would force callers to guard, which is worse. They are noted here rather than tested via component-driver scaffolding since the logic they guard is trivial (render vs. don't render) and the prop interface is stable.
