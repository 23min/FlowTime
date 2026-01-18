# Evaluation Integrity (DAG/Spreadsheet Contract)

## Why this epic exists

FlowTime models are a spreadsheet: each series is a cell, formulas depend on upstream cells, and evaluation must follow a strict DAG order. Once a cell is computed, it must never be mutated without a full recompute of dependents. This contract is foundational to trust in the engine.

Recent class propagation fixes exposed a gap: some class series were injected after the topological pass, creating a post-evaluation mutation that could leave downstream expressions stale. This epic eliminates that category of risk and makes the evaluation contract explicit, enforced, and testable.

## Scope

- Define and document the evaluation contract (DAG order, no post-eval mutation).
- Centralize overrides into a single pre-eval pipeline.
- Enforce guardrails (fail fast on late injection, or mandate full recompute).
- Add invariant diagnostics and tests that lock the contract.

## Current Evaluation Pipeline (As-Is)

Today the engine relies on a mix of evaluation passes and post-eval injections:

- **Router totals override:** totals are evaluated once, router overrides are computed, then the graph is re-evaluated with overrides.
- **Class contributions:** class series are evaluated in DAG order, then router/serviceWithBuffer/topology contributions are injected and partially recomputed.
- **Artifact-time injections:** queue depth and retry echo series can be injected into the evaluated context, and external class series overrides can be applied.

These patterns work, but they blur the contract and allow post-eval mutation without explicit recompute guarantees.

## Target Contract

The evaluation contract for this epic is:

- **Single pipeline:** all overrides are applied before evaluation (or force a full recompute).
- **No post-eval mutation:** once a series is computed, it cannot be overwritten without an explicit recompute path.
- **Class parity:** class series must follow the same DAG order and override rules as totals.
- **Override transparency:** any override source is visible, testable, and enforced by guardrails.

## Compile-to-DAG (Internal Expansion)

We can keep the **user-facing node model** intact while making evaluation pure by adding a compile step:

- **UI/UX contract stays node-based:** users still reason about services, routers, and serviceWithBuffer nodes.
- **Engine compiles nodes into an internal DAG:** complex node behaviors expand into internal series (queue depth, router splits, retry echo, etc.).
- **Evaluation stays pure:** the compiled DAG is evaluated topologically; no post-eval mutation is allowed.
- **Internal nodes are not surfaced:** the expansion is engine-internal and never leaks into UI or templates.

**Purity scope:** M-06.01 covers the current override/injection behaviors (queue depth, retry echo, class overrides). Future node types may add additional internal expansions, but they must flow through the same compile-to-DAG contract.

This gives us a strict DAG evaluation contract without fragmenting the user model.

## Override Sources to Normalize

The following override or injection paths must be pulled into the pre-eval pipeline (or require full recompute):

- Router routing overrides
- ServiceWithBuffer outflow/loss contributions
- Topology semantics served/errors contributions
- Queue depth precompute (SHIFT-based semantics)
- Retry echo precompute (kernel-based retries)
- External class series overrides

## Non-goals

- UI redesign or visualization changes.
- New routing logic unrelated to evaluation order.
- Performance optimization that changes semantics.

## Milestones

- `docs/milestones/M-06.01-evaluation-integrity-dag-contract.md` — Evaluation integrity contract, guardrails, and tests.

## References

- `docs/architecture/engine-semantics-layer/README.md`
- `docs/architecture/time-travel/`
