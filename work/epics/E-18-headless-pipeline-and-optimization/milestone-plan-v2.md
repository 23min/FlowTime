# E-18 / E-17 Milestone Plan v2 — Rust Engine Reality

**Date:** 2026-04-10
**Context:** E-20 complete. The Rust engine is the evaluation path. The original E-18 milestone plan assumed C# Core was the engine and front-loaded Generator extraction + C# refactoring. With the Rust engine, the foundation layer is Rust-native.

**User goal:** Parameterized evaluation → streaming engine as pipeline component → Svelte UI as streaming client. No shortcuts. No backward-compatibility tax.

## What changes from the original plan

| Original milestone | What happens |
|---|---|
| m-E18-01a (Generator extraction) | **Deferred.** Not on the critical path for interactive/headless. Generator stays alive for now; the Rust engine is the new execution path alongside it. |
| m-E18-01b (ITelemetrySource, tiered validation) | **Deferred.** Telemetry contracts not needed for parameter tweaking. Tiered validation moves to a later milestone. |
| m-E18-01c (Runtime parameter foundation) | **Becomes m-E18-01.** The foundation, but in Rust, not C#. |
| m-E18-02 (CLI/sidecar) | **Becomes m-E18-02.** Session mode with streaming protocol. |
| m-E18-03+ (sweep, optimize, fit) | **Renumbered.** Sweep is m-E18-03. Others follow. |

## Milestone Sequence

### E-18: Headless Engine Foundation (Rust)

| # | ID | Title | Depends on | Summary |
|---|---|---|---|---|
| 1 | m-E18-01 | Parameterized Evaluation | E-20 (done) | ParamTable in Plan. Compiler extracts tweakable parameters from const nodes, traffic arrivals, WIP limits. `evaluate_with_params(plan, overrides)` pure function. Parameter metadata (id, kind, default, bounds). |
| 2 | m-E18-02 | Engine Session + Streaming Protocol | m-E18-01 | `flowtime-engine session` persistent CLI mode. Length-prefixed MessagePack over stdin/stdout. Commands: `compile`, `eval`, `patch`, `get_params`, `get_series`. Session holds compiled Plan + current state. |
| 3 | m-E18-03 | Parameter Sweep | m-E18-01 | `flowtime-engine sweep` batch mode. Scenario grid definition (JSON). N evaluations without recompile. Tabular output. |

### E-17: Interactive What-If (Bridge + Svelte UI)

| # | ID | Title | Depends on | Summary |
|---|---|---|---|---|
| 4 | m-E17-01 | WebSocket Engine Bridge | m-E18-02 | .NET API manages persistent engine session process. WebSocket endpoint proxies client ↔ engine session protocol. Svelte UI connects via WebSocket. |
| 5 | m-E17-02 | Svelte Parameter Panel | m-E17-01 | Auto-generated parameter controls (sliders, numeric inputs) from parameter schema. WebSocket send on change. Svelte stores for reactive series data. |
| 6 | m-E17-03 | Live Topology & Charts | m-E17-02, E-11 M3 | Topology heatmap + time-series charts reactively update when series store changes. Value-only updates (no graph re-layout). |

### Later E-18 milestones (not immediate)

| # | ID | Title | Depends on | Summary |
|---|---|---|---|---|
| 7 | m-E18-04 | Sensitivity Analysis | m-E18-03 | Numerical gradient: perturb each parameter, measure output change. |
| 8 | m-E18-05 | Optimization & Fitting | m-E18-03, Telemetry Loop | Objective-based optimization + model fitting against observed data. |
| 9 | m-E18-06 | Tiered Validation | m-E18-02 | Schema/compile/analyze tiers via session protocol. Client-agnostic. |
| 10 | m-E18-07 | Generator Extraction | m-E18-02 | Migrate FlowTime.Generator → FlowTime.TimeMachine. Delete Generator. |

## Detailed milestone descriptions

### m-E18-01: Parameterized Evaluation

**The critical primitive.** Everything else builds on this.

The Plan currently bakes constants into `Op::Const { out, values }` at compile time. This milestone separates "model structure" from "tweakable values."

