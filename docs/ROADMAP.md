Here’s a structured **FlowTime-Sim Roadmap** you can use as a guide. I followed the format of your FlowTime roadmapand your engineering ground doc, while also weaving in the ideas around PMFs, time windows, and synthetic load generation.

---

# FlowTime-Sim Roadmap (Draft v1.0)

> **Audience**: Engineers, architects, domain experts building simulations.  
> **Purpose**: Define the milestones and architecture of FlowTime-Sim, the synthetic data generator for FlowTime.  
> **Scope**: FlowTime-Sim provides synthetic “Gold” datasets for FlowTime in supported domains, enabling experimentation, demos, and statistical calibration when real telemetry is unavailable or incomplete.

---

## Introduction: What FlowTime-Sim is and why it exists

**One-liner**  
FlowTime-Sim is a spec-driven simulation engine that produces synthetic event streams and Gold telemetry for FlowTime, enabling a “lab environment” for experimentation, demos, and what-if analysis.

**Why needed**  
FlowTime itself is an engine and observability tool, but it is data-hungry. In many cases:

- Real telemetry is unavailable, restricted, or incomplete.
    
- Early adoption and CI need **sample data**.
    
- Domain experts want to test scenarios (e.g., “Black Friday,” “Airport disruption”) before telemetry exists.
    

FlowTime-Sim solves this by **creating realistic inputs** at the edges of modeled systems. It generates arrivals, routing, retries, and timing distributions consistent with how entities move in domains like:

- Logistics (packages through depots and hubs)
    
- Transportation (buses, trains, fleets)
    
- Factory/Manufacturing (work orders through stations)
    
- Cloud Systems (requests through services/queues)
    

**How used**

- FlowTime-Sim runs standalone (`flow-sim run scenario.yaml`).
    
- It outputs **events** (NDJSON/Parquet) or **Gold series** (Parquet).
    
- FlowTime consumes these via the **Synthetic Adapter** (SYN-M0).
    
- Developers and analysts can test FlowTime without Azure/App Insights dependencies.
    

**Relation to PMFs**  
FlowTime-Sim can output:

- **Absolute series** (curve-first mode).
    
- **PMFs** (shape-first mode) that describe demand distributions (daily/weekly profiles, retry kernels).  
    PMFs are optional, but they make synthetic data more portable and scenario-driven.
    

**Example (Logistics)**  
A “parcel” is created with pickup time, goes to origin depot, linehaul, hub, destination depot, and last-mile delivery. FlowTime-Sim generates event sequences for thousands of parcels, including wave batching, retries, and failures. FlowTime ingests this to show throughput, backlog, and SLA attainment over time.

---

## Principles

1. **Spec-driven**: Scenarios defined in YAML, versioned, deterministic with seeds.
    
2. **Gold contract**: Outputs must match FlowTime’s Gold schema (entity/time aligned).
    
3. **Lightweight**: No infra required. Local runs must work on a laptop.
    
4. **Reproducible**: Same scenario + seed → identical outputs.
    
5. **Multi-domain**: Start with logistics, extend to transport, manufacturing, IT.
    
6. **Modes**:
    
    - FileDrop mode (offline Parquet/NDJSON).
        
    - Streaming mode (optional, for live demos).
        

---

## Data Contracts

### Event schema (synthetic events)

```
entity_id: string
event_type: string
ts: timestamp (UTC)
attrs: map<string, string|number|bool>
```

### Gold schema (series)

```
timestamp (UTC, aligned to grid e.g. 5m/hourly)
node: string          -- e.g. “Depot.STO-A”, “Service.Auth”
flow: string or "*"   -- class/flow (e.g. EXPRESS, STANDARD, VIP)
arrivals: double
served: double
errors: double
queue_depth: double?  -- optional
```

### PMFs

- Daily PMF (24 bins) or Weekly PMF (168 bins).
    
- Retry/delay kernels as short PMFs (lags).
    

---

## Milestones

### SIM-M0 — Skeleton & Contracts

- Goal: Minimum viable FlowTime-Sim.
    
- CLI `flow-sim run scenario.yaml --out ./out`.
    
- YAML specs: arrivals (constant or Poisson), one simple route.
    
- Emit NDJSON events + convert to Gold Parquet.
    
- Deterministic with seed.
    
- Acceptance: FlowTime Synthetic Adapter can ingest outputs and stitch edges.
    

### SIM-M1 — Domain Templates (Logistics, Transport)

