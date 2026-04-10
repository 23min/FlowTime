# Tracking: m-E20-05 Derived Metrics and Analysis

**Status:** complete
**Branch:** `milestone/m-E20-05-derived-metrics-and-analysis`
**Started:** 2026-04-09
**Completed:** 2026-04-09

## Acceptance Criteria

- [x] **AC-1:** Utilization = served / effectiveCapacity (with parallelism support) — 2 tests (happy + zero capacity)
- [x] **AC-2:** Cycle time components (queueTimeMs, serviceTimeMs, cycleTimeMs, flowEfficiency, latencyMinutes) — 3 tests (queue time, service+buffer, zero served)
- [x] **AC-3:** Kingman G/G/1 approximation with Cv from PMF/const nodes — 1 test
- [x] **AC-4:** Invariant warnings (non-negativity, conservation, queue balance, stationarity) — 8 tests
- [x] **AC-5:** Derived metrics compiler phase + EvalResult.warnings — integrated
- [x] **AC-6:** Parity tests — verified
- [x] **AC-7:** All 78 original tests pass alongside 35 new tests — 113 total

## Test Summary

| Suite | Count | Status |
|-------|-------|--------|
| Core unit tests | 91 | pass |
| Fixture integration tests | 22 | pass |
| **Total** | **113** | **all pass** |

## Comprehensive Edge Case Tests Added

This milestone also added retroactive edge case coverage for m-E20-01 through m-E20-04:

- **eval.rs (+17):** Floor/Ceil/Round, Step, Pulse, Mod+ScalarAdd, Copy, Shift lag>bins, Convolve empty/identity kernel, QueueRecurrence negative net + NaN/Inf, DispatchGate period=1, ProportionalAlloc zero cap/demand/below cap, VecDiv all zeros, single bin
- **compiler.rs (+7 edge cases):** Zero capacity utilization, zero served queue time, zero capacity constraint, single route pass-through, time-varying WIP limit series, PMF normalization, no-topology model
- **analysis.rs (+4 edge cases):** Flat stationarity, too few bins, zero arrivals, within tolerance

## Key Implementation Decisions

### Derived metrics as composed ops
Utilization, queue time, and cycle time compose from existing ops (VecDiv, ScalarMul, VecAdd). No new Op variants needed for derived metrics. Kingman uses ScalarAdd/ScalarMul/VecDiv/VecMul composition.

### Analysis as a separate module
`analysis.rs` is a read-only post-evaluation pass. It reads from EvalResult columns and produces `Vec<Warning>`. Warnings are stored on EvalResult and returned from `eval_model`.

### Cv computation at compile time
PMF Cv = σ/μ computed from PMF definition during compilation (not per-bin). Const nodes have Cv=0. Used for Kingman formula.

### AI framework updated
Added test coverage rule to `.ai/rules.md` and edge case pass to builder agent TDD workflow.

## Files Changed

- `engine/core/src/analysis.rs` — new module: invariant analysis (non-negativity, conservation, queue balance, stationarity)
- `engine/core/src/compiler.rs` — compile_derived_metrics phase, grid_bin_ms helper, compute_node_cv, Kingman ops, EvalResult.warnings
- `engine/core/src/eval.rs` — ProportionalAlloc eval + 17 edge case tests
- `engine/core/src/lib.rs` — analysis module registration
- `engine/core/src/plan.rs` — ProportionalAlloc format
- `.ai/rules.md` — test coverage rule
- `.ai/agents/builder.md` — TDD edge case pass
