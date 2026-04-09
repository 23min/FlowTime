# Milestone: Topology and Sequential Ops

**ID:** m-E20-03
**Epic:** E-20 Matrix Engine
**Status:** complete
**Branch:** `milestone/m-E20-03-topology-and-sequential` (off `epic/E-20-matrix-engine`)

## Goal

Add topology synthesis and sequential operations to the Rust engine so it can evaluate models with queues, retry echo, dispatch schedules, WIP limits, overflow routing, and SHIFT-based backpressure. After this milestone, the matrix engine handles the core flow dynamics — everything except routing, constraints, derived metrics, and artifact writing.

## Context

m-E20-02 delivered the compiler and evaluator for flat models (const + expr + PMF). The compiler produces a plan of element-wise ops, the evaluator executes against a flat matrix. 38 Rust tests passing.

The C# engine handles topology via:
1. `ModelCompiler` — synthesizes `serviceWithBuffer` and `retryEcho` nodes from topology definitions
2. `ServiceWithBufferNode.Evaluate` — sequential queue recurrence: `Q[t] = max(0, Q[t-1] + inflow - outflow - loss)`
3. `ExprNode.EvaluateShiftFunction` — temporal shift: `out[t] = input[t - lag]`
4. `DispatchScheduleProcessor` — gates outflow to dispatch bins only
5. `WipOverflowEvaluator` — post-evaluation routing of WIP overflow to target queues
6. `Graph.EvaluateFeedbackSubgraph` — bin-by-bin evaluation for SHIFT feedback cycles

In the matrix model, all of these become ops in the plan. Sequential ops (QueueRecurrence, Shift, Convolve) process bins in order, reading from previous bins in the same matrix — feedback falls out naturally without special handling.

## Acceptance Criteria

1. **AC-1: Sequential op variants.** Add to the `Op` enum:
   - `Shift { out, input, lag }` — `out[t] = input[t - lag]` (0 for t < lag)
   - `Convolve { out, input, kernel }` — causal convolution: `out[t] = Σ(k) input[t-k] * kernel[k]`
   - `QueueRecurrence { out, inflow, outflow, loss, init, wip_limit, overflow_out }` — sequential queue depth with optional WIP limit and overflow tracking
   - `DispatchGate { out, input, period, phase, capacity }` — gates output to dispatch bins, optionally capping at capacity

2. **AC-2: Topology synthesis.** The compiler processes `model.topology.nodes` to synthesize queue and retry echo nodes (same logic as C# `ModelCompiler`):
   - For each `serviceWithBuffer`/`queue`/`dlq` topology node: synthesize a `QueueRecurrence` op from `semantics.arrivals`, `semantics.served` (or capacity), `semantics.errors`, and `initialCondition.queueDepth`.
   - For each topology node with `retryEcho` + `retryKernel`: synthesize a `Convolve` op.
   - Queue node ID follows the C# snake_case convention (`Queue → queue_queue`).
   - Dispatch schedule on topology node → `DispatchGate` op on the outflow before `QueueRecurrence`.

3. **AC-3: WIP limits and overflow routing.**
   - `QueueRecurrence` op supports optional `wip_limit` column (scalar const or series) and `overflow_out` column.
   - Overflow routing: compiler resolves `wipOverflow` topology node ID to the target's inflow column, emits an additional `VecAdd` to inject overflow into the target's inflow.
   - Overflow cycle validation at compile time (same as C# `ValidateNoOverflowCycles`).

4. **AC-4: SHIFT feedback.** Models with SHIFT-based cross-node feedback cycles evaluate correctly without special handling. The Shift op reads `state[input, t - lag]` which was written in a previous bin iteration since the evaluator processes ops in plan order and sequential ops process bins in order. Test: the backpressure model from E-10 p3b (queue → pressure → SHIFT → effective_arrivals → queue) produces the same stabilization pattern.

5. **AC-5: Parity fixtures.** Create simulation-mode topology fixtures (with `grid` + inline `const`/`expr` nodes + topology) and verify parity with C# output:
   - Simple queue: const arrivals/served → queue depth matches hand-calculated values
   - Queue with WIP limit: overflow tracked correctly
   - Queue with dispatch schedule: outflow gated to period bins
   - Retry echo: CONV(failures, kernel) produces correct retry series
   - Backpressure feedback: SHIFT-based throttle stabilizes queue
   - Cascading WIP overflow: A→B→C overflow chain

6. **AC-6: Existing tests unbroken.** All 38 existing Rust tests still pass. The compiler changes don't break const/expr compilation.

## Technical Notes

- **Evaluation order for sequential ops:** The plan is still executed as a linear op list. Sequential ops (QueueRecurrence, Shift, Convolve) internally loop over bins. This is correct because the evaluator processes ops in dependency order (topo sort), and within a sequential op, each bin reads from previous bins that are already written. No special "feedback mode" needed.
- **Overflow routing without re-evaluation:** Unlike the C# `WipOverflowEvaluator` (which iterates evaluate → override → re-evaluate), the matrix compiler can emit the overflow routing as additional ops after the queue ops. The QueueRecurrence op writes overflow to `overflow_out` column; a subsequent `VecAdd` adds it to the target's inflow. For cascading (A→B→C), the compiler orders the QueueRecurrence ops so A runs before B which runs before C. No iteration needed — single-pass.
- **Topology node ID → queue column:** The compiler maintains a mapping from topology node ID to the synthesized queue column index, used for overflow routing and later for derived metrics (m-E20-05).
- **New fixtures needed:** Existing fixtures in `engine/fixtures/` are either flat models (hello, simple-const) or telemetry models (file: references). This milestone needs simulation-mode topology fixtures with inline data. Create them as Rust test inline YAML or as new fixture files.
- **Dispatch schedule:** The C# `DispatchScheduleProcessor` zeros outflow on non-dispatch bins, then caps at capacity on dispatch bins. In the matrix model, this is a `DispatchGate` op applied to the outflow column before it reaches `QueueRecurrence`.

## Out of Scope

- Routing (router flow materialization) — m-E20-04
- Constraints (proportional allocation) — m-E20-04
- Multi-class flows — m-E20-04
- Derived metrics (utilization, latency, etc.) — m-E20-05
- Invariant analysis — m-E20-05
- Artifact writing — m-E20-06
- File-based series references (`file:*.csv`) — future (telemetry mode)

## Dependencies

- m-E20-02 complete (compiler + evaluator + element-wise ops)
