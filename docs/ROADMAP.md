# FlowTime Sim Roadmap (v2.0, Harmonized with Engine ↔ UI)

> **Purpose:** Synchronized milestone plan ensuring lock-step development across Engine ↔ Sim ↔ UI with artifact-first contracts.

**Current Engine Status**: 📋 M3 (Backlog v1 + Latency) not started yet  
**Next Engine Priority**: 📋 M4 (Scenarios & Compare) or M3 - TBD  
**Retry Status**: 📋 M9.5 (Retry & Feedback) - deferred from M4.5

## Harmonized Guiding Rules

1. **Artifact-first, catalog-required**: run.json, manifest.json, series/index.json, series/*.csv; **Catalog.v1 is required** stable ID source
2. **Units & IDs locked**: flows = entities/bin, levels = entities, latency = minutes; seriesId = measure@componentId[@class]  
3. **Retry/feedback deferred**: Engine M9.5 ↔ Sim SIM-M6 (not urgent)
4. **Lock-step milestones**: Engine↔Sim parity tests required on every milestone touching artifacts

## FlowTime-Sim's Role (per Harmonized Plan)

FlowTime-Sim is the **synthetic generator companion** producing telemetry-like data ("Gold" series) with **artifact-first, catalog-required contracts**. **Critical separation of concerns:**

- **✅ FlowTime-Sim Responsibilities:**
  - Generate synthetic telemetry when real telemetry unavailable
  - Produce Gold series validating Engine features at each milestone
  - Create training/calibration data for parameter learners  
  - Support deterministic replay with seeded stochastic draws
  - **Maintain Catalog.v1 as required stable ID source**

- **❌ FlowTime-Sim DOES NOT:**
  - Implement Engine's expression language (SHIFT, CONV, MIN, MAX, etc.)
  - Evaluate DAG models or perform flow analysis
  - Replace Engine's analytical capabilities
  - Provide real-time flow processing or decision making

**Schema Compatibility**: Identical artifact schemas and API contracts between Engine and Sim. **Units & IDs locked**: flows = entities/bin, levels = entities, latency = minutes; seriesId = measure@componentId[@class].

## Status Summary (✅ = Done, 🔄 = Current Priority, 🚀 = Next)

**Harmonized Engine ↔ Sim ↔ UI Development**

**Current Engine Status**: 📋 M3 (Backlog v1 + Latency) not started yet  
**Next Engine Priority**: 📋 M4 (Scenarios & Compare) or M3 - TBD  
**Retry Status**: 📋 M9.5 (Retry & Feedback) - deferred from M4.5

**FlowTime-Sim Alignment** (following harmonized milestone sequence):
- **SIM-M0 — Core Foundations** — **✅ Done** (aligns with Engine M0)
- **SIM-M1 — Contracts Parity Pack** — **✅ Done** (aligns with Engine M1)  
- **SIM-M2 — Artifact Parity & Structure** — **✅ Done** (run.json, manifest.json, series index)
- **SIM-CAT-M2 — Catalog.v1 Required** — **✅ Done** (stable ID source)
- **SIM-SVC-M2 — Minimal Service/API** — **✅ Done** (artifact endpoints)
- **SIM-M2.1 — PMF Generator Support** — **📋 Planned** (PMF arrivals for Engine M2 testing)
- **SIM-M3 — Backlog v1 + Latency + Endpoints** — **🔄 PRIORITY** (Basic queues, ready for Engine M3)
- **SIM-M4 — Scenarios & Compare** — **🚀 Next** (Overlay support for Engine M4)
- **SIM-M5 — Routing/Fan-out/Capacity** — **📋 Aligned** (Multi-path for Engine M5)
- **SIM-M6 — Retry & Feedback** — **📋 Deferred** (Aligns with Engine M9.5)

---

## Completed Milestones

### SIM-M0 — Core Foundations — **✅ Done**

- **Goal** Canonical grid, Series<T>, basic synthetic data generation
- **Aligns with** Engine M0 (Core Foundations)
- **Acceptance** Deterministic eval, cycle detection, unit tests

### SIM-M1 — Contracts Parity Pack — **✅ Done**

- **Goal** Dual-write artifacts (run/manifest/index), JSON schema validation, deterministic hashing
- **Aligns with** Engine M1 (Contracts Parity Pack)
- **Acceptance** Schema validation in CI; CLI vs API parity; Sim pack consumable by Engine adapters

### SIM-M2 — Artifact Parity & Structure — **✅ Done**

- **Goal** Standardize artifact structure with dual JSON artifacts and per-series CSV files
- **Released** 2025-09-02 (tag: sim-m2)
- **Features** 
  - **Artifact structure**: `run.json`, `manifest.json`, `series/index.json`, per-series CSV files
  - **Deterministic hashing**: SHA-256 integrity for all artifacts with tamper detection
  - **Schema compliance**: Standardized artifact contracts for Engine compatibility
  - **Deprecations**: Removed legacy `metadata.json` and single `gold.csv` patterns
- **Acceptance** Deterministic hashing; tamper detection; series index discovery; Engine artifact compatibility

### SIM-CAT-M2 — Catalog.v1 Required — **✅ Done**

- **Goal** Domain-neutral catalog as stable ID source for both Engine & Sim
- **Released** 2025-09-03 (tag: sim-cat-m2)
- **Features** Component/connection structure, API endpoints, validation
- **Acceptance** Catalog.v1 required (not optional); stable component IDs

### SIM-SVC-M2 — Minimal Service/API — **✅ Done**

- **Goal** Stateless HTTP service exposing artifact endpoints
- **Released** 2025-09-03 (tag: sim-svc-m2)
- **Features** POST /sim/run, GET /sim/runs/{id}/series/{seriesId}, overlay support
- **Acceptance** CLI vs API parity; artifact streaming; scenario registry

---

## Current Gap & Planned Work

### SIM-M2.1 — PMF Generator Support — **📋 Planned**

- **Goal** Extend arrival generators to support PMF distributions for Engine M2 testing
- **Why** Engine M2 PMF support is complete, but FlowTime-Sim cannot generate PMF-based synthetic arrivals
- **Gap** UI and testing workflows need PMF synthetic data but `ArrivalGenerators` only supports `const` and `poisson`
- **Scope**
  - Add `pmf` kind to `ArrivalGenerators.Generate()` method
  - Support discrete value distributions with probability masses
  - PMF validation (probabilities sum to 1.0, non-negative values)
  - Maintain deterministic output with RNG seeding
- **Example Usage**
  ```yaml
  arrivals:
    kind: pmf
    values: [1, 2, 3, 5]
    probabilities: [0.1, 0.3, 0.4, 0.2]
  ```
- **Enables** Complete PMF workflows for UI testing, demo scenarios, and Engine validation
- **Priority** High - needed for UI PMF workflow completeness

---

## Next Priority

### SIM-M3 — Backlog v1 + Latency + Artifact Endpoints — **🔄 PRIORITY**

- **Goal** Generate basic queues and latency for Engine M3, consolidating queuing basics.
- **Why** Engine M3 (Backlog v1 + Latency) pulled forward. Basic queues only - later M7 = Backlog v2 (buffers & spill).
- **Engine Status** 📋 M3 not started yet, SIM-M3 ready when needed  
- **Supports** Engine M3 (Backlog v1 + Latency + Artifact Endpoints)
- **Core Features** 
  - **backlog[t] = max(0, backlog[t-1] + inflow[t] − served[t])**
  - **latency[t] = served[t]==0 ? 0 : backlog[t]/served[t]*binMinutes**
  - **Emit series/backlog.csv, series/latency.csv**
  - **Basic generator**: arrivals, served, capacity, backlog derived identically to Engine's formula
  - **No retry echoes** (deferred to SIM-M6)
- **CLI**
  ```
  flow-sim gen basic --config basic-config.yaml --out runs/backlog-v1 --seed 42
  # Basic queues for Engine M3 development
  ```
- **Acceptance** 
  - Engine M3 processes FlowTime-Sim artifacts without errors  
  - Conservation holds: backlog formula matches Engine exactly
  - Latency div-by-zero safe; file streaming endpoints pass
  - **Integration validated**: Engine M3 development proceeds with synthetic data

---

### SIM-M4 — Scenarios & Compare — **🚀 NEXT**

- **Goal** Generate scenario variations using overlay patterns for Engine M4.
- **Why** Engine M4 implements overlay YAML and compare CLI. FlowTime-Sim must generate baseline and scenario data.
- **Aligned with** Engine M4 (Scenarios & Compare)
- **Features**
  - **Overlay framework**: Apply time-windowed demand/capacity modifiers
  - **Scenario generation**: Baseline + variants matching Engine M4 overlay semantics
  - **Compare support**: Generate delta.csv and kpi.csv equivalents
- **CLI** 
  ```
  flow-sim scenarios --base baseline.yaml --overlays peak-load.yaml --out runs/scenarios
  ```
- **Acceptance** 
  - Engine M4 compare functionality processes scenario artifacts correctly
  - Overlay invariants maintained; compare reproducible across Engine/Sim artifacts
  - **Integration validated**: Engine M4 scenario features work with synthetic data

---

### SIM-M5 — Routing/Fan-out/Capacity Caps — **📋 ALIGNED WITH ENGINE M5**

- **Goal** Generate synthetic data for routing, fan-out, and capacity constraints.
- **Why** Engine M5 implements RouterNode, FanOutNode, CapacityNode. FlowTime-Sim must provide test data.
- **Aligned with** Engine M5 (Routing, Fan-out, Capacity Caps)
- **Features**
  - **Multi-path routing**: Generate flows across multiple downstream paths
  - **Fan-out patterns**: Replicated flows for testing FanOutNode
  - **Capacity overflow**: Generate overflow series for capacity-constrained scenarios
- **Acceptance** Engine M5 routing features validate correctly with synthetic multi-path data; splits sum to 1; overflow computed

---

### SIM-M6 — Retry & Feedback Modeling — **📋 DEFERRED TO ENGINE M9.5**

- **Goal** Generate synthetic retry patterns that validate Engine M9.5 retry & feedback capabilities.
- **Why** Engine moved retry features to M9.5. No longer urgent - deferred until Engine M9.5 active.
- **Aligned with** Engine M9.5 (Retry & Feedback Modeling) - much later
- **Features** (Future implementation when M9.5 becomes priority)
  - **CONV operator validation**: Generate synthetic data for Engine M9.5 retry testing
  - **Temporal echoes**: Retry kernels create realistic delay patterns
  - **Conservation compliance**: Complex retry conservation validation
- **Status** **DEFERRED** - No longer blocking Engine development
- **Trigger** Engine M9.5 development becomes active priority
- **Acceptance** Engine M9.5 retry volumes match kernels; conservation holds with retries & DLQ

---

### SIM-M7 — Backlog v2 (Multi-Queue Features) — **📋 ALIGNED WITH ENGINE M7**

- **Goal** Generate synthetic data for finite buffers, basic spill to DLQ, configurable draining.
- **Why** Engine M7 extends queues beyond basic M3. FlowTime-Sim must provide spill validation data.
- **Aligned with** Engine M7 (Backlog v2 Multi-Queue Features)
- **Features**
  - **Finite buffer patterns**: Generate overflow when buffers reach limits
  - **DLQ spill series**: Emit synthetic DLQ/spill series to validate Engine behaviors
  - **Draining policies**: Different queue draining patterns
- **Acceptance** Conservation with spill; buffer limits enforced; Engine M7 DLQ/spill indicators work

---

### SIM-M8 — Multi-Class + Priority/Fairness — **📋 ALIGNED WITH ENGINE M8**

- **Goal** Generate per-class synthetic data with priority/fairness policies.
- **Why** Engine M8 implements multi-class flows with capacity sharing. FlowTime-Sim must provide per-class test data.
- **Aligned with** Engine M8 (Multi-Class + Priority/Fairness)
- **Features**
  - **Per-class series**: Generate `arrivals@serviceA@VIP.csv`, `served@serviceA@STANDARD.csv`
  - **Capacity sharing**: Test data for weighted-fair vs strict priority allocation
  - **Class-specific policies**: Different behaviors per class (VIP vs Standard)
- **Outputs** Class-segmented Gold series following Engine M8 naming convention
- **Acceptance** 
  - Engine M8 multi-class processing works correctly with synthetic per-class data
  - Priority/fairness policies validated under capacity constraints

---

### SIM-M9 — Data Import & Fitting — **📋 ALIGNED WITH ENGINE M9**

- **Goal** Generate synthetic data patterns that match real telemetry characteristics.
- **Why** Engine M9 implements data import & fitting. FlowTime-Sim should support calibration workflows.
- **Aligned with** Engine M9 (Data Import & Fitting)
- **Features**
  - **Telemetry-like patterns**: Generate synthetic data that resembles real system behavior
  - **Parameter fitting**: Support calibration of synthetic generators against real data
  - **Validation datasets**: Provide known-good synthetic data for fitting algorithm testing
- **Acceptance** Engine M9 fitting algorithms work correctly with FlowTime-Sim calibration data

---

## Cross-Cutting Contract & Process Gates

### Required for Every Milestone

1. **Catalog.v1 is required**: remove all "optional" language - both Engine & Sim require catalog
2. **Series ID/units are normative**: any change must bump schema version
3. **Engine↔Sim parity tests are required gates**: on every milestone that touches artifacts
4. **UI consumes artifacts only**: no bespoke payloads

### Always-on Acceptance Gates

- **Determinism:** same spec + seed ⇒ identical artifacts (hash-stable)
- **Schema guard:** reject unknown top-level scenario keys unless `x-…`
- **Time alignment:** all timestamps divisible by `binMinutes`
- **Contracts parity:** CLI and Service write **identical artifacts**
- **Catalog join test:** bijection between `Catalog.components[].id` and Gold `component_id`
- **Unit parity:** Engine's adapters consume Sim pack with no overrides
- **Schema compatibility:** FlowTime-Sim artifacts validate against Engine without modification
- **Integration validation:** Each phase demonstrates actual Engine compatibility before advancement

---

## Repository Layout

```
flow-sim/
├─ src/
│  ├─ Cli/                        # CLI entrypoints
│  ├─ Core/                       # planner, sequencer, distributions
│  ├─ Generators/                 # arrivals, capacity, backlog, scenarios
│  ├─ Writers/                    # events + Gold writers (shared)
│  └─ Service/                    # Minimal Sim HTTP service
├─ catalogs/                      # Catalog.v1 files (REQUIRED, domain-neutral)
│  ├─ tiny-demo.yaml
│  └─ baseline.yaml
├─ specs/                         # scenarios & overlays
│  ├─ baseline.weekday.yaml
│  ├─ peak_day.overlay.yaml
│  └─ outage.overlay.yaml
├─ tests/
│  ├─ determinism/
│  ├─ schema/
│  ├─ generators/
│  ├─ integration/                # Engine↔Sim parity tests
│  └─ service/
└─ docs/
   ├─ ROADMAP.md
   ├─ architecture/
   └─ schemas/
```
