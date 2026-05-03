---
id: G-031
title: Backend language choice for session service
status: wontfix
---

### Why this is a gap

The current stack is Rust engine + .NET session layer. The .NET choice is historical — it predates the Rust engine and predates Studio's scope expansion. Under cloud-agnostic deployment (Docker on Hetzner or similar) and with the Pipeline SDK's “embeddable into .NET only” framing challenged, the first-principles calculus shifts:

- **Pipeline SDK embedding reach.** A Rust crate + C ABI reaches .NET (P/Invoke), Python (ctypes / PyO3), Node (NAPI), Go (cgo), and any other runtime. A .NET assembly reaches .NET only. Broader embedder reach is strictly better for “FlowTime as a callable function” positioning.
- **Container profile on cloud-agnostic hosting.** Rust: ~10 MB static binary, sub-second cold start. .NET AOT-trimmed: ~80 MB, multi-second cold start. On managed Azure the gap is absorbed; on plain Docker hosting it is visible.
- **Collapsing the engine/service divide.** If the service is Rust, the “engine as subprocess” discussion ends — the session service owns the engine state directly via a linked crate.
- **Studio's .NET scope is mostly net-new.** Session IR, patch vocabulary, WS multiplex, snapshot/resume do not exist today. The rewrite cost is “build Studio in Rust from scratch” rather than “port a mature .NET layer to Rust.” The existing analysis runners (SweepRunner, Nelder-Mead, etc., ~1,600 LOC) are near-pure math and port cleanly.

### Status

Open question. Not scheduled. Worth deciding before M-046 scope lands so the implementation substrate is settled.

### Resolution path

Prototype spike:

1. `axum` or `actix-web` service exposing the current `/v1/sweep` shape, wrapping the Rust engine as a linked crate (no subprocess).
2. Measure: cold start, steady-state memory at N=50 sessions, end-to-end sweep latency vs .NET + subprocess baseline, container image size.
3. Re-evaluate with data. If Rust wins decisively on memory + deployment profile and the async-Rust complexity for WS fan-out is tractable, Studio (E-23) is the natural place to land the shift — it is mostly new scope anyway.

### Immediate implications

- Do not treat “.NET session service” as settled in the Studio architecture doc. Mark it as an assumption pending spike.
- Keep the `IModelEvaluator` seam language-neutral in spirit — it is currently .NET-flavored but the pattern (compile-once-eval-many with parameter patches) ports directly.
- Do not expand the .NET layer with anything that would be load-bearing to keep on .NET specifically. Structural patch handling, session IR, and WS multiplex should be written as if any language could host them.

---
