---
id: M-002
title: Engine Session + Streaming Protocol
status: done
parent: E-18
depends_on:
  - M-001
acs:
  - id: AC-1
    title: '**AC-1: `session` CLI command.** `flowtime-engine session` enters a persistent loop reading from stdin and writing
      to stdout. No file arguments required. Exits cleanly on stdin EOF or SIGTERM.'
    status: met
  - id: AC-2
    title: '**AC-2: Length-prefixed MessagePack framing.** Each message is `[4-byte big-endian length][MessagePack payload]`.
      Both requests (stdin) and responses (stdout) use this framing. Stderr is reserved for human-readable log messages (not
      protocol).'
    status: met
  - id: AC-3
    title: '**AC-3: `compile` command.** Request: `{ method: "compile", params: { yaml: "<model YAML>" } }`. Response: `{
      result: { params: [{ id, kind, default }], series: [{ id, bins, values }], bins, grid } }`. Compiles the model, holds
      the Plan in session state, evaluates with defaults, returns the parameter schema and initial series.'
    status: met
  - id: AC-4
    title: '**AC-4: `eval` command.** Request: `{ method: "eval", params: { overrides: { "arrivals": 15.0, "Queue.wipLimit":
      30.0 } } }`. Response: `{ result: { series: { "arrivals": <f64 array>, "served": <f64 array>, ... }, elapsed_us } }`.
      Re-evaluates with overrides, returns updated series. Must not recompile. Series values are MessagePack binary arrays
      (not JSON text arrays).'
    status: met
  - id: AC-5
    title: '**AC-5: `get_params` command.** Request: `{ method: "get_params" }`. Response: `{ result: { params: [{ id, kind,
      default }] } }`. Returns the current parameter table from the compiled Plan.'
    status: met
  - id: AC-6
    title: '**AC-6: `get_series` command.** Request: `{ method: "get_series", params: { names: ["arrivals", "served"] } }`.
      Response: `{ result: { series: { "arrivals": <f64 array>, "served": <f64 array> } } }`. Returns specific series from
      the current evaluation state. If no names provided, returns all non-internal series.'
    status: met
  - id: AC-7
    title: '**AC-7: Error handling.** Invalid requests return `{ error: { code, message } }`. Specific errors: `not_compiled`
      (eval before compile), `compile_error` (bad YAML), `unknown_method`. The session continues after errors — it does not
      exit.'
    status: met
  - id: AC-8
    title: '**AC-8: Session state.** The session holds: compiled Plan, current parameter overrides, current state matrix (from
      most recent eval). `compile` replaces the entire session state. `eval` updates overrides and state. Multiple `eval`
      calls are independent (no accumulation).'
    status: met
  - id: AC-9
    title: '**AC-9: Performance.** For a model with 8 bins and ~10 series, `eval` with scalar overrides completes in under
      1ms (excluding I/O). A Rust benchmark test evaluates 1,000 times in a loop and asserts total < 1 second.'
    status: met
  - id: AC-10
    title: '**AC-10: Integration test.** A Rust integration test spawns `flowtime-engine session` as a subprocess, sends compile
      + eval + eval (with different overrides) + get_params via the MessagePack protocol over stdin/stdout, and verifies all
      responses are correct.'
    status: met
---

## Goal

The Rust engine runs as a persistent process that accepts commands and streams results. `flowtime-engine session` reads length-prefixed MessagePack messages from stdin, holds a compiled Plan in memory, and writes responses to stdout. This is the headless pipeline component — the same protocol works over stdin/stdout (CLI pipes) and WebSocket (UI, via M-018 proxy).

## Context

After M-001, the engine can compile once and evaluate many times with different parameters via `evaluate_with_params(plan, overrides)`. But every invocation is still a batch subprocess: spawn → parse YAML → compile → evaluate → write files → exit. The overhead of process spawn + file I/O dominates latency (100-500ms). For interactive use, we need a persistent process that holds the compiled Plan and responds to parameter changes in microseconds.

The session is a stateful loop:

```
stdin → [compile] → hold Plan → [eval overrides] → stdout
                               → [eval overrides] → stdout
                               → [eval overrides] → stdout
                               → EOF → exit
