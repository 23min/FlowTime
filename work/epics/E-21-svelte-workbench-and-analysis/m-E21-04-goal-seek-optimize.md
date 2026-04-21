# Milestone: Goal Seek & Optimization Surfaces

**ID:** m-E21-04-goal-seek-optimize
**Epic:** E-21 — Svelte Workbench & Analysis Surfaces
**Status:** in progress
**Started:** 2026-04-21

## Goal

Wire the remaining two `/analysis` tabs — Goal Seek and Optimize — so every Time Machine search mode built in E-18 is accessible from the Svelte workbench. Goal Seek drives a single parameter to a target metric value (1-D bisection). Optimize searches N parameters under bounds to minimize or maximize an objective metric (Nelder-Mead). Ship a shared convergence chart component so the user can see the shape of the search, not just the final number, and extend the two compute endpoints with the per-iteration trace they already compute internally but currently discard.

## Context

m-E21-03 shipped the `/analysis` route shell with a four-tab bar (Sweep, Sensitivity, Goal Seek, Optimize). Goal Seek and Optimize currently render `coming in m-E21-04` placeholders. The `TAB_INFO` copy already promises "convergence info" for goal-seek and "convergence history" for optimize. This milestone turns the placeholders into working surfaces and delivers the convergence visualization the copy already promises.

Shared infrastructure already in place from m-E21-03:

- `ui/src/routes/analysis/+page.svelte` — run/sample picker, scenario card, tab bar, active-tab persistence
- `ui/src/lib/utils/analysis-helpers.ts` — `discoverConstParams`, `ConstParam` type, numeric helpers
- `ui/src/lib/api/flowtime.ts` — `flowtime.sweep(...)`, `flowtime.sensitivity(...)` methods
- `GET /v1/runs/{runId}/model` — read-only model fetch (D-2026-04-17-033)
- Density tokens, `--ft-viz-*` palette, `Loader2` spinner pattern, inline-error pattern
- `sensitivity-bar-geometry.ts` — template for pure-SVG geometry helpers with vitest coverage

### API contracts — current and extended

The existing endpoints already compute per-iteration state but discard it before returning. This milestone extends both response shapes with an additive `trace` field (see Decision Record below). Requests are unchanged. `POST /v1/sweep` and `POST /v1/sensitivity` are untouched.

**POST /v1/goal-seek** — `src/FlowTime.API/Endpoints/GoalSeekEndpoints.cs`

Request (unchanged):
```json
{
  "yaml": "...",
  "paramId": "capacity",
  "metricSeriesId": "derived.utilization",
  "target": 0.8,
  "searchLo": 10,
  "searchHi": 100,
  "tolerance": 1e-6,        // optional, default 1e-6
  "maxIterations": 50        // optional, default 50
}
```

Response (extended):
```json
{
  "paramValue": 42.187,
  "achievedMetricMean": 0.7999,
  "converged": true,
  "iterations": 12,
  "trace": [
    { "iteration": 0, "paramValue": 10,  "metricMean": 0.42, "searchLo": 10,    "searchHi": 100   },
    { "iteration": 0, "paramValue": 100, "metricMean": 0.95, "searchLo": 10,    "searchHi": 100   },
    { "iteration": 1, "paramValue": 55,  "metricMean": 0.88, "searchLo": 10,    "searchHi": 55    },
    { "iteration": 2, "paramValue": 32.5,"metricMean": 0.72, "searchLo": 32.5,  "searchHi": 55    }
    /* ... */
  ]
}
```

Trace semantics:
- Two `iteration: 0` entries for the initial boundary evaluations (`searchLo`, `searchHi`), in that order.
- One entry per bisection step with `iteration: 1..N`, where the recorded `paramValue` is the midpoint evaluated at that step and `searchLo` / `searchHi` are the **post-step** bracket (after narrowing).
- `metricMean` is the unsigned mean at that `paramValue` — same value that drives the bisection decision.
- When the target is already hit at a boundary (converged in 0 iterations), the trace contains only the two boundary entries. When the target is not bracketed, the trace contains only the two boundary entries and the response reports `converged: false`, `iterations: 0`.

