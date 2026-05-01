---
id: M-016
title: Analytical Warning Facts and Primitive Cleanup
status: done
parent: E-16
---

## Goal

Move analytical warning facts into the consolidated Core analytical result surface and finish the primitive ownership cleanup so analytical policy has one owner per concept.

## Context

E16-04 correctly moved descriptor-backed analytical evaluation, emitted-series truth, effective capacity, utilization, and flow latency into Core. The remaining impurity is narrower: warning production and a few helper seams still leave the adapter and public helper APIs with partial analytical ownership.

Current warning production is still split across adapter-local policy and Core predicates:
- `BuildBacklogWarnings()` in `StateQueryService` assembles analytical warnings in the API.
- `FindQueueGrowthStreak()`, `FindOverloadStreak()`, and `FindAgeRiskStreak()` still live in the adapter and compute analytical policy over evaluated series.
- `BuildStationarityWarnings()` still constructs warning DTOs in the adapter after calling `RuntimeAnalyticalEvaluator.CheckStationarity()`.
- Descriptor-backed warning eligibility is already in Core, but warning production itself is not.

Current primitive ownership is also still split:
- `RuntimeAnalyticalEvaluator` is already the public analytical owner for descriptor-backed evaluation and emitted truth.
- `LatencyComputer` still duplicates `CycleTimeComputer.CalculateQueueTime()` in different units and is called both from the evaluator and directly from the API.
- `CycleTimeComputer.CheckNonStationary()` still acts as the public stationarity policy implementation even though stationarity is now an evaluator/analyzer concern.
- E16-04 already established one consolidated internal analytical result family. E16-05 must extend that surface with analytical warning facts rather than introduce a second parallel warning pipeline.

## Acceptance Criteria

1. Stationarity, backlog growth, overload, and age-risk analysis consume compiled descriptors plus evaluated analytical facts from Core; no current-state API path reconstructs analytical warning applicability from raw semantics or payload shape.
2. Core extends the consolidated internal analytical result surface with structured analytical warning facts for the relevant window/current-state paths. The API projects those facts into contract DTOs without building analytical warning policy locally.
3. Primitive ownership is singular:
   - `RuntimeAnalyticalEvaluator`, or a tightly-scoped Core analyzer directly beneath it, is the only public analytical owner for latency and stationarity warning semantics.
   - `LatencyComputer` is deleted or reduced to private/internal helper math beneath the evaluator/analyzer surface; `StateQueryService` does not call it directly.
   - `CycleTimeComputer.CheckNonStationary()` is deleted or reduced to private/internal helper math; public stationarity policy is no longer exposed from `CycleTimeComputer`.
4. `StateQueryService` no longer contains analytical warning producers such as `BuildBacklogWarnings()`, `BuildStationarityWarnings()`, `FindQueueGrowthStreak()`, `FindOverloadStreak()`, or `FindAgeRiskStreak()` for runtime analytical behavior.
5. Duplicate analytical policy paths are removed. Each analytical concept relevant to this slice (`queueTimeMs`, `latencyMinutes`, stationarity, backlog growth, overload, age risk) has exactly one public owner in Core, and the API acts only as projector.
6. `dotnet build` and `dotnet test --nologo` are green, with regenerated approved outputs where warning facts changed.

## Guards / DO NOT

- **DO NOT** fold non-analytical warnings (e.g., configuration warnings, model validation warnings) into this milestone. Scope is analytical warnings only.
- **DO NOT** move warning formatting/presentation into Core. Core returns analytical facts; the adapter projects those facts into DTOs.
- **DO NOT** introduce a second parallel analytical warning pipeline or result surface beside the consolidated internal analytical result family established in M-015.
- **DO NOT** keep `FindQueueGrowthStreak`, `FindOverloadStreak`, `FindAgeRiskStreak`, or `BuildStationarityWarnings()` in the adapter. These are analytical policy over evaluated facts — they belong in Core.
- **DO NOT** let `LatencyComputer.Calculate()` survive as a public semantic owner parallel to `RuntimeAnalyticalEvaluator`.
- **DO NOT** keep `CycleTimeComputer.CheckNonStationary()` as a public analytical seam once stationarity ownership is moved behind the evaluator/analyzer layer.
- **DO NOT** add new analyzer policies in the adapter. If a new warning type is needed, it goes in Core.

## Deletion Targets

| Target | Location | Why |
|--------|----------|-----|
| `BuildBacklogWarnings()` | StateQueryService.cs | Adapter-local analytical warning assembly moves to Core |
| `BuildStationarityWarnings()` | StateQueryService.cs | Adapter-local stationarity warning construction moves to Core |
| `FindQueueGrowthStreak()` | StateQueryService.cs | Backlog growth analyzer policy moves to Core |
| `FindOverloadStreak()` | StateQueryService.cs | Overload analyzer policy moves to Core |
| `FindAgeRiskStreak()` | StateQueryService.cs | Age-risk analyzer policy moves to Core |
| Direct `LatencyComputer.Calculate()` API call | StateQueryService.cs | API stops calling analytical primitives directly for warning production |
| Public `LatencyComputer` semantic ownership | LatencyComputer.cs | Little's Law latency has one public owner in Core |
| Public stationarity policy on `CycleTimeComputer` | CycleTimeComputer.cs | Stationarity becomes evaluator/analyzer-owned rather than a public helper seam |

## Test Strategy

- **Core warning-fact tests:** Evaluator/analyzer tests prove structured warning facts for stationarity, backlog growth, overload, and age risk from descriptor-backed inputs.
- **Projector tests:** API tests prove `state_window` warnings are projected from Core warning facts rather than assembled from adapter-local streak helpers.
- **Ownership audit:** Grep-based check that no analytical warning detection logic or direct analytical primitive calls remain in `StateQueryService`.
- **Helper-boundary tests:** If helper math survives internally, public behavior tests move to the evaluator/analyzer surface; helper tests do not assert those helpers are the public analytical owner.
- **Golden/API parity:** Existing backlog/stationarity endpoint coverage and approved outputs are regenerated from Core-produced warning facts as needed.

## Technical Notes

- Extend the consolidated analytical result family from M-015 rather than inventing a second analytical warning surface.
- Keep warnings as analyzers over evaluated facts, not as ad hoc projection helpers.
- If helper math survives, make it private/internal beneath evaluator/analyzer ownership rather than a public analytical seam.
- Avoid folding unrelated non-analytical warnings into this milestone.
- Use explicit analytical warning facts nested under the Core analytical result surface; the adapter maps those facts to warning DTOs.

## Out of Scope

- Public contract redesign (M-017)
- UI/client heuristic deletion (M-017)

## Dependencies

- [M-015](M-015.md)
