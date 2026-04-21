# Tracking: Goal Seek Surface

**Milestone:** m-E21-04-goal-seek
**Branch:** milestone/m-E21-04-goal-seek-optimize (pre-existing; intentionally unchanged after the split — see spec Notes)
**Started:** 2026-04-21
**Status:** in progress

## Scope Recap

Wire the `/analysis` Goal Seek tab to the live `/v1/goal-seek` Time Machine endpoint and ship the shared `AnalysisResultCard` + `ConvergenceChart` components used by both Goal Seek here and the Optimize surface in m-E21-05. Extend `/v1/goal-seek` and `/v1/optimize` response shapes with an additive `trace` field (per D-2026-04-21-034); the optimize trace is implemented here because it ships under the same decision record but is consumed in m-E21-05. See spec `work/epics/E-21-svelte-workbench-and-analysis/m-E21-04-goal-seek.md`.

### Split note (2026-04-21)

This milestone was originally scoped as `m-E21-04-goal-seek-optimize` (16 ACs, backend + shared components + two surfaces). On 2026-04-21, after Phase 1 backend had landed, the milestone was split: Goal Seek stays on m-E21-04; Optimize moves to **m-E21-05 Optimize Surface** (new). Heatmap / Validation / Polish renumbered to 06 / 07 / 08. The Phase 1 backend commit (`29ac3e9`) is kept — AC1/AC2/AC3 below are already complete and covered its implementation of both goal-seek **and** optimize traces. AC4–AC13 below are the rescoped frontend + audit work that remains on this milestone.

## Acceptance Criteria

### Backend — trace extension (AC1-AC3)
- [x] AC1: Goal-seek trace plumbed end-to-end (`GoalSeeker`, `GoalSeekResult`, endpoint); all five return paths covered by `GoalSeekEndpointsTraceTests` + `GoalSeekerTests`
- [x] AC2: Optimize trace plumbed end-to-end (`Optimizer`, `OptimizeResult`, endpoint); pre-loop + per-iteration entries; unsigned-metric invariant for both objectives; `OptimizeEndpointsTraceTests` + `OptimizerTests`. _The Optimize surface that consumes this trace ships in m-E21-05._
- [x] AC3: `D-2026-04-21-034` appended to `work/decisions.md`; E-21 epic spec Scope / Constraints updated to reference the new decision alongside D-2026-04-17-033 (landed in the start-milestone commit `5988f5c`)

### UI — Tab activation + shared components (AC4-AC5)
- [ ] AC4: Goal-seek tab panel renders live content (no `coming in m-E21-04` stub); optimize tab placeholder copy updated to reference m-E21-05
- [ ] AC5: Shared `analysis-result-card.svelte` and `convergence-chart.svelte` components land first; chart consumes normalized `Array<{ iteration, metricMean }>`; pure-ts siblings with vitest geometry coverage (components reused by m-E21-05)

### UI — Goal Seek surface (AC6-AC9)
- [ ] AC6: Param single-select (reuses `discoverConstParams`); `{id} (base {baseline})` format; empty state when no const params
- [ ] AC7: `searchLo` / `searchHi` defaults to `0.5 × baseline` / `2 × baseline`; metric picker with Sensitivity chip shortcuts; `target` numeric input; Advanced disclosure for `tolerance` (1e-6) and `maxIterations` (50); inline validation
- [ ] AC8: Run goal-seek renders shared result card (paramValue, achievedMetricMean, target, residual, converged badge, iterations) + shared convergence chart (line + target reference line + two iteration-0 boundary points) + search-interval bar (SVG via `intervalMarkerGeometry`, marker at final paramValue); 400/503 inline errors
- [ ] AC9: Not-bracketed state (amber warning, chart shows only two boundary points); not-converged state (amber "did not converge" badge, full trace)

