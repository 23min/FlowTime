# FlowTime

**A deterministic flow algebra engine. A spreadsheet for flow dynamics. Queueing theory made executable.**

Traditional tools measure *symptoms* — high latency, growing backlogs, missed SLAs. FlowTime models the *mechanics* — queue depth × service rate × retry amplification — so you can reason about causes and test interventions before deploying them.

---

## What FlowTime actually does

FlowTime models systems as a **directed acyclic graph (DAG) of services and queues evaluated over a discrete time grid**. Think of it like a spreadsheet for flow dynamics:

- **Nodes** represent services (with queues), routers, constants, or expressions.
- **Edges** carry work volume between nodes — throughput, effort, or terminal (DLQ/escalation).
- **Time bins** discretize everything into fixed intervals (e.g., 60-minute bins over 24 hours).
- The engine does a **single-pass topological evaluation** — for each bin, it computes arrivals, served, queue depth, errors, retries, latency, utilization, and more.

All feedback (retries, backpressure) is modeled via **causal delay** — the `SHIFT` and `CONV` operators push effects forward in time, which guarantees determinism. Same inputs always produce the same outputs. No algebraic loops. No iterative solvers.

### The spreadsheet metaphor

| Spreadsheet | FlowTime |
|-------------|----------|
| Cells | Time bins on the grid |
| Formulas | Expressions with built-in operators |
| Columns | Series — vectors of values across time |
| Cell references | Graph edges — nodes reference each other |
| Recalculation | Single-pass topological evaluation |

A spreadsheet user writes `=B2*0.8` and the cell updates. A FlowTime author writes `served := MIN(capacity, arrivals)` and gets a time series. The difference is that FlowTime's "spreadsheet" is aware of queues, retries, conservation laws, and causal time delays — things that would require fragile, hand-rolled VBA macros in an actual spreadsheet.

---

## What makes FlowTime unique

### 1. Deterministic replay and what-if scenarios

Traditional dashboards show you what happened. FlowTime lets you model what *would* happen if you changed capacity, routing, or retry policies — and get reproducible results. Run a scenario today and run it again next month: identical outputs. Jira dashboards and LinearB cannot do forward simulation.

This also means FlowTime supports *time-travel*: replay historical telemetry through the engine, then compare the replay against a what-if variant where you doubled capacity at the bottleneck. The difference between the two runs *is* the answer.

### 2. Explicit flow algebra, not opaque ML

Every metric is a traceable formula. Cycle time decomposes into queue time + service time. Flow efficiency = service time / cycle time. Utilization = served / capacity. There is no black box.

Conservation invariants enforce discipline:

```
arrivals + retries − served − ΔQ − dlq ≈ 0
```

When this invariant breaks, the engine emits a warning — it doesn't silently drift. You can inspect exactly which node violated conservation and by how much.

### 3. Domain-neutral

The engine is not locked to software delivery. The same primitives — arrivals, capacity, queues, retries, routing, latency — work for:

- **IT operations**: API request flows, incident queues, retry storms.
- **Healthcare**: patient flows through clinics, follow-up scheduling, triage queues.
- **Logistics**: warehouse picking lines, delivery retry loops, shipment routing.
- **Manufacturing**: production lines with rework loops, inspection queues, WIP limits.
- **Finance**: trade processing, payment retry chains, settlement queues.
- **Customer support**: ticket queues, escalation paths, SLA monitoring.

If it has queues, throughput, and latency, FlowTime can model it.

### 4. Retry and feedback modeling

Retries are modeled as convolution kernels that reinject failed work across future time bins:

```yaml
retries := CONV(errors, [0.0, 0.6, 0.3, 0.1])
```

This single line says: "60% of errors retry after 1 bin, 30% after 2 bins, 10% after 3 bins." The kernel captures exponential backoff, retry storms, and amplification effects that traditional dashboards completely miss. The engine tracks attempts vs. successes, enforces retry budgets, routes exhausted failures to dead-letter queues, and ensures the total mass is conserved throughout.

### 5. Multi-class flow isolation

Different work types (Orders, Refunds, VIP) flow through the same topology with separate SLAs, priorities, and metrics — not just tagged aggregates. Classes are first-class: per-class series, per-class conservation checks, per-class latency. A single model can answer "what is the p85 flow time for Refunds through the intake queue?" without conflating it with Order traffic.

