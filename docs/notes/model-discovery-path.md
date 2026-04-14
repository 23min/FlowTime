# Model Discovery Path

**Status:** Aspirational — partially built; key stages not yet started
**Context:** Complements E-15, Telemetry Loop & Parity, and E-18 Time Machine

---

## What model discovery means for FlowTime

A FlowTime model has two components:

1. **Topology** — the graph: which nodes exist, how they connect, what kind each is
   (source, queue, service, sink).
2. **Parameters** — the values: arrival rates, service times, parallelism, capacity
   per node, per time bin.

**Model discovery** is the process of deriving both from real-world data rather than
constructing them by hand. It is the inverse of running a model: instead of
"I have parameters, give me outputs," it asks "I have outputs (telemetry), give me
parameters."

This is not one problem. It is three sequential problems:

```
Raw data
  │
  ▼  [E-15 Gold Builder]
Canonical telemetry bundles    ← binned facts per node per time window
  │
  ▼  [E-15 Graph Builder]
Topology                       ← nodes, edges, confidence scores, human curation
  │
  ▼  [E-18 Fit + Telemetry Loop & Parity]
Fitted parameters              ← values that minimize residual vs observed telemetry
  │
  ▼
Validated model                ← synthetic run reproduces telemetry within tolerance
```

Each stage is a hard problem in its own right. None of the downstream stages can
start until the upstream one produces clean output.

---

## Stage 1 — Telemetry ingestion (E-15 Gold Builder)

**Status:** Not started. Schema defined; no ingestion pipeline exists.

Raw data (event logs, CSV exports, API feeds, OD pairs) must be normalized and
binned into FlowTime's canonical Gold format before anything else can run:

| Field | Description |
|-------|-------------|
| `timestamp` | Aligned to canonical bin grid (5m / 15m / 1h) |
| `node` | Stable node identifier |
| `flow` | Optional class/category tag |
| `arrivals`, `served`, `errors` | Core throughput signals |
| `queue_depth`, `capacity_proxy` | Optional — enable richer analytical primitives |

The pipeline is Bronze → Silver → Gold: raw payloads → normalized events → binned
facts. Gap filling, class coverage metadata, and data quality warnings are produced
as first-class artifacts — low-quality data is surfaced explicitly, not silently hidden.

**References:**
- `work/epics/E-15-telemetry-ingestion/spec.md` — full scope and milestone decomposition
- `work/epics/E-15-telemetry-ingestion/reference/dataset-fitness-and-ingestion-research.md`
  — what makes a dataset FlowTime-fit; three dataset fitness gates

---

## Stage 2 — Topology inference (E-15 Graph Builder)

**Status:** Not started. Inference methods documented; no implementation.

Given canonical telemetry, infer which nodes exist and how they connect. Methods,
from most to least reliable:

1. **Explicit transitions** — case traces with ordered activities → directly-follows graph
2. **Origin-destination pairs** — shipments, trips, tickets → natural edges
3. **Physical adjacency** — road/rail/network joins to telemetry
4. **Lagged cross-correlation** — A's outflow predicts B's inflow after a lag (weakest;
   unreliable during correlated demand spikes — needs human guardrails)

**Human curation is mandatory.** Inference will hallucinate edges. The output graph
carries confidence scores and provenance; humans accept/reject edges, merge/split
nodes, and annotate node kinds before a model is considered ready for fitting.

**References:**
- `work/epics/E-15-telemetry-ingestion/spec.md` — Graph Builder scope and milestone plan
- Target datasets: BPI Challenge 2012 (loan applications), Road Traffic Fines, PeMS,
  MTA Ridership + GTFS — see dataset fitness reference for fit assessment

---

## Stage 3 — Parameter fitting (E-18 Fit)

**Status:** Infrastructure exists; Fit endpoint not assembled.

Once topology is known, fitting finds the const-node parameter values that minimize the
residual between model output and observed telemetry. The inner loop is already built:

- `ITelemetrySource` / `FileCsvSource` / `CanonicalBundleSource` — loads observed data
- `Optimizer` (Nelder-Mead) — minimizes any scalar objective over N parameters
- `SensitivityRunner` — identifies which parameters most affect each metric; focus fitting
  effort on high-sensitivity parameters first

What is not yet assembled: a `FitSpec` / `FitRunner` that loads a telemetry bundle,
computes the residual against model output at each evaluation point, and drives the
optimizer toward minimum residual. This is E-18's planned "Fit" mode.

**Prerequisite:** Telemetry Loop & Parity — a parity harness that verifies a synthetic
run reproduces a telemetry replay within defined tolerances. Without measured drift
bounds, fitting is unvalidated: you can minimize the residual, but you cannot tell
whether the model is actually calibrated or just overfit to noise.

**References:**
- `work/epics/E-18-headless-pipeline-and-optimization/spec.md` — Time Machine epic
- `docs/architecture/time-machine-analysis-modes.md` — current analysis mode inventory
  (Fit listed as future; Optimizer as current)
- `work/epics/unplanned/telemetry-loop-parity/spec.md` — Parity harness proposal

---

## The relationship to process mining

Process mining tools (ProM, Disco, Celonis) work with event logs — individual trace
records at the activity/task level — and produce process models (Petri nets, BPMN
flow charts) plus conformance and variant analysis. FlowTime is a different kind of
tool: it works with **aggregate flow metrics** (throughput rates, queue depths,
utilizations) and executes queueing-theoretic models, not declarative process graphs.

The two are complementary, not competitive:

```
Event logs
  │
  ▼  [process mining tools — ProM, Disco, etc.]
Process graph + aggregate statistics
  (activity throughput, median service times, rework rates, routing probabilities)
  │
  ▼  [E-15 Gold Builder — if not already aggregated]
FlowTime canonical telemetry bundle
  │
  ▼  [E-15 Graph Builder or manual authoring]
FlowTime topology + fitted parameters
  │
  ▼
Live what-if, optimization, goal-seeking, sensitivity analysis
```

Process mining answers: what process is actually running, who does what, where are the
rework loops? FlowTime answers: given this process structure, what are the flow dynamics
— where will queues grow, when will capacity run out, what happens if arrival rate doubles?

Process mining output — aggregate statistics per activity — is exactly what FlowTime
needs as input. The two tools share a dataset but answer different questions.

---

## What we can do today toward model discovery

| Task | Status | How |
|------|--------|-----|
| Load canonical telemetry | Working | `FileCsvSource`, `CanonicalBundleSource` (m-E18-08) |
| Identify which parameters to fit | Working | `SensitivityRunner` — rank by \|∂metric/∂param\| |
| Fit parameters to target metric | Partial | `Optimizer` (Nelder-Mead) can minimize any residual — Fit endpoint not wired |
| Ingest raw event logs or CSV data | Not built | E-15 Gold Builder |
| Infer topology from traces | Not built | E-15 Graph Builder |
| Validate fit against telemetry | Not built | Telemetry Loop & Parity |

The path to a first end-to-end validated model from real data: E-15 M1 (Gold Builder) →
E-15 M2 (Graph Builder) → Telemetry Loop & Parity v1 → E-18 Fit mode.

---

## References

- `work/epics/E-15-telemetry-ingestion/spec.md`
- `work/epics/E-15-telemetry-ingestion/reference/dataset-fitness-and-ingestion-research.md`
- `work/epics/unplanned/telemetry-loop-parity/spec.md`
- `work/epics/E-18-headless-pipeline-and-optimization/spec.md`
- `docs/architecture/time-machine-analysis-modes.md`
- `docs/notes/ui-optimization-explorer-vision.md` — UI surface for model exploration
