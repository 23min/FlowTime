# Milestone: Phase 3a — Cycle Time & Flow Efficiency

**ID:** m-ec-p3a
**Epic:** Engine Correctness & Analytical Primitives
**Status:** complete

## Goal

Move cycle time computation from the API layer to engine Core, add flow efficiency as a derived metric, and make both available per-class. This answers: "What is the cycle time at this stage?" and "How much of it is queue time vs processing time?"

## Context

FlowTime already computes `flowLatencyMs` in `StateQueryService.cs` (API layer) by combining queue latency and service time. But this computation lives outside the engine core, making it unavailable to analyzers, the future DSL, and other consumers. The building blocks exist — `queueDepth`, `served`, `processingTimeMsSum`, `servedCount` — they just need to be composed in Core.

For synthetic models (no telemetry), `processingTimeMsSum` is unavailable. Cycle time degrades gracefully to queue time only.

## Acceptance Criteria

1. **AC-1: Cycle time decomposition in Core.** A new `CycleTimeComputer` in `FlowTime.Core/Metrics/` computes per-bin:
   - `queueTimeMs` = `(queueDepth / served) * binMs` (null when served <= 0)
   - `serviceTimeMs` = `processingTimeMsSum / servedCount` (null when unavailable)
   - `cycleTimeMs` = `queueTimeMs + serviceTimeMs` (or just `queueTimeMs` when service time unavailable)
   - Follows the three-tier NaN policy (Tier 2: null for unavailable metrics).

2. **AC-2: Per-class cycle time.** Cycle time decomposition works with per-class `byClass` data. Each class gets its own `queueTimeMs`, `serviceTimeMs`, `cycleTimeMs` when class-level series are available.

3. **AC-3: Flow efficiency metric.** `flowEfficiency = serviceTimeMs / cycleTimeMs` per bin per node. Ranges 0 (all queue time) to 1 (no queue time). Null when `cycleTimeMs` is zero, undefined, or `serviceTimeMs` unavailable. Added to `CycleTimeComputer`.

4. **AC-4: Tests and gate.** Unit tests for `CycleTimeComputer` covering: normal case, zero served (null), missing processingTimeMsSum (graceful), per-class, flow efficiency. Build green; no new test failures introduced. Pre-existing failures unrelated to this milestone (e.g., schema meta-resolution) are excluded from the gate.

5. **AC-5: Steady-state validation warning.** When computing cycle time over a window, check whether arrival and departure rates are stable enough for Little's Law to be meaningful:
   - Compare average arrival rate in the first half of the window to the second half.
   - If the rates diverge beyond a configurable tolerance (default: 25%), emit a warning annotation on the node: `"littles-law-non-stationary"`.
   - This is a diagnostic signal, not a gate — cycle time is still computed, but flagged as potentially unreliable.
   - Rationale: Little's Law (which underpins `queueTimeMs = Q/λ × binMs`) assumes steady state. Without this check, point estimates silently mislead during transients, ramp-ups, or drain-downs.

## Technical Notes

- `CycleTimeComputer` should be a static class like `LatencyComputer` and `UtilizationComputer`.
- `binMs` = `binSize * timeUnitToMs(binUnit)` — needs the grid's bin duration in milliseconds.
- `queueTimeMs` is equivalent to `latencyMinutes * 60000` but computed directly in ms for consistency with `serviceTimeMs`.
- The existing `StateQueryService` flow latency logic (lines 2633-2789) should delegate to the new Core computer.

## Out of Scope

- Moving the full `StateQueryService` flow latency propagation algorithm to Core (that's graph-level, not per-node).
- Cycle time distributions (FlowTime is bin-based, not event-based).
- Post-review analytical projection hardening discovered during verification (logicalType parity, honest metadata, finite-value safety, contract/DTO symmetry, and AC-5 applicability) — tracked in `m-ec-p3a1`.

## Dependencies

- Phase 1 complete ✅