### 6. Warnings as first-class artifacts

Most tools silently produce misleading results when assumptions break. FlowTime makes wrongness visible. The engine emits structured warnings for:

- Conservation violations (work appeared or disappeared).
- Queue depth mismatches (model vs. telemetry divergence).
- Retry kernel policy adjustments (kernel trimmed or rescaled).
- Backlog health signals (growth streaks, overload ratios, age risk).
- Missing series or incomplete telemetry.
- Steady-state violations (Little's Law applied during transients).
- Router diagnostics (missing class routes, class leakage).

Warnings are persisted in run artifacts and surfaced through APIs, so they survive beyond the moment of evaluation. They tell you not just *what* the engine computed, but *where it is uncertain or constrained*.

---

## Core concepts

### The time grid

Everything in FlowTime begins with a fixed time grid:

```yaml
grid:
  bins: 24
  binSize: 60
  binUnit: minutes
```

This defines 24 bins of 60 minutes each — a full day. All series align to this grid. All computations happen per-bin. The grid is UTC-aligned and left-closed: bin 0 covers `[T₀, T₀+60m)`.

The fixed grid is a deliberate choice. It sacrifices the ability to model sub-bin dynamics in exchange for determinism, simplicity, and composability. If you need finer resolution, use a smaller bin size.

### Series

A series is a vector of values aligned to the grid — one value per bin. Series come in two flavors:

- **Flow series** (rates): arrivals, served, errors, attempts — per-bin totals.
- **Level series** (stocks): queue depth, backlog, WIP — end-of-bin state.

The distinction matters because flows are additive (you can sum arrivals across two time windows) while levels are not (you can't sum queue depths).

### Nodes

Nodes are the computational units. Each node takes input series and produces output series:

| Node kind | What it does |
|-----------|-------------|
| `const` | Emits fixed values — a known demand pattern, a capacity schedule. |
| `expr` | Evaluates a formula — arithmetic, `MIN`, `MAX`, `SHIFT`, `CONV`, `CLAMP`, and more. |
| `serviceWithBuffer` | The canonical "station" — a service with an owned queue, optional dispatch schedule, inflow/outflow/loss series, queue depth tracking, and latency derivation. |
| `router` | Splits flow by weights or class — probabilistic or class-based routing. |
| `dependency` | Models a shared resource or external dependency (arrivals/served/errors). |
| `sink` | Terminal node — absorbs flow (e.g., DLQ, completed work). |

### Edges

Edges connect nodes and carry work between them. They are typed:

- **Throughput edges**: successful work flowing downstream.
- **Effort edges**: total attempts including retries — shows load, not just success.
- **Terminal edges**: failed/exhausted work routed to DLQ or escalation.

Edge metrics include `flowVolume`, `attemptsVolume`, `failuresVolume`, `retryRate`, and derived `retryVolume`. Each edge carries quality metadata (`exact`, `approx`, `partialClass`, `missing`) so consumers know how much to trust the numbers.

### Classes

Classes name the kinds of work flowing through the system. When declared, all node computations run per-class vectors:

```yaml
classes:
  - id: Order
    displayName: "Order Flow"
  - id: Refund
    displayName: "Refund Flow"
```

Per-class metrics, per-class conservation checks, per-class series in artifacts and APIs. A `DEFAULT` class serves as the aggregate fallback when classes are not declared.

### Expressions

Expressions are FlowTime's formula language. They compile to nodes in the DAG:

```yaml
- id: intake
  kind: expr
  expr: |
    arrivals_total := arrivals + retries
    attempts := MIN(capacity, arrivals_total)
    successes := attempts * (1 - fail_rate)
    failures := attempts - successes
    retries := CONV(failures, [0.0, 0.6, 0.3, 0.1])
    served := successes
```

Available operators and functions:

| Category | Operations |
|----------|-----------|
| Arithmetic | `+`, `-`, `*`, `/` (vectorized) |
| Comparison | `MIN`, `MAX`, `CLAMP` |
| Rounding | `FLOOR`, `CEIL`, `ROUND` |
| Time | `SHIFT` (lag), `CONV` (convolution/delay) |
| Pattern | `MOD`, `STEP`, `PULSE` |

Expressions compile down to a node subgraph. The graph remains explicit and inspectable — you can always trace how a formula was evaluated.

---

## The evaluation model

### How it works

1. **Compile**: Parse the model (YAML) into a dependency graph of nodes and edges.
2. **Topological sort**: Order nodes so every node's inputs are computed before the node itself. Delay operators (`SHIFT`, `CONV` with lag ≥ 1) break what would otherwise be cycles.
3. **Single-pass evaluation**: Iterate over time bins sequentially. For each bin, evaluate all nodes in topological order. Store outputs in aligned arrays.

This is the key insight: all feedback is delayed by at least one bin, so there are no algebraic loops. The engine never iterates to convergence — it evaluates once, forward in time, and is done.

### Example: retry echo

```
Bin 0: arrivals=100, capacity=80 → served=80, errors=20, retries=0
Bin 1: arrivals=100, retries=12 (20×0.6) → total=112, served=80, errors=32
Bin 2: arrivals=100, retries=15.2 (20×0.3 + 32×0.6) → total=115.2, served=80
```

The retry kernel `[0.0, 0.6, 0.3, 0.1]` spreads errors forward in time. The amplification is visible: 20 errors in bin 0 create 12 + 6 + 2 = 20 retry attempts across bins 1–3, which themselves generate new errors that create more retries. The engine traces this chain deterministically.

### Conservation invariants

After evaluation, the engine checks that work is conserved:

```
arrivals + retries − served − ΔQ − dlq ≈ 0
```

This runs per-node, per-class, and per-edge. Violations produce warnings with the node ID, the magnitude of the imbalance, and the bin range. Conservation checking is what makes FlowTime trustworthy — you know when the model's accounting is broken.

---

## The queue: ServiceWithBuffer

The `serviceWithBuffer` node is FlowTime's canonical abstraction for any place where work accumulates and waits:

```yaml
- id: intake_queue
  kind: serviceWithBuffer
  inflow: upstream.served
  outflow: dispatch
  loss: dlq_overflow
  dispatchSchedule:
    periodBins: 2
    phaseOffset: 0
    capacitySeries: processing_capacity
```

It owns:

- **Queue depth**: `Q[t] = max(0, Q[t-1] + inflow[t] - outflow[t] - loss[t])`.
- **Latency derivation**: `W[t] ≈ Q[t] / served_rate[t]` — Little's Law applied per bin.
- **Dispatch schedules**: gate releases to specific intervals (e.g., "process the queue every 2 bins").
- **Loss tracking**: overflow to DLQ when limits are exceeded.
- **Initial conditions**: start with a non-zero queue to model recovery scenarios.

ServiceWithBuffer replaces ad-hoc "backlog node + manual wiring" patterns. It encapsulates queue mechanics so models stay clean and conservation checks work automatically.

---

## Derived metrics

The engine computes and exposes a rich set of derived metrics per node:

| Metric | Formula / meaning |
|--------|------------------|
| `utilization` | `served / capacity` — how busy the service is. |
| `latencyMinutes` | Queue time estimate via Little's Law. |
| `serviceTimeMs` | Processing time per served item. |
| `flowLatencyMs` | End-to-end latency including queue + service. |
| `throughputRatio` | Served / arrivals — how much demand is met. |
| `retryTax` | Retry overhead as fraction of total effort. |
| `color` | UI-aid signal (green/amber/red) based on utilization and health. |

Plus SLA descriptors: completion SLA (with dispatch carry-forward), backlog age SLA, and schedule-adherence SLA. SLA payloads include status codes so UIs can surface "No data" without fabricating values.

---

## The platform: four surfaces

FlowTime is not just an engine. It is a platform with four complementary surfaces:

### 1. The Engine (FlowTime.Core + FlowTime.API)

The execution and semantics layer. Takes a model, evaluates it on the time grid, produces canonical run artifacts, and exposes stable APIs. This is the source of truth.

**Run artifacts** produced per evaluation:
- `run.json` — metadata, hashes, scenario name, warnings.
- `manifest.json` — RNG seed, software version, per-series SHA-256 hashes.
- `series/index.json` — list of all output series with IDs, units, and types.
- Per-series CSVs — `t,value` with invariant-culture floats.

**API endpoints:**
- `POST /v1/runs` — create a run from a model.
- `GET /v1/runs/{id}/graph` — compiled DAG with semantics and aliases.
- `GET /v1/runs/{id}/state` — single-bin snapshot with derived metrics and warnings.
- `GET /v1/runs/{id}/state_window` — windowed series including edge metrics.
- `GET /v1/runs/{id}/metrics` — aggregate metrics over a bin range.
- `GET /v1/runs/{id}/series/{seriesId}` — raw CSV stream for a specific series.

### 2. The Simulation layer (FlowTime.Sim)

Owns **template authoring and stochastic inputs**. Templates produce models; models produce runs.

Templates are parameterized blueprints:
- Define topology, capacity, routing, retry kernels, traffic patterns.
- Parameters can be overridden for what-if scenarios.
- Stochastic inputs (PMFs, Poisson arrivals) are sampled to deterministic series before the engine ever sees them.
- Templates support mode selection: `simulation` (synthetic) or `telemetry` (replay from captured data).

The simulation layer bridges the gap between "I have a hypothesis about my system" and "here is a model the engine can evaluate."

### 3. The UI (FlowTime.UI)

A time-travel visualization surface:
- **Topology view**: the DAG rendered as a graph with edge overlays showing flow volume, retry rates, and health signals.
- **Inspector**: per-node deep-dive showing all series, metrics, SLAs, and warnings over time.
- **Time slider**: scrub through bins to see how the system evolves.
- **Focus filters**: isolate specific classes, nodes, or paths.

The UI is strictly a consumer of engine semantics. It never invents metrics or performs computation — if it needs a new derived value, that value must first be added to the engine.

### 4. MCP server (agent workflows)

FlowTime exposes an MCP (Model Context Protocol) server for AI agent workflows:
- Modeling, analysis, and inspection tools designed around outcomes.
- Agents can draft models, validate them, run scenarios, and inspect results without hand-stitching dashboards.
- The MCP surface uses the same stable APIs as the UI — no separate semantics.

---

## Telemetry: from what-if to what-was

FlowTime supports two modes of operation:

**Simulation mode** — You define a model with synthetic inputs (PMFs, constant rates, parameterized patterns). The engine evaluates it. You get a "what-if" answer: "If demand were 100/hour and capacity were 80/hour, what would the backlog look like after 24 hours?"

**Telemetry mode** — You capture real system telemetry into a canonical bundle (arrivals, served, errors, queue depth as time series). The engine replays it through the model topology. You get a "what-was" answer with full FlowTime semantics: conservation checks, derived metrics, warnings.

The power is in the comparison. Run telemetry mode to establish the baseline. Then run simulation mode with one parameter changed. The delta between the two runs tells you exactly what that change would have done.

### Telemetry interchangeability

A core architectural principle: **PMFs are interchangeable with telemetry**. Once compiled, a PMF-driven node produces the same type of time series as a telemetry-driven node. You can start with PMFs for early modeling, then swap in real data without changing the model topology. This enables a smooth progression:

```
Hypothesis → PMF modeling → Synthetic testing → Telemetry replay → Production monitoring
```

---

## A concrete example

A logistics company has an order intake pipeline: orders arrive, get validated, go into a processing queue, and are dispatched in batches.

```yaml
grid:
  bins: 24
  binSize: 60
  binUnit: minutes

classes:
  - id: Standard
  - id: Express

nodes:
  - id: orders
    kind: const
    values: [40, 45, 50, 60, 70, 80, 90, 95, 100, 95, 90, 80,
             70, 60, 55, 50, 45, 40, 35, 30, 25, 20, 15, 10]

  - id: validation
    kind: expr
    expr: |
      attempts := orders
      failures := attempts * 0.05
      served := attempts - failures
      retries := CONV(failures, [0.0, 0.7, 0.2, 0.1])

  - id: processing_queue
    kind: serviceWithBuffer
    inflow: validation.served
    outflow: dispatch_rate
    dispatchSchedule:
      periodBins: 3
      phaseOffset: 0
      capacitySeries: processing_capacity

  - id: processing_capacity
    kind: const
    values: [50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50,
             50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50]

  - id: dispatch_rate
    kind: expr
    expr: "MIN(processing_queue, processing_capacity)"

edges:
  - from: orders
    to: validation
    type: throughput
  - from: validation
    to: processing_queue
    type: throughput
  - from: processing_queue
    to: dispatch_rate
    type: throughput

outputs:
  - series: validation.served
  - series: validation.retries
  - series: processing_queue.queue
  - series: dispatch_rate
```

What questions can this model answer?

- **Does the queue grow or drain over the day?** Look at `processing_queue.queue` across bins. If the midday peak (bins 6–10) pushes arrivals above dispatch capacity, the queue grows — and you can see whether it recovers by evening.
- **How much retry pressure does validation create?** The `[0.0, 0.7, 0.2, 0.1]` kernel spreads 5% failures forward. At peak (100 orders/bin), that is 5 failures generating ~3.5 retries in bin+1, ~1 in bin+2, ~0.5 in bin+3. The retry tax is small here, but change the failure rate to 20% and watch the amplification.
- **What if we dispatch every bin instead of every 3 bins?** Change `periodBins: 3` to `periodBins: 1` and re-run. Compare queue depth profiles.
- **What if Express orders get priority?** Add class-specific routing and capacity allocation to see whether Express orders meet their SLA without starving Standard.

---

## Theoretical foundations

FlowTime's modeling primitives are grounded in established flow theory:

| Theory | FlowTime implementation |
|--------|------------------------|
| **Little's Law** (`L = λW`) | Latency derivation per node: `W ≈ Q / served_rate`. Steady-state validation planned to flag when the law's assumptions don't hold. |
| **Conservation of flow** | The `arrivals + retries − served − ΔQ − dlq ≈ 0` invariant, checked per-node and per-class. |
| **Queueing theory** | ServiceWithBuffer nodes implement `Q[t] = Q[t-1] + in - out - loss`. Utilization tracking (`ρ = served / capacity`). Kingman's approximation planned for variability-aware latency. |
| **Theory of Constraints** | Per-node utilization comparison surfaces the bottleneck. WIP accumulation patterns identify where work piles up. |
| **Cumulative Flow Diagrams** | Edge time bins and `/state_window` provide the raw cumulative series. Band width → WIP and horizontal distance → cycle time derivations are planned. |
| **Flow Framework metrics** | Multi-class support maps to flow item types. Throughput → flow velocity. Latency → flow time. `served / arrivals` → throughput ratio. Class distribution → flow distribution. |
| **Retry / feedback** | Convolution kernels model temporal feedback without algebraic loops. Kernel governance enforces mass ≤ 1.0 and length ≤ 32. |

FlowTime is deliberately **not** a discrete-event simulator (DES). It operates on aggregated flows over time bins, not on individual entities. This is a fundamental architectural choice: it trades per-item fidelity for determinism, performance, transparency, and composability.

---

## What FlowTime is not

- **Not a streaming engine.** It evaluates complete time windows, not live event streams.
- **Not a BI tool.** It produces structured data and APIs that BI tools and UIs consume.
- **Not a telemetry warehouse.** It ingests telemetry bundles and produces run artifacts — it does not store or query raw telemetry.
- **Not a probabilistic simulator by default.** The engine is deterministic. Stochastic sampling happens in the Sim layer before the engine sees the data.
- **Not a discrete-event simulator.** It models aggregate flows, not individual work items. Per-item behaviors (balking, reneging, priority queuing within a queue, aging WIP) are outside the core architecture.

These are deliberate boundaries, not gaps. FlowTime is sharp and explainable for its chosen semantics rather than a general-purpose simulation environment.

---

## Design principles

1. **Determinism over drift.** Same inputs → same outputs. No hidden mutation after evaluation. No non-deterministic execution paths.

2. **Minimal basis, rich meaning.** Arrivals, served, and queue depth are the backbone. Every other metric is derived, and every derivation is labeled with provenance.

3. **Explainability first.** Make wrongness visible. Warnings are first-class artifacts, not log noise. Every metric is traceable to a formula. Every formula is traceable to inputs.

4. **Contracts over convenience.** APIs and schemas are stable. UIs and agents consume semantics — they do not invent them. If the UI needs a new metric, that metric is first added to the engine.

5. **Semantics are server-side.** The engine is the single source of truth. The UI never performs computation that redefines model behavior. What-if workflows drive the engine with alternative inputs, not local browser simulation.

6. **Flow-first, not event-first.** Models operate on aggregated flows over time bins. Any future per-entity analysis is a derived, optional layer — not a change to core semantics.

---

## Getting started

### Run a model

```bash
# Build
dotnet build FlowTime.sln

# Run a model
dotnet run --project src/FlowTime.Cli -- run examples/hello/model.yaml --out out/hello --verbose

# Start the API
dotnet run --project src/FlowTime.API
# → http://localhost:8081

# Run tests
dotnet test FlowTime.sln
```

### Explore a run

```bash
# Create a run via API
curl -X POST http://localhost:8081/v1/runs -H "Content-Type: application/yaml" -d @model.yaml

# Get the compiled graph
curl http://localhost:8081/v1/runs/{runId}/graph

# Get state at bin 5
curl http://localhost:8081/v1/runs/{runId}/state?ts=5

# Get windowed series for bins 0-23
curl http://localhost:8081/v1/runs/{runId}/state_window?from=0&to=23
```

### Author a template

Templates live in FlowTime.Sim and produce models:

```bash
# List available templates
dotnet run --project src/FlowTime.Sim.Cli -- list templates

# Generate a model from a template
dotnet run --project src/FlowTime.Sim.Cli -- generate --id my-template --mode simulation --out out/model

# Validate template parameters
dotnet run --project src/FlowTime.Sim.Cli -- validate params --id my-template
```

---

## Architecture at a glance

```
┌──────────────────────────────────────────────────────────┐
│                     FlowTime Platform                     │
│                                                          │
│  ┌─────────────┐   ┌──────────────┐   ┌──────────────┐  │
│  │  FlowTime   │   │   FlowTime   │   │   FlowTime   │  │
│  │    Sim      │──▶│    Engine    │◀──│     UI       │  │
│  │  (Templates │   │  (Core+API)  │   │  (Time-travel│  │
│  │   + Stoch.) │   │              │   │   + Inspect.) │  │
│  └─────────────┘   └──────┬───────┘   └──────────────┘  │
│                           │                              │
│                    ┌──────┴───────┐                      │
│                    │  Run Artifacts│                      │
│                    │  (run.json,   │                      │
│                    │   manifest,   │                      │
│                    │   series/)    │                      │
│                    └──────┬───────┘                      │
│                           │                              │
│                    ┌──────┴───────┐                      │
│                    │  MCP Server  │                      │
│                    │  (Agent      │                      │
│                    │   workflows) │                      │
│                    └──────────────┘                      │
└──────────────────────────────────────────────────────────┘
```

**Sim** authors templates and samples stochastic inputs into deterministic series.
**Engine** evaluates models, produces artifacts, exposes APIs.
**UI** visualizes results — topology, inspector, time-travel.
**MCP** exposes the same APIs for AI agent workflows.
**Artifacts** are the durable contract between all surfaces.

---

## Further reading

| Document | What it covers |
|----------|---------------|
| [Architecture whitepaper](architecture/whitepaper.md) | Deep engineering reference — DAG evaluation, expressions, retry patterns, artifacts. |
| [Engine capabilities](reference/engine-capabilities.md) | Authoritative snapshot of what is shipped today. |
| [Flow theory foundations](reference/flow-theory-foundations.md) | The mathematical and conceptual foundations (Little's Law, ToC, queueing theory, CFDs). |
| [Flow theory coverage](reference/flow-theory-coverage.md) | Matrix mapping theory concepts to FlowTime's current implementation status. |
| [FlowTime charter](flowtime-charter.md) | Product-level purpose, audience roles, principles. |
| [Engine charter](flowtime-engine-charter.md) | Engine-specific scope and non-goals. |
| [Nodes and expressions](concepts/nodes-and-expressions.md) | Detailed concept doc on node kinds, expression compilation, and execution model. |
| [Retry modeling](architecture/retry-modeling.md) | Deep dive on retry kernels, governance, terminal disposition, and effort edges. |
| [FlowTime vs Ptolemy](notes/flowtime-vs-ptolemy-and-related-systems.md) | Design comparison with Ptolemy, Simulink, system dynamics, and queueing network tools. |
| [Modeling documentation map](modeling.md) | Index of where different kinds of modeling information live in docs. |
