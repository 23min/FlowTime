# Tracking: m-E17-03 Live Topology and Charts

**Milestone:** m-E17-03
**Epic:** E-17 Interactive What-If Mode
**Status:** complete
**Branch:** `milestone/m-E17-03-live-topology-and-charts`
**Started:** 2026-04-10

## Progress

| AC | Description | Status |
|----|-------------|--------|
| AC-1 | Topology graph renders on What-If page | done |
| AC-2 | Graph structure from compile response | done |
| AC-3 | Layout stability on value changes | done |
| AC-4 | Reactive heatmap | done |
| AC-5 | Metric selection dropdown | done |
| AC-6 | Time-series chart component | done |
| AC-7 | Chart reactive updates | done |
| AC-8 | Multi-series overlay (optional) | done |
| AC-9 | Chart hover tooltip | done |
| AC-10 | Pure unit tests | done |
| AC-11 | Playwright E2E | done |
| AC-12 | Latency badge stays under 1000 µs | done |

## Implementation Summary

### New Rust code
- `engine/core/src/compiler.rs`: `derive_graph(model)` + `GraphInfo`/`GraphNodeInfo`/`GraphEdgeInfo` types
- `engine/cli/src/protocol.rs`: `GraphInfoMsg` added to `CompileResult`
- `engine/cli/src/session.rs`: `handle_compile` populates graph from `derive_graph`
- 6 new Rust unit tests for graph derivation (simple expr, multi-ref, topology, router, empty, const-only)

### New Svelte code
- `ui/src/lib/api/engine-session.ts`: `EngineGraph` type added to `CompileResult`
- `ui/src/lib/api/topology-metrics.ts`: `buildMetricMap`, `availableMetrics`, `defaultMetric`, `seriesMean`, `seriesRange`
- `ui/src/lib/api/graph-adapter.ts`: `adaptEngineGraph` (EngineGraph → GraphResponse)
- `ui/src/lib/components/chart-geometry.ts`: pure chart geometry + `binFromX`/`xFromBin`
- `ui/src/lib/components/chart.svelte`: interactive SVG chart with axes, hover tooltip, multi-series
- `ui/src/routes/what-if/+page.svelte`: wired topology DagMapView, metric dropdown, replaced Sparkline with Chart

### New vitest tests (48 new)
- `topology-metrics.test.ts`: 21 tests covering all helpers
- `graph-adapter.test.ts`: 4 tests for adapter conversion
- `chart-geometry.test.ts`: 23 tests for path computation, axis ticks, bin conversion

### New Playwright tests (5 new)
- `topology graph renders after compile` — verifies panel + metric select
- `chart component renders for each non-internal series` — verifies chart count
- `chart hover shows tooltip with values` — verifies interactive tooltip
- `layout stability — topology DOM structure does not change on parameter tweak` — the critical AC
- `model switch recreates topology graph` — verifies structural changes re-layout

### Layout stability implementation
- `graphResponse` state is updated ONLY in `compileModel`, never in `runEval`
- `metricMap` is a `$derived.by(() => ...)` that reads `series` + `engineGraph` — no graph mutation
- dag-map-view's layout is computed from `$derived.by(() => ...)` which only retriggers when `graph` prop changes by reference
- Playwright verifies by comparing SVG innerHTML before/after a parameter tweak, with dynamic attrs (fill, stroke, style) stripped

## Test count
- Rust: 212 tests (up from 206)
- Vitest: 133 tests (up from 85)
- Playwright E2E: 12 tests (up from 7)
- Total new tests: 54 + 6 Rust

## Latency verification
End-to-end: slider drag → debounced eval → WebSocket round-trip → series update → chart re-render → topology heatmap recolor. Observed in Playwright runs: under 100ms from input fill to value change detection. `Last eval: N µs` badge consistently shows values under 1000 µs for all example models.
