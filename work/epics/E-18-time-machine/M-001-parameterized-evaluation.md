---
id: M-001
title: Parameterized Evaluation
status: done
parent: E-18
---

## Goal

The Rust engine can compile a model once and re-evaluate it many times with different parameter values without recompiling. This is the critical primitive that every downstream use case builds on — interactive what-if, parameter sweeps, optimization, sensitivity analysis. The Plan becomes a reusable program; parameters are its inputs.

## Context

The current `compile(model) → Plan` bakes all constants into `Op::Const { out, values }` at compile time. To change an arrival rate from 10 to 15, you must recompile the entire model. Compilation is O(nodes) with topological sorting, expression parsing, and constraint resolution — unnecessary work when only a scalar value changed.

After this milestone, the Plan carries a `ParamTable` that lists every user-visible constant. `evaluate_with_params(plan, overrides)` writes overrides into the state matrix before the eval loop, then runs the same bin-major evaluation. The Plan is immutable and shareable; only the parameter values change.

### Where constants come from in the compiler

The compiler creates `Op::Const` from seven sources:

| Source | Example | Parameter? |
|--------|---------|-----------|
| `kind: const` node values | `values: [10, 20, 30]` | Yes — primary user input |
| Traffic arrival `ratePerBin` | `ratePerBin: 20` | Yes — class arrival rate |
| PMF expected value | `pmf: { values, probabilities }` | Yes — derived from PMF definition |
| WIP limit scalar | `wipLimit: 50` | Yes — topology constraint |
| Queue initial condition | `initialCondition: { queueDepth: 5 }` | Yes — initial state |
| Expression literal | `8` in `MIN(arrivals, 8)` | Yes — inline constant in formula |
| Compiler-generated temps | Internal proportional alloc, router weight columns | No — derived, not user-visible |

The distinction: a parameter is a constant that traces back to a user-authored value in the model YAML. Compiler-generated intermediate constants (temp columns, normalized weights) are NOT parameters.

## Acceptance Criteria

1. **AC-1: ParamTable struct.** `Plan` gains a `params: ParamTable` field. `ParamTable` contains a `Vec<ParamEntry>` where each entry has:
   - `id: String` — stable identifier matching the model YAML source (e.g., `"arrivals"` for a const node, `"arrivals.Order"` for a traffic class rate, `"Queue.wipLimit"` for a topology WIP limit)
   - `column: usize` — the column index in the state matrix this parameter fills
   - `default: ParamValue` — original value from the model (`Scalar(f64)` for uniform, `Vector(Vec<f64>)` for per-bin)
   - `kind: ParamKind` — `ConstNode`, `ArrivalRate`, `WipLimit`, `InitialCondition`, `ExprLiteral`

2. **AC-2: Compiler populates ParamTable.** The compiler registers parameters for:
   - Every `kind: const` node (id = node id, value from `values` field)
   - Every `traffic.arrivals` entry with `ratePerBin` (id = `"{nodeId}.{classId}"`)
   - Every topology node with scalar `wipLimit` (id = `"{topoNodeId}.wipLimit"`)
   - Every topology node with `initialCondition.queueDepth` (id = `"{topoNodeId}.init"`)
   - Expression literals are NOT parameters (they're inline formula constants, not model inputs)

3. **AC-3: `evaluate_with_params` function.** New public function:
   ```rust
   pub fn evaluate_with_params(plan: &Plan, overrides: &[(String, ParamValue)]) -> Vec<f64>
   ```
   - Applies overrides to matching param IDs before the eval loop
   - `Scalar(v)` fills all bins with `v`; `Vector(vs)` writes per-bin values
   - Unmatched override IDs are ignored (forward-compatible)
   - Unknown param IDs do not cause errors
   - Returns the filled state matrix (same shape as `evaluate`)

4. **AC-4: Equivalence.** `evaluate_with_params(plan, &[])` (no overrides) produces identical results to `evaluate(plan)`. A Rust test asserts bitwise equality.

5. **AC-5: Full post-eval pipeline.** `eval_model` is refactored to accept optional overrides. When overrides are provided, it calls `evaluate_with_params` instead of `evaluate`, then runs the same post-eval pipeline: class decomposition normalization, proportional allocation propagation, edge series computation, analysis warnings. A new public entry point:
   ```rust
   pub fn eval_model_with_params(
       model: &ModelDefinition,
       overrides: &[(String, ParamValue)]
   ) -> Result<EvalResult, CompileError>
   ```

6. **AC-6: Parameter override affects downstream.** Overriding a const node's value propagates through all downstream expressions, queue recurrences, per-class decomposition, and edge series. Test: override `arrivals` from 10 to 20 → verify `served`, `queue_depth`, per-class series, and edge flow all change correctly.

7. **AC-7: Class arrival rate override.** Overriding a class arrival rate (e.g., `"arrivals.Order"` from 6 to 12) changes the class fraction and propagates through normalization and downstream decomposition. Test: change one class rate, verify normalization invariant still holds.

8. **AC-8: WIP limit override.** Overriding `"{topoNodeId}.wipLimit"` changes the queue's WIP limit and affects overflow. Test: lower WIP limit → verify overflow increases.

9. **AC-9: Parameter schema extraction.** New public function:
   ```rust
   pub fn extract_params(plan: &Plan) -> &ParamTable
   ```
   Returns the plan's parameter table. Clients use this to discover what can be tweaked, with IDs, kinds, and defaults. This is what the UI will use to auto-generate controls.

10. **AC-10: Compile-once, eval-many pattern.** Demonstrate the pattern with a Rust test that compiles once, evaluates 10 times with different arrival rates, and verifies each result is independent (no state leakage between evaluations). Measure that subsequent evals are faster than the first (no recompilation).

## Out of Scope

- Session management or persistent process (M-002)
- Streaming protocol or MessagePack framing (M-002)
- CLI interface changes (M-002)
- UI parameter controls (M-019)
- Parameter bounds, display names, or template metadata enrichment (future — the parameter table carries IDs and defaults only)
- Expression literal parameterization (inline `8` in `MIN(arrivals, 8)` stays baked — parameterizing expression constants requires expression-tree rewriting, which is a different problem)
- Structural model changes (adding/removing nodes requires recompilation — by design)

## Key References

- `engine/core/src/plan.rs` — Plan struct, Op enum, ColumnMap
- `engine/core/src/eval.rs` — `evaluate()` function, bin-major loop
- `engine/core/src/compiler.rs` — `compile()`, `eval_model()`, all `Op::Const` emission sites
- `docs/architecture/headless-engine-architecture.md` — overall architecture
- `work/epics/E-18-headless-pipeline-and-optimization/milestone-plan-v2.md` — milestone sequence
