---
id: G-018
title: '`IModelEvaluator` Series-Key Shape Divergence'
status: open
---

### Why this is a gap

Discovered during M-010 implementation (2026-04-15). The two production
`IModelEvaluator` implementations return different key shapes for the same underlying
series:

| Implementation | Example keys |
|----------------|--------------|
| `RustModelEvaluator` (via `RustEngineRunner`, reads run artifacts) | `arrivals@ARRIVALS@DEFAULT`, `served@SERVED@DEFAULT` |
| `SessionModelEvaluator` (via Rust session protocol) | `arrivals`, `served` |

The numeric values are identical (both invoke the same engine), but the dictionary keys
are not interchangeable. `RustEngineRunner` reads artifacts laid out per E-20 conventions
(`{nodeId}@{COMPONENT}@{CLASS}`). The session protocol returns bare column-map IDs from
`session.rs::extract_all_series`.

### Immediate implications

- `SweepRunner.FilterSeries` does case-insensitive exact-match lookup on keys. Sweeps
  that specify `captureSeriesIds: ["served"]` work correctly against `SessionModelEvaluator`
  but return empty dictionaries against `RustModelEvaluator` — keys won't match because
  the evaluator's keys are `served@SERVED@DEFAULT`.
- Sweeps with `captureSeriesIds: null` (API default) work with both — all series pass through.
- No existing test catches this because sweep unit tests use `FakeEvaluator` with bare
  keys, and no integration test drove `RustModelEvaluator` with `captureSeriesIds`.

### Resolution options

1. Normalize `RustModelEvaluator` to strip `@COMPONENT@CLASS` suffix when the default
   component/class is the only one present — matches session protocol. Preserves per-class
   series when classes are used.
2. Teach `SweepRunner.FilterSeries` to do prefix-match on `captureSeriesIds` (match
   `"served"` to any key starting with `served@`). More tolerant but may over-match when
   classes are involved.
3. Document the divergence as acceptable and require callers to know which evaluator is
   wired. Worst option — leaves a footgun.

### Status

Not scheduled. Tracked pending a decision on normalization. `SessionModelEvaluator` is
the default evaluator in production, so the divergence does not currently break sweeps.
If `RustEngine:UseSession=false` is flipped, sweeps with `captureSeriesIds` will break.

### Reference

- `src/FlowTime.TimeMachine/Sweep/RustModelEvaluator.cs`
- `src/FlowTime.TimeMachine/Sweep/SessionModelEvaluator.cs`
- `src/FlowTime.TimeMachine/Sweep/SweepRunner.cs` — `FilterSeries`
- `engine/cli/src/session.rs` — `extract_all_series`
- `tests/FlowTime.Integration.Tests/SessionModelEvaluatorIntegrationTests.cs` —
  `SessionVsPerEval_NumericValuesAgree`

---
