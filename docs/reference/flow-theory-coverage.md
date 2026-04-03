# Flow Theory Coverage Matrix

Companion to [flow-theory-foundations.md](flow-theory-foundations.md). Maps each
foundational concept to FlowTime's current implementation, planned work, and
remaining gaps.

**Scope constraint:** FlowTime is a deterministic, bin-first, aggregate-flow
engine. It does not track individual work items. The "Not planned" column
reflects deliberate architectural boundaries, not oversights.

**Last updated:** 2026-04-03

---

## Coverage Summary

| Status | Count | Meaning |
|--------|-------|---------|
| Modeled | 10 | Implemented and tested in engine Core |
| Planned | 12 | Accepted in a milestone spec (p3a–p3d) or sequenced plan |
| Deferred | 4 | Acknowledged, sequenced for Wave 6+ |
| Not planned | 7 | Outside aggregate-flow architecture or deliberately out of scope |

---

## 1. Flow Conservation & Accounting

| Principle | Status | Milestone | Gap / Impact |
|-----------|--------|-----------|--------------|
| Mass balance (arrivals − departures = ΔQ + loss) | **Modeled** | — | — |
| Per-class conservation | **Modeled** | — | — |
| Edge flow conservation | **Modeled** | — | — |
| Retry reinjection accounting (CONV kernel, Σ ≤ 1) | **Modeled** | — | — |

InvariantAnalyzer checks these and emits first-class warnings on violation.
This is one of FlowTime's strongest areas.

---

## 2. Queue Mechanics

| Principle | Status | Milestone | Gap / Impact |
|-----------|--------|-----------|--------------|
| Queue accumulation Q[t] = Q[t−1] + in − out − loss | **Modeled** | — | — |
| Initial conditions (non-zero starting queue) | **Modeled** | — | — |
| WIP limits (cap Q, overflow → loss/DLQ/reroute) | **Planned** | m-ec-p3b | Cannot model Kanban or any pull-based system today |
| Back-pressure propagation (downstream full → upstream throttled) | **Deferred** | Wave 6 | m-ec-p3b does lossy overflow only; true pull mechanics are later. SHIFT-based backpressure is expressible manually today. |
| Blocking detection (downstream full, upstream stalls) | **Planned** | Phase 3 analytical (W2.6) | Detection/annotation only — no automatic upstream throttling |
| Starvation detection (Q=0, capacity idle, no arrivals) | **Planned** | Phase 3 analytical (W2.6) | Detection/annotation only |
| Balking (arrivals decline as Q grows) | **Not planned** | — | Would need queue-length-dependent arrival function; conflicts with bin-first model |
| Reneging (items leave queue before service) | **Not planned** | — | Requires per-item age tracking; conflicts with flow-first architecture |

---

## 3. Utilization & Capacity

| Principle | Status | Milestone | Gap / Impact |
|-----------|--------|-----------|--------------|
| Utilization ρ = served / capacity | **Modeled** | — | — |
| Time-varying capacity (shift patterns, maintenance windows) | **Modeled** | — | Via DispatchSchedule and capacity-as-series |
| Shared resource allocation (proportional split across competing nodes) | **Planned** | m-ec-p3d | ConstraintAllocator code exists but has zero callers — dead code today |
| Multi-server capacity (M/M/c — c parallel servers) | **Not planned** | — | `Parallelism` field exists as `object?` with no semantics; no Erlang-C. Start with Kingman single-server (m-ec-p3c). |

---

## 4. Latency & Cycle Time

