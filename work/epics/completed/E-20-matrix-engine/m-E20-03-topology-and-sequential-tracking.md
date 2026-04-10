# Tracking: m-E20-03 Topology and Sequential Ops

**Status:** complete
**Branch:** `milestone/m-E20-03-topology-and-sequential`
**Started:** 2026-04-09
**Completed:** 2026-04-09

## Acceptance Criteria

- [x] **AC-1:** Sequential op variants (Shift, Convolve, QueueRecurrence, DispatchGate) — 7 eval unit tests
- [x] **AC-2:** Topology synthesis (serviceWithBuffer/queue/dlq → QueueRecurrence, retryEcho → Convolve, dispatch → DispatchGate) — 7 compiler tests
- [x] **AC-3:** WIP limits + overflow routing (scalar/series limit, cascading A→B→C, cycle validation) — 5 compiler tests
- [x] **AC-4:** SHIFT feedback (backpressure model stabilizes at Q=40 via bin-major evaluation) — 1 compiler test
- [x] **AC-5:** 6 parity fixture YAML files + integration evaluation tests — 6 fixture tests
- [x] **AC-6:** All 38 original tests pass alongside 27 new tests — 65 total

## Test Summary

| Suite | Count | Status |
|-------|-------|--------|
| Core unit tests | 49 | pass |
| Fixture integration tests | 16 | pass |
| **Total** | **65** | **all pass** |
| Miri (UB check) | 48 | pass (skipped 1 file-IO test) |

## Key Implementation Decisions

### Bin-major evaluation (architectural change)
The evaluator was refactored from op-major (`for op: for t`) to bin-major (`for t: for op`). This is the critical enabler for SHIFT feedback — at each bin, all ops execute in plan order, so when QueueRecurrence writes Q[t], subsequent ops (pressure, SHIFT) read from already-written bins. Const ops are pre-written before the bin loop. Produces identical results for non-feedback models.

### Unified topo sort
The compiler's topological sort was unified to include both expression nodes and topology-produced columns. SHIFT references are excluded from same-bin dependency edges (they read t-1). Overflow edges between topology nodes are included so source queues compile before target queues.

### Overflow routing inline
Overflow VecAdd ops are emitted inside `compile_single_topology_node` when the target node is compiled (not as a separate post-pass), ensuring correct bin-major evaluation order.

### snake_case conversion
`to_snake_case` handles consecutive uppercase correctly: "DLQ" → "dlq", "HTTPService" → "http_service", "Queue" → "queue". Queue column IDs follow `{snake_case(id)}_queue` convention.

## Files Changed

- `engine/core/src/plan.rs` — 4 new Op variants + format support
- `engine/core/src/eval.rs` — bin-major evaluator + `execute_op_at_bin` + `safe_val`
- `engine/core/src/compiler.rs` — unified topo sort, topology synthesis, SHIFT/CONV expression functions, overflow routing, cycle validation
- `engine/core/tests/fixture_deserialization.rs` — 6 topology eval tests
- `engine/fixtures/topology-*.yaml` — 6 parity fixture files
