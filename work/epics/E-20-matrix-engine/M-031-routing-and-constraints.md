---
id: M-031
title: Routing and Constraints
status: done
parent: E-20
acs:
  - id: AC-1
    title: 'AC-1: Router weight-based splitting'
    status: met
  - id: AC-2
    title: 'AC-2: Router class-based routing'
    status: met
  - id: AC-3
    title: 'AC-3: Constraint proportional allocation'
    status: met
  - id: AC-4
    title: 'AC-4: Router → Constraint evaluation order'
    status: met
  - id: AC-5
    title: 'AC-5: Parity fixtures'
    status: met
  - id: AC-6
    title: 'AC-6: Existing tests unbroken'
    status: met
---

## Goal

Add router flow materialization and constraint allocation to the Rust engine so it can evaluate models with routers (weight-based and class-based flow splitting) and shared-capacity constraints (proportional allocation when demand exceeds capacity). After this milestone, the matrix engine handles the full evaluation pipeline except derived metrics, invariant analysis, and artifact writing.

## Context

M-030 delivered topology synthesis (QueueRecurrence, Shift, Convolve, DispatchGate), WIP overflow routing, and SHIFT-based backpressure feedback. The evaluator uses bin-major evaluation. 65 Rust tests passing.

The C# engine handles routing and constraints via:
1. `RouterFlowMaterializer.ComputeOverrides()` — distributes flows from a source to targets via class-based routing (priority 1) then weight-based routing (remaining flow, priority 2).
2. `ConstraintAllocator.AllocateProportional()` — when total demand exceeds capacity, allocates proportionally: `allocated[node] = capacity * (demand[node] / totalDemand)`.
3. `ConstraintAwareEvaluator` — applies router overrides first, then constraint overrides, then re-evaluates.
4. `ClassContributionBuilder` — decomposes totals into per-class series, propagates through graph.

In the matrix model, all of these become plan ops. Router splitting is `ScalarMul` (weight fractions) or direct column copying (class routing). Constraint allocation is a new `ProportionalAlloc` op that reads multiple demand columns + capacity and writes capped output columns. Multi-class is tracked as separate columns per class.

## Acceptance criteria

### AC-1 — AC-1: Router weight-based splitting

**AC-1: Router weight-based splitting.** The compiler processes `NodeDefinition.router` to split a source series across targets by weight:
- For each route: `target_arrivals += source * (weight / totalWeight)`.
- Routes without explicit weight default to 1.0.
- Multiple routes to the same target accumulate via VecAdd.
- The router's source is resolved from `router.inputs.queue` (the queue node whose outflow feeds the router) or the node's own series.
- Emitted as ScalarMul + VecAdd ops — no new Op variant needed.
### AC-2 — AC-2: Router class-based routing

**AC-2: Router class-based routing.** Routes with a `classes` list route per-class flow to specific targets:
- The compiler resolves per-class arrival columns from `model.traffic.arrivals` (each entry has a `classId` and `nodeId`).
- Class routes extract per-class columns and sum them for the target.
- Remaining flow (after class routes) is distributed by weight among weight-only routes.
- Per-class columns use the naming convention `{nodeId}__class_{classId}`.
### AC-3 — AC-3: Constraint proportional allocation

**AC-3: Constraint proportional allocation.** New `ProportionalAlloc` op:
- Reads N demand columns + 1 capacity column.
- Per bin: if `totalDemand > capacity`, writes `capped[i] = capacity * (demand[i] / totalDemand)`. Otherwise writes demands unchanged.
- The compiler processes `topology.constraints` to emit ProportionalAlloc ops, connecting each constraint's `semantics.arrivals` (demand total) and `semantics.served` (capacity) to the constrained topology nodes via `topologyNode.constraints` lists.
- Constrained nodes' inflow columns are replaced with the capped versions.
### AC-4 — AC-4: Router → Constraint evaluation order

**AC-4: Router → Constraint evaluation order.** The compiler emits router ops before constraint ops (matching C# `RouterAwareGraphEvaluator` → `ConstraintAwareEvaluator` order). Constraint allocation reads from router-adjusted columns. The unified topo sort orders: data nodes → router splits → constraint allocation → queue recurrence.
### AC-5 — AC-5: Parity fixtures

**AC-5: Parity fixtures.** Create test models and verify parity with C# output:
- Weight-based router: 3 routes with weights [0.5, 0.3, 0.2], verify target arrivals sum to source
- Class-based router: 2 classes routed to different targets
- Mixed router: some routes class-based, remainder weight-based
- Simple constraint: 2 nodes sharing capacity, demand > capacity → proportional split
- Constraint below capacity: demand < capacity → no capping
- Router + constraint combined: router feeds constrained nodes
### AC-6 — AC-6: Existing tests unbroken

**AC-6: Existing tests unbroken.** All 65 existing Rust tests still pass.
## Technical Notes

- **Router source resolution:** A router node in the YAML has `router.inputs.queue` pointing to a queue node. The router distributes the queue's outflow (served series) across targets. Each target gets a fraction of the served flow as its arrivals.
- **No new Op for routing:** Weight-based routing decomposes to ScalarMul + VecAdd (existing ops). Class routing decomposes to Copy + VecAdd. The compiler emits these standard ops — the router abstraction lives in the compiler, not the evaluator.
- **New Op for constraints:** `ProportionalAlloc` is a genuinely new operation — it reads N+1 columns and writes N columns with per-bin conditional logic. This is similar in spirit to QueueRecurrence (reads multiple columns, writes with conditional logic) but operates on groups of columns.
- **Per-class column naming:** `{nodeId}__class_{classId}` (double underscore to avoid collision with user-defined node IDs). These are internal columns that may not appear in outputs.
- **Constraint topology nodes:** Each constraint in `topology.constraints` has `semantics.arrivals` (total demand reference) and `semantics.served` (capacity reference). Topology nodes reference constraints via their `constraints` list. The compiler maps constraint IDs to the topology nodes they constrain.
- **Bin-major evaluation:** ProportionalAlloc processes one bin at a time (like all ops), reading demand[t] and capacity[t] and writing capped[t]. This is compatible with the bin-major evaluator from M-030.

## Out of Scope

- Derived metrics (utilization, latency, etc.) — M-032
- Invariant analysis — M-032
- Artifact writing — M-033
- File-based series references (`file:*.csv`) — future (telemetry mode)
- Per-class output series in artifacts — M-033 (artifact layer decides what to write)

## Dependencies

- M-030 complete (topology synthesis, sequential ops, bin-major evaluation)
