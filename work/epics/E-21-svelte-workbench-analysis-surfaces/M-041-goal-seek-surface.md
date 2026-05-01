---
id: M-041
title: Goal Seek Surface
status: done
parent: E-21
---

**Started:** 2026-04-21
**Completed:** 2026-04-22

## Goal

Wire the `/analysis` Goal Seek tab to live Time Machine so single-parameter target-seeking is usable from the Svelte workbench, and extend `/v1/goal-seek` (plus its sibling `/v1/optimize`) with the per-iteration `trace` they already compute internally but currently discard. Ship the shared convergence chart + analysis result card components here so the subsequent Optimize milestone (M-042) consumes them directly.

## Context

M-040 shipped the `/analysis` route shell with a four-tab bar (Sweep, Sensitivity, Goal Seek, Optimize). Goal Seek and Optimize currently render `coming in m-E21-04` placeholders. This milestone activates the Goal Seek tab and lands the two shared visualization components; the Optimize tab stays placeholder (pointing at M-042) until the follow-up milestone.

The backend `trace` extension covers **both** `/v1/goal-seek` and `/v1/optimize` in one change because they ship under one decision (D-047). That work landed early in this milestone (commit `29ac3e9`); the optimize trace is ready for M-042 to consume without further backend work.

Shared infrastructure already in place from M-040:

- `ui/src/routes/analysis/+page.svelte` — run/sample picker, scenario card, tab bar, active-tab persistence
- `ui/src/lib/utils/analysis-helpers.ts` — `discoverConstParams`, `ConstParam` type, numeric helpers
- `ui/src/lib/api/flowtime.ts` — `flowtime.sweep(...)`, `flowtime.sensitivity(...)` methods
- `GET /v1/runs/{runId}/model` — read-only model fetch (D-046)
- Density tokens, `--ft-viz-*` palette, `Loader2` spinner pattern, inline-error pattern
- `sensitivity-bar-geometry.ts` — template for pure-SVG geometry helpers with vitest coverage

### Scope split note

This milestone was originally drafted as `m-E21-04-goal-seek-optimize` covering both Goal Seek and Optimize tabs. It was split on 2026-04-21 after Phase 1 backend landed: Goal Seek remains here; Optimize moved to a new **M-042 Optimize Surface**; heatmap / validation / polish renumbered to 06 / 07 / 08. Rationale: 16 ACs across backend + shared components + two surfaces was too large, and "Phase 1 / Phase 2" sub-phasing in the tracking doc was the smell. The backend trace change on `/v1/optimize` that landed here is kept; M-042 consumes it.

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
  "tolerance": 1e-6,
  "maxIterations": 50
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

The `trace` extension on `/v1/optimize` is owned by this milestone's backend AC (AC2) since it shares D-047 with goal-seek. The surface that consumes it — the Optimize tab — lives in M-042. **Full request/response shape is owned by M-042's spec** (`m-E21-05-optimize.md` → API contract section); do not duplicate it here. AC2's tests lock these trace invariants:

- One entry per iteration (one post-sort entry before the main loop as `iteration: 0`, plus one per main-loop iteration after its post-iteration sort).
- `paramValues` is the current best vertex (`simplex[0]`), `metricMean` is its **unsigned** mean — the internal minimize-sign flip is reversed at record time for maximize runs.
- Trace length equals `iterations + 1` on every return path (pre-loop converged, main-loop converged, max-iterations exhausted).
- 0-iteration convergence yields a single `iteration: 0` entry. The per-evaluation probe log (reflection / expansion / contraction / shrink intermediate vertices) is intentionally not exposed.

### Decision Record

**D-047 — Additive `trace` field on `/v1/goal-seek` and `/v1/optimize`** — appended to `work/decisions.md` at start-milestone time (commit `5988f5c`). Scope covers both endpoints; implementation landed in commit `29ac3e9` of this milestone. No rewording needed for the split.

## Acceptance Criteria

### Backend — trace extension (AC1-AC3)

1. **Goal-seek trace plumbed end-to-end.** `GoalSeeker.SeekAsync` records the two boundary evaluations and each bisection midpoint with the post-step bracket. `GoalSeekResult` gains a `Trace` property (`IReadOnlyList<GoalSeekTracePoint>`). `GoalSeekEndpoints` passes the trace through to the response. All five return paths (`Converged` at `searchLo`, `Converged` at `searchHi`, not-bracketed, tolerance hit mid-loop, max-iterations exhausted) return a trace whose shape matches the semantics above. Existing `GoalSeekEndpointsTests.cs` gains coverage for trace shape + ordering + post-step bracket invariants on each return path.