- Goal: Ready-to-use scenario packs.
    
- Logistics: depot → hub → depot → delivery (with batching).
    
- Transport: bus/train arrivals, station dwell, passenger flows.
    
- Adds distributions: lognormal service times, gamma travel times.
    
- Acceptance: Two example scenarios produce plausible daily curves.
    

### SIM-M2 — PMF Support

- Goal: Output shape-first PMFs (daily/weekly) instead of absolute curves.
    
- Convert telemetry → empirical PMFs, or use synthetic PMFs.
    
- Acceptance: FlowTime consumes PMFs × scalar totals to recreate synthetic demand.
    

### SIM-M3 — Streaming Mode

- Goal: Optional live demo playback.
    
- Scheduler replays event time at configurable speed (e.g. 10×).
    
- Publishes to local MQ or STDOUT.
    
- Acceptance: FlowTime Synthetic Adapter reads stream and updates incrementally.
    

### SIM-M4 — Scenarios & Overlays

- Goal: Apply scenario YAML overlays (surge, outage, reroute).
    
- Implement demand multipliers, capacity outages, routing shifts.
    
- Acceptance: “Black Friday surge” overlay produces expected load increase.
    

### SIM-M5 — Exceptions, Retries, Fan-out

- Goal: Model failures and retries via retry PMFs. Add fan-out (entity → multiple).
    
- Acceptance: Retry loads appear with expected delay; fan-out increases flow volumes.
    

### SIM-M6 — Multi-Class Flows

- Goal: Multiple flows (EXPRESS vs STANDARD). Add priority/fairness knobs.
    
- Acceptance: Scenarios can change flow mix; FlowTime reports SLA differences.
    

### SIM-M7 — Calibration Mode

- Goal: Fit parameters (PMFs, service times) from real telemetry.
    
- Acceptance: Feeding telemetry CSV reproduces a parameter pack that matches observed distributions.
    

### SIM-M8 — Library of Scenarios

- Goal: Publish a catalog of realistic scenarios (per domain).
    
- Acceptance: Demos and CI can pick from named scenarios.
    

---

## Repository Layout (Proposed)

```
flow-sim/
├─ src/
│  ├─ Cli/            # CLI entrypoints
│  ├─ Core/           # Planner, Sequencer, Distributions
│  ├─ Emitters/       # NDJSON, Parquet, PMF
│  └─ Domains/        # Logistics, Transport, Manufacturing templates
├─ specs/
│  ├─ logistics.weekday.yaml
│  ├─ logistics.blackfriday.yaml
│  ├─ transport.morningrush.yaml
├─ samples/
│  ├─ weekday-10k/
│  └─ blackfriday-10k/
└─ docs/
   ├─ roadmap.md
   └─ contracts.md
```

---

## Mapping FlowTime ↔ FlowTime-Sim Milestones

|FlowTime milestone|Description|FlowTime-Sim support|
|---|---|---|
|**M0 (Engine skeleton)**|Canonical grid, CSV outputs|SIM-M0 (basic generator producing Gold schema)|
|**M2 (PMF support)**|PMF nodes in FlowTime engine|SIM-M2 (emit PMFs and totals)|
|**M4 (Scenarios & Compare)**|Overlay YAML (surge, outage)|SIM-M4 (generate scenario overlays)|
|**M5 (Routing, Fan-out)**|Multi-path, capacity caps|SIM-M5 (generate fan-out, retries, failures)|
|**M7 (Backlog + Latency)**|Real queues and latency metrics|SIM-M1/M5 (queue arrivals/served, retry loads)|
|**M8 (Multi-Class)**|Priority/fairness across flows|SIM-M6 (generate EXPRESS/STD classes)|
|**M9 (Data Import & Fitting)**|Fit model parameters from telemetry|SIM-M7 (calibration mode producing parameter packs)|
|**M10 (Scenario Sweeps)**|Sensitivity analysis across ranges|SIM-M8 (library of scenarios, parameter variation)|

---

## Conclusion

FlowTime-Sim is the **twin of FlowTime’s engine**:

- FlowTime models & analyzes flows.
    
- FlowTime-Sim creates believable flows to feed FlowTime.
    

Together, they provide a **lab environment** for experimentation, demos, calibration, and teaching.

---

Would you like me to also draft a **contracts.md** (event schema + Gold schema + PMF JSON example) so both FlowTime and FlowTime-Sim repos have the same clear data contract?