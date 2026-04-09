# Milestone: Runtime Analytical Descriptor

**ID:** m-E16-03-runtime-analytical-descriptor
**Epic:** Formula-First Core Purification
**Status:** completed

## Goal

Compile an authoritative analytical descriptor onto runtime nodes and make all analytical capability checks consume that descriptor. This removes adapter-owned logical-type reconstruction and turns analytical identity into a compiled invariant. The descriptor absorbs and replaces `AnalyticalCapabilities`.

## Context

`AnalyticalCapabilities` is currently a useful bridge abstraction resolved at query time from `kind + logicalType` strings. Once typed semantic references exist and class truth is explicit, runtime nodes can carry an analytical descriptor that is authoritative instead of forcing `StateQueryService` to infer one from strings and fallback behavior.

The descriptor absorbs `AnalyticalCapabilities` rather than sitting alongside it. `AnalyticalCapabilities.Resolve(kind, logicalType)` was the right bridge for m-ec-p3a1, but string-based resolution is exactly what E-16 eliminates. The descriptor is produced by the compiler using typed semantic references. The computation methods formerly on `AnalyticalCapabilities` (`ComputeBin`, `ComputeWindow`, etc.) now live on the analytical evaluator; broader evaluator consolidation continues in m-E16-04.

## Structural Decision: Descriptor absorbs AnalyticalCapabilities

| Aspect | AnalyticalCapabilities (current) | Analytical Descriptor (target) |
|--------|----------------------------------|-------------------------------|
| Resolution | `Resolve(string? kind, string? logicalType)` at query time | Produced by compiler at compile time from typed refs |
| Capability flags | `HasQueueSemantics`, `HasServiceSemantics`, etc. | Same flags, now compiled facts |
| `EffectiveKind` | String normalization bridge | Removed — replaced by typed analytical identity on the descriptor |
| Computation methods | `ComputeBin`, `ComputeWindow`, etc. | Owned by the descriptor-backed analytical evaluator; extended further in m-E16-04 |
| Queue origin | Not captured — recovered by `TryResolveSeriesNodeId` in API | Compiled fact: source-node identity for queue depth |
| Node category | Not captured — `GraphMapper.Classify()` and `TopologyCanvas.ClassifyNode()` reconstruct it | Compiled fact: expression / constant / service / queue / dlq / router |
| Parallelism | Not captured — resolved at runtime from `object?` | Compiled fact: resolved from typed parallelism reference |

## Acceptance Criteria

1. Runtime nodes carry a compiled analytical descriptor that captures: effective analytical identity, queue/service semantics, cycle-time applicability, warning applicability, queue-origin/source-node facts, node category, and resolved parallelism.
2. Explicit `serviceWithBuffer` nodes and reference-resolved queue-backed nodes produce identical descriptors using typed references and real fixture shapes, not basename heuristics.
3. Snapshot/window analytical paths, backlog warnings, flow-latency base composition, SLA helper logic, and internal state/graph projection paths consume the descriptor rather than reconstructing analytical identity from strings.
4. `AnalyticalCapabilities` is deleted. Its capability flags are absorbed into the descriptor. Its computation methods live on the descriptor-backed evaluator surface.
5. Adapter-side logical-type inference helpers used for runtime analytical behavior are deleted.
6. Core and targeted API tests prove parity for both explicit and reference-resolved cases.
7. Node category (expression/constant/service/queue/dlq) is a compiled descriptor field. No downstream code re-derives it from `kind` strings.
8. `dotnet build` and `dotnet test --nologo` are green.

## Guards / DO NOT

- **DO NOT** keep `AnalyticalCapabilities` alongside the descriptor. The descriptor replaces it.
- **DO NOT** resolve the descriptor from `kind + logicalType` strings. It must be produced by the compiler using typed semantic references.
- **DO NOT** put computation methods on the descriptor. Descriptor fields are facts, not deferred computations. Math moves to the evaluator.
- **DO NOT** add a `string EffectiveKind` field to the descriptor. That was a bridge concept for string-based resolution. The descriptor captures identity directly.
- **DO NOT** let any adapter or UI code reconstruct node category from `kind` strings after this milestone.
- **DO NOT** design the descriptor as a bag of booleans. Prefer a structured type with explicit semantics over `bool HasX` proliferation where a richer type is clearer.

## Deletion Targets

| Target | Location | Why |
|--------|----------|-----|
| `AnalyticalCapabilities` class | Core/Metrics/AnalyticalCapabilities.cs | Absorbed into the compiled descriptor |
| `AnalyticalCapabilities.Resolve()` | Core/Metrics/AnalyticalCapabilities.cs:32 | String-based resolution replaced by compiler |
| `DetermineLogicalType()` (if survived m-E16-01) | StateQueryService.cs:5252 | Adapter-side logicalType inference |
| `NormalizeKind()` (if survived m-E16-01) | StateQueryService.cs:5240 | String normalization — descriptor has the identity |
| `IsDlqKind()` (if survived m-E16-01) | StateQueryService.cs:5354 | DLQ classification becomes a compiled fact |
| `TryResolveServiceWithBufferDefinition()` | StateQueryService.cs:5267 | Queue-origin discovery from strings — now a compiled fact |
| UI-side `IsServiceLike()` for analytical gating (if any exists) | Various | Replaced by descriptor fact; UI deletion completes in m-E16-06 |

## Test Strategy

- **Descriptor compilation tests:** Given a model with explicit `serviceWithBuffer`, reference-resolved queue, expression, constant, DLQ, and router nodes, prove the compiler produces correct descriptors.
- **Parity tests:** Explicit `serviceWithBuffer` and reference-resolved queue-backed nodes produce identical descriptor fields.
- **Adapter-does-not-reclassify tests:** Assert that `StateQueryService` and projection code read descriptor fields without re-deriving analytical identity.
- **Category tests:** Prove that node category (expression/constant/service/queue/dlq) is a compiled fact, not inferred from strings downstream.
- **Regression guard:** Grep-based check that no new `NormalizeKind`, `DetermineLogicalType`, or `kind.ToLowerInvariant()` patterns appear in API code.

## Technical Notes

- Separate authoring `kind` from runtime analytical category. `kind` remains on the authored model; the descriptor owns the runtime truth.
- Effective analytical identity is a typed descriptor fact, not a normalized string bridge.
- Descriptor fields should be facts, not deferred computations.
- Queue origin and source-node identity should come from compiled references rather than file-name or string-shape inference.
- Consider an enum for node category rather than strings: `NodeCategory { Expression, Constant, Service, Queue, Dlq, Router }`.
- Legacy projection hints may still be serialized for compatibility until m-E16-06, but they must be derived from descriptor facts rather than reparsed semantics or local string heuristics.

## Out of Scope

- Public contract publication of the descriptor (m-E16-06)
- Consolidation of emitted-series truth and warning facts into the evaluator (m-E16-04, m-E16-05)

## Dependencies

- [m-E16-02-class-truth-boundary](m-E16-02-class-truth-boundary.md)
