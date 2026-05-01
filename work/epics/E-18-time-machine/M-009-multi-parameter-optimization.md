---
id: M-009
title: Multi-parameter Optimization
status: done
parent: E-18
acs:
  - id: AC-1
    title: OptimizeSpec validates
    status: met
  - id: AC-2
    title: Optimizer.OptimizeAsync converges on a 1D bowl function to within
    status: met
  - id: AC-3
    title: Optimizer.OptimizeAsync converges on a 2D bowl function to within
    status: met
  - id: AC-4
    title: Optimizer.OptimizeAsync supports Maximize objective (maximizes a
    status: met
  - id: AC-5
    title: Optimizer returns Converged=false when MaxIterations exhausted before
    status: met
  - id: AC-6
    title: Optimizer respects CancellationToken
    status: met
  - id: AC-7
    title: POST /v1/optimize returns 400 for missing/invalid required fields
    status: met
  - id: AC-8
    title: POST /v1/optimize returns 503 when engine not enabled
    status: met
  - id: AC-9
    title: 'Unit tests pass: 29 tests (OptimizeSpec ×17, Optimizer ×12)'
    status: met
  - id: AC-10
    title: 'API tests pass: 10 tests (9×400, 1×503)'
    status: met
  - id: AC-11
    title: dotnet test FlowTime.sln all green (192 TimeMachine, 260 API)
    status: met
---

## Goal

Add multi-parameter optimization: given a model, a set of const-node parameters with search
ranges, a metric series, and an objective (minimize or maximize), find the parameter values that
drive the metric mean to its optimum using Nelder-Mead simplex — a derivative-free method that
works for any number of parameters without needing gradients.

Answers "what combination of arrival rate and capacity minimizes queue depth?" without a full
multi-dimensional grid search.

Builds on:
- `IModelEvaluator` seam (M-006)
- `ConstNodePatcher` for multi-parameter YAML mutation (M-006)
- `ConstNodeReader` (M-007) — used in tests to read patched values

## Scope

**`FlowTime.TimeMachine.Sweep` namespace:**
- `OptimizeObjective` — `Minimize | Maximize` enum
- `SearchRange` — `record(double Lo, double Hi)` with `Lo < Hi` invariant
- `OptimizeSpec` — validated input: ModelYaml, ParamIds, MetricSeriesId, Objective,
  SearchRanges (one entry per ParamId), Tolerance (default 1e-4), MaxIterations (default 200)
- `OptimizeResult` — output: ParamValues, AchievedMetricMean, Converged, Iterations
- `Optimizer` — Nelder-Mead simplex over `IModelEvaluator`; patches all parameters
  simultaneously per evaluation; respects CancellationToken

**`POST /v1/optimize`** — in `src/FlowTime.API/Endpoints/OptimizeEndpoints.cs`
- Request: `{ yaml, paramIds, metricSeriesId, objective, searchRanges, tolerance?, maxIterations? }`
  where `searchRanges` is `{ "<paramId>": { "lo": N, "hi": N }, ... }`
  and `objective` is `"minimize"` or `"maximize"` (case-insensitive)
- Response (200): `{ paramValues, achievedMetricMean, converged, iterations }`
- 400: missing/invalid required fields, searchRange lo >= hi, unknown objective string
- 503: engine not enabled

**In scope:**
- `src/FlowTime.TimeMachine/Sweep/OptimizeObjective.cs`
- `src/FlowTime.TimeMachine/Sweep/SearchRange.cs`
- `src/FlowTime.TimeMachine/Sweep/OptimizeSpec.cs`
- `src/FlowTime.TimeMachine/Sweep/OptimizeResult.cs`
- `src/FlowTime.TimeMachine/Sweep/Optimizer.cs`
- `src/FlowTime.API/Endpoints/OptimizeEndpoints.cs`
- DI registration in `Program.cs`
- Unit tests: `tests/FlowTime.TimeMachine.Tests/Sweep/`
- API tests: `tests/FlowTime.Api.Tests/OptimizeEndpointsTests.cs`
- Architecture doc update: `docs/architecture/time-machine-analysis-modes.md`

**Out of scope:**
- Constraint handling (utilization < 0.8 etc.) — future milestone
- Bayesian optimization — future milestone
- Parallel evaluation of simplex vertices
- Gradient-based methods (sensitivity-driven descent)

## Algorithm

Nelder-Mead simplex (N parameters → N+1 vertices):

```
Coefficients: α=1.0 (reflect), γ=2.0 (expand), ρ=0.5 (contract), σ=0.5 (shrink)
Objective f(v) = metricMean(v)            for Minimize
             f(v) = -metricMean(v)           for Maximize  (internally always minimize f)

1. Build initial N+1 simplex:
   v[0]     = midpoint of all search ranges
   v[i]     = v[0] with param[i-1] shifted +5% of its range (clamped)

2. Evaluate f at each vertex.

3. Sort vertices so v[0] is best (lowest f) and v[N] is worst.

4. Check pre-loop convergence: if |f[N] - f[0]| < tolerance → Converged(0 iterations)

5. For iteration = 1 to MaxIterations:
   a. Compute centroid c of best N vertices (v[0]..v[N-1]).
   b. Reflect:  xr = c + α*(c - v[N]); clamp; fr = f(xr)
   c. if fr < f[0]:         expand: xe = c + γ*(xr-c); clamp; fe = f(xe)
                             replace v[N] with (fe<fr ? xe : xr)
      elif fr < f[N-1]:     replace v[N] with xr
      else:                 if fr < f[N]: outside contraction
                               xoc = c + ρ*(xr-c); clamp; foc = f(xoc)
                               replace v[N] with xoc if foc <= fr, else shrink
                            else: inside contraction
                               xic = c + ρ*(v[N]-c); clamp; fic = f(xic)
                               replace v[N] with xic if fic < f[N], else shrink
      shrink: v[i] = v[0] + σ*(v[i]-v[0]), clamp and re-evaluate for i=1..N
   d. Sort.
   e. if |f[N] - f[0]| < tolerance → Converged(iteration)
   f. if iteration == MaxIterations → NotConverged(best v[0], iteration)
```

`Optimizer` takes `IModelEvaluator` directly (not `SweepRunner`) and applies
`ConstNodePatcher.Patch` sequentially for all parameters before each evaluation.

## Acceptance criteria

### AC-1 — OptimizeSpec validates

`OptimizeSpec` validates: non-null/whitespace ModelYaml/MetricSeriesId; non-null/non-empty
ParamIds; non-null SearchRanges with an entry for every ParamId (Lo < Hi for each);
Tolerance > 0; MaxIterations ≥ 1
### AC-2 — Optimizer.OptimizeAsync converges on a 1D bowl function to within

`Optimizer.OptimizeAsync` converges on a 1D bowl function to within tolerance
### AC-3 — Optimizer.OptimizeAsync converges on a 2D bowl function to within

`Optimizer.OptimizeAsync` converges on a 2D bowl function to within tolerance
### AC-4 — Optimizer.OptimizeAsync supports Maximize objective (maximizes a

`Optimizer.OptimizeAsync` supports Maximize objective (maximizes a linear metric)
### AC-5 — Optimizer returns Converged=false when MaxIterations exhausted before

`Optimizer` returns `Converged=false` when MaxIterations exhausted before convergence
### AC-6 — Optimizer respects CancellationToken

`Optimizer` respects `CancellationToken`
### AC-7 — POST /v1/optimize returns 400 for missing/invalid required fields

`POST /v1/optimize` returns 400 for missing/invalid required fields
### AC-8 — POST /v1/optimize returns 503 when engine not enabled

`POST /v1/optimize` returns 503 when engine not enabled
### AC-9 — Unit tests pass: 29 tests (OptimizeSpec ×17, Optimizer ×12)

### AC-10 — API tests pass: 10 tests (9×400, 1×503)

### AC-11 — dotnet test FlowTime.sln all green (192 TimeMachine, 260 API)

`dotnet test FlowTime.sln` all green (192 TimeMachine, 260 API)
