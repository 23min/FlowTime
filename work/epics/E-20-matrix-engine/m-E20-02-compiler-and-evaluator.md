# Milestone: Compiler and Core Evaluator

**ID:** m-E20-02
**Epic:** E-20 Matrix Engine
**Status:** in-progress
**Branch:** `milestone/m-E20-02-compiler-and-evaluator` (off `epic/E-20-matrix-engine`)

## Goal

Compile simple FlowTime models (const + expr nodes, no topology) into an evaluation plan, execute the plan against a flat matrix, and produce correct series output. This is the first milestone where the Rust engine computes something — the "hello world" of the matrix model.

## Context

m-E20-01 delivered the Rust workspace, model types with YAML deserialization, and the expression parser. The crate can parse any model and any expression, but cannot compile or evaluate anything.

The C# engine evaluates models via:
1. `ModelParser.ParseNodes()` — creates `INode` instances from `NodeDefinition`
2. `Graph(nodes)` — topological sort
3. `Graph.Evaluate(grid)` — iterate in topo order, each node produces a `Series`

The matrix engine replaces this with:
1. Compiler: assign column indices, emit ops from node definitions
2. Evaluator: iterate ops, execute against flat `f64[]` matrix

## Acceptance Criteria

1. **AC-1: ColumnMap.** Bidirectional mapping between series names (strings) and column indices (usize). `name_to_index()` and `index_to_name()`. Constructed during compilation.

2. **AC-2: Op enum and evaluator.** `Op` enum with variants for the element-wise operations needed by const + expr models:
   - `Const { out, values }` — write constant values to a column
   - `VecAdd { out, a, b }`, `VecSub`, `VecMul`, `VecDiv` — element-wise binary ops
   - `ScalarMul { out, input, k }`, `ScalarAdd { out, input, k }` — scalar ops
   - `VecMin { out, a, b }`, `VecMax { out, a, b }` — element-wise min/max
   - `Clamp { out, val, lo, hi }` — clamp to range
   - `Mod { out, a, b }` — modulo
   - `Floor { out, input }`, `Ceil { out, input }`, `Round { out, input }` — rounding
   - `Step { out, input, threshold }` — step function
   - `Pulse { out, period, phase, amplitude }` — periodic pulse
   
   Evaluator function: `fn evaluate(plan: &[Op], bins: usize, series_count: usize) -> Vec<f64>` — allocates matrix, iterates ops, returns filled matrix.

3. **AC-3: Expression compiler.** Compile an expression AST (`Expr`) into a sequence of `Op`s given a `ColumnMap`. Each binary op and function call emits one or more ops, using temporary columns for intermediate results. Node references resolve to column indices via the ColumnMap.

4. **AC-4: Model compiler (const + expr).** `fn compile(model: &ModelDefinition) -> Result<(Plan, ColumnMap), CompileError>`:
   - Assigns a column index to each node's output series.
   - Topological sort based on expression dependencies.
   - Emits `Const` ops for `kind: "const"` nodes.
   - Emits expression ops for `kind: "expr"` nodes.
   - Returns the plan (ordered ops) and column map.

5. **AC-5: End-to-end evaluation.** `fn eval_model(model: &ModelDefinition) -> Result<EvalResult, Error>` that compiles and evaluates, returning named series. Test with the `hello.yaml` fixture:
   - `demand` = [10, 10, 10, 10, 10, 10, 10, 10]
   - `served` = demand * 0.8 = [8, 8, 8, 8, 8, 8, 8, 8]

6. **AC-6: Parity with C# on simple models.** Create a parity test that evaluates a model with both the Rust engine and the C# engine (via pre-computed reference outputs) and compares series values. At minimum:
   - Const-only model: all series match
   - Const + expr model: expression results match (binary ops, scalar multiply)
   - Nested expressions: `MIN(a, b)`, `MAX(a, b)`, `CLAMP(x, lo, hi)`
   - Multiple dependent expressions (chain: a → b → c)

7. **AC-7: Plan inspection.** `fn format_plan(plan: &Plan, column_map: &ColumnMap) -> String` that prints a human-readable plan. The CLI `flowtime-engine plan <model.yaml>` command uses this. Output shows op type, column names (not just indices).

## Technical Notes

- **Matrix layout:** Row-major `Vec<f64>` of size `series_count * bins`. Column `c` at bin `t` is at index `c * bins + t`. All bins for one series are contiguous.
- **Temporary columns:** Expression compilation may need intermediate columns (e.g., `a + b` in `(a + b) * c` needs a temp column for `a + b`). The compiler allocates these from the column map with generated names like `__temp_0`.
- **Topo sort:** Collect dependencies from expression AST (node references). Kahn's algorithm (same as C#). Reject cycles.
- **Fixture update:** `simple-const.yaml` uses legacy field `expression` instead of `expr`. Update the fixture to use `expr` so it works with the Rust model types.
- **No topology:** This milestone handles flat node lists only. Topology synthesis (serviceWithBuffer queue nodes) comes in m-E20-03.
- **PMF nodes:** `kind: "pmf"` computes a constant expected value from the distribution. Can be included here as a simple op, or deferred to m-E20-03. Include if straightforward.

## Out of Scope

- Topology synthesis (queue nodes, retry echo) — m-E20-03
- Sequential ops (QueueRecurrence, Shift, Convolve, DispatchGate) — m-E20-03
- Routing and constraints — m-E20-04
- Derived metrics — m-E20-05
- Artifact writing (CSVs, JSON) — m-E20-06
- SHIFT/feedback handling — m-E20-03

## Dependencies

- m-E20-01 complete (model types + expression parser)
