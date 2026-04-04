# Milestone: Class Truth Boundary

**ID:** m-E16-02-class-truth-boundary
**Epic:** Formula-First Core Purification
**Status:** draft

## Goal

Separate real by-class truth from wildcard fallback before runtime analytical descriptors and evaluators depend on it. This milestone makes class truth explicit instead of letting later layers infer it from `*` and coverage side effects.

## Context

The current class path mixes aggregation, coverage, warnings, and synthesized wildcard fallback. That is too late and too implicit for the rest of E-16: if evaluator and contract work build on it as-is, they will keep guessing whether a class result is real or synthesized.

Wildcard handling is currently scattered across:
- `StateQueryService` (line ~1711): blank class keys → `*`
- `StateQueryService` (line ~4712): `*` and `DEFAULT` detection, skip logic to avoid double-counting
- `Dashboard.razor.cs` `EnsureFallbackClassesFromWindow()`: client-side fallback class extraction

## Acceptance Criteria

1. Internal class surfaces distinguish real by-class data, synthesized fallback, and no-class coverage explicitly via a typed representation (not just string key conventions).
2. Wildcard fallback is represented as an explicit fallback fact rather than inferred solely from the `*` key or the absence of real class data.
3. Analytical evaluation and projection code consume explicit class-truth facts instead of silently relying on wildcard fallback.
4. Tests cover real multi-class fixtures separately from fallback projection cases, and approved outputs are regenerated forward-only where needed.
5. A test or assertion proves that fallback-only data cannot be confused with real by-class analytical results in downstream evaluation.
6. `dotnet build` and `dotnet test --nologo` are green.

## Guards / DO NOT

- **DO NOT** add `IsFallback` booleans to every DTO or scatter fallback awareness across unrelated types. Use a small explicit runtime shape (e.g., a tagged union or wrapper) at the class-data boundary.
- **DO NOT** keep `*` as the sole signal for fallback. The `*` key may persist for serialization, but runtime code must not use string-equality checks against `*` or `DEFAULT` to decide truth vs fallback.
- **DO NOT** unify `*` and `DEFAULT` by normalizing to one string. Replace the string convention with a typed fact.
- **DO NOT** change the public API contract in this milestone. Class-truth is an internal boundary; contract publication comes in m-E16-06.
- **DO NOT** let tests pass by exercising only fallback projection. Real multi-class and fallback-only must be separate test categories.

## Deletion Targets

| Target | Location | Why |
|--------|----------|-----|
| Blank-key → `*` string inference | StateQueryService.cs:~1711 | Replace with typed fallback fact at the source |
| `*`/`DEFAULT` string-equality skip logic | StateQueryService.cs:~4712 | Replaced by typed class-truth discriminator |
| `EnsureFallbackClassesFromWindow()` | Dashboard.razor.cs:~181 | Client-side fallback heuristic — replaced by explicit fact from API |

## Test Strategy

- **Two distinct fixture categories:** (1) real multi-class data with 2+ named classes, (2) single-class or no-class data that produces fallback. Tests must not share fixtures.
- **Fallback-is-labeled tests:** Assert that downstream evaluation code receives an explicit fallback marker, not just a `*` key string.
- **No false parity tests:** A test that proves "by-class analytical results match expectations" must use real multi-class fixtures, not wildcard-only data.
- **Negative assertion:** Grep-based check that no new `== "*"` or `== "DEFAULT"` string comparisons are added for analytical class-truth decisions.

## Technical Notes

- Favor a small explicit runtime shape over spreading fallback booleans across unrelated DTOs. A tagged type like `ClassResult<T>` with `Real | Fallback` discriminator is one option.
- Keep this milestone internal to Core/API surfaces; public analytical contract publication comes later.
- If wildcard fallback remains visible externally at this stage, it must be labeled as fallback in the internal representation even if the external key is still `*`.

## Out of Scope

- Public analytical contract redesign
- Runtime analytical descriptor publication
- Client heuristic deletion

## Dependencies

- [m-E16-01-compiled-semantic-references](m-E16-01-compiled-semantic-references.md)
