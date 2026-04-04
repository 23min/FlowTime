# Milestone: Compiled Semantic References

**ID:** m-E16-01-compiled-semantic-references
**Epic:** Formula-First Core Purification
**Status:** draft

## Goal

Introduce typed semantic references in the compiled/runtime model so FlowTime stops deriving runtime relationships from raw authoring strings inside API and analytical code. This milestone creates the first hard boundary between authoring syntax and runtime truth. It also types the `Parallelism` field, which currently uses `object?` and is parsed at runtime in both Core and API.

## Context

Today runtime-facing semantics still preserve raw strings such as `file:SupportQueue_queue.csv` or `series:NodeId`, and later layers parse those strings again to recover meaning. That is the root cause behind the current logical-type drift and duplicate parsers in `StateQueryService`.

Reference parsing is currently duplicated across four sites:
- `SemanticLoader.IsFileUri()` / `LoadSeries()` — Core, combines reference resolution with file I/O
- `ModelCompiler.IsFileReference()` — Core, reference detection only
- `StateQueryService.TryResolveSeriesNodeId()` — API, extracts node IDs from references for logicalType promotion
- `RunArtifactReader.NormalizeSeriesIdentifier()` — Generator, normalizes references during capture

Before analytical purity can be enforced, the compiler must own semantic reference resolution once. This cut is forward-only: old runs, fixtures, and approved snapshots can be regenerated once the compiled runtime shape changes.

## Pre-flight Gate

Before implementation begins, confirm these invariants are testable and will gate review:

1. No semantic string parsing in API/UI for analytical identity after this milestone.
2. No new `file:` / `series:` / `@` parsing helper outside the compiler.
3. `Parallelism` is no longer `object?` — it is a typed reference resolved at compile time.
4. SemanticLoader accepts typed references for data loading, not raw authoring strings.

## Acceptance Criteria

1. Runtime-facing node semantics use typed references for the semantic fields that affect runtime behavior, including arrivals, served, queueDepth, capacity, processingTimeMsSum, servedCount, attempts, failures, retryEcho, and related analytical inputs.
2. The compiler resolves self, node, file, and series references into one canonical representation with deterministic tests covering current repo-standard reference patterns.
3. Raw source text is retained only for provenance/debug surfaces; runtime behavior no longer depends on reparsing those raw strings.
4. Forward-only migration is explicit: existing run directories, generated fixtures, and approved snapshots that depend on the old runtime shape are regenerated; no compatibility reader or fallback path is added for the old analytical/runtime boundary.
5. `StateQueryService` no longer contains semantic-reference parsing helpers for analytical behavior or queue-source recovery.
6. `Parallelism` becomes a typed reference (numeric constant or series ref) resolved at compile time. The `object?` type on `NodeSemantics.Parallelism` is replaced.
7. `SemanticLoader` is split: reference resolution moves to the compiler; data loading stays as I/O and takes typed references as input instead of raw strings.
8. A grep-based audit confirms no `file:` / `series:` string parsing remains in API or adapter code for runtime analytical behavior.
9. `dotnet build` and `dotnet test --nologo` are green.

## Guards / DO NOT

- **DO NOT** add a compatibility reader or fallback parser for the old raw-string reference shapes. Forward-only.
- **DO NOT** create a new reference-parsing helper in the API or adapter layer. If a new reference pattern is discovered, extend the compiler.
- **DO NOT** keep `NodeSemantics.Parallelism` as `object?`. The loose typing is the problem, not a feature.
- **DO NOT** make SemanticLoader parse raw strings itself. After this milestone, SemanticLoader receives typed refs for I/O.
- **DO NOT** preserve `TryResolveSeriesNodeId`, `DetermineLogicalType`, or `seriesFileRegex` as "temporary" helpers. They must be deleted in this milestone.
- **DO NOT** introduce adapter-side reference resolution "for convenience." The compiler owns reference resolution.

## Deletion Targets

These specific code paths must be removed or replaced by this milestone's completion:

| Target | Location | Why |
|--------|----------|-----|
| `TryResolveSeriesNodeId()` | StateQueryService.cs:5288 | Parses `file:`, `series:`, `@` in the adapter to extract node IDs |
| `DetermineLogicalType()` | StateQueryService.cs:5252 | Infers logicalType by re-resolving semantic references at query time |
| `NormalizeKind()` | StateQueryService.cs:5240 | String normalization for kind — replaced by compiled descriptor |
| `IsDlqKind()` | StateQueryService.cs:5354 | String matching for DLQ — should be a compiled fact |
| `seriesFileRegex` | StateQueryService.cs:5250 | Regex for extracting filenames from `file:` URIs |
| `IsFileUri()` (as reference resolution) | SemanticLoader.cs:185 | Data loader should not decide what a reference means |
| `IsFileReference()` | ModelCompiler.cs:140 | Collapses into typed reference resolution in the compiler |
| `ParseParallelismScalar()` | StateQueryService.cs | Runtime parsing of `object?` parallelism — compiler resolves it |
| `BuildParallelismSeries()` | StateQueryService.cs:1694 | Runtime conversion of parallelism to series — compiler resolves it |

## Test Strategy

- **Reference resolution tests:** Cover all current reference patterns (`file:name.csv`, `series:NodeId`, `series:NodeId@Class`, self-reference, numeric literals for parallelism, node references for parallelism) with deterministic compiler-level tests.
- **Round-trip parity tests:** Prove that compiled typed references produce the same runtime data loading as the old raw-string path, then delete the old path.
- **Negative tests:** Verify that `StateQueryService` and adapter code do not contain reference-parsing helpers post-migration (grep-based audit as AC).
- **Fixture regeneration:** All existing approved snapshots that depend on the old runtime shape are regenerated and re-approved.

## Technical Notes

- Introduce a `SeriesRef`-style value object or equivalent typed semantic reference model. Consider a discriminated union: `FileRef | SeriesRef | SelfRef | NodeRef | ConstantRef`.
- Keep provenance-friendly raw text separate from runtime compiled semantics.
- Centralize reference parsing in the compiler/parser boundary rather than adding new adapter helpers.
- Prefer deleting and regenerating old runs/fixtures over carrying mixed old/new runtime shapes.
- **SemanticLoader split:** `SemanticLoader.LoadSeries(string uri, int bins)` becomes `SemanticLoader.LoadSeries(SeriesRef ref, int bins)` or equivalent. The `UriResolver.ResolveFilePath()` call stays but receives a typed ref.
- **Parallelism typing:** Replace `object? Parallelism` with a typed ref (e.g., `ParallelismRef` that is either a constant double or a series reference). Resolve at compile time. The 21-file cross-cut noted in gaps.md is acceptable because E-16 is the right place to do it.

## Out of Scope

- Public contract changes for analytical facts
- Class-truth boundary cleanup
- Consolidation of analytical evaluator logic
- UI/client cleanup

## Dependencies

- Builds on the existing compiler/model parser foundation in Core
