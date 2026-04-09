# Tracking: m-E20-02 Compiler and Core Evaluator

**Status:** in-progress
**Started:** 2026-04-09
**Epic:** [E-20 Matrix Engine](./spec.md)
**Milestone spec:** [m-E20-02-compiler-and-evaluator.md](./m-E20-02-compiler-and-evaluator.md)
**Branch:** `milestone/m-E20-02-compiler-and-evaluator` (off `epic/E-20-matrix-engine`)
**Baseline Rust test count:** 24 passed, 0 failed

## Acceptance Criteria

- [ ] **AC-1.** ColumnMap — bidirectional name ↔ index mapping.
- [ ] **AC-2.** Op enum + evaluator — all element-wise ops, evaluator loop.
- [ ] **AC-3.** Expression compiler — AST → ops with temp columns.
- [ ] **AC-4.** Model compiler — const/expr nodes, topo sort, plan generation.
- [ ] **AC-5.** End-to-end evaluation — hello.yaml produces correct series.
- [ ] **AC-6.** C# parity — pre-computed reference outputs match.
- [ ] **AC-7.** Plan inspection — human-readable plan, CLI `plan` command.

## Commit Plan (tentative)

- [ ] **Status-sync** — branch, flip statuses, create this tracking doc.
- [ ] **Bundle A** — AC-1 + AC-2: ColumnMap, Op enum, evaluator loop.
- [ ] **Bundle B** — AC-3 + AC-4 + AC-5: Expression compiler, model compiler, end-to-end eval.
- [ ] **Bundle C** — AC-6 + AC-7: Parity tests, plan inspection, CLI update.
- [ ] **Wrap** — tracking doc, status reconciliation.

## Implementation Log

_Appended per bundle._

## Test Summary

- **Baseline Rust:** 24 passed, 0 failed
- **Current Rust:** (status-sync only)
- **Build:** green

## Notes

_Decisions made, issues encountered, deviations from spec._

## Completion

- **Completed:** pending
- **Final Rust test count:** pending
- **Deferred items:** (none yet)