```

### Why MessagePack

- **Binary f64 arrays.** A 1,000-bin series is 8KB as binary vs ~8KB+ as JSON text (with formatting overhead and parse cost). MessagePack encodes `Vec<f64>` as a binary ext type — zero parsing, memcpy-fast.
- **Length-prefixed framing.** 4-byte big-endian length prefix before each message. No newline ambiguity, no incomplete-line bugs.
- **Cross-language.** Native libraries: Rust (`rmp-serde`), JavaScript (`@msgpack/msgpack`), C# (`MessagePack-CSharp`), Python (`msgpack`).
- **Pipe-friendly.** Works over stdin/stdout for CLI composition, over WebSocket for UI.

## Acceptance criteria

### AC-1 — **AC-1: `session` CLI command.** `flowtime-engine session` enters a persistent loop reading from stdin and writing to stdout. No file arguments required. Exits cleanly on stdin EOF or SIGTERM.

### AC-2 — **AC-2: Length-prefixed MessagePack framing.** Each message is `[4-byte big-endian length][MessagePack payload]`. Both requests (stdin) and responses (stdout) use this framing. Stderr is reserved for human-readable log messages (not protocol).

### AC-3 — **AC-3: `compile` command.** Request: `{ method: "compile", params: { yaml: "<model YAML>" } }`. Response: `{ result: { params: [{ id, kind, default }], series: [{ id, bins, values }], bins, grid } }`. Compiles the model, holds the Plan in session state, evaluates with defaults, returns the parameter schema and initial series.

### AC-4 — **AC-4: `eval` command.** Request: `{ method: "eval", params: { overrides: { "arrivals": 15.0, "Queue.wipLimit": 30.0 } } }`. Response: `{ result: { series: { "arrivals": <f64 array>, "served": <f64 array>, ... }, elapsed_us } }`. Re-evaluates with overrides, returns updated series. Must not recompile. Series values are MessagePack binary arrays (not JSON text arrays).

### AC-5 — **AC-5: `get_params` command.** Request: `{ method: "get_params" }`. Response: `{ result: { params: [{ id, kind, default }] } }`. Returns the current parameter table from the compiled Plan.

### AC-6 — **AC-6: `get_series` command.** Request: `{ method: "get_series", params: { names: ["arrivals", "served"] } }`. Response: `{ result: { series: { "arrivals": <f64 array>, "served": <f64 array> } } }`. Returns specific series from the current evaluation state. If no names provided, returns all non-internal series.

### AC-7 — **AC-7: Error handling.** Invalid requests return `{ error: { code, message } }`. Specific errors: `not_compiled` (eval before compile), `compile_error` (bad YAML), `unknown_method`. The session continues after errors — it does not exit.

### AC-8 — **AC-8: Session state.** The session holds: compiled Plan, current parameter overrides, current state matrix (from most recent eval). `compile` replaces the entire session state. `eval` updates overrides and state. Multiple `eval` calls are independent (no accumulation).

### AC-9 — **AC-9: Performance.** For a model with 8 bins and ~10 series, `eval` with scalar overrides completes in under 1ms (excluding I/O). A Rust benchmark test evaluates 1,000 times in a loop and asserts total < 1 second.

### AC-10 — **AC-10: Integration test.** A Rust integration test spawns `flowtime-engine session` as a subprocess, sends compile + eval + eval (with different overrides) + get_params via the MessagePack protocol over stdin/stdout, and verifies all responses are correct.
## Technical Notes

### Dependencies to add

- `rmp-serde` (MessagePack serialization for Rust) — workspace dependency
- `serde` derive on request/response types

### Module structure

- `engine/core/src/session.rs` — Session struct, state management, command dispatch
- `engine/core/src/protocol.rs` — Request/Response types, MessagePack framing (read/write)
- `engine/cli/src/main.rs` — `cmd_session()` entry point

### Message envelope

```rust
#[derive(Serialize, Deserialize)]
struct Request {
    method: String,
    #[serde(default)]
    params: serde_json::Value, // flexible params per method
}

#[derive(Serialize)]
struct Response {
    #[serde(skip_serializing_if = "Option::is_none")]
    result: Option<serde_json::Value>,
    #[serde(skip_serializing_if = "Option::is_none")]
    error: Option<ErrorInfo>,
}
```

Note: We use `serde_json::Value` as the flexible inner type even though the wire format is MessagePack. MessagePack and JSON share the same data model (maps, arrays, strings, numbers, bools, null). `rmp-serde` serializes/deserializes `serde_json::Value` correctly.

### Series encoding

Series data (`Vec<f64>`) serializes naturally as MessagePack arrays of floats. For very large series, a future optimization could use MessagePack binary ext type for raw f64 bytes, but the standard array encoding is correct and sufficient for this milestone.

### Post-eval pipeline

After `evaluate_with_params`, the session must also run:
- Class decomposition normalization + proportional allocation
- Edge series computation
- Analysis warnings

This means the session calls the same post-eval pipeline as `eval_model_with_params`. The simplest approach: the session stores the compiled Plan and the ModelDefinition, and each `eval` call runs `eval_model_with_params` reusing the model but with the new overrides.

For the compile-once optimization (skip recompilation), a future milestone can cache the Plan separately. For now, recompiling per eval is acceptable if latency is under the AC-9 target.

## Out of Scope

- WebSocket transport (M-018)
- .NET bridge for session mode (M-018)
- UI parameter controls (M-019)
- Parameter sweep batch mode (m-E18-03)
- Request IDs / multiplexing (single-client, sequential for now)
- Authentication or access control
- TLS/encryption

## Key References

- `engine/core/src/compiler.rs` — `compile()`, `eval_model_with_params()`
- `engine/core/src/plan.rs` — `ParamTable`, `ParamValue`
- `engine/core/src/eval.rs` — `evaluate_with_params()`
- `engine/cli/src/main.rs` — existing CLI command dispatch
- `docs/architecture/headless-engine-architecture.md` — protocol design
- [rmp-serde crate](https://crates.io/crates/rmp-serde) — MessagePack for Rust
- [MessagePack spec](https://msgpack.org/) — wire format
