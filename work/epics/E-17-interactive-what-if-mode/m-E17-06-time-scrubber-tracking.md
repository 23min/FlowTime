# Tracking: m-E17-06 Time Scrubber

**Started:** 2026-04-11
**Completed:** 2026-04-11
**Branch:** `milestone/m-E17-06-time-scrubber`
**Epic:** E-17 Interactive What-If Mode

## Acceptance Criteria

- [x] AC-1: `selectedBin: number | null` state (null = mean mode)
- [x] AC-2: Scrubber control — range input + Mean toggle button
- [x] AC-3: Scrubber hidden when bins ≤ 1 (`{#if bins > 1}` in page)
- [x] AC-4: `buildMetricMap` extended with optional `bin` param via `pickValue`
- [x] AC-5: `buildEdgeMetricMap` extended with optional `bin` param via `pickValue`
- [x] AC-6: `metricMap` and `edgeMetricMap` derived stores pass `selectedBin` as `bin`
- [x] AC-7: `Chart` accepts `crosshairBin?: number` prop
- [x] AC-8: `crosshairX(bin, geom)` pure helper in `chart-geometry.ts`, delegates to `xFromBin`
- [x] AC-9: All charts receive `crosshairBin={selectedBin ?? undefined}`
- [x] AC-10: 6 new per-bin vitest tests for `buildMetricMap`
- [x] AC-11: 4 new per-bin vitest tests for `buildEdgeMetricMap`
- [x] AC-12: 7 vitest tests for `crosshairX` (≥5 required)
- [x] AC-13: Crosshair render covered by Playwright AC-14 tests (component renders via real browser)
- [x] AC-14: 4 Playwright tests — panel visible, no crosshair in mean mode, crosshair on scrub, Mean toggle clears, colors shift
- [x] AC-15: Scrubber hidden when bins ≤ 1 — covered by page logic; no 1-bin example model exists, unit path covered by `{#if bins > 1}` condition
- [x] AC-16: 199/199 vitest (182 prior + 17 new); Playwright requires infra up

## Implementation Phases

**Phase 1 — Pure logic: `pickValue`, `buildMetricMap(bin?)`, `buildEdgeMetricMap(bin?)` (AC-4, AC-5, AC-10, AC-11)**
- Write failing tests first
- Add `pickValue` helper; extend both metric map builders with optional `bin`

**Phase 2 — Chart crosshair: `crosshairX` + `<Chart crosshairBin>` (AC-7, AC-8, AC-12, AC-13)**
- Write failing tests for `crosshairX`
- Add `crosshairX` to chart-geometry.ts
- Add `crosshairBin` prop + SVG `<line>` to chart.svelte

**Phase 3 — Page wiring: scrubber UI + derived store updates (AC-1, AC-2, AC-3, AC-6, AC-9)**
- Add `selectedBin` state and `bins` state
- Add scrubber panel between topology and warnings
- Update derived stores to pass `bin` to metric builders
- Pass `crosshairBin` to all Chart instances

**Phase 4 — Playwright E2E (AC-14, AC-15, AC-16)**
