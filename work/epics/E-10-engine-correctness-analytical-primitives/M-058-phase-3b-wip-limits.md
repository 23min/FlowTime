---
id: M-058
title: Phase 3b — WIP Limits
status: done
parent: E-10
acs:
  - id: AC-1
    title: '**AC-1: WIP limit on ServiceWithBufferNode.** Optional `wipLimit` field (scalar or series reference for time-varying
      limits). When `Q[t] > wipLimit[t]`: ``` overflow[t] = Q[t] - wipLimit[t] Q[t] = wipLimit[t] ``` The overflow amount
      is tracked as a series.'
    status: met
  - id: AC-2
    title: '**AC-2: WIP overflow routing.** Optional `wipOverflow` field on the node definition: - If omitted or `"loss"`:
      overflow is added to the loss series (items disappear). - If a node ID: the compiler wires the overflow as inflow to
      the target node. - Cascading: the target can itself have a `wipLimit` and overflow to another target. - The compiler
      validates no cycles are created by overflow wiring.'
    status: met
  - id: AC-3
    title: '**AC-3: Model schema updated.** `model.schema.yaml` and `NodeDefinition` support `wipLimit` (number or string
      series reference) and `wipOverflow` (string node ID or `"loss"`). Template schema updated if applicable.'
    status: met
  - id: AC-4
    title: '**AC-4: Backpressure pattern documented and tested.** A doc at `docs/architecture/backpressure-pattern.md` describes
      the SHIFT-based backpressure approach as a first-class modeling pattern. An integration test demonstrates upstream throttling
      via t-1 queue feedback.'
    status: met
  - id: AC-5
    title: '**AC-5: Tests and gate.** Tests cover: WIP clamping (queue capped at limit), overflow to loss (default), overflow
      to DLQ node, cascading overflow, time-varying wipLimit (series), backpressure via SHIFT. Full test suite green. Determinism
      test updated.'
    status: met
---

## Goal

Add WIP limit modeling to ServiceWithBufferNode so FlowTime can answer: "What happens if we set a WIP limit?" Overflow is configurable — items can be lost, routed to a DLQ, or handled by any downstream node. Also document and test the existing SHIFT-based backpressure pattern.

## Context

Currently, ServiceWithBufferNode queues grow unbounded: `Q[t] = max(0, Q[t-1] + inflow - outflow - loss)`. In real systems, queues have limits — a Kanban board has a WIP cap, a message queue has a max depth, a connection pool has a size limit. When the limit is hit, excess work is either dropped (loss), diverted (DLQ/overflow route), or upstream is throttled (backpressure).

FlowTime already supports:
- `loss` series on ServiceWithBufferNode (items removed from system)
- SHIFT-based backpressure via expressions (t-1 feedback, documented in retry-modeling.md)

What's missing: automatic WIP limit enforcement with configurable overflow routing.

## Acceptance criteria

### AC-1 — **AC-1: WIP limit on ServiceWithBufferNode.** Optional `wipLimit` field (scalar or series reference for time-varying limits). When `Q[t] > wipLimit[t]`: ``` overflow[t] = Q[t] - wipLimit[t] Q[t] = wipLimit[t] ``` The overflow amount is tracked as a series.

**AC-1: WIP limit on ServiceWithBufferNode.** Optional `wipLimit` field (scalar or series reference for time-varying limits). When `Q[t] > wipLimit[t]`:
```
overflow[t] = Q[t] - wipLimit[t]
Q[t] = wipLimit[t]
```
The overflow amount is tracked as a series.

### AC-2 — **AC-2: WIP overflow routing.** Optional `wipOverflow` field on the node definition: - If omitted or `"loss"`: overflow is added to the loss series (items disappear). - If a node ID: the compiler wires the overflow as inflow to the target node. - Cascading: the target can itself have a `wipLimit` and overflow to another target. - The compiler validates no cycles are created by overflow wiring.

**AC-2: WIP overflow routing.** Optional `wipOverflow` field on the node definition:
- If omitted or `"loss"`: overflow is added to the loss series (items disappear).
- If a node ID: the compiler wires the overflow as inflow to the target node.
- Cascading: the target can itself have a `wipLimit` and overflow to another target.
- The compiler validates no cycles are created by overflow wiring.

### AC-3 — **AC-3: Model schema updated.** `model.schema.yaml` and `NodeDefinition` support `wipLimit` (number or string series reference) and `wipOverflow` (string node ID or `"loss"`). Template schema updated if applicable.

### AC-4 — **AC-4: Backpressure pattern documented and tested.** A doc at `docs/architecture/backpressure-pattern.md` describes the SHIFT-based backpressure approach as a first-class modeling pattern. An integration test demonstrates upstream throttling via t-1 queue feedback.

### AC-5 — **AC-5: Tests and gate.** Tests cover: WIP clamping (queue capped at limit), overflow to loss (default), overflow to DLQ node, cascading overflow, time-varying wipLimit (series), backpressure via SHIFT. Full test suite green. Determinism test updated.
## Technical Notes

- **Overflow series architecture:** `ServiceWithBufferNode` returns queue depth (one series per INode contract). The overflow series needs a separate mechanism:
  - **Option A:** Compiler generates a companion `ConstSeriesNode` wired to the overflow data. ServiceWithBufferNode stores overflow in a shared context (e.g., `Dictionary<NodeId, double[]>` passed through evaluation).
  - **Option B:** New `IMultiOutputNode` interface that returns a dictionary of series.
  - Decision during implementation — Option A avoids interface changes.
- **`wipLimit` resolution:** Follows the same pattern as `Parallelism` — can be a literal number or a series reference resolved by `SemanticLoader`.
- **Backpressure vs WIP limits:** WIP limits are "push with overflow" (engine-enforced). Backpressure is "pull with throttling" (model-expressed via SHIFT). Both are valid; document when to use each.

## Out of Scope

- Backpressure as an automatic engine feature (already expressible via SHIFT).
- WIP limit recommendations (DSL epic).
- UI for configuring WIP limits.

## Dependencies

- Phase 1 complete ✅ (Series immutability)
