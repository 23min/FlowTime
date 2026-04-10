# Matrix Engine Architecture

> Rust implementation of the FlowTime evaluation engine. Replaces the C# object-graph evaluator with a column-store + evaluation-plan model. Ships as a standalone CLI binary (`flowtime-engine`). Foundation for E-17 (Interactive What-If) and E-18 (Time Machine).

**Epic:** E-20 (`work/epics/E-20-matrix-engine/spec.md`)
**Research:** `docs/research/engine-rewrite-language-and-representation.md`
**Crate:** `engine/core` (library) + `engine/cli` (binary)

## Overview

The matrix engine is a rewrite of `FlowTime.Core`'s evaluation pipeline in Rust. It reads the same YAML model files, performs the same computations, and produces compatible output artifacts.

The key architectural difference: instead of an object graph where each node evaluates independently and results are memoized in a `Dictionary<NodeId, Series>`, the matrix engine compiles the model into an ordered list of operations (**plan**) that execute against a single flat matrix (**state**). All series live in one contiguous `f64` array. The plan is inspectable data, not opaque code.

```
YAML model → parse → compile → plan + column map → evaluate → state matrix → analyze → write artifacts
```

## Data Representation

```
State = f64[series_count x bin_count]
```

Row-major layout: `state[col * bins + t]` is column `col` at bin `t`. All bins for one series are contiguous in memory. Single allocation for the entire evaluation regardless of model size.

The **column map** is a bidirectional mapping between human-readable series names (e.g., `"arrivals"`, `"queue_queue"`) and integer column indices. Produced once during compilation.

## Evaluation Plan

The plan is an ordered list of `Op` variants. Each op reads from input columns and writes to an output column:

```
Element-wise:  VecAdd, VecSub, VecMul, VecDiv, VecMin, VecMax
               Clamp, Mod, ScalarAdd, ScalarMul
               Floor, Ceil, Round, Step, Pulse, Copy

Sequential:    Shift        — out[t] = input[t - lag]
               Convolve     — causal convolution with kernel
               QueueRecurrence — Q[t] = max(0, Q[t-1] + in - out - loss)
               DispatchGate — zeros non-dispatch bins, caps at capacity

Allocation:    ProportionalAlloc — caps N demands against shared capacity

Data:          Const        — writes literal values to a column
```

### Bin-Major Evaluation

The evaluator processes all ops per bin (outer loop = bins, inner loop = ops), not all bins per op. This is the critical design choice that enables SHIFT-based feedback cycles without special handling:

```rust
// Pre-write Const ops (fill all bins)
for op in plan.ops { if Const { write all bins } }

// Bin-major: process all ops for each bin
for t in 0..bins {
    for op in plan.ops {
        execute_op_at_bin(op, state, t, bins);
    }
}
```

At bin `t`, when QueueRecurrence writes `queue_depth[t]`, subsequent ops in the same bin (e.g., `pressure = queue_depth / capacity`) read the just-written value. SHIFT reads from `t-1` which was already computed. No special feedback mode needed.

## Compilation Pipeline

The compiler transforms a `ModelDefinition` into a `Plan` through these phases:

```
Phase 1   — Assign column indices to all explicit nodes
Phase 1b  — Pre-allocate topology-produced columns (queue depth, retry echo)
Phase 2   — Unified topological sort (expression nodes + topology columns)
Phase 3   — Emit ops in unified order (const/expr/pmf + topology nodes interleaved)
Phase 4   — Constraint allocation (ProportionalAlloc before QueueRecurrence)
Phase 5   — Derived metrics (utilization, cycle time, Kingman)
```

### Unified Topological Sort

The compiler builds a single dependency graph that includes both expression nodes and topology-produced columns. This ensures correct interleaving — for example, if `pressure` depends on `queue_depth` (a topology-produced column), the QueueRecurrence that writes `queue_depth` is ordered before `pressure` in the plan.

SHIFT references are excluded from same-bin dependency edges (they read `t-1`, not `t`), which breaks feedback cycles in the dependency graph while preserving correct evaluation via bin-major ordering.

### Topology Synthesis

The compiler processes `model.topology.nodes` to synthesize sequential ops, matching the C# `ModelCompiler` logic:

| Topology node kind | Synthesized ops |
|-------|------|
| `serviceWithBuffer` / `queue` / `dlq` | QueueRecurrence (+ DispatchGate if dispatch schedule, + Convolve if retryEcho) |
| WIP limit | QueueRecurrence with `wip_limit` + `overflow_out` columns |
| WIP overflow routing | VecAdd injecting overflow into target's inflow (ordered by overflow dependency graph) |
| Router | ScalarMul + VecAdd for weight-based splitting; per-class column Copy for class-based routing |
| Constraint | ProportionalAlloc capping demands against shared capacity |

### Derived Metrics

