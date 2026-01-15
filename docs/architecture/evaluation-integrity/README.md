# Evaluation Integrity (DAG/Spreadsheet Contract)

## Why this epic exists

FlowTime models are a spreadsheet: each series is a cell, formulas depend on upstream cells, and evaluation must follow a strict DAG order. Once a cell is computed, it must never be mutated without a full recompute of dependents. This contract is foundational to trust in the engine.

Recent class propagation fixes exposed a gap: some class series were injected after the topological pass, creating a post-evaluation mutation that could leave downstream expressions stale. This epic eliminates that category of risk and makes the evaluation contract explicit, enforced, and testable.

## Scope

- Define and document the evaluation contract (DAG order, no post-eval mutation).
- Centralize overrides into a single pre-eval pipeline.
- Enforce guardrails (fail fast on late injection, or mandate full recompute).
- Add invariant diagnostics and tests that lock the contract.

## Non-goals

- UI redesign or visualization changes.
- New routing logic unrelated to evaluation order.
- Performance optimization that changes semantics.

## Milestones

- `docs/milestones/FT-M-06.01-evaluation-integrity-dag-contract.md` — Evaluation integrity contract, guardrails, and tests.

## References

- `docs/architecture/engine-semantics-layer/README.md`
- `docs/architecture/time-travel/`
