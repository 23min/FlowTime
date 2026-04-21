# Tracking: Goal Seek & Optimization Surfaces

**Milestone:** m-E21-04-goal-seek-optimize
**Branch:** milestone/m-E21-04-goal-seek-optimize
**Started:** 2026-04-21
**Status:** in progress

## Scope Recap

Wire the remaining two `/analysis` tabs (Goal Seek + Optimize) to the live Time Machine endpoints, extend `/v1/goal-seek` and `/v1/optimize` response shapes with an additive `trace` field (per D-2026-04-21-034), and ship a shared convergence chart + result card so the two new surfaces reuse one visualization spine. Goal Seek drives one parameter to a target (bisection). Optimize searches N parameters under bounds to minimize or maximize an objective (Nelder-Mead). See spec `work/epics/E-21-svelte-workbench-and-analysis/m-E21-04-goal-seek-optimize.md`.

## Acceptance Criteria

### Backend — trace extension (AC1-AC3)
- [ ] AC1: Goal-seek trace plumbed end-to-end (`GoalSeeker`, `GoalSeekResult`, endpoint); all five return paths covered by `GoalSeekEndpointsTests` trace-shape assertions
- [ ] AC2: Optimize trace plumbed end-to-end (`Optimizer`, `OptimizeResult`, endpoint); pre-loop + per-iteration entries; unsigned-metric invariant for both objectives; `OptimizeEndpointsTests` trace coverage
- [ ] AC3: `D-2026-04-21-034` appended to `work/decisions.md`; E-21 epic spec Scope / Constraints updated to reference the new decision alongside D-2026-04-17-033 (this AC lands in the start-milestone commit)

### UI — Tab activation + shared components (AC4-AC5)
- [ ] AC4: `goal-seek` and `optimize` tab panels render live content (no `coming in m-E21-04` stubs)
- [ ] AC5: Shared `analysis-result-card.svelte` and `convergence-chart.svelte` components land first; chart consumes normalized `Array<{ iteration, metricMean }>`; pure-ts siblings with vitest geometry coverage

### UI — Goal Seek surface (AC6-AC9)
- [ ] AC6: Param single-select (reuses `discoverConstParams`); `{id} (base {baseline})` format; empty state when no const params
- [ ] AC7: `searchLo` / `searchHi` defaults to `0.5 × baseline` / `2 × baseline`; metric picker with Sensitivity chip shortcuts; `target` numeric input; Advanced disclosure for `tolerance` (1e-6) and `maxIterations` (50); inline validation
- [ ] AC8: Run goal-seek renders shared result card (paramValue, achievedMetricMean, target, residual, converged badge, iterations) + shared convergence chart (line + target reference line + two iteration-0 boundary points); 400/503 inline errors
- [ ] AC9: Not-bracketed state (amber warning, chart shows only two boundary points); not-converged state (amber "did not converge" badge, full trace)

### UI — Optimize surface (AC10-AC12)
- [ ] AC10: Chip-bar multi-select of const params at top; compact table with per-param `baseline` / `lo` / `hi` inputs below (only rendered when at least one chip active); defaults `0.5 × baseline` / `2 × baseline`; inline validation
- [ ] AC11: Metric picker with Sensitivity chip shortcuts; minimize/maximize toggle; Advanced disclosure for `tolerance` (1e-4) and `maxIterations` (200)
- [ ] AC12: Run optimize renders result card + per-param range table (with mini SVG range bars) + convergence chart (no target line; y-label reflects direction); 400/503 inline errors

### Cross-cutting (AC13-AC15)
- [ ] AC13: Session form state — Goal Seek + Optimize forms retain input across tab switches; reset when scenario changes (mirrors Sweep)
- [ ] AC14: Vitest branch coverage for `defaultSearchBounds`, `validateSearchInterval`, `validateOptimizeForm`, `intervalMarkerGeometry`, `convergence-chart-geometry`, plus any `analysis-result-card-geometry` helpers if extracted
- [ ] AC15: Playwright coverage in `tests/ui/specs/svelte-analysis.spec.ts` (or sibling spec): goal-seek happy path + not-bracketed deterministic repro (first `SAMPLE_MODELS` entry, first discovered const param, `target: 1e12`); optimize happy path; graceful skip when API/dev server is down

