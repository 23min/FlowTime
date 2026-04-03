# Milestone: Compiled Semantic References

**ID:** m-E16-01-compiled-semantic-references
**Epic:** Formula-First Core Purification
**Status:** draft

## Goal

Introduce typed semantic references in the compiled/runtime model so FlowTime stops deriving runtime relationships from raw authoring strings inside API and analytical code. This milestone creates the first hard boundary between authoring syntax and runtime truth.

## Context

Today runtime-facing semantics still preserve raw strings such as `file:SupportQueue_queue.csv` or `series:NodeId`, and later layers parse those strings again to recover meaning. That is the root cause behind the current logical-type drift and duplicate parsers in `StateQueryService`.

Before analytical purity can be enforced, the compiler must own semantic reference resolution once. This cut is forward-only: old runs, fixtures, and approved snapshots can be regenerated once the compiled runtime shape changes.

## Acceptance Criteria

1. Runtime-facing node semantics use typed references for the semantic fields that affect runtime behavior, including arrivals, served, queueDepth, capacity, processingTimeMsSum, servedCount, attempts, failures, retryEcho, and related analytical inputs.
2. The compiler resolves self, node, file, and series references into one canonical representation with deterministic tests covering current repo-standard reference patterns.
3. Raw source text is retained only for provenance/debug surfaces; runtime behavior no longer depends on reparsing those raw strings.
4. Forward-only migration is explicit: existing run directories, generated fixtures, and approved snapshots that depend on the old runtime shape are regenerated; no compatibility reader or fallback path is added for the old analytical/runtime boundary.
5. `StateQueryService` no longer contains semantic-reference parsing helpers for analytical behavior or queue-source recovery.
6. `dotnet build` and `dotnet test --nologo` are green.

## Technical Notes

- Introduce a `SeriesRef`-style value object or equivalent typed semantic reference model.
- Keep provenance-friendly raw text separate from runtime compiled semantics.
- Centralize reference parsing in the compiler/parser boundary rather than adding new adapter helpers.
- Prefer deleting and regenerating old runs/fixtures over carrying mixed old/new runtime shapes.

## Out of Scope

- Public contract changes for analytical facts
- Class-truth boundary cleanup
- Consolidation of analytical evaluator logic
- UI/client cleanup

## Dependencies

- Builds on the existing compiler/model parser foundation in Core
