# Headless Engine Architecture

**Status:** Draft — for discussion
**Date:** 2026-04-10
**Context:** E-20 complete. The Rust engine is a batch subprocess. We need parameterized evaluation and a streaming protocol so the engine becomes a pipeline component that the UI (and other clients) consume as a stream.

## Problem

The current architecture is request-response batch:

```
UI → HTTP POST → .NET API → spawn Rust subprocess → write YAML → eval → write CSVs → read back → HTTP response → UI renders
```

Round-trip: 100-500ms. No way to tweak a parameter and see instant updates. No way to compose the engine into pipelines.

## Target

The engine is a **persistent process** that:
1. Compiles a model once, holds the Plan in memory
2. Accepts parameter patches, re-evaluates in microseconds
3. Streams results to connected clients (UI, CLI, pipelines)

Clients are **streaming consumers**. The UI is one such client. A CLI pipeline is another. An optimization loop is a third.

## Design Principles

- **The engine is a server, not a tool.** It stays alive, holds state, accepts commands.
- **The protocol is the product.** It must be clean enough that non-FlowTime clients can consume it.
- **Parameters are first-class.** The engine knows which Const ops are tweakable and what their bounds are.
- **Streaming, not polling.** When state changes, clients are notified. They don't ask.

## Layer 1: Parameterized Evaluation

### Current Plan structure

```rust
pub struct Plan {
    pub ops: Vec<Op>,        // includes Op::Const { out, values }
    pub column_map: ColumnMap,
    pub bins: usize,
}
```

Constants are baked into the op list at compile time. To re-evaluate with different constants, you must recompile.

### Proposed: Parameter Table

```rust
pub struct Plan {
    pub ops: Vec<Op>,
    pub column_map: ColumnMap,
    pub bins: usize,
    pub params: ParamTable,  // NEW
}

pub struct ParamTable {
    pub entries: Vec<ParamEntry>,
}

pub struct ParamEntry {
    pub id: String,           // e.g. "arrivals.rate", "capacity"
    pub column: usize,        // which column this parameter fills
    pub default: ParamValue,  // original value from model
    pub kind: ParamKind,      // scalar (fill all bins) or vector (per-bin)
}

pub enum ParamValue {
    Scalar(f64),              // fill all bins with this value
    Vector(Vec<f64>),         // per-bin values
}

pub enum ParamKind {
    Scalar,   // node kind=const with uniform values
    Vector,   // node kind=const with varying values
    Rate,     // traffic.arrivals ratePerBin
}
```

The compiler identifies which `Op::Const` ops come from user-visible model inputs (const nodes, traffic arrival rates, WIP limits, etc.) and registers them in the ParamTable. Derived constants (compiler-generated temps, WIP limit columns) are NOT parameters.

### Evaluation with overrides

```rust
pub fn evaluate_with_params(plan: &Plan, overrides: &[(String, ParamValue)]) -> Vec<f64> {
    // 1. Allocate state matrix
    // 2. Write Const ops (using overrides where provided, defaults otherwise)
    // 3. Run bin-major loop (unchanged)
    // 4. Return state
}
```

This is a pure function. No recompilation. The Plan is immutable and shared.

## Layer 2: Engine Session

A **session** holds a compiled Plan and accepts commands:

```
Session {
    model: ModelDefinition,
    plan: Plan,
    current_state: Vec<f64>,
    current_params: HashMap<String, ParamValue>,
}
```

Commands:
- `compile(yaml)` → compiles model, returns parameter schema + initial state
- `eval(overrides)` → re-evaluates with parameter overrides, returns updated series
- `get_params()` → returns current parameter values and metadata
- `get_series(names)` → returns specific series from current state
- `subscribe(filter)` → stream updates when state changes

## Layer 3: Protocol

### Requirements

