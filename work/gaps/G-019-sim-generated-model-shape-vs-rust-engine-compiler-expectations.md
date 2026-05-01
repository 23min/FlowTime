---
id: G-019
title: Sim-generated model shape vs. Rust engine compiler expectations
status: open
---

### Why this is a gap

Discovered 2026-04-17 while wiring the Svelte `/analysis` surface
(M-040 sweep + sensitivity) against real runs. The Rust engine's
compiler (`engine/core/src/compiler.rs`) has a hard split between
**top-level `nodes:`** (only `const`/`expr`/`pmf`/`router` accepted)
and **`topology.nodes:`** (where `queue`/`service`/`servicewithbuffer`/
`dlq` belong).

Sim-generated models (Supply Chain Multi-Tier Classes, Warehouse Picker
Wave Dispatch, and similar templates) emit service/queue/
servicewithbuffer nodes at the top level of `nodes:` instead. The Rust
engine rejects them at compile time:

- `Unsupported node kind 'servicewithbuffer' on node 'intake_queue'`
- `Router node 'ReturnsRouter' requires router field` (shape mismatch
  when routing is expressed via edge weights rather than a node field)

As a result, **no existing Sim-generated run compiles in the Rust
engine session**, which means the full `/v1/sweep`, `/v1/sensitivity`,
`/v1/goal-seek`, and `/v1/optimize` surfaces cannot be exercised
against real runs until the shape mismatch is closed.

### Current mitigation

- `/analysis` route has a **Run / Sample** source toggle. Sample mode
  ships bundled, Rust-compatible minimal models (`simple-queue`,
  `two-stage-pipeline` at `ui/src/lib/utils/sample-models.ts`) so the
  analysis flow can be demoed without a real run.
- Sweep/sensitivity API endpoints now return the engine's compile
  error as a structured 400 (rather than a 500 developer page), so
  the UI surfaces an actionable message.

### Resolution path

Two non-exclusive options:

1. **Shape-normalizing transform in Sim or a pre-compile step**:
   translate `kind: servicewithbuffer` / `kind: service` /
   `kind: queue` at the top-level into `topology.nodes` entries before
   handing the YAML to the Rust engine. Routing weights on edges could
   similarly be translated into router-node form.
2. **Widen the Rust engine compiler** to accept the Sim-generated
   shape directly (promote top-level service/queue nodes into the
   topology layer inside `compiler.rs`).

Option 1 keeps the Rust engine's compile contract strict; option 2
reduces friction but blurs the boundary that E-16/E-20 established.
Decision belongs to the engine epic owner — not in E-21 scope.

### Immediate implications

- Do not assume Sim-generated runs work with `/v1/sweep`,
  `/v1/sensitivity`, or downstream analysis APIs. Sample-model mode
  is the working path for demos until this gap is closed.
- Telemetry Loop & Parity (prerequisite for fitting) will hit the
  same issue — parity testing requires the Rust engine to be able
  to evaluate the models Sim produces.

### Reference

- `engine/core/src/compiler.rs` lines 637-670 (top-level node kind
  dispatch), lines 1163-1190 (topology-node kind handling)
- `work/epics/completed/E-21-svelte-workbench-and-analysis/m-E21-03-sweep-sensitivity.md`
- `ui/src/lib/utils/sample-models.ts` (bundled Rust-compatible fixtures)

---
