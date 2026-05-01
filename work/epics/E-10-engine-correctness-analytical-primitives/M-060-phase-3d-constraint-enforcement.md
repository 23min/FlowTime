---
id: M-060
title: Phase 3d — Constraint Enforcement
status: done
parent: E-10
acs:
  - id: AC-1
    title: "**AC-1: Constraint allocation during evaluation.** After all nodes in the DAG have been evaluated, apply constraint
      allocation per bin: - For each constraint, collect the unconstrained `served[t]` for all assigned nodes. - If total
      demand exceeds constraint `capacity[t]`, allocate proportionally via `ConstraintAllocator.AllocateProportional`. - Cap
      each node's `served[t]` to its allocation. - Store the allocation results in the evaluation context for downstream consumption
      (state/state_window)."
    status: met
  - id: AC-2
    title: "**AC-2: Downstream propagation.** Capped `served` values propagate correctly through the DAG. Nodes that depend
      on a constrained node's output see the constrained (reduced) values, not the unconstrained originals. This may require
      a re-evaluation pass for downstream nodes after constraints are applied."
    status: met
  - id: AC-3
    title: '**AC-3: Constraint metadata in evaluation results.** The evaluation result includes per-constraint, per-node,
      per-bin allocation data so `StateQueryService` can expose constraint status (limited/unlimited) without recomputing.'
    status: met
  - id: AC-4
    title: '**AC-4: Tests and gate.** Tests cover: single constraint with two nodes (proportional split), unconstrained case
      (demand ≤ capacity, no capping), constraint with zero capacity (all nodes get zero), downstream propagation (constrained
      served affects downstream queue), multiple constraints on different node groups. Full test suite green. Determinism
      test updated.'
    status: met
---

## Goal

Wire `ConstraintAllocator` into `Graph.Evaluate` so declared constraints actually cap served throughput per bin. This makes constraints real — models that declare shared resource constraints will see them enforced during evaluation, not silently ignored.

## Context

`ConstraintAllocator` has existed since M-063 with correct proportional allocation logic:
- If total demand ≤ capacity: each node gets its full demand
- If total demand > capacity: each node gets `capacity × (its_demand / total_demand)`

But it has **zero callers in the evaluation pipeline**. It's only used in `StateQueryService` as a view-time filter. The March 2026 deep review flagged this as a design contradiction. Phase 2 downgraded the documentation to "foundations laid, enforcement pending." Phase 3d makes it real.

No backward compatibility is needed — existing model results are throwaway.

## Acceptance criteria

### AC-1 — **AC-1: Constraint allocation during evaluation.** After all nodes in the DAG have been evaluated, apply constraint allocation per bin: - For each constraint, collect the unconstrained `served[t]` for all assigned nodes. - If total demand exceeds constraint `capacity[t]`, allocate proportionally via `ConstraintAllocator.AllocateProportional`. - Cap each node's `served[t]` to its allocation. - Store the allocation results in the evaluation context for downstream consumption (state/state_window).

**AC-1: Constraint allocation during evaluation.** After all nodes in the DAG have been evaluated, apply constraint allocation per bin:
- For each constraint, collect the unconstrained `served[t]` for all assigned nodes.
- If total demand exceeds constraint `capacity[t]`, allocate proportionally via `ConstraintAllocator.AllocateProportional`.
- Cap each node's `served[t]` to its allocation.
- Store the allocation results in the evaluation context for downstream consumption (state/state_window).

### AC-2 — **AC-2: Downstream propagation.** Capped `served` values propagate correctly through the DAG. Nodes that depend on a constrained node's output see the constrained (reduced) values, not the unconstrained originals. This may require a re-evaluation pass for downstream nodes after constraints are applied.

### AC-3 — **AC-3: Constraint metadata in evaluation results.** The evaluation result includes per-constraint, per-node, per-bin allocation data so `StateQueryService` can expose constraint status (limited/unlimited) without recomputing.

### AC-4 — **AC-4: Tests and gate.** Tests cover: single constraint with two nodes (proportional split), unconstrained case (demand ≤ capacity, no capping), constraint with zero capacity (all nodes get zero), downstream propagation (constrained served affects downstream queue), multiple constraints on different node groups. Full test suite green. Determinism test updated.
## Technical Notes

- **Evaluation order:** Constraints are cross-node — they span multiple nodes in the DAG. The natural approach:
  1. Evaluate all nodes in topological order (unconstrained).
  2. Apply constraints: cap `served` for constrained nodes.
  3. Re-evaluate downstream nodes that depend on constrained outputs.
  
  Step 3 is necessary for correctness — if a ServiceWithBufferNode's `served` (outflow) is capped, its queue depth changes, which affects downstream nodes.

- **Alternative:** Integrate constraints into the evaluation loop. When evaluating a node, check if it has a constraint. If so, defer final `served` until all nodes in the constraint group are evaluated, then allocate. This avoids re-evaluation but complicates the topological traversal.

- **Constraint data model:** Constraints are defined in `model.Topology.Constraints` with assigned node IDs and a capacity series. `ConstraintAllocator` takes `Dictionary<string, double>` (demands) and `double` (capacity) per bin — already the right interface.

- **`StateQueryService` simplification:** Once constraints are enforced at evaluation time, `StateQueryService` no longer needs to apply them at view-time. The allocation results from evaluation can be passed through directly.

## Out of Scope

- New constraint types (only proportional allocation — existing algorithm).
- Constraint-aware bottleneck analysis (DSL epic).
- Constraint recommendation ("you should set capacity to X") — future work.
- MCP constraint patterns (deferred M-065 in gaps.md).

## Dependencies

- Phase 1 complete ✅ (Series immutability)
- Phase 3b helpful (WIP limits + constraints interact: a constrained node's overflow could hit a WIP limit) but not strictly required
