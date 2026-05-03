# m-E18-11 — Goal Seeking

**Epic:** E-18 Time Machine
**Branch:** `epic/E-18-time-machine`
**Status:** complete

## Goal

Add 1D goal seeking: given a model YAML, a const-node parameter, a metric series, and a
target value, find the parameter value that drives the metric mean to the target via bisection.
Answers "what arrival rate gives 80% utilization?" without a full parameter sweep.

Builds on:
- m-E18-09 `SweepRunner` + `ConstNodePatcher` / `ConstNodeReader` (m-E18-10)
- Same `IModelEvaluator` seam

## Scope

**`FlowTime.TimeMachine.Sweep` namespace:**
- `GoalSeekSpec` — validated input: ModelYaml, ParamId, MetricSeriesId, Target, SearchLo,
  SearchHi, Tolerance (default 1e-6), MaxIterations (default 50)
- `GoalSeekResult` — output: ParamValue, AchievedMetricMean, Converged, Iterations
- `GoalSeeker` — bisection over `SweepRunner`; handles non-bracketed case gracefully

**`POST /v1/goal-seek`** — in `src/FlowTime.API/Endpoints/GoalSeekEndpoints.cs`
- Request: `{ yaml, paramId, metricSeriesId, target, searchLo, searchHi, tolerance?, maxIterations? }`
- Response (200): `{ paramValue, achievedMetricMean, converged, iterations }`
- 400: missing/invalid required fields (searchLo ≥ searchHi is invalid)
- 503: engine not enabled

**In scope:**
- `src/FlowTime.TimeMachine/Sweep/GoalSeekSpec.cs`
- `src/FlowTime.TimeMachine/Sweep/GoalSeekResult.cs`
- `src/FlowTime.TimeMachine/Sweep/GoalSeeker.cs`
- `src/FlowTime.API/Endpoints/GoalSeekEndpoints.cs`
- DI registration in `Program.cs`
- Unit tests: `tests/FlowTime.TimeMachine.Tests/Sweep/`
- API tests: `tests/FlowTime.Api.Tests/GoalSeekEndpointsTests.cs`
- Architecture doc: `docs/architecture/time-machine-analysis-modes.md` (written alongside)

**Out of scope:**
- Multi-dimensional optimization (Nelder-Mead) — m-E18-12+
- Constraint handling beyond the `[searchLo, searchHi]` range
- Non-monotonic functions (bisection is undefined; `Converged=false` returned)

## Algorithm

Bisection on the metric mean:

```
1. Evaluate at searchLo → meanLo = mean(metric at searchLo)
2. Evaluate at searchHi → meanHi = mean(metric at searchHi)
3. If target not in [min(meanLo,meanHi), max(meanLo,meanHi)]:
       return best endpoint, Converged=false
4. While iterations < maxIterations:
       mid = (lo + hi) / 2
       midMean = mean(metric at mid)
       if |midMean - target| < tolerance: return mid, Converged=true
       if (midMean - target) same sign as (meanLo - target): lo = mid, meanLo = midMean
       else: hi = mid, meanHi = midMean
5. Return mid, Converged=false (max iterations reached)
```

## Acceptance Criteria

- [x] `GoalSeekSpec` validates: non-null/whitespace ModelYaml/ParamId/MetricSeriesId;
      SearchLo < SearchHi; Tolerance > 0; MaxIterations ≥ 1
- [x] `GoalSeeker.SeekAsync` converges on a linear model to within tolerance
- [x] `GoalSeeker` returns `Converged=false` when target is not bracketed
- [x] `GoalSeeker` returns `Converged=false` (best guess) when max iterations exhausted
- [x] `GoalSeeker` respects `CancellationToken`
- [x] `POST /v1/goal-seek` returns 400 for missing/invalid required fields
- [x] `POST /v1/goal-seek` returns 503 when engine not enabled
- [x] Unit tests pass: 26 tests (GoalSeekSpec ×14, GoalSeeker ×12)
- [x] API tests pass: 8 tests (7×400, 1×503)
- [x] `dotnet test FlowTime.sln` all green (163 TimeMachine, 250 API)