### Branch-coverage audit (AC16)
- [ ] AC16: Line-by-line branch audit before commit-approval prompt — five goal-seek return paths + pre-loop/main-loop/shrink branches on Nelder-Mead + new UI components/helpers; unreachable/defensive branches documented below in Coverage Notes

## Implementation Log

| Phase | What | Tests | Status |
|-------|------|-------|--------|
| 1 | Decision + status sync (D-2026-04-21-034, epic spec, CLAUDE.md, roadmap) | — | done (start-milestone) |
| 2 | Backend: `GoalSeekTracePoint`, `OptimizeTracePoint`, runner trace plumbing, endpoint response records | xUnit (existing tests extended) | pending |
| 3 | Shared components: `analysis-result-card.svelte`, `convergence-chart.svelte` + `convergence-chart-geometry.ts` + `interval-bar-geometry.ts` | vitest | pending |
| 4 | API client: `flowtime.goalSeek`, `flowtime.optimize` methods | — | pending |
| 5 | Goal Seek tab surface (param select, bounds, target, Advanced, run, result + chart) | vitest + Playwright | pending |
| 6 | Optimize tab surface (chip-bar, bounds table, direction toggle, Advanced, run, per-param table + chart) | vitest + Playwright | pending |
| 7 | Session form state + scenario-change reset | vitest / Playwright | pending |
| 8 | Line-by-line branch audit + Coverage Notes | — | pending |

## Test Summary

(Filled at wrap — vitest count, Playwright spec list, build status for Svelte + .NET.)

## Files Changed

### New files
(Filled during implementation.)

### Modified files
(Filled during implementation.)

## Notes

- **Deterministic Playwright repro for AC15 not-bracketed path:** resolve `SAMPLE_MODELS[0]` (module id, first discovered const param id, baseline) against live `ui/src/lib/utils/sample-models.ts` at implementation time; record the resolved tuple here so later sample-model edits surface as a spec failure, not a silent flake. Path to reconfirm with a one-line grep at implementation start.
- **Chart normalized input shape (AC5):** each caller adapts its response — Goal Seek emits `trace.map(p => ({ iteration: p.iteration, metricMean: p.metricMean }))` with both `iteration: 0` entries preserved; Optimize emits the same projection over its own trace. The chart never branches on surface type.
- **Nelder-Mead sign convention check:** `src/FlowTime.TimeMachine/Sweep/Optimizer.cs` verified at lines 68/72/141/144 — pre-loop sort + per-iteration sort both occur before the trace capture point, and the minimize-internally / maximize-externally sign flip lives around the metric evaluation so exposing the unsigned mean on the trace is a sign-reverse at record time (for maximize only). AC2 assertions must lock this.
- **CLI passthrough (m-E18-14):** `trace` appears automatically in CLI JSON output via pipe-through; add one integration test (no new CLI code required).

## Coverage Notes

(Filled at wrap — follow m-E21-03's structure: pure-logic tests, component rendering via Playwright, defensive / unreachable branches enumerated with rationale, the five goal-seek return paths + two Nelder-Mead exit paths each matched to a named xUnit test, normalized-shape adaptation branches on the chart geometry.)

## Decisions & Gaps

- **D-2026-04-21-034** (appended to `work/decisions.md` at start-milestone commit) — Additive `trace` field on `/v1/goal-seek` and `/v1/optimize` response shapes. Requests unchanged. Number claimed 2026-04-21; draft body in spec `m-E21-04-goal-seek-optimize.md` § "Decision Record (draft)".
- **Prior carve-out still in force:** D-2026-04-17-033 (read-only run-adjacent endpoints) — the `GET /v1/runs/{runId}/model` and any similar reads used by Goal Seek / Optimize inherit this admission; no new admission is needed for them.

## Completion

(Filled at wrap — merge commit SHA, Playwright run summary, any deferred follow-ups.)