2. **Optimize trace plumbed end-to-end.** `Optimizer.OptimizeAsync` records the post-sort best vertex once before the main loop (as `iteration: 0`) and once per iteration thereafter. `OptimizeResult` gains a `Trace` property (`IReadOnlyList<OptimizeTracePoint>`). `OptimizeEndpoints` passes the trace through. Maximize runs report unsigned `metricMean` on trace entries (sign reversed internally). Existing `OptimizeEndpointsTests.cs` gains coverage for trace shape + ordering + unsigned-metric invariant on both objectives + trace-length / iterations consistency. _The consuming Optimize surface is delivered in M-042._

3. **`D-2026-04-21-034` appended to `work/decisions.md` at start-milestone time.** Body matches the draft in Context. E-21 epic spec Scope / Constraints updated in the same commit to reference the new decision alongside D-046 (read-only run-adjacent) and to list the additive compute-response change as the other explicit carve-out.

### UI — Tab activation + shared components (AC4-AC5)

4. **Goal Seek placeholder replaced.** The `goal-seek` tab panel in `ui/src/routes/analysis/+page.svelte` renders live content (not the `coming in m-E21-04` stub). The `optimize` tab panel keeps its placeholder copy updated to reference **M-042**. `TAB_INFO` copy for Goal Seek stands as-is — "convergence info" now accurately describes what the UI renders.

5. **Shared result card + shared convergence chart extracted up front.** `ui/src/lib/components/analysis-result-card.svelte` and `ui/src/lib/components/convergence-chart.svelte` land as reusable components in this milestone so M-042's Optimize surface can consume them without further extraction work. Required behaviours (exact prop/slot names are an implementation decision):
   - **Result card** — accepts a distinct header region, a primary-value region (large monospace), and a meta region for compact key-value pairs (iterations / converged badge / tolerance / direction / target / residual as applicable per surface).
   - **Convergence chart** — consumes a **normalized** input shape `Array<{ iteration: number; metricMean: number }>`; each caller adapts its response into that shape before passing it in. The chart does not branch on surface type. Goal Seek's bracket and (future) Optimize's `paramValues` are rendered elsewhere (interval bar, per-param table) and do not enter the chart. Required behaviours: optional horizontal reference line when a target is supplied (dashed); caller-supplied y-axis label; line colour reflects converged state (teal when converged, amber when not); the converged/final point is visually emphasized (e.g. a larger marker) relative to intermediate points.
   - Geometry lives in pure `.ts` siblings with vitest coverage, mirroring `sensitivity-bar-geometry`.

### UI — Goal Seek surface (AC6-AC9)

6. **Parameter selector.** Single-select dropdown listing the current model's const-node parameters (reuses `discoverConstParams`). Each option shows `{id} (base {baseline})` — same format as the Sweep tab. Empty state when no const params exist (same copy as Sweep).

7. **Search interval + target + advanced inputs.** Two numeric inputs `searchLo` and `searchHi` with inline validation (both required, `searchLo < searchHi`, defaults `0.5 × baseline` / `2 × baseline` of the selected parameter). Free-text input for `metricSeriesId` with the same chip shortcuts as Sensitivity (`served`, `queue`, `flowLatencyMs`, `utilization`). Numeric input for `target`. A collapsed "Advanced" disclosure exposes `tolerance` (default 1e-6) and `maxIterations` (default 50). All required fields must be valid before the Run button enables.

8. **Run goal-seek and render results.** "Run goal seek" button calls `flowtime.goalSeek(...)` (new API method, response type includes `trace`). While running, show a spinner (`Loader2Icon`) and disable the button. On success, render:
   - The shared result card (AC5) with the final `paramValue`, `achievedMetricMean`, `target`, `|achieved − target|` residual, converged badge, and iteration count.
   - The shared convergence chart (AC5) plotting `metricMean` vs `iteration` as a line, with a horizontal reference line at `target`. Boundary evaluations (`iteration: 0`) are plotted as two initial points on the x-axis at position 0. The converged/final point is visually emphasized per AC5.
   - A **search-interval bar** (SVG) showing the original `[searchLo, searchHi]` range with a marker at the final `paramValue`, using `intervalMarkerGeometry` from `interval-bar-geometry.ts`. This is the Goal Seek consumer that justifies landing that geometry file in this milestone; Optimize reuses it for per-param mini bars in M-042.
   - 400 and 503 errors surfaced as inline messages using the existing analysis-page error pattern.

