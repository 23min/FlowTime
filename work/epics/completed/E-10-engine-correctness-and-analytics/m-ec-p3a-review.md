# Review: m-ec-p3a — Cycle Time & Flow Efficiency

**Initial Review:** 2026-04-02  
**Re-verified:** 2026-04-03  
**Latest Verification Pass:** 2026-04-03  
**Scope:** Unstaged implementation review for the current E-10 / m-ec-p3a changes  
**Verdict:** Request changes

## Summary

Latest verification closes most of the earlier blockers. Snapshot `byClass` now includes derived values, the FlowTime.UI DTOs were updated, and the new window series now have `seriesMetadata`. The remaining issues are smaller but still real: the new AC-5 warning path can emit false positives on nodes that do not use Little's Law queue-time estimation, the snapshot path still uses a different queue-like predicate than the window path for `queueTimeMs`, and AC-4 is still not literally met while the same 15 pre-existing schema failures remain.

Planning note: these remaining projection/contract hardening findings are now being split into follow-on milestone `m-ec-p3a1` so p3a can remain the primitive-introduction milestone and Phase 3 can continue with a cleaner boundary.


## Closed Findings

### 1. Closed — Core helper is now wired into the runtime path

- The runtime now computes node-level `queueTimeMs`, `cycleTimeMs`, and `flowEfficiency` via `CycleTimeComputer` in [StateQueryService.cs](../../src/FlowTime.API/Services/StateQueryService.cs#L652), [StateQueryService.cs](../../src/FlowTime.API/Services/StateQueryService.cs#L657), and [StateQueryService.cs](../../src/FlowTime.API/Services/StateQueryService.cs#L658).
- Snapshot `Derived` now surfaces the new fields in [StateQueryService.cs](../../src/FlowTime.API/Services/StateQueryService.cs#L713), [StateQueryService.cs](../../src/FlowTime.API/Services/StateQueryService.cs#L714), and [StateQueryService.cs](../../src/FlowTime.API/Services/StateQueryService.cs#L716).
- Window computation now delegates the base cycle-time composition to `CycleTimeComputer` in [StateQueryService.cs](../../src/FlowTime.API/Services/StateQueryService.cs#L2782), [StateQueryService.cs](../../src/FlowTime.API/Services/StateQueryService.cs#L2789), and [StateQueryService.cs](../../src/FlowTime.API/Services/StateQueryService.cs#L2799).
- Result: the earlier AC-1 blocker is closed.

### 2. Closed — Pure service-node regression risk is fixed

- `CalculateCycleTime` now returns the available component when the other side is null in [CycleTimeComputer.cs](../../src/FlowTime.Core/Metrics/CycleTimeComputer.cs#L25).
- The behavior is covered directly in [MetricsTests.cs](../../tests/FlowTime.Core.Tests/MetricsTests.cs#L142), [NaNPolicyTests.cs](../../tests/FlowTime.Core.Tests/Safety/NaNPolicyTests.cs#L177), and the scenario tests for pure service nodes in [CycleTimeScenarioTests.cs](../../tests/FlowTime.Core.Tests/Metrics/CycleTimeScenarioTests.cs#L48) and [CycleTimeScenarioTests.cs](../../tests/FlowTime.Core.Tests/Metrics/CycleTimeScenarioTests.cs#L62).
- Result: the earlier service-only-node regression concern is closed.

### 3. Closed — Snapshot `byClass` now includes derived metrics

- Snapshot `byClass` now routes through `ConvertClassMetrics(..., binMs)` in [StateQueryService.cs](../../src/FlowTime.API/Services/StateQueryService.cs#L704) and [StateQueryService.cs](../../src/FlowTime.API/Services/StateQueryService.cs#L990).
- `ClassMetrics` now carries `QueueTimeMs`, `ServiceTimeMs`, `CycleTimeMs`, and `FlowEfficiency` in [StateContracts.cs](../../src/FlowTime.Contracts/TimeTravel/StateContracts.cs#L199).
- Golden snapshots now include those fields for snapshot responses in [state-approved.json](../../tests/FlowTime.Api.Tests/Golden/state-approved.json) and [state-dependency-approved.json](../../tests/FlowTime.Api.Tests/Golden/state-dependency-approved.json).
- Result: the earlier snapshot `byClass` gap is closed.

### 4. Closed — FlowTime.UI DTOs were updated

- `TimeTravelNodeDerivedMetricsDto` now exposes `queueTimeMs`, `cycleTimeMs`, and `flowEfficiency` in [TimeTravelApiModels.cs](../../src/FlowTime.UI/Services/TimeTravelApiModels.cs#L388).
- Result: the earlier UI DTO gap is closed.

### 5. Closed — New cycle-time series now have metadata

- `BuildDerivedSeriesMetadata` now emits entries for `queueTimeMs`, `cycleTimeMs`, and `flowEfficiency` in [StateQueryService.cs](../../src/FlowTime.API/Services/StateQueryService.cs#L1853).
- Golden window snapshots now include those metadata entries in [state-window-approved.json](../../tests/FlowTime.Api.Tests/Golden/state-window-approved.json), [state-window-edges-approved.json](../../tests/FlowTime.Api.Tests/Golden/state-window-edges-approved.json), and related fixtures.
- Result: the earlier missing-`seriesMetadata` gap is closed.

## Remaining Findings

### 1. Medium — AC-5 warning path can emit false positives

- The window path now injects `BuildStationarityWarnings(...)` into telemetry generation in [StateQueryService.cs](../../src/FlowTime.API/Services/StateQueryService.cs#L1636).
- `BuildStationarityWarnings` currently only checks whether arrivals exist and the window has at least two bins in [StateQueryService.cs](../../src/FlowTime.API/Services/StateQueryService.cs#L1641).
- That means a node can receive a `littles-law-non-stationary` warning even if it never computes `queueTimeMs` via Little's Law.
- Coverage exists for `CheckNonStationary` itself in [StationarityTests.cs](../../tests/FlowTime.Core.Tests/Metrics/StationarityTests.cs#L1), but there is no API-level test proving the warning is gated to Little's Law-applicable nodes.

### 2. Medium — Snapshot and window paths still use different queue-like predicates

- Snapshot `queueTimeMs` still keys off `IsQueueLikeKind(kind)` in [StateQueryService.cs](../../src/FlowTime.API/Services/StateQueryService.cs#L653).
- Window series generation uses `IsQueueLikeKind(kind) || IsQueueLikeKind(logicalType)` in [StateQueryService.cs](../../src/FlowTime.API/Services/StateQueryService.cs#L1546).
- `DetermineLogicalType` can still reclassify nodes to `serviceWithBuffer` in [StateQueryService.cs](../../src/FlowTime.API/Services/StateQueryService.cs#L5359).
- Existing service-with-buffer API coverage exercises explicit `kind: serviceWithBuffer` fixtures in [StateEndpointTests.cs](../../tests/FlowTime.Api.Tests/StateEndpointTests.cs#L602) and [StateEndpointTests.cs](../../tests/FlowTime.Api.Tests/StateEndpointTests.cs#L2858); it does not prove snapshot correctness for logicalType-resolved cases.

### 3. Low — AC-4 is still not literally met under the current spec

- The milestone spec still says “Full test suite green” in [m-ec-p3a-cycle-time.md](m-ec-p3a-cycle-time.md#L29).
- Current validation is improved but not fully green:
  - `dotnet build` passed.
  - `dotnet test tests/FlowTime.Core.Tests/FlowTime.Core.Tests.csproj --nologo --no-restore` passed with 190/190 tests.
  - `dotnet test tests/FlowTime.Api.Tests/FlowTime.Api.Tests.csproj --nologo --no-restore` passed 193 tests and failed 15, all in [StateResponseSchemaTests.cs](../../tests/FlowTime.Api.Tests/StateResponseSchemaTests.cs#L508) with the same pre-existing custom meta-schema resolution problem.
- Result: even if the failures remain unrelated to this milestone logic, AC-4 is still overstated if treated as fully complete.

## Validation Summary

1. `dotnet build`: passed.
2. `dotnet test tests/FlowTime.Core.Tests/FlowTime.Core.Tests.csproj --nologo --no-restore`: 190 passed, 0 failed.
3. `dotnet test tests/FlowTime.Api.Tests/FlowTime.Api.Tests.csproj --nologo --no-restore`: 193 passed, 15 failed, 0 skipped.

## Recommended Next Actions

1. Gate the AC-5 warning on nodes that actually compute `queueTimeMs` via Little's Law and add API-level coverage for that behavior.
2. Align the snapshot queue-time predicate with the window path (`kind || logicalType`) and add a snapshot test for a logicalType-resolved `serviceWithBuffer` node.
3. Reconcile AC-4 with reality: either reopen it until the full suite is green, or explicitly relax the milestone gate to exclude the known pre-existing schema failures.