Derived metrics are additional ops appended after topology synthesis, composing from existing op types:

| Metric | Formula | Ops used |
|--------|---------|----------|
| Utilization | served / effectiveCapacity | VecDiv (+ ScalarMul for parallelism) |
| Queue time (ms) | (queueDepth / served) x binMs | VecDiv + ScalarMul |
| Service time (ms) | processingTimeMsSum / servedCount | VecDiv |
| Cycle time (ms) | queueTime + serviceTime | VecAdd |
| Flow efficiency | serviceTime / cycleTime | VecDiv |
| Latency (min) | queueTimeMs / 60000 | ScalarMul |
| Kingman E[Wq] | (rho/(1-rho)) x ((Ca^2+Cs^2)/2) x E[S] | ScalarAdd + ScalarMul + VecDiv + VecMul |

Cv (coefficient of variation) is computed at compile time from PMF definitions (sigma/mu) or set to 0 for constant series.

## Invariant Analysis

Post-evaluation read-only pass over the state matrix. Produces a list of warnings:

| Check | Code | Condition |
|-------|------|-----------|
| Non-negativity | `arrivals_negative`, `served_negative`, etc. | series[t] < -1e-6 |
| Conservation | `served_exceeds_capacity` | served[t] > capacity[t] + 1e-6 |
| Queue balance | `queue_depth_mismatch` | computed Q diverges from actual Q |
| Stationarity | `non_stationary` | first-half vs second-half mean diverges > 25% |

## Artifact Writing

The writer produces structured output to a directory:

```
<output>/
  series/
    {seriesName}.csv          — bin_index,value per line
    index.json                — series metadata (id, path, points, grid)
  run.json                    — run metadata (engineVersion, grid, warnings, series list)
```

Temp columns (`__temp_*`) are excluded from output. CSVs use invariant culture formatting (`.` decimal separator).

## CLI Interface

```
flowtime-engine parse <model.yaml>                  — parse and summarize
flowtime-engine plan <model.yaml>                   — print evaluation plan
flowtime-engine eval <model.yaml> [--output <dir>]  — evaluate (optionally write artifacts)
flowtime-engine validate <model.yaml>               — parse + compile + analyze, JSON output
```

Exit code 0 on success, 1 on error. `validate` outputs `{"valid": true/false, "warnings": [...]}`.

## Module Structure

```
engine/
  core/src/
    model.rs      — YAML model types (serde deserialization)
    expr.rs       — Expression parser (recursive descent, 5 AST variants)
    plan.rs       — ColumnMap, Op enum, Plan struct
    compiler.rs   — Model compiler (phases 1-5), eval_model entry point
    eval.rs       — Bin-major evaluator (execute_op_at_bin)
    analysis.rs   — Invariant analysis (warnings)
    writer.rs     — Artifact writer (CSV + JSON)
  cli/src/
    main.rs       — CLI binary (parse, plan, eval, validate)
  fixtures/       — 21 reference YAML models for testing
```

## C# Engine Mapping

| C# concept | Rust equivalent |
|-------------|-----------------|
| `INode.Evaluate()` | `Op` variant in plan |
| `Dictionary<NodeId, Series>` | Flat `f64[]` state matrix + column map |
| `ModelCompiler` topology synthesis | `compile_single_topology_node` + `compile_router` + `compile_constraints` |
| `ServiceWithBufferNode.Evaluate` | `Op::QueueRecurrence` |
| `DispatchScheduleProcessor` | `Op::DispatchGate` |
| `RouterFlowMaterializer` | `compile_router` (ScalarMul + VecAdd) |
| `ConstraintAllocator` | `Op::ProportionalAlloc` |
| `RuntimeAnalyticalEvaluator` | `compile_derived_metrics` (composed ops) |
| `InvariantAnalyzer` | `analysis::analyze` |
| `WipOverflowEvaluator` (iterative) | QueueRecurrence overflow + VecAdd (single-pass) |
| `Graph.EvaluateFeedbackSubgraph` (bin-by-bin) | Bin-major evaluation (default mode) |

## Test Coverage

119 tests across unit tests and fixture integration tests. Coverage includes:
- All Op variants (happy path + edge cases: div by zero, NaN/Inf, empty inputs, boundaries)
- Compiler: topology synthesis, routing, constraints, derived metrics, feedback cycles
- Analysis: non-negativity, conservation, queue balance, stationarity
- Writer: CSV format, JSON structure, temp column exclusion
- 21 reference fixture YAML files

## Future Work

- SHA256 hashing of artifacts (series, model, scenario)
- manifest.json (RNG seed, provenance)
- Per-class series output in artifacts
- .NET subprocess bridge (API calls `flowtime-engine eval` and reads artifacts)
- Full C# parity harness (bitwise output comparison)
- WebAssembly compilation (engine in browser for E-17)
- Incremental re-evaluation (change one input, replay only affected ops)
