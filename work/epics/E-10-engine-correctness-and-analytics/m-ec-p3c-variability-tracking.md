# Tracking: m-ec-p3c Variability (Cv + Kingman)

**Status:** completed (2026-04-09)
**Started:** 2026-04-09
**Completed:** 2026-04-09
**Epic:** [E-10 Engine Correctness & Analytical Primitives](./spec.md)
**Milestone spec:** [m-ec-p3c-variability.md](./m-ec-p3c-variability.md)
**Branch:** `milestone/m-ec-p3c-variability` (off `main`)
**Baseline test count (main HEAD `3a53a5f`):** 1254 passed, 9 skipped, 0 failed
**Final test count:** 1274 passed, 9 skipped, 0 failed (+20 new tests)

## Acceptance Criteria

- [x] **AC-1.** Cv computed from two sources: (a) `Pmf.Variance`, `Pmf.StandardDeviation`, `Pmf.CoefficientOfVariation` computed from the distribution shape in the constructor alongside `ExpectedValue`; (b) `CvCalculator.ComputeSampleCv(double[])` for observed series using population statistics. Constant/empty/single-element/zero-mean series all return Cv=0. (Bundle A, commit `ea5ad79`)
- [x] **AC-2.** `CvSource` enum (`Pmf`, `Observed`, `Constant`) + `CvMetadata` record (`CoefficientOfVariation: double[], Source: CvSource`) created in `FlowTime.Core`. (Bundle B, commit `6580a5e`)
- [x] **AC-3.** `KingmanApproximation.Compute(utilization, cvArrivals, cvService, meanServiceTimeMs)` returns `E[Wq] = (ρ/(1-ρ)) × ((Ca² + Cs²)/2) × E[S]` in milliseconds, or null when ρ ≥ 1.0, ρ ≤ 0, Cv < 0, or E[S] ≤ 0. (Bundle B, commit `6580a5e`)
- [x] **AC-4.** 20 tests across two test files:
  - `CvComputationTests.cs` (9 tests): deterministic PMF (Cv=0), symmetric two-value (Cv=1.0), known four-value distribution (Cv≈0.41), zero expected value (Cv=0), constant series (Cv=0), known series (Cv=0.4), empty series (Cv=0), single element (Cv=0), zero mean (Cv=0).
  - `KingmanApproximationTests.cs` (11 tests): known inputs (25.0ms), deterministic both (wait=0), M/M/1 exact case, ρ ≥ 1.0 null (3 cases), ρ ≤ 0 null (2 cases), negative Cv null, zero service time null, high variability arrivals.
  - Full test suite green: 1274/9/0. (Bundles A+B)

## Commit Plan

- [x] **Status-sync** — commit `3637bdb`. Branch, flip statuses, tracking doc. 5 files, +53/−7.
- [x] **Bundle A** (AC-1) — commit `ea5ad79`. `Pmf.Variance`/`StandardDeviation`/`CoefficientOfVariation` + `CvCalculator.ComputeSampleCv`. 3 files, +209. Tests: 1263/9/0 (+9 from baseline).
- [x] **Bundle B** (AC-2 + AC-3 + AC-4 completion) — commit `6580a5e`. `CvSource` enum + `CvMetadata` record + `KingmanApproximation.Compute`. 3 files, +219. Tests: 1274/9/0 (+20 from baseline).
- [x] **Wrap** — tracking doc finalization + status reconciliation. (this commit)

## Implementation Log

### Status-sync — 2026-04-09

Branch `milestone/m-ec-p3c-variability` created off `main` HEAD (`3a53a5f`). Status flipped: spec approved→in-progress, epic spec table, CLAUDE.md, epic-roadmap.md.

### Bundle A — 2026-04-09

Added `Variance`, `StandardDeviation`, `CoefficientOfVariation` to the `Pmf` class. Computed in both constructors alongside `ExpectedValue` using:
- `Var[X] = Σ(pᵢ × (vᵢ - μ)²)` (population variance from the distribution)
- `Cv = σ / |μ|` (0 when μ = 0)

Created `CvCalculator.ComputeSampleCv(double[])` in `FlowTime.Core` namespace for observed series, using population variance (consistent with the PMF definition). Returns 0 for degenerate inputs.

### Bundle B — 2026-04-09

Created `CvSource` enum and `CvMetadata` record (AC-2 types). These are available for future wiring into the evaluation context — the types exist in Core and are ready for consumption by the analytical evaluator or StateQueryService.

Created `KingmanApproximation.Compute` (AC-3 formula). Pure static method, no side effects, returns `double?`. Guards against all degenerate inputs (ρ ≥ 1.0, ρ ≤ 0, negative Cv, zero service time, NaN/infinity). The M/M/1 exact case test confirms the formula reduces to the known closed-form for exponential arrivals and service.

## Test Summary

- **Baseline (main `3a53a5f`):** 1254 passed, 9 skipped, 0 failed
- **After Bundle A:** 1263 passed (+9), 9 skipped, 0 failed
- **After Bundle B (final):** 1274 passed (+20), 9 skipped, 0 failed
- **Build:** green (1 pre-existing xUnit2031 warning, not introduced)
- **Per-project:** FlowTime.Core.Tests: 297 (+20 from baseline 277). All others unchanged.

## Notes

### Follow-up: wiring Cv and Kingman into the evaluation context and state responses

Like p3d's constraint enforcement, the Core primitives (Cv computation, Kingman formula) are now available as tested library functions but are not yet wired into the production evaluation pipeline or the `StateQueryService` response shape. The wiring requires:

1. A `CvComputer` layer that walks the evaluation result + graph nodes, determines each node's Cv source (PMF vs observed vs constant), and produces `IReadOnlyDictionary<NodeId, CvMetadata>`.
2. Integration into `ConstraintAwareEvaluator` (or a new `AnalyticalEvaluator` that composes constraint enforcement + Cv computation).
3. `StateQueryService` consuming the Cv metadata to expose `kingmanPredictedWaitMs` in state responses for ServiceWithBuffer nodes.

This wiring is conceptually similar to p3d's "StateQueryService simplification" — it moves analytical computation from the view layer into the Core evaluation pipeline. It can land as a follow-up milestone or as part of the orchestration wiring that also connects `ConstraintAwareEvaluator`.

## Completion

- **Completed:** 2026-04-09
- **Final test count:** 1274 passed, 9 skipped, 0 failed
- **Commits on `milestone/m-ec-p3c-variability`:**
  - `3637bdb` — status-sync
  - `ea5ad79` — Bundle A: Cv computation (AC-1)
  - `6580a5e` — Bundle B: Kingman + CvMetadata types (AC-2, AC-3, AC-4)
  - _(pending wrap commit)_ — tracking doc + status reconciliation
- **Deferred items:**
  - Wiring Cv/Kingman into evaluation context + StateQueryService response shape (follow-up, same pattern as p3d constraint metadata)
  - Sliding window Cv computation (spec AC-1 mentions "configurable sliding window" — current implementation uses whole-series; window support deferred to when telemetry replay use case needs it)
