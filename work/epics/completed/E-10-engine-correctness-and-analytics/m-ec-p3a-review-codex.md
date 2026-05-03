# Review: m-ec-p3a — Cycle Time & Flow Efficiency (Codex)

**Date:** 2026-04-03  
**Scope:** Unstaged changes on `milestone/m-ec-p3a` (cycle time + flow efficiency)  
**Verdict:** Request changes

## Summary

Solid implementation progress with core metrics in place, but a few correctness and contract issues remain. The most significant risks are metadata that claims series exist when they do not and possible NaN/Infinity propagation in the new metrics. There is also a DTO gap for per-class derived metrics.

## Findings

1. High — `queueTimeMs` metadata is advertised for service-only nodes.
`BuildDerivedSeriesMetadata` adds `queueTimeMs` whenever `hasCycleTime` is true, but service-only nodes do not emit a `queueTimeMs` series. This makes metadata lie to clients.  
File: `src/FlowTime.API/Services/StateQueryService.cs:1860`

2. High — `queueTimeMs`/`cycleTimeMs`/`flowEfficiency` can propagate NaN/Infinity into API output in telemetry mode.
`CycleTimeComputer.CalculateQueueTime` has no `double.IsFinite` guard, and the new series assignment doesn’t normalize outputs. Telemetry mode allows invalid series, so NaN/Infinity can leak into JSON (System.Text.Json rejects those by default).  
Files: `src/FlowTime.Core/Metrics/CycleTimeComputer.cs:5`, `src/FlowTime.API/Services/StateQueryService.cs:1547`

3. Medium — Per-class cycle-time metrics aren’t exposed in the Blazor DTOs.
`ClassMetrics` now includes `queueTimeMs`, `serviceTimeMs`, `cycleTimeMs`, and `flowEfficiency`, but `TimeTravelClassMetricsDto` does not mirror them, so AC-2 is not consumable by the UI.  
File: `src/FlowTime.UI/Services/TimeTravelApiModels.cs:356`

4. Low — Stationarity tolerance is hard-coded (spec says configurable).
`CheckNonStationary` defaults to `0.25` and is called without configuration. If AC-5 truly requires configurability, this should be wired to configuration.  
Files: `src/FlowTime.Core/Metrics/CycleTimeComputer.cs:51`, `src/FlowTime.API/Services/StateQueryService.cs:1667`

## Test Gaps

1. No direct unit test for `CycleTimeComputer.CheckNonStationary` threshold/boundary behavior (only an API-level warning check).
2. No coverage for NaN/Infinity inputs to the new cycle-time metrics. Add to `tests/FlowTime.Core.Tests/Safety/NaNPolicyTests.cs` to ensure derived outputs are `null` rather than NaN.

## Notes

- I did not run tests locally.
- Assumption: “current milestone” refers to `m-ec-p3a` on `milestone/m-ec-p3a`.