400 / 503 behaviour unchanged.

**POST /v1/optimize** — `src/FlowTime.API/Endpoints/OptimizeEndpoints.cs`

Request (unchanged):
```json
{
  "yaml": "...",
  "paramIds": ["arrivals", "capacity"],
  "metricSeriesId": "queue.queueTimeMs",
  "objective": "minimize",            // or "maximize", case-insensitive
  "searchRanges": {
    "arrivals": { "lo": 5,  "hi": 50  },
    "capacity": { "lo": 10, "hi": 200 }
  },
  "tolerance": 1e-4,                   // optional, default 1e-4
  "maxIterations": 200                 // optional, default 200
}
```

Response (extended):
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

Trace semantics:
- One entry per Nelder-Mead iteration, recorded **after** the per-iteration `Sort` so `paramValues` is the current best vertex (`simplex[0]`) and `metricMean` is the unsigned mean at that vertex.
- `iteration: 0` is the initial simplex's best vertex after the pre-loop sort (reported before the main loop begins). `iteration: 1..N` are the post-iteration bests.
- When the search converges in 0 iterations (initial simplex already satisfies tolerance), the trace contains only the `iteration: 0` entry.
- Maximize runs still report unsigned `metricMean` on the trace (same convention as `achievedMetricMean`). The internal sign-flip is not leaked.
- The per-iteration best is canonical; the raw per-evaluation log (reflection / expansion / contraction / shrink probes) is intentionally **not** exposed — out of scope for this milestone.

400 / 503 behaviour unchanged.

### Decision Record (draft)

**D-2026-04-21-034 — Additive `trace` field on `/v1/goal-seek` and `/v1/optimize`**

- **Status:** pending approval (authored during m-E21-04 spec draft; number claimed 2026-04-21 against `work/decisions.md`; appended at start-milestone time, not during draft)
- **Context:** E-21 epic constraints (Scope / Constraints, line 120) restrict UI-side backend work to read-only run-adjacent endpoints per D-2026-04-17-033; compute-endpoint shape changes require their own decision. The `/v1/goal-seek` and `/v1/optimize` endpoints already compute per-iteration state (bisection midpoints; Nelder-Mead per-iteration best vertex) and discard it before returning. The Svelte analysis surface needs that state to show users the shape of the search, not just the final number. m-E18-14's .NET Time Machine CLI will want the same information in JSON mode.
- **Decision:** Add an additive `trace` field to both response shapes with the semantics specified in this milestone's API Contracts section. Request shapes, validation rules, 400/503 behaviour, and the existing `paramValue(s)` / `achievedMetricMean` / `converged` / `iterations` fields remain unchanged. The trace captures per-iteration best state only — not the raw per-evaluation probe log.
- **Consequences:** m-E21-04 implements the trace plumbing through `GoalSeeker`, `Optimizer`, and the endpoint response records; existing API tests gain trace-shape assertions; the .NET CLI passthroughs inherit the field for free. The internal signing convention used by Nelder-Mead (minimize always) must be reversed before exposing `metricMean` so the API contract stays in user-space semantics. Backwards compatibility: additive only — existing consumers that ignore unknown fields continue to work.
- **Out of scope of this decision:** the per-evaluation probe log (reflection/expansion/contraction/shrink intermediate vertices); any change to `/v1/sweep`, `/v1/sensitivity`, or other compute endpoints.

The record will be appended to `work/decisions.md` at start-milestone time and referenced from the E-21 epic spec's Scope / Constraints section.

## Acceptance Criteria

### Backend — trace extension (AC1-AC3)

