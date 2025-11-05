# Time‑Travel Queues: SHIFT, Initial Conditions, and Telemetry

Status: Draft (TT‑M‑03.27)  
Audience: Platform (API/Sim), UI, Authors of templates/telemetry

## Purpose

This document describes how FlowTime represents queue backlog (queue depth) over discrete time, how the SHIFT operator relates to per‑bin dynamics, how to handle the t=0 boundary via initial conditions, and how this affects telemetry and simulation. It also documents options and recommendations to support “realistic” queue telemetry and playback.

## Terminology

- Bin: the discrete time slot `t` of fixed duration `binMinutes` in the run grid.
- Arrivals `a(t)`: units entering the queue during bin `t`.
- Served `s(t)`: units leaving the queue during bin `t` (i.e., processed by the downstream service during bin `t`).
- Queue depth `q(t)`: the number of units waiting in the queue at the end of bin `t` (backlog).
- SHIFT(x, k): the series `x` shifted by `k` bins to the right. E.g., `SHIFT(q, 1)(t) = q(t−1)` for `t>0`.

## Queue Dynamics in Discrete Time

The canonical queue depth recurrence (end‑of‑bin values) is:

```
q(t) = max(0, q(t−1) + a(t) − s(t))
```

- `q(t−1)` is the backlog at the end of the previous bin.
- `a(t)` adds to backlog; `s(t)` removes from backlog.
- `max(0, ·)` prevents negative depth (no negative backlog).

In our expression language, this is:

```
queue_depth := MAX(0, SHIFT(queue_depth, 1) + queue_inflow − queue_outflow)
```

This self‑referential definition requires a defined initial condition at `t = 0`.

## Initial Condition at t = 0

At bin `t = 0`, `SHIFT(queue_depth, 1)` is undefined unless we provide `q(−1)` or an equivalent initial value. FlowTime templates convey this with topology initial conditions:

```
topology:
  nodes:
    - id: MyQueue
      kind: queue
      semantics:
        arrivals: queue_inflow
        served: queue_outflow
        queue: queue_depth
      initialCondition:
        queueDepth: <q0>
```

Semantics:
- `q(0) = max(0, q0 + a(0) − s(0))`
- For `t ≥ 1`, use the recurrence with `q(t−1)`.

Notes:
- Choose `q0 ≥ 0`. If omitted, treat as `0` for telemetry playback; in simulation mode a self‑SHIFT expression must have an explicit initial condition.

## Queue Latency via Little’s Law (Per Bin)

We expose queue latency `W(t)` (minutes) using Little’s Law with bin granularity:

```
W(t) = (q(t) / s(t)) * binMinutes      if s(t) > 0
W(t) = null                             otherwise
```

This provides a sensible instantaneous queueing delay estimate when throughput is non‑zero, and avoids division by zero when no items are served.

## Telemetry Requirements

To visualize queues from telemetry, we need, per queue node:

- Required series per bin:
  - `arrivals` (a(t)): units entering the queue
  - `served` (s(t)): units leaving the queue
  - `queue` (q(t)): queue depth at end of bin (optional only if API computes fallback; see below)

Recommendations:
- Emit counts per bin (not cumulative totals). The run grid’s bin size gives `binMinutes`.
- If queue depth is not emitted, latency can still be derived if the API computes `q(t)` from `a(t)` and `s(t)` given an initial value.

Synthetic telemetry (for testing) should emit exactly what real telemetry must include. That means all three series for a queue node, or a defensible fallback if the API computes depth.

## Simulation Support Strategies

The end goal is to support the SHIFT recurrence for queue depth while keeping the engine and contracts simple and performant. There are four viable strategies; pick one or combine short‑ and long‑term choices.

1) Artifact‑time derivation (recommended short‑term)
- During run generation (simulation build), precompute `queue_depth` from `queue_inflow` and `queue_outflow` using the recurrence and `initialCondition.queueDepth`.
- Persist `queue_depth` as a concrete series (CSV). The topology’s `semantics.queue` then references the concrete series.
- Pros: keeps the evaluation graph acyclic; works with current engine; guarantees file URIs exist.
- Cons: moves stateful computation out of the expression system; less flexible if we want to vary recurrence at runtime.

