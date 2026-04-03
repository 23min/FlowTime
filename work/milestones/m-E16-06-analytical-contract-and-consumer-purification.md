# Milestone: Analytical Contract and Consumer Purification

**ID:** m-E16-06-analytical-contract-and-consumer-purification
**Epic:** Formula-First Core Purification
**Status:** draft

## Goal

Publish authoritative analytical facts in the current state contracts and delete the named current-state consumer heuristics in one forward-only cut.

## Context

Internal purity is not enough if current consumers still classify analytical behavior from `kind + logicalType`. This milestone makes the consumer scope explicit so the cleanup does not balloon into a vague "fix the UI later" bucket.

## Acceptance Criteria

1. `FlowTime.Contracts` and the current `/state` and `/state_window` response shapes expose a compact authoritative analytical fact surface sufficient to determine analytical behavior without `kind + logicalType` inference.
2. The explicit consumer scope for this milestone is migrated to use engine-published analytical facts: `src/FlowTime.UI/Services/TimeTravelMetricsClient.cs`, `src/FlowTime.UI/Pages/TimeTravel/Dashboard.razor.cs`, `src/FlowTime.UI/Components/Topology/GraphMapper.cs`, and `src/FlowTime.UI/Components/Topology/TopologyCanvas.razor.cs`.
3. Old hint fields and targeted analytical heuristics are removed in the same forward-only cut once those consumers are migrated; runs, fixtures, and approved snapshots are regenerated rather than compatibility-layered.
4. API/UI tests and a grep-based audit prove the targeted analytical classification helpers are deleted.
5. Documentation and decision records are updated so E-10 Phase 3 can resume on the purified boundary.

## Technical Notes

- Prefer a small explicit fact surface over leaking internal Core types directly.
- Visual presentation categorization that is unrelated to analytical truth may remain, but it must stop driving analytical behavior.
- If another consumer surface appears before this milestone starts, add it explicitly or defer it; do not grow scope implicitly.

## Out of Scope

- General UI redesign unrelated to analytical truth
- New analytical primitives

## Dependencies

- [m-E16-05-analytical-warning-facts-and-primitive-cleanup](m-E16-05-analytical-warning-facts-and-primitive-cleanup.md)
