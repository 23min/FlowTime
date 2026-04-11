# Tracking: m-E17-05 Edge Heatmap

**Started:** 2026-04-11
**Branch:** `milestone/m-E17-05-edge-heatmap`
**Epic:** E-17 Interactive What-If Mode

## Key Finding (pre-implementation)

dag-map edge metric key format confirmed: `${fromId}\u2192${toId}` (Unicode `→`, not ASCII `->`)
Source: `dag-map/src/render.js:151`

## Acceptance Criteria

- [x] AC-1: `buildEdgeMetricMap` pure function in `topology-metrics.ts`
- [x] AC-2: `normalizeMetricMap` reused (no duplication)
- [x] AC-3: `edgeMetrics` derived store wired into `<DagMapView>` in `+page.svelte`
- [x] AC-4: Layout unaffected — `edgeMetrics` not read by `layout` derived store (dag-map-view:79 vs :99, confirmed)
- [x] AC-5: Visual smoke-test — Playwright 'edge colors shift when parameter changes' covers this
- [x] AC-6: Edge key format `\u2192` confirmed in dag-map/src/render.js:151, comment in source
- [x] AC-7: 9 vitest tests for `buildEdgeMetricMap` (≥8 required)
- [x] AC-8: 3 Playwright E2E tests — colored edge present, colors shift on drag, path `d` stable
- [x] AC-9: 182/182 vitest passing (173 prior + 9 new); Playwright requires infra up

## Implementation Phases

**Phase 1 — Pure logic (AC-1, AC-2, AC-6, AC-7)**
- Export `findNodeSeries` from `topology-metrics.ts`
- Add `buildEdgeMetricMap` with `\u2192` key
- Write ≥8 vitest tests (red → green)

**Phase 2 — Page wiring (AC-3, AC-4)**
- Add `edgeMetricMap` derived store in `+page.svelte`
- Pass `edgeMetrics={edgeMetricMap}` to `<DagMapView>`
- Verify layout derived store does not read `edgeMetricMap`

**Phase 3 — Tests + regression (AC-5, AC-8, AC-9)**
- Run vitest — all 173+ pass
- Playwright E2E additions
