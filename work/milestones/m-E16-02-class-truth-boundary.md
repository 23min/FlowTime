# Milestone: Class Truth Boundary

**ID:** m-E16-02-class-truth-boundary
**Epic:** Formula-First Core Purification
**Status:** draft

## Goal

Separate real by-class truth from wildcard fallback before runtime analytical descriptors and evaluators depend on it. This milestone makes class truth explicit instead of letting later layers infer it from `*` and coverage side effects.

## Context

The current class path mixes aggregation, coverage, warnings, and synthesized wildcard fallback. That is too late and too implicit for the rest of E-16: if evaluator and contract work build on it as-is, they will keep guessing whether a class result is real or synthesized.

## Acceptance Criteria

1. Internal class surfaces distinguish real by-class data, synthesized fallback, and no-class coverage explicitly.
2. Wildcard fallback is represented as an explicit fallback fact rather than inferred solely from the `*` key.
3. Analytical evaluation and projection code consume explicit class-truth facts instead of silently relying on wildcard fallback.
4. Tests cover real multi-class fixtures separately from fallback projection cases, and approved outputs are regenerated forward-only where needed.
5. `dotnet build` and `dotnet test --nologo` are green.

## Technical Notes

- Favor a small explicit runtime shape over spreading fallback booleans across unrelated DTOs.
- Keep this milestone internal to Core/API surfaces; public analytical contract publication comes later.
- If wildcard fallback remains visible externally at this stage, it must be labeled as fallback.

## Out of Scope

- Public analytical contract redesign
- Runtime analytical descriptor publication
- Client heuristic deletion

## Dependencies

- [m-E16-01-compiled-semantic-references](m-E16-01-compiled-semantic-references.md)
