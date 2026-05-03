---
id: G-030
title: FFI-based engine evaluator (alternative to subprocess)
status: wontfix
---

### Why this is a gap

The current `IModelEvaluator` seam has two implementations — `RustModelEvaluator` (fresh subprocess per eval) and `SessionModelEvaluator` (persistent `flowtime-engine session` subprocess via MessagePack over stdio). Both share one property: the engine is a separate OS process, with IPC on every call.

For Studio (E-23 as proposed), this boundary gets expensive:

- **Per-session memory multiplies.** “One Rust process per session” with ~50 concurrent sessions = 50 process address spaces. The alternative — a pooled subprocess with session affinity — is real complexity that does not serve users.
- **Session IR wants direct access to engine state.** Node identity, per-node cache, and generation counters live in Rust (per M-046). The .NET session service projects patches onto that state via IPC round-trips. A shared-memory boundary would let the service hand the evaluator `&mut Graph` or an `Arc<Session>` directly.
- **Cold start matters for short-lived embedders.** `FlowTime.Pipeline` SDK callers pay subprocess startup per invocation unless they keep a session open; an in-process library pays zero.
- **Container profile.** On cloud-agnostic hosting (Hetzner, plain Docker), collapsing engine + service into one process shrinks image size and PID footprint.

### Alternative shape

Rust engine compiled as a **cdylib** (or `staticlib` for .NET AOT scenarios), loaded into the hosting process via FFI:

- .NET host → P/Invoke or a managed wrapper around a stable C ABI.
- Rust host → same crate linked directly (no FFI needed if the service is also Rust — see below).
- Python/Node/Go embedders → same C ABI via their native-interop story.

Keeps the language-boundary benefit (engine correctness isolated from service churn) without the process boundary.

### Why this may or may not be worth doing

Worth it if any of these bite:

- Per-session memory at `N=50` concurrent sessions exceeds budget.
- Cold-start latency on short-lived pipeline invocations shows up in traces.
- The subprocess pool-with-affinity logic grows into a distinct subsystem rather than a small helper.

Not worth it if:

- Sessions are few and long-lived (the subprocess boundary amortizes).
- The managed wrapper surface area (marshalling complex types, lifetime of native handles, allocator mismatches) exceeds the subprocess complexity it replaces.

### Immediate implications

- Keep `IModelEvaluator` as the seam; do not let subprocess assumptions leak past it.
- Before committing to “one Rust process per session” in M-046/M-047, prototype an FFI-based evaluator alongside the subprocess one and measure memory + cold start at realistic `N`.
- A language reconsideration for the service layer (see “Backend language choice for session service” below) interacts with this decision: if the service ends up in Rust, FFI collapses to a direct link and the IPC discussion ends.

---
