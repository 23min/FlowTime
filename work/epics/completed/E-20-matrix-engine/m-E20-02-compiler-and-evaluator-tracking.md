# Tracking: m-E20-02 Compiler and Core Evaluator

**Status:** completed
**Started:** 2026-04-09
**Epic:** [E-20 Matrix Engine](./spec.md)
**Milestone spec:** [m-E20-02-compiler-and-evaluator.md](./m-E20-02-compiler-and-evaluator.md)
**Branch:** `milestone/m-E20-02-compiler-and-evaluator` (off `epic/E-20-matrix-engine`)
**Baseline Rust test count:** 24 passed, 0 failed

## Acceptance Criteria

- [x] **AC-1.** ColumnMap — bidirectional name ↔ index mapping.
- [x] **AC-2.** Op enum + evaluator — 16 op variants, evaluator loop.
- [x] **AC-3.** Expression compiler — AST → ops with temp columns.
- [x] **AC-4.** Model compiler — const/expr/pmf nodes, topo sort (Kahn's), cycle detection.
- [x] **AC-5.** End-to-end evaluation — hello.yaml: demand=[10...], served=[8...].
- [x] **AC-6.** C# parity — const, scalar mul, chained exprs, MIN/MAX/CLAMP, PMF expected value, cycle detection.
- [x] **AC-7.** Plan inspection — `plan.format()` prints columns + ops. CLI `plan` and `eval` commands.

## Commit Plan

- [x] **Status-sync** — branch, flip statuses, create this tracking doc.
- [x] **Implementation** — all ACs in single commit (naturally coupled pipeline).
- [x] **Wrap** — tracking doc, status reconciliation.

## Implementation Log

### Implementation — 2026-04-09

All ACs delivered in a single commit:

- **plan.rs** (~220 LOC): ColumnMap (insert, get, alloc_temp, get_or_insert, iter), Op enum (16 variants), Plan struct with format().
- **eval.rs** (~240 LOC): `evaluate()` → allocate matrix, iterate ops, execute each. `get()`/`set()` for row-major indexing. `extract_column()` for series extraction. Element-wise ops: Add/Sub/Mul/Div/Min/Max/Clamp/Mod/ScalarAdd/ScalarMul/Floor/Ceil/Round/Step/Pulse/Copy.
- **compiler.rs** (~500 LOC): `compile()` → assign columns, topo sort, emit ops. `eval_model()` → compile + evaluate + wrap as EvalResult. Expression compiler: recursive `emit_expr()` with temp columns for intermediates. PMF: expected value as constant. Topo sort: Kahn's algorithm with cycle detection.
- **CLI**: `parse`, `plan`, `eval` commands. Plan prints column map + op list. Eval prints named series.

## Test Summary

- **Baseline Rust:** 24 passed, 0 failed
- **Final Rust:** 38 passed, 0 failed (+14 new)
- **Build:** green

## Completion

- **Completed:** 2026-04-09
- **Final Rust test count:** 38 passed, 0 failed
- **Deferred items:** (none)
