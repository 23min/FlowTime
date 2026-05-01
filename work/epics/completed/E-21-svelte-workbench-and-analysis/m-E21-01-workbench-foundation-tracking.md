# Tracking: Workbench Foundation

**Milestone:** m-E21-01-workbench-foundation
**Branch:** milestone/m-E21-01-workbench-foundation
**Started:** 2026-04-17
**Status:** complete (merged to epic 2026-04-17)

## Acceptance Criteria

### Density system
- [x] AC1: Main content area padding reduced (context-dependent, not blanket `p-6`)
- [x] AC2: Sidebar narrowed (expanded 200px, collapsed 40px)
- [x] AC3: Compact design tokens defined in `app.css` (chrome, data-viz, spacing, radius, type)
- [x] AC4: shadcn component overrides applied (compact tokens, tighter sidebar, topbar h-8)
- [x] AC5: Existing pages still function (what-if, run, topology, health — vitest 217 passed, build green)

### dag-map library events
- [x] AC6: `bindEvents()` exported from dag-map (node/edge click + hover, edge hit areas, cleanup function)
- [x] AC7: `selected` render option in dag-map (selection ring composable with heatmap via `dag-map-selected` class)
- [x] AC8: dag-map tests cover events and selection (11 event tests + 7 render tests = 18 new, 292 total)

### Workbench panel
- [x] AC9: Topology page restructured as split layout (DAG + workbench, resizable splitter, ratio persisted to localStorage)
- [x] AC10: Click-to-pin interaction (click pins/unpins via `bindEvents`, multiple pinned, selection ring via `selected` set)
- [x] AC11: Node card content (ID, kind, metrics at current bin: utilization, queue, arrivals, served, errors, capacity)
- [x] AC12: Timeline integration (cards update on bin scrub, sparkline position indicator via vertical line overlay)
- [x] AC13: Cards dismissible (X button removes from selected set and workbench panel)
- [x] AC14: Auto-pin highest-utilization node on first load (via `findHighestUtilizationNode`)

### Cross-cutting
- [x] AC15: Playwright test coverage (4 specs: load, click-to-pin, dismiss, auto-pin — graceful skip when infra down)
- [x] AC16: Vitest coverage for new pure logic (17 tests: extractNodeMetrics + findHighestUtilizationNode)

## Implementation Log

| Phase | What | Tests | Status |
|-------|------|-------|--------|
| 1 | Density system — app.css tokens, layout, sidebar, topbar, page padding | 0 (CSS) | done |
| 2 | dag-map library events — bindEvents, selected, edge hit areas | 18 | done |
| 3 | Workbench panel — split layout, pinning store, WorkbenchCard, state API integration, auto-pin | 17 | done |
| 4 | Test coverage — Playwright spec, vitest for helpers | 4 Playwright + 17 vitest | done |

## Test Summary

- **Vitest:** 217 passed (11 files) — +17 new
- **dag-map:** 292 passed — +18 new
- **Playwright:** 4 specs (3 existing + 1 new file with 4 tests, graceful skip when infra down)
- **Build:** green (Svelte + .NET)

## Files Changed

### New files
- `ui/src/lib/stores/workbench.svelte.ts` — pinning store (session-ephemeral)
- `ui/src/lib/components/workbench-card.svelte` — node inspection card
- `ui/src/lib/utils/workbench-metrics.ts` — metric extraction helpers
- `ui/src/lib/utils/workbench-metrics.test.ts` — 17 vitest tests
- `lib/dag-map/src/events.js` — bindEvents with event delegation
- `lib/dag-map/test/unit/events.test.mjs` — 11 event tests
- `tests/ui/specs/svelte-workbench.spec.ts` — 4 Playwright specs

### Modified files
- `ui/src/app.css` — complete token rewrite (chrome, data-viz, spacing, radius)
- `ui/src/routes/+layout.svelte` — removed blanket p-6
- `ui/src/routes/+page.svelte` — compact padding and text sizes
- `ui/src/routes/health/+page.svelte` — compact padding and text sizes
- `ui/src/routes/run/+page.svelte` — compact padding and text sizes
- `ui/src/routes/time-travel/topology/+page.svelte` — workbench split layout, event wiring, auto-pin
- `ui/src/lib/components/app-sidebar.svelte` — compact nav, smaller logo
- `ui/src/lib/components/app-topbar.svelte` — h-8, tighter padding
- `ui/src/lib/components/dag-map-view.svelte` — accepts `selected` prop, updated theme colors, reduced padding
- `lib/dag-map/src/index.js` — exports bindEvents
- `lib/dag-map/src/render.js` — selected ring, edge hit areas
- `lib/dag-map/test/unit/render.test.mjs` — 7 new tests (selected, edge hit)

### Status files
- `work/epics/E-21-svelte-workbench-and-analysis/spec.md` — m-E21-01 → in-progress
- `work/epics/E-21-svelte-workbench-and-analysis/m-E21-01-workbench-foundation.md` — status → in-progress
- `CLAUDE.md` — Current Work updated with E-21

## Notes

- Sparkline position indicator uses a simple SVG vertical line overlay rather than computing the exact value position. Good enough for the current display density.
- The workbench store uses Svelte 5 runes (`$state`) as a class-based reactive store. Session-ephemeral — cleared on page reload and run selection change.
- Edge hit areas use transparent wider strokes (8px min at scale) with `pointer-events="stroke"` for clickability. The visible edge path has `pointer-events="none"` so clicks go through to the hit area.
- Split ratio stored in `localStorage` under `ft.topology.split` for persistence across page navigations.

## Completion

- **Completed:** 2026-04-17 (merged to epic branch)
- **Final test count:** 217 vitest + 292 dag-map + 4 Playwright = 513 total across surfaces
- **Deferred items:** (none — all 16 ACs delivered)
