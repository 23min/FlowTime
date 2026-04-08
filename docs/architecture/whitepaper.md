# FlowTime Engineering Whitepaper

> **Charter Update Note:** This whitepaper focuses on FlowTime Engine capabilities and architectural foundations. For the full ecosystem scope (Engine + Sim + UI + MCP), see [`docs/flowtime-charter.md`](../flowtime-charter.md). For shipped behavior, see [`docs/reference/engine-capabilities.md`](../reference/engine-capabilities.md).

**Version:** 1.0 (Baseline)
**Audience:** Architects, engineers, telemetry specialists, SREs.
**Purpose:** Define the design, functionality, and implementation of the FlowTime Engine, including retry and feedback loop modeling, DAG evaluation, expression semantics, artifacts, and contracts. This whitepaper serves as the engineering reference for building, extending, and maintaining FlowTime Engine.

---

## 1. Conceptual Foundation

### 1.1 FlowTime in one line

FlowTime is a **deterministic, discrete-time, graph-based engine** that models flows of entities across services, queues, and dependencies. It produces **explainable time-series** for throughput, backlog, latency, retries, and utilization, supporting both **what-if simulation** and **what-is/what-was replay**.

### 1.2 Spreadsheet Metaphor

FlowTime is designed as a **spreadsheet for flows**:

* **Cells** = time bins (grid-aligned).
* **Formulas** = expressions with built-in operators and functions.
* **Series** = columns of values across time.
* **Graph nodes** = spreadsheet cells that reference each other, forming a DAG.

This keeps models **deterministic, transparent, and explainable** while allowing complex behaviors such as retries and feedback loops.

---

## 2. Core Abstractions

### 2.1 Grid & Series

* **Canonical time grid**: fixed resolution (e.g., 5m, 1h).
* **Series**: vectors `[t]` aligned to the grid, typed as:

  * **Flow series** (arrivals, served, errors) → per-bin totals.
  * **Level series** (backlog, queue depth) → end-of-bin state.
  * **Metadata series** (replicas, capacity, cost).

### 2.2 DAG Model

* **Nodes**: services, queues, routers, or formula expressions.
* **Edges**: flow (throughput) or effort (dependencies).
* **Evaluation**: topologically sorted, bin-by-bin scan. Cycles only allowed if broken by explicit delay (`SHIFT`/`CONV`).

### 2.3 Classes

Flows (e.g., Order, Refund, VIP) are modeled as **classes**. All node computations can run per-class vectors with strict or weighted-fair capacity sharing.

### 2.4 ServiceWithBuffer Nodes

Queues owned by a service are modeled with `kind: serviceWithBuffer`. These nodes couple inflow/outflow/loss series, expose queue depth metrics, and optionally apply dispatch schedules to gate releases. They replace the legacy `kind: backlog` surface; see [`../service-with-buffer/service-with-buffer-architecture.md`](../service-with-buffer/service-with-buffer-architecture.md) for the canonical schema and visualization rules.

---

## 3. Expressions & Built-ins

Expressions are the "formula language" of FlowTime. Each node can declare `expr: "..."` referencing series and applying built-ins.

### 3.1 Elementwise Operators

* `+ - * /` → vectorized arithmetic.
* `MIN(x,y)`, `MAX(x,y)`, `CLAMP(x,lo,hi)`.

### 3.2 Time Operators

* `SHIFT(x,k)` → shift series by k bins (lag ≥ 0).
* `DELAY(x,kernel)` / `CONV(x,kernel)` → discrete convolution (spread values forward in time).
* `RESAMPLE(x,freq,mode)` → rescale to new grid (mode = sum|mean).

### 3.3 Stateful Primitives

* **ServiceWithBuffer / Backlog**:
  `Q[t] = max(0, Q[t-1] + inflow[t] - outflow[t] - loss[t])`, optionally gated by dispatch schedules.
* **Latency**:
  `W[t] ≈ Q[t] / max(ε, served_rate[t]) * binMinutes`. <!-- m-E19-03:allow-binminutes-notation -->
  (Derived concept: bin-duration-in-minutes. Not the deprecated YAML schema field.)
* **Autoscale**: threshold/target policies with lag & cooldown.
* **Retry**:
  `retries = CONV(errors, retry_kernel)`.

### 3.4 Derived Functions

* `RETRY(errors,kernel,share)` → specialized wrapper around `CONV`.
* `EMA(x,α)` → exponential moving average (for smoothed feedback).
* `FAIL_RATE(latency,params)` → templated failure model.

---

