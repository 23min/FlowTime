# Nodes, Expressions, and Execution

> **📋 Charter Context**: This document describes core engine concepts that remain central to the [FlowTime-Engine Charter](../flowtime-engine-charter.md). The execution engine and node system directly support the charter's artifacts-centric workflow by producing structured, deterministic outputs.

This document explains how FlowTime represents models internally (nodes and graphs), how expressions map to nodes, and how evaluation executes deterministically on a canonical time grid. These capabilities form the execution core of the charter paradigm: **[Models] → [Runs] → [Artifacts] → [Learn]**.odes, Expressions, and Execution

This document explains how FlowTime represents models internally (nodes and graphs), how expressions map to nodes, and how evaluation executes deterministically on a canonical time grid. It also shows how today’s M0 implementation works and how it evolves per the roadmap.

## Mental model

Think of FlowTime as a spreadsheet for flows:
- The time axis is a fixed grid of bins (e.g., 8 bins, 60 minutes each).
- Each node produces a numeric series aligned to the grid.
- Edges express dependencies between nodes, like spreadsheet cell references.
- Evaluation is deterministic and ordered topologically: inputs first, outputs last.

## The building blocks

- TimeGrid
  - Fixed number of bins and binMinutes. All series align to this grid.
- Series
  - A contiguous double[] representing a value per bin. Immutable by contract (copy on write in nodes).
- NodeId
  - Stable identity for a node in a graph.
- INode
  - Contract: Id, Inputs, Evaluate(grid, getInput).
- Graph
  - Manages nodes, checks for cycles, derives a topological order, and memoizes evaluation results.

## Current nodes (M0)

- ConstSeriesNode
  - Emits a fixed series of doubles with length == grid.Bins.
- BinaryOpNode
  - Performs Add or Mul over time series.
  - Inputs: left series and either a right series or a scalarRight.
  - Today’s YAML exprs ("name * k", "name + k") compile to BinaryOpNode with scalarRight.

These form the minimal set to enable simple what-if scenarios right away.

## Expressions and compilation

Expressions are the authoring surface. Behind the scenes, FlowTime compiles expressions to node graphs. In M0, we support tiny inline exprs that translate to a BinaryOpNode with scalar RHS. In later milestones (M1+), a real parser will support:
- Arithmetic: +, -, *, /
- Functions: SHIFT, DELAY, RESAMPLE, CLAMP, MIN, MAX, etc.
- References: identifiers for other nodes

### Example: from expression to nodes

Expression (YAML):
```yaml
nodes:
  - id: demand
    kind: const
    values: [10, 10, 10, 10, 10, 10, 10, 10]
  - id: served
    kind: expr
    expr: "demand * 0.8"
```

Compilation (conceptually):
- demand → ConstSeriesNode("demand")
- served → BinaryOpNode("served", left=demand, op=Mul, scalarRight=0.8)

Graph edges:
- demand → served

Evaluation order:
- [demand, served]

### Future expressions

Example (planned):
```yaml
  - id: backlog
    kind: expr
    expr: "CLAMP(demand - SHIFT(capacity, 1), 0, +INF)"
```
This will compile into a small subgraph:
- SHIFT(capacity, 1) → ShiftNode("shift_cap", capacity, 1)
- demand - shift_cap → BinaryOpNode(Sub)
- CLAMP(…, 0, +INF) → ClampNode

The graph remains explicit and explainable.

## Execution model

FlowTime uses a directed acyclic graph (DAG) and topological sort:
1. Validate: detect cycles (Kahn’s algorithm). Fail fast on invalid models.
2. Order: compute a topological order over nodes.
3. Evaluate: for each node in order, call Evaluate(grid, getInput) where getInput returns previously computed series.
4. Memoize: store each node’s output once; downstream nodes reuse it.

This yields deterministic, repeatable results with no hidden state.

### Determinism and safety
- Deterministic per-bin math, culture-invariant CSV.
- Length checks ensure ConstSeriesNode values match grid.Bins.
- Missing inputs throw clear exceptions.

## Examples in practice

### Minimal model (M0)
```yaml
grid:
  bins: 8
  binMinutes: 60
nodes:
  - id: demand
    kind: const
    values: [10,10,10,10,10,10,10,10]
  - id: served
    kind: expr
    expr: "demand * 0.8"
outputs:
  - series: served
    as: served.csv
```
Run (CLI):
```powershell
dotnet run --project src/FlowTime.Cli -- run examples/hello/model.yaml --out out/hello --verbose
```
Console summary (verbose):
```
FlowTime run summary:
  Grid: bins=8, binMinutes=60
  Topological order: demand -> served
  Wrote served.csv (8 rows)
```
CSV head:
```
t,value
0,8
1,8
2,8
...
```

### Series × series
If you define two const nodes and an expr that references both (future parser), we compile to a BinaryOpNode with both inputs:
- a=[2,3,4], c=[5,6,7], b=a*c → b=[10,18,28]

Today, you can build the same in code/tests by composing nodes directly.

## Roadmap alignment

- M0: grid + series + DAG + minimal nodes + CLI. Tiny exprs translate to BinaryOpNode with scalar RHS.
- M1: expression parser, SHIFT built-in, general references → compiled nodes under the hood.
- M2+: add DELAY/RESAMPLE/CLAMP, backlog/queues/routing, API/SPA, real-time, storage providers.

## Why nodes (even with expressions)?

- Explainability: graphs make lineage explicit and reviewable.
- Testability: primitives are easy to verify independently and in composition.
- Optimization: specific nodes (e.g., Shift) can be optimized later.
- Caching: memoization at node granularity.

## Model YAML Schema: FlowTime vs FlowTime-Sim

FlowTime and FlowTime-Sim share the same YAML model schema for defining flows and nodes, but they differ in how they handle determinism and randomness:

### FlowTime (Engine)
FlowTime is **always deterministic** by design:
- **No randomness**: Models are evaluated deterministically using fixed rules and expressions
- **Seed/RNG ignored**: If `seed` or `rng` fields are present in the YAML, they are ignored during execution
- **Reproducible results**: The same model always produces identical outputs regardless of `seed` values

Example FlowTime model:
```yaml
grid:
  bins: 8
  binMinutes: 60
nodes:
  - id: demand
    kind: const
    values: [10,10,10,10,10,10,10,10]
  - id: served
    kind: expr
    expr: "demand * 0.8"
outputs:
  - series: served
    as: served.csv
# seed: 42          # This field is ignored by FlowTime
```

### FlowTime-Sim (Simulator)
FlowTime-Sim uses **configurable randomness** for synthetic data generation:
- **Requires seed for determinism**: To get reproducible results, you must specify `seed` and `rng` fields
- **Seed/RNG significant**: These fields control random number generation for synthetic arrivals and events
- **Non-deterministic without seed**: If no seed is provided, results will vary between runs

Example FlowTime-Sim model:
```yaml
schemaVersion: 1
grid:
  bins: 4
  binMinutes: 60
  start: 2025-01-01T00:00:00Z
seed: 12345          # Required for deterministic results
rng:
  kind: pcg32        # Required for deterministic results
arrivals:
  kind: const
  values: [5, 5, 5, 5]
route:
  id: nodeA
outputs:
  events: out/events.ndjson
  gold: out/gold.csv
```

### Summary
- **FlowTime**: Always deterministic, `seed`/`rng` ignored
- **FlowTime-Sim**: Deterministic only when `seed`/`rng` are properly defined

Both engines produce the same artifact structure (CSV files, JSON metadata) but use different approaches to achieve determinism.

## FAQs

- Do expressions replace nodes? No—expressions compile to nodes. Nodes remain the execution plan.
- Can I do series × series operations? Yes. The node supports it; the YAML parser will cover it in M1+. For now, tests show the behavior.
- How are cycles handled? The graph rejects cycles at construction.
- How do I see what was evaluated? Use `--verbose` to print topo order and outputs.
