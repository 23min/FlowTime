---
id: M-017
title: Analytical Contract and Consumer Purification
status: done
parent: E-16
acs:
  - id: AC-1
    title: '`FlowTime.Contracts` and the current `/state`, `/state_window`, and `/graph` response shapes expose a compact
      authoritative fact surface sufficient to determine analytical behavior and node category without `kind + logicalType`
      inference. This includes at minimum: node category (expression/constant/service/queue/dlq/router/sink as needed by first-party
      consumers), analytical applicability flags (queue semantics, service semantics, cycle-time decomposition), fallback/class-truth
      labeling, and warning applicability.'
    status: met
  - id: AC-2
    title: 'The explicit first-party consumer scope for this milestone is migrated to use engine-published facts: `src/FlowTime.UI/Services/TimeTravelMetricsClient.cs`,
      `src/FlowTime.UI/Pages/TimeTravel/Dashboard.razor.cs`, `src/FlowTime.UI/Pages/TimeTravel/Topology.razor`, `src/FlowTime.UI/Components/Topology/GraphMapper.cs`,
      `src/FlowTime.UI/Components/Topology/TopologyCanvas.razor.cs`, `src/FlowTime.UI/Components/Topology/TooltipFormatter.cs`,
      and `src/FlowTime.UI/wwwroot/js/topologyCanvas.js`.'
    status: met
  - id: AC-3
    title: Old hint fields and targeted analytical heuristics are removed in the same forward-only cut once those 
      consumers are migrated; runs, fixtures, and approved snapshots are regenerated rather than compatibility-layered.
    status: met
  - id: AC-4
    title: API/UI tests and a grep-based audit prove the targeted analytical classification helpers are deleted.
    status: met
  - id: AC-5
    title: Documentation and decision records are updated so E-10 Phase 3 can resume on the purified boundary.
    status: met
---

## Goal

Publish authoritative analytical and categorical facts in the current state and graph contracts and delete the first-party consumer heuristics in one forward-only cut.

## Context

Internal purity is not enough if current consumers still classify analytical behavior or node category from `kind + logicalType`. This milestone makes the first-party consumer scope explicit so the cleanup does not balloon into a vague "fix the UI later" bucket.

The current first-party consumers reconstruct node classification from strings across both state and graph surfaces:
- `TimeTravelMetricsClient.IsServiceLike()` (line ~234) — filters nodes for metrics by string-matching kind/logicalType
- `Dashboard.razor.cs.IsServiceLike()` (line ~382) — identical copy of the same heuristic
- `Topology.razor` helpers (`IsComputedKind`, `IsSinkKind`, fallback-class extraction) — classify and patch topology state from string hints
- `GraphMapper.Classify()` (line ~227) — categorizes nodes as Expression/Constant/Service from kind strings for layout
- `TopologyCanvas.ClassifyNode()` (line ~499) — categorizes nodes for visual filtering with a broader enum
- `TooltipFormatter` and `topologyCanvas.js` helpers — classify sink/queue/computed nodes in the rendering path

All of these can be replaced by consuming compiled node category and analytical facts from the API contracts.

## Scope Clarification: State vs Graph Consumers

`/state` and `/state_window` consumers need analytical applicability facts. `/graph` consumers need node category facts. Both are compiled domain facts and both are in scope for this cut.

What stays in UI:
- **Layout rules** that consume the category: spacing ratios (55%/70%/100%), lane assignment (left/center/right), rendering dimensions — these are visual presentation, not domain logic.
- **Visibility toggles** (IncludeExpressionNodes, IncludeConstNodes, etc.) — these are user preferences, not domain classification.
- **Rendering decisions** (IsQueueLikeKind for width, IsComputedKind for leaf circles) — these may consume the category from the contract instead of re-deriving it.

What is deleted:
- The `Classify()` and `ClassifyNode()` methods themselves — replaced by reading the category from the contract.
- The `IsServiceLike()` methods — replaced by reading analytical facts from the contract.

## Acceptance criteria