2) Stateful node in the engine (“Accumulator/Delay”)
- Extend the evaluator with a dedicated node that models `q(t) = f(q(t−1), a(t), s(t))` using the stored initial condition. Treat the self‑reference as an explicit time‑lag edge that does not contribute to cycle detection.
- Implementation notes:
  - Add a `QueueAccumulatorNode` (inputs: `a`, `s`, initial `q0`; output: `q`).
  - Adjust cycle checks to ignore 1‑bin delay edges for this node class.
  - Preserve the expression `MAX(0, SHIFT(queue_depth, 1) + ... )` by lowering it into this node.
- Pros: authentic recurrence inside the engine; no precomputation.
- Cons: requires code changes across parser, compiler, and graph cycle validation.

3) API fallback derivation (read‑time compute)
- When `/state_window` is requested and a queue node is missing a `queue` series but has `arrivals` and `served`, compute `q(t)` on the fly using the recurrence and either:
  - `initialCondition.queueDepth` from topology; or
  - assume `q0 = 0` if absent (telemetry mode only).
- Pros: fills gaps for playback when depth is missing; keeps templates simple.
- Cons: more CPU per request; more complexity in the state assembler.

4) Telemetry‑only depth (strict)
- Require telemetry producers to emit `queue` depth explicitly. Engine and API do not derive depth.
- Pros: very simple runtime; single source of truth.
- Cons: less resilient; harder to test without full telemetry.

Recommended path:
- Short‑term: (1) Artifact‑time derivation for sim + (3) API fallback for telemetry gaps; keep (4) supported when full telemetry is present.
- Mid‑term: consider (2) to make SHIFT a first‑class, stateful primitive, removing the need for precomputation.

## Handling the t = 0 Boundary Precisely

Given `q0 = initialCondition.queueDepth`:

```
q(0) = max(0, q0 + a(0) − s(0))
for t ≥ 1:
  q(t) = max(0, q(t−1) + a(t) − s(t))
```

Implementation choices:
- If `q0` is omitted in telemetry mode, treat `q(−1) = 0`. For simulation mode with self‑SHIFT, require `q0` and validate it (already enforced by our template validator).
- If `s(t) > q(t−1) + a(t)`, the clamp forces `q(t) = 0`. Any “overserve” is implicitly capped by backlog; if drops/denials must be tracked, model a separate `errors`/`dropped` series explicitly.

## Telemetry vs Simulation: What’s Required When

### Telemetry mode

- If telemetry provides `queue` depth `q(t)` per bin:
  - No SHIFT is needed. We consume `q(t)` as authoritative. There is no special handling at `t = 0`; the first recorded depth is just `q(0)`.
  - Latency uses the per‑bin Little’s Law formula with this `q(t)`.

- If telemetry provides only `arrivals` and `served` (no `queue`):
  - We can reconstruct `q(t)` using the recurrence and an initial condition.
  - Initial condition policy (pragmatic):
    - Prefer an explicit `q0` per queue node in the run metadata/manifest.
    - If absent, default to `q0 = 0` and annotate provenance (so users know depth was reconstructed).
  - This is an API fallback path; it improves resilience when depth isn’t emitted.

### Simulation mode

- With self‑SHIFT expressions, an explicit `initialCondition.queueDepth = q0` is required to make the recurrence well‑defined at `t=0`.
- Alternative (no self‑SHIFT at evaluation time): precompute `q(t)` during artifact generation and bind `semantics.queue` to the concrete series; in this case, no runtime SHIFT is involved.
- Modeler guidance for choosing `q0`:
  - If sim starts “cold”, set `q0 = 0`.
  - If sim starts “under load”, set `q0 > 0` to represent carry‑over backlog.
  - For steady‑state starts, estimate `q0 ≈ λ · W` at the start bin (λ from prior period) or run a warm‑up period and discard it (see below).

