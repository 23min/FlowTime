---
id: M-007
title: Sensitivity Analysis
status: done
parent: E-18
acs:
  - id: AC-1
    title: ConstNodeReader.ReadValue(yaml, nodeId) returns the first-bin value
    status: met
  - id: AC-2
    title: SensitivitySpec validates
    status: met
  - id: AC-3
    title: SensitivityRunner.RunAsync returns one SensitivityPoint per found
    status: met
  - id: AC-4
    title: Gradient computed correctly via central difference
    status: met
  - id: AC-5
    title: Zero-base param produces Gradient = 0.0 (no crash)
    status: met
  - id: AC-6
    title: Unknown param ID silently skipped (omitted from result)
    status: met
  - id: AC-7
    title: Missing metric series throws InvalidOperationException
    status: met
  - id: AC-8
    title: SensitivityRunner respects CancellationToken
    status: met
  - id: AC-9
    title: POST /v1/sensitivity returns 400 for missing yaml / paramIds /
    status: met
  - id: AC-10
    title: POST /v1/sensitivity returns 503 when Rust engine not enabled
    status: met
  - id: AC-11
    title: Unit tests pass
    status: met
  - id: AC-12
    title: 'API tests pass: 7 tests (6×400, 1×503)'
    status: met
  - id: AC-13
    title: dotnet test FlowTime.sln all green (137 TimeMachine, 242 API)
    status: met
---

## Goal

Add numerical sensitivity analysis as a Time Machine operation: given a model YAML, a set
of const-node parameters, and a target metric series, compute ∂metric_mean/∂param for each
parameter using a central-difference approximation. Answers "which parameter has the most
impact on this metric?"

Builds on:
- M-006 `SweepRunner` + `ConstNodePatcher` — two-point sweep per parameter reuses the
  sweep infrastructure directly
- `ConstNodePatcher` — YAML DOM manipulation already in place

## Scope

**`FlowTime.TimeMachine.Sweep` namespace** (extending M-006's namespace):
- `ConstNodeReader` — companion to `ConstNodePatcher`; reads the current scalar value of a
  named const node's first bin. Returns `null` if the node is not found or not a const node.
- `SensitivitySpec` — validated input: ModelYaml, ParamIds[], MetricSeriesId, Perturbation (default 5%)
- `SensitivityPoint` — single result: ParamId, BaseValue, Gradient (∂metric_mean/∂param)
- `SensitivityResult` — `SensitivityPoint[]` sorted by `|Gradient|` descending
- `SensitivityRunner` — composes `SweepRunner`; for each param: read base, 2-point sweep,
  central difference

**`POST /v1/sensitivity`** — in `src/FlowTime.API/Endpoints/SensitivityEndpoints.cs`
- Request: `{ yaml, paramIds: [string...], metricSeriesId, perturbation?: double }`
- Response (200): `{ metricSeriesId, points: [{ paramId, baseValue, gradient }] }`
- 400: missing yaml / paramIds (null or empty) / metricSeriesId
- 503: engine not enabled

**In scope:**
- `src/FlowTime.TimeMachine/Sweep/ConstNodeReader.cs`
- `src/FlowTime.TimeMachine/Sweep/SensitivitySpec.cs`
- `src/FlowTime.TimeMachine/Sweep/SensitivityResult.cs`
- `src/FlowTime.TimeMachine/Sweep/SensitivityRunner.cs`
- `src/FlowTime.API/Endpoints/SensitivityEndpoints.cs`
- DI registration in `Program.cs`
- Unit tests: `tests/FlowTime.TimeMachine.Tests/Sweep/`
- API tests: `tests/FlowTime.Api.Tests/SensitivityEndpointsTests.cs`

**Out of scope:**
- Multi-metric sensitivity — single metric per call
- Distribution-based sensitivity (Morris method, Sobol indices) — follow-on
- Forward-difference vs central-difference choice — central difference only
- Optimization / fitting — M-008+

## Design Notes

### Gradient formula (central difference)

For each parameter `p` with base value `b` and perturbation fraction `ε`:

```
hi  = b × (1 + ε)
lo  = b × (1 - ε)
gradient = (mean(metric_series_at_hi) − mean(metric_series_at_lo)) / (hi − lo)
         = (mean_hi − mean_lo) / (2 × b × ε)
```

**Zero-base edge case:** when `b == 0`, `hi == lo == 0` and the gradient is indeterminate.
Gradient is set to `0.0` and a note is included in the point. The parameter is still included
in the result so callers can see it was processed.

**Missing metric series:** if the evaluator returns series that do not include `MetricSeriesId`,
`SensitivityRunner` throws `InvalidOperationException` with a clear message. This is a caller
error (wrong series ID), not a graceful skip.

**Unknown param:** if `ConstNodeReader.ReadValue` returns `null` for a param ID (node not
found or not a const node), that param is skipped (omitted from result). Callers can detect
skipped params by comparing `spec.ParamIds.Length` vs `result.Points.Length`.

### `SensitivityRunner` composes `SweepRunner`

`SensitivityRunner(SweepRunner sweepRunner)` — takes the full `SweepRunner` including its
injected `IModelEvaluator`. Tests pass a `SweepRunner(fakeEvaluator)` — no additional
test doubles needed.

## Acceptance criteria

### AC-1 — ConstNodeReader.ReadValue(yaml, nodeId) returns the first-bin value

`ConstNodeReader.ReadValue(yaml, nodeId)` returns the first-bin value for known const
nodes; returns `null` for unknown nodes, non-const nodes, and missing `nodes` section
### AC-2 — SensitivitySpec validates

`SensitivitySpec` validates: non-null/whitespace ModelYaml, non-null/non-empty ParamIds,
non-null/whitespace MetricSeriesId, Perturbation in (0, 1) exclusive
### AC-3 — SensitivityRunner.RunAsync returns one SensitivityPoint per found

`SensitivityRunner.RunAsync` returns one `SensitivityPoint` per found param, sorted by
`|Gradient|` descending
### AC-4 — Gradient computed correctly via central difference

### AC-5 — Zero-base param produces Gradient = 0.0 (no crash)

### AC-6 — Unknown param ID silently skipped (omitted from result)

### AC-7 — Missing metric series throws InvalidOperationException

Missing metric series throws `InvalidOperationException`
### AC-8 — SensitivityRunner respects CancellationToken

`SensitivityRunner` respects `CancellationToken`
### AC-9 — POST /v1/sensitivity returns 400 for missing yaml / paramIds /

`POST /v1/sensitivity` returns 400 for missing yaml / paramIds / metricSeriesId
### AC-10 — POST /v1/sensitivity returns 503 when Rust engine not enabled

`POST /v1/sensitivity` returns 503 when Rust engine not enabled
### AC-11 — Unit tests pass

Unit tests pass: 32 tests (ConstNodeReader ×8, SensitivitySpec ×12, SensitivityRunner ×12)
### AC-12 — API tests pass: 7 tests (6×400, 1×503)

### AC-13 — dotnet test FlowTime.sln all green (137 TimeMachine, 242 API)

`dotnet test FlowTime.sln` all green (137 TimeMachine, 242 API)
