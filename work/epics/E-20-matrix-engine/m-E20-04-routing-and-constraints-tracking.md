# Tracking: m-E20-04 Routing and Constraints

**Status:** complete
**Branch:** `milestone/m-E20-04-routing-and-constraints`
**Started:** 2026-04-09
**Completed:** 2026-04-09

## Acceptance Criteria

- [x] **AC-1:** Router weight-based splitting (ScalarMul + VecAdd, default weight 1.0, accumulate same target) — 3 compiler tests
- [x] **AC-2:** Router class-based routing (per-class columns from traffic.arrivals, mixed class+weight) — 2 compiler tests
- [x] **AC-3:** ProportionalAlloc op (N demands + 1 capacity → N capped outputs, per-bin conditional) — 2 compiler tests
- [x] **AC-4:** Router → Constraint evaluation order (constraints inserted before QueueRecurrence) — verified by router+constraint combined test
- [x] **AC-5:** 6 parity fixtures (weight router, class router, mixed, proportional constraint, below-capacity, combined) — 6 fixture eval tests
- [x] **AC-6:** All 65 original tests pass alongside 13 new tests — 78 total

## Test Summary

| Suite | Count | Status |
|-------|-------|--------|
| Core unit tests | 56 | pass |
| Fixture integration tests | 22 | pass |
| **Total** | **78** | **all pass** |

## Key Implementation Decisions

### No new Op for routing
Router weight splitting decomposes to existing ScalarMul + VecAdd + Copy ops. Class routing uses per-class Const columns + Copy. The router abstraction lives in the compiler, not the evaluator.

### ProportionalAlloc as a new Op
Constraint allocation genuinely needs a new op — it reads N+1 columns and writes N columns with per-bin conditional logic. Implemented in the bin-major evaluator's `execute_op_at_bin`.

### Constraint insertion before QueueRecurrence
`compile_constraints` scans the ops list for QueueRecurrence ops that consume constrained arrivals, inserts ProportionalAlloc before the earliest one, then patches the QueueRecurrence inflow to read from the capped column.

### Per-class column naming
Class columns use `{sourceNodeId}__class_{classId}` naming (double underscore separator). Created as Const ops from `traffic.arrivals[].pattern.ratePerBin`.

## Files Changed

- `engine/core/src/plan.rs` — ProportionalAlloc op variant + format support
- `engine/core/src/eval.rs` — ProportionalAlloc evaluation logic
- `engine/core/src/compiler.rs` — `compile_router` (weight + class), `compile_constraints`, router kind in Phase 3
- `engine/core/tests/fixture_deserialization.rs` — 6 routing/constraint eval tests
- `engine/fixtures/router-*.yaml` — 3 router fixture files
- `engine/fixtures/constraint-*.yaml` — 2 constraint fixture files
- `engine/fixtures/router-with-constraint.yaml` — combined fixture
