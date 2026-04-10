# Tracking: m-E20-09 Per-Class Decomposition and Edge Series

**Milestone:** m-E20-09
**Epic:** E-20 Matrix Engine
**Status:** in-progress
**Branch:** `milestone/m-E20-09-per-class-and-edge-series`
**Started:** 2026-04-10

## Progress

| AC | Description | Status |
|----|-------------|--------|
| AC-1 | Class assignment map | pending |
| AC-2 | Per-class series in EvalResult | pending |
| AC-3 | Expression-tree per-class evaluation | pending |
| AC-4 | ServiceWithBuffer per-class decomposition | pending |
| AC-5 | Edge series in EvalResult | pending |
| AC-6 | Router per-class distribution | pending |
| AC-7 | Parity harness green for class fixtures | pending |
| AC-8 | Parity harness green for edge fixtures | pending |
| AC-9 | Normalization invariant | pending |

## Design Decision: Option B (classes as plan ops)

The Rust compiler already creates per-class columns for router inputs (`{source}__class_{classId}`)
with `Op::Const` values from traffic arrivals. This is naturally Option B from the spec — classes
as columns in the plan, evaluated in the bin-major loop. We extend this approach:

1. **Compiler emits per-class columns** for all nodes where class decomposition is needed
2. **Proportional decomposition** for expr nodes: per-class = total * (class_arrivals / total_arrivals)
3. **Queue nodes** get per-class queue depth via proportional allocation of arrival fractions
4. **EvalResult** carries a class map: `(node_id, class_id) → column_index`
5. **Writer** exposes per-class series alongside total series

This avoids the C# approach of a 4-pass post-evaluation algorithm. The matrix architecture
makes it natural to have per-class columns evaluated in the same bin-major loop.

## Implementation Phases

### Phase 1: Class infrastructure (AC-1, AC-2)
- Add `ClassMap` to EvalResult: maps (node_id, class_id) → column index
- Build class assignment map from traffic.arrivals
- Expose existing `__class_` columns in EvalResult class map
- Rust tests for class map construction

### Phase 2: Per-class decomposition for expressions (AC-3, AC-6)
- For expr nodes that depend on class-aware sources, emit per-class columns
- Proportional allocation: class_fraction = class_arrivals / total_arrivals at source
- Router per-class distribution already works; verify and test
- Normalization test: sum of per-class == total

### Phase 3: ServiceWithBuffer per-class (AC-4)
- Queue nodes: per-class arrivals, per-class served (proportional to arrival fraction)
- Per-class queue depth = proportional allocation based on arrival class fractions
- Rust tests for queue class decomposition

### Phase 4: Edge series (AC-5)
- Add edge series to EvalResult
- For each topology edge: compute flowVolume from source→target series
- Per-class edge flow: proportional to class fractions at source
- Rust tests for edge series

### Phase 5: Parity and normalization (AC-7, AC-8, AC-9)
- Update parity harness: remove class fixtures from skip list
- Verify class-enabled, router-class, router-mixed pass parity
- Verify edge-bearing fixtures pass with edge comparison
- Add normalization invariant test
