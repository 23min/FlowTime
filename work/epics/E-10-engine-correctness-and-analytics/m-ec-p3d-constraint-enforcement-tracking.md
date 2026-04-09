# Tracking: m-ec-p3d Constraint Enforcement

**Status:** completed (2026-04-09)
**Started:** 2026-04-08
**Completed:** 2026-04-09
**Epic:** [E-10 Engine Correctness & Analytical Primitives](./spec.md)
**Milestone spec:** [m-ec-p3d-constraint-enforcement.md](./m-ec-p3d-constraint-enforcement.md)
**Branch:** `milestone/m-ec-p3d-constraint-enforcement` (off `main`)
**Baseline test count (main HEAD `5d59f06`):** 1246 passed, 9 skipped, 0 failed
**Final test count:** 1254 passed, 9 skipped, 0 failed (+8 new constraint enforcement tests)

## Acceptance Criteria

- [x] **AC-1.** Constraint allocation during evaluation. `ConstraintAwareEvaluator.Evaluate` composes `RouterAwareGraphEvaluator.Evaluate` → `ConstraintAllocator.AllocateProportional` per bin → `Graph.EvaluateWithOverrides` to cap served and re-evaluate downstream. Graph stays pure; constraint layer composes it. (Bundle A, commit `3e21791`)
- [x] **AC-2.** Downstream propagation. Works automatically via `Graph.EvaluateWithOverrides` — the capped served series is an override, and every downstream node re-evaluates correctly through the existing dependency mechanism. No special re-evaluation logic needed. Verified by `DownstreamPropagation_ConsumerSeesConstrainedValues` test. (Bundle A, commit `3e21791`)
- [x] **AC-3.** Constraint metadata in evaluation results. `ConstraintEvaluationResult` includes `Allocations: IReadOnlyDictionary<string, ConstraintAllocation>` with per-node per-bin allocation arrays and per-bin `Limited` bool flags. Allocations computed for ALL bins (not just constrained bins) for metadata completeness. (Bundle B, commit `ca6f371`)
- [x] **AC-4.** Tests and gate. 8 tests in `ConstraintEnforcementTests.cs`: (1) single constraint proportional split, (2) unconstrained pass-through, (3) zero capacity, (4) downstream propagation, (5) multiple constraints on different groups, (6) AC-3 metadata constrained case, (7) AC-3 metadata unconstrained case, (8) constraint-aware determinism (bitwise comparison). Full test suite 1254/9/0 green. (Bundles A+B)

## Commit Plan

Status-sync commit first (this doc + spec approved→in-progress + p3a status drift fix + epic spec table + CLAUDE.md + epic-roadmap.md), then implementation bundles determined by TDD cycle complexity.

- [x] **Status-sync commit** — commit `2ccef08`. Created branch, flipped statuses, fixed pre-existing p3a drift, created this tracking doc. 6 files, +87/−16.
- [x] **Bundle A** (AC-1 + AC-2 + AC-4 first 5 tests) — commit `3e21791`. New `ConstraintAwareEvaluator` (201 lines) + `ConstraintEnforcementTests` (367 lines). 2 files, +568. Tests: 1251 passed, 9 skipped, 0 failed (+5 from baseline).
- [x] **Bundle B** (AC-3 metadata + AC-4 remaining 3 tests) — commit `ca6f371`. Extended `ConstraintEvaluationResult` with `Allocations` property + `ConstraintAllocation` record type. Added metadata and determinism tests. 2 files, +130/−16. Tests: 1254 passed, 9 skipped, 0 failed (+8 from baseline).
- [x] **Wrap** — tracking doc finalization + status reconciliation. (this commit)

**Note: Bundles A and B landed more cleanly than the original tentative plan.** The spec's Technical Notes described two approaches for downstream propagation (option 1: re-evaluate downstream after constraint application; option 2: integrate constraints into the evaluation loop). In practice, the existing `Graph.EvaluateWithOverrides` mechanism IS option 1 — it re-evaluates every node in topological order, using override values where provided. Downstream propagation was automatic, not a separate concern. This collapsed what the original plan called "Bundle A (simple cases)" and "Bundle B (downstream propagation)" into a single Bundle A, and freed Bundle B to focus on AC-3 metadata. No "Bundle C (StateQueryService simplification)" was needed as a separate commit because the metadata is now available for StateQueryService to consume; the actual consumption is a follow-up improvement, not a p3d requirement.

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

- **Baseline (main `5d59f06`):** 1246 passed, 9 skipped, 0 failed
- **After Bundle A:** 1251 passed (+5), 9 skipped, 0 failed
- **After Bundle B (final):** 1254 passed (+8), 9 skipped, 0 failed
- **Build:** green (1 pre-existing xUnit2031 warning in `ClassMetricsAggregatorTests.cs:126`, not introduced)
- **Per-project final counts:**
  - FlowTime.Core.Tests: 277 (+8 from baseline 269)
  - All other projects unchanged from baseline

## Notes

### Design decision: compose via EvaluateWithOverrides, not modify Graph

The spec's Technical Notes described two approaches. At implementation time a third emerged: the existing `Graph.EvaluateWithOverrides(grid, overrides)` mechanism (built for time-travel parameter overrides) IS the natural composition point. The constraint layer evaluates unconstrained → allocates → caps served series as overrides → calls `EvaluateWithOverrides` which automatically re-evaluates every downstream node with the capped values. Graph stays pure. No changes to `Graph.cs`, `Graph.Evaluate`, or `EvaluateWithOverrides`. No changes to `RouterAwareGraphEvaluator`. The constraint layer sits on top and composes them.

### Follow-up: StateQueryService simplification not in scope for p3d

AC-3 says "so StateQueryService CAN expose constraint status without recomputing." The metadata is now available (`ConstraintEvaluationResult.Allocations`). The actual StateQueryService change — dropping its view-time `AllocateConstraintCapacity` call and consuming evaluation-time metadata instead — is a follow-up improvement. It touches the API layer (~5000-line file, different project) and is better scoped as either a p3d follow-up patch or as part of the orchestration wiring that connects `ConstraintAwareEvaluator` to the run pipeline (`RunOrchestrationService` / `RunArtifactWriter`). Currently `ConstraintAwareEvaluator` is callable from tests but not yet wired into the production run pipeline. Wiring it in requires changes to `FlowTime.Generator` — and E-19's shared framing says "Generator is frozen during E-19 scope; Path B extraction belongs to E-18." Since p3d is E-10 (not E-19), Generator changes are not blocked — but the wiring is a separate concern from the Core-level constraint enforcement logic that p3d delivers.

## Completion

- **Completed:** 2026-04-09
- **Final test count:** 1254 passed, 9 skipped, 0 failed
- **Commits on `milestone/m-ec-p3d-constraint-enforcement`:**
  - `2ccef08` — status-sync (approved→in-progress, p3a drift fix, tracking doc)
  - `3e21791` — Bundle A: wire constraint enforcement into evaluation pipeline (AC-1, AC-2, AC-4 tests 1-5)
  - `ca6f371` — Bundle B: emit constraint allocation metadata (AC-3, AC-4 tests 6-8)
  - _(pending wrap commit)_ — tracking doc finalization + status reconciliation
- **Deferred items:**
  - StateQueryService simplification (consume evaluation-time constraint metadata instead of recomputing at view-time) — follow-up improvement, not a p3d requirement
  - Production pipeline wiring (`RunOrchestrationService` / `RunArtifactWriter` calling `ConstraintAwareEvaluator` instead of `RouterAwareGraphEvaluator`) — a Generator/orchestration change, separate from Core-level enforcement