- Binary-efficient for series data (thousands of f64 values per series)
- Streaming (server pushes results, client doesn't poll)
- Composable (stdin/stdout for pipes, WebSocket for UI)
- Language-neutral (not Rust-specific or .NET-specific)

### Recommendation: Length-prefixed MessagePack over stdio / WebSocket

**Why not JSONL:**
- JSON encodes f64 arrays as text — 100 doubles = ~800 bytes JSON vs ~800 bytes binary. Not a huge win for small models, but 10,000 bins × 50 series = significant.
- JSONL has no framing — incomplete messages are ambiguous.
- No schema — every client re-discovers the shape.

**Why not gRPC:**
- Heavyweight infrastructure (HTTP/2, codegen, tonic/protobuf)
- Poor fit for stdin/stdout pipe composition
- Good for service mesh, overkill for single-process IPC

**Why MessagePack:**
- Binary, compact, self-describing (like JSON but binary)
- Zero-copy deserialization possible
- Native support in Rust (`rmp-serde`), JavaScript (`@msgpack/msgpack`), C# (`MessagePack-CSharp`), Python (`msgpack`)
- Simple framing: 4-byte length prefix + MessagePack payload
- Series data: f64 arrays encoded as binary ext type (not array-of-numbers)

**Transport:**
- **stdin/stdout** for CLI pipelines: `cat model.yaml | flowtime-engine session`
- **WebSocket** for UI: `.NET API proxies WebSocket → engine process stdin/stdout`
- Same protocol, different transport.

### Message format

```
Request:
{
  "id": 1,
  "method": "eval",
  "params": {
    "overrides": { "arrivals": 15.0, "capacity": 8.0 }
  }
}

Response:
{
  "id": 1,
  "result": {
    "series": {
      "arrivals": <binary f64 array>,
      "served": <binary f64 array>,
      "queue_depth": <binary f64 array>
    },
    "warnings": [...],
    "elapsed_us": 42
  }
}
```

## Layer 4: UI as Streaming Client

The Svelte UI connects via WebSocket to the .NET API, which proxies to the engine session:

```
Svelte UI ←WebSocket→ .NET API ←stdin/stdout→ Rust engine session
```

UI flow:
1. Load model → `compile` → receive parameter schema + initial series
2. Render topology graph + charts from initial series
3. User drags a slider → `eval({ arrivals: 15 })` → receive updated series
4. Svelte store updates → reactive chart/graph re-render

The parameter panel is auto-generated from the parameter schema returned by `compile`.

## Implementation Sequence

### Epic: E-21 Headless Engine & Interactive Pipeline

| # | Milestone | Scope |
|---|-----------|-------|
| 1 | Parameterized evaluation | ParamTable in Plan, `evaluate_with_params()`, parameter extraction from model. Rust tests. |
| 2 | Engine session + protocol | Session struct, MessagePack framing, `compile`/`eval`/`get_params` commands. `flowtime-engine session` CLI mode. |
| 3 | .NET bridge + WebSocket proxy | .NET API manages engine session process, proxies WebSocket ↔ stdin/stdout. |
| 4 | Svelte parameter panel | Auto-generated parameter controls from schema. WebSocket connection. Reactive chart updates. |
| 5 | Pipeline composition | File-based scenarios, batch parameter sweeps, `--scenario` flag. |

### What changes, what doesn't

- `compile()` and `evaluate()` stay as they are (backward compatible)
- New `evaluate_with_params()` is additive
- `flowtime-engine eval` CLI mode unchanged (batch, file-based)
- New `flowtime-engine session` CLI mode (persistent, streaming)
- Existing .NET `RustEngineRunner` unchanged (batch bridge)
- New .NET `RustEngineSession` (persistent bridge)

## Open Questions

1. **Parameter granularity:** Should traffic arrival rates be individual parameters, or one "arrivals" parameter per source node? The model YAML has `ratePerBin: 20` — is that one parameter or one per class?

2. **Delta vs full state:** When a parameter changes, send all series or only changed ones? Dependency tracking could identify which columns are downstream of the changed parameter, but adds complexity.

3. **Topology graph updates:** If a parameter change doesn't alter the graph structure (just values), the topology visualization only needs updated heatmap colors, not a re-layout. The protocol should distinguish structural vs. value-only changes.

4. **Multi-client sessions:** Can multiple UIs connect to the same session? Useful for collaborative what-if, but adds concurrency complexity.