## 4. Retry & Feedback Modeling

### 4.1 Motivation

Retries are a universal system behavior (databases, APIs, queues). They create **temporal feedback loops**: failed work at bin `t` becomes new arrivals at `t+Δ`. FlowTime must capture:

* Amplification (retries increase downstream load).
* Conservation (work is delayed, not lost, unless DLQ).
* Separation of **throughput** (successes) vs **effort** (attempts, dependency calls).

### 4.2 Patterns

#### A) Internal retries (capacity tax or explicit)

* **Tax mode**: efficiency η reduces effective capacity.
* **Explicit mode**:

  ```yaml
  retries := CONV(errors, [0.0,0.6,0.3,0.1])
  arrivals_total := arrivals + retries
  ```

#### B) Externalized retries (requeue pattern)

Failures are sent back to a queue:

```yaml
queue.reinflow := CONV(failures, retry_kernel)
queue.inflow := upstream + queue.reinflow
```

#### C) Attempt limits & DLQ

Cap retries at `K` attempts; spill to DLQ after exhaustion:

```yaml
dlq := failures * (1 - retryable_share) + exhausted_mass
```

#### D) Status-aware retries (4xx/5xx)

Split errors into retryable vs permanent:

```yaml
fail_4xx := failures * p4xx
fail_5xx := failures * (1-p4xx)
retries  := CONV(fail_5xx, kernel)
```

### 4.3 Invariants

* `served == successes`.
* `attempts_started ≥ served`.
* `arrivals + retries − served − ΔQ − dlq ≈ 0`.

---

## 5. DAG Evaluation Algorithm

### 5.1 Overview

1. **Compile** expressions into a dependency graph.
2. **Topo sort** nodes (delay operations like SHIFT/CONV with lag>0 break cycles).
3. **Single-pass evaluation**: Iterate over time bins sequentially:

   * For each bin `t`, evaluate nodes in topological order:
     * Nodes reference current bin data OR historical data via stateful operators
     * SHIFT/CONV nodes maintain history buffers for causal delays
     * No algebraic loops or iterative solvers required
   * Store outputs in aligned arrays.

**Key insight:** All feedback is delayed (lag ≥ 1), ensuring causal, single-pass evaluation while supporting complex retry behaviors.

### 5.2 Example

Model with retries:

```yaml
arrivals_total[t] = arrivals[t] + retries[t]
attempts[t] = MIN(capacity[t], arrivals_total[t])
errors[t] = attempts[t] * fail_rate[t]
retries[t] = CONV(errors, kernel)
```

* At bin 0: `retries[0] = 0` (kernel has no lag-0 mass).
* At bin 1: `retries[1]` picks up from `errors[0] * k[1]`.
* Evaluation is causal, single-pass, deterministic.

---

## 6. FlowTime Engine Implementation

### 6.1 Architecture

* **FlowTime.Core**: DAG compiler, primitives, evaluator.
* **FlowTime.API**: stateless HTTP surface (`/run`, `/graph`, `/state`).
* **FlowTime.UI**: Blazor app consuming API outputs.
* **Artifacts**: `run.json`, `series/index.json`, CSV/Parquet for external consumption.

### 6.2 Evaluation

* Vectorized arrays per node/class.
* State maintained in per-node buffers (for queues, retries, autoscale).
* Conservation checks ensure integrity.

### 6.3 API Contracts

* `POST /run` — run model and return outputs.
* `GET /graph` — return compiled DAG.
* `GET /state?ts=` — per-bin snapshot.
* `POST /learn/retry` — fit kernels from telemetry.

---

## 7. Artifacts & Contracts

* **series/index.json**: list of all output series (id, unit, type).
* **run.json**: metadata, retry config, scenario name, hashes.
* **CSV/Parquet**: per-series outputs.
* **Manifest.json**: RNG seed, scenario hash, software version.

Readers must tolerate unknown series (forward compatibility).

---

## 8. Domain-General Application

While the motivating use case is IT systems (queues, APIs, retries), **feedback loops and retries exist in many domains**:

* **Healthcare**: patient retries at clinics, follow-ups after failed appointments.
* **Logistics**: re-attempted deliveries, re-routed shipments.
* **Manufacturing**: rework of defective items, queues with inspection/retry loops.
* **Finance**: re-submission of trades or payments after rejection.

FlowTime's spreadsheet-based, DAG + delay model makes it **domain-neutral**. By keeping terminology generic (arrivals, capacity, retries, service-with-buffer queues/backlog), it adapts to any flow system.

---

## 9. Developer Guidance