1. **Goal-seek trace plumbed end-to-end.** `GoalSeeker.SeekAsync` records the two boundary evaluations and each bisection midpoint with the post-step bracket. `GoalSeekResult` gains a `Trace` property (`IReadOnlyList<GoalSeekTracePoint>`). `GoalSeekEndpoints` passes the trace through to the response. All five return paths (`Converged` at `searchLo`, `Converged` at `searchHi`, not-bracketed, tolerance hit mid-loop, max-iterations exhausted) return a trace whose shape matches the semantics above. Existing `GoalSeekEndpointsTests.cs` gains coverage for trace shape + ordering + post-step bracket invariants on each return path.

2. **Optimize trace plumbed end-to-end.** `Optimizer.OptimizeAsync` records the post-sort best vertex once before the main loop (as `iteration: 0`) and once per iteration thereafter. `OptimizeResult` gains a `Trace` property (`IReadOnlyList<OptimizeTracePoint>`). `OptimizeEndpoints` passes the trace through. Maximize runs report unsigned `metricMean` on trace entries (sign reversed internally). Existing `OptimizeEndpointsTests.cs` gains coverage for trace shape + ordering + unsigned-metric invariant on both objectives + trace-length / iterations consistency.

3. **`D-2026-04-21-034` appended to `work/decisions.md` at start-milestone time.** Number is claimed here but the entry is authored when the milestone actually starts (not during spec draft) so it lands alongside the first implementation commit. Body matches the draft in Context. E-21 epic spec Scope / Constraints updated in the same commit to reference the new decision alongside D-2026-04-17-033 (read-only run-adjacent) and to list the additive compute-response change as the other explicit carve-out.

### UI — Tab activation + shared components (AC4-AC5)

4. **Placeholders replaced.** The `goal-seek` and `optimize` tab panels in `ui/src/routes/analysis/+page.svelte` render live content (not the `coming in m-E21-04` stub). `TAB_INFO` copy stands as-is — "convergence info" / "convergence history" now accurately describes what the UI renders.

5. **Shared result card + shared convergence chart extracted up front.** `ui/src/lib/components/analysis-result-card.svelte` and `ui/src/lib/components/convergence-chart.svelte` land as reusable components **before** the tab-specific work so Goal Seek and Optimize can both consume them. The result card takes a header slot, a primary-value slot, and a meta slot (iterations / converged / tolerance / direction / target). The convergence chart consumes a **normalized** input shape — `Array<{ iteration: number; metricMean: number }>` — and each caller adapts its response into that shape before passing it in. The chart does not branch on goal-seek vs optimize. Goal Seek's bracket and Optimize's `paramValues` are rendered elsewhere (interval bar, per-param table) and do not enter the chart. Optional chart props: `target?: number` (dashed reference line), `yLabel: string`, `converged: boolean` (teal line when true, amber when false). Geometry lives in pure `.ts` siblings with vitest coverage, mirroring `sensitivity-bar-geometry`.

### UI — Goal Seek surface (AC6-AC9)

6. **Parameter selector.** Single-select dropdown listing the current model's const-node parameters (reuses `discoverConstParams`). Each option shows `{id} (base {baseline})` — same format as the Sweep tab. Empty state when no const params exist (same copy as Sweep).

7. **Search interval + target + advanced inputs.** Two numeric inputs `searchLo` and `searchHi` with inline validation (both required, `searchLo < searchHi`, defaults `0.5 × baseline` / `2 × baseline` of the selected parameter). Free-text input for `metricSeriesId` with the same chip shortcuts as Sensitivity (`served`, `queue`, `flowLatencyMs`, `utilization`). Numeric input for `target`. A collapsed "Advanced" disclosure exposes `tolerance` (default 1e-6) and `maxIterations` (default 50). All required fields must be valid before the Run button enables.

