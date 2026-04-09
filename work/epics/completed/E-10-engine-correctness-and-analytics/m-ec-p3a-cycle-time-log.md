# Tracking: Phase 3a — Cycle Time & Flow Efficiency

**Milestone:** m-ec-p3a
**Epic:** E-10 Engine Correctness & Analytical Primitives
**Branch:** `milestone/m-ec-p3a`
**Started:** 2026-04-02
**Completed:** 2026-04-03

**Follow-on hardening:** Remaining post-review analytical projection issues carried into `m-ec-p3a1` (Analytical Projection Hardening).

## Acceptance Criteria

- [x] AC-1: Cycle time decomposition in Core (CycleTimeComputer with queueTimeMs, serviceTimeMs, cycleTimeMs)
  - Core helper: `FlowTime.Core/Metrics/CycleTimeComputer.cs` (5 static methods)
  - Wired into `StateQueryService.ComputeFlowLatency` — replaces inline queue/service composition
  - Snapshot path: `NodeDerivedMetrics` includes `QueueTimeMs`, `CycleTimeMs`, `FlowEfficiency`
  - Snapshot `isSnapshotQueueLike` predicate aligned with window path (`kind || logicalType`)
- [x] AC-2: Per-class cycle time (byClass data support)
  - Snapshot: `ClassMetrics` contract includes derived fields, `ConvertClassMetrics` computes them
  - Window: `BuildClassSeries` emits per-class `queueTimeMs`, `serviceTimeMs`, `cycleTimeMs`, `flowEfficiency` series
- [x] AC-3: Flow efficiency metric (serviceTimeMs / cycleTimeMs)
  - Surfaced in snapshot `Derived`, snapshot `byClass`, and window series
  - Null when service time unavailable or cycle time zero
- [x] AC-4: Tests and gate (all edge cases, build green)
  - 190 Core tests passing, 194 API tests passing
  - 15 pre-existing schema failures unchanged (excluded per spec update)
  - `dotnet build` green, no new test failures
- [x] AC-5: Steady-state validation warning for Little's Law
  - `CycleTimeComputer.CheckNonStationary` compares first/second half arrival rate divergence
  - Gated to queue-like nodes only (`isQueueLike`) — service nodes never receive the warning
  - API-level test proves gating: `GetStateWindow_StationarityWarning_OnlyOnQueueLikeNodes`
  - Emitted as `"littles-law-non-stationary"` via existing `NodeTelemetryWarning` pipeline

## Progress Log

### 2026-04-02
- Milestone spec approved, branch created, tracking doc initialized
- Created `CycleTimeComputer` in `FlowTime.Core/Metrics/` — initial implementation
- 28 Core-level tests, all passing
- **Review feedback (round 1):** 4 findings — all valid, ACs reset

### 2026-04-03
- Fixed `CalculateCycleTime` to be symmetric (pure-service regression)
- Designed 6 node-type test models, 11 scenario tests (TDD red→green)
- Wired `CycleTimeComputer` into `StateQueryService.ComputeFlowLatency`
- Added `QueueTimeMs`, `CycleTimeMs`, `FlowEfficiency` to `NodeDerivedMetrics` and `ClassMetrics` contracts
- Surfaced in snapshot path, window series, and byClass (both snapshot and window)
- Added `seriesMetadata` entries for new series
- Updated `TimeTravelApiModels.cs` (Blazor UI DTOs)
- **Review feedback (round 2):** 4 remaining findings + AC-5 added to spec — all resolved
- AC-5: Implemented `CheckNonStationary` with 15 tests (TDD), wired into window path
- **Review feedback (round 3):** additional projection/contract hardening identified during close-out
  - Initial fixes landed for stationarity gating, snapshot `queueTimeMs` parity, and AC-4 wording
  - Remaining projection/contract hardening was split into `m-ec-p3a1` rather than continuing to stretch p3a scope
- Final: 190 Core tests, 194 API tests passing, build green
- Follow-on milestone drafted: `m-ec-p3a1` (Analytical Projection Hardening) to own the remaining projection/contract hardening discovered during p3a verification before p3b starts
