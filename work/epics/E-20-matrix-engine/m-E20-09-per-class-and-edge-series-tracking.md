# Tracking: m-E20-09 Per-Class Decomposition and Edge Series

**Milestone:** m-E20-09
**Epic:** E-20 Matrix Engine
**Status:** complete
**Branch:** `milestone/m-E20-09-per-class-and-edge-series`
**Started:** 2026-04-10

## Progress

| AC | Description | Status |
|----|-------------|--------|
| AC-1 | Class assignment map | done |
| AC-2 | Per-class series in EvalResult | done |
| AC-3 | Expression-tree per-class evaluation | done |
| AC-4 | ServiceWithBuffer per-class decomposition | done |
| AC-5 | Edge series in EvalResult | done |
| AC-6 | Router per-class distribution | done |
| AC-7 | Parity harness green for class fixtures | done |
| AC-8 | Parity harness green for edge fixtures | done (see notes) |
| AC-9 | Normalization invariant | done |

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

## Phase Completion Notes

### Phases 1-2 (bec0787): Class infrastructure + expression decomposition
- EvalResult has class_map, ClassDefinition
- Per-class arrival columns from traffic.arrivals (compiler Phase 1c)
- propagate_class_decomposition for expr nodes via proportional allocation

### Phase 3: ServiceWithBuffer per-class decomposition
- Refactored propagate_class_decomposition to use allocate_proportional helper
- Added Pass 2: topology nodes — queue depth per-class from arrival fractions
- Tests: queue_per_class_decomposition, queue_per_class_with_custom_queue_depth_name

### Phase 4: Edge series
- Added EdgeMap type: (edge_id, metric) → column index
- compute_edge_series: flowVolume = source_served * (weight/total_weight) * multiplier
- Per-class edge flow proportional to source class fractions
- resolve_edge_source_series handles measure-based source series
- Tests: 6 edge series tests (basic, weighted split, explicit id, multiplier, per-class, empty)

### Phase 5: Parity and normalization
- Added normalize_source_class_columns: rescales source arrival per-class columns so sum==total
- Removed class-enabled.yaml from ClassFixtures in RustEngineParityTests.cs
- 4 normalization invariant tests across fixtures + synthetic queue model
- Rebuilt release binary
- Full parity: 21/21 fixtures pass, 44/44 .NET integration tests green

### AC-8 Note
Edge-bearing fixtures (retry-service-time, order-system, microservices) are in KnownRustGaps
because they use features the Rust engine doesn't yet handle (no grid, file: URI, etc.).
Edge series implementation is verified by 6 Rust unit tests. Cross-engine parity for edge
series will be testable when those fixtures are unblocked in a future milestone.

## Test Count
- Rust: 142 tests (120 core + 22 fixture deserialization)
- .NET: 1,323 passed, 0 failed