### Cross-cutting (AC10-AC12)
- [ ] AC10: Session form state — Goal Seek form retains input across tab switches; resets when scenario changes (mirrors Sweep). Optimize session state is m-E21-05 scope.
- [ ] AC11: Vitest branch coverage for `defaultSearchBounds`, `validateSearchInterval`, `intervalMarkerGeometry`, `convergence-chart-geometry`, plus any `analysis-result-card-geometry` helpers if extracted. `validateOptimizeForm` is out of scope here (m-E21-05).
- [ ] AC12: Playwright coverage in `tests/ui/specs/svelte-analysis.spec.ts` (or sibling spec): goal-seek happy path + not-bracketed deterministic repro (tuple recorded in Notes below); graceful skip when API/dev server is down. Optimize Playwright is m-E21-05 scope.

### Branch-coverage audit (AC13)
- [x] AC13a (backend pass — complete below): Line-by-line branch audit — five goal-seek return paths + pre-loop/main-loop/shrink branches on Nelder-Mead; unreachable/defensive branches documented in Coverage Notes
- [ ] AC13b (UI pass — pending): new UI components/helpers audited before commit-approval prompt

## Implementation Log

| Phase | What | Tests | Status |
|-------|------|-------|--------|
| 1 | Decision + status sync (D-2026-04-21-034, epic spec, CLAUDE.md, roadmap) | — | done (start-milestone) |
| 2 | Backend: `GoalSeekTracePoint`, `OptimizeTracePoint`, runner trace plumbing, endpoint response records | 12 unit (GoalSeeker+Optimizer) + 12 API (endpoint trace shape) + 2 CLI integration = 26 new | **done (commit `29ac3e9`)** |
| — | **Scope split (2026-04-21)**: `m-E21-04-goal-seek-optimize` split into `m-E21-04-goal-seek` + new `m-E21-05-optimize`; downstream milestones renumbered to 06/07/08 | — | done |
| 3 | Shared components: `analysis-result-card.svelte`, `convergence-chart.svelte` + `convergence-chart-geometry.ts` + `interval-bar-geometry.ts` | vitest | pending |
| 4 | API client: `flowtime.goalSeek` method (optimize method lives on m-E21-05) | — | pending |
| 5 | Goal Seek tab surface (param select, bounds, target, Advanced, run, result + chart) | vitest + Playwright | pending |
| 6 | Session form state + scenario-change reset (goal-seek only) | vitest / Playwright | pending |
| 7 | Line-by-line branch audit + Coverage Notes for UI pass | — | phase-1 audit done below; UI pass pending |

### Phase 1 (Backend) — Implementation Log

**Landed in milestone branch commit `29ac3e9`.** Covers both goal-seek and optimize trace plumbing — they share D-2026-04-21-034. The optimize endpoint trace is ready for m-E21-05 to consume without further backend work.

#### Files added
- `src/FlowTime.TimeMachine/Sweep/GoalSeekTracePoint.cs` — record `(int Iteration, double ParamValue, double MetricMean, double SearchLo, double SearchHi)` with XML docs capturing AC1 semantics.
- `src/FlowTime.TimeMachine/Sweep/OptimizeTracePoint.cs` — record `(int Iteration, IReadOnlyDictionary<string, double> ParamValues, double MetricMean)` with XML docs calling out the unsigned-metric invariant for maximize.

