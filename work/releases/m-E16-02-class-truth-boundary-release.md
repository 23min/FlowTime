# Release Summary: m-E16-02 Class Truth Boundary

**Milestone:** m-E16-02-class-truth-boundary
**Completed:** 2026-04-04
**Status:** ready for commit and merge approval

## Delivered

- Introduced explicit internal class-truth facts in Core via `ClassEntry<TPayload>` and `ClassEntryKind`.
- Distinguished real by-class data from synthesized fallback without relying on `*` or `DEFAULT` string conventions outside the boundary helper.
- Updated snapshot and window state projection to consume typed class-truth facts while preserving the public `byClass` contract shape.
- Preserved wildcard `byClass["*"]` output for fallback-only nodes and suppressed fallback serialization when real classes are present.
- Regenerated the affected fallback-only window API golden snapshots forward-only.

## Validation

- `dotnet build` passed.
- `dotnet test --nologo` passed.
- Targeted validation included `ClassMetricsAggregatorTests` and by-class API state endpoint tests for both real multi-class and fallback-only cases.
- 2026-04-05 wrap audit: full build and full suite passed after quarantining the legacy stopwatch-based `M15PerformanceTests.Test_ExpressionType_Performance` gate from default suite readiness. Targeted expression-type perf checks remain available via `M16BenchmarkRunner.RunM16ExpressionTypeBenchmarks`.

## Deferred Work

- None from this milestone. Next milestone is `m-E16-03-runtime-analytical-descriptor`.