8. **Run goal-seek and render results.** "Run goal seek" button calls `flowtime.goalSeek(...)` (new API method, response type includes `trace`). While running, show a spinner (`Loader2Icon`) and disable the button. On success, render:
   - The shared result card (AC5) with the final `paramValue`, `achievedMetricMean`, `target`, `|achieved − target|` residual, converged badge, and iteration count.
   - The shared convergence chart (AC5) plotting `metricMean` vs `iteration` as a line, with a horizontal reference line at `target`. Boundary evaluations (`iteration: 0`) are plotted as two initial points on the x-axis at position 0. The converged/final point is visually emphasized.
   - 400 and 503 errors surfaced as inline messages using the existing analysis-page error pattern.

9. **Not-bracketed and not-converged states.** When the API returns `converged: false` with `iterations: 0` (target not bracketed), the result card shows an amber warning explaining that the target was not reachable within the search interval and suggests widening the bounds. The convergence chart still renders the two boundary evaluations. When `converged: false` with `iterations == maxIterations`, the card shows an amber "did not converge" badge and the chart is drawn over the full trace.

### UI — Optimize surface (AC10-AC12)

10. **Param multi-select with bounds — layout.** Chip-bar of all discovered const params at the top (toggle to include in the optimization, same chip styling and toggle interaction as Sensitivity). Below the chip-bar, a **compact table** with one row per selected param and columns `param id`, `baseline`, `lo`, `hi`. The `lo`/`hi` cells are inline numeric inputs with defaults `0.5 × baseline` / `2 × baseline`; the table appears only when at least one chip is active. Rationale: keeps the chip-bar a pure selector (matches Sensitivity muscle memory) and groups the bounds into one aligned grid, which reads cleanly for 1–5 params (the realistic scale for a hand-driven Nelder-Mead session). Inline validation: at least one param selected; for every selected param, both bounds required and `lo < hi`.

11. **Objective metric + direction + advanced inputs.** Free-text `metricSeriesId` with the same chip shortcuts used by Sensitivity. A two-option toggle for direction (`minimize` / `maximize`). "Advanced" disclosure exposes `tolerance` (default 1e-4) and `maxIterations` (default 200).

