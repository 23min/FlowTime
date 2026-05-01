---
id: M-006
title: Parameter Sweep
status: done
parent: E-18
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

## Acceptance Criteria

- [x] `IModelEvaluator` interface exists in `FlowTime.TimeMachine.Sweep`
- [x] `SweepSpec` validates: non-null/whitespace ModelYaml, non-null/whitespace ParamId, non-null/non-empty Values
- [x] `ConstNodePatcher.Patch` correctly replaces const node values; returns original YAML for unknown/non-const nodes
- [x] `SweepRunner.RunAsync` returns one `SweepPoint` per input value, with correct ParamValue and Series
- [x] `SweepRunner` respects `CaptureSeriesIds` filter (null = all series)
- [x] `SweepRunner` respects `CancellationToken` between evaluation points
- [x] `RustModelEvaluator` wraps `RustEngineRunner` and maps series list to dictionary
- [x] `POST /v1/sweep` returns 400 for missing yaml / paramId / empty values
- [x] `POST /v1/sweep` returns 503 when Rust engine not enabled
- [x] Unit tests pass: 28 sweep unit tests (SweepSpec ×9, ConstNodePatcher ×7, SweepRunner ×12)
- [x] API validation tests pass: 7 tests (6×400, 1×503)
- [x] `dotnet test FlowTime.sln` all green (105 TimeMachine, 235 API — pre-existing integration failures unrelated)
