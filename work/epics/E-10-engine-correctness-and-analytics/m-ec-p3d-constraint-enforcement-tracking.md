# Tracking: m-ec-p3d Constraint Enforcement

**Status:** in-progress
**Started:** 2026-04-08
**Epic:** [E-10 Engine Correctness & Analytical Primitives](./spec.md)
**Milestone spec:** [m-ec-p3d-constraint-enforcement.md](./m-ec-p3d-constraint-enforcement.md)
**Branch:** `milestone/m-ec-p3d-constraint-enforcement` (off `main`)
**Baseline test count (main HEAD `5d59f06`):** 1246 passed, 9 skipped, 0 failed (per epic/E-19 → main post-merge verification in this session)

## Acceptance Criteria

- [ ] **AC-1.** Constraint allocation during evaluation. After all nodes in the DAG have been evaluated, apply constraint allocation per bin:
  - For each constraint, collect the unconstrained `served[t]` for all assigned nodes.
  - If total demand exceeds constraint `capacity[t]`, allocate proportionally via `ConstraintAllocator.AllocateProportional`.
  - Cap each node's `served[t]` to its allocation.
  - Store the allocation results in the evaluation context for downstream consumption (state/state_window).
- [ ] **AC-2.** Downstream propagation. Capped `served` values propagate correctly through the DAG. Nodes that depend on a constrained node's output see the constrained (reduced) values, not the unconstrained originals. This may require a re-evaluation pass for downstream nodes after constraints are applied.
- [ ] **AC-3.** Constraint metadata in evaluation results. The evaluation result includes per-constraint, per-node, per-bin allocation data so `StateQueryService` can expose constraint status (limited/unlimited) without recomputing.
- [ ] **AC-4.** Tests and gate. Tests cover: single constraint with two nodes (proportional split), unconstrained case (demand ≤ capacity, no capping), constraint with zero capacity (all nodes get zero), downstream propagation (constrained served affects downstream queue), multiple constraints on different node groups. Full test suite green. Determinism test updated.

## Commit Plan

Status-sync commit first (this doc + spec approved→in-progress + p3a status drift fix + epic spec table + CLAUDE.md + epic-roadmap.md), then implementation bundles determined by TDD cycle complexity.

- [ ] **Status-sync commit** — create branch, flip statuses, fix pre-existing p3a drift, create this tracking doc.
- [ ] **Bundle A (tentative)** — AC-4 red tests for the simple cases (unconstrained pass-through, single-constraint proportional split, zero-capacity all-zero), then AC-1 implementation until those tests pass.
- [ ] **Bundle B (tentative)** — AC-2 downstream propagation: the tricky one. Capped `served` must propagate through the DAG. Two design options from the spec's Technical Notes:
  1. **Re-evaluate downstream nodes** after constraint application (simpler, potentially wasteful).
  2. **Integrate constraints into the evaluation loop** — defer final `served` until all nodes in the constraint group are evaluated, then allocate (more efficient, complicates topological traversal).

  Decision deferred to implementation time after profiling the simple case and measuring the blast radius of option 1.
- [ ] **Bundle C (tentative)** — AC-3 metadata emission + `StateQueryService` simplification: drop the view-time constraint filter now that evaluation produces authoritative allocation results.
- [ ] **Wrap** — full test suite, determinism test, tracking doc finalization, status reconciliation across all five surfaces.

## Preserved Surfaces (Must Not Regress)

- `ConstraintAllocator.AllocateProportional` — the allocation algorithm itself is correct (per M-10.01). Do not touch the math.
- Determinism: every code change must maintain the end-to-end determinism invariant (same template + seed → bitwise-identical artifacts). Re-run determinism tests after each bundle.
- `Graph.EvaluateWithOverrides` — used by time-travel and parameter-override flows. Constraint logic must not break override semantics.
- `RouterAwareGraphEvaluator` — the router convergence loop composes Graph.Evaluate. Constraint enforcement must not break router iteration.

## Implementation Log

### Status-sync — 2026-04-08

Branch `milestone/m-ec-p3d-constraint-enforcement` created off `main` HEAD (`5d59f06`). Status flipped across 5 surfaces in one pass:

- **This spec** (`m-ec-p3d-constraint-enforcement.md`): `approved` → `in-progress`; branch line added.
- **This tracking doc** (new file).
- **Epic spec milestone table** (`spec.md`): m-ec-p3d `approved` → `in-progress`. **Pre-existing drift fixed in the same pass:** m-ec-p3a was showing `approved` in the table even though CLAUDE.md correctly said "m-ec-p3a merged to main" and the milestone was actually complete. Flipped m-ec-p3a to `complete` in the table. Also flipped `m-ec-p3a-cycle-time.md` spec header from `approved` to `complete` for consistency.
- **CLAUDE.md Current Work**: Immediate next step pointer updated to p3d; E-10 section updated to "Phase 3 in progress (p3d)"; E-19 block updated from "epic→main merge pending" to "completed and merged to main (2026-04-08)" (was already stale from the earlier E-19 merge in this session).
- **epic-roadmap.md**: E-10 status line updated to reflect p3d in progress.
- **ROADMAP.md**: no change — the Phase 3 section correctly describes the resume sequence without per-milestone status.

Baseline test count carried forward from the epic/E-19 → main merge sanity check: 1246 passed, 9 skipped, 0 failed. Build green (1 pre-existing xUnit2031 warning in `ClassMetricsAggregatorTests.cs:126`, not introduced by any recent milestone).

## Test Summary

- **Baseline:** 1246 passed, 9 skipped, 0 failed
- **Current:** (status-sync only — no code changes yet)
- **Build:** green

## Notes

_Decisions made, issues encountered, deviations from spec — appended per bundle._

## Completion

- **Completed:** pending
- **Final test count:** pending
- **Deferred items:** (none yet)