### 9.1 Key Principles

* Always model on a fixed grid.
* Keep loops causal with explicit delay.
* Use additive series to avoid schema churn.
* Expose retry and error patterns explicitly for explainability.

### 9.2 Recommended Workflow

1. Start with arrivals/capacity/serviceWithBuffer queues.
2. Add retries (tax → explicit).
3. Extend to multi-class and priority.
4. Calibrate against telemetry.
5. Use simulation tools for what-if tests and learner validation.

---

## 10. Example: Service with Retries & ServiceWithBuffer Queue

```yaml
grid: { bins: 6, binSize: 5, binUnit: minutes }
nodes:
  - id: svcA
    kind: expr
    expr: |
      arrivals_total := arrivals + retries
      attempts := MIN(capacity, arrivals_total)
      successes := attempts * (1 - fail_rate)
      failures := attempts - successes
      retries := CONV(failures, [0.0,0.6,0.3,0.1])
      served := successes
      errors := failures
  - id: qA_capacity
    kind: const
    values: [50, 50, 50, 50, 50, 50]
  - id: downstream_demand
    kind: expr
    expr: "svcA.served * 0.95"
  - id: qA_dispatch
    kind: expr
    expr: "MIN(qA_capacity, downstream_demand)"
  - id: qA
    kind: serviceWithBuffer
    inflow: svcA.served
    outflow: qA_dispatch
    dispatchSchedule:
      periodBins: 2
      phaseOffset: 0
      capacitySeries: qA_capacity
outputs:
  - series: svcA.served
  - series: svcA.errors
  - series: svcA.retries
  - series: qA.queue
```

Evaluation:

* Bin 0: `retries=0`, queue releases immediately because the schedule fires on even bins.
* Bin 1: `retries[1] = failures[0]*0.6`.
* Bin 2: `retries[2] = failures[0]*0.3 + failures[1]*0.6`.
* And so on — retries "echo" across future bins while the ServiceWithBuffer cadence gates releases every other bin.

---

## 11. Acceptance Criteria

* Deterministic evaluation: same inputs → same outputs.
* Conservation checks pass.
* Retry echoes appear per kernel definition.
* Artifacts match schema; additive outputs don't break compatibility.
* UI shows retries, DLQ, effort vs throughput distinctly.

---

## 12. Future Directions

* Monte Carlo (uncertainty bands).
* Streaming ingestion & WASM engine.
* Genetic optimization for retry/capacity policies.
* Plugin nodes for domain-specific retry/backoff.

---

## 13. Non-Goals and Guardrails

### 13.1 Stay Flow-First (No DES)

FlowTime is intentionally **not** a discrete-event simulator (DES):

- Models operate on **aggregated flows over time bins**, not on individual entities or per-event timelines.
- Core semantics, artifacts, and APIs are all defined in terms of **flows and levels** (arrivals, served, backlog, retries) on the fixed grid.

Implications:

- We do not introduce per-entity event queues, arbitrary event-scheduling APIs, or per-entity state machines into the engine.
- Any future “silver telemetry” drill-down (e.g., per-request traces) is a **derived, optional analysis feature** layered on top of flow outputs, not a change to the core semantics.

### 13.2 Engine-Only Semantics, UI-Only Visualization

The engine is the **single source of truth** for model semantics and computation:

- All metrics, series, and behaviors must be computed by the engine and surfaced via APIs.
- The UI is responsible for **visualization, navigation, and annotation** only.

Implications for UI and tooling:

- The UI must never introduce its own hidden computations that change or redefine model behavior.
- Any “smart” UX (what-if sliders, overlays, annotations, suggested insights) must be a **thin layer over engine APIs**:
  - If the UX needs a new metric, that metric is first added to the engine + schema and then consumed by the UI.
  - What-if workflows are implemented by driving the engine with alternative inputs/templates, not by simulating locally in the browser.

These guardrails keep FlowTime’s semantics centralized, reproducible, and explainable, and ensure the engine remains a first-class, UI-independent component in larger pipelines.

---

# Conclusion

FlowTime provides a **domain-neutral, spreadsheet-like engine** for modeling flows with retries, feedback loops, queues, and capacity dynamics.
By grounding everything in **deterministic DAG evaluation with explicit delay**, FlowTime remains both **beautifully simple** and **expressively powerful**.

It supports IT systems today and generalizes to healthcare, logistics, manufacturing, and finance — anywhere feedback and retries matter.

This whitepaper serves as the engineering baseline for FlowTime Engine implementation and integration.

---