12. **Run optimize and render results.** "Run optimize" button calls `flowtime.optimize(...)` (new API method, response type includes `trace`). While running, show a spinner and disable the button. On success, render:
    - The shared result card (AC5) showing the objective metric + direction, final `achievedMetricMean`, converged badge, iteration count.
    - A per-param table: `paramId`, final value, `[lo, hi]` bound, a mini "range bar" (SVG, shares geometry with Goal Seek's interval bar) showing where the final value landed inside its bound.
    - The shared convergence chart (AC5) plotting `metricMean` vs `iteration` over the full trace. No target reference line (there is no target for optimize) — the y-axis label reflects the direction ("minimizing X" / "maximizing X").
    - 400 and 503 errors surfaced inline.

### Cross-cutting (AC13-AC15)

13. **Session form state.** Both the Goal Seek and Optimize forms retain their last input values across tab switches within the same page session (in-memory is sufficient). Form values reset when the scenario (run / sample model) changes. Mirrors the Sweep tab behaviour.

14. **Vitest coverage for pure logic.** New helpers added to `ui/src/lib/utils/analysis-helpers.ts` (or a sibling `goal-seek-optimize-helpers.ts` if the file grows unwieldy) have vitest tests with branch coverage:
    - `defaultSearchBounds(baseline)` — `0.5 × baseline` / `2 × baseline`; guards for `baseline === 0`, negative baselines, non-finite inputs.
    - `validateSearchInterval({lo, hi})` — structured error for missing / non-finite / `lo >= hi`.
    - `validateOptimizeForm({ selectedParams, bounds, metricSeriesId, objective })` — per-field error map.
    - `intervalMarkerGeometry({ lo, hi, value, width })` — clamping when `value ∉ [lo, hi]`, degenerate `hi === lo`, non-finite inputs.
    - `convergence-chart-geometry.ts` — operates on the **normalized** `Array<{ iteration, metricMean }>` shape defined in AC5. `convergencePath({ trace, width, height, padding, yDomain })` with tests for empty trace, single-point trace, trace with multiple entries at the same `iteration` (goal-seek boundary case: two points at `iteration: 0`), monotonic vs non-monotonic traces, flat metric (all equal), non-finite values, y-domain override vs auto-fit, target-line y-coordinate computation.
    - `analysis-result-card-geometry.ts` (if needed) — whatever pure logic the card uses (badge-colour selection given `converged`, residual formatting). Skip the file if the card is pure markup with no computation worth testing.
    - No mocks; no DOM.

15. **Playwright coverage.** Extend `tests/ui/specs/svelte-analysis.spec.ts` (preferred) or add `svelte-analysis-goal-seek-optimize.spec.ts`:
    - Goal Seek: page loads, param selector populates, interval defaults render, Run button disabled until form is complete, run against a real engine returns a result card with `paramValue`, `converged` badge, iterations, **and a rendered convergence chart with at least one plotted point beyond iteration 0**.
    - Goal Seek not-bracketed path: deterministic repro — first bundled sample in `SAMPLE_MODELS` (see Dependencies), its first discovered const param, `searchLo: 0.5 × baseline`, `searchHi: 2 × baseline`, `metricSeriesId: served`, `target: 1e12` (unreachable by construction). Assert the warning message + the chart rendering only the two boundary points. Resolve the exact model id, param id, and baseline at start-milestone against live `SAMPLE_MODELS`; record the chosen tuple in the tracking doc so future edits to sample models don't silently break the spec.
    - Optimize: param multi-select toggles, bounds inputs render per selected param, direction toggle works, run against a real engine returns a per-param result table and a convergence chart with multiple iterations plotted.
    - Graceful skip when Engine API (8081) or Svelte dev server (5173) is down, matching the existing probe-and-skip pattern in `svelte-analysis.spec.ts`.

### Branch-coverage audit (AC16)

16. **Line-by-line branch audit** before the commit-approval prompt — both the backend trace-plumbing paths (five goal-seek return paths; pre-loop and main-loop exits in Nelder-Mead; shrink-vs-no-shrink branches) and the new frontend components/helpers. Enumerate every reachable branch and match each to a test (xUnit / vitest / Playwright). Record unreachable / defensive-default branches in the tracking doc's Coverage Notes, following m-E21-03's pattern.

## Technical Notes

### Backend

- **`GoalSeekTracePoint` record** in `FlowTime.TimeMachine.Sweep` — `(int Iteration, double ParamValue, double MetricMean, double SearchLo, double SearchHi)`. Serializes to camelCase JSON automatically via existing endpoint serialization settings.
- **`OptimizeTracePoint` record** in `FlowTime.TimeMachine.Sweep` — `(int Iteration, IReadOnlyDictionary<string, double> ParamValues, double MetricMean)`.
- **Trace buffer inside the runners** — accumulate in a `List<...>` and hand the result to `MakeResult` / `Converged` / `NotConverged` helpers. Avoid allocating per-iteration closures.
- **Max trace size** — bounded by `maxIterations + 2` for goal-seek and `maxIterations + 1` for optimize. No separate cap needed.
- **Serialization** — endpoint response records already use System.Text.Json camelCase; adding `Trace` on both response records picks up the same convention. Verify with a round-trip test.
- **.NET CLI (m-E18-14) impact** — the `goal-seek` and `optimize` CLI subcommands pipe JSON through; the new `trace` field appears automatically. No CLI code change required; add a CLI test confirming trace is present in the JSON output.

### Frontend

- **API client additions** (`ui/src/lib/api/flowtime.ts`):

  ```ts
  async goalSeek(body: {
    yaml: string;
    paramId: string;
    metricSeriesId: string;
    target: number;
    searchLo: number;
    searchHi: number;
    tolerance?: number;
    maxIterations?: number;
  }) {
    return post<{
      paramValue: number;
      achievedMetricMean: number;
      converged: boolean;
      iterations: number;
      trace: {
        iteration: number;
        paramValue: number;
        metricMean: number;
        searchLo: number;
        searchHi: number;
      }[];
    }>(`${API}/goal-seek`, body);
  }

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

- **`ConvergenceChart.svelte`** — pure SVG, ~80-120 lines. Consumes the normalized `Array<{ iteration, metricMean }>` shape (see AC5). Geometry in `convergence-chart-geometry.ts` handles y-domain computation, point projection, target-line y-coord, and multi-point-at-same-x placement (goal-seek's two `iteration: 0` entries). Single-series line for simplicity; no legend. Uses `--ft-viz-*` palette tokens; teal when `converged`, amber otherwise; dashed horizontal reference line at `target` when provided. Axis labels: `iteration` on x, `yLabel` prop (caller-supplied, e.g. "metric mean" or "queue.queueTimeMs") on y.

- **`AnalysisResultCard.svelte`** — compact card using existing density tokens. Header (title + converged badge), primary value (large monospace), meta grid (iterations, tolerance, direction, target if present). No new shadcn components required.

- **Interval bar + per-param range bars** — extracted into `interval-bar-geometry.ts` with vitest. Reused by Goal Seek's interval visualization (single bar) and by Optimize's per-param table (one mini bar per row).

- **Form state** — co-located in the route component using `$state` runes. Promote to a `goal-seek-optimize-state.svelte.ts` store only if readability degrades; m-E21-03 kept state local and that's the baseline to beat.

- **Scenario-change reset** — when `selectedRunId` or `selectedSampleId` changes, reset the Goal Seek and Optimize forms. Wire into the same reactivity that already drives scenario changes in `/analysis`.

- **Error messaging** — reuse the existing error surface pattern from m-E21-03; do not introduce a new toast or modal system.

- **Density / styling** — small inputs, tight gutters, 8–12 px steps. Use the analysis page's existing typography scale; no new font sizes.

## Out of Scope

- Per-evaluation probe log for optimize (raw reflection/expansion/contraction/shrink intermediate vertices). The exposed trace is per-iteration best only.
- Multi-objective / Pareto optimization (not in the engine).
- Constraints on optimization (deferred — tracked in `work/gaps.md`).
- History panel for past goal-seek / optimize runs.
- Exporting results (CSV, JSON download).
- Persisting form values to `localStorage` across browser sessions.
- Keyboard shortcuts beyond what the analysis page already supports.
- Server-side parameter discovery (browser-side `discoverConstParams` still owns this).
- Trace extension on `/v1/sweep` or `/v1/sensitivity` (not needed; not covered by D-2026-04-21-034).

## Dependencies

- m-E21-03 (complete) — analysis route shell, tab bar, run/sample picker, param discovery, density tokens, inline-error pattern, sensitivity bar geometry (as a template for the interval bar + convergence chart geometry).
- `POST /v1/goal-seek`, `POST /v1/optimize` — available on port 8081 against `RustEngine:Enabled=true`. Both covered by existing API tests (`GoalSeekEndpointsTests.cs`, `OptimizeEndpointsTests.cs`) that this milestone extends with trace-shape assertions.
- D-2026-04-21-034 — appended to `work/decisions.md` at start-milestone time; blocks AC3.
- Sample models bundled at `ui/src/lib/utils/sample-models.ts` (path to reconfirm with a one-line grep at start-milestone). At least one sample must have const nodes, a reachable metric target (for the happy-path Playwright goal-seek), and accommodate the unreachable-target case from AC15.

## Coverage Notes

(Filled at wrap time — follow m-E21-03's structure: pure-logic tests, component rendering covered by Playwright, defensive / unreachable branches documented, and the five goal-seek return paths + two optimize exit paths each matched to a named xUnit test.)