9. **Not-bracketed and not-converged states.** When the API returns `converged: false` with `iterations: 0` (target not bracketed), the result card shows an amber warning explaining that the target was not reachable within the search interval and suggests widening the bounds. The convergence chart still renders the two boundary evaluations. When `converged: false` with `iterations == maxIterations`, the card shows an amber "did not converge" badge and the chart is drawn over the full trace.

### Cross-cutting (AC10-AC12)

10. **Session form state — goal-seek.** The Goal Seek form retains its last input values across tab switches within the same page session (in-memory is sufficient). Form values reset when the scenario (run / sample model) changes. Mirrors the Sweep tab behaviour. _Optimize session state lives in M-042._

11. **Vitest coverage for pure logic.** New helpers added to `ui/src/lib/utils/analysis-helpers.ts` (or a sibling `goal-seek-helpers.ts` if the file grows unwieldy) have vitest tests with branch coverage:
    - `defaultSearchBounds(baseline)` — `0.5 × baseline` / `2 × baseline`; guards for `baseline === 0`, negative baselines, non-finite inputs.
    - `validateSearchInterval({lo, hi})` — structured error for missing / non-finite / `lo >= hi`.
    - `intervalMarkerGeometry({ lo, hi, value, width })` — clamping when `value ∉ [lo, hi]`, degenerate `hi === lo`, non-finite inputs. _(Shared with Optimize's per-param range bars in M-042.)_
    - `convergence-chart-geometry.ts` — operates on the **normalized** `Array<{ iteration, metricMean }>` shape defined in AC5. `convergencePath({ trace, width, height, padding, yDomain })` with tests for empty trace, single-point trace, trace with multiple entries at the same `iteration` (goal-seek boundary case: two points at `iteration: 0`), monotonic vs non-monotonic traces, flat metric (all equal), non-finite values, y-domain override vs auto-fit, target-line y-coordinate computation.
    - `analysis-result-card-geometry.ts` (if needed) — whatever pure logic the card uses (badge-colour selection given `converged`, residual formatting). Skip the file if the card is pure markup with no computation worth testing.
    - No mocks; no DOM.
    - `validateOptimizeForm` is out of scope here; it lives in M-042.

12. **Playwright coverage.** Extend `tests/ui/specs/svelte-analysis.spec.ts` (preferred) or add `svelte-analysis-goal-seek.spec.ts`:
    - Goal Seek happy path: page loads, param selector populates, interval defaults render, Run button disabled until form is complete, run against a real engine returns a result card with `paramValue`, `converged` badge, iterations, **and a rendered convergence chart with at least one plotted point beyond iteration 0**.
    - Goal Seek not-bracketed deterministic repro — uses the tuple recorded in the tracking doc's Notes section (first bundled sample in `SAMPLE_MODELS`, its first discovered const param, `target: 1e12` unreachable). Assert the warning message + the chart rendering only the two boundary points.
    - Graceful skip when Engine API (8081) or Svelte dev server (5173) is down, matching the existing probe-and-skip pattern in `svelte-analysis.spec.ts`.
    - _Optimize Playwright coverage is owned by M-042._

### Branch-coverage audit (AC13)

13. **Line-by-line branch audit** performed in two passes, each captured in the tracking doc's Coverage Notes before its respective commit-approval prompt:
    - **AC13a — Backend pass (already complete, commit `29ac3e9`).** Five goal-seek return paths; pre-loop and main-loop exits in Nelder-Mead; shrink-vs-no-shrink branches. The optimize branches are audited here even though the consumer is M-042, because the implementation lives on this milestone's commits.
    - **AC13b — UI pass (pending).** New frontend components, geometry helpers, form validators, and render-condition branches in the Goal Seek tab.
    Both passes enumerate every reachable branch and match each to a named test (xUnit / vitest / Playwright). Unreachable / defensive-default branches are documented with rationale, following M-040's pattern.

## Technical Notes

### Backend

- **`GoalSeekTracePoint` record** in `FlowTime.TimeMachine.Sweep` — `(int Iteration, double ParamValue, double MetricMean, double SearchLo, double SearchHi)`. Serializes to camelCase JSON automatically via existing endpoint serialization settings.
- **`OptimizeTracePoint` record** in `FlowTime.TimeMachine.Sweep` — `(int Iteration, IReadOnlyDictionary<string, double> ParamValues, double MetricMean)`.
- **Trace buffer inside the runners** — accumulate in a `List<...>` and hand the result to `MakeResult` / `Converged` / `NotConverged` helpers. Avoid allocating per-iteration closures.
- **Max trace size** — bounded by `maxIterations + 2` for goal-seek and `maxIterations + 1` for optimize. No separate cap needed.
- **Serialization** — endpoint response records already use System.Text.Json camelCase; adding `Trace` on both response records picks up the same convention. Verify with a round-trip test.
- **.NET CLI (M-011) impact** — the `goal-seek` and `optimize` CLI subcommands pipe JSON through; the new `trace` field appears automatically. No CLI code change required; add a CLI test confirming trace is present in the JSON output.

### Frontend

- **API client addition** (`ui/src/lib/api/flowtime.ts`):

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
  ```

  The matching `flowtime.optimize(...)` method is owned by M-042 — draft preserved in that milestone's Technical Notes.

- **`ConvergenceChart.svelte`** — pure SVG, ~80-120 lines. Consumes the normalized `Array<{ iteration, metricMean }>` shape (see AC5). Geometry in `convergence-chart-geometry.ts` handles y-domain computation, point projection, target-line y-coord, and multi-point-at-same-x placement (goal-seek's two `iteration: 0` entries). Single-series line for simplicity; no legend. Uses `--ft-viz-*` palette tokens; teal when `converged`, amber otherwise; dashed horizontal reference line at `target` when provided. Axis labels: `iteration` on x, `yLabel` prop (caller-supplied, e.g. "metric mean" or "queue.queueTimeMs") on y.

- **`AnalysisResultCard.svelte`** — compact card using existing density tokens. Header (title + converged badge), primary value (large monospace), meta grid (iterations, tolerance, direction, target if present). No new shadcn components required.

- **Interval bar + per-param range bars** — extracted into `interval-bar-geometry.ts` with vitest. Reused by Goal Seek's interval visualization (single bar) and by Optimize's per-param table (one mini bar per row) — the file lands here, M-042 reuses it.

- **Form state** — co-located in the route component using `$state` runes. Promote to a `goal-seek-state.svelte.ts` store only if readability degrades; M-040 kept state local and that's the baseline to beat.

- **Scenario-change reset** — when `selectedRunId` or `selectedSampleId` changes, reset the Goal Seek form. Wire into the same reactivity that already drives scenario changes in `/analysis`.

- **Error messaging** — reuse the existing error surface pattern from M-040; do not introduce a new toast or modal system.

- **Density / styling** — small inputs, tight gutters, 8–12 px steps. Use the analysis page's existing typography scale; no new font sizes.

## Out of Scope

- Optimize tab surface — separate milestone **M-042**.
- Per-evaluation probe log for optimize (raw reflection/expansion/contraction/shrink intermediate vertices). The exposed trace is per-iteration best only.
- Multi-objective / Pareto optimization (not in the engine).
- Constraints on optimization (deferred — tracked in `work/gaps.md`).
- History panel for past goal-seek / optimize runs.
- Exporting results (CSV, JSON download).
- Persisting form values to `localStorage` across browser sessions.
- Keyboard shortcuts beyond what the analysis page already supports.
- Server-side parameter discovery (browser-side `discoverConstParams` still owns this).
- Trace extension on `/v1/sweep` or `/v1/sensitivity` (not needed; not covered by D-047).

## Dependencies

- M-040 (complete) — analysis route shell, tab bar, run/sample picker, param discovery, density tokens, inline-error pattern, sensitivity bar geometry (as a template for the interval bar + convergence chart geometry).
- `POST /v1/goal-seek`, `POST /v1/optimize` — available on port 8081 against `RustEngine:Enabled=true`. Both covered by existing API tests (`GoalSeekEndpointsTests.cs`, `OptimizeEndpointsTests.cs`) that this milestone extends with trace-shape assertions.
- D-047 — appended to `work/decisions.md` at start-milestone time; covers AC3.
- Sample models bundled at `ui/src/lib/utils/sample-models.ts`. At least one sample must have const nodes, a reachable metric target (for the happy-path Playwright goal-seek), and accommodate the unreachable-target case from AC12.

## Notes

- **Branch name vs milestone title.** The milestone branch is `milestone/m-E21-04-goal-seek-optimize` — it keeps its original name after the split because it already carries the Phase 1 backend commit (`29ac3e9`) and is referenced across CLAUDE.md Current Work and status surfaces. The branch name is the one documented mismatch with the renamed milestone folder (`m-E21-04-goal-seek`); all other surfaces reflect the new title.

## Coverage Notes

See `m-E21-04-goal-seek-tracking.md` sections "Phase 1 — Branch-coverage audit" (backend) and "Coverage Notes → UI pass" (frontend) for the full line-by-line audit. Each reachable branch is matched to a named xUnit / vitest / Playwright test; defensive / unreachable branches are enumerated with rationale.