## Recommended Policy (Realistic, Future‑Proof, Pragmatic)

- Unify UI/API contracts: both modes expose the same series and latency semantics.
- Telemetry mode:
  - Prefer telemetry with `q(t)`; if missing, reconstruct from `a(t), s(t)` using `q0` (manifest) or `0`.
  - Record whether `q(t)` was measured vs reconstructed in response metadata (provenance/warnings).
- Simulation mode:
  - Short‑term: precompute `q(t)` at artifact time when authored; require `q0` when authoring self‑SHIFT.
  - Mid‑term: introduce an engine `QueueAccumulatorNode` so self‑SHIFT is a first‑class, stateful primitive.
- Provide a “warm‑up window” option (burn‑in) so modelers can start with any `q0` and exclude initial transients from analysis/visuals.

## Practical FAQ

- Do we still need `t−1`/SHIFT in telemetry mode when `q(t)` exists? No. We consume `q(t)` as recorded; SHIFT is only needed if we derive `q` from `a` and `s`.
- Why set an initial queue depth in simulation? Because the recurrence references `q(t−1)` at `t=0`; `q0` anchors the state at the start of the window and models “how much work is waiting” when the sim begins.
- Two solutions by mode? The API/UI remain unified; the ingestion path differs:
  - Telemetry: accept `q(t)` or reconstruct with `q0` fallback.
  - Simulation: either precompute `q(t)` or evaluate via a stateful node with `q0`.

## Contracts and Architecture Touchpoints

Templates
- Use `semantics.queue` to bind queue depth series (telemetry URI after generation). If self‑SHIFT is authored, include `initialCondition.queueDepth`.
- For sim with precomputation, author `queue_inflow`, `queue_outflow`, and a `queue_depth` expression; generation materializes depth to CSV.

Run Generation (Artifact Writer)
- Normalize all topology semantics, including `queue`, to `file:` URIs by rewriting logical series names to the artifact CSV path.

API `/state_window`
- Present `latencyMinutes` for `kind=queue` using the per‑bin Little’s Law formula above.
- Optional fallback: compute `queue` from `arrivals`/`served` when missing (telemetry mode), using `q0` if available, else `0`.

UI
- Canvas renders `kind=queue` as a rectangle with optional scalar badge of `q(t)`.
- Inspector shows Queue depth, Latency, Arrivals, Served; all support horizon context and window highlight.

## Numerical/Visualization Notes

- Latency nulling: when `served == 0`, set latency to null rather than `∞` or a large sentinel.
- Units: ensure counts are per bin; if upstream exports rates, convert to counts using `rate * binMinutes` before computing depth.
- Smoothing (out of scope for TT‑M‑03.27): moving averages may be applied at the UI layer, but not part of the core contract.

## Options Summary (Pros/Cons)

- Artifact‑time derivation: simple, performant, works now; less dynamic.
- Engine stateful node: most faithful; requires engine/compiler changes.
- API fallback derivation: resilient playback; higher CPU per window; clear server responsibility.
- Telemetry‑only depth: simplest runtime; strict requirement on data providers.

## Open Questions

- Do we allow negative `a(t) − s(t)` within a bin to carry into the next bin (beyond clamp)? Answer: No; we clamp at zero backlog. Drops should be modeled explicitly.
- Should we record and surface `q0` in API responses for provenance? (Recommended: include as part of node metadata or telemetry manifest.)
- Do we want a “strict” mode where latency is null unless both `q` and `s` are telemetry? (Out of scope for this milestone.)

## Next Steps

1) Keep artifact writer normalization for `queue` semantics; add sim‑time precompute of `queue_depth` from inflow/outflow when authored.
2) Add `/state_window` tests for `latencyMinutes` and (if adopted) fallback derivation of `queue`.
3) Consider an engine `QueueAccumulatorNode` prototype and cycle‑detection adjustments in a future milestone.
