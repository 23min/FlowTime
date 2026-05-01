---
id: M-054
title: Engineering Foundation (Phase 1)
status: done
parent: E-10
---

## Goal

Harden engineering quality across the FlowTime engine: cache topological order, define and enforce the NaN/Infinity/division-by-zero policy, make Series fully immutable, fix PCG32 modulo bias, and clean up code quality issues.

## Acceptance Criteria

- [x] AC-1: Cache topological order in Graph constructor (compute once, reuse on every Evaluate call)
- [x] AC-2: Precompute Topology adjacency lists (eliminate O(V*E) scan in topo sort)
- [x] AC-3: NaN/Infinity/div-by-zero policy document (`docs/architecture/nan-policy.md`) covering all division sites
- [x] AC-4: NaN policy tests — Tier 1 (return 0.0), Tier 2 (return null), Tier 3 (NaN sentinel), exception (PMF throws)
- [x] AC-5: Expression edge-case tests — MOD, FLOOR, CEIL, ROUND, STEP with NaN inputs
- [x] AC-6: Series immutability — remove public indexer setter, constructor clones input array, ToArray returns copy
- [x] AC-7: PCG32 rejection sampling — fix modulo bias in NextInt(max) for non-power-of-two bounds
- [x] AC-8: Replace Console.WriteLine with ILogger in engine code
- [x] AC-9: Narrow #pragma warning disable to specific warning codes (no blanket suppression)
- [x] AC-10: End-to-end determinism gate — existing determinism test still passes after all changes

## Out of Scope

- Router convergence guard (max-iteration limit) — deferred; needs design for backpressure semantics
- Parallelism typing (replace `double` parallelism with typed Parallelism struct) — deferred; broad refactor
- CUE (model schema language) — Phase 2 scope
- Documentation honesty (expression docs, constraint claims) — Phase 2 scope
- Analytical primitives (bottleneck, cycle time, WIP limits) — Phase 3 scope

## Dependencies

- Phase 0 (correctness bugs) must be complete — merged to `epic/engine-correctness`
