# Tracking: m-E16-05 — Analytical Warning Facts and Primitive Cleanup

**Started:** 2026-04-06
**Completed:** 2026-04-06
**Branch:** `milestone/m-E16-05-analytical-warning-facts-and-primitive-cleanup`
**Spec:** `work/epics/E-16-formula-first-core-purification/m-E16-05-analytical-warning-facts-and-primitive-cleanup.md`
**Preflight:** `dotnet build` green on 2026-04-06; `dotnet test --nologo --no-build` green on 2026-04-06

## Acceptance Criteria

- [x] AC-1: Stationarity, backlog growth, overload, and age-risk analysis consume compiled descriptors plus evaluated analytical facts from Core; no current-state API path reconstructs analytical warning applicability from raw semantics or payload shape.
- [x] AC-2: Core extends the consolidated internal analytical result surface with structured analytical warning facts for the relevant window/current-state paths, and the API projects those facts into DTOs without building analytical warning policy locally.
- [x] AC-3: Primitive ownership is singular: `RuntimeAnalyticalEvaluator`, or a tightly-scoped Core analyzer directly beneath it, is the only public analytical owner for latency and stationarity warning semantics.
- [x] AC-4: `StateQueryService` no longer contains analytical warning producers or direct analytical primitive calls for runtime analytical behavior.
- [x] AC-5: Duplicate analytical policy paths are removed. Each analytical concept relevant to this slice (`queueTimeMs`, `latencyMinutes`, stationarity, backlog growth, overload, age risk) has exactly one public owner in Core, and the API acts only as projector.
- [x] AC-6: `dotnet build` and `dotnet test --nologo` are green, with regenerated approved outputs where warning facts changed.

## Initial Plan

| Phase | Scope | Test Strategy | Status |
|-------|-------|---------------|--------|
| 1 | Extend the consolidated Core analytical result surface with structured warning facts for window/current-state analytical behavior | New Core red tests on `RuntimeAnalyticalEvaluator` warning facts for stationarity, backlog growth, overload, and age risk | completed |
| 2 | Collapse primitive ownership so latency and stationarity semantics have one public Core owner | Focused Core unit tests around evaluator/analyzer ownership and removal of public helper seams | completed |
| 3 | Replace adapter-local warning assembly with projection from Core warning facts | API/service tests proving `state_window` warnings are mapped from Core facts without adapter-local analytical policy | completed |
| 4 | Delete remaining duplicate warning and primitive paths, then run wrap validation | `dotnet build`, targeted API/Core tests, then `dotnet test --nologo` | completed |

## Notes

- This milestone starts from commit `1e26b5e7b2617decf2c570bbaab24658a5297165`, which carries m-E16-01 through m-E16-04 on the continuation line.
- E16 remains forward-only: warning fact changes regenerate approved outputs rather than layering compatibility helpers.
- E16-05 now explicitly extends the consolidated analytical result family rather than introducing a second warning pipeline; adapter-local warning producers are deletion targets, not tolerated bridges.
- 2026-04-06: First AC1 red tests added in `RuntimeAnalyticalWarningFactsTests` for stationarity and backlog-growth warning facts on `AnalyticalWindowResult`.
- 2026-04-06: RED confirmed with `dotnet test tests/FlowTime.Core.Tests/FlowTime.Core.Tests.csproj --nologo --filter RuntimeAnalyticalWarningFactsTests`; failure is the expected missing `AnalyticalWindowResult.WarningFacts` surface.
- 2026-04-06: GREEN for stationarity/backlog-growth facts after adding `AnalyticalWindowResult.WarningFacts`, `AnalyticalWindowWarningFacts`, and streak facts in `RuntimeAnalyticalEvaluator`.
- 2026-04-06: Added overload and age-risk red tests in `RuntimeAnalyticalWarningFactsTests`, then greened them by extending `BuildWindowWarningFacts(...)` with Core-owned overload and age-risk streak facts.
- 2026-04-06: `StateQueryService` now projects stationarity/backlog/overload/age-risk warnings from `analyticalWindow.WarningFacts`; adapter-local backlog warning producers and streak helpers were removed.
- 2026-04-06: Verification green: `dotnet test tests/FlowTime.Core.Tests/FlowTime.Core.Tests.csproj --nologo --filter "RuntimeAnalytical"` and `dotnet test tests/FlowTime.Api.Tests/FlowTime.Api.Tests.csproj --nologo`.
- 2026-04-06: Latency ownership now sits behind `RuntimeAnalyticalEvaluator`: `LatencyComputer` was deleted, latency minute coverage was moved to evaluator-facing tests, and age-risk warning logic now uses evaluator-local latency computation.
- 2026-04-06: Stationarity ownership now sits behind `RuntimeAnalyticalEvaluator`: `CycleTimeComputer.CheckNonStationary()` was removed, helper-facing tests were redirected to the evaluator surface, and public-boundary reflection tests assert the helper seam stays closed.
- 2026-04-06: Full validation green: `dotnet build`, `dotnet test tests/FlowTime.Core.Tests/FlowTime.Core.Tests.csproj --nologo --filter "RuntimeAnalyticalDescriptorMetadataTests|StationarityTests|MetricsTests|NaNPolicyTests|RuntimeAnalyticalWarningFactsTests|RuntimeAnalyticalEvaluatorTests|RuntimeAnalyticalEvaluatorComputationTests"`, `dotnet test tests/FlowTime.Api.Tests/FlowTime.Api.Tests.csproj --nologo`, and `dotnet test --nologo`.
- 2026-04-06: Reviewer wrap found that `WarningFacts.NonStationary` had fallen back to the default 0.25 tolerance; the fix threaded `AnalyticsOptions.StationarityTolerance` into `RuntimeAnalyticalEvaluator.ComputeWindow(...)`, added Core/API regression tests for non-default tolerance, and reran `dotnet build` plus `dotnet test --nologo` successfully.

## Wrap Summary

- Core now owns structured analytical warning facts, latency semantics, and stationarity semantics behind `RuntimeAnalyticalEvaluator`.
- The API projects warning DTOs from Core facts and no longer carries adapter-local analytical warning builders.
- Wrap validation is green after the configured-tolerance regression fix: `dotnet build` and `dotnet test --nologo` both succeeded on 2026-04-06.