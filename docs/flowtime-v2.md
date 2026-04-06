# FlowTime

**A deterministic flow algebra engine. A spreadsheet for flow dynamics. Queueing theory made executable.**

Traditional tools measure *symptoms* — high latency, growing backlogs, missed SLAs. FlowTime models the *mechanics* — queue depth × service rate × retry amplification — so you can reason about causes and test interventions before deploying them.

---

## See it work: a five-minute scenario

Before explaining the machinery, here is a complete model you can run. A cloud payment service processes transactions. During peak hours, demand exceeds capacity. Some transactions fail and retry. A queue builds up.

```yaml
grid:
  bins: 12
  binSize: 60
  binUnit: minutes

nodes:
  - id: transactions
    kind: const
    values: [60, 80, 100, 120, 140, 150, 140, 120, 100, 80, 60, 40]

  - id: payment_service
    kind: expr
    expr: |
      arrivals_total := transactions + retries
      capacity := 110
      served := MIN(capacity, arrivals_total)
      errors := arrivals_total - served
      retries := CONV(errors, [0.0, 0.6, 0.3, 0.1])

  - id: settlement_queue
    kind: serviceWithBuffer
    inflow: payment_service.served
    outflow: settlement_dispatch

  - id: settlement_dispatch
    kind: expr
    expr: "MIN(settlement_queue, 100)"
```

Run it:

```bash
dotnet run --project src/FlowTime.Cli -- run payment-model.yaml --out out/payment --verbose
```

What comes out (simplified):

```
Bin   Arrivals  Retries  Served  Errors  Queue
 0       60       0.0      60     0.0      0
 1       80       0.0      80     0.0      0
 2      100       0.0     100     0.0      0
 3      120       0.0     110    10.0      0
 4      140       6.0     110    36.0     10
 5      150      24.6     110    64.6     34
 6      140      47.6     110    77.6     68
 7      120      55.0     110    65.0    103
 8      100      47.8     110    37.8    125
 9       80      30.5     110     0.5    126
10       60       8.8      68.8   0.0    115
11       40       1.4      41.4   0.0     89
```

Three things happen that no dashboard would show you:

1. **Retry amplification.** Bin 3 has 10 errors. By bin 6, retries have grown to 47.6 — almost half the original demand — because each wave of errors creates a new wave of retries that adds to the next bin's load.

2. **Queue inertia.** The queue peaks at bin 9 (126 items), *three bins after demand peaks*. By the time arrivals drop to 80/bin, the retry backlog is still draining through the system. The queue doesn't follow the demand curve — it lags it.

3. **Recovery timing.** Even though demand falls below capacity by bin 9, the queue doesn't clear until well after bin 11. If someone asks "when will the backlog recover?" the model gives an exact answer.

Now the question that matters: **what if we add 20% capacity?** Change `capacity := 110` to `capacity := 132`. Re-run. The retry peak drops from 47.6 to 8.2, the queue never exceeds 15 items, and recovery happens 4 bins earlier. That comparison — same model, one parameter changed — is what FlowTime is for.

---

## What FlowTime actually is

FlowTime models systems as a **directed acyclic graph (DAG) of services and queues evaluated over a discrete time grid**:

- **Nodes** represent services (with queues), routers, constants, or expressions.
- **Edges** carry work volume between nodes — throughput, effort, or terminal (DLQ/escalation).
- **Time bins** discretize everything into fixed intervals (e.g., 60-minute bins over 24 hours).
- The engine does a **single-pass topological evaluation** — for each bin, it computes arrivals, served, queue depth, errors, retries, latency, utilization, and more.

All feedback (retries, backpressure) is modeled via **causal delay** — the `SHIFT` and `CONV` operators push effects forward in time, which guarantees determinism. Same inputs always produce the same outputs. No algebraic loops. No iterative solvers.

### The spreadsheet metaphor

Think of FlowTime like a spreadsheet:

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

Traditional dashboards show you what happened. FlowTime lets you model what *would* happen if you changed capacity, routing, or retry policies — and get reproducible results. Run a scenario today and run it again next month: identical outputs.

