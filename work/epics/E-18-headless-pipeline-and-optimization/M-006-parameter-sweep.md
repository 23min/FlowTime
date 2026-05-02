---
id: M-006
title: Parameter Sweep
status: done
parent: E-18
acs:
  - id: AC-1
    title: IModelEvaluator interface exists in FlowTime.TimeMachine.Sweep
    status: met
  - id: AC-2
    title: SweepSpec validates
    status: met
  - id: AC-3
    title: ConstNodePatcher.Patch correctly replaces const node values; returns
    status: met
  - id: AC-4
    title: SweepRunner.RunAsync returns one SweepPoint per input value, with
    status: met
  - id: AC-5
    title: SweepRunner respects CaptureSeriesIds filter (null = all series)
    status: met
  - id: AC-6
    title: SweepRunner respects CancellationToken between evaluation points
    status: met
  - id: AC-7
    title: RustModelEvaluator wraps RustEngineRunner and maps series list to
    status: met
  - id: AC-8
    title: POST /v1/sweep returns 400 for missing yaml / paramId / empty values
    status: met
  - id: AC-9
    title: POST /v1/sweep returns 503 when Rust engine not enabled
    status: met
  - id: AC-10
    title: Unit tests pass
    status: met
  - id: AC-11
    title: 'API validation tests pass: 7 tests (6×400, 1×503)'
    status: met
  - id: AC-12
    title: dotnet test FlowTime.sln all green (105 TimeMachine, 235 API
    status: met
---

## Goal

Implement parameter sweep as a first-class Time Machine operation: given a model YAML, a
const-node ID, and an array of values, evaluate the model once per value and return a
structured table of (param_value → series outputs).

Builds on:
- M-001 `evaluate_with_params` in the Rust engine (compile-once foundation)
- M-004 `FlowTime.TimeMachine` project (host for the sweep domain model)
- M-005 `ITelemetrySource` (pattern for injectable evaluation contracts)

## Scope

**`FlowTime.TimeMachine.Sweep` namespace** — in `src/FlowTime.TimeMachine/Sweep/`:
- `IModelEvaluator` — injectable evaluation contract; decouples SweepRunner from the Rust binary in tests
- `SweepSpec` — validated input: ModelYaml, ParamId, Values[], optional CaptureSeriesIds
- `SweepPoint` — single evaluation result: ParamValue + Series dictionary
- `SweepResult` — full sweep result: ParamId + SweepPoint[]
- `ConstNodePatcher` — internal YAML DOM manipulation; patches a named const node's values array
- `SweepRunner` — orchestrates N evaluations via injected `IModelEvaluator`
- `RustModelEvaluator : IModelEvaluator` — wraps `RustEngineRunner`, maps series list to dictionary

**`POST /v1/sweep`** — in `src/FlowTime.API/Endpoints/SweepEndpoints.cs`:
- Request: `{ yaml, paramId, values: [double...], captureSeriesIds?: [string...] }`
- Response (200): `{ paramId, points: [{ paramValue, series: { seriesId: double[] } }] }`
- 400: missing yaml / paramId / values
- 503: engine not enabled (RustEngine:Enabled=false)

**In scope:**
- `src/FlowTime.TimeMachine/Sweep/IModelEvaluator.cs`
- `src/FlowTime.TimeMachine/Sweep/SweepSpec.cs`
- `src/FlowTime.TimeMachine/Sweep/SweepResult.cs`
- `src/FlowTime.TimeMachine/Sweep/ConstNodePatcher.cs`
- `src/FlowTime.TimeMachine/Sweep/SweepRunner.cs`
- `src/FlowTime.TimeMachine/Sweep/RustModelEvaluator.cs`
- `src/FlowTime.API/Endpoints/SweepEndpoints.cs`
- DI registration in `Program.cs`
- Unit tests: `tests/FlowTime.TimeMachine.Tests/Sweep/`
- API tests: `tests/FlowTime.Api.Tests/SweepEndpointsTests.cs`

**Out of scope:**
- Sensitivity analysis (numerical gradient) — follow-on
- Multi-parameter sweeps (grid sweeps) — follow-on
- Session-based compile-once optimization — follow-on (each sweep point uses subprocess eval)
- Optimization / fitting — M-007+
- Sweep result persistence / artifact writing — follow-on

## Design Notes

### Implementation approach

Each sweep point calls `RustEngineRunner.EvaluateAsync(patchedYaml)` independently (one
subprocess per point). The YAML is patched in-memory before each call via `ConstNodePatcher`,
which uses YamlDotNet's representation model to substitute the const node's values array.

This deliberately trades compile-once efficiency for implementation simplicity: the Rust
session protocol requires a MessagePack NuGet dependency not yet in the tree, while the
subprocess approach reuses existing infrastructure with no new dependencies.

The `IModelEvaluator` abstraction isolates this choice from `SweepRunner`, so a future
session-based evaluator can be dropped in without changing the sweep domain model or tests.

### ConstNodePatcher behaviour

- Finds the first `nodes` entry where `id == nodeId` AND `kind == "const"`
- Replaces its `values` sequence with `[value, value, ..., value]` (same bin count)
- Returns the original YAML unchanged if the node is not found or is not a const node
- Uses `InvariantCulture` formatting for decimal precision

## Acceptance criteria

### AC-1 — IModelEvaluator interface exists in FlowTime.TimeMachine.Sweep

`IModelEvaluator` interface exists in `FlowTime.TimeMachine.Sweep`
### AC-2 — SweepSpec validates

`SweepSpec` validates: non-null/whitespace ModelYaml, non-null/whitespace ParamId, non-null/non-empty Values
### AC-3 — ConstNodePatcher.Patch correctly replaces const node values; returns

`ConstNodePatcher.Patch` correctly replaces const node values; returns original YAML for unknown/non-const nodes
### AC-4 — SweepRunner.RunAsync returns one SweepPoint per input value, with

`SweepRunner.RunAsync` returns one `SweepPoint` per input value, with correct ParamValue and Series
### AC-5 — SweepRunner respects CaptureSeriesIds filter (null = all series)

`SweepRunner` respects `CaptureSeriesIds` filter (null = all series)
### AC-6 — SweepRunner respects CancellationToken between evaluation points

`SweepRunner` respects `CancellationToken` between evaluation points
### AC-7 — RustModelEvaluator wraps RustEngineRunner and maps series list to

`RustModelEvaluator` wraps `RustEngineRunner` and maps series list to dictionary
### AC-8 — POST /v1/sweep returns 400 for missing yaml / paramId / empty values

`POST /v1/sweep` returns 400 for missing yaml / paramId / empty values
### AC-9 — POST /v1/sweep returns 503 when Rust engine not enabled

`POST /v1/sweep` returns 503 when Rust engine not enabled
### AC-10 — Unit tests pass

Unit tests pass: 28 sweep unit tests (SweepSpec ×9, ConstNodePatcher ×7, SweepRunner ×12)
### AC-11 — API validation tests pass: 7 tests (6×400, 1×503)

### AC-12 — dotnet test FlowTime.sln all green (105 TimeMachine, 235 API

`dotnet test FlowTime.sln` all green (105 TimeMachine, 235 API — pre-existing integration failures unrelated)
