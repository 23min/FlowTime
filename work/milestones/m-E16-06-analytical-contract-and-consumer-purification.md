# Milestone: Analytical Contract and Consumer Purification

**ID:** m-E16-06-analytical-contract-and-consumer-purification
**Epic:** Formula-First Core Purification
**Status:** draft

## Goal

Publish authoritative analytical facts in the current state contracts and delete the named current-state consumer heuristics in one forward-only cut.

## Context

Internal purity is not enough if current consumers still classify analytical behavior from `kind + logicalType`. This milestone makes the consumer scope explicit so the cleanup does not balloon into a vague "fix the UI later" bucket.

The four named Blazor UI consumers all reconstruct node classification from strings:
- `TimeTravelMetricsClient.IsServiceLike()` (line ~234) — filters nodes for metrics by string-matching kind/logicalType
- `Dashboard.razor.cs.IsServiceLike()` (line ~382) — identical copy of the same heuristic
- `GraphMapper.Classify()` (line ~227) — categorizes nodes as Expression/Constant/Service from kind strings for layout
- `TopologyCanvas.ClassifyNode()` (line ~499) — categorizes nodes for visual filtering with a broader enum

All four can be replaced by consuming the compiled node category and analytical facts from the API contract.

## Scope Clarification: GraphMapper.Classify() and TopologyCanvas.ClassifyNode()

Both `Classify()` and `ClassifyNode()` are in scope because they reconstruct a **domain fact** (node category) from `kind` strings. Node categorization is a compiled fact (expression/constant/service/queue/dlq) that the descriptor provides.

What stays in UI:
- **Layout rules** that consume the category: spacing ratios (55%/70%/100%), lane assignment (left/center/right), rendering dimensions — these are visual presentation, not domain logic.
- **Visibility toggles** (IncludeExpressionNodes, IncludeConstNodes, etc.) — these are user preferences, not domain classification.
- **Rendering decisions** (IsQueueLikeKind for width, IsComputedKind for leaf circles) — these may consume the category from the contract instead of re-deriving it.

What is deleted:
- The `Classify()` and `ClassifyNode()` methods themselves — replaced by reading the category from the contract.
- The `IsServiceLike()` methods — replaced by reading analytical facts from the contract.

## Acceptance Criteria

1. `FlowTime.Contracts` and the current `/state` and `/state_window` response shapes expose a compact authoritative analytical fact surface sufficient to determine analytical behavior without `kind + logicalType` inference. This includes at minimum: node category (expression/constant/service/queue/dlq), analytical applicability flags (queue semantics, service semantics, cycle-time decomposition), and warning applicability.
2. The explicit consumer scope for this milestone is migrated to use engine-published analytical facts: `src/FlowTime.UI/Services/TimeTravelMetricsClient.cs`, `src/FlowTime.UI/Pages/TimeTravel/Dashboard.razor.cs`, `src/FlowTime.UI/Components/Topology/GraphMapper.cs`, and `src/FlowTime.UI/Components/Topology/TopologyCanvas.razor.cs`.
3. Old hint fields and targeted analytical heuristics are removed in the same forward-only cut once those consumers are migrated; runs, fixtures, and approved snapshots are regenerated rather than compatibility-layered.
4. API/UI tests and a grep-based audit prove the targeted analytical classification helpers are deleted.
5. Documentation and decision records are updated so E-10 Phase 3 can resume on the purified boundary.

## Guards / DO NOT

- **DO NOT** expose the full internal Core descriptor type directly in the API contract. Prefer a compact fact surface designed for consumers, not a leak of internal types.
- **DO NOT** keep `kind + logicalType` as the primary way consumers determine analytical behavior. The old hint fields may remain for backward-compatible display purposes, but analytical behavior must come from the new fact surface.
- **DO NOT** grow scope implicitly. If another consumer surface appears before this milestone starts, add it explicitly to the consumer list or defer it with a documented reason.
- **DO NOT** let `IsServiceLike()`, `Classify()`, or `ClassifyNode()` survive in any form. These are the heuristics this milestone exists to delete.
- **DO NOT** move layout/rendering logic into Core. Layout spacing, lane assignment, and rendering dimensions stay in the UI — they consume the category, they don't define it.
- **DO NOT** add a new `kind`-string-based classification method in the UI as a "simpler" replacement. The contract provides the category as a fact.

## Deletion Targets

| Target | Location | Why |
|--------|----------|-----|
| `IsServiceLike()` | TimeTravelMetricsClient.cs:234 | String-based analytical node classification |
| `IsServiceLike()` | Dashboard.razor.cs:382 | Duplicate of the above |
| `Classify()` | GraphMapper.cs:227 | String-based node categorization |
| `ClassifyNode()` | TopologyCanvas.razor.cs:499 | String-based node categorization (broader enum) |
| `IsQueueLikeKind()` | TopologyCanvas.razor.cs | String-based queue detection for rendering |
| `IsComputedKind()` | TopologyCanvas.razor.cs | String-based expression detection for rendering |
| `IsSinkKind()` | TopologyCanvas.razor.cs | String-based sink detection |
| `EnsureFallbackClassesFromWindow()` (if survived m-E16-02) | Dashboard.razor.cs | Client-side class fallback heuristic |
| Old `nodeLogicalType` hint field usage for analytical behavior | Contracts, consumers | Replaced by authoritative fact surface |

## Test Strategy

- **Contract completeness tests:** Assert that the new fact surface on `/state` and `/state_window` responses contains node category, analytical flags, and warning applicability for all node types in test fixtures.
- **Consumer migration tests:** Each of the four named consumers has tests proving it reads from the contract fact surface, not from `kind + logicalType` strings.
- **Grep-based deletion audit:** `rg "IsServiceLike\|Classify\b\|ClassifyNode\|IsQueueLikeKind\|IsComputedKind\|IsSinkKind" src/FlowTime.UI/` returns zero matches.
- **Negative contract test:** Assert that removing `nodeLogicalType` from the contract does not break any consumer's analytical behavior (display may degrade, but analytical classification must not).
- **End-to-end parity:** UI integration tests (if present) show the same analytical behavior as before the migration.

## Technical Notes

- Prefer a small explicit fact surface over leaking internal Core types directly. A nested `analytical` object on the node contract with `{ category, hasQueueSemantics, hasServiceSemantics, hasCycleTimeDecomposition, warningApplicable }` is one option.
- Visual presentation categorization that is unrelated to analytical truth may remain in the UI, but it must consume the contract-provided category rather than re-deriving it from strings.
- If another consumer surface appears before this milestone starts, add it explicitly or defer it; do not grow scope implicitly.

## Out of Scope

- General UI redesign unrelated to analytical truth
- New analytical primitives
- Svelte UI migration (E-11) — the four named files are Blazor UI

## Dependencies

- [m-E16-05-analytical-warning-facts-and-primitive-cleanup](m-E16-05-analytical-warning-facts-and-primitive-cleanup.md)
