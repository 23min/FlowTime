# Milestone: Optimize Surface

**ID:** m-E21-05-optimize
**Epic:** E-21 — Svelte Workbench & Analysis Surfaces
**Status:** in-progress
**Created:** 2026-04-21 (split from m-E21-04)
**Started:** 2026-04-22
**Branch:** `milestone/m-E21-05-optimize` (branched from `epic/E-21-svelte-workbench-and-analysis` at commit `8c4898f`)

## Goal

Wire the `/analysis` Optimize tab to live `/v1/optimize` so N-parameter Nelder-Mead optimization under bounds is usable from the Svelte workbench. Consume the shared `AnalysisResultCard` + `ConvergenceChart` components delivered by m-E21-04 and the already-landed `trace` field on the optimize response (commit `29ac3e9` of m-E21-04's branch). Deliver a per-param result table with mini range bars so the user sees where each optimized parameter landed inside its bound.

## Context

This milestone was split out of the original `m-E21-04-goal-seek-optimize` on 2026-04-21 (16 ACs was too large; "Phase 1 / Phase 2" sub-phasing was the smell). The preconditions for Optimize to land cheaply are all complete before this milestone starts:

- **Backend `trace` on `/v1/optimize`** — landed in m-E21-04 commit `29ac3e9` under D-2026-04-21-034. `OptimizeResponse` already carries `IReadOnlyList<OptimizeTracePoint> Trace` with pre-loop + per-iteration best-vertex entries; maximize runs emit unsigned `metricMean`. `OptimizeEndpointsTraceTests` + `OptimizerTests` already lock every reachable branch.
- **Shared UI components** — `ui/src/lib/components/analysis-result-card.svelte` and `ui/src/lib/components/convergence-chart.svelte` land in m-E21-04 (goal-seek is the first consumer). Their geometry siblings (`convergence-chart-geometry.ts`, `interval-bar-geometry.ts`) also land there with vitest coverage. This milestone consumes them directly — no further component extraction is needed.
- **Analysis route shell** — tab bar, scenario picker, inline-error pattern, session form state model, and the `optimize` tab placeholder (updated in m-E21-04 to reference this milestone) are all in place from m-E21-03 / m-E21-04.

What remains is: the Optimize tab surface (param multi-select + bounds table + direction toggle + Advanced), the `flowtime.optimize(...)` API client method, the per-param result table with mini range bars, and the Playwright / vitest coverage for the optimize-specific pieces.

### API contract

**POST /v1/optimize** — `src/FlowTime.API/Endpoints/OptimizeEndpoints.cs`

Request (unchanged):
```json
{
  "yaml": "...",
  "paramIds": ["arrivals", "capacity"],
  "metricSeriesId": "queue.queueTimeMs",
  "objective": "minimize",
  "searchRanges": {
    "arrivals": { "lo": 5,  "hi": 50  },
    "capacity": { "lo": 10, "hi": 200 }
  },
  "tolerance": 1e-4,
  "maxIterations": 200
}
```

Response (already extended in m-E21-04 commit `29ac3e9`):
```json
{
  "paramValues": { "arrivals": 17.3, "capacity": 74.2 },
  "achievedMetricMean": 0.042,
  "converged": true,
  "iterations": 87,
  "trace": [
    { "iteration": 0, "paramValues": { "arrivals": 27.5, "capacity": 105 }, "metricMean": 0.31 },
    { "iteration": 1, "paramValues": { "arrivals": 25.8, "capacity": 112 }, "metricMean": 0.27 },
    { "iteration": 2, "paramValues": { "arrivals": 22.4, "capacity":  98 }, "metricMean": 0.19 }
    /* ... */
  ]
}
```

Trace semantics (as delivered by m-E21-04's backend AC2):
- One entry per Nelder-Mead iteration, recorded **after** the per-iteration `Sort` so `paramValues` is the current best vertex (`simplex[0]`) and `metricMean` is the unsigned mean at that vertex.
- `iteration: 0` is the initial simplex's best vertex after the pre-loop sort. `iteration: 1..N` are the post-iteration bests.
- When the search converges in 0 iterations (initial simplex already satisfies tolerance), the trace contains only the `iteration: 0` entry.
- Maximize runs emit unsigned `metricMean` on the trace (same convention as `achievedMetricMean`). The internal sign-flip is not leaked.
- The per-iteration best is canonical; the raw per-evaluation log (reflection / expansion / contraction / shrink probes) is intentionally not exposed.

400 / 503 behaviour unchanged.

## Acceptance Criteria

1. **Optimize placeholder replaced.** The `optimize` tab panel in `ui/src/routes/analysis/+page.svelte` renders live content (not the m-E21-04-era "coming in m-E21-05" stub). `TAB_INFO` copy for Optimize stands as-is — "convergence history" now accurately describes what the UI renders.

2. **Param multi-select with bounds — layout.** Chip-bar of all discovered const params at the top (toggle to include in the optimization; same chip styling and toggle interaction as Sensitivity). Below the chip-bar, a **compact table** with one row per selected param and columns `param id`, `baseline`, `lo`, `hi`. The `lo` / `hi` cells are inline numeric inputs with defaults `0.5 × baseline` / `2 × baseline`; the table appears only when at least one chip is active. Rationale: keeps the chip-bar a pure selector (matches Sensitivity muscle memory) and groups the bounds into one aligned grid, which reads cleanly for 1–5 params (the realistic scale for a hand-driven Nelder-Mead session). **Empty state** when no const params are discoverable on the current model: render the Sweep/Goal-Seek shape string `"No const-kind parameters in this model to optimize over."` in the same `<p class="text-xs text-muted-foreground italic">` wrapper used by the Sweep (line 678) and Goal Seek (line 1004) surfaces in `ui/src/routes/analysis/+page.svelte`, with the Run button disabled. **No-params-selected state**: when the chip-bar has rendered but zero chips are toggled on, the bounds table is hidden and the Run button is disabled with an inline hint ("select at least one parameter"). Inline validation: at least one param selected; for every selected param, both bounds required and `lo < hi`.

3. **Objective metric + direction + advanced inputs.** Free-text `metricSeriesId` with the same chip shortcuts used by Sensitivity (`served`, `queue`, `flowLatencyMs`, `utilization`). A two-option toggle for direction (`minimize` / `maximize`), defaulting to `minimize` on first render and after every scenario-change reset. A collapsed "Advanced" disclosure exposes `tolerance` (default 1e-4) and `maxIterations` (default 200). All required fields must be valid before the Run button enables.

4. **Run optimize and render results.** "Run optimize" button calls `flowtime.optimize(...)` (new API method — see Technical Notes). While running, show a spinner (`Loader2Icon`) and disable the button. On success, render:
    - The **shared** result card (delivered in m-E21-04) showing the objective metric + direction, final `achievedMetricMean`, converged badge, iteration count.
    - A per-param table: `paramId`, final value, `[lo, hi]` bound (printed as `[lo, hi]` in a text cell), and a **separate** column for the mini "range bar" (SVG, reuses `interval-bar-geometry.ts` from m-E21-04) showing where the final value landed inside its bound. The range bar is its own column — do not overlay it on the `[lo, hi]` text cell, so the text stays selectable/copyable and the bar's width is not coupled to text length.
    - The **shared** convergence chart (delivered in m-E21-04) plotting `metricMean` vs `iteration` over the full trace. No target reference line (there is no target for optimize) — the y-axis label reflects the direction ("minimizing X" / "maximizing X").
    - 400 and 503 errors surfaced as inline messages using the existing analysis-page error pattern.

5. **Not-converged state.** When the API returns `converged: false` with `iterations == maxIterations`, the shared result card shows an amber "did not converge" badge (same pattern as Goal Seek's max-iterations case from m-E21-04 AC9) and the convergence chart is drawn over the full trace. When `converged: false` with `iterations == 0` (initial simplex failed to satisfy tolerance in 0 iterations — a degenerate max-iterations case), the single `iteration: 0` trace point is plotted and the amber badge still shows. The per-param table renders whatever final `paramValues` the response carries.

6. **Session form state.** The Optimize form retains its last input values (selected param chips, per-param bounds, metric, direction, advanced fields) across tab switches within the same page session (in-memory is sufficient). Form values reset when the scenario (run / sample model) changes. Mirrors the Sweep + Goal Seek tab behaviour.

7. **Vitest coverage for pure logic.** Optimize-specific pure helpers live in a new sibling file `ui/src/lib/utils/optimize-helpers.ts` (with `optimize-helpers.test.ts` alongside it). Do **not** pile them into `analysis-helpers.ts` — keep the optimize surface's helpers modular and scoped to the surface, mirroring how each analysis surface owns its own component files. Branch-covered tests:
    - `validateOptimizeForm({ selectedParams, bounds, metricSeriesId, objective })` — per-field error map. Exercises: no params selected; missing lo or hi on any selected param; `lo >= hi`; non-finite bounds; empty metric string; invalid objective.
    - Any per-param range-bar geometry helper extracted from `interval-bar-geometry.ts` for the table's mini bars. (If the existing `intervalMarkerGeometry` from m-E21-04 covers this unchanged, no new tests are required beyond a call-site test.)
    - Shared cross-surface helpers (e.g. `discoverConstParams`) stay in `analysis-helpers.ts`; optimize-only helpers stay in `optimize-helpers.ts`.
    - No mocks; no DOM.

8. **Playwright coverage.** Extend `tests/ui/specs/svelte-analysis.spec.ts` (preferred) or add `svelte-analysis-optimize.spec.ts`:
    - Optimize happy path: page loads, param chip-bar populates, multi-select toggles work, bounds inputs render per selected param, direction toggle works, Run button disabled until form is complete, run against a real engine returns the shared result card **with a converged badge**, a **per-param result table with one row per selected param (id, final value, `[lo, hi]` bound, and a rendered range bar)**, and a rendered convergence chart with multiple iterations plotted. Uses the deterministic tuple recorded in the tracking doc's Notes section (≥ 2 const params from a named bundled sample, bounds that reliably converge inside `maxIterations`).
    - No-params-selected state: when the user opens the Optimize tab with no chips toggled on, the bounds table is hidden, the Run button is disabled, and the inline hint renders.
    - Graceful skip when Engine API (8081) or Svelte dev server (5173) is down, matching the existing probe-and-skip pattern in `svelte-analysis.spec.ts`.

9. **Line-by-line branch audit** before the commit-approval prompt — the new UI components / helpers only (backend audit is complete from m-E21-04). Enumerate every reachable branch in `validateOptimizeForm` (and any sibling helpers in `optimize-helpers.ts`), the per-param range-bar call sites, and the Optimize tab's render conditions (happy-path / empty / no-params-selected / not-converged), matching each to a test (vitest / Playwright). Record unreachable / defensive-default branches in the tracking doc's Coverage Notes, following m-E21-03's pattern.

## Technical Notes

- **API client addition** (`ui/src/lib/api/flowtime.ts`):

  ```ts
  async optimize(body: {
    yaml: string;
    paramIds: string[];
    metricSeriesId: string;
    objective: 'minimize' | 'maximize';
    searchRanges: Record<string, { lo: number; hi: number }>;
    tolerance?: number;
    maxIterations?: number;
  }) {
    return post<{
      paramValues: Record<string, number>;
      achievedMetricMean: number;
      converged: boolean;
      iterations: number;
      trace: {
        iteration: number;
        paramValues: Record<string, number>;
        metricMean: number;
      }[];
    }>(`${API}/optimize`, body);
  }
  ```

- **Trace adaptation.** Convert the optimize trace into the chart's normalized `Array<{ iteration, metricMean }>` shape at the call site: `trace.map(p => ({ iteration: p.iteration, metricMean: p.metricMean }))`. The chart does not branch on surface type — it receives the same normalized shape Goal Seek passes.

- **Chart y-axis label.** Reflects the direction — e.g. `minimizing ${metricSeriesId}` / `maximizing ${metricSeriesId}`. No target reference line (no target for optimize). Line colour reflects converged state (teal vs amber), same as Goal Seek. Exact prop names match whatever m-E21-04's extraction settled on.

- **Per-param result table.** One row per paramId in `paramValues`. Columns (in order): id, final value (monospace, fixed precision), `[lo, hi]` bound as a text cell, mini SVG range bar as its own column (reuses `interval-bar-geometry.ts` from m-E21-04). Keeping the range bar in a dedicated column preserves text-cell copyability and decouples the bar's rendered width from the `[lo, hi]` string length.

- **Form state.** Co-located in the route component using `$state` runes, mirroring Goal Seek's pattern from m-E21-04. If readability degrades with the multi-select + per-param bounds structure, promote to `optimize-state.svelte.ts` (sibling file, same modularization philosophy as `optimize-helpers.ts`).

- **Helper module layout.** `ui/src/lib/utils/optimize-helpers.ts` owns optimize-specific pure helpers (form validation, trace→chart normalization, per-param table-row construction). Cross-surface helpers that were shared across Sweep / Sensitivity / Goal Seek / Optimize (e.g. `discoverConstParams`) stay in `analysis-helpers.ts`. The test file `optimize-helpers.test.ts` sits alongside the helper file; do not extend `analysis-helpers.test.ts`.

- **Scenario-change reset.** Wire into the same reactivity that already drives scenario changes in `/analysis` (Sweep, Sensitivity, Goal Seek all do this).

- **Error messaging.** Reuse the existing error surface pattern from m-E21-03 / m-E21-04; do not introduce a new toast or modal system.

- **Density / styling.** Small inputs, tight gutters, 8–12 px steps. Use the analysis page's existing typography scale; no new font sizes.

## Out of Scope

- Per-evaluation probe log for optimize (raw reflection/expansion/contraction/shrink intermediate vertices). The exposed trace is per-iteration best only.
- Multi-objective / Pareto optimization (not in the engine).
- Constraints on optimization (deferred — tracked in `work/gaps.md`).
- History panel for past optimize runs.
- Exporting results (CSV, JSON download).
- Persisting form values to `localStorage` across browser sessions.
- Keyboard shortcuts beyond what the analysis page already supports.
- Server-side parameter discovery (browser-side `discoverConstParams` still owns this).
- Backend trace / endpoint changes — complete in m-E21-04.
- Extraction of shared `AnalysisResultCard` / `ConvergenceChart` components — complete in m-E21-04.

## Dependencies

- **m-E21-04 (complete before this milestone starts)** — delivers shared `AnalysisResultCard`, `ConvergenceChart`, `convergence-chart-geometry.ts`, `interval-bar-geometry.ts`, and the goal-seek surface baseline.
- **Backend trace on `/v1/optimize`** — already landed in m-E21-04 commit `29ac3e9` under D-2026-04-21-034. No backend work required in this milestone.
- `POST /v1/optimize` — available on port 8081 against `RustEngine:Enabled=true`.
- Sample models bundled at `ui/src/lib/utils/sample-models.ts` — at least one with ≥ 2 const nodes and a metric that changes monotonically with them (so the Nelder-Mead simplex has room to move during the Playwright happy path). See the candidate tuple in Notes.

## Notes

- The original combined milestone `m-E21-04-goal-seek-optimize` was split on 2026-04-21 after the shared backend trace landed but before any UI work began. The split preserves the decision record (D-2026-04-21-034 covers both endpoints) and preserves commit `29ac3e9` on the m-E21-04 branch.

- **Candidate Playwright happy-path tuple (to verify at milestone start):**
  - Model id: `coffee-shop` (first entry in `SAMPLE_MODELS`, `ui/src/lib/utils/sample-models.ts:47`) — same sample used by the m-E21-04 goal-seek not-bracketed Playwright case, chosen for continuity and because it ships with multiple const nodes.
  - `paramIds`: the first two discoverable const params via `discoverConstParams` (expected to include `customers_per_hour`; confirm the second at milestone start).
  - `searchRanges`: for each selected param, `{ lo: 0.5 × baseline, hi: 2 × baseline }` — mirrors the default-bounds rule in AC2.
  - `metricSeriesId`: `served` (Sensitivity chip shortcut — verify the exact engine-emitted id at authoring time; if the actual series is namespaced, e.g. `Register.served`, update this tuple + the AC8 assertion together).
  - `objective`: `minimize`; `tolerance`: `1e-4`; `maxIterations`: `200`.
  - Expected: `converged: true` within the iteration budget, trace length ≥ 2, `paramValues` populated for both selected params, per-param table renders one row per param with a visible range-bar marker inside `[lo, hi]`.
  - **Verification gate at milestone start**: if `coffee-shop` lacks a second usable const param, or the chosen metric does not move monotonically under these bounds in the Rust engine's output, **swap the sample** (pick an alternate from `SAMPLE_MODELS` whose metric is monotonic under its default bounds, record the replacement tuple here) before writing the Playwright spec. Do **not** soften AC8 to a not-converged assertion — the converged-badge happy path is what AC8 is proving; AC5 already owns the not-converged rendering. A silently-flaky Playwright test is the failure mode to avoid.

## Coverage Notes

(Filled at wrap — follow m-E21-03's structure: pure-logic tests, component rendering via Playwright, defensive / unreachable branches enumerated with rationale.)
