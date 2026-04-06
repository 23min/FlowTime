# Tracking: m-E16-01 — Compiled Semantic References

**Started:** 2026-04-05
**Completed:** pending
**Branch:** `milestone/m-E16-01-compiled-semantic-references` (from `main`)
**Spec:** `work/epics/E-16-formula-first-core-purification/m-E16-01-compiled-semantic-references.md`
**Build:** `dotnet build` green at milestone start
**Tests:** `dotnet test tests/FlowTime.Sim.Tests/FlowTime.Sim.Tests.csproj --nologo` green at milestone start

## Acceptance Criteria

- [x] AC-1: Runtime-facing node semantics use typed references for the semantic fields that affect runtime behavior, including arrivals, served, queueDepth, capacity, processingTimeMsSum, servedCount, attempts, failures, retryEcho, and related analytical inputs.
- [x] AC-2: The compiler resolves self, node, file, and series references into one canonical representation with deterministic tests covering current repo-standard reference patterns.
- [x] AC-3: Raw source text is retained only for provenance/debug surfaces; runtime behavior no longer depends on reparsing those raw strings.
- [x] AC-4: Forward-only migration is explicit: existing run directories, generated fixtures, and approved snapshots that depend on the old runtime shape are regenerated; no compatibility reader or fallback path is added for the old analytical/runtime boundary.
- [x] AC-5: `StateQueryService` no longer contains raw-string semantic-reference parsing helpers for data loading, queue-source recovery, or parallelism resolution. Any remaining logical-type bridge code consumes compiled typed semantics and is tracked for deletion in m-E16-03.
- [x] AC-6: `Parallelism` becomes a typed reference (numeric constant or series ref) resolved at compile time. The `object?` type on `NodeSemantics.Parallelism` is replaced.
- [x] AC-7: `SemanticLoader` is split: reference resolution moves to the compiler; data loading stays as I/O and takes typed references as input instead of raw strings.
- [x] AC-8: A grep-based audit confirms no `file:` / `series:` string parsing remains in API or adapter code for runtime analytical behavior.
- [x] AC-9: Runtime metadata readers no longer recover telemetry-source facts by reparsing raw YAML/model text once regenerated artifacts can carry those facts explicitly; raw-text fallback readers are deleted in the same forward-only cut.
- [x] AC-10: `dotnet build` and `dotnet test --nologo` are green.

## Delivery Summary

- Milestone started on a clean branch cut from `main`, with green build/test preflight.
- First implementation slice landed runtime typed `parallelism` across Core, Contracts conversion, and API/Core consumers before widening to the full semantic-reference model.
- Second implementation slice introduced `CompiledSeriesReference` + `SemanticReferenceResolver`, migrated runtime node/constraint semantics plus API graph/metrics consumers, and removed the remaining API raw `file:` / `series:` runtime parsers.
- Canonical run writers and test fixture writers now persist `telemetrySources` / `nodeSources` into `metadata.json`, and `RunManifestReader` consumes only those explicit facts instead of reparsing raw YAML/model text.
- Focused Core, Sim, and API regression slices are green, `dotnet build` is green, and the full `dotnet test --nologo` suite is now green after the downstream schema/manifest fallout was reconciled forward-only.
- Existing local whitespace-only cleanup in `src/FlowTime.Core/Models/ModelParser.cs` was preserved and left untouched.

## Implementation Log

| Phase | What | Tests | Status |
|-------|------|-------|--------|
| 1 | Start milestone on a clean main-based branch, add epic-local tracking, and land runtime `ParallelismReference` through Core plus API/Core consumers | `dotnet test tests/FlowTime.Core.Tests/FlowTime.Core.Tests.csproj --nologo --filter "FullyQualifiedName~TopologyTests\|FullyQualifiedName~SemanticLoaderTests\|FullyQualifiedName~InvariantAnalyzerTests"`; `dotnet test tests/FlowTime.Api.Tests/FlowTime.Api.Tests.csproj --nologo --filter "FullyQualifiedName~Parallelism"`; `dotnet build` | complete |
| 2 | Expand from typed parallelism to compiler-owned semantic reference resolution for other runtime semantic fields, including API graph/metrics fallback cleanup | `dotnet test tests/FlowTime.Core.Tests/FlowTime.Core.Tests.csproj --nologo --filter "FullyQualifiedName~SemanticReferenceResolverTests\|FullyQualifiedName~SemanticLoaderTests\|FullyQualifiedName~ModelCompilerTests\|FullyQualifiedName~ModelServiceParallelismTests\|FullyQualifiedName~ParallelismReferenceSerializationTests"`; `dotnet test tests/FlowTime.Api.Tests/FlowTime.Api.Tests.csproj --nologo --filter "FullyQualifiedName~MetricsServiceTests\|FullyQualifiedName~GraphServiceTests"` | complete |
| 3 | Run repo validation and deletion audit | `dotnet build`; `dotnet test --nologo`; grep audit over `src/FlowTime.API/Services/**` and `src/FlowTime.Adapters.Synthetic/**` for raw `file:` / `series:` parsing | complete |

## Notes

- This branch uses the epic-local milestone layout described by the current framework docs.
- Review gate for the first slice: failing tests prove numeric-literal and series-backed `parallelism` compile into a typed runtime form without API-side reparsing.
- Audit note: `src/FlowTime.API/Services/**` and `src/FlowTime.Adapters.Synthetic/**` no longer contain `StartsWith("file:")` / `StartsWith("series:")` runtime analytical parsing.
- Runtime metadata note: `RunManifestReader.ExtractTelemetrySourcesFromText()` is deleted; regenerated artifacts and hand-written test fixtures now carry explicit telemetry-source metadata.
- Full-suite validation note: `dotnet test --nologo` is green after the schema loader/meta-schema cleanup and state schema contract reconciliation landed alongside the E-16-02 work.