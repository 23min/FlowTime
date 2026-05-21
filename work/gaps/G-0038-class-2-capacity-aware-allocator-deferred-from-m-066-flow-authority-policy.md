---
id: G-0038
title: Class-2 capacity-aware allocator (deferred from M-0066 flow-authority policy)
status: open
discovered_in: M-0066
---

## What's missing

Capacity-aware (pull-style) flow allocation as a first-class engine surface. Specifically:

- **Allocator semantics.** A pull-driven allocation step where downstream consumers' available capacity drives how producer outflow is distributed across outgoing edges, rather than being prescribed by producer-side weights or routing rules.
- **Flow-conservation accounting under capacity limits.** When a consumer cannot accept its share, the conservation equation must account for the spillover (queue, backpressure, drop, or re-route) explicitly — not silently re-balance.
- **Queue / backpressure modeling at consumers.** A representation of consumer capacity bounds and the queueing or backpressure that arises when offered load exceeds capacity, integrated with the time-bin evaluator.
- **Schema for declaring capacity bounds.** Producer-side schema (or node-level schema) for stating capacity limits and the policy that applies when bounds are exceeded (drop, queue with bound, retry, route to overflow).

## Why it matters

M-0066 established the three-class flow-authority policy (see [`docs/architecture/flow-authority-policy.md`](../../docs/architecture/flow-authority-policy.md) and the M-0066 spec). The policy names three places flow can be authoritatively determined:

- **Class 1 — producer-side push routing** via a `router` node. Immediate authoritative surface; covers most fan-out and class-based routing today.
- **Class 2 — capacity-aware (pull) allocation.** Deferred. This gap.
- **Class 3 — static-share edges** (edge-as-channel, fixed-weight outgoing edges). Supplementary structural authority; covers fixed proportional splits.

Class 2 was deferred from M-0066 because:

- Its engineering footprint is the largest of the three (the M-0066 footprint analysis estimated 150–300 LOC across schema, compile, and analyse, plus a semantic asterisk on flow-purity guarantees).
- M-0066's scope was decision + policy + doc-sweep, not allocator implementation.
- Class 1 (push routing) and class 3 (static-weight edges) together cover the immediate authoritative surface for current modeling needs.

Without class 2, FlowTime cannot model:

- Dynamic load-balancing where routing depends on downstream queue length.
- Capacity-bounded consumers with explicit backpressure / spillover semantics.
- Pull-style flow distribution where consumers, not producers, drive allocation.

`docs/reference/flow-theory-coverage.md` records dynamic routing (route by downstream queue length) as **deferred** with a pointer to this gap.

## Carrier work

When a future milestone (likely under E-0025 — Engine Truth Gate — or a successor epic) takes class-2 on, this gap is the entry point. The carrier milestone will need to:

- Decide the allocator semantics (greedy, proportional-fair, priority-based, etc.).
- Extend the schema to declare consumer capacity bounds and overflow policy.
- Update the compile and analyse phases to enforce the new schema and check flow-conservation under capacity.
- Update the invariant analyzer to surface conservation violations the new path can produce.

## Cross-references

- [`work/epics/E-25-engine-truth-gate/M-066-edge-flow-authority-decision.md`](../epics/E-25-engine-truth-gate/M-066-edge-flow-authority-decision.md) — the spike that established the policy and deferred this work.
- [`docs/architecture/flow-authority-policy.md`](../../docs/architecture/flow-authority-policy.md) — the policy doc naming the three classes.
- [`docs/reference/flow-theory-coverage.md`](../../docs/reference/flow-theory-coverage.md) — coverage matrix that points dynamic-routing entries here.
- ADR-NNNN flow-authority policy — forthcoming under M-0066/AC-5.
