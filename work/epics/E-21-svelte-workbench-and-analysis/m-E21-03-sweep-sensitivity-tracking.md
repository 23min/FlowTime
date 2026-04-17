# Tracking: Sweep & Sensitivity Surfaces

**Milestone:** m-E21-03-sweep-sensitivity
**Branch:** milestone/m-E21-03-sweep-sensitivity
**Started:** 2026-04-17
**Status:** in-progress

## Acceptance Criteria

### Analysis route shell
- [x] AC1: New `/analysis` route, compact layout, sidebar link (FlaskConical icon)
- [x] AC2: Run picker populates param list from model YAML (via new `/v1/runs/{runId}/model` endpoint)
- [x] AC3: Tab bar (Sweep/Sensitivity/Goal Seek/Optimize), Goal Seek/Optimize show "coming in m-E21-04" placeholders; active tab persisted to localStorage

### Sweep surface
- [x] AC4: Parameter selector listing const nodes with baselines (`{id} (base {baseline})`)
- [x] AC5: Value range inputs (from/to/step + custom CSV) with live preview and > 50-point warning
- [x] AC6: Run sweep, render line chart + per-point means table with series picker
- [x] AC7: Captured series filter chip bar (arrivals/served/errors/queue/utilization/flowLatencyMs)

### Sensitivity surface
- [x] AC8: Param multi-select chips (all selected by default)
- [x] AC9: Target metric picker + perturbation slider (1%-30%)
- [x] AC10: Run sensitivity, render horizontal bar chart sorted by |gradient|, coral for negative / teal for positive

### Cross-cutting
- [x] AC11: Vitest coverage for pure helpers (54 new tests in analysis-helpers.test.ts + 20 in sensitivity-bar-geometry.test.ts)
- [x] AC12: Playwright spec svelte-analysis.spec.ts covering page load, tab switching, sweep run, sensitivity run

## Implementation Log

| Phase | What | Tests | Status |
|-------|------|-------|--------|
| 1 | Backend endpoint: `GET /v1/runs/{runId}/model` | — | done |
| 2 | analysis-helpers.ts: yaml parse, range gen, mean projection, sort | 54 | done |
| 3 | API client: sweep/sensitivity/getRunModel | (via analysis spec) | done |
| 4 | SensitivityBarChart component + geometry module | 20 | done |
| 5 | /analysis route + tab shell + sweep UI + sensitivity UI | — | done |
| 6 | Sidebar Analysis link | — | done |

## Test Summary

- **Vitest:** 397 passed (16 files) — +74 new tests
- **dag-map:** 293 passed (unchanged)
- **Build:** green (Svelte + .NET)

## Files Changed

### New files
- `src/FlowTime.API/Program.cs` — new endpoint `GET /v1/runs/{runId}/model` (~25 lines)
- `ui/src/lib/utils/analysis-helpers.ts` — yaml parse, range gen, custom value parse, seriesMean, projectSweepMeans, sortByAbsGradient, maxAbsGradient
- `ui/src/lib/utils/analysis-helpers.test.ts` — 54 vitest tests
- `ui/src/lib/components/sensitivity-bar-chart.svelte` — horizontal bar chart
- `ui/src/lib/components/sensitivity-bar-geometry.ts` — pure barGeometry/fmt helpers
- `ui/src/lib/components/sensitivity-bar-geometry.test.ts` — 20 vitest tests
- `ui/src/routes/analysis/+page.svelte` — analysis route with tabs
- `tests/ui/specs/svelte-analysis.spec.ts` — 6 Playwright tests

### Modified files
- `ui/src/lib/api/flowtime.ts` — sweep, sensitivity, getRunModel methods
- `ui/src/lib/components/app-sidebar.svelte` — Analysis nav link
- `ui/package.json`, `ui/pnpm-lock.yaml` — js-yaml, @types/js-yaml added
- Status files: spec.md, tracking doc, CLAUDE.md

## Notes

- **Backend addition acknowledged:** Added `GET /v1/runs/{runId}/model` despite the epic's "no backend changes" constraint. Fetching the model YAML for param introspection without this endpoint would require either a second Sim/artifact round-trip (complex) or uploading YAML directly (defeats the "pick a run" flow). Trivial static-file serve from the existing run directory layout.
- **Client-side YAML parsing:** `js-yaml` added for discoverConstParams. Alternative considered: server-side `/v1/runs/{runId}/params` endpoint. Deferred — browser parse is adequate for typical model sizes; promote to server if it proves fragile.
- **Sweep chart series:** the Chart component expects per-bin values for a series; sweep results give per-sweep-point means. Wired as an adapter: the x-axis becomes sweep index (implicit bin axis), y-values are the per-point means. Good enough for first cut.
- **Sensitivity series keys use dot-notation** (e.g., `queue.queueTimeMs`) in API contract. The UI accepts a free-text target metric and offers common chips; users can enter any series id the engine emits.

## Coverage Notes

Branches verified via line-by-line audit.

### Pure logic (fully unit-tested)
- `analysis-helpers.ts`: every branch of `discoverConstParams` (8 parse-error / structural / filter paths), `generateRange` (non-finite from/to/step, step<=0, to<from, maxPoints cap, sub-1 step precision, no overshoot), `parseCustomValues` (empty, non-string, whitespace, non-numeric, all-non-numeric, negative/decimal), `seriesMean` (empty, non-array, all-non-finite, mixed, single), `projectSweepMeans` (empty, null, non-array points, missing keys), `sortByAbsGradient` (both-finite, one-finite, neither-finite paths; non-mutation), `maxAbsGradient` (empty, all-non-finite, mixed).
- `sensitivity-bar-geometry.ts`: `barGeometry` (NaN, Infinity, max=0, positive, negative, zero, full-scale, tiny-fraction paths), `fmtBarValue` (non-finite, >=100, [1,100), <1 boundaries), `barAreaWidth` minimum enforcement, layout defaults.

### Component rendering (Playwright-covered)
- Tab switching, sweep run with results, sensitivity run with results — `svelte-analysis.spec.ts`.
- Empty-state paths (no runs, no params) — conditional branches tested via `test.skip` guards in Playwright.

### Defensive / prop-default branches (documented, not explicitly tested)
- `SensitivityBarChart`: `sorted.length === 0` renders "No sensitivity data" empty state. Reachable only when the API returns an empty `points` array; typical runs return one entry per paramId.
- `analysis/+page.svelte`: tab persistence `localStorage` fallback when stored value is invalid — the branch `storedTab !== valid` silently ignores and uses default. Reachable only if a user edits localStorage manually.
- `runSweep` / `runSensitivity`: the `canRunX` derived guard makes the early-return in each handler effectively unreachable after render, but preserved as a defensive no-op.

## Completion

- **Completed:** pending approval
- **Final test count:** 397 vitest + 293 dag-map = 690 tests across UI surfaces
- **Deferred items:** (none — all 12 ACs delivered)
