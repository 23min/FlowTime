# Epic: Matrix Engine

**ID:** E-20
**Status:** complete (m-E20-01–10 all complete)
**Owner:** Engine

## Goal

Replace the C# object-graph evaluation engine with a Rust-based column-store + evaluation-plan engine. The new engine reads the same YAML model files, produces identical output artifacts, and ships as a standalone CLI binary (`flowtime-engine`). This is the foundation for E-17 (Interactive What-If) and E-18 (Time Machine).

## Context

The current C# engine (`FlowTime.Core`) evaluates models via an object graph: `INode` implementations, `Dictionary<NodeId, Series>` memoization, defensive copies, and class hierarchy dispatch. It works correctly (1287 tests, all passing) but the representation fights the domain:

- **Defensive copying**: `Series` clones on construction AND on `ToArray()` — two allocations per node per evaluation.
- **Hash lookups during evaluation**: `Dictionary<NodeId, Series>` instead of direct array indexing.
- **No incremental re-evaluation**: changing one input requires re-evaluating the entire graph.
- **Feedback subgraphs require special handling**: the bin-by-bin evaluator added in E-10 p3b is correct but is a bridge to the matrix model where bin-sequential evaluation is the default.
- **No plan introspection**: the evaluation is opaque code, not inspectable data.

The research doc (`docs/research/engine-rewrite-language-and-representation.md`) establishes that the engine is fundamentally an array programming problem. All series live in one contiguous matrix (`double[seriesCount × binCount]`), and the evaluation plan is an ordered list of operations that read input columns and write output columns.

### Why Rust

- Ownership eliminates all defensive copying — `&[f64]` slices are immutable by construction.
- `enum Op` + `match` replaces 6 class files and an interface hierarchy with ~80 lines.
- Compiles to native CLI binary (zero runtime, ~5-10 MB) and to WebAssembly (engine in browser for E-17).
- Single allocation during evaluation regardless of model size.
- SIMD opportunity for element-wise operations (70-80% of typical models).

### Why now

E-17 (Interactive What-If) needs incremental re-evaluation (<50ms response). E-18 (Time Machine) needs parameter sweeps (1 compile + N partial replays). Both are economically different with the matrix model vs without it. Building E-17/E-18 on the object graph means building them twice.

The bin-by-bin feedback evaluator from E-10 p3b validates the approach — it IS the matrix model scoped to feedback subgraphs.

## Scope

### In scope

