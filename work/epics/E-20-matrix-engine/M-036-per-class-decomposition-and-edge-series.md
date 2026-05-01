---
id: M-036
title: Per-Class Decomposition and Edge Series
status: done
parent: E-20
depends_on:
  - M-035
acs:
  - id: AC-1
    title: '**AC-1: Class assignment map.** The engine extracts `traffic.arrivals` entries to build a node-to-class mapping.
      Equivalent to `ClassAssignmentMapBuilder` (trivial — 37 lines of C#).'
    status: met
  - id: AC-2
    title: '**AC-2: Per-class series in EvalResult.** The `EvalResult` struct (or its successor) includes per-class series:
      for each node that has class contributions, the result contains `(node_id, class_id) → f64[]` series values. At minimum,
      arrival nodes, expression nodes, and serviceWithBuffer nodes must have per-class decomposition.'
    status: met
  - id: AC-3
    title: '**AC-3: Expression-tree per-class evaluation.** For `expr` nodes, per-class decomposition must handle the expression
      tree correctly: binary ops (add, sub, mul, div), scalar ops, and functions (SHIFT, CONV, MIN, MAX, CLAMP). The per-class
      series must sum to the total series within floating-point tolerance.'
    status: met
  - id: AC-4
    title: '**AC-4: ServiceWithBuffer per-class decomposition.** Queue nodes produce per-class queue depth, per-class served,
      per-class arrivals. Queue depth decomposition follows proportional allocation based on arrival class fractions.'
    status: met
  - id: AC-5
    title: '**AC-5: Edge series in EvalResult.** The result includes per-edge metrics: `(edge_id, metric) → f64[]` where metric
      is one of: `flowVolume`, `attemptsVolume`, `failuresVolume`, `retryVolume`. Per-class edge decomposition: `(edge_id,
      metric, class_id) → f64[]`.'
    status: met
  - id: AC-6
    title: '**AC-6: Router per-class distribution.** Class-based routes distribute flow to their designated targets. Weight-based
      routes distribute remaining (non-class-assigned) flow by weight. Router diagnostics (leakage, accuracy) are available
      in the result.'
    status: met
  - id: AC-7
    title: '**AC-7: Parity harness green for class fixtures.** The 3 class-enabled fixtures (`class-enabled.yaml`, `router-class.yaml`,
      `router-mixed.yaml`) pass the parity harness from M-035. Known divergences resolved.'
    status: met
  - id: AC-8
    title: '**AC-8: Parity harness green for edge fixtures.** The 6 edge-bearing fixtures pass the parity harness with edge
      series comparison. Edge series values match C# `EdgeFlowMaterializer` output within tolerance.'
    status: met
  - id: AC-9
    title: '**AC-9: Normalization invariant.** For every node, the sum of per-class series equals the total series within
      1e-10 tolerance. A Rust test asserts this invariant across all class-enabled fixtures.'
    status: met
---

## Goal

The Rust engine core returns complete evaluation results including per-class series decomposition and per-edge metrics. After this milestone, the engine is feature-complete for evaluation — it computes everything the C# engine computes. This is the critical prerequisite for E-17 and E-18.

## Context

The Rust engine currently computes class-based routing internally (using `__class_` prefixed temporary columns) but does not expose per-class series in its output. The C# engine uses `ClassContributionBuilder` (1,695 lines, 4-pass algorithm) and `EdgeFlowMaterializer` (764 lines) to produce per-class and per-edge series after evaluation.

Per D-044, the engine core must return complete results. The artifact sink (M-037) then formats and persists them. This milestone focuses on the evaluation layer only.

### Design consideration: port vs. redesign

The C# `ClassContributionBuilder` is a post-evaluation 4-pass algorithm that decomposes total series into per-class contributions using proportional allocation and expression-tree re-evaluation. In the Rust matrix engine, an alternative approach may be more natural:

- **Option A (port):** Implement a post-evaluation decomposition pass similar to C#. Receives the evaluated state matrix and splits columns by class proportions.
- **Option B (plan ops):** Extend the compiler to emit per-class columns as explicit plan operations during compilation. Each class gets its own columns, evaluated in the main bin-major loop. No post-processing needed.

Option B leverages the matrix architecture — it's "classes as columns" rather than "classes as post-processing." But it may increase matrix size for models with many classes. The spec author should evaluate both approaches during implementation and choose based on correctness and simplicity. Document the choice in the tracking doc.

## Acceptance criteria

### AC-1 — **AC-1: Class assignment map.** The engine extracts `traffic.arrivals` entries to build a node-to-class mapping. Equivalent to `ClassAssignmentMapBuilder` (trivial — 37 lines of C#).

### AC-2 — **AC-2: Per-class series in EvalResult.** The `EvalResult` struct (or its successor) includes per-class series: for each node that has class contributions, the result contains `(node_id, class_id) → f64[]` series values. At minimum, arrival nodes, expression nodes, and serviceWithBuffer nodes must have per-class decomposition.

### AC-3 — **AC-3: Expression-tree per-class evaluation.** For `expr` nodes, per-class decomposition must handle the expression tree correctly: binary ops (add, sub, mul, div), scalar ops, and functions (SHIFT, CONV, MIN, MAX, CLAMP). The per-class series must sum to the total series within floating-point tolerance.

### AC-4 — **AC-4: ServiceWithBuffer per-class decomposition.** Queue nodes produce per-class queue depth, per-class served, per-class arrivals. Queue depth decomposition follows proportional allocation based on arrival class fractions.

### AC-5 — **AC-5: Edge series in EvalResult.** The result includes per-edge metrics: `(edge_id, metric) → f64[]` where metric is one of: `flowVolume`, `attemptsVolume`, `failuresVolume`, `retryVolume`. Per-class edge decomposition: `(edge_id, metric, class_id) → f64[]`.

### AC-6 — **AC-6: Router per-class distribution.** Class-based routes distribute flow to their designated targets. Weight-based routes distribute remaining (non-class-assigned) flow by weight. Router diagnostics (leakage, accuracy) are available in the result.

### AC-7 — **AC-7: Parity harness green for class fixtures.** The 3 class-enabled fixtures (`class-enabled.yaml`, `router-class.yaml`, `router-mixed.yaml`) pass the parity harness from M-035. Known divergences resolved.

### AC-8 — **AC-8: Parity harness green for edge fixtures.** The 6 edge-bearing fixtures pass the parity harness with edge series comparison. Edge series values match C# `EdgeFlowMaterializer` output within tolerance.

### AC-9 — **AC-9: Normalization invariant.** For every node, the sum of per-class series equals the total series within 1e-10 tolerance. A Rust test asserts this invariant across all class-enabled fixtures.
## Out of Scope

- Artifact directory layout changes (M-037)
- Series ID naming convention (`{node}@{component}@{class}`) — that's sink concern
- Per-class CSV file writing — that's sink concern
- StateQueryService compatibility — that's sink concern
- Sim-specific provenance (D-043 deferral still applies)

## Key References

- `src/FlowTime.Core/Artifacts/ClassContributionBuilder.cs` — C# reference (1,695 lines)
- `src/FlowTime.Core/Routing/EdgeFlowMaterializer.cs` — C# reference (764 lines)
- `src/FlowTime.Core/Artifacts/ClassAssignmentMapBuilder.cs` — C# reference (37 lines)
- `engine/core/src/compiler.rs` — existing class-aware routing (lines 397-533)
- `engine/fixtures/class-enabled.yaml`, `router-class.yaml`, `router-mixed.yaml` — class fixtures
- D-044 — three-layer architecture decision
