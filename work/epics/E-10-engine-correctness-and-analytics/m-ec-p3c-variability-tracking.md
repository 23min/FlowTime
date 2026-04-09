# Tracking: m-ec-p3c Variability (Cv + Kingman)

**Status:** in-progress
**Started:** 2026-04-09
**Epic:** [E-10 Engine Correctness & Analytical Primitives](./spec.md)
**Milestone spec:** [m-ec-p3c-variability.md](./m-ec-p3c-variability.md)
**Branch:** `milestone/m-ec-p3c-variability` (off `main`)
**Baseline test count (main HEAD `3a53a5f`):** 1254 passed, 9 skipped, 0 failed

## Acceptance Criteria

- [ ] **AC-1.** Cv computed from two sources: (a) PMF compilation — compute σ and Cv = σ/μ from the PMF distribution shape alongside E[X]; (b) observed series statistics — compute sample Cv from non-PMF series values over a configurable sliding window. Constant series produce Cv = 0.
- [ ] **AC-2.** Cv accessible in evaluation context. `CvMetadata` record wraps `{ CoefficientOfVariation: double[], Source: Pmf | Observed | Constant }`. Source tag distinguishes PMF-derived (exact, per-bin) from observed (sample statistic) and constant (zero).
- [ ] **AC-3.** Kingman's approximation per ServiceWithBuffer node. Compute `E[Wq] ≈ (ρ/(1-ρ)) × ((Ca² + Cs²)/2) × E[S]` per bin where Cv data is available for both arrivals and service. Exposed as `kingmanPredictedWaitMs`. Null when inputs unavailable or ρ ≥ 1.0.
- [ ] **AC-4.** Tests: Cv from known PMFs (Cv=0 deterministic, Cv=1 exponential), Cv from observed series (known sample statistics), Cv source tagging, Kingman with known inputs (PMF and observed), graceful null for missing inputs and ρ ≥ 1.0. Full test suite green.

## Commit Plan (tentative)

- [ ] **Status-sync commit** — branch, flip statuses, create this tracking doc.
- [ ] **Bundle A** — AC-1 + AC-2: Cv computation from PMFs and observed series, CvMetadata type, wired into evaluation context. TDD: red tests for known PMF Cv values → implement → green.
- [ ] **Bundle B** — AC-3 + AC-4 completion: Kingman's approximation per node, remaining tests.
- [ ] **Wrap** — tracking doc, status reconciliation.

## Implementation Log

### Status-sync — 2026-04-09

Branch `milestone/m-ec-p3c-variability` created off `main` HEAD (`3a53a5f`). Status flipped: spec approved→in-progress, epic spec table, CLAUDE.md, epic-roadmap.md.

## Test Summary

- **Baseline:** 1254 passed, 9 skipped, 0 failed
- **Current:** (status-sync only — no code changes yet)
- **Build:** green

## Notes

_Decisions made, issues encountered, deviations from spec — appended per bundle._

## Completion

- **Completed:** pending
- **Final test count:** pending
- **Deferred items:** (none yet)
