# Tracking: Compiled Semantic References

**Milestone:** m-E16-01-compiled-semantic-references
**Branch:** milestone/m-E16-01-compiled-semantic-references
**Started:** 2026-04-04
**Status:** complete

## Acceptance Criteria

- [x] AC1: Runtime-facing node semantics use typed references for runtime-relevant semantic fields, including analytical inputs and related sources.
- [x] AC2: The compiler resolves self, node, file, and series references into one canonical representation with deterministic coverage for repo-standard patterns.
- [x] AC3: Raw source text remains provenance/debug only; runtime behavior no longer depends on reparsing it.
- [x] AC4: Forward-only migration is explicit; old runs, generated fixtures, and approved snapshots are regenerated with no compatibility reader.
- [x] AC5: StateQueryService no longer contains raw-string semantic-reference parsing helpers for data loading, queue-source recovery, or parallelism resolution.
- [x] AC6: Parallelism becomes a typed reference resolved at compile time; NodeSemantics.Parallelism is no longer object?.
- [x] AC7: SemanticLoader is split so reference resolution lives in the compiler and data loading takes typed refs as input.
- [x] AC8: Grep audit confirms no file:/series:/@ parsing remains in API or adapter code for runtime analytical behavior.
- [x] AC9: dotnet build and dotnet test --nologo are green.

## Implementation Log

| Phase | What | Tests | Status |
|-------|------|-------|--------|
| 1 | Preflight, branch setup, tracking, and baseline capture | 0 | complete |
| 2 | Typed runtime reference model + failing compiler/runtime tests | 4 | complete |
| 3 | Compiler-owned reference resolution + SemanticLoader split | targeted + full | complete |
| 4 | API de-parser cleanup + validation sweep | targeted + full | complete |

## Test Summary

- **Baseline build:** passing
- **Milestone tests added:** 5
- **Targeted validation:** `ModelCompilerTests`, `SemanticReferenceResolverTests`, and `ModeValidatorTests` green in `FlowTime.Core.Tests`; `GraphServiceTests` + `StateEndpointTests` green (68 tests in `FlowTime.Api.Tests`).
- **Full validation:** `dotnet build` green and `dotnet test --nologo` green on 2026-04-04 after restoring raw-semantics fallback in `ModeValidator`, normalizing local schema loading, and aligning `time-travel-state.schema.json` with the live payload.

## Notes

- User explicitly approved starting E-16 on 2026-04-04.
- Local epic and milestone branches created; no push performed because push requires separate human approval.
- First implementation slice landed: compiled semantic reference types, compiler-side reference resolver, runtime semantics populated with compiled refs, and `SemanticLoader` now reads compiled refs instead of parsing raw node semantics.
- Completion slice removed API-side semantic reparsing from `StateQueryService` and `GraphService`, migrated runtime parallelism to `CompiledParallelismReference`, and preserved authored graph output while using compiled semantics internally.
- Follow-up fix restored `ModeValidator` telemetry unresolved-source warnings for raw-only `NodeSemantics` inputs by falling back to `SemanticReferenceResolver` when compiled refs are absent.
- Final validation fix strips local schema `$schema` declarations before `JsonSchema.FromText(...)` in runtime/tests and updates `docs/schemas/time-travel-state.schema.json` to match the current state payload.

## Completion

- **Completed:** 2026-04-04
- **Final test count:** targeted `FlowTime.Api.Tests` 68/68 passing; `StateResponseSchemaTests` 15/15 passing; full suite green via `dotnet test --nologo`.
- **Deferred items:** none.