**Acceptance criteria:**
1. `ParamTable` struct in Plan: lists all user-visible parameters with id, column, default value, kind (scalar/vector/rate).
2. Compiler extracts parameters from: const nodes, traffic arrival rates, WIP limits, initial conditions.
3. `evaluate_with_params(plan, overrides: &[(id, ParamValue)])` — re-evaluates with parameter overrides without recompilation.
4. Parameters have metadata: id (matches model YAML node/field), display name, kind, default value.
5. Round-trip: `evaluate(plan)` produces identical results to `evaluate_with_params(plan, &[])` (no overrides = defaults).
6. Normalization invariant still holds after parameter override.
7. Post-eval class decomposition and edge series recomputed with overridden values.

### m-E18-02: Engine Session + Streaming Protocol

**The pipeline component.** A persistent Rust process that holds a compiled Plan and streams results.

**Acceptance criteria:**
1. `flowtime-engine session` CLI mode: reads commands from stdin, writes responses to stdout.
2. Length-prefixed MessagePack framing (4-byte big-endian length + payload).
3. Commands: `compile` (YAML → parameter schema + initial series), `eval` (overrides → updated series), `get_params` (→ current parameter values), `get_series` (names → series data).
4. Session holds: compiled Plan, current parameter values, current state matrix.
5. `eval` with overrides returns updated series within 50ms for typical models (no recompilation, no file I/O).
6. Series data encoded as binary f64 arrays in MessagePack ext type (not JSON text).
7. Error responses with structured error codes.
8. Graceful shutdown on stdin EOF or SIGTERM.
9. Rust integration tests: spawn session process, send compile + eval + patch sequence, verify results.

### m-E17-01: WebSocket Engine Bridge

**The bridge.** Connects web clients to the engine session.

**Acceptance criteria:**
1. .NET API WebSocket endpoint: `ws://localhost:8081/v1/engine/session`.
2. On WebSocket connect: spawn Rust engine session subprocess, pipe WebSocket frames ↔ engine stdin/stdout.
3. MessagePack frames pass through transparently (API is a dumb proxy).
4. Session lifetime = WebSocket lifetime. Engine process killed on disconnect.
5. Multiple concurrent sessions supported (one engine process per WebSocket).
6. Health check: session responds to ping within 100ms.

### m-E17-02: Svelte Parameter Panel

**The UI.** Parameter controls auto-generated from engine schema.

**Acceptance criteria:**
1. WebSocket connection to engine session on page load.
2. `compile` sent with model YAML → receive parameter schema.
3. Parameter panel renders controls: slider for scalar params, numeric input for all.
4. Control change → `eval` with override → receive updated series.
5. Svelte writable stores for each series. Charts/topology bind to stores.
6. Debounced updates: slider drag sends eval at most every 50ms.
7. Loading state while eval in flight.

### m-E17-03: Live Topology & Charts

**The visualization.** Graphs and charts react to series store changes.

**Acceptance criteria:**
1. Topology heatmap colors update when series values change (no re-layout).
2. Time-series line charts re-render on store update.
3. Chart axes auto-scale to new data range.
4. Latency from slider drag to visual update < 200ms end-to-end.

## E-11 dependency

m-E17-03 depends on E-11's topology visualization (M3) and timeline (M4) being functional. E-11 is paused at M6 with M1-M4 + M6 done. The Svelte UI already has:
- SvelteKit scaffold + shadcn-svelte (M1)
- API client + page routes (M2)
- Topology canvas via dag-map (M3)
- Timeline visualization (M4)
- Run orchestration page (M6)

What's missing for E-17: WebSocket infrastructure, parameter panel, reactive data binding. These are new features, not E-11 backlog.

## Protocol detail: why MessagePack over stdin/stdout

**vs. JSONL:** Binary f64 arrays avoid text encoding overhead. Length-prefixed framing eliminates incomplete-line ambiguity. Self-describing like JSON but 2-4x more compact for series data.

**vs. gRPC:** gRPC requires HTTP/2, codegen infrastructure, and doesn't compose with Unix pipes. MessagePack over stdin/stdout works with `cat`, `tee`, `jq` (with msgpack2json adapters), and any language with a MessagePack library.

**vs. custom binary:** MessagePack is a well-specified, widely-implemented format. No custom parser needed. Libraries exist for Rust (rmp-serde), JavaScript (@msgpack/msgpack), C# (MessagePack-CSharp), Python (msgpack).

**Transport flexibility:** Same MessagePack protocol works over:
- stdin/stdout (CLI pipelines)
- WebSocket (browser UI, via .NET proxy)
- TCP socket (future service mode)
