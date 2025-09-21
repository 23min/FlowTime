# FlowTime Sim Roadmap (Legacy - Charter Superseded)

> **🚀 CHARTER NOTICE**: This roadmap has been superseded by the [FlowTime-Engine Charter](../../flowtime-vnext/docs/flowtime-engine-charter.md) and [Charter Roadmap](../../flowtime-vnext/docs/milestones/CHARTER-ROADMAP.md). 
>
> **Current Development**: FlowTime-Sim follows **charter milestone sequence** (SIM-M2.7 → SIM-M2.8 → SIM-M2.9 → SIM-M3.0) aligned with Engine milestones. See [FlowTime-Sim Charter](flowtime-sim-charter.md) for current scope and [SIM-M3.0 Charter Milestone](milestones/SIM-M3.0.md) for implementation details.
>
> **Legacy Status**: The milestone sequence below represents the pre-charter roadmap preserved for historical reference.

---

## Legacy Purpose *(Charter Superseded)*
~~Synchronized milestone plan ensuring lock-step development across Engine ↔ Sim ↔ UI with artifact-first contracts.~~

**Charter Status**: 📋 **Charter Milestone Alignment** - Model authoring platform  
**Aligned Milestones**: 📋 **SIM-M2.7** → **SIM-M2.8** → **SIM-M2.9** → **SIM-M3.0**  
**Engine Dependencies**: 📋 **Engine M2.7** → **Engine M2.8** → **Engine M2.9**  
**Charter Workflow**: **[Models] → [Runs] → [Artifacts] → [Learn]**

## Harmonized Guiding Rules

