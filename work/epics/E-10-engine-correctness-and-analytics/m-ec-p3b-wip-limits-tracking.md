# Tracking: m-ec-p3b WIP Limits

**Status:** in-progress
**Started:** 2026-04-09
**Epic:** [E-10 Engine Correctness & Analytical Primitives](./spec.md)
**Milestone spec:** [m-ec-p3b-wip-limits.md](./m-ec-p3b-wip-limits.md)
**Branch:** `milestone/m-ec-p3b-wip-limits` (off `main`)
**Baseline test count (main HEAD `6cc3209`):** 1274 passed, 9 skipped, 0 failed

## Acceptance Criteria

- [ ] **AC-1.** WIP limit on ServiceWithBufferNode. Optional `wipLimit` (scalar or series). When `Q[t] > wipLimit[t]`, overflow = Q[t] - wipLimit[t], Q[t] = wipLimit[t]. Overflow tracked as a series.
- [ ] **AC-2.** WIP overflow routing. Optional `wipOverflow` field: `"loss"` (default, overflow added to loss series) or a node ID (compiler wires overflow as inflow to target). Cascading supported. Compiler validates no cycles.
- [ ] **AC-3.** Model schema updated. `NodeDefinition` supports `wipLimit` and `wipOverflow`. Schema docs updated.
- [ ] **AC-4.** Backpressure pattern documented and tested. `docs/architecture/backpressure-pattern.md` describes SHIFT-based backpressure. Integration test demonstrates upstream throttling via t-1 queue feedback.
- [ ] **AC-5.** Tests: WIP clamping, overflow to loss, overflow to DLQ node, cascading overflow, time-varying wipLimit (series), backpressure via SHIFT. Full test suite green. Determinism test updated.

## Commit Plan (tentative)

- [ ] **Status-sync** — branch, flip statuses, create this tracking doc.
- [ ] **Bundle A** — AC-1 + AC-5 (WIP clamping tests): Extend ServiceWithBufferNode with wipLimit. TDD: write tests for clamping and overflow-to-loss → implement → green.
- [ ] **Bundle B** — AC-2 + AC-3: Overflow routing to a target node. ModelCompiler wires overflow. Schema updated. Cycle validation.
- [ ] **Bundle C** — AC-4: Backpressure pattern doc + integration test.
- [ ] **Wrap** — tracking doc, status reconciliation. E-10 epic completion assessment.

## Implementation Log

### Status-sync — 2026-04-09

Branch `milestone/m-ec-p3b-wip-limits` created off `main` HEAD (`6cc3209`). Status flipped: spec approved→in-progress, epic spec table, CLAUDE.md, epic-roadmap.md.

## Test Summary

- **Baseline:** 1274 passed, 9 skipped, 0 failed
- **Current:** (status-sync only)
- **Build:** green

## Notes

_Decisions made, issues encountered, deviations from spec — appended per bundle._

## Completion

- **Completed:** pending
- **Final test count:** pending
- **Deferred items:** (none yet)