| Principle | Status | Milestone | Gap / Impact |
|-----------|--------|-----------|--------------|
| Little's Law point estimate W = L/λ | **Modeled** | — | — |
| Steady-state validation (is λ stable enough for Little's Law?) | **Planned** | m-ec-p3a (AC-5) | Without this, point estimates silently mislead during transients |
| Cycle time decomposition (queue time + service time) | **Planned** | m-ec-p3a | CycleTimeComputer exists in branch, not yet merged |
| Flow efficiency (service time / cycle time) | **Planned** | m-ec-p3a | Same |
| Cycle time distributions (p50, p85, p95) | **Deferred** | Wave 6 (W6.2) | Can only say "average is X" — cannot answer "95% complete in < Y" |
| Aging WIP (how long has oldest item waited?) | **Deferred** | Wave 6 (W6.3) | No per-item state; queue depth is a scalar |

---

## 5. Variability

| Principle | Status | Milestone | Gap / Impact |
|-----------|--------|-----------|--------------|
| Coefficient of variation Cv = σ/μ | **Planned** | m-ec-p3c | PMFs collapsed to expected values today — all variance lost |
| Kingman's approximation Wq ≈ (Ca²+Cs²)/2 · ρ/(1−ρ) · E[S] | **Planned** | m-ec-p3c | The key formula connecting variability → queue growth is absent |
| Variance propagation through the DAG | **Not planned** | — | m-ec-p3c preserves Cv per node but does not compose across paths |

---

## 6. Bottleneck & Constraint Analysis

| Principle | Status | Milestone | Gap / Impact |
|-----------|--------|-----------|--------------|
| Cross-node utilization comparison | **Planned** | W2.1 | Per-node ρ exists but no comparison — user must eyeball |
| WIP accumulation pattern detection | **Planned** | W2.1 | Growing Q before a node = likely bottleneck signal |
| Theory of Constraints — Five Focusing Steps | **Not planned** | — | Only step 1 (identify) is planned. Exploit, subordinate, elevate, repeat are modeling strategies, not engine features. |
| Drum-Buffer-Rope scheduling | **Not planned** | — | DispatchSchedule models the Drum; Buffer and Rope are modeling patterns, not engine primitives. |

---

## 7. Cumulative Flow Diagrams

| Principle | Status | Milestone | Gap / Impact |
|-----------|--------|-----------|--------------|
| Raw data for CFDs (cumulative arrivals/departures per stage) | **Modeled** | — | Edge time bins + `/state_window` provide the series |
| Band width → WIP | **Planned** | W4.4 | Data exists, computation doesn't |
| Horizontal distance → approximate cycle time | **Planned** | W4.4 | Same |
| Slope analysis (trends in flow health) | **Planned** | W4.4 | Same |

---

## 8. Routing & Flow Splitting

| Principle | Status | Milestone | Gap / Impact |
|-----------|--------|-----------|--------------|
| Weighted probabilistic routing | **Modeled** | — | Static weights; no dynamic load-balancing |
| Class-based routing | **Modeled** | — | Per-class metrics tracked end-to-end |
| Priority queuing (classes served in priority order) | **Not planned** | — | Classes are independent streams, not prioritized within a shared queue |
| Dynamic routing (route by downstream queue length) | **Not planned** | — | Weights fixed at model definition time |

---

## 9. Feedback & Retry

| Principle | Status | Milestone | Gap / Impact |
|-----------|--------|-----------|--------------|
| Retry via convolution kernel | **Modeled** | — | Elegant causal implementation, no algebraic loops |
| Exponential backoff modeling | **Modeled** | — | Expressible via kernel shape |
| Retry amplification detection (retry storm) | **Not planned** | — | Retries modeled but no diagnostic when they dominate arrivals. Natural fit for anomaly-detection epic. |
| Circuit breaker / DLQ | **Partially modeled** | — | Loss series captures DLQ; no explicit circuit-breaker state machine |

---

## 10. Forecasting & Simulation

| Principle | Status | Milestone | Gap / Impact |
|-----------|--------|-----------|--------------|
| Deterministic what-if scenarios | **Modeled** | — | Core strength |
| Stochastic distributions (Poisson, exponential) | **Sim layer only** | — | Engine is deterministic; Sim templates sample PMFs to series |
| Monte Carlo forecasting | **Deferred** | Wave 6 (W6.4) | Explicitly out of engine scope per charter |

---

## 11. Multi-Stage Pipeline

| Principle | Status | Milestone | Gap / Impact |
|-----------|--------|-----------|--------------|
| DAG topology with typed edges | **Modeled** | — | — |
| Per-edge throughput/effort/terminal metrics | **Modeled** | — | — |
| Stage-level cycle time decomposition | **Planned** | m-ec-p3a | Queue time vs. processing time per node |
| Variability compounding across stages | **Not planned** | — | Cv preserved per node (m-ec-p3c) but not composed across paths |
| Handoff / transition delay modeling | **Modeled** | — | Via SHIFT operator on edges |

---

## Phase 3 Milestone Map

| ID | Title | Key Concepts Unlocked |
|----|-------|-----------------------|
| m-ec-p3a | Cycle Time & Flow Efficiency | Little's Law (applied), cycle decomposition, flow efficiency, steady-state validation |
| m-ec-p3b | WIP Limits | Pull systems, overflow routing, Kanban modeling |
| m-ec-p3c | Variability Preservation | Cv computation, Kingman's approximation |
| m-ec-p3d | Constraint Enforcement | Shared resource allocation, ToC step 1 |

---

## Principles That Require Per-Item Tracking

The following would require FlowTime to track individual work items through
the system, which conflicts with the bin-first, aggregate-flow architecture.
These are deliberately out of scope for the engine layer:

- Aging WIP (current age of specific in-progress items)
- Balking (arrival decision based on queue length perception)
- Reneging (departure from queue based on wait time)
- Priority queuing (serve highest-priority item first within a queue)
- Per-item cycle time distributions (exact p50/p85/p95)
- WIP age consistency validation (zombie item detection)

Some of these can be **approximated** from aggregate data (Wave 6 plans
distributional Little's Law for approximate percentiles). Others are
fundamentally outside the model.