1. **Artifact-first, catalog-required**: run.json, manifest.json, series/index.json, series/*.csv; **Catalog.v1 is required** stable ID source
2. **Units & IDs locked**: flows = entities/bin, levels = entities, latency = minutes; seriesId = measure@componentId[@class]  
3. **Retry/feedback deferred**: Engine M9.5 ↔ Sim SIM-M6 (not urgent)
4. **Lock-step milestones**: Engine↔Sim parity tests required on every milestone touching artifacts

## FlowTime-Sim's Role (per Charter v1.0)

FlowTime-Sim is the **modeling front-end** for FlowTime that generates models, stochastic input patterns, and templates for systems, but **never computes telemetry**. **Charter-compliant separation of concerns:**

- **✅ FlowTime-Sim Responsibilities (Charter v1.0):**
  - Generate model artifacts and stochastic input definitions  
  - Provide templates and model authoring capabilities
  - Create parameterizable models for Engine execution
  - Support model validation and Engine compatibility checking
  - **Maintain model artifacts as input to Engine ecosystem**

- **❌ FlowTime-Sim DOES NOT (Charter Boundaries):**
  - Generate telemetry or execute models for data production
  - Implement Engine's execution, expression evaluation, or DAG processing
  - Replace Engine's analytical or telemetry generation capabilities
  - Provide model execution, run processing, or telemetry computation

**Charter Integration**: Model artifacts from Sim integrate with Engine registry for execution. **Role Separation**: Sim authors models, Engine executes models and generates telemetry.

## Status Summary (✅ = Done, 🔄 = Current Priority, 🚀 = Next)

**Harmonized Engine ↔ Sim ↔ UI Development**

**Current Engine Status**: 📋 M3 (Backlog v1 + Latency) not started yet  
**Next Engine Priority**: 📋 M4 (Scenarios & Compare) or M3 - TBD  
**Retry Status**: 📋 M9.5 (Retry & Feedback) - deferred from M4.5

**Legacy FlowTime-Sim Alignment** *(Charter Superseded)*:
- **SIM-M0 — Core Foundations** — **✅ Done** (aligns with Engine M0) *(Pre-Charter)*
- **SIM-M1 — Contracts Parity Pack** — **✅ Done** (aligns with Engine M1) *(Pre-Charter)*  
- **SIM-M2 — Artifact Parity & Structure** — **✅ Done** (run.json, manifest.json, series index) *(Pre-Charter)*
- **SIM-CAT-M2 — Catalog.v1 Required** — **✅ Done** (stable ID source) *(Pre-Charter)*
- **SIM-SVC-M2 — Minimal Service/API** — **✅ Done** (artifact endpoints) *(Pre-Charter)*
- **SIM-M2.1 — PMF Generator Support** — **✅ Done** (PMF arrivals for Engine M2 testing) *(Pre-Charter)*

**Charter-Aligned Milestones** *(Current Development)*:
- **SIM-M3.0 — Charter Model Authoring Platform** — **🔄 IN PROGRESS** (Charter-compliant model artifacts creation)
  - Replaces legacy SIM-M2.6 + SIM-M2.7 sequence 
  - Dependencies: Engine M2.7 (Registry Foundation)
  - Integrates with Engine M2.8 (Charter UI) → M2.9 (Compare)

**Legacy Sequence** *(Superseded by Charter)*:
- ~~**SIM-M3 — Backlog v1 + Latency + Endpoints**~~ → **See Engine M2.7-M2.9 Charter Milestones**
- ~~**SIM-M4 — Scenarios & Compare**~~ → **See Engine M2.9 Charter Compare Workflow**
- ~~**SIM-M5 — Routing/Fan-out/Capacity**~~ → **Future Charter Milestones TBD**
- ~~**SIM-M6 — Retry & Feedback**~~ → **Deferred in Charter Scope**

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

### SIM-SVC-M2 — Minimal Service/API — **✅ Done** (Pre-Charter)

- **Goal** Stateless HTTP service exposing artifact endpoints
- **Released** 2025-09-03 (tag: sim-svc-m2)
- **Features** Legacy endpoints (superseded by charter-compliant SIM-M2.6/M2.7)
- **Charter Note** Replaced by charter-compliant model export and registry integration

### SIM-M2.1 — PMF Generator Support — **✅ Done**

- **Goal** Extend arrival generators to support PMF distributions for Engine M2 testing
- **Released** 2025-09-11 (tag: M2.1-v0.3.0)
- **Features** 
  - **PMF arrival generation**: Complete discrete probability distribution support in `SimulationSpec.cs`
  - **Environment configuration**: `FLOWTIME_API_BASEURL` and `FLOWTIME_API_VERSION` support with proper precedence
  - **Cross-container networking**: Full Docker container communication via flowtime-dev network
  - **API versioning**: Consistent `/v1/` endpoint usage across services
  - **Enhanced testing**: 88 passing tests with comprehensive integration validation
- **Acceptance** PMF workflows complete for UI testing; deterministic output with RNG seeding; Engine M2 PMF validation enabled

### SIM-M2.6 — Charter-Aligned Model Authoring — **🔄 IN PROGRESS**

- **Goal** Transform FlowTime-Sim to charter-compliant model authoring platform that creates Engine-compatible model artifacts
- **Charter Alignment** FlowTime-Sim Charter v1.0 - "modeling front-end" that generates models but never computes telemetry  
- **Dependencies** SIM-M2.1 (PMF Generator Support), FlowTime Engine M2.6 (Artifacts Registry)
- **Core Features**
  - **Charter-Compliant Model Artifacts**: Engine-compatible model export without telemetry generation
  - **Structure Validation**: Model validation without execution (charter boundary respect)
  - **Template System Enhancement**: Engine model generation from Sim templates
  - **Quality Assessment**: Structure-only quality metrics and compatibility validation
- **Charter Boundaries** NO telemetry generation, NO execution, pure model authoring focus
- **Acceptance** Model artifacts validate for Engine execution; charter compliance verified; template-to-Engine workflow complete

### SIM-M2.7 — Artifacts Registry Integration — **📋 Planned**

- **Goal** Enable FlowTime-Sim model artifacts to integrate seamlessly with FlowTime Engine M2.7 Artifacts Registry
- **Dependencies** SIM-M2.6 (Charter-Aligned Model Authoring), FlowTime Engine M2.7 (Artifacts Registry Foundation)
- **Engine Alignment** Builds on Engine M2.7 Registry KISS file-based implementation
- **Core Features**
  - **Registry Integration**: Sim model artifacts discoverable in Engine registry system
  - **Auto-Registration**: Automatic model registration with Engine registry upon creation
  - **Charter UI Support**: Models selectable in Engine Runs wizard "Select Input" step
  - **Health Monitoring**: Registry synchronization status and integration health tracking
- **Charter Workflow** `Sim (Create + Register Models) → Engine Registry (Discover Models) → Engine Execution`
- **Acceptance** Sim models appear in Engine registry; Engine UI can select Sim models; charter boundaries maintained

---

## Next Priority

### SIM-M3 — Charter-Aligned Backlog & Latency Models — **� NEXT**

- **Goal** Create charter-compliant backlog and latency model templates for Engine M3 execution.
- **Charter Alignment** Generate model definitions with backlog/latency expressions, Engine executes for telemetry
- **Dependencies** SIM-M2.7 (Registry Integration), Engine M3 (Backlog v1 + Latency execution)  
- **Supports** Engine M3 backlog/latency execution with Sim-authored model templates
- **Charter-Compliant Features** 
  - **Model Templates**: Backlog and latency model definitions for Engine execution
  - **Expression Definitions**: `backlog[t] = max(0, backlog[t-1] + inflow[t] − served[t])` unified Model artifacts
  - **Engine Integration**: Models define latency calculations for Engine to execute
  - **Registry Integration**: Backlog/latency models discoverable in Engine via SIM-M2.7
  - **Charter Boundary**: Sim creates models, Engine generates backlog.csv and latency.csv
- **Charter-Compliant CLI**
  ```bash
  # Create backlog model templates (charter-compliant)
  flowtime sim export --template backlog-queue --name "basic-backlog" --out artifacts
  flowtime sim export --template latency-analysis --name "basic-latency" --out artifacts
  
  # Models registered for Engine M3 execution via SIM-M2.7
  ```
- **Charter-Compliant Acceptance** 
  - Engine M3 can discover and execute Sim-created backlog/latency models via registry
  - Model templates generate valid Engine artifacts with backlog/latency expressions
  - Engine execution produces backlog.csv and latency.csv from Sim models
  - **Charter Integration**: Sim models → Engine registry → Engine execution → telemetry generation

---

### SIM-M4 — Charter-Aligned Scenarios & Model Variations — **� Aligned**

- **Goal** Create charter-compliant scenario model templates for Engine M4 execution and comparison.
- **Charter Alignment** Generate model variations and overlay definitions, Engine executes scenarios for comparison
- **Dependencies** SIM-M3 (Backlog Models), Engine M4 (Scenarios & Compare execution)
- **Features**
  - **Model Variation Templates**: Baseline and scenario model definitions with parameter variations
  - **Overlay Schema Generation**: Time-windowed demand/capacity modifier definitions for Engine
  - **Comparison Model Support**: Model templates designed for Engine M4 comparison workflows
  - **Charter Integration**: Scenario models discoverable in Engine registry for execution
- **Charter-Compliant CLI** 
  ```bash
  # Create scenario model variations (charter-compliant)
  flowtime sim derive --from baseline-model --overlay peak-load --name "peak-scenario" 
  flowtime sim export --template comparison-set --scenarios baseline,peak,low --out artifacts
  ```
- **Charter-Compliant Acceptance** 
  - Engine M4 can execute Sim scenario models and generate comparison telemetry
  - Model variations produce Engine-compatible overlay semantics
  - **Charter Workflow**: Sim creates scenario models → Engine executes → Engine generates delta.csv/kpi.csv

---

### SIM-M5 — Charter-Aligned Routing & Multi-Path Models — **📋 ALIGNED WITH ENGINE M5**

- **Goal** Create charter-compliant routing, fan-out, and capacity model templates for Engine M5.
- **Charter Alignment** Generate multi-path model definitions, Engine executes for routing telemetry  
- **Dependencies** SIM-M4 (Scenario Models), Engine M5 (RouterNode, FanOutNode, CapacityNode execution)
- **Charter-Compliant Features**
  - **Multi-path Model Templates**: Router and fan-out model definitions for Engine execution
  - **Capacity Constraint Models**: Overflow and capacity-limited Model artifacts for Engine
  - **Routing Logic Definitions**: Split ratios and routing rules for Engine RouterNode execution
- **Charter-Compliant Acceptance** Engine M5 can execute Sim routing models and generate multi-path telemetry; Engine computes splits and overflow from Sim models

---

### SIM-M6 — Charter-Aligned Retry & Feedback Models — **📋 DEFERRED TO ENGINE M9.5**

- **Goal** Create charter-compliant retry and feedback model templates for Engine M9.5 execution.
- **Charter Alignment** Generate retry model definitions with CONV operators, Engine executes for retry telemetry
- **Dependencies** Engine M9.5 (Retry & Feedback execution capabilities) - much later
- **Charter-Compliant Features** (Future implementation when M9.5 becomes priority)
  - **Retry Model Templates**: CONV operator and temporal echo definitions for Engine execution
  - **Feedback Loop Models**: Retry kernel and delay pattern artifacts for Engine processing
  - **Conservation Model Definitions**: Complex retry conservation rules for Engine validation
- **Status** **DEFERRED** - No longer blocking Engine development, charter-aligned when needed
- **Trigger** Engine M9.5 development becomes active priority
- **Charter-Compliant Acceptance** Engine M9.5 executes Sim retry models and generates retry telemetry with conservation

---

### SIM-M7 — Charter-Aligned Multi-Queue & Buffer Models — **📋 ALIGNED WITH ENGINE M7**

- **Goal** Create charter-compliant finite buffer and DLQ model templates for Engine M7 execution.
- **Charter Alignment** Generate buffer overflow and spill model definitions, Engine executes for queue telemetry
- **Dependencies** SIM-M5 (Routing Models), Engine M7 (Backlog v2 Multi-Queue execution)
- **Charter-Compliant Features**
  - **Buffer Limit Models**: Finite buffer and overflow model definitions for Engine execution
  - **DLQ Model Templates**: Spill-to-DLQ pattern artifacts for Engine processing
  - **Draining Policy Models**: Queue draining behavior definitions for Engine implementation
- **Charter-Compliant Acceptance** Engine M7 executes Sim buffer models and generates DLQ/spill telemetry with conservation

---

### SIM-M8 — Charter-Aligned Multi-Class & Priority Models — **📋 ALIGNED WITH ENGINE M8**

- **Goal** Create charter-compliant multi-class and priority model templates for Engine M8 execution.
- **Charter Alignment** Generate per-class model definitions and priority policies, Engine executes for multi-class telemetry
- **Dependencies** SIM-M7 (Multi-Queue Models), Engine M8 (Multi-Class + Priority/Fairness execution)
- **Charter-Compliant Features**
  - **Multi-Class Model Templates**: Per-class flow definitions for Engine execution (`arrivals@serviceA@VIP`, etc.)
  - **Priority Policy Models**: Weighted-fair and strict priority Model artifacts for Engine
  - **Capacity Sharing Models**: Class-specific capacity allocation definitions for Engine processing
- **Charter-Compliant Acceptance** 
  - Engine M8 executes Sim multi-class models and generates per-class telemetry series
  - Priority/fairness policies validated through Engine execution of Sim model definitions

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
