# Tracking: m-ec-p1 — Engineering Foundation (Phase 1)

**Epic:** Engine Correctness & Analytical Primitives (`work/epics/E-10-engine-correctness-and-analytics/spec.md`)  
**Started:** 2026-04-01  
**Completed:** 2026-04-01  
**Branch:** `epic/engine-correctness`

## Acceptance Criteria

- [x] AC-1: Cache topological order in Graph constructor
- [x] AC-2: Precompute Topology adjacency lists
- [x] AC-3: NaN/Infinity/div-by-zero policy document
- [x] AC-4: NaN policy tests (Tier 1, Tier 2, Tier 3, exception)
- [x] AC-5: Expression edge-case tests (MOD, FLOOR, CEIL, ROUND, STEP with NaN)
- [x] AC-6: Series immutability (no public setter, clone on construct, clone on ToArray)
- [x] AC-7: PCG32 rejection sampling (fix modulo bias)
- [x] AC-8: Console.WriteLine replaced with ILogger
- [x] AC-9: Narrow #pragma warning disable to specific codes
- [x] AC-10: End-to-end determinism gate passes

## Progress Log

### 2026-04-01

- AC-1: Cached topological order in `Graph` constructor; `Evaluate` reuses `cachedTopologicalOrder`
- AC-2: Precomputed adjacency lists in `ComputeTopologicalOrder`; eliminated O(V*E) inner scan
- AC-3: Created `docs/architecture/nan-policy.md` — three-tier policy (0.0 / null / NaN) with location tables, design rationale, comparison chart, and contribution guide
- AC-4: Created `tests/FlowTime.Core.Tests/Safety/NaNPolicyTests.cs` — 15 tests covering Tier 1 (expression div/0, MOD/0, Safe NaN, Safe Infinity), Tier 2 (UtilizationComputer, LatencyComputer), exception (PMF zero-sum)
- AC-5: Added NaN-input tests for FLOOR, CEIL, ROUND, STEP, MOD in NaNPolicyTests
- AC-6: Series class made fully immutable — constructor clones input array, indexer is get-only, ToArray returns copy; tests in `tests/FlowTime.Core.Tests/Execution/SeriesTests.cs`
- AC-7: PCG32.NextInt updated with rejection sampling to eliminate modulo bias for non-power-of-two bounds
- AC-8: Replaced Console.WriteLine calls with structured ILogger usage
- AC-9: Narrowed blanket `#pragma warning disable` to specific warning codes
- AC-10: Determinism test passes — all changes maintain bitwise-reproducible evaluation