#### Files modified
- `src/FlowTime.TimeMachine/Sweep/GoalSeekResult.cs` — added `Trace` (defaults to empty array so non-runner consumers don't trip `required` init).
- `src/FlowTime.TimeMachine/Sweep/OptimizeResult.cs` — same.
- `src/FlowTime.TimeMachine/Sweep/GoalSeeker.cs` — accumulates trace in a pre-sized `List<GoalSeekTracePoint>`; two iteration-0 boundary entries (both carry the spec's original bracket); each bisection step records **post-step** `(searchLo, searchHi)` after narrowing; all five return paths hand the buffer to `Converged` / `NotConverged`.
- `src/FlowTime.TimeMachine/Sweep/Optimizer.cs` — trace entry recorded once after the pre-loop `Sort` (`iteration: 0`) and once per main-loop iteration after the post-iteration `Sort` (`iteration: 1..N`). `metricMeans[0]` is already unsigned (the sign flip lives only in `fValues`), so maximize runs naturally emit positive `MetricMean` without a second negate.
- `src/FlowTime.API/Endpoints/GoalSeekEndpoints.cs` — `GoalSeekResponse` gained `IReadOnlyList<GoalSeekTracePoint> Trace`; handler passes `result.Trace` through unchanged.
- `src/FlowTime.API/Endpoints/OptimizeEndpoints.cs` — symmetric.
- `tests/FlowTime.TimeMachine.Tests/Sweep/GoalSeekerTests.cs` — 7 trace-shape tests (one per return path + ordering + metricMean matching evaluator output).
- `tests/FlowTime.TimeMachine.Tests/Sweep/OptimizerTests.cs` — 7 trace-shape tests (3 exit-path-length assertions + min/max unsigned invariants + 2D params + shrink path).
- `tests/FlowTime.Api.Tests/GoalSeekEndpointsTests.cs` — new `GoalSeekEndpointsTraceTests` class + inline test factory with fake `LinearEvaluator` (one test per return path + camelCase JSON shape = 6 tests).
- `tests/FlowTime.Api.Tests/OptimizeEndpointsTests.cs` — new `OptimizeEndpointsTraceTests` class + inline test factory (3 exit-path tests + maximize unsigned-metric + camelCase JSON shape = 5 tests).
- `tests/FlowTime.Integration.Tests/TimeMachineCliIntegrationTests.cs` — 2 new CLI-passthrough tests confirming `trace` appears in `GoalSeekCommand` / `OptimizeCommand` JSON output (real Rust engine, skip-on-missing).

#### Test counts
- `FlowTime.TimeMachine.Tests` — 238 passed / 0 failed (prior baseline 226; +12 trace tests).
- `FlowTime.Api.Tests` — 275 passed / 0 failed (prior baseline 263; +12 endpoint trace tests).
- `FlowTime.Integration.Tests` — 80 passed / 0 failed (prior baseline 78; +2 CLI passthrough tests).
- Full solution: 1,729 passed / 9 skipped / 0 failed across 10 test projects.

#### Design notes
- **Unsigned `metricMean` emitted naturally, not post-hoc.** The Optimizer's internal sign flip lives in `fValues` (the quantity used for simplex ordering / tolerance comparison); `metricMeans[]` stays in user space throughout. Maximize runs therefore emit positive trace entries without a separate reverse-sign step at record time. This is asserted explicitly by `OptimizeAsync_MaximizeLinear_TraceMetricsAreUnsigned` (unit) and `Maximize_TraceMetricMean_IsUnsigned` (API).
- **Pre-sized trace buffers.** Both runners pre-allocate the `List<>` to the upper bound (`MaxIterations + 2` for goal-seek, `MaxIterations + 1` for optimize) to avoid `List.Add` growth allocations on the hot bisection / simplex path.
- **`Trace` with default empty array (not `required`) on the result records.** Lets existing unit tests that construct `GoalSeekResult` / `OptimizeResult` manually keep working without a touch, while the runners always replace the default with their accumulated list.
- **Test factory pattern.** `GoalSeekEndpointsTraceTests` and `OptimizeEndpointsTraceTests` each define a one-off `TestWebApplicationFactory` subclass that flips `RustEngine:Enabled=true` + `UseSession=false` and swaps `IModelEvaluator` for a deterministic fake via `ConfigureTestServices` / `services.RemoveAll<IModelEvaluator>()`. This exercises the real endpoint + runner pipeline without touching the Rust binary, so every return path is deterministic on CI.

### Phase 1 — Branch-coverage audit

Line-by-line audit of backend changes. Every reachable branch is exercised by at least one named test. Unreachable branches documented with rationale. _This audit stays useful for m-E21-05 as reference for the optimize-side paths it consumes._

#### `GoalSeeker.SeekAsync` — five return paths + decision branches

| Branch | Condition | Named test(s) |
|--------|-----------|---------------|
| Boundary @ searchLo | `Math.Abs(meanLo − Target) < Tolerance` | `GoalSeekerTests.SeekAsync_TargetAtLoBoundary_ConvergesQuickly`, `...SeekAsync_ConvergedAtSearchLo_TraceHasOnlyBoundaryEntries`, `GoalSeekEndpointsTraceTests.Converged_AtSearchLo_TraceHasTwoBoundaryEntries` |
| Boundary @ searchHi | `Math.Abs(meanHi − Target) < Tolerance` (after lo-check fails) | `GoalSeekerTests.SeekAsync_TargetAtHiBoundary_ConvergesQuickly`, `...SeekAsync_ConvergedAtSearchHi_TraceHasOnlyBoundaryEntries`, `GoalSeekEndpointsTraceTests.Converged_AtSearchHi_TraceHasTwoBoundaryEntries` |
| Not bracketed | `Math.Sign(loResidual) == Math.Sign(hiResidual)` | `GoalSeekerTests.SeekAsync_TargetAboveMetricRange_ReturnsNotConverged`, `...SeekAsync_TargetBelowMetricRange_ReturnsNotConverged`, `...SeekAsync_ConstantMetric_ReturnsNotConverged`, `...SeekAsync_NotBracketed_TraceHasOnlyBoundaryEntries`, `GoalSeekEndpointsTraceTests.NotBracketed_TraceHasTwoBoundaryEntriesOnly` |
| Tolerance hit mid-loop | `Math.Abs(midMean − Target) < Tolerance` (inside loop) | `GoalSeekerTests.SeekAsync_LinearModel_ConvergesToTarget`, `...SeekAsync_ToleranceHitMidLoop_TraceIncludesBisectionSteps`, `GoalSeekEndpointsTraceTests.ConvergedMidLoop_TraceHasBoundariesPlusIterations` |
| Narrow lo (midResidual sign == loResidual sign) | `Math.Sign(midResidual) == Math.Sign(currentMeanLo − Target)` | `GoalSeekerTests.SeekAsync_MaxIterationsExhausted_TraceHasBoundariesPlusMaxIterations` (iter 2 at target=33 moves lo from 0 → 25) |
| Narrow hi (else branch) | signs opposite → `hi := mid` | same test, iter 1 at target=33 moves hi from 100 → 50; `GoalSeekEndpointsTraceTests.MaxIterationsExhausted_TraceHasBoundariesPlusMaxIterations` |
| Max-iterations exhausted | `iteration == spec.MaxIterations` after narrowing | `GoalSeekerTests.SeekAsync_MaxIterationsExhausted_ReturnsNotConverged`, `...SeekAsync_MaxIterationsExhausted_TraceHasBoundariesPlusMaxIterations`, `GoalSeekEndpointsTraceTests.MaxIterationsExhausted_TraceHasBoundariesPlusMaxIterations` |
| Cancellation | `cancellationToken.ThrowIfCancellationRequested` | `GoalSeekerTests.SeekAsync_CancelledToken_Throws` |
| Null guard | `ArgumentNullException.ThrowIfNull(spec)` | `GoalSeekerTests.SeekAsync_NullSpec_Throws` |
| Empty metric series in `EvaluateMeanAsync` | `!series.TryGetValue(...) || values.Length == 0` | defensive — see Coverage Notes below |
| Constructor null | `sweepRunner == null` | `GoalSeekerTests.Constructor_NullSweepRunner_Throws` |

#### `Optimizer.OptimizeAsync` — new trace recordings + existing paths

| Branch | Condition | Named test(s) |
|--------|-----------|---------------|
| Pre-loop trace record | unconditional after pre-loop `Sort` | `OptimizerTests.OptimizeAsync_PreLoopConvergence_TraceHasSingleEntry`, `OptimizeEndpointsTraceTests.PreLoopConvergence_TraceHasSingleIterationZeroEntry` |
| Pre-loop converged return | `fValues[n] − fValues[0] < Tolerance` before loop | same two tests |
| Per-iteration trace record | unconditional after main-loop `Sort` | `OptimizerTests.OptimizeAsync_MainLoopConvergence_TraceLengthMatchesIterationsPlusOne`, `OptimizeEndpointsTraceTests.MainLoopConvergence_TraceLengthEqualsIterationsPlusOne` |
| Main-loop converged return | `fValues[n] − fValues[0] < Tolerance` in loop | `OptimizerTests.OptimizeAsync_1DBowl_Minimize_ConvergesToMinimum`, `...OptimizeAsync_MainLoopConvergence_TraceLengthMatchesIterationsPlusOne`, `OptimizeEndpointsTraceTests.MainLoopConvergence_TraceLengthEqualsIterationsPlusOne` |
| Max-iterations-exhausted return | `iteration == MaxIterations` | `OptimizerTests.OptimizeAsync_MaxIterationsExhausted_ReturnsNotConverged`, `...OptimizeAsync_MaxIterationsExhausted_TraceLengthMatchesIterationsPlusOne`, `OptimizeEndpointsTraceTests.MaxIterationsExhausted_TraceLengthEqualsIterationsPlusOne` |
| Reflection better than best → expansion kept | `fr < fValues[0]` then `fe < fr` | `OptimizerTests.OptimizeAsync_1DBowl_Minimize_ConvergesToMinimum` (standard convergence run exercises expansion-accepted path) |
| Reflection better than best → expansion rejected | `fr < fValues[0]` then `fe >= fr` | `OptimizerTests.OptimizeAsync_AbsMetric_ExpansionRejected_AcceptsReflection` |
| Reflection better than second-worst | `fr < fValues[n-1]` | `OptimizerTests.OptimizeAsync_1DBowl_Minimize_ConvergesToMinimum` + `...OptimizeAsync_MaxIterationsExhausted_ReturnsNotConverged` (both exercise middling reflections) |
| Outside contraction accepted | `fr < fValues[n]` then `foc <= fr` | exercised by `OptimizerTests.OptimizeAsync_1DBowl_Minimize_ConvergesToMinimum` during convergence |
| Outside contraction rejected → shrink | `fr < fValues[n]` then `foc > fr` | `OptimizerTests.OptimizeAsync_ShrinkPath_BothContractionVariantsCovered` iter 1, `...OptimizeAsync_ShrinkPath_TraceStillCapturesPerIteration` |
| Inside contraction accepted | `fr >= fValues[n]` then `fic < fValues[n]` | `OptimizerTests.OptimizeAsync_LinearMetric_Maximize_ConvergesToUpperBound` exercises this during late convergence |
| Inside contraction rejected → shrink | `fr >= fValues[n]` then `fic >= fValues[n]` | `OptimizerTests.OptimizeAsync_ShrinkPath_BothContractionVariantsCovered` iter 2, `...OptimizeAsync_ShrinkPath_TraceStillCapturesPerIteration` |
| Shrink block `if (shrink)` | either contraction rejection path | both shrink tests above |
| `sign = 1` (minimize) | `Objective == Minimize` | multiple (all minimize tests) |
| `sign = -1` (maximize) | `Objective == Maximize` | `OptimizerTests.OptimizeAsync_LinearMetric_Maximize_ConvergesToUpperBound`, `...OptimizeAsync_MaximizeLinear_TraceMetricsAreUnsigned`, `OptimizeEndpointsTraceTests.Maximize_TraceMetricMean_IsUnsigned` |
| `MakeTracePoint` paramIds loop | iterates `paramIds.Count` times, always reachable | `OptimizerTests.OptimizeAsync_2DBowl_TraceParamValuesContainAllParams` (2 params), `OptimizerTests.OptimizeAsync_PreLoopConvergence_TraceHasSingleEntry` (1 param) |
| Clamp hi | `v > hi` | exercised whenever expansion or reflection oversteps a bound — covered in `OptimizeAsync_LinearMetric_Maximize_ConvergesToUpperBound` |
| Clamp lo | `v < lo` | exercised by shrink / contraction runs that drag vertices toward a bound |
| Empty metric series in `EvaluateAsync` | `!series.TryGetValue(...) \|\| values.Length == 0` | defensive — see Coverage Notes below |
| Null guards (`spec`, `evaluator`) | constructor + null spec | `OptimizerTests.Constructor_NullEvaluator_Throws`, `...OptimizeAsync_NullSpec_Throws` |
| Cancellation | `cancellationToken.ThrowIfCancellationRequested` (both pre-eval and per-iter) | `OptimizerTests.OptimizeAsync_CancelledToken_Throws` |

#### `GoalSeekEndpoints` / `OptimizeEndpoints`

Response construction adds `Trace` to each respective response record unconditionally. No new branches beyond the existing 400 / 503 validation paths. The JSON camelCase wire shape is asserted explicitly by `Response_TraceField_UsesCamelCasePropertyNames` on both endpoints.

## Coverage Notes

### Genuinely unreachable branches (documented, not tested)

1. **`GoalSeeker.SeekAsync` final unreachable `return NotConverged(...)` (line ~95).** The bisection loop runs from `iteration = 1..MaxIterations` inclusive and **always** returns on some iteration — either via the tolerance-hit branch or via the `iteration == MaxIterations` branch. Falling off the loop is mathematically impossible with `MaxIterations >= 1` (enforced by `GoalSeekSpec` constructor). Kept as a defensive programming safeguard.

2. **`Optimizer.OptimizeAsync` final unreachable `return MakeResult(...)` (line ~165).** Same shape as goal-seek's fall-through: the main loop body either converges (line ~161) or hits `iteration == MaxIterations` (line ~164) on every iteration, so the post-loop return is unreachable given `MaxIterations >= 1` (enforced by `OptimizeSpec` constructor). Defensive.

3. **`GoalSeeker.EvaluateMeanAsync` empty-series guard (`!series.TryGetValue(...) || values.Length == 0` → returns 0.0).** The `SweepRunner` in production always evaluates against a model that produces the requested metric series with the grid's bin count (non-empty). The guard defends against misconfigured fake evaluators at test time; since the two fakes in `GoalSeekerTests` always return a populated series, this branch is not exercised. Adding a fake evaluator just to exercise this branch would be test bloat for a pure safeguard — consistent with the m-E18-13 convention of naming such defensive branches in Coverage Notes rather than forcing a test.

4. **`Optimizer.EvaluateAsync` empty-series guard (same shape).** Same rationale as #3.

These four branches are the only reachable-looking code paths not covered by explicit tests. Every other reachable branch in the backend changes is named in the audit table above.

### UI pass — to be filled at wrap

Follow m-E21-03's structure: pure-logic tests, component rendering covered by Playwright, defensive / unreachable branches named with rationale, normalized-shape adaptation branches on the chart geometry.

## Test Summary

### Phase 1 — Backend

- `FlowTime.TimeMachine.Tests` — 238 passed / 0 failed (+12 trace tests).
- `FlowTime.Api.Tests` — 275 passed / 0 failed (+12 endpoint trace-shape tests).
- `FlowTime.Integration.Tests` — 80 passed / 0 failed (+2 CLI passthrough trace tests).
- Full .NET solution — 1,729 passed / 9 skipped / 0 failed across 10 projects.
- Playwright / vitest — not yet touched in this phase.

## Files Changed

### Phase 1 — Backend (commit `29ac3e9`)

#### New files
- `src/FlowTime.TimeMachine/Sweep/GoalSeekTracePoint.cs`
- `src/FlowTime.TimeMachine/Sweep/OptimizeTracePoint.cs`

#### Modified files
- `src/FlowTime.TimeMachine/Sweep/GoalSeekResult.cs` — added `Trace` property.
- `src/FlowTime.TimeMachine/Sweep/OptimizeResult.cs` — added `Trace` property.
- `src/FlowTime.TimeMachine/Sweep/GoalSeeker.cs` — trace plumbing through all five return paths.
- `src/FlowTime.TimeMachine/Sweep/Optimizer.cs` — trace plumbing through pre-loop + main-loop exits; unsigned `metricMean` at record time.
- `src/FlowTime.API/Endpoints/GoalSeekEndpoints.cs` — response carries `Trace`.
- `src/FlowTime.API/Endpoints/OptimizeEndpoints.cs` — response carries `Trace`.
- `tests/FlowTime.TimeMachine.Tests/Sweep/GoalSeekerTests.cs` — 7 new trace tests.
- `tests/FlowTime.TimeMachine.Tests/Sweep/OptimizerTests.cs` — 7 new trace tests.
- `tests/FlowTime.Api.Tests/GoalSeekEndpointsTests.cs` — new `GoalSeekEndpointsTraceTests` (6 tests) + factory.
- `tests/FlowTime.Api.Tests/OptimizeEndpointsTests.cs` — new `OptimizeEndpointsTraceTests` (5 tests) + factory.
- `tests/FlowTime.Integration.Tests/TimeMachineCliIntegrationTests.cs` — 2 CLI-passthrough tests.

### Split (docs-only, 2026-04-21)
- Rename `work/epics/E-21-svelte-workbench-and-analysis/m-E21-04-goal-seek-optimize.md` → `m-E21-04-goal-seek.md` (rescoped).
- Rename `work/epics/E-21-svelte-workbench-and-analysis/m-E21-04-goal-seek-optimize-tracking.md` → `m-E21-04-goal-seek-tracking.md` (this file).
- New `work/epics/E-21-svelte-workbench-and-analysis/m-E21-05-optimize.md` — Optimize Surface milestone spec.
- Epic spec, `ROADMAP.md`, `work/epics/epic-roadmap.md`, `CLAUDE.md` Current Work updated to reflect the split and renumber downstream milestones (heatmap → 06, validation → 07, polish → 08).

## Notes

- **Deterministic Playwright repro for AC12 not-bracketed path (resolved 2026-04-21):**
  - Model id: `coffee-shop` (first entry in `SAMPLE_MODELS`, `ui/src/lib/utils/sample-models.ts:47`)
  - First discovered const param id: `customers_per_hour`
  - Baseline: `22` (first value of the const node's `values` array, per `discoverConstParams`)
  - `searchLo: 11` (0.5 × baseline), `searchHi: 44` (2 × baseline)
  - `metricSeriesId: "served"` (Sensitivity chip shortcut — verify the exact engine-emitted id at authoring time; if the actual series is namespaced (e.g. `Register.served`), update this tuple + the spec's AC12 together)
  - `target: 1e12` — unreachable by construction (baseline served rate is 20/hr; max reachable served mean across any `customers_per_hour ∈ [11, 44]` is far below 1e12)
  - Expected result: `converged: false`, `iterations: 0`, trace has exactly two `iteration: 0` entries at paramValue 11 and 44. Amber warning in UI. Chart renders only the two boundary points.
  - If sample-models.ts changes and breaks this tuple, the Playwright test fails loudly (not a silent flake) — update the tuple here + the spec's AC12 in the same commit.
- **Chart normalized input shape (AC5):** the caller adapts its response — Goal Seek emits `trace.map(p => ({ iteration: p.iteration, metricMean: p.metricMean }))` with both `iteration: 0` entries preserved. Optimize (m-E21-05) will emit the same projection over its own trace. The chart never branches on surface type.
- **Nelder-Mead sign convention check:** `src/FlowTime.TimeMachine/Sweep/Optimizer.cs` verified at lines 68/72/141/144 — pre-loop sort + per-iteration sort both occur before the trace capture point, and the minimize-internally / maximize-externally sign flip lives around the metric evaluation so exposing the unsigned mean on the trace is a sign-reverse at record time (for maximize only). AC2 assertions lock this. _Still accurate after the split — the optimize trace landed on this milestone's commit `29ac3e9`._
- **CLI passthrough (m-E18-14):** `trace` appears automatically in CLI JSON output via pipe-through; covered by the 2 integration tests added in commit `29ac3e9` (no new CLI code required).

## Decisions & Gaps

- **D-2026-04-21-034** (appended to `work/decisions.md` at start-milestone commit `5988f5c`) — Additive `trace` field on `/v1/goal-seek` and `/v1/optimize` response shapes. Requests unchanged. Scope covers both endpoints; the split does not require a new decision. Implementation landed in commit `29ac3e9`.
- **Prior carve-out still in force:** D-2026-04-17-033 (read-only run-adjacent endpoints) — the `GET /v1/runs/{runId}/model` and any similar reads used by Goal Seek inherit this admission; no new admission is needed for them.

## Completion

(Filled at wrap — merge commit SHA, Playwright run summary, any deferred follow-ups.)
