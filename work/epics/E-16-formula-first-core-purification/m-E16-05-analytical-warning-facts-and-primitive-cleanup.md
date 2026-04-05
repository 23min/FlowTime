# Milestone: Analytical Warning Facts and Primitive Cleanup

**ID:** m-E16-05-analytical-warning-facts-and-primitive-cleanup
**Epic:** Formula-First Core Purification
**Status:** draft

## Goal

Move analytical warning facts into Core analyzers and finish the primitive ownership cleanup so analytical policy has one owner per concept.

## Context

Even after analytical values and emitted truth move into Core, warning eligibility and primitive ownership can still drift if stationarity, backlog logic, and latency helpers remain split across adapter code and partial Core helpers.

Current warning computation in the adapter:
- `FindQueueGrowthStreak()` — detects queue growth for 3+ bins
- `FindQueueOverloadStreak()` — detects queue > capacity for 3+ bins
- `FindQueueAgeRiskStreak()` — detects age risk for 3+ bins
- Stationarity checking via `AnalyticalCapabilities.CheckStationarity()` (Core, but consumed and formatted by adapter)

Current primitive ownership:
- `CycleTimeComputer` — Core, pure math (correct location)
- `LatencyComputer` — Core, single `Calculate()` method that duplicates `CycleTimeComputer.CalculateQueueTime()` in different units
- Stationarity logic in `CycleTimeComputer.CheckNonStationary()` — Core math, but it is analyzer policy (half-window divergence detection), not pure math

## Acceptance Criteria

1. Stationarity, backlog, queue growth, queue overload, and age risk warnings consume compiled descriptors plus evaluated analytical facts rather than raw semantics or adapter-local gating.
2. Core returns warning facts or analyzer results, and projection code only formats them into DTOs.
3. Ownership of `CycleTimeComputer`, `LatencyComputer`, and stationarity logic is explicit:
   - `CycleTimeComputer` stays as pure math in Core.
   - `LatencyComputer` is either folded into the evaluator or clearly documented as a unit-conversion helper if it remains distinct. Its relationship to `CycleTimeComputer.CalculateQueueTime()` is resolved (they compute the same thing in different units).
   - `CheckNonStationary()` is documented as analyzer policy, not pure math. If it stays on `CycleTimeComputer`, it is clearly labeled.
4. Current state paths no longer call direct analytical primitives outside Core evaluator/analyzer surfaces for runtime analytical behavior.
5. Duplicate analytical policy paths are removed. Each analytical concept (queue time, service time, cycle time, stationarity, backlog detection, overload detection) has exactly one owning implementation.
6. `dotnet build` and `dotnet test --nologo` are green, with regenerated approved outputs where warning facts changed.

## Guards / DO NOT

- **DO NOT** fold non-analytical warnings (e.g., configuration warnings, model validation warnings) into this milestone. Scope is analytical warnings only.
- **DO NOT** move warning formatting/presentation into Core. Core returns facts; the adapter formats them into DTOs with human-readable messages.
- **DO NOT** keep `FindQueueGrowthStreak`, `FindQueueOverloadStreak`, `FindQueueAgeRiskStreak` in the adapter. These are analytical policy over evaluated facts — they belong in Core analyzers.
- **DO NOT** let `LatencyComputer.Calculate()` survive as a near-duplicate of `CycleTimeComputer.CalculateQueueTime()` without resolving the relationship.
- **DO NOT** add new analyzer policies in the adapter. If a new warning type is needed, it goes in Core.

## Deletion Targets

| Target | Location | Why |
|--------|----------|-----|
| `FindQueueGrowthStreak()` | StateQueryService.cs | Backlog analyzer policy moves to Core |
| `FindQueueOverloadStreak()` | StateQueryService.cs | Overload analyzer policy moves to Core |
| `FindQueueAgeRiskStreak()` | StateQueryService.cs | Age risk analyzer policy moves to Core |
| Warning-building helpers in adapter | StateQueryService.cs (BuildBacklogWarnings etc.) | Warning fact production moves to Core; adapter only formats |
| Adapter-local analytical primitive calls | StateQueryService.cs | Replaced by Core evaluator/analyzer surface |

## Test Strategy

- **Analyzer unit tests:** Each warning type (stationarity, backlog growth, overload, age risk) has isolated Core tests with known input series and expected warning facts.
- **Ownership audit:** Grep-based check that no analytical warning detection logic remains in the adapter.
- **Primitive ownership documentation:** If a primitive survives (e.g., `CycleTimeComputer`), a test or doc explicitly states what it owns and what it does not.
- **LatencyComputer resolution test:** If `LatencyComputer` survives, test proves it is not a duplicate of `CycleTimeComputer.CalculateQueueTime()` — or it is deleted.

## Technical Notes

- Keep warnings as analyzers over evaluated facts, not as ad hoc projection helpers.
- If a primitive survives, document exactly what it owns and what it does not.
- Avoid folding unrelated non-analytical warnings into this milestone.
- Consider a `WarningFact` or `AnalyzerResult` type that Core returns and the adapter maps to warning DTOs.

## Out of Scope

- Public contract redesign (m-E16-06)
- UI/client heuristic deletion (m-E16-06)

## Dependencies

- [m-E16-04-core-analytical-evaluation](m-E16-04-core-analytical-evaluation.md)
