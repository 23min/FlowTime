# Release Summary: m-E16-01 Compiled Semantic References

**Milestone:** m-E16-01-compiled-semantic-references
**Completed:** 2026-04-04
**Status:** ready for commit and merge approval

## Delivered

- Introduced compiled semantic reference types for runtime-facing node and constraint semantics.
- Moved runtime semantic reference resolution behind `SemanticReferenceResolver` and removed API-side reparsing for state and graph behavior.
- Typed runtime parallelism as `CompiledParallelismReference` and removed runtime dependence on raw `object?` parallelism parsing in Core/API.
- Preserved authored graph payload surfaces while using compiled/runtime facts internally.
- Restored raw-only telemetry warning compatibility in `ModeValidator` for tests and non-compiled `NodeSemantics` inputs.
- Normalized local JSON schema loading and aligned the time-travel state schema with the live state payload.

## Validation

- `dotnet build` passed.
- `dotnet test --nologo` passed.
- Targeted validation included compiler/reference tests, API graph/state tests, adapter manifest tests, and state schema tests.

## Deferred Work

- None from this milestone. Next milestone is `m-E16-02-class-truth-boundary`.