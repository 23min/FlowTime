# Research: Dataset Fitness, Topology Inference, and Validation Datasets

> **Date:** 2026-03-24
> **Context:** FlowTime needs to prove it works beyond synthetic models. This document identifies what makes a dataset "FlowTime-fit," proposes a telemetry → topology pipeline, and selects candidate open datasets for early validation.

---

## 1. The Problem

FlowTime currently assumes `/graph` (topology) is provided and telemetry fills series for those nodes. This is fine for synthetic models where the user defines the DAG, but it blocks two important capabilities:

1. **Domain-agnostic adoption** — If FlowTime can only consume hand-built models, it stays a niche simulation tool. If it can ingest real data and reconstruct structure, it becomes a flow analysis platform for any domain.
2. **Validation against real systems** — Without real data, we can't prove FlowTime's analytical primitives (bottleneck ID, cycle time, WIP limits) produce useful answers. Synthetic models validate correctness but not usefulness.

---

## 2. Dataset Fitness Checklist

Three gates determine whether a dataset can drive FlowTime.

### Gate A: Can FlowTime run at all? (minimum viable)

| Requirement | What to check |
|---|---|
| **Time axis + resolution** | Timestamps exist and can be binned to a canonical grid (5m / 15m / 1h). Covers enough time to see patterns (days to weeks). |
| **Stable node identifiers** | Events can be mapped to a stable node string (service, station, department, machine, segment). |
| **A throughput signal** | At least one of: served/completed count, departed/processed count, or a defensible proxy. |

Red flags: only daily/weekly aggregates; node names change constantly; only anonymous aggregates.

### Gate B: Can you reconstruct topology? (structure)

| Requirement | What to check |
|---|---|
| **Directional dependency signal** | At least one of: explicit transitions (A → B) in data; ordered sequences per entity (case traces); origin→destination pairs; physical adjacency graph to join. |
| **Entity identity** | A case ID / shipment ID / request ID that lets you connect events into a path. Without this, edge inference falls back to correlation-guessing. |

### Gate C: Is it interesting? (the "FlowTime should exist here" test)

| Requirement | What to check |
|---|---|
| **Backlog / queue visibility** | Measured queue/WIP (best), or start/end timestamps per step (derive WIP), or latency distributions (weakest). |
| **Capacity moves over time** | Staffing schedules, shifts, machine downtime, weather impacts, policy thresholds. |
| **Heterogeneity / classes** | Different entity types, priorities, categories flowing through the same system on different paths. |
| **Feedback / rework** | Retries, re-openings, rejected/returned work. This is where causality questions get interesting. |
| **Joinable context drivers** | Weather, incidents, holidays, special events — lets FlowTime answer "why did it break?" |

---

## 3. The Missing Pipeline: Telemetry → Topology → Gold

FlowTime needs two builders that don't exist yet:

### Graph Builder (telemetry → `/graph`)

**Input:** Event logs, OD pairs, adjacency networks, or trace data.
**Output:** Nodes, edges, ports, groups — plus confidence and provenance metadata.

Methods for edge discovery (from most to least reliable):
1. **Explicit transitions** — Case traces with ordered activities → directly-follows graph
2. **Origin-destination pairs** — Shipments, flights, trips → natural edges
3. **Physical adjacency** — Road/rail/pipe networks joined to telemetry
4. **Lagged cross-correlation** — A's outflow predicts B's arrivals with a lag (weakest, needs guardrails)

Critical: the Graph Builder must support **human curation** — accept/reject edges, pin known edges, merge/split nodes, annotate node kinds. Without this, inference will hallucinate edges during correlated demand spikes.

### Gold Builder (raw events → binned facts)

**Input:** Raw event streams, API payloads, CSV dumps.
**Output:** FlowTime Gold schema — per node, per time bin:
- `timestamp` (aligned to canonical grid)
- `node` (stable ID)
- `flow` (optional class tag)
- `arrivals`, `served`, `errors`
- Optional: `queue_depth`, `oldest_age_s`, `replicas`, `capacity_proxy`

The pipeline is: **Bronze** (raw payloads) → **Silver** (normalized events) → **Gold** (binned facts).

### How confidence flows through the UI

If topology has uncertain edges:
- Solid edges for high-confidence / pinned / catalog-sourced edges
- Dotted edges for inferred / low-confidence edges
- Warnings when the model depends on low-confidence structure

This keeps FlowTime honest as a reasoning engine.

---

## 4. Dataset Families Evaluated

### Tier 1: Best fit — topology from data, high volume

#### Process Mining Event Logs (BPI Challenge datasets)

**Why this is the #1 pick for domain-agnostic FlowTime:**
- Entities = cases (loans, permits, hospital bills, purchase orders)
- Each case has a sequence of events with timestamps and activities
- Topology reconstructed from "directly-follows" relations — no external catalog needed
- Volume: tens of thousands to 100K+ cases
- Real rework loops, compliance constraints, batching, staffing, priorities

**Key datasets:**
- **BPI Challenge 2012** — Loan application process. ~13K cases, ~262K events. Activities include submission, validation, offers, acceptance. Rich rework loops.
- **BPI Challenge 2015** — Building permit applications across 5 Dutch municipalities. ~1.2K cases but complex multi-department routing.
- **BPI Challenge 2017** — Loan application with offer sub-process. ~31K cases, ~1.2M events. High volume, complex.
- **Road Traffic Fine Management** — ~150K cases. Fine → payment/appeal process. Very high volume, clear bottlenecks.
- **Sepsis Cases** — Hospital patient flow. ~1K cases but medically complex paths with urgency classes.

**FlowTime mapping:**
- Nodes = activity types (or department.activity for granularity)
- Edges = directly-follows relations with transition counts
- Arrivals = cases entering activity per bin
- Served = cases completing activity per bin
- WIP = cases in-progress (derivable from start/complete timestamps)

**Verdict:** The closest thing to a universal FlowTime demo. Proves "any process" positioning.

#### Distributed Systems Traces (Alibaba Cluster Trace 2018)

**Why it's the gold standard for IT validation:**
- Millions of requests/jobs across hundreds of services
- Explicit service → service calls → DAG per request
- Queueing via latency inflation, retries explicit in traces
- Real production noise

**FlowTime mapping:**
- Nodes = services
- Edges = RPC dependencies
- Arrivals = request starts per bin
- Served = request completions
- Backlog = inferred via Little's Law

**Verdict:** Best proof that FlowTime can reconstruct causality from telemetry alone. But IT-specific.

### Tier 2: High volume + physical network (needs structure backbone)

#### Road Traffic (PeMS + OpenStreetMap)

- Caltrans PeMS: tens of thousands of freeway detectors, 5-minute resolution
- OpenStreetMap provides the road network (topology backbone)
- Entities = vehicles (very high volume)
- Congestion = real backlog; speed drops = queue proxy
- Joinable with weather, incidents

**FlowTime mapping:**
- Nodes = road segments or detectors
- Edges = adjacency along directed road graph
- Arrivals = vehicles entering segment per bin
- Served = vehicles exiting
- Queue proxy = density/occupancy or speed-based inference

#### Transit Passengers (MTA Ridership + GTFS)

- MTA publishes hourly ridership by station (massive volume — passengers, not vehicles)
- GTFS static provides topology (stop sequences = directed edges)
- GTFS-realtime provides capacity proxy (headways) and incidents

**FlowTime mapping:**
- Nodes = stations
- Edges = station→station segments per line
- Arrivals = estimated entries per hour
- Capacity = trains/hour × assumed capacity
- Incidents from service alerts → scenario overlays

#### Airline Network (BTS On-Time + T-100)

- BTS publishes flight-level delay data + passenger counts at scale
- Nodes = airports, edges = flight segments
- High volume, weather-dependent, cascading delays

### Tier 3: Useful as context drivers (not topology sources)

- **SMHI** (Swedish weather) — Exogenous driver series, not a system graph
- **SCB** (Swedish statistics) — Demand baselines, not operational telemetry
- **Svenska kraftnät** (power grid) — Interesting but detailed topology not openly available

---

## 5. Recommended Validation Path

### Phase A: Process mining as first proof (earliest — can start now)

**Dataset:** BPI Challenge 2012 or Road Traffic Fine Management

**Why first:**
- No external topology needed — structure emerges from case traces
- Gold generation is straightforward (bin events by activity per time window)
- Validates the Graph Builder and Gold Builder pipeline end-to-end
- Domain-agnostic — proves "FlowTime for any process"

**What it proves:**
- FlowTime can consume real data, not just synthetic models
- Bottleneck ID (Phase 3.1) produces meaningful answers on real rework loops
- Cycle time decomposition (Phase 3.2) works on real process stages

**Prerequisite:** Phase 3 analytical primitives (otherwise FlowTime can only show throughput/queue, which any process mining tool already does).

### Phase B: Physical network as second proof (after ingestion pipeline exists)

**Dataset:** PeMS + OpenStreetMap, or MTA + GTFS

**Why second:**
- Requires a topology backbone (OSM or GTFS) — tests the "join" path
- High volume stresses the engine
- Incidents and weather as context drivers test scenario overlays

**What it proves:**
- FlowTime works on physical systems, not just business processes
- The topology + telemetry join pattern is viable
- Visualizations epic has real data to render

### Phase C: Distributed systems trace (when ready for IT positioning)

**Dataset:** Alibaba Cluster Trace 2018

**Why third:**
- Most complex: topology inference from request traces
- Validates the "reconstruct causality from telemetry alone" thesis
- IT-specific — saves this for when FlowTime is already proven on simpler domains

---

## 6. What This Means for the Roadmap

1. **Telemetry Ingestion epic** needs to be broadened to include Graph Builder (topology inference) and Gold Builder, not just "TelemetryLoader for ADX/parquet."
2. **Phase 3 analytical primitives** are prerequisites for datasets to be interesting in FlowTime (otherwise it's just a dashboard).
3. **Process mining event logs** can be used for early validation even before the full ingestion pipeline exists — they're small enough to pre-process manually into Gold format.
4. **The dag-map spike** matters here too — whatever topology emerges from data needs to render well.

---

## 7. References

- BPI Challenge datasets: https://www.win.tue.nl/bpi/
- Alibaba Cluster Trace: https://github.com/alibaba/clusterdata
- Caltrans PeMS: https://pems.dot.ca.gov/
- MTA ridership data: https://data.ny.gov/
- GTFS feeds: https://gtfs.org/
- Trafiklab (Swedish transport): https://www.trafiklab.se/
- FlowTime Gold schema: `docs/schemas/telemetry-manifest.schema.json`
- Telemetry Ingestion epic: `work/epics/telemetry-ingestion/spec.md`
