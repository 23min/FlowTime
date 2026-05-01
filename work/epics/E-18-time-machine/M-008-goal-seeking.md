---
id: M-008
title: Goal Seeking
status: done
parent: E-18
acs:
  - id: AC-1
    title: GoalSeekSpec validates
    status: met
  - id: AC-2
    title: GoalSeeker.SeekAsync converges on a linear model to within tolerance
    status: met
  - id: AC-3
    title: GoalSeeker returns Converged=false when target is not bracketed
    status: met
  - id: AC-4
    title: GoalSeeker returns Converged=false (best guess) when max iterations
    status: met
  - id: AC-5
    title: GoalSeeker respects CancellationToken
    status: met
  - id: AC-6
    title: POST /v1/goal-seek returns 400 for missing/invalid required fields
    status: met
  - id: AC-7
    title: POST /v1/goal-seek returns 503 when engine not enabled
    status: met
  - id: AC-8
    title: 'Unit tests pass: 26 tests (GoalSeekSpec ×14, GoalSeeker ×12)'
    status: met
  - id: AC-9
    title: 'API tests pass: 8 tests (7×400, 1×503)'
    status: met
  - id: AC-10
    title: dotnet test FlowTime.sln all green (163 TimeMachine, 250 API)
    status: met
---

## Goal

Add 1D goal seeking: given a model YAML, a const-node parameter, a metric series, and a
target value, find the parameter value that drives the metric mean to the target via bisection.
Answers "what arrival rate gives 80% utilization?" without a full parameter sweep.

Builds on:
- M-006 `SweepRunner` + `ConstNodePatcher` / `ConstNodeReader` (M-007)
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
- Multi-dimensional optimization (Nelder-Mead) — M-009+
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

## Acceptance criteria

### AC-1 — GoalSeekSpec validates

`GoalSeekSpec` validates: non-null/whitespace ModelYaml/ParamId/MetricSeriesId;
SearchLo < SearchHi; Tolerance > 0; MaxIterations ≥ 1
### AC-2 — GoalSeeker.SeekAsync converges on a linear model to within tolerance

`GoalSeeker.SeekAsync` converges on a linear model to within tolerance
### AC-3 — GoalSeeker returns Converged=false when target is not bracketed

`GoalSeeker` returns `Converged=false` when target is not bracketed
### AC-4 — GoalSeeker returns Converged=false (best guess) when max iterations

`GoalSeeker` returns `Converged=false` (best guess) when max iterations exhausted
### AC-5 — GoalSeeker respects CancellationToken

`GoalSeeker` respects `CancellationToken`
### AC-6 — POST /v1/goal-seek returns 400 for missing/invalid required fields

`POST /v1/goal-seek` returns 400 for missing/invalid required fields
### AC-7 — POST /v1/goal-seek returns 503 when engine not enabled

`POST /v1/goal-seek` returns 503 when engine not enabled
### AC-8 — Unit tests pass: 26 tests (GoalSeekSpec ×14, GoalSeeker ×12)

### AC-9 — API tests pass: 8 tests (7×400, 1×503)

### AC-10 — dotnet test FlowTime.sln all green (163 TimeMachine, 250 API)

`dotnet test FlowTime.sln` all green (163 TimeMachine, 250 API)
