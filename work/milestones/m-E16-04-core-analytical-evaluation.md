# Milestone: Core Analytical Evaluation

**ID:** m-E16-04-core-analytical-evaluation
**Epic:** Formula-First Core Purification
**Status:** draft

## Goal

Move analytical values and emitted-series truth into a pure Core evaluation surface so the API projects analytical results instead of deciding them.

## Context

`AnalyticalCapabilities` moved current math into Core, but emitted-series truth is still partially computed in the adapter. That leaves the most important projection question — what should actually be emitted for this node/window/class — split across Core and API.

## Acceptance Criteria

1. Core exposes an analytical evaluation surface for snapshot, window, and by-class values driven by the compiled analytical descriptor and explicit class-truth boundary.
2. The analytical result includes derived values and emitted-derived-keys, emitted-series facts, or equivalent truth metadata sufficient for projection.
3. `StateQueryService` no longer computes analytical emission truth or per-node/per-class analytical math locally in the current state paths.
4. Tests prove analytical evaluation against both real multi-class fixtures and explicit fallback cases without conflating the two.
5. `dotnet build` and `dotnet test --nologo` are green.

## Technical Notes

- Keep pure math helpers where useful, but move policy composition out of the adapter.
- Prefer explicit result types over ad hoc dictionaries and flag tuples.
- Warning/analyzer facts are split into the next milestone so this slice stays independently shippable.

## Out of Scope

- Warning/analyzer fact publication
- Public contract changes
- Client migration

## Dependencies

- [m-E16-03-runtime-analytical-descriptor](m-E16-03-runtime-analytical-descriptor.md)