### AC-1 — `FlowTime.Contracts` and the current `/state`, `/state_window`, and `/graph` response shapes expose a compact authoritative fact surface sufficient to determine analytical behavior and node category without `kind + logicalType` inference. This includes at minimum: node category (expression/constant/service/queue/dlq/router/sink as needed by first-party consumers), analytical applicability flags (queue semantics, service semantics, cycle-time decomposition), fallback/class-truth labeling, and warning applicability.

### AC-2 — The explicit first-party consumer scope for this milestone is migrated to use engine-published facts: `src/FlowTime.UI/Services/TimeTravelMetricsClient.cs`, `src/FlowTime.UI/Pages/TimeTravel/Dashboard.razor.cs`, `src/FlowTime.UI/Pages/TimeTravel/Topology.razor`, `src/FlowTime.UI/Components/Topology/GraphMapper.cs`, `src/FlowTime.UI/Components/Topology/TopologyCanvas.razor.cs`, `src/FlowTime.UI/Components/Topology/TooltipFormatter.cs`, and `src/FlowTime.UI/wwwroot/js/topologyCanvas.js`.

### AC-3 — Old hint fields and targeted analytical heuristics are removed in the same forward-only cut once those consumers are migrated; runs, fixtures, and approved snapshots are regenerated rather than compatibility-layered.

### AC-4 — API/UI tests and a grep-based audit prove the targeted analytical classification helpers are deleted.

### AC-5 — Documentation and decision records are updated so E-10 Phase 3 can resume on the purified boundary.
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
| `EnsureFallbackClassMetadataFromWindow()` | Topology.razor | Client-side fallback class heuristic on the topology page |
| `IsComputedKind()` / `IsSinkKind()` / related kind helpers | Topology.razor | String-based topology-page categorization |
| `Classify()` | GraphMapper.cs:227 | String-based node categorization |
| `ClassifyNode()` | TopologyCanvas.razor.cs:499 | String-based node categorization (broader enum) |
| `IsQueueLikeKind()` | TopologyCanvas.razor.cs | String-based queue detection for rendering |
| `IsComputedKind()` | TopologyCanvas.razor.cs | String-based expression detection for rendering |
| `IsSinkKind()` | TopologyCanvas.razor.cs | String-based sink detection |
| `IsSinkKind()` / dependency-kind helpers | TooltipFormatter.cs | String-based tooltip categorization |
| `isComputedKind()` / `isQueueLikeKind()` / `isSinkKind()` | wwwroot/js/topologyCanvas.js | String-based canvas/rendering categorization |
| `EnsureFallbackClassesFromWindow()` (if survived M-013) | Dashboard.razor.cs | Client-side class fallback heuristic |
| Old `nodeLogicalType` hint field usage for analytical behavior | Contracts, consumers | Replaced by authoritative fact surface |

## Test Strategy

- **Contract completeness tests:** Assert that the new fact surface on `/state`, `/state_window`, and `/graph` responses contains the category and analytical flags required by first-party consumers.
- **Consumer migration tests:** Each named first-party consumer has tests proving it reads from the contract fact surface, not from `kind + logicalType` strings.
- **Grep-based deletion audit:** `rg "IsServiceLike\|Classify\b\|ClassifyNode\|IsQueueLikeKind\|IsComputedKind\|IsSinkKind\|EnsureFallbackClassMetadataFromWindow\|isComputedKind\|isQueueLikeKind\|isSinkKind" src/FlowTime.UI/` returns zero matches.
- **Negative contract test:** Assert that removing `nodeLogicalType` from the contract does not break any consumer's analytical behavior (display may degrade, but analytical classification must not).
- **End-to-end parity:** UI integration tests (if present) show the same analytical behavior as before the migration.

## Technical Notes

- Prefer a small explicit fact surface over leaking internal Core types directly. A nested `analytical` object on state nodes and a compact `category` fact on graph nodes is one option.
- Visual presentation categorization that is unrelated to analytical truth may remain in the UI, but it must consume the contract-provided category rather than re-deriving it from strings.
- If another consumer surface appears before this milestone starts, add it explicitly or defer it; do not grow scope implicitly.

## Out of Scope

- General UI redesign unrelated to analytical truth
- New analytical primitives
- Svelte UI migration (E-11) — the four named files are Blazor UI

## Dependencies

- [M-016](M-016.md)
