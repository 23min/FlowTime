# Milestone: Analytical Warning Facts and Primitive Cleanup

**ID:** m-E16-05-analytical-warning-facts-and-primitive-cleanup
**Epic:** Formula-First Core Purification
**Status:** draft

## Goal

Move analytical warning facts into Core analyzers and finish the primitive ownership cleanup so analytical policy has one owner per concept.

## Context

Even after analytical values and emitted truth move into Core, warning eligibility and primitive ownership can still drift if stationarity, backlog logic, and latency helpers remain split across adapter code and partial Core helpers.

## Acceptance Criteria

1. Stationarity, backlog, and related analytical warnings consume compiled descriptors plus evaluated analytical facts rather than raw semantics or adapter-local gating.
2. Core returns warning facts or analyzer results, and projection code only formats them into DTOs.
3. Ownership of `CycleTimeComputer`, `LatencyComputer`, and stationarity logic is explicit; duplicate analytical policy paths are removed.
4. Current state paths no longer call direct analytical primitives outside Core evaluator/analyzer surfaces for runtime analytical behavior.
5. `dotnet build` and `dotnet test --nologo` are green, with regenerated approved outputs where warning facts changed.

## Technical Notes

- Keep warnings as analyzers over evaluated facts, not as ad hoc projection helpers.
- If a primitive survives, document exactly what it owns and what it does not.
- Avoid folding unrelated non-analytical warnings into this milestone.

## Out of Scope

- Public contract redesign
- UI/client heuristic deletion

## Dependencies

- [m-E16-04-core-analytical-evaluation](m-E16-04-core-analytical-evaluation.md)
