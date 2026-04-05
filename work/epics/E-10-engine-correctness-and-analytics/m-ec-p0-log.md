# Tracking: Phase 0 — Correctness Bugs

**Epic:** Engine Correctness & Analytical Primitives (`work/epics/E-10-engine-correctness-and-analytics/spec.md`)
**Started:** 2026-03-31
**Completed:** 2026-03-31
**Branch:** `milestone/phase-0-bugs` (from `epic/engine-correctness`)

## Acceptance Criteria

- [x] BUG-1: Clone outflow series before dispatch mutation — fix + regression test
- [x] BUG-2: Include CapacitySeriesId in ServiceWithBufferNode.Inputs — fix + regression test
- [x] BUG-3: Make InvariantAnalyzer.ValidateQueue dispatch-aware — fix + regression test
- [x] End-to-end determinism test (bitwise-identical series from two independent compile+evaluate cycles)
- [x] All existing tests still pass (110 total, 0 failures)

## TDD Plan

For each bug:
1. Write a failing test that reproduces the bug
2. Run test → confirm RED
3. Apply minimal fix
4. Run test → confirm GREEN
5. Run full suite → no regressions
