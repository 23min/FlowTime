# Tracking: m-E16-02 — Class Truth Boundary

**Started:** 2026-04-06
**Completed:** 2026-04-06
**Branch:** `milestone/m-E16-01-compiled-semantic-references` (continuation branch)
**Spec:** `work/epics/E-16-formula-first-core-purification/m-E16-02-class-truth-boundary.md`

## Acceptance Criteria

- [x] AC-1: Internal class surfaces distinguish real by-class data, synthesized fallback, and no-class coverage explicitly via a typed representation (not just string key conventions).
- [x] AC-2: Wildcard fallback is represented as an explicit fallback fact rather than inferred solely from the `*` key or the absence of real class data.
- [x] AC-3: Analytical evaluation and projection code consume explicit class-truth facts instead of silently relying on wildcard fallback.
- [x] AC-4: Tests cover real multi-class fixtures separately from fallback projection cases, and approved outputs are regenerated forward-only where needed.
- [x] AC-5: A test or assertion proves that fallback-only data cannot be confused with real by-class analytical results in downstream evaluation.
- [x] AC-6: Forward-only regenerated runtime metadata carries explicit fallback labeling at the class boundary; legacy `*` / `DEFAULT` normalization helpers are deleted rather than retained as compatibility translators.
- [x] AC-7: `dotnet build` and `dotnet test --nologo` are green.

## Delivery Summary

- Added an explicit internal `ClassEntry<T>` shape so specific class data, explicit fallback data, and missing class coverage are modeled separately instead of through `*` / `DEFAULT` key conventions.
- Updated `ClassMetricsAggregator` to emit explicit class entries, compute coverage from real classes only, and stop synthesizing fallback from no-class totals.
- Updated `StateQueryService` snapshot/window projection to consume explicit class entries, preserving real multi-class output while emitting wildcard `byClass` only when explicit fallback series exist.
- Added focused core and API regressions for missing-class omission, explicit class coverage, and file-backed no-inference behavior; regenerated affected state approvals forward-only.
- Series metadata now carries explicit `classKind` labels, `StateQueryService` ignores unlabeled legacy class entries instead of translating `DEFAULT`, and `ClassEntry.FromLegacyClassId()` / `IsLegacyFallbackId()` are deleted.
- Repaired generated-telemetry class lookup and aligned schema validators/state schema with the live API contract so full-solution validation is green.

## Implementation Log

| Phase | What | Tests | Status |
|-------|------|-------|--------|
| 1 | Introduce typed specific-vs-fallback class entries in Core aggregation | `dotnet test tests/FlowTime.Core.Tests/FlowTime.Core.Tests.csproj --nologo --filter "FullyQualifiedName~ClassMetricsAggregatorTests\|FullyQualifiedName~SemanticReferenceResolverTests\|FullyQualifiedName~InvariantAnalyzerTests"` | complete |
| 2 | Rework state snapshot/window projection so missing class coverage omits `byClass` and explicit fallback remains distinct | `dotnet test tests/FlowTime.Api.Tests/FlowTime.Api.Tests.csproj --nologo --filter "FullyQualifiedName~GetStateWindow_ReturnsByClassSeries\|FullyQualifiedName~GetStateWindow_ReturnsByClassSeries_ForQueueNode\|FullyQualifiedName~GetState_ServiceWithBufferWithoutClassSeries_OmitsByClass\|FullyQualifiedName~GetStateWindow_ServiceWithBufferWithoutClassSeries_OmitsByClass\|FullyQualifiedName~GetStateWindow_NoExplicitClassSeries_OmitsByClass"` | complete |
| 3 | Wider validation and approval regeneration | `dotnet build FlowTime.sln` + `dotnet test tests/FlowTime.Api.Tests/FlowTime.Api.Tests.csproj --nologo --filter "FullyQualifiedName~GraphServiceTests\|FullyQualifiedName~MetricsServiceTests\|FullyQualifiedName~StateEndpointTests"` | complete |
| 4 | Full-suite fallout cleanup for generated telemetry lookup and schema validation drift | `dotnet test --nologo` | complete |

## Notes

- This milestone now treats "missing class coverage" as distinct from "explicit fallback coverage": missing coverage omits `byClass`, while explicit fallback still projects the wildcard `*` contract.
- Runtime logical-type and dependency resolution no longer infer producer identity from file-backed references; only node/self compiled references carry producer identity.
- Runtime metadata note: regenerated `series/index.json` now labels fallback vs specific class entries explicitly with `classKind`, and the legacy `*` / `DEFAULT` translator helpers are removed.
- Full-solution `dotnet test --nologo` is green after syncing manifest/state schema loaders and the state schema itself to the live DTO contract.