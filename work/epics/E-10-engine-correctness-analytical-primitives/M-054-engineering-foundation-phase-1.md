---
id: M-054
title: Engineering Foundation (Phase 1)
status: done
parent: E-10
acs:
  - id: AC-1
    title: 'AC-1: Cache topological order in Graph constructor (compute once, reuse on every Evaluate call)'
    status: met
  - id: AC-2
    title: 'AC-2: Precompute Topology adjacency lists (eliminate O(V*E) scan in topo sort)'
    status: met
  - id: AC-3
    title: 'AC-3: NaN/Infinity/div-by-zero policy document (`docs/architecture/nan-policy.md`) covering all division sites'
    status: met
  - id: AC-4
    title: 'AC-4: NaN policy tests — Tier 1 (return 0.0), Tier 2 (return null), Tier 3 (NaN sentinel), exception (PMF throws)'
    status: met
  - id: AC-5
    title: 'AC-5: Expression edge-case tests — MOD, FLOOR, CEIL, ROUND, STEP with NaN inputs'
    status: met
  - id: AC-6
    title: 'AC-6: Series immutability — remove public indexer setter, constructor clones input array, ToArray returns copy'
    status: met
  - id: AC-7
    title: 'AC-7: PCG32 rejection sampling — fix modulo bias in NextInt(max) for non-power-of-two bounds'
    status: met
  - id: AC-8
    title: 'AC-8: Replace Console.WriteLine with ILogger in engine code'
    status: met
  - id: AC-9
    title: 'AC-9: Narrow #pragma warning disable to specific warning codes (no blanket suppression)'
    status: met
  - id: AC-10
    title: 'AC-10: End-to-end determinism gate — existing determinism test still passes after all changes'
    status: met
---

## Goal

Harden engineering quality across the FlowTime engine: cache topological order, define and enforce the NaN/Infinity/division-by-zero policy, make Series fully immutable, fix PCG32 modulo bias, and clean up code quality issues.

## Acceptance criteria

### AC-1 — AC-1: Cache topological order in Graph constructor (compute once, reuse on every Evaluate call)

### AC-2 — AC-2: Precompute Topology adjacency lists (eliminate O(V*E) scan in topo sort)

### AC-3 — AC-3: NaN/Infinity/div-by-zero policy document (`docs/architecture/nan-policy.md`) covering all division sites

### AC-4 — AC-4: NaN policy tests — Tier 1 (return 0.0), Tier 2 (return null), Tier 3 (NaN sentinel), exception (PMF throws)

### AC-5 — AC-5: Expression edge-case tests — MOD, FLOOR, CEIL, ROUND, STEP with NaN inputs

### AC-6 — AC-6: Series immutability — remove public indexer setter, constructor clones input array, ToArray returns copy

### AC-7 — AC-7: PCG32 rejection sampling — fix modulo bias in NextInt(max) for non-power-of-two bounds

### AC-8 — AC-8: Replace Console.WriteLine with ILogger in engine code

### AC-9 — AC-9: Narrow #pragma warning disable to specific warning codes (no blanket suppression)

### AC-10 — AC-10: End-to-end determinism gate — existing determinism test still passes after all changes
## Out of Scope

- Router convergence guard (max-iteration limit) — deferred; needs design for backpressure semantics
- Parallelism typing (replace `double` parallelism with typed Parallelism struct) — deferred; broad refactor
- CUE (model schema language) — Phase 2 scope
- Documentation honesty (expression docs, constraint claims) — Phase 2 scope
- Analytical primitives (bottleneck, cycle time, WIP limits) — Phase 3 scope

## Dependencies

- Phase 0 (correctness bugs) must be complete — merged to `epic/engine-correctness`
