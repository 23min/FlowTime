# Tracking: m-E16-04 — Core Analytical Evaluation

**Started:** 2026-04-06
**Completed:** 2026-04-06
**Branch:** `milestone/m-E16-01-compiled-semantic-references` (continuation branch)
**Spec:** `work/epics/E-16-formula-first-core-purification/m-E16-04-core-analytical-evaluation.md`
**Preflight:** `dotnet build` green; full test validation green at E16-03 wrap
**Build:** `dotnet build` green on 2026-04-06 after final emission-truth fix
**Validation:** `dotnet test --nologo` green on 2026-04-06 after final emission-truth fix (1,504 passed / 9 skipped total across solution test projects)

## Acceptance Criteria

- [x] AC-1: Core exposes an analytical evaluation surface for snapshot, window, and by-class values driven by the compiled analytical descriptor and explicit class-truth boundary.
- [x] AC-2: Core returns one consolidated internal analytical result surface with explicit nested sections for derived values, emitted-series truth, effective-capacity/utilization facts, and graph-level flow-latency outputs sufficient for projection.
- [x] AC-3: `StateQueryService` no longer computes analytical emission truth or per-node/per-class analytical math locally in the current state paths.
- [x] AC-4: `flowLatencyMs` computation moves to Core as a pure function: `(compiledTopology, perNodeCycleTime[], edgeFlowVolume[]) -> perNodeFlowLatency[]`.
- [x] AC-5: Effective capacity computation (`capacity x parallelism`) has one owner in Core. The API's `GetEffectiveCapacity` / `GetParallelismValue` / `ComputeUtilizationSeries` delegation is replaced by Core evaluation.
- [x] AC-6: Tests prove analytical evaluation against both real multi-class fixtures and explicit fallback cases without conflating the two.
- [x] AC-7: `MetricsService` and analogous analytical query surfaces consume the same Core evaluation surface instead of maintaining a second model-evaluation fallback path for analytical behavior; legacy runs that depended on the fallback path are removed or regenerated as part of the forward-only cut.
- [x] AC-8: `dotnet build` and `dotnet test --nologo` are green.

## Initial Plan

| Phase | Scope | Test Strategy | Status |
|-------|-------|---------------|--------|
| 1 | Introduce consolidated Core analytical result types and move effective-capacity/utilization ownership into Core evaluator helpers | New Core evaluator tests for constant/series parallelism and utilization emission truth | completed |
| 2 | Move snapshot/window/by-class analytical emission truth out of `StateQueryService` and into Core evaluation results | API projector tests showing state snapshot/window consume Core results without local analytical computation | completed |
| 3 | Move `flowLatencyMs` traversal and edge-volume inputs into Core | Focused Core graph-latency tests plus API parity tests | completed |
| 4 | Delete `MetricsService.ResolveViaModelAsync()` analytical fallback and finish forward-only cleanup | Metrics API tests over regenerated supported runs only | completed |

## Notes

- E16 remains forward-only: legacy analytical rescue paths are deleted, and any runs/fixtures that depended on them are regenerated or removed instead of tolerated through a fallback mode.
- The consolidated Core result surface is internal and structured; later milestones may project it into narrower public/API contracts without re-fragmenting analytical ownership.
- 2026-04-06: Phase 1 landed the first Core-owned slice. `RuntimeAnalyticalEvaluator` now returns nested capacity/utilization facts, `StateQueryService` snapshot/window paths consume that Core surface, overload warning evaluation delegates to Core effective-capacity ownership, and the API-local capacity/utilization helpers were removed.
- 2026-04-06: Phases 2-4 landed. Core now owns emitted-series truth, by-class analytical projection, and graph-level `flowLatencyMs`; `MetricsService` no longer falls back to model evaluation; test fixtures were regenerated to current run-artifact rules (`run.json` series entries, `series/index.json`, run-level series files, and file-backed model references).
- 2026-04-06: Focused validation is green: `RuntimeAnalyticalEvaluatorTests` passed (19), and the targeted API suite covering `StateEndpointTests`, `MetricsEndpointTests`, and `MetricsServiceTests` passed (76).
- 2026-04-06: Full wrap validation is green after the final flow-efficiency emission-truth fix. `dotnet build` passed, and `dotnet test --nologo` passed across the solution with 1,504 tests passed and 9 skipped.