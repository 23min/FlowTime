# Milestone: Runtime Analytical Descriptor

**ID:** m-E16-03-runtime-analytical-descriptor
**Epic:** Formula-First Core Purification
**Status:** draft

## Goal

Compile an authoritative analytical descriptor onto runtime nodes and make all analytical capability checks consume that descriptor. This removes adapter-owned logical-type reconstruction and turns analytical identity into a compiled invariant.

## Context

`AnalyticalCapabilities` is currently a useful bridge abstraction layered on top of incomplete inputs. Once typed semantic references exist and class truth is explicit, runtime nodes can carry an analytical descriptor that is authoritative instead of forcing `StateQueryService` to infer one from strings and fallback behavior.

## Acceptance Criteria

1. Runtime nodes carry a compiled analytical descriptor that captures effective analytical kind, queue/service semantics, cycle-time applicability, warning applicability, and queue/source facts.
2. Explicit `serviceWithBuffer` nodes and reference-resolved queue-backed nodes produce identical descriptors using typed references and real fixture shapes, not basename heuristics.
3. Snapshot/window analytical paths, backlog warnings, flow-latency base composition, and SLA helper logic consume the descriptor rather than reconstructing analytical identity from strings.
4. Adapter-side logical-type inference helpers used for runtime analytical behavior are deleted.
5. Core and targeted API tests prove parity for both explicit and reference-resolved cases.

## Technical Notes

- Separate authoring `kind` from runtime analytical category.
- Descriptor fields should be facts, not deferred computations.
- Queue origin and source-node identity should come from compiled references rather than file-name or string-shape inference.

## Out of Scope

- Public contract publication of the descriptor
- Consolidation of emitted-series truth and warning facts into the evaluator

## Dependencies

- [m-E16-02-class-truth-boundary](m-E16-02-class-truth-boundary.md)
