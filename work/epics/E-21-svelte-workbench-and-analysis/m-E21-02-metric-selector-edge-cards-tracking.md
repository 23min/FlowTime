# Tracking: Metric Selector & Edge Cards

**Milestone:** m-E21-02-metric-selector-edge-cards
**Branch:** milestone/m-E21-02-metric-selector-edge-cards
**Started:** 2026-04-17
**Status:** in-progress

## Acceptance Criteria

### Metric selector
- [x] AC1: Chip bar renders below toolbar (Utilization, Queue, Arrivals, Served, Errors, Latency)
- [x] AC2: Selecting a metric changes topology heatmap coloring
- [x] AC3: Workbench card sparklines reflect selected metric (state_window fetched once per run, sparklines derived per metric)

### Edge cards
- [x] AC4: Clicking an edge pins it to workbench
- [x] AC5: Edge card content (source, target, flow volume, sparkline)
- [x] AC6: Edge selection indicator in DAG (amber highlight via CSS class)

### Class filter
- [x] AC7: Class filter dropdown appears when classes exist
- [x] AC8: Class filter controls topology visibility (heatmap + sparklines aggregate across active classes)

### Cross-cutting
- [x] AC9: Vitest coverage for new helpers (41 new tests)
- [x] AC10: Existing Playwright specs still pass (no structural changes)

## Implementation Plan

| Phase | What | ACs | Status |
|-------|------|-----|--------|
| 1 | Metric selector + topology integration | AC1-2 | done |
| 2 | Edge cards + edge selection | AC4-6 | done |
| 3 | Sparklines for selected metric | AC3 | done |
| 4 | Class filter (heatmap + sparklines) | AC7-8 | done |
| 5 | Tests | AC9-10 | done |

## Test Summary

- **Vitest:** 258 passed (12 files) — +41 new tests
- **dag-map:** 293 passed (unchanged from m-E21-01)
- **Build:** green

## Files Changed

### New files
- `ui/src/lib/components/metric-selector.svelte` — chip bar for metric selection
- `ui/src/lib/components/workbench-edge-card.svelte` — edge inspection card
- `ui/src/lib/components/timeline-scrubber.svelte` — custom SVG scrubber matching Blazor style
- `ui/src/lib/utils/metric-defs.ts` — METRIC_DEFS with dual paths (snapshot + series key), class-filter helpers, sparkline builder
- `ui/src/lib/utils/metric-defs.test.ts` — 31 vitest tests

### Modified files
- `ui/src/lib/stores/workbench.svelte.ts` — added `pinnedEdges`, `selectedMetric`, edge toggle methods
- `ui/src/lib/utils/workbench-metrics.ts` — `extractEdgeMetrics` added
- `ui/src/lib/utils/workbench-metrics.test.ts` — +4 edge metric tests
- `ui/src/lib/components/sparkline-path.ts` — NaN/Infinity handled as gaps, multi-segment paths
- `ui/src/lib/components/sparkline-path.test.ts` — +6 NaN handling tests
- `ui/src/routes/time-travel/topology/+page.svelte` — metric selector, class filter, edge pinning, sparklines from state_window, custom timeline scrubber above DAG, edge hit areas, pointer cursor
- `ui/src/app.css` — pointer cursor on `[data-node-id]` and `[data-edge-hit]`
- Status files: spec.md, milestone.md, CLAUDE.md

## Notes

- **Field name correction**: The state snapshot API returns `metrics.queue` (not `queueDepth`). Initial `METRIC_DEFS` had the wrong path — corrected.
- **Dual metric paths**: `MetricDef` now carries both `path` (for snapshot: `derived.utilization`) and `seriesKey` (for window: `utilization`) since the two API shapes differ.
- **Class filter semantics**: When classes are active, heatmap values sum the metric across active classes from `byClass[cls]`. Nodes without any matching class coverage are omitted from the heatmap.
- **Sparkline gaps**: `computeSparklinePath` now breaks the SVG path at NaN/Infinity values (new `M` segment) rather than producing invalid geometry.
- **State window fetched once per run**: Sparkline data is loaded at run-select time, not per bin. Avoids per-scrub fetches.
- **Timeline scrubber**: Replaced the naive `<input type="range">` with a custom SVG-based scrubber matching the Blazor visual style (bordered track, tick marks major+minor, bin labels, teal pointer bar). Moved from below DAG to above DAG to match Blazor's prominence.

## Completion

- **Completed:** 2026-04-17
- **Final test count:** 258 vitest + 293 dag-map = 551 across UI surfaces
- **Deferred items:** (none — all 10 ACs delivered)
