# Tracking: m-ec-p3a1 — Analytical Projection Hardening

**Started:** 2026-04-03
**Completed:** 2026-04-03
**Branch:** `milestone/m-ec-p3a1` (from `epic/E-10-engine-correctness`)
**Spec:** `work/milestones/m-ec-p3a1-analytical-projection-hardening.md`
**Build:** `dotnet build` green
**Tests:** `dotnet test --nologo` green

## Acceptance Criteria

- [x] AC-1: Current analytical capabilities resolved in Core
- [x] AC-2: Core computes the current analytical derived metrics for state projection
- [x] AC-3: Finite-value safety and metadata honesty for the current analytical payload
- [x] AC-4: Stationarity warning policy is explicit in the runtime path
- [x] AC-5: Current DTO parity is complete for analytical class fields
- [x] AC-6: Remaining purification work is explicitly handed off to E-16
- [x] AC-7: Tests and gate

## Delivery Summary

- Core now owns the current analytical capability/computation surface used by `/state` and `/state_window`.
- The wrapped branch includes logicalType parity for the current state surfaces, honest analytical metadata, explicit stationarity configuration, and by-class DTO parity.
- Full formula-first purification is no longer split across E-10 and E-16. E-16 now owns compiled semantic references, class-truth separation, runtime analytical descriptors, contract redesign, and consumer heuristic deletion.

## Notes

- Wrapped as a bridge milestone. This milestone is complete for its revised scope and hands the remaining purity work to E-16.