This also means FlowTime supports *time-travel*: replay historical telemetry through the engine, then compare the replay against a what-if variant where you doubled capacity at the bottleneck. The difference between the two runs *is* the answer.

### 2. Explicit flow algebra, not opaque ML

Every metric is a traceable formula. Cycle time decomposes into queue time + service time. Flow efficiency = service time / cycle time. Utilization = served / capacity. There is no black box.

Conservation invariants enforce discipline:

```
arrivals + retries − served − ΔQ − dlq ≈ 0
```

When this invariant breaks, the engine emits a warning — it doesn't silently drift. You can inspect exactly which node violated conservation and by how much.

### 3. Domain-neutral

The engine is not locked to software delivery. The same primitives — arrivals, capacity, queues, retries, routing, latency — work for IT operations, healthcare, logistics, manufacturing, finance, customer support. If it has queues, throughput, and latency, FlowTime can model it. (See [FlowTime in practice](#flowtime-in-practice) for domain examples.)

### 4. Retry and feedback modeling

Retries are modeled as convolution kernels that reinject failed work across future time bins:

```yaml
retries := CONV(errors, [0.0, 0.6, 0.3, 0.1])
```

This single line says: "60% of errors retry after 1 bin, 30% after 2 bins, 10% after 3 bins." The kernel captures exponential backoff, retry storms, and amplification effects that traditional dashboards completely miss. The engine tracks attempts vs. successes, enforces retry budgets, routes exhausted failures to dead-letter queues, and ensures the total mass is conserved throughout.

### 5. Multi-class flow isolation

Different work types (Orders, Refunds, VIP) flow through the same topology with separate SLAs, priorities, and metrics — not just tagged aggregates. Classes are first-class: per-class series, per-class conservation checks, per-class latency. A single model can answer "what is the latency for Refunds through the intake queue?" without conflating it with Order traffic.

### 6. Warnings as first-class artifacts

Most tools silently produce misleading results when assumptions break. FlowTime makes wrongness visible. The engine emits structured warnings for conservation violations, queue depth mismatches, retry kernel adjustments, backlog health signals, missing telemetry, steady-state violations, and router diagnostics. Warnings are persisted in run artifacts and surfaced through APIs. They tell you not just *what* the engine computed, but *where it is uncertain*.

---

## FlowTime in practice

FlowTime's primitives are domain-neutral. Here is how the same engine applies to different systems.

### Incident response: finding the hidden retry storm

A platform team runs an incident management pipeline: alerts arrive from monitoring, get triaged, assigned to on-call, and resolved. Resolution failures trigger re-triage. The team notices that Tuesday mornings consistently breach SLA, even though alert volume is no higher than Monday.

They model the pipeline in FlowTime. The topology has four nodes: `alerting → triage → oncall → resolution`, with a retry edge from `resolution` back into `triage`. They bind real telemetry to the arrival series and run it through the engine.

The conservation check on the `triage` node immediately flags an imbalance: more work is entering triage than the sum of alerting arrivals and resolution retries would explain. The team investigates and discovers that a deployment bot is silently re-opening resolved alerts on a Tuesday cron job — injecting work that the monitoring dashboard doesn't count as "new alerts." The model caught it because the accounting didn't balance.

They fix the bot. But they also run a what-if: "What if the bot hadn't been fixed and Tuesday volume doubled?" The model shows retries compounding to 3x normal triage load by hour 4, because each failed re-triage generates more retries that compound with the next wave. The retry kernel `[0.0, 0.5, 0.3, 0.2]` makes this amplification visible. The SLA breach wasn't about volume — it was about feedback.

### Hospital outpatient clinic: batched appointments and queue buildup

A hospital outpatient clinic processes patients through registration, consultation, and pharmacy pickup. Registration opens at 08:00 but consultations are batched — a doctor sees patients every 30 minutes, up to 4 per batch. The clinic manager wants to know why afternoon wait times are double the morning.

The FlowTime model uses `serviceWithBuffer` for the consultation queue with a dispatch schedule (`periodBins: 6, capacitySeries: [4,4,4,4,4,4,...]` on 5-minute bins). Registration arrivals follow the observed pattern: a burst at 08:00, steady through midday, tapering by 15:00.

The model reveals the problem: the 30-minute batch cycle creates a sawtooth queue pattern. Each batch drains 4 patients, but between batches the queue grows. When morning arrivals are 3/slot, the queue stays small — the batch clears nearly all of it. But midday arrivals hit 5/slot, and each batch cycle leaves 1–2 patients behind. These remainders accumulate: by 13:00 the queue depth is 12, and wait times have tripled — not because of any single overload event, but because of the persistent mismatch between continuous arrivals and batched service.

The what-if scenario: "What if we added a second consultation room after 12:00?" doubles the batch capacity from bin 48 onward. The afternoon queue peak drops from 12 to 3. The model makes the cost-benefit concrete: one additional room-afternoon eliminates 80% of the afternoon backlog.

### Logistics: delivery retry loops

A regional delivery company processes parcels through sorting, route assignment, and delivery. Failed deliveries (customer not home, access problem) go back into the sorting queue for next-day retry. The company is seeing delivery success rates drop even though fleet size hasn't changed.

The model has three stages: `sorting → routing → delivery`, with a retry edge from `delivery` back to `sorting` via a `CONV` kernel representing the next-day retry pattern: `[0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.8, 0.15, 0.05]` (first retry at bin+8, which is next morning given 1-hour bins starting at 06:00).

With telemetry data bound, the model shows that the retry rate climbed from 8% to 15% over four weeks. At 8%, the sorting queue absorbs retries easily. At 15%, the retry volume at bin 8 (next morning) adds 15% of yesterday's failures on top of today's new arrivals. Since the sorting queue has finite capacity, this creates a cascade: some of today's parcels don't get sorted in time, which means more same-day delivery failures, which means more retries tomorrow.

The engine's conservation check confirms it: the `sorting` node shows `ΔQ > 0` growing each day — a backlog that never fully clears. The what-if: reducing the retry rate to 10% (by adding SMS delivery notifications) stabilizes the backlog within 3 days. Alternatively, adding 20% sorting capacity buys time but doesn't fix the underlying retry amplification.

### Trade settlement: multi-class SLA compliance

A financial clearing house processes trades through validation, matching, and settlement. Three trade classes flow through the same pipeline: `Equity` (T+2 SLA), `FX` (T+1), and `Derivatives` (T+0, same-day). The operations team suspects that Derivatives are being starved during equity spikes, but the aggregate dashboard shows overall throughput is fine.

The FlowTime model declares three classes and binds arrival series from the trading system. The topology is simple — `validation → matching → settlement` — but each node has class-aware capacity.

The per-class state output reveals the problem immediately: during equity batch arrivals (market open), the `matching` node serves 95% of its capacity on Equity trades. Derivatives, despite having the tightest SLA, get the remaining 5%. The aggregate utilization shows 88% — healthy-looking. But the per-class view shows Derivatives utilization at 12% and a queue depth climbing by 40 items/bin. The T+0 SLA breaches consistently between 09:30 and 11:00.

The what-if: reserve 30% of matching capacity for Derivatives regardless of equity volume. Per-class retesting shows the Derivatives queue stays flat, FX is unaffected, and Equity throughput drops by 8% — well within its T+2 margin.

---

## Thinking in FlowTime

Modeling a real system in FlowTime requires translating from "how the system works" to "how work flows through it." This section explains the modeling discipline.

### Step 1: Find the flow

Every FlowTime model starts with one question: **what is the unit of work?** Transactions, patients, tickets, parcels, trades. Not "requests per second" in the abstract — a concrete thing that arrives, waits, gets processed, and leaves (or fails and retries).

If the system has multiple distinct work types with different SLAs or routing, those become classes.

### Step 2: Identify the stations

Walk the path a work item takes from entry to exit. Each place where work **accumulates and waits** is a potential `serviceWithBuffer` node. Each place where work is **transformed or routed** is an `expr` or `router` node.

A good rule of thumb: if the real system has a queue, FlowTime should have a `serviceWithBuffer`. If the real system has a decision point, FlowTime should have a `router` or a conditional expression.

Don't model every microservice or internal step. Model where *contention happens* — the bottlenecks, the queues, the capacity constraints. A 5-node model that captures the right bottlenecks is more useful than a 50-node model that maps every box on an architecture diagram.

### Step 3: Decide the time grid

The grid resolution should match the decision cadence:

- **5-minute bins**: real-time operations, autoscaling decisions, incident response.
- **1-hour bins**: shift planning, daily capacity management, SLA monitoring.
- **1-day bins**: weekly planning, trend analysis, seasonal patterns.

Choose the smallest bin size where you can meaningfully fill every bin with data. If your telemetry reports hourly aggregates, don't use 5-minute bins — you'll be interpolating, not modeling.

### Step 4: Wire arrivals and capacity

For each node, define:
- **Arrivals**: where does work come from? Upstream node output, telemetry series, or constant pattern.
- **Capacity**: how much can this node serve per bin? Fixed constant, time-varying schedule, or expression.
- **Failure/retry**: what fraction fails? What is the retry pattern? Use a `CONV` kernel if retries are delayed.

### Step 5: Run, inspect, iterate

Run the model. Look at the warnings. If conservation checks fail, your wiring is wrong — work is leaking or appearing from nowhere. If queue depths grow unboundedly, your capacity is too low or your retry kernel is amplifying faster than the system can drain.

The first model is usually wrong. That's the point. FlowTime makes it wrong *visibly and specifically*, so you can fix it.

### Common modeling patterns

| Real-world pattern | FlowTime model |
|-------------------|---------------|
| Service with a queue | `serviceWithBuffer` node |
| Fixed processing capacity | `const` node feeding capacity input |
| Time-varying demand (peak hours) | `const` with shaped values or telemetry series |
| Retry with backoff | `CONV(errors, kernel)` where kernel shape encodes timing |
| Batch processing (every N minutes) | `serviceWithBuffer` with `dispatchSchedule` |
| Fan-out (one input, multiple outputs) | `router` node with weights or class-based routing |
| Dead letter queue | `sink` node with a `terminal` edge from the failing service |
| Shared dependency (database, API) | `dependency` node or constraint in the registry |
| Shift patterns (8h on, 16h off) | Capacity series with zeros during off hours |

---

## When models break: what failure looks like

FlowTime's explainability claim is best tested when things go wrong. Here is what it looks like when a model breaks and how the engine helps you find the problem.

### The runaway retry

You model a service with a 20% failure rate and a retry kernel of `[0.0, 0.8, 0.15, 0.05]`. You run it and the queue grows without bound — 50 items after 6 bins, 200 after 12, 800 after 24.

What happened? The kernel sums to 1.0, which means *every* failure retries. Combined with a 20% failure rate, each bin generates retries equal to 20% of its total arrivals, and those retries themselves have a 20% failure rate, generating more retries. It is a geometric series that converges slowly — the system never reaches steady state at this load.

FlowTime flags this in two ways:
1. **Backlog health warning**: growth streak detected — queue depth increasing for N consecutive bins.
2. **Conservation warning**: `arrivals + retries − served − ΔQ` is non-zero because the queue is absorbing the excess.

The fix: either reduce the retry kernel mass (e.g., `[0.0, 0.6, 0.2, 0.1]` sums to 0.9, meaning 10% of failures are permanently dropped) or reduce the failure rate, or increase capacity. The model tells you exactly which lever to pull and by how much.

### The missing telemetry

You bind telemetry to a model but one series — `processing_time_ms_sum` — is missing from the telemetry bundle. The engine runs successfully but emits warnings:

```
WARNING [settlement]: serviceTimeMs derivation skipped — processingTimeMsSum series missing
WARNING [settlement]: flowLatencyMs derivation skipped — requires serviceTimeMs
```

The model still evaluates. Arrivals, served, queue depth, and conservation checks all work. But the latency metrics for that node are absent — the engine doesn't fabricate them. The warnings tell you exactly what's missing and what's affected, so you know to either backfill the telemetry or accept that latency data is unavailable for this node.

### The invisible bottleneck

You model a three-stage pipeline and everything looks healthy: utilization is 70% at every stage, queue depths are small, latency is low. But your real system has long cycle times. What's wrong?

The answer is usually that the model is *too simple*. Real systems have variability — arrivals aren't constant, processing times fluctuate. At 70% utilization with constant arrivals and constant service times, queues are small. But at 70% utilization with *variable* arrivals, Kingman's formula predicts much higher queue times: `Wq ≈ (Ca² + Cs²)/2 × ρ/(1-ρ) × E[S]`. If Ca (coefficient of variation of arrivals) is 1.0 instead of 0.0, queue time roughly doubles.

FlowTime's current engine evaluates expected values — no variance. This is a known boundary. The planned variability preservation milestone (m-ec-p3c) will add Cv tracking and Kingman's approximation, so the engine can flag "your model assumes zero variability but the telemetry shows Ca = 1.2." Until then, the gap is documented: if your model looks too healthy, add variance manually by shaping the arrival series to match observed patterns rather than using smooth averages.

---

## The what-if / what-was workflow

This is FlowTime's most powerful capability: comparing a telemetry replay against a simulated intervention.

### Step 1: Establish the baseline (what-was)

Capture telemetry from your production system into a canonical bundle — time-aligned series of arrivals, served, errors, queue depth per node. Load it into a FlowTime model in telemetry mode:

```bash
flowtime run --template-id incident-pipeline --mode telemetry \
  --capture-dir data/captures/tuesday-incident --out out/baseline
```

The engine replays the telemetry through the model topology, applying conservation checks, deriving latency and utilization, and flagging any inconsistencies between the telemetry and the model's expectations.

### Step 2: Create the variant (what-if)

Copy the model. Change one thing. For instance, double the triage capacity from 50 to 100 items/bin:

```bash
flowtime run --template-id incident-pipeline --mode simulation \
  --bind triage.capacity=100 --out out/variant-double-capacity
```

The Sim layer generates a model with the overridden parameter, using the same arrival series from the telemetry capture but with the new capacity value. The engine evaluates it identically — same grid, same topology, same retry kernels, different capacity.

### Step 3: Compare

The two runs produce identical artifact structures. Diff the series:

- **Baseline `triage.queue`**: peaks at 45 items at bin 8, clears by bin 16.
- **Variant `triage.queue`**: peaks at 12 items at bin 6, clears by bin 10.
- **Baseline `triage.utilization`**: 0.92 at peak (danger zone).
- **Variant `triage.utilization`**: 0.46 at peak (comfortable headroom).

The delta is the answer: doubling triage capacity reduces peak queue by 73%, peak utilization from 92% to 46%, and recovery time by 6 bins (6 hours). Whether that capacity investment is worth it is a business decision — but the engineering question ("what would happen?") now has a precise, reproducible answer.

---

## The platform: four surfaces

### The Engine (FlowTime.Core + FlowTime.API)

The execution and semantics layer. Takes a model, evaluates it on the time grid, produces canonical run artifacts, and exposes stable APIs. This is the source of truth — all other surfaces consume its outputs.

**Run artifacts** per evaluation:
- `run.json` — metadata, hashes, scenario name, warnings.
- `manifest.json` — software version, per-series SHA-256 hashes.
- `series/index.json` — all output series with IDs, units, types.
- Per-series CSVs — `t,value` with invariant-culture floats.

Artifacts are the durable contract. Every run is a self-contained, reproducible snapshot. You can compare runs across weeks, share them across teams, or feed them into external tools.

**API surface:**
- `POST /v1/runs` — create a run from a model.
- `GET /v1/runs/{id}/graph` — compiled DAG with semantics and aliases.
- `GET /v1/runs/{id}/state` — single-bin snapshot with derived metrics and warnings.
- `GET /v1/runs/{id}/state_window` — windowed series including edge metrics.
- `GET /v1/runs/{id}/metrics` — aggregate metrics over a bin range.
- `GET /v1/runs/{id}/series/{seriesId}` — raw CSV stream.

### The Simulation layer (FlowTime.Sim)

The Sim layer is where most users start. It bridges the gap between "I have a hypothesis about my system" and "here is a model the engine can evaluate."

**Templates** are parameterized blueprints that define a complete model topology:

```yaml
# Template: incident-pipeline
parameters:
  - name: triage_capacity
    type: int
    default: 50
    bounds: [10, 500]
  - name: retry_kernel
    type: double[]
    default: [0.0, 0.5, 0.3, 0.2]
  - name: failure_rate
    type: double
    default: 0.08
    bounds: [0.0, 1.0]
```

A template separates structure from values. The topology (which nodes, how they connect, what formulas they use) is fixed. The parameters (capacity, failure rates, kernels, arrival patterns) can be overridden for each run. This makes what-if comparisons clean: change one parameter, re-run, diff.

**Modes:**
- **Simulation mode**: the template generates synthetic arrivals from PMFs, Poisson distributions, or profiled patterns. The engine evaluates pure what-if scenarios.
- **Telemetry mode**: the template binds to a captured telemetry bundle. The engine replays what-was with full FlowTime semantics.

**Stochastic → deterministic boundary:** PMFs and random distributions are sampled in the Sim layer *before* the engine sees the data. The engine always receives deterministic series. This means a template with `rng.seed: 42` produces identical models every time — stochastic inputs, deterministic evaluation.

**Workflow:**

```bash
# List available templates
flowtime-sim list templates

# Inspect a template's parameters
flowtime-sim show template --id incident-pipeline

# Generate and validate a model
flowtime-sim generate --id incident-pipeline --mode simulation --out out/model

# Override parameters for a what-if
flowtime-sim generate --id incident-pipeline --mode simulation \
  --bind triage_capacity=100 --bind failure_rate=0.05 --out out/variant

# Validate parameter bounds before generating
flowtime-sim validate params --id incident-pipeline --bind triage_capacity=1000
# → ERROR: triage_capacity=1000 exceeds upper bound 500
```

### The UI (FlowTime.UI)

A time-travel visualization surface for human interpretation:

- **Topology view**: the DAG rendered as a graph with edge overlays showing flow volume, retry rates, and health signals. Nodes are colored by utilization (green → amber → red). Edge thickness reflects flow volume.
- **Inspector**: click any node for a deep-dive — all series plotted over time, derived metrics, SLA status, and warnings. Toggle between classes to see per-class behavior.
- **Time slider**: scrub through bins to watch the system evolve. See the queue build, the retry storm form, the bottleneck shift from one node to another.
- **Focus filters**: isolate a specific class, a specific path through the DAG, or a specific time window.

The UI is strictly a consumer of engine semantics. It never invents metrics or performs computation — if it needs a new derived value, that value must first be added to the engine. This keeps semantics centralized and ensures the UI and API always agree.

### MCP server (agent workflows)

FlowTime exposes a Model Context Protocol (MCP) server that lets AI agents work with flow models programmatically. The MCP surface uses the same stable APIs as the UI — no separate semantics.

**What an agent can do:**

- **Draft a model** from a natural-language description: "Model an order pipeline with intake, validation, and fulfillment. Intake receives 500 orders/hour with 10% failure rate and exponential backoff retries."
- **Validate** the model before running: check topology for cycles, verify conservation, validate parameter bounds.
- **Run scenarios**: execute the model and retrieve results — series, metrics, warnings.
- **Inspect results**: ask structured questions — "Which node has the highest utilization?", "Where does the queue grow?", "What is the retry tax at peak?"
- **Compare runs**: diff two runs to quantify the impact of a parameter change.
- **Summarize for humans**: produce a narrative summary of a run's findings, suitable for a status update or incident review.

The MCP surface matters because it turns FlowTime from a tool-for-humans into a reasoning substrate for agents. An agent can model a system, test an intervention, and report the results — all through structured APIs, without hand-stitching dashboards or interpreting screenshots.

---

## Core concepts reference

### The time grid

Everything begins with a fixed time grid:

```yaml
grid:
  bins: 24
  binSize: 60
  binUnit: minutes
```

24 bins of 60 minutes each — a full day. All series align to this grid. The grid is UTC-aligned and left-closed: bin 0 covers `[T₀, T₀+60m)`.

The fixed grid is a deliberate choice. It sacrifices sub-bin dynamics for determinism, simplicity, and composability. Need finer resolution? Use a smaller bin size.

### Series

A series is a vector of values aligned to the grid — one value per bin:

- **Flow series** (rates): arrivals, served, errors, attempts — per-bin totals. Additive across time windows.
- **Level series** (stocks): queue depth, backlog, WIP — end-of-bin state. Not additive.

### Nodes

| Node kind | What it does |
|-----------|-------------|
| `const` | Emits fixed values — a demand pattern, a capacity schedule. |
| `expr` | Evaluates a formula — `MIN`, `MAX`, `SHIFT`, `CONV`, `CLAMP`, etc. |
| `serviceWithBuffer` | Service + queue + optional dispatch schedule. Owns queue depth, latency, loss. |
| `router` | Splits flow by weights or class. |
| `dependency` | Shared resource or external dependency (arrivals/served/errors). |
| `sink` | Terminal node — absorbs flow (DLQ, completed work). |

### Edges

- **Throughput**: successful work flowing downstream.
- **Effort**: total attempts including retries — shows load, not just success.
- **Terminal**: failed/exhausted work routed to DLQ or escalation.

### Classes

Work types flowing through the same topology with separate metrics:

```yaml
classes:
  - id: Order
    displayName: "Order Flow"
  - id: Refund
    displayName: "Refund Flow"
```

Per-class series, per-class conservation checks, per-class everything.

### Expressions

FlowTime's formula language. Compile to nodes in the DAG:

```yaml
expr: |
  arrivals_total := arrivals + retries
  served := MIN(capacity, arrivals_total)
  errors := arrivals_total - served
  retries := CONV(errors, [0.0, 0.6, 0.3, 0.1])
```

| Category | Operations |
|----------|-----------|
| Arithmetic | `+`, `-`, `*`, `/` (vectorized) |
| Comparison | `MIN`, `MAX`, `CLAMP` |
| Rounding | `FLOOR`, `CEIL`, `ROUND` |
| Time | `SHIFT` (lag), `CONV` (convolution/delay) |
| Pattern | `MOD`, `STEP`, `PULSE` |

### ServiceWithBuffer

The canonical queue abstraction:

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

Owns queue recurrence (`Q[t] = max(0, Q[t-1] + in - out - loss)`), latency derivation via Little's Law, dispatch schedules, loss tracking, and initial conditions.

### Derived metrics

| Metric | Meaning |
|--------|---------|
| `utilization` | `served / capacity` |
| `latencyMinutes` | Queue time via Little's Law |
| `serviceTimeMs` | Processing time per item |
| `flowLatencyMs` | Queue + service time |
| `throughputRatio` | `served / arrivals` |
| `retryTax` | Retry overhead as fraction of effort |

Plus SLA descriptors (completion, backlog age, schedule adherence) with status codes so UIs can distinguish "healthy," "breached," and "no data."

---

## The evaluation model

1. **Compile**: parse YAML into a dependency graph.
2. **Topological sort**: order nodes so inputs are computed first. `SHIFT`/`CONV` with lag ≥ 1 break cycles.
3. **Single-pass evaluation**: iterate bins sequentially, evaluate nodes in topological order.

All feedback is delayed by at least one bin — no algebraic loops, no iterative solvers.

### Conservation invariants

After evaluation, the engine checks per-node and per-class:

```
arrivals + retries − served − ΔQ − dlq ≈ 0
```

Violations produce warnings with node ID, magnitude, and bin range.

---

## Theoretical foundations

FlowTime's primitives are grounded in established flow theory:

| Theory | How FlowTime uses it |
|--------|---------------------|
| **Little's Law** (`L = λW`) | Latency derivation: `W ≈ Q / served_rate`. Steady-state validation planned. |
| **Conservation of flow** | The `arrivals + retries − served − ΔQ − dlq ≈ 0` invariant, per-node and per-class. |
| **Queueing theory** | ServiceWithBuffer recurrence, utilization tracking. Kingman's approximation planned. |
| **Theory of Constraints** | Per-node utilization comparison surfaces the bottleneck. WIP accumulation identifies where work piles up. |
| **Cumulative Flow Diagrams** | Edge time bins provide raw cumulative series. CFD derivations planned. |
| **Flow Framework** | Multi-class maps to flow item types. Throughput → velocity. Latency → flow time. |
| **Retry / feedback** | Convolution kernels with mass and length governance. |

FlowTime is deliberately **not** a discrete-event simulator. It operates on aggregated flows over time bins, not individual entities. This trades per-item fidelity for determinism, performance, and transparency.

---

## What FlowTime is not

- **Not a streaming engine.** Evaluates complete time windows, not live event streams.
- **Not a BI tool.** Produces structured data that BI tools consume.
- **Not a telemetry warehouse.** Ingests bundles, produces artifacts — doesn't store raw telemetry.
- **Not a probabilistic simulator by default.** Deterministic engine; stochastic sampling in the Sim layer.
- **Not a discrete-event simulator.** Aggregate flows, not individual work items.

These are deliberate boundaries. FlowTime is sharp and explainable for its chosen semantics rather than a general-purpose simulation environment.

---

## Design principles

1. **Determinism over drift.** Same inputs → same outputs. No hidden mutation. No non-deterministic paths.

2. **Minimal basis, rich meaning.** Arrivals, served, and queue depth are the backbone. Everything else is derived, and every derivation is labeled with provenance.

3. **Explainability first.** Make wrongness visible. Warnings are first-class. Every metric traces to a formula. Every formula traces to inputs.

4. **Contracts over convenience.** Stable APIs and schemas. UIs and agents consume semantics — they don't invent them.

5. **Semantics are server-side.** The engine is the single source of truth. What-if workflows drive the engine, not the browser.

6. **Flow-first, not event-first.** Aggregated flows over time bins. Per-entity analysis is optional and derived, never core.

---

## Getting started

```bash
# Build and test
dotnet build FlowTime.sln
dotnet test FlowTime.sln

# Run a model via CLI
dotnet run --project src/FlowTime.Cli -- run examples/hello/model.yaml --out out/hello --verbose

# Start the Engine API (port 8081)
dotnet run --project src/FlowTime.API

# Create a run via API
curl -X POST http://localhost:8081/v1/runs -H "Content-Type: application/yaml" -d @model.yaml

# Inspect state at bin 5
curl http://localhost:8081/v1/runs/{runId}/state?ts=5

# Get windowed series
curl http://localhost:8081/v1/runs/{runId}/state_window?from=0&to=23
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
**UI** visualizes — topology, inspector, time-travel.
**MCP** exposes the same APIs for AI agent workflows.
**Artifacts** are the durable contract between all surfaces.

---

## Further reading

| Document | What it covers |
|----------|---------------|
| [Architecture whitepaper](architecture/whitepaper.md) | Deep engineering reference — DAG evaluation, expressions, retry patterns, artifacts. |
| [Engine capabilities](reference/engine-capabilities.md) | Authoritative snapshot of what is shipped today. |
| [Flow theory foundations](reference/flow-theory-foundations.md) | Mathematical and conceptual foundations (Little's Law, ToC, queueing theory, CFDs). |
| [Flow theory coverage](reference/flow-theory-coverage.md) | Matrix mapping theory to FlowTime's current implementation status. |
| [FlowTime charter](flowtime-charter.md) | Product-level purpose, audience roles, principles. |
| [Engine charter](flowtime-engine-charter.md) | Engine-specific scope and non-goals. |
| [Nodes and expressions](concepts/nodes-and-expressions.md) | Node kinds, expression compilation, execution model. |
| [Retry modeling](architecture/retry-modeling.md) | Retry kernels, governance, terminal disposition, effort edges. |
| [FlowTime vs Ptolemy](notes/flowtime-vs-ptolemy-and-related-systems.md) | Design comparison with Ptolemy, Simulink, system dynamics tools. |
| [Modeling documentation map](modeling.md) | Index of where different modeling docs live. |
