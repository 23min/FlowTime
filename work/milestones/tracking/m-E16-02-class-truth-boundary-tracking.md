# Tracking: Class Truth Boundary

**Milestone:** m-E16-02-class-truth-boundary
**Branch:** milestone/m-E16-02-class-truth-boundary
**Started:** 2026-04-04
**Status:** complete

## Acceptance Criteria

- [x] AC1: Internal class surfaces distinguish real by-class data, synthesized fallback, and no-class coverage explicitly via a typed representation.
- [x] AC2: Wildcard fallback is represented as an explicit fallback fact rather than inferred solely from the `*` key or absence of real class data.
- [x] AC3: Analytical evaluation and projection code consume explicit class-truth facts instead of silently relying on wildcard fallback.
- [x] AC4: Tests cover real multi-class fixtures separately from fallback projection cases, and approved outputs are regenerated forward-only where needed.
- [x] AC5: A test or assertion proves fallback-only data cannot be confused with real by-class analytical results in downstream evaluation.
- [x] AC6: dotnet build and dotnet test --nologo are green.

## Implementation Log

| Phase | What | Tests | Status |
|-------|------|-------|--------|
| 1 | Wrap E16-01, preflight E16-02, and identify class-truth hotspots | targeted + full baseline | complete |
| 2 | Introduce typed internal class-truth representation at the Core aggregation boundary | RED/GREEN | complete |
| 3 | Move snapshot/window projection to consume explicit class-truth facts without changing contract shape | RED/GREEN | complete |
| 4 | Validate integration and update notes | targeted + full | complete |

## Test Summary

- **Targeted Core validation:** `ClassMetricsAggregatorTests` green with explicit fallback-vs-real coverage.
- **Targeted API validation:** by-class snapshot/window tests green, including fallback-only wildcard window projection.
- **Golden updates:** regenerated fallback-only window API goldens for `state-window-approved`, `state-window-dependency-approved`, `state-window-constraints-attached-approved`, `state-window-edges-approved`, and `state-window-queue-null-approved`.
- **Full validation:** `dotnet build` green and `dotnet test --nologo` green on 2026-04-04.
- **Wrap audit (2026-04-05):** `dotnet build` green and `dotnet test --nologo` green after quarantining the legacy stopwatch-based `M15PerformanceTests.Test_ExpressionType_Performance` gate from default suite readiness. Targeted expression-type perf checks remain available via `M16BenchmarkRunner.RunM16ExpressionTypeBenchmarks`.

## Notes

- E16-01 was wrapped in repo artifacts, but the branch handoff was corrected late: E16-02 work started before the local branch was rotated. The working tree is now on `milestone/m-E16-02-class-truth-boundary`.
- Initial audit hotspots: `ClassMetricsAggregator`, `StateQueryService.BuildClassSeries()`, `StateQueryService.ConvertClassMetrics()`, and manifest by-class augmentation skip logic.
- Core now models class truth explicitly with `ClassEntry<TPayload>` and `ClassEntryKind` (`Specific | Fallback`).
- Snapshot and window projection consume explicit class-truth facts and suppress fallback serialization only when real classes are present.

## Completion

- **Completed:** 2026-04-04
- **Final result:** typed internal class-truth boundary landed without changing the public `byClass` shape; full build and full suite are green for wrap readiness.
- **Deferred items:** none.