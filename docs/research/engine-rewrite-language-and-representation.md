# Engine Rewrite: Language and Data Representation

Research notes from evaluating alternative languages and data representations for FlowTime.Core.

---

## Context

FlowTime.Core is a deterministic flow algebra engine: a pure function that computes over vectors (time-series). A model (YAML DAG) goes in, canonical run artifacts come out. Same inputs always produce the same outputs. The core is ~13,000 lines of C#.

The question: is C# the right language, and is the current object-per-node representation the right data layout?

---

## Current C# representation

### How it works today

- **Series**: immutable wrapper around `double[]`, defensive copies on construction and on `ToArray()`
- **Nodes**: `INode` interface with 6 concrete implementations (ConstSeries, BinaryOp, ServiceWithBuffer, Router, Shift, Expr)
- **Graph evaluation**: topological sort (Kahn's algorithm), then iterate in order; each node calls `Evaluate(grid, getInput)`, results memoized in `Dictionary<NodeId, Series>`
- **Expression evaluator**: recursive pattern match on AST nodes (`LiteralNode`, `NodeReferenceNode`, `BinaryOpNode`, `FunctionCallNode`)
- **Derived metrics**: pure functions in `CycleTimeComputer`, `UtilizationComputer`, `RuntimeAnalyticalEvaluator`
- **Invariant analysis**: ~400 lines of conservation checks, non-negativity, capacity bounds

### What C# does well

- Compilation and evaluation are cleanly separated
- `Series` is immutable by convention (defensive copies)
- `INode.Evaluate()` is stateless — takes inputs, returns output
- Derived metrics are pure functions
- Records and sealed classes for data modeling

### Where C# fights the domain

- **Defensive copying everywhere**: `Series` clones on construction AND on `ToArray()` because C# can't enforce immutability on `double[]` — two allocations per node per evaluation
- **Nullable gymnastics**: `double?` throughout metrics, `Sanitize()` to catch NaN/Infinity, null checks on every optional series
- **In-place mutation leaks**: `DispatchScheduleProcessor.ApplySchedule()` mutates arrays in-place; safety depends on caller making a defensive copy first (convention, not guarantee)
- **Lazy init race condition**: `Topology`'s edge index uses `??=` without synchronization — safe only because nothing is multi-threaded today
- **`IStatefulNode` interface**: the type system can't distinguish pure from impure at compile time
- **Hash lookups during evaluation**: `Dictionary<NodeId, Series>` instead of direct array indexing
- **No SIMD opportunity**: series data isn't contiguous across nodes

---

## The matrix representation

This is the central idea. Independent of language choice, the data representation should change. The engine is fundamentally an array programming problem, and the representation should reflect that.

### What the engine actually does

Strip away all the C# abstractions — interfaces, classes, records, dictionaries — and what remains is:

1. A set of named vectors, each of length `bins`
2. An ordered sequence of operations that read some vectors and write one vector
3. A few operations that are sequential across bins (queue recurrence); the rest are element-wise

That's it. That's the entire engine. Everything else is bookkeeping.

### The matrix

All series live in one contiguous allocation:

```
State = double[seriesCount × binCount]    // flat 1D, row-major
```

Logically a 2D matrix where each row is a series and each column is a time bin:

```
         bin0   bin1   bin2   bin3   bin4   ...  bin11
col 0  [  60.0,  80.0, 100.0, 120.0, 140.0, ... , 40.0 ]  ← transactions (const)
col 1  [ 110.0, 110.0, 110.0, 110.0, 110.0, ... ,110.0 ]  ← capacity (const)
col 2  [   0.0,   0.0,   0.0,   0.0,   6.0, ... ,  1.4 ]  ← retries (conv output)
col 3  [  60.0,  80.0, 100.0, 120.0, 146.0, ... , 41.4 ]  ← arrivals_total (add)
col 4  [  60.0,  80.0, 100.0, 110.0, 110.0, ... , 41.4 ]  ← served (min)
col 5  [   0.0,   0.0,   0.0,  10.0,  36.0, ... ,  0.0 ]  ← errors (sub)
col 6  [   0.0,   0.0,   0.0,   0.0,  10.0, ... , 89.0 ]  ← settlement queue (recurrence)
col 7  [   0.0,   0.0,   0.0,   0.0,  10.0, ... , 41.4 ]  ← settlement dispatch (min)
```

This IS the computation state. No wrappers, no dictionaries, no object graphs. A flat block of doubles.

### The evaluation plan

The topological sort produces an ordered list of operations, not nodes. Each operation names its input and output columns by integer index:

```
Op[] plan = [
  Const(out=0, values=[60, 80, 100, 120, 140, 150, 140, 120, 100, 80, 60, 40]),
  Const(out=1, values=[110, 110, 110, 110, 110, 110, 110, 110, 110, 110, 110, 110]),
  VecAdd(out=3, a=0, b=2),               // arrivals_total := transactions + retries
  VecMin(out=4, a=1, b=3),               // served := MIN(capacity, arrivals_total)
  VecSub(out=5, a=3, b=4),               // errors := arrivals_total - served
  Convolve(out=2, input=5, kernel=[0.0, 0.6, 0.3, 0.1]),  // retries := CONV(errors, kernel)
  QueueRecurrence(out=6, inflow=4, outflow=7, init=0.0),   // settlement queue
  VecMin(out=7, a=6, b=Const(100)),       // settlement dispatch := MIN(queue, 100)
]
```

Each op is a pure function: read input rows from the matrix, write one output row. No object graph traversal, no type dispatch, no hash lookups, no allocations.

### The evaluator

```
fn evaluate(plan: &[Op], bins: usize, series_count: usize) -> Vec<f64> {
    let mut state = vec![0.0; series_count * bins];
    for op in plan {
        execute(op, &mut state, bins);
    }
    state
}
```

That's the entire evaluator. ~10 lines. The complexity lives in the compiler (which produces the plan) and in the individual op implementations (which are each 3-10 lines of array math).

### Complete operation catalog

Every FlowTime operation expressed as a matrix row operation:

```
Const(out, values)
    state[out, 0..bins] = values[0..bins]

VecAdd(out, a, b)
    for t: state[out, t] = state[a, t] + state[b, t]

VecSub(out, a, b)
    for t: state[out, t] = state[a, t] - state[b, t]

VecMul(out, a, b)
    for t: state[out, t] = state[a, t] * state[b, t]

VecDiv(out, a, b)
    for t: state[out, t] = if state[b, t] != 0 { state[a, t] / state[b, t] } else { 0.0 }

VecMin(out, a, b)
    for t: state[out, t] = min(state[a, t], state[b, t])

VecMax(out, a, b)
    for t: state[out, t] = max(state[a, t], state[b, t])

Clamp(out, val, lo, hi)
    for t: state[out, t] = clamp(state[val, t], state[lo, t], state[hi, t])

Shift(out, input, lag)
    for t: state[out, t] = if t >= lag { state[input, t - lag] } else { 0.0 }

Convolve(out, input, kernel)
    for t: state[out, t] = Σ(k=0..kernel.len) if t >= k { state[input, t-k] * kernel[k] }

Floor(out, input)           for t: state[out, t] = floor(state[input, t])
Ceil(out, input)            for t: state[out, t] = ceil(state[input, t])
Round(out, input)           for t: state[out, t] = round(state[input, t])
Mod(out, a, b)              for t: state[out, t] = state[a, t] mod state[b, t]
Step(out, input, threshold) for t: state[out, t] = if state[input, t] >= threshold { 1.0 } else { 0.0 }
Pulse(out, period, phase, amplitude)
                            for t: state[out, t] = if (t - phase) mod period == 0 { amplitude } else { 0.0 }

ScalarAdd(out, input, k)    for t: state[out, t] = state[input, t] + k
ScalarMul(out, input, k)    for t: state[out, t] = state[input, t] * k

QueueRecurrence(out, inflow, outflow, init)
    state[out, 0] = max(0, init + state[inflow, 0] - state[outflow, 0])
    for t in 1..bins: state[out, t] = max(0, state[out, t-1] + state[inflow, t] - state[outflow, t])

QueueRecurrenceWithLoss(out, loss_out, inflow, outflow, capacity, init)
    // queue with overflow/loss tracking
    state[out, 0] = max(0, init + state[inflow, 0] - state[outflow, 0])
    for t in 1..bins:
        raw = state[out, t-1] + state[inflow, t] - state[outflow, t]
        state[out, t] = clamp(raw, 0, state[capacity, t])
        state[loss_out, t] = max(0, raw - state[capacity, t])

DispatchGate(out, input, period, phase, capacity_col)
    for t: state[out, t] = if (t - phase) mod period == 0 { min(state[input, t], state[capacity_col, t]) } else { 0.0 }
```

Every one of these is 1-5 lines of array math. The entire operation catalog is ~80 lines of code. Compare to the current C# implementation: 6 class files, an interface, a stateful interface, visitors, factories, and defensive copies — ~400+ lines for the same semantics.

### Vectorizable vs sequential operations

| Category | Operations | SIMD? |
|----------|-----------|-------|
| Element-wise | Add, Sub, Mul, Div, Min, Max, Clamp, Floor, Ceil, Round, Mod, Step, Scalar* | Yes — full SIMD, 4-8x on f64 |
| Temporal (no recurrence) | Shift, Pulse | Yes — with offset indexing |
| Temporal (scan) | Convolve | Partially — inner kernel loop is SIMD-friendly, outer loop is sequential |
| Sequential | QueueRecurrence, QueueRecurrenceWithLoss | No — each bin depends on the previous |
| Gated | DispatchGate | Yes — element-wise with mask |

In a typical FlowTime model, 70-80% of operations are fully vectorizable. Queue recurrence is the exception, and it's a single O(bins) scan — fast regardless.

### Memory layout: row-major

The matrix is stored row-major: all bins for one series are contiguous in memory.

```
Memory: [col0_bin0, col0_bin1, ..., col0_binN, col1_bin0, col1_bin1, ..., col1_binN, ...]
```

This is correct for FlowTime because every operation processes one or more complete rows. A `VecAdd` reads two full rows and writes one full row — all sequential memory access. Cache lines are fully utilized.

Column-major (all series for one bin contiguous) would be wrong — FlowTime does NOT evaluate "all nodes at bin t, then all nodes at bin t+1." It evaluates "all bins for node A, then all bins for node B." Row-major matches the access pattern.

### The column map

The bridge between named series (authoring) and integer indices (runtime):

```
ColumnMap:
  "transactions"                    → col 0
  "capacity"                        → col 1
  "retries@payment_service"         → col 2
  "arrivals_total@payment_service"  → col 3
  "served@payment_service"          → col 4
  "errors@payment_service"          → col 5
  "queue@settlement"                → col 6
  "dispatch@settlement"             → col 7
```

Produced once during compilation. Bidirectional — you can go from name to index (for compiling expressions) or from index to name (for labeling output series).

The column map is the only place where human-readable names exist at runtime. The evaluator never sees strings — only integers.

### DAG and matrix: two views, not a conversion

The DAG and the matrix are not different data — they are different projections of the same model. You keep both. There is no "converting" between them.

| Projection | What it answers | Used by |
|-----------|----------------|---------|
| DAG | "What depends on what?" "What are the edges?" | Compiler, UI topology view, conservation checker |
| Column map | "Which series is where in memory?" | Evaluator, serializer, API projections |
| Matrix | "What are the values?" | Everything that reads results |

```
DAG (authoring/modeling)
  │
  │  compile: topo-sort + assign column indices
  ▼
Evaluation Plan + Column Map (runtime)
  │
  │  execute: fill the matrix
  ▼
State Matrix (results)
  │
  │  project: column map tells you which column belongs to which node
  ▼
DAG + series (visualization)
```

**DAG → Matrix** (compile time): walk the DAG, assign column indices. `payment_service.arrivals → col 0`, `settlement.queue → col 5`. The DAG's topology compiles into operation ordering. The DAG's node/series structure compiles into column assignments. Nothing is lost.

**Matrix → DAG** (projection for visualization): after execution, the matrix is `double[seriesCount, binCount]`. To project back: `columnMap.Get("settlement.queue") → col 5 → state[5, *]`. The DAG structure never changes — you attach result vectors to it for display.

The visualization layer does: `for each node in DAG: look up its series via column map, read vectors from matrix, render`. That's a join, not a conversion.

This is the same insight APL got right 60 years ago: the array is the natural runtime representation; the named graph is the natural authoring representation. They coexist.

### Multi-class flows in the matrix

Classes multiply the column count. Each class gets its own set of columns for each measure:

```
ColumnMap (with classes):
  "arrivals@payment_service"              → col 3   (total / DEFAULT)
  "arrivals@payment_service@Order"        → col 8
  "arrivals@payment_service@Refund"       → col 9
  "served@payment_service"                → col 4
  "served@payment_service@Order"          → col 10
  "served@payment_service@Refund"         → col 11
```

The evaluation plan includes per-class operations as separate ops referencing separate columns. A 5-node model with 3 classes and 6 measures per node: `5 × 6 × (1 + 3) = 120 columns`. Still trivially small — a 120 × 288 matrix is ~270 KB.

Conservation checks become column arithmetic: for each node, sum the class columns and compare to the total column. If `state[col_arrivals_Order, t] + state[col_arrivals_Refund, t] != state[col_arrivals_total, t]`, emit a warning. No object graph traversal needed.

### Derived metrics as matrix operations

Derived metrics are additional columns computed after the main evaluation plan:

```
DerivedOps = [
  // utilization = served / capacity
  VecDiv(out=20, a=col_served, b=col_capacity),

  // latencyMinutes = queueDepth / served × binMinutes (Little's Law)
  VecDiv(out=21, a=col_queue, b=col_served),
  ScalarMul(out=21, input=21, k=binMinutes),

  // throughputRatio = served / arrivals
  VecDiv(out=22, a=col_served, b=col_arrivals),

  // serviceTimeMs = processingTimeMsSum / servedCount
  VecDiv(out=23, a=col_ptms_sum, b=col_served_count),

  // flowLatencyMs = latencyMinutes × 60000 + serviceTimeMs
  ScalarMul(out=24, input=21, k=60000.0),
  VecAdd(out=24, a=24, b=23),
]
```

These are just more ops appended to the plan. No separate "metrics computation" phase — it's all array operations on the same matrix. The `RuntimeAnalyticalEvaluator`, `CycleTimeComputer`, and `UtilizationComputer` classes collapse into ~15 additional ops in the plan.

For optional series (e.g., `processingTimeMsSum` might not exist), the compiler either omits the derived ops from the plan or uses a sentinel value (NaN) in the column. The plan is generated per-model, so if a model doesn't have service time data, the `serviceTimeMs` ops simply aren't in the plan.

### Conservation checks as matrix operations

The invariant `arrivals + retries - served - ΔQ - dlq ≈ 0` is column arithmetic:

```
for each node with queue semantics:
    for t in 0..bins:
        delta_q = state[col_queue, t] - (if t > 0 { state[col_queue, t-1] } else { init })
        residual = state[col_arrivals, t] + state[col_retries, t]
                 - state[col_served, t] - delta_q - state[col_dlq, t]
        if abs(residual) > tolerance:
            emit warning(node_id, t, residual)
```

The node-to-column mapping comes from the column map. The check iterates the matrix directly — no need to reconstruct objects.

Non-negativity checks: a single scan per column. `if state[col, t] < -tolerance: warn`. Capacity bounds: `if state[col_served, t] > state[col_capacity, t] + tolerance: warn`.

The entire invariant analysis reduces to: given a list of (node_id, relevant_columns), scan those columns and check arithmetic relationships. ~50 lines of code instead of ~400.

### What-if comparison

Two runs of the same model with different parameters produce two matrices of identical shape (same column count, same bin count). Comparison is trivial:

```
delta = matrix_variant - matrix_baseline    // element-wise subtract
```

The result is a matrix where each cell is the difference. To answer "how much did queue depth change at peak?":

```
col = column_map.get("queue@settlement")
peak_delta = max(delta[col, *])
```

To answer "which node had the biggest utilization change?":

```
for each node with utilization column:
    max_change = max(abs(delta[col_utilization, *]))
    report(node, max_change)
```

No object diffing. No recursive comparison. Matrix subtract + column scan.

### Artifact writing

The current artifact format (per-series CSVs with `t,value`) maps directly:

```
for (name, col_index) in column_map:
    write_csv(name, state[col_index, 0..bins])
```

Each row of the matrix IS a series CSV. The SHA-256 hash of each series is the hash of the row bytes. No transformation needed.

`series/index.json` is the column map serialized. `run.json` warnings come from the conservation check pass over the matrix.

### The plan as intermediate representation

The evaluation plan is effectively a **bytecode** for a flow algebra virtual machine. The matrix is the VM's memory. Each op is an instruction.

This opens possibilities:

- **Serialization**: save the plan to disk. Re-execute without recompiling. Compare plans across model versions.
- **Static analysis**: inspect the plan to determine data dependencies without executing. "Which columns does column 5 depend on?" Walk the plan backwards.
- **Optimization**: merge consecutive element-wise ops into fused ops. `VecSub(out=5, a=3, b=4)` followed by `Convolve(out=2, input=5, ...)` could fuse into `ConvolveOfDiff(out=2, a=3, b=4, kernel=...)`. (Premature optimization for now, but the structure supports it.)
- **Incremental evaluation**: if one input changes, re-execute only the ops downstream of that input. The plan's dependency structure makes this trivial to determine.
- **Provenance**: for any cell `state[col, t]`, trace which ops wrote it, which columns those ops read, and so on recursively back to the inputs. Full lineage for free.

### Sizes and performance

Typical FlowTime models:

| Model | Nodes | Series per node | Classes | Total columns | Bins | Matrix size |
|-------|-------|----------------|---------|---------------|------|------------|
| Simple (tutorial) | 4 | 4 | 1 | 16 | 12 | 1.5 KB |
| Medium (incident pipeline) | 8 | 6 | 1 | 48 | 24 | 9 KB |
| Large (multi-class trade settlement) | 12 | 8 | 3 | 384 | 288 | 864 KB |
| Extreme (stress test) | 50 | 10 | 5 | 3,000 | 1,440 | 33 MB |

Even the extreme case fits in L3 cache on modern CPUs. The matrix representation doesn't just fit the domain — it fits the hardware.

Allocation count:
- **Current C#**: 2 allocations per node per evaluation (array + Series wrapper) + dictionary entries. A 12-node model: ~30+ heap allocations.
- **Matrix**: 1 allocation total, regardless of model size. The single `vec![0.0; series_count * bins]` is the only allocation during evaluation.

### What the matrix is NOT

- **Not a sparse matrix.** FlowTime models are small and dense. Every column is populated. Sparse representations add complexity for no benefit at this scale.
- **Not a DataFrame.** No column types, no string columns, no nullable semantics. Every cell is `f64`. Period.
- **Not a tensor.** No batching, no broadcasting, no automatic differentiation. It's a flat 2D array with named rows.
- **Not a replacement for the DAG.** The DAG lives on for authoring, topology visualization, and the API's `/graph` endpoint. The matrix replaces only the evaluation-time data layout.

### Summary of what changes and what doesn't

| Layer | Current | Matrix representation | Changes? |
|-------|---------|----------------------|----------|
| YAML model authoring | YAML → DTO → ModelDefinition | Same — YAML is the authoring format | No |
| DAG (topology, edges, semantics) | Topology record with node/edge lists | Same — still needed for authoring + visualization | No |
| Compilation | ModelCompiler synthesizes nodes, ModelParser creates INode instances | Compiler produces column map + evaluation plan instead of INode instances | **Yes** |
| Evaluation | Graph.Evaluate: topo-sort, INode.Evaluate per node, Series memo dict | Execute plan against flat matrix | **Yes — drastically simpler** |
| Series storage | Series wrapper class with defensive copies | Row in the matrix (no wrapper) | **Yes — eliminated** |
| Derived metrics | Separate CycleTimeComputer, UtilizationComputer classes | Additional ops in the plan | **Yes — merged into plan** |
| Invariant analysis | InvariantAnalyzer walks nodes and series | Column arithmetic on matrix | **Yes — simpler** |
| Artifact writing | Walk nodes, extract series, write CSVs | Walk column map, write matrix rows as CSVs | **Yes — simpler** |
| API responses | Build DTOs from node data + metrics | Build DTOs from column map + matrix | Minor change |
| UI topology view | Reads DAG from `/graph` endpoint | Same — DAG is unchanged | No |
| UI series charts | Reads series from API | Same — series data is the same, just sourced from matrix rows | No |

### New capabilities unlocked by the matrix representation

The matrix + plan representation is not just a different encoding of the same system. It enables capabilities that are hard or impossible with the current object graph, because **the plan is inspectable data, not opaque code**.

#### Incremental re-evaluation

Change one input, re-execute only the downstream ops. The plan's dependency structure tells you exactly which ops are affected.

```
User changes: capacity column (col 1)
Plan analysis: ops reading col 1 → VecMin(out=4, a=1, b=3) → downstream: cols 4, 5, 2, 6, 7
Re-execute: 5 ops instead of 8
```

Today you re-run the entire graph — there's no way to know what's downstream of a given node without tracing the whole object graph at runtime. With the plan, it's a static lookup: scan the op list for any op reading the changed column, then transitively find all ops reading those outputs.

For the what-if workflow this is transformative: "try 20 different capacity values" becomes 1 full evaluation + 19 partial re-evaluations.

#### Automatic sensitivity analysis

"Which parameter matters most for queue depth?"

Perturb each input column by ε, re-run only downstream ops, measure the output delta:

```
for each input column c:
    state_perturbed = state.clone()
    state_perturbed[c, *] *= 1.01                      // +1% perturbation
    replay downstream ops only
    sensitivity[c] = max(abs(state_perturbed[col_queue, *] - state[col_queue, *]))

report: "capacity has 5x more impact on queue depth than arrival volume"
```

With incremental re-evaluation, this is cheap — each perturbation replays a subset of the plan. With the object graph, each perturbation requires a full evaluation from scratch. For 10 parameters, that's potentially 10 full evaluations vs 10 partial replays.

#### Goal seeking / optimization

"What capacity keeps queue depth below 20?"

```
target: state[col_queue, *].max() < 20
variable: state[col_capacity, *]  (constant series, so one scalar)

bisect:
    lo = 50, hi = 500
    while hi - lo > 1:
        mid = (lo + hi) / 2
        set state[col_capacity, *] = mid
        replay downstream ops
        if state[col_queue, *].max() < 20: hi = mid
        else: lo = mid

answer: capacity = 134 keeps queue below 20
```

Each bisection step replays only the downstream ops, not the full evaluation. The plan tells you which ops to replay.

This generalizes: "what failure rate keeps retry tax below 10%?", "what dispatch period minimizes peak latency?", etc. Any scalar parameter can be optimized against any output metric.

#### Batch what-if evaluation

10 capacity values × 10 failure rates = 100 scenarios. Same plan, 100 input matrices:

```
plan = compile(model)                    // compiled once
for (cap, fail_rate) in parameter_grid:
    state = allocate(series_count, bins)
    state[col_capacity, *] = cap
    state[col_failure_rate, *] = fail_rate
    execute(plan, state)
    results.push(state)
```

No compilation cost per variant — the plan is compiled once. Today each variant rebuilds the full `ModelDefinition → INode → Graph` object chain.

This enables parameter sweep visualizations: heatmaps of "queue depth peak vs (capacity, failure_rate)" across the entire parameter space.

#### Provenance / "why is this value 126?"

For any cell `state[col, t]`, walk the plan backwards to produce a complete lineage:

```
query: why is state[col_queue=6, t=9] == 126?

trace:
  QueueRecurrence(out=6, inflow=4, outflow=7, init=0.0)
    → state[6, 9] = max(0, state[6, 8] + state[4, 9] - state[7, 9])
    → state[6, 9] = max(0, 125 + 110 - 109) = 126

  state[4, 9] = 110 ← VecMin(out=4, a=1, b=3)
    → min(state[1, 9], state[3, 9]) = min(110, 110.5) = 110

  state[3, 9] = 110.5 ← VecAdd(out=3, a=0, b=2)
    → state[0, 9] + state[2, 9] = 80 + 30.5 = 110.5

  state[2, 9] = 30.5 ← Convolve(out=2, input=5, kernel=[0, 0.6, 0.3, 0.1])
    → 0.6 × state[5, 8] + 0.3 × state[5, 7] + 0.1 × state[5, 6]
    → 0.6 × 37.8 + 0.3 × 65.0 + 0.1 × 77.6 = 30.5

answer: queue is 126 at bin 9 because:
  - 80 new arrivals + 30.5 retries = 110.5 total demand
  - capacity capped served at 110
  - queue carried 125 from previous bin, added 0.5 net → 126
  - the 30.5 retries came from errors in bins 6-8 via the retry kernel
```

This is data inspection, not runtime debugging. The plan IS the trace — each op directly references its inputs by column index. With the object graph, you'd need to attach debugger breakpoints and step through method calls.

This capability is particularly powerful for FlowTime's explainability goal: every value traces back through named formulas to named inputs.

#### Plan diff / structural comparison

"Did this model change structurally, or just in parameter values?"

```
plan_v1 = compile(model_v1)
plan_v2 = compile(model_v2)

diff(plan_v1, plan_v2):
  ops identical: VecAdd(3, 0, 2), VecMin(4, 1, 3), VecSub(5, 3, 4), Convolve(2, 5, ...)
  ops changed:  Const(1, [110, ...]) → Const(1, [132, ...])    // capacity changed
  ops added:    none
  ops removed:  none

verdict: parameter-only change (capacity 110 → 132), topology unchanged
```

With the object graph, structural comparison requires deep equality across heterogeneous class hierarchies — fragile and incomplete. Plans are flat lists of simple value types — trivially diffable.

#### Model composition

Two models with compatible grids can merge:

```
model_a: payment pipeline (cols 0-7, 8 ops)
model_b: settlement pipeline (cols 0-5, 6 ops)

composed:
  column_map: model_a columns at 0-7, model_b columns at 8-13
  plan: model_a ops ++ model_b ops (with column indices shifted by 8)
  link: model_b's inflow column (8) reads from model_a's served column (4)
        → add Copy(out=8, input=4) between the two plan sections
```

Model A's output becomes Model B's input — just reference the column index. With object graphs, you'd need to wire up cross-graph node references and handle ID collisions.

#### Analytical extensions as plan ops

After the main evaluation, append analysis operations to the same plan:

```
// Cumulative flow diagram: cumulative sum of arrivals and served
CumulativeSum(out=30, input=col_arrivals)   // cumulative arrivals
CumulativeSum(out=31, input=col_served)     // cumulative served
VecSub(out=32, a=30, b=31)                 // WIP = cumulative in - cumulative out

// Rolling average of utilization (window = 3 bins)
RollingAvg(out=33, input=col_utilization, window=3)

// Cross-correlation between arrivals and queue depth
CrossCorrelation(out=34, a=col_arrivals, b=col_queue, max_lag=5)
```

These are just more column operations — same execution model, same matrix. Not a separate "analysis phase" with different abstractions. The plan is open for extension.

#### Summary: new capabilities vs same-thing-different-form

| Capability | Object graph | Matrix + plan | Verdict |
|-----------|-------------|---------------|---------|
| Basic evaluation | Works | Works | Same |
| What-if (full re-run) | Works | Works (faster — no recompile) | Faster |
| Incremental re-evaluation | Impossible (no dependency tracking) | Partial plan replay | **New** |
| Sensitivity analysis | N full evaluations | N partial replays | **New** |
| Goal seeking / optimization | N full evaluations per bisection step | Partial replay per step | **New** |
| Batch what-if (parameter sweeps) | N full compile + evaluate cycles | 1 compile + N executes | **New** |
| Value provenance ("why 126?") | Debugger / runtime tracing | Plan walkback — data inspection | **New** |
| Plan diff / structural comparison | Deep object equality (fragile) | Flat list diff (trivial) | **New** |
| Model composition | Cross-graph wiring (hard) | Column map union + plan concat | **New** |
| Analytical extensions (CFDs, rolling avg) | Separate code path | More ops in the plan | **New** |
| Plan serialization / caching | Serialize object graph (complex) | Serialize flat op list + column map | **New** |

The key shift: the object graph is opaque code. The plan is inspectable data. Once evaluation becomes data, you can analyze, transform, diff, compose, and partially replay it.

---

## Language evaluation

### Rust — top pick

**Why it fits:**
- Algebraic types (`enum Op`) replace class hierarchies — one `match` instead of 6 class files
- Ownership eliminates all defensive copying — `&[f64]` slices are immutable by construction
- `Option<f64>` forces exhaustive handling — no forgotten null checks
- `&self` vs `&mut self` — the function signature IS the purity contract
- First-class SIMD (`std::simd`, `packed_simd`)
- Compiles to WebAssembly — engine in the browser, no API round-trip
- Single static binary — no runtime, no framework, ~5-10 MB Docker images
- Cross-compilation is straightforward

**What to expect:**
- Ownership/borrowing learning curve: ~2 weeks to productive, then "if it compiles, it works"
- Compiler is strict but helpful — suggests fixes
- No GC pauses — consistent request-response latency

**What Rust would find:**
Not bugs producing wrong outputs today, but latent risks held together by convention:

| C# convention (discipline) | Rust guarantee (compiler) |
|----------------------------|--------------------------|
| "Always clone before calling ApplySchedule" | Ownership transfer — can't mutate borrowed data |
| "Series is immutable because we only expose read-only indexer" | `&[f64]` — the slice is immutable, no wrapper needed |
| "Don't access Topology from multiple threads" | `Send`/`Sync` traits — won't compile if unsafe to share |
| "Sanitize nullable doubles before use" | `Option<f64>` — match arms enforced |
| "Graph is immutable after construction" | No `&mut` reference exists after construction |

**Deployment:**
- `cargo build --release` → single static binary, copy to server
- `cargo build --target wasm32-unknown-unknown` → WASM for browser
- Docker images ~5-10 MB vs hundreds for .NET

### F# — runner-up

**Why it fits:**
- Discriminated unions for node types, pipeline operators, immutability by default
- Stays on .NET — preserves API/CLI infrastructure
- Pattern matching, same `dotnet run` / `dotnet test` workflow

**Why not:**
- Second-class citizen in .NET ecosystem — tooling is perpetually "almost good enough"
- VS Code + Ionide: autocomplete drops out, project loading unreliable
- JetBrains Rider is the only good F# IDE
- Microsoft invests minimally, no trajectory improvement
- For a solo developer, tooling friction is a real cost

### Go — no

Lacks generics maturity, no algebraic types, no SIMD intrinsics, poor array ergonomics. Great for network services, wrong for array math.

### APL / J / K / Q — study, don't implement

The computation model is a near-perfect fit. FlowTime's core IS array programming:

```
served ← capacity ⌊ arrivals        ⍝ APL: element-wise min
errors ← arrivals - served
retries ← kernel +.× errors         ⍝ convolution
```

kdb+/q is used in finance for exactly this: vectorized time-series computation against columnar stores. The column store + evaluation plan representation is essentially APL's memory model made explicit in any language.

**Dealbreakers:** tiny ecosystem, impossible hiring, minimal IDE support, write-only syntax, kdb+ is expensive commercial software. Open-source alternatives (GNU APL, J, oK) are hobby-grade for production.

**Takeaway:** APL is the proof that the column store representation is correct. It's a 60-year-old language that got array computation exactly right. Steal the data model, not the syntax.

### Mojo — watch, don't use yet

Python syntax with systems-level performance. Designed for array math, auto-vectorizes to SIMD. Created by Chris Lattner (LLVM, Swift).

**Status:** pre-1.0, language changing rapidly, thin ecosystem, minimal IDE support, deployment story not production-ready. Worth revisiting in 2-3 years.

### Zig — niche

Similar space as Rust (no GC, SIMD, WASM) but simpler, less type-system power. Interesting if Rust's borrow checker feels like overkill.

### TypeScript/Bun — only if

Only makes sense if engine and UI share one language. Adequate performance for the problem size (hundreds of bins). No real SIMD, no algebraic types.

---

## Size estimate

| Layer | C# (current) | Rust (column store + eval plan) |
|-------|-------------|-------------------------------|
| Core engine (eval + nodes + series) | ~3,000 lines | ~400-600 lines |
| Metrics / derived computation | ~1,500 lines | ~800 lines |
| Compilation / parsing | ~2,500 lines | ~1,200 lines |
| Analysis / invariants | ~1,500 lines | ~1,200 lines |
| Models / types / DTOs | ~2,500 lines | ~800 lines |
| I/O / artifacts / data sources | ~2,000 lines | ~1,500 lines |
| **Total** | **~13,000** | **~5,000-6,000** |

~50-55% reduction. The biggest wins are in the core engine (column store replaces class hierarchy) and the type/model layer (`enum` + `struct` + `derive` eliminates boilerplate).

The reduction comes from eliminating defensive code, not from compressing logic:
- ~200 lines of `Series` wrapper: gone (the type system is the protection)
- ~300 lines of null/NaN sanitization: reduced (`Option` forces handling)
- ~400 lines of node class hierarchy: replaced by ~80 lines of `enum Op` + `match`
- ~800 lines of record/class boilerplate: replaced by `derive` macros

---

## SIMD note

Single Instruction, Multiple Data: modern CPUs operate on 4-8 doubles in one clock cycle. Requires contiguous memory layout (which the column store guarantees). For FlowTime's problem sizes (12-288 bins), the SIMD win is nice but not critical — the bigger win is cache-friendly column layout itself.

---

## Recommendation

1. **Highest value, language-independent:** adopt column store + evaluation plan representation. This can be done in C# first.
2. **If switching language:** Rust — for WASM (engine in browser), single-binary deployment, and compile-time correctness guarantees.
3. **Skip:** F# (tooling pain), Mojo (too early), Go (wrong domain).
