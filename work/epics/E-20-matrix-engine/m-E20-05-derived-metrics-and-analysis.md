# Milestone: Derived Metrics and Analysis

**ID:** m-E20-05
**Epic:** E-20 Matrix Engine
**Status:** complete
**Branch:** `milestone/m-E20-05-derived-metrics-and-analysis` (off `epic/E-20-matrix-engine`)

## Goal

Add derived metric computation and invariant analysis to the Rust engine. Derived metrics (utilization, cycle time, flow efficiency, Kingman approximation) are emitted as additional plan ops on the evaluation matrix. Invariant analysis (conservation checks, warnings) runs as a post-evaluation pass over the matrix columns. After this milestone, the engine produces all analytical output — only artifact writing and CLI remain.

## Context

m-E20-04 delivered routing and constraint allocation. The engine now handles the full evaluation pipeline: const/expr/PMF nodes, topology synthesis (queues, retry echo, dispatch), WIP overflow, SHIFT feedback, routers, and constraints. 78 Rust tests passing.

The C# engine computes derived metrics in `RuntimeAnalyticalEvaluator` and runs invariant checks in `InvariantAnalyzer`. In the matrix model, derived metrics are additional columns computed from evaluation output. Invariant analysis is a read-only pass that produces warnings.

## Acceptance Criteria

1. **AC-1: Utilization metric.** Compute `utilization[t] = served[t] / effectiveCapacity[t]` for topology nodes with capacity semantics. Emit as a derived column per node. Returns 0 when capacity is 0. Effective capacity = base capacity × parallelism (when parallelism is defined).

2. **AC-2: Cycle time components.** Compute per-bin:
   - `queueTimeMs[t] = (queueDepth[t] / served[t]) × binMs` (0 when served ≤ 0)
   - `serviceTimeMs[t] = processingTimeMsSum[t] / servedCount[t]` (0 when servedCount ≤ 0)
   - `cycleTimeMs[t] = queueTimeMs[t] + serviceTimeMs[t]` (sum of available components)
   - `flowEfficiency[t] = serviceTimeMs[t] / cycleTimeMs[t]` (0 when cycleTime ≤ 0)
   - `latencyMinutes[t] = queueTimeMs[t] / 60000`
   - Which components are emitted depends on node category: queue-only → queueTime+latency, service-only → serviceTime, serviceWithBuffer → all.

3. **AC-3: Kingman G/G/1 approximation.** Compute `E[Wq] ≈ (ρ/(1-ρ)) × ((Ca² + Cs²)/2) × E[S]` where:
   - `ρ` = utilization (must be in (0, 1))
   - `Ca` = coefficient of variation of arrivals
   - `Cs` = coefficient of variation of service
   - `E[S]` = mean service time (ms)
   - Returns 0 for invalid inputs (ρ ≥ 1, negative Cv, etc.)
   - Cv is computed from PMF nodes (σ/μ) or as 0.0 for constant series.

4. **AC-4: Invariant warnings.** Post-evaluation analysis producing a `Vec<Warning>` struct:
   - **Non-negativity:** Flag bins where arrivals, served, errors, or queueDepth < -ε (ε = 1e-6)
   - **Conservation:** Flag bins where served > arrivals + ε (for non-queue nodes) or served > capacity + ε
   - **Queue balance:** Flag bins where computed queue depth diverges from actual (|computed - actual| > ε)
   - **Stationarity:** Flag when arrivals first-half vs second-half mean diverges > 25%
   - Warning struct: `{ node_id, code, message, bins, severity }`

5. **AC-5: Derived metrics integration in compiler.** The compiler emits derived metric ops after topology ops, reading from queue depth, served, capacity, and other evaluation columns. A new `compile_derived_metrics` phase appends ops to the plan. The `EvalResult` includes a method to retrieve warnings.

6. **AC-6: Parity tests.** Test models verifying:
   - Utilization: served=8, capacity=10 → utilization=0.8
   - Queue time: queueDepth=10, served=5, binMs=60000 → queueTimeMs=120000
   - Kingman: ρ=0.8, Ca=1.0, Cs=0.5, E[S]=10 → E[Wq]=25
   - Conservation violation: served > arrivals detected as warning
   - Stationarity: increasing arrivals flagged

7. **AC-7: Existing tests unbroken.** All 78 existing Rust tests still pass.

## Technical Notes

- **Derived columns naming:** `{nodeId}_utilization`, `{nodeId}_queue_time_ms`, `{nodeId}_cycle_time_ms`, `{nodeId}_flow_efficiency`, `{nodeId}_latency_min`, `{nodeId}_kingman_wq`.
- **Bin duration:** Resolved from `grid.binSize` × `grid.binUnit` → milliseconds. Needed for queue time computation.
- **Cv computation:** For PMF nodes, Cv = σ/μ computed at compile time from the PMF definition. For const nodes, Cv = 0. For expr nodes, Cv is not computed (future: sample Cv from evaluated series).
- **Invariant analysis is read-only.** It does not modify the matrix. It returns a list of warnings. The warnings are stored alongside the EvalResult.
- **No new Op for most derived metrics.** Utilization = VecDiv(served, capacity). Queue time = VecDiv(queueDepth, served) then ScalarMul by binMs. These compose from existing ops. Kingman may need a dedicated op or can be computed at compile time from scalar inputs.

## Out of Scope

- Artifact writing (CSVs, index.json, run.json) — m-E20-06
- CLI commands — m-E20-06
- Per-class derived metrics — future
- Window-level aggregation (multi-bin statistics) — future
- Edge-specific warnings (edge flow conservation) — future
- Streak detection (backlog growth, overload, age risk) — future (could add in m-E20-05 if time permits)

## Dependencies

- m-E20-04 complete (routing, constraints, full evaluation pipeline)
