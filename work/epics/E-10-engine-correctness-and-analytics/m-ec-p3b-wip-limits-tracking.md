# Tracking: m-ec-p3b WIP Limits

**Status:** in-progress
**Started:** 2026-04-09
**Epic:** [E-10 Engine Correctness & Analytical Primitives](./spec.md)
**Milestone spec:** [m-ec-p3b-wip-limits.md](./m-ec-p3b-wip-limits.md)
**Branch:** `milestone/m-ec-p3b-wip-limits` (off `main`)
**Baseline test count (main HEAD `6cc3209`):** 1274 passed, 9 skipped, 0 failed

## Acceptance Criteria

- [x] **AC-1.** WIP limit on ServiceWithBufferNode. Optional `wipLimit` (scalar or series). When `Q[t] > wipLimit[t]`, overflow = Q[t] - wipLimit[t], Q[t] = wipLimit[t]. Overflow tracked as a series.
- [x] **AC-2.** WIP overflow routing. Optional `wipOverflow` field: `"loss"` (default, overflow added to loss series) or a node ID (compiler wires overflow as inflow to target). Cascading supported. Compiler validates no cycles.
- [x] **AC-3.** Model schema updated. `NodeDefinition` supports `wipLimit`, `wipLimitSeries`, and `wipOverflow`. Schema docs updated.
- [x] **AC-4.** Backpressure pattern documented and tested. `docs/architecture/backpressure-pattern.md` describes SHIFT-based backpressure. Integration test demonstrates upstream throttling via SHIFT(signal, lag).
- [x] **AC-5.** Tests: WIP clamping, overflow to loss, overflow to DLQ node, cascading overflow, time-varying wipLimit (series), backpressure via SHIFT, WIP+backpressure combined. Full test suite green.

## Commit Plan (tentative)

- [x] **Status-sync** — branch, flip statuses, create this tracking doc.
- [x] **Bundle A** — AC-1 + AC-5 (WIP clamping tests): Extend ServiceWithBufferNode with wipLimit. TDD: write tests for clamping and overflow-to-loss → implement → green.
- [x] **Bundle B** — AC-3 partial: `NodeDefinition.WipLimit`/`WipOverflow` + `TopologyNodeDefinition.WipLimit`/`WipOverflow` fields. ModelCompiler propagates from topology to generated NodeDefinition. CloneTopologyNode fixed. End-to-end pipeline test passing.
- [ ] **Bundle C** — AC-2 + AC-3 + AC-4 + AC-5 remaining: Overflow routing to target node. WipOverflowEvaluator. Cycle validation. Time-varying wipLimit (series ref). Backpressure doc + SHIFT test.
- [ ] **Wrap** — tracking doc, status reconciliation. E-10 epic completion assessment.

## Implementation Log

### Status-sync — 2026-04-09

Branch `milestone/m-ec-p3b-wip-limits` created off `main` HEAD (`6cc3209`). Status flipped: spec approved→in-progress, epic spec table, CLAUDE.md, epic-roadmap.md.

### Bundle A — 2026-04-09

ServiceWithBufferNode extended with `wipLimit` (double?) and `LastOverflow` (double[]?). WIP clamping: Q[t] = min(Q[t], wipLimit), overflow tracked per bin. 5 unit tests: clamping, overflow tracked, no-limit unbounded, overflow-to-loss, zero limit.

### Bundle B — 2026-04-09

NodeDefinition and TopologyNodeDefinition gain `WipLimit` and `WipOverflow` fields. ModelCompiler propagates from topology to generated ServiceWithBuffer nodes. CloneTopologyNode copies new fields. ModelParser passes wipLimit through to ServiceWithBufferNode. End-to-end pipeline test: topology with wipLimit=15 through compile→parse→evaluate.

### Bundle C — 2026-04-09

**AC-2: WIP overflow routing**
- `ServiceWithBufferNode` extended with `wipOverflowTarget` (string?) and `InflowNodeId` (exposed for injection).
- `Graph` gains `NodesOfType<T>()` and `TryGetNode()` helpers.
- New `WipOverflowEvaluator` (post-evaluation pass): reads `LastOverflow` from WIP-limited nodes, injects overflow as additional inflow to target queue via `EvaluateWithOverrides`. Handles cascading A→B→C via iterative convergence (max 10 iterations).
- `ModelCompiler.ResolveWipOverflowTargets()`: resolves topology node IDs to queue node IDs after queue synthesis. `ValidateNoOverflowCycles()`: detects cycles in overflow routing graph before node construction.
- 3 new tests: overflow to DLQ node, cascading overflow (A→B→C with intermediate WIP limits), cycle detection.

**AC-3 completion: WipLimitSeries**
- `NodeDefinition.WipLimitSeries` and `TopologyNodeDefinition.WipLimitSeries` added for time-varying limits.
- `ServiceWithBufferNode` gains `wipLimitSeriesId` (NodeId?) — same pattern as `DispatchScheduleConfig.CapacitySeriesId`.
- Evaluate resolves per-bin limit from series (precedence over scalar). Added to `Inputs` for graph dependency tracking.
- ModelCompiler propagates `WipLimitSeries` from topology to generated nodes. CloneTopologyNode copies it.
- ModelParser passes `wipLimitSeriesId` through both parse paths.
- 1 new test: time-varying wipLimit with limit series [20, 10, 5].

**AC-4: Backpressure pattern doc + SHIFT test**
- Created `docs/architecture/backpressure-pattern.md`: documents WIP limits (push+overflow) vs SHIFT-based backpressure (pull+throttle), with examples and comparison table.
- Note: SHIFT-based backpressure uses external signal series, not self-referencing queue feedback. The graph is a DAG — direct queue→pressure→arrivals→queue cycles are topological cycles rejected by the evaluator. SHIFT breaks time but not topology.
- 2 new tests: SHIFT throttles effective arrivals via lagged capacity signal; WIP limit + SHIFT combined behavior.

**AC-5: All scenarios covered**
- WIP clamping (Bundle A), overflow to loss (Bundle A), overflow to DLQ node (Bundle C), cascading overflow (Bundle C), time-varying wipLimit series (Bundle C), backpressure via SHIFT (Bundle C), WIP+backpressure combined (Bundle C).

## Test Summary

- **Baseline:** 1274 passed, 9 skipped, 0 failed
- **After Bundle A:** 1279 passed (+5 WIP limit unit tests)
- **After Bundle B:** 1280 passed (+1 end-to-end pipeline test)
- **After Bundle C:** 1286 passed (+3 overflow routing + 1 time-varying + 2 backpressure)
- **Build:** green

## Notes

### Design decisions (Bundle C)
- **Option A from spec chosen for overflow routing:** Post-evaluation override injection via `WipOverflowEvaluator`, same pattern as `ConstraintAwareEvaluator`. No new `IMultiOutputNode` interface needed.
- **Overflow resolved at compile time:** `ModelCompiler.ResolveWipOverflowTargets` maps topology node IDs to queue node IDs after queue synthesis. Cycle validation also at compile time.
- **SHIFT backpressure is signal-driven, not queue-feedback:** Cross-node queue→pressure→arrivals feedback creates topological cycles. The documented pattern uses external signal series with SHIFT for time-lagged throttling. Self-referencing SHIFT on expr nodes is validated by the semantic checker but creates self-loops in the graph's topological sort — a pre-existing limitation, not introduced by this milestone.

## Completion

- **Completed:** pending (all ACs done, awaiting wrap)
- **Final test count:** 1286 passed, 9 skipped, 0 failed
- **Deferred items:** (none)