1. **Rust crate** (`flowtime-core`) implementing:
   - YAML model deserialization (same schema as C# `ModelDefinition`)
   - Model compilation: YAML → column map + evaluation plan
   - Topology synthesis (same logic as `ModelCompiler`: queue nodes, retry echo, WIP overflow routing, cycle validation)
   - Expression parsing and compilation to plan ops
   - Evaluation: execute plan against flat matrix
   - Derived metrics as additional plan ops (utilization, latency, throughput ratio, cycle time, flow efficiency, Cv, Kingman)
   - Invariant analysis (conservation checks, non-negativity, capacity bounds) as matrix column arithmetic
   - WIP limit enforcement with overflow routing
   - Feedback subgraph evaluation (bin-by-bin — the default mode for sequential ops)
   - SHIFT-based backpressure (falls out naturally from bin-sequential evaluation)
   - Constraint allocation (proportional allocation when demand > capacity)
   - Router flow materialization
   - Dispatch schedule gating
   - Artifact writing: per-series CSVs, `series/index.json`, `run.json` (warnings, metadata)
   - Tiered validation: schema validation → compilation → analysis (dry-run without artifact writing)

2. **CLI binary** (`flowtime-engine`) with commands:
   - `eval <model.yaml> --output <dir>` — full evaluation + artifact writing
   - `validate <model.yaml>` — tiered validation (schema → compile → analyse), no artifacts
   - `plan <model.yaml>` — print the evaluation plan (for inspection/debugging)
   - stdin/stdout support for pipeline composition

3. **Parity test harness**: a set of reference models (extracted from the C# test suite) with approved output artifacts. The Rust engine must produce bitwise-identical outputs. If discrepancies are found that reveal C# bugs, fix C# and update the approved outputs.

4. **Integration bridge**: the .NET API calls the Rust binary as a subprocess, reads its output artifacts, and serves existing API responses. No API contract changes.

### Out of scope

- API hosting (stays in C#/.NET)
- Topology metadata projection for `/graph` endpoint (stays in C#)
- UI work
- WebAssembly compilation (future — architecture supports it, but not this epic)
- In-process FFI from .NET to Rust (future optimization)
- E-17 interactive features (parameter sessions, push channel)
- E-18 advanced modes (sweep, optimize, fit) — though the plan structure enables them

## Architecture

### Data representation

```
State = f64[series_count × bin_count]    // flat 1D, row-major
```

Each row is a series. Each column is a time bin. All series contiguous in memory.

### Evaluation plan

```rust
enum Op {
    Const { out: usize, values: Vec<f64> },
    VecAdd { out: usize, a: usize, b: usize },
    VecSub { out: usize, a: usize, b: usize },
    VecMul { out: usize, a: usize, b: usize },
    VecDiv { out: usize, a: usize, b: usize },
    VecMin { out: usize, a: usize, b: usize },
    VecMax { out: usize, a: usize, b: usize },
    Clamp { out: usize, val: usize, lo: usize, hi: usize },
    Shift { out: usize, input: usize, lag: usize },
    Convolve { out: usize, input: usize, kernel: Vec<f64> },
    QueueRecurrence { out: usize, inflow: usize, outflow: usize, loss: Option<usize>, init: f64, wip_limit: Option<usize>, overflow_out: Option<usize> },
    DispatchGate { out: usize, input: usize, period: usize, phase: usize, capacity: Option<usize> },
    ScalarAdd { out: usize, input: usize, k: f64 },
    ScalarMul { out: usize, input: usize, k: f64 },
    Floor { out: usize, input: usize },
    Ceil { out: usize, input: usize },
    Round { out: usize, input: usize },
    Mod { out: usize, a: usize, b: usize },
    Step { out: usize, input: usize, threshold: usize },
    Pulse { out: usize, period: usize, phase: usize, amplitude: Option<usize> },
    // Router ops, constraint allocation ops, derived metric ops...
}
```

### Evaluator

```rust
fn evaluate(plan: &[Op], bins: usize, series_count: usize) -> Vec<f64> {
    let mut state = vec![0.0; series_count * bins];
    for op in plan {
        execute(op, &mut state, bins);
    }
    state
}
```

### Column map

Bidirectional mapping between human-readable series names and integer column indices. Produced once during compilation.

### Pipeline

```
YAML model
  │
  │  deserialize
  ▼
ModelDefinition (Rust structs)
  │
  │  compile: topo-sort + assign column indices + emit ops
  ▼
EvaluationPlan + ColumnMap
  │
  │  execute: fill the matrix
  ▼
State Matrix (f64[series_count × bins])
  │
  │  derive: append derived metric ops, re-execute tail
  ▼
Full Matrix (with derived columns)
  │
  │  analyse: conservation checks, warnings
  ▼
Warnings
  │
  │  write: column map tells you which column is which series
  ▼
Artifacts (CSVs + index.json + run.json)
```

## Constraints

- The YAML model schema does not change. Existing models must work unmodified.
- Output artifacts must be bitwise-identical to C# output (modulo discovered C# bugs).
- The Rust crate must compile on Linux x86_64 (devcontainer target). macOS and Windows are nice-to-have.
- No external runtime dependencies (no JVM, no .NET, no Python). Single static binary.
- The .NET API integration must not require changes to API contracts or client code.

## Risks

1. **Expression parser parity**: The C# expression parser (`FlowTime.Expressions`) supports a specific syntax. The Rust parser must match it exactly. Mitigation: extract expression test cases as a shared fixture.
2. **Floating-point parity**: Different compilers may produce different results for the same math. Mitigation: use IEEE 754 double precision, avoid platform-specific optimizations, test with bitwise comparison.
3. **YAML deserialization edge cases**: `serde_yaml` may handle edge cases differently from the C# YAML deserializer. Mitigation: test with all existing model fixtures.
4. **Model compiler complexity**: `ModelCompiler` has significant logic (queue synthesis, retry echo, WIP overflow routing, constraint wiring). Mitigation: port method-by-method with test parity at each step.
5. **Devcontainer Rust toolchain**: Need to add Rust to the devcontainer. Mitigation: `rustup` + `cargo` install is straightforward.

## Success Criteria

- [ ] `flowtime-engine eval` produces bitwise-identical artifacts to C# engine on all reference models
- [ ] `flowtime-engine validate` reports the same warnings/errors as C# validation
- [ ] `flowtime-engine plan` prints a human-readable evaluation plan
- [ ] All reference models evaluate correctly (parity harness green)
- [ ] .NET API can call the Rust binary and serve identical API responses
- [ ] Single static binary, no runtime dependencies
- [ ] Evaluation performance equal to or faster than C# (not a hard gate, but expected)
- [ ] Feedback subgraphs (SHIFT-based backpressure) work without special-casing — bin-sequential evaluation handles them naturally

## Dependencies

- E-10 complete (provides the analytical primitives the Rust engine must reproduce)
- E-16 complete (provides the pure evaluation model the Rust engine implements)

## References

- `docs/research/engine-rewrite-language-and-representation.md` — detailed research and design
- `src/FlowTime.Core/` — current C# engine (source of truth for semantics)
- `src/FlowTime.Core/Compiler/ModelCompiler.cs` — compilation logic to port
- `src/FlowTime.Core/Execution/Graph.cs` — evaluation logic to replace
- `src/FlowTime.Core/Expressions/` — expression system to port
- `tests/FlowTime.Core.Tests/` — test cases defining expected behavior

## Milestones

| ID | Title | Summary | Status |
|----|-------|---------|--------|
| m-E20-01 | Scaffold, types, and parsers | Rust crate, model types with serde, YAML deserialization, expression parser. Reference model fixtures. Devcontainer Rust toolchain. | complete |
| m-E20-02 | Compiler and core evaluator | Column map, topo sort, plan generation for const/expr nodes. Evaluator loop + element-wise ops. First end-to-end parity on simple models. | complete |
| m-E20-03 | Topology and sequential ops | Queue synthesis, QueueRecurrence, Shift, Convolve, DispatchGate, PMF, WIP limits with overflow. Feedback subgraphs (bin-sequential). | complete |
| m-E20-04 | Routing and constraints | Router flow materialization, constraint allocation, multi-class flow distribution — all as plan ops. | complete |
| m-E20-05 | Derived metrics and analysis | Utilization, latency, cycle time, Cv, Kingman as plan ops. Invariant analysis as column arithmetic. Warnings. | complete |
| m-E20-06 | Artifacts, CLI, and integration | CSV/JSON artifact writer. CLI (eval, validate, plan). | complete |
| m-E20-07 | .NET subprocess bridge | SHA256 hashing + manifest.json. RustEngineRunner subprocess bridge. Config switch. Parity tests. | complete |
| m-E20-08 | Full parity harness | All 21 fixtures tested against C# engine. `outputs:` filtering. Green/red parity matrix. | complete |
| m-E20-09 | Per-class decomposition and edge series | Per-class columns, edge metrics, class assignment. Engine core feature-complete. | complete |
| m-E20-10 | Artifact sink parity | Full directory layout. StateQueryService compatible. RunArtifactWriter replaceable. | complete |

### Milestone progression

Each milestone delivers a progressively more capable engine, testable against C# reference output at every stage:

- After **M1**: parse any model YAML and any expression — data layer complete
- After **M2**: evaluate simple models (const + expr) — first correct series output
- After **M3**: evaluate models with queues, SHIFT, WIP, backpressure — core flow dynamics
- After **M4**: evaluate models with routers, constraints, classes — full flow algebra
- After **M5**: produce derived metrics and warnings — full analytical layer
- After **M6**: produce artifacts, run from CLI — standalone binary
- After **M7**: .NET can call the Rust engine as a subprocess — bridge operational
- After **M8**: parity verified against all fixtures — no surprises before core work
- After **M9**: engine returns complete results (classes, edges) — evaluation-complete
- After **M10**: artifact sink produces full layout — C# RunArtifactWriter replaceable, E-17/E-18 unblocked
