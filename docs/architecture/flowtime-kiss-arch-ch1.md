# FlowTime Time-Travel Architecture: KISS Approach

**Version:** 2.0  
**Date:** October 8, 2025  
**Status:** Architectural Design  
**Authors:** FlowTime Architecture Team

---

## Document Structure

This architecture is split across multiple chapters:

1. **Chapter 1**: Executive Summary, Principles, System Context
2. **Chapter 2**: Data Contracts (Gold Schema, Model Schema, API)
3. **Chapter 3**: Component Design (TelemetryLoader, Templates, Engine)
4. **Chapter 4**: Data Flows and Examples
5. **Chapter 5**: Implementation Roadmap and Milestones
6. **Chapter 6**: Decision Log and Appendices

---

## Chapter 1: Executive Summary and Principles

### 1.1 Executive Summary

#### The Core Insight

FlowTime's time-travel capability is built on a **single-mode, telemetry-as-input architecture** that treats historical telemetry and simulated data identically. This enables both **replay** (time-travel over historical data) and **exploration** (what-if scenarios) through a unified execution model.

**Key Principle:** Telemetry is just another input format. The Engine evaluates expressions deterministically regardless of data source.

#### Why KISS (Keep It Simple, Stupid)?

The original Gold-First architecture attempted to solve too many problems simultaneously:
- Semantic mapping inference
- Real-time validation
- Capacity estimation
- Dual-mode logic
- Observed vs modeled reconciliation

This complexity created fragility and implementation risk.

**KISS Solution:**
```
Telemetry (CSV/JSON) → Model Template (YAML) → Engine (Pure Evaluation) → Artifacts
```

No adapters, no mode switching, no semantic inference. Just data + formulas = results.

#### Architecture Overview

```
┌────────────────────────────────────────────────────────────────┐
│ TELEMETRY LAYER (Azure Data Explorer)                         │
│ • NodeTimeBin: Base observations only                         │
│   - arrivals, served, errors (REQUIRED)                       │
│   - queue_depth, external_demand (OPTIONAL)                   │
│ • NO derived metrics stored (latency, utilization)            │
│ • NO capacity_proxy required                                  │
└────────────────────────┬───────────────────────────────────────┘
                         │
                         ▼
            ┌────────────────────────────┐
            │ TelemetryLoader            │
            │ • Query ADX (simple KQL)   │
            │ • Write CSV/JSON files     │
            │ • ~200 lines of code       │
            └────────────────────────────┘
                         │
                         ▼
            ┌────────────────────────────┐
            │ Model Templates            │
            │ • Hand-authored YAML       │
            │ • Topology + Expressions   │
            │ • Version-controlled       │
            │ • References telemetry     │
            └────────────────────────────┘
                         │
                         ▼
┌────────────────────────────────────────────────────────────────┐
│ ENGINE (Pure, Unchanged from M2.10)                           │
│ • Load const series from files                                │
│ • Evaluate expressions                                         │
│ • Compute stateful dynamics (SHIFT)                           │
│ • No telemetry logic                                          │
│ • No capacity required                                        │
└────────────────────────┬───────────────────────────────────────┘
                         │
                         ▼
            ┌────────────────────────────┐
            │ Run Artifacts              │
            │ • run.json (metadata)      │
            │ • topology.json (UI)       │
            │ • series/*.csv (outputs)   │
            └────────────────────────────┘
```

#### Key Simplifications

| Aspect | Original Gold-First | KISS Approach |
|--------|-------------------|---------------|
| **Telemetry Integration** | Complex adapter with semantic mapping | Simple CSV export from ADX |
| **Capacity Handling** | Required capacity_proxy | Optional, inference-based |
| **Topology Source** | Generated from Catalog_Nodes | Hand-authored templates |
| **Engine Modes** | Dual mode (Gold vs Model) | Single mode (unified) |
| **Validation** | During adaptation (blocking) | Post-evaluation (warnings) |
| **Initial Conditions** | Implicit defaults | Explicit in expressions |
| **Code Complexity** | ~1000 LOC adapter | ~200 LOC loader |

#### What This Document Covers

1. **Architecture Principles** - KISS philosophy and constraints
2. **Data Contracts** - Minimal Gold schema, Model format, API contracts
3. **Component Design** - TelemetryLoader, Templates, Engine extensions
4. **Data Flows** - End-to-end examples with real telemetry
5. **Implementation Roadmap** - 4 milestones, 10 days total
6. **Decision Log** - Why capacity-free, why templates, tradeoffs
7. **Migration Path** - From M2.10 to production-ready

#### Success Criteria

**For Junior Engineers:**
- Clear acceptance criteria per milestone
- Explicit file formats and schemas
- Step-by-step implementation guidance
- Test patterns and golden examples

**For Senior Engineers:**
- Component boundaries and contracts
- Integration patterns and error handling
- Performance targets and scalability
- Extension points for future features

**For Architects:**
- Design rationale and tradeoffs
- System evolution strategy
- Operational considerations
- Risk mitigation approaches

---

### 1.2 Architecture Principles

#### P1: Telemetry is Just File Input

**Principle:** Telemetry data is treated identically to any other const series input. The Engine loads arrays from files and doesn't distinguish between telemetry, synthetic, or hand-authored data.

**Rationale:**
- Eliminates special-case logic in Engine
- Makes testing trivial (CSV fixtures)
- Enables multiple data sources without engine changes
- Clear separation: data extraction vs computation

**Example:**
```yaml
# Engine sees this (doesn't care about origin):
nodes:
  - id: arrivals
    kind: const
    source: "file://data/arrivals.csv"  # Could be telemetry, synthetic, or manual

  - id: served
    kind: expr
    expr: "arrivals - errors"  # Pure computation
```

**Contrast with Original:**
```yaml
# Original approach (rejected):
nodes:
  - id: arrivals
    kind: gold_query
    query: "NodeTimeBin | where node == 'Orders'"  # Engine knows about ADX
```

#### P2: Capacity is Optional and Inferred

**Principle:** The Engine does NOT require capacity as input. Capacity can be inferred post-evaluation when needed for visualization, with clear labeling of inference method and confidence.

**Rationale:**
- Capacity is unknowable until services hit saturation
- All capacity proxies (replicas, config, throughput) are flawed
- Observed `served` is ground truth for replay
- What-if scenarios can specify capacity explicitly

**Implications:**
- Gold schema: capacity_proxy is OPTIONAL
- Engine formulas: Use observed `served`, not `MIN(arrivals, capacity)`
- API responses: Label inferred capacity with method and confidence
- Overlays: User provides capacity assumptions explicitly

**Example:**
```yaml
# Replay mode (no capacity needed):
- id: served_observed
  kind: const
  source: "telemetry/served.csv"  # From Gold

# Inference (for visualization only):
- id: capacity_inferred
  kind: expr
  expr: "infer_from_saturation(served_observed, queue_depth)"

topology:
  nodes:
    - id: "OrderService"
      semantics:
        served: "served_observed"           # Use observed
        capacity: "capacity_inferred"       # Optional, labeled
```

#### P3: Templates Define Structure, Not Adapters

**Principle:** System topology and expression formulas are version-controlled YAML templates, not generated by adapters querying metadata tables.

**Rationale:**
- Topology is design artifact (should be in git)
- Changes to topology require engineering review
- Templates can be shared and parameterized
- No runtime dependency on Catalog_Nodes table
- Same template works for telemetry and simulation

**Template Structure:**
```yaml
# templates/order-system.yaml
templateType: "capacity-planning"
description: "Order processing system"

parameters:
  - name: data_source
    type: enum
    values: [telemetry, simulation, manual]
  
  - name: q0
    type: number
    default: 0
    description: "Initial queue depth"

topology:
  nodes:
    - id: "OrderService"
      kind: "service"
      group: "Orders"
      ui: { x: 120, y: 260 }
  
  edges:
    - from: "OrderService:out"
      to: "OrderQueue:in"

expressions:
  # Data-source agnostic formulas
  - id: queue_depth
    kind: expr
    expr: "MAX(0, SHIFT(queue_depth, 1) + arrivals - served)"
    initial: "{{q0}}"
```

#### P4: Explicit Initial Conditions

**Principle:** All self-referencing expressions (e.g., `SHIFT(x, 1)` where x references itself) MUST declare explicit initial conditions. No implicit defaults.

**Rationale:**
- Implicit q0=0 causes confusion (real queues often start non-empty)
- Explicit initial conditions enable validation (compare q0 to telemetry)
- Clear semantics for edge cases (start-of-window behavior)
- Forces engineers to think about initial state

**Validation:**
```
Parser Rule: If expr contains SHIFT(node_id, k) where k>0 and node_id == current node:
  REQUIRE: initial field is present
  ERROR: "Self-referencing SHIFT requires explicit 'initial' value"
```

**Example:**
```yaml
# ✅ Valid:
- id: queue_depth
  kind: expr
  expr: "MAX(0, SHIFT(queue_depth, 1) + inflow - outflow)"
  initial: 5  # Explicit

# ❌ Invalid (parse error):
- id: queue_depth
  kind: expr
  expr: "MAX(0, SHIFT(queue_depth, 1) + inflow - outflow)"
  # Missing initial field
```

#### P5: Queue-Centric Telemetry Model

**Principle:** For queue-based services, distinguish between external demand (queue arrivals) and service throughput (queue completions). Queue depth accumulates the difference.

**Rationale:**
- Reflects actual system architecture (Service Bus + processing pods)
- Service arrivals ≈ service served (small error rate)
- Gap is between external demand and service capacity
- Queue depth is directly observable from Service Bus

**Telemetry Mapping:**
```
External System → Queue → Service → Queue → External System

Observables:
1. queue_incoming (external demand from Service Bus IncomingMessages)
2. queue_completed (service consumption from Service Bus CompletedMessages)
3. queue_depth (backlog from Service Bus ActiveMessageCount)
4. service_processed (completions from App Insights)
5. service_errors (failures from App Insights)

Relationships:
- arrivals_to_queue = queue_incoming
- arrivals_to_service = queue_completed
- served_by_service = service_processed
- queue_depth[t] = queue_depth[t-1] + arrivals_to_queue[t] - arrivals_to_service[t]
```

#### P6: Validation is Post-Evaluation

**Principle:** Validation checks run AFTER model evaluation and produce warnings, not errors. Invalid data doesn't block runs.

**Rationale:**
- Real telemetry is messy (gaps, spikes, inconsistencies)
- Blocking on validation prevents investigation of problems
- Warnings highlight issues without stopping analysis
- Engineers decide how to handle violations

**Validation Types:**
```yaml
validation:
  - type: "conservation"
    formula: "arrivals - served - (queue[t] - queue[t-1])"
    tolerance: 0.01
    action: "warn"  # Not "error"
  
  - type: "capacity_realism"
    formula: "capacity_inferred > 10 * MAX(served)"
    action: "warn"
    message: "Inferred capacity unrealistically high"
```

#### P7: UTC and Bin Discipline

**Principle:** All timestamps are UTC. Windows are `[start, end)` (start inclusive, end exclusive). Bin boundaries align to grid (no partial bins).

**Rationale:**
- Matches Azure Data Explorer bin() semantics
- Prevents timezone conversion bugs
- Simplifies timestamp arithmetic
- Aligns with ISO-8601 standards

**Validation:**
```
Request Window Validation:
1. window.start MUST be UTC (contains 'Z' suffix)
2. window.start MUST align to bin boundary: start % binMinutes == 0
3. window.end = start + (bins × binMinutes)
4. API query ?ts=<timestamp> MUST align to bin start
```

#### P8: API Responses Include binMinutes

**Principle:** Engine derives `binMinutes = binSize × toMinutes(binUnit)` and includes it in ALL API responses alongside grid.

**Rationale:**
- Single scalar for timestamp arithmetic in clients
- Prevents unit conversion bugs (minutes vs hours vs days)
- Simplifies Little's Law calculations: `latency = queue / (served/binMinutes)`
- Consistent across UI, CLI, notebooks

**Conversion Table:**
```
binUnit: "minutes" → 1
binUnit: "hours"   → 60
binUnit: "days"    → 1440
binUnit: "weeks"   → 10080
```

---

### 1.3 Non-Goals

What this architecture explicitly does NOT do:

#### NG1: Real-Time Telemetry

**Not a Goal:** Sub-minute latency from production events to FlowTime visualization.

**Why:** Gold MVs are batch-updated (typical lag: 5-15 minutes). This is acceptable for time-travel investigations.

**Mitigation:** Provenance includes `extraction_ts` so users know data freshness.

#### NG2: Schema Inference

**Not a Goal:** Automatically detect topology from telemetry patterns or service discovery.

**Why:** Topology is a design artifact requiring human judgment (grouping, layout, semantic meaning).

**Alternative:** Hand-authored templates (version-controlled, reviewed).

#### NG3: Capacity Prediction

**Not a Goal:** Predict future capacity needs or autoscaling thresholds.

**Why:** Capacity planning requires business context, growth models, cost constraints beyond telemetry.

**Alternative:** What-if scenarios with user-specified capacity assumptions.

#### NG4: Multi-Tenant Isolation

**Not a Goal:** Enforce tenant boundaries or access control within Engine.

**Why:** Engine produces immutable artifacts. Access control happens at UI/API layer.

**Alternative:** Artifacts include tenant_id for filtering; UI enforces permissions.

#### NG5: Distributed Execution

**Not a Goal:** Scale Engine horizontally across multiple nodes for single model evaluation.

**Why:** Current models (300 nodes × 288 bins) fit in single-node memory and compute in <1s.

**Alternative:** Scale via multiple independent runs (different models/windows), not sharding.

#### NG6: Interactive Query Rewrites

**Not a Goal:** Allow users to modify KQL queries or expression formulas in UI.

**Why:** Creates security risks, validation complexity, and non-reproducible runs.

**Alternative:** Templates are parameterized but formulas are fixed. Advanced users can fork templates.

---

### 1.4 System Context

#### Deployment Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│ PRODUCTION SYSTEMS                                              │
│ • Kubernetes pods (OrderService, BillingService, ...)          │
│ • Azure Service Bus queues (orders-in, billing-out, ...)       │
│ • Emit telemetry to Application Insights                       │
└────────────────────────┬────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────────┐
│ TELEMETRY LAYER (Azure Data Explorer)                          │
│ • Ingestion from App Insights, Service Bus, Kubernetes         │
│ • ETL pipelines aggregate to 5-minute bins                     │
│ • Gold Materialized Views:                                     │
│   - NodeTimeBin (service metrics)                              │
│   - QueueTimeBin (queue metrics, optional)                     │
│ • Retention: 90 days hot, 1 year cold                          │
└────────────────────────┬────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────────┐
│ FLOWTIME ECOSYSTEM                                              │
│                                                                 │
│ ┌──────────────┐   ┌──────────────┐   ┌──────────────────────┐│
│ │ FlowTime-Sim │   │ FlowTime     │   │ FlowTime UI          ││
│ │              │   │ Engine       │   │                      ││
│ │ Generates    │   │ Evaluates    │   │ Orchestrates         ││
│ │ PMF models   │   │ Deterministic│   │ Coordinates          ││
│ │              │   │              │   │ Visualizes           ││
│ └──────────────┘   └──────────────┘   └──────────────────────┘│
│        ↓                  ↓                      ↓              │
│   Templates         TelemetryLoader        User Workflows      │
│   Parameters        Model Evaluator        Time-Travel         │
│                     Artifacts Writer       Comparisons         │
│                                                                 │
│ ┌───────────────────────────────────────────────────────────┐  │
│ │ TEMPLATES REPOSITORY                                      │  │
│ │ • order-system.yaml (topology + expressions)             │  │
│ │ • billing-system.yaml                                     │  │
│ │ • Versioned in Git                                        │  │
│ └───────────────────────────────────────────────────────────┘  │
│                                                                 │
│ ┌───────────────────────────────────────────────────────────┐  │
│ │ ARTIFACT STORAGE (ADLS Gen2 / Blob)                      │  │
│ │ /runs/{runId}/                                            │  │
│ │   run.json, model.yaml, topology.json, series/*.csv      │  │
│ └───────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────────┐
│ CONSUMERS                                                       │
│ • Power BI (queries Gold directly for dashboards)              │
│ • Jupyter notebooks (loads artifacts for analysis)             │
│ • Alerts (monitors Gold for thresholds)                        │
└─────────────────────────────────────────────────────────────────┘
```

#### Component Responsibilities

##### Azure Data Explorer (Gold Layer)

**Owns:**
- Raw event ingestion (App Insights, Service Bus, Kubernetes)
- ETL pipeline execution (aggregation, normalization)
- Materialized Views (NodeTimeBin, QueueTimeBin)
- Index maintenance and query performance
- Data retention policies

**Provides:**
- KQL query interface for time-windowed data
- Pre-aggregated base observations (arrivals, served, errors)
- Optional queue depth from Service Bus metrics
- Provenance metadata (extraction timestamps)

**Does NOT:**
- Compute derived metrics (latency, utilization) in MVs
- Store capacity estimates or inferences
- Enforce FlowTime model semantics
- Execute DAG evaluations

##### TelemetryLoader

**Owns:**
- KQL query construction for requested time windows
- Bin alignment and zero-filling
- CSV/JSON file generation
- Data gap detection and warning generation

**Provides:**
- Extracted telemetry as flat files (CSV preferred)
- Manifest of extracted files and metadata
- Warning log for missing bins or data quality issues

**Does NOT:**
- Validate telemetry semantics (conservation, capacity)
- Generate topology or semantic mappings
- Transform data beyond simple aggregation
- Store state (stateless utility)

##### Model Templates

**Owns:**
- Topology definitions (nodes, edges, groupings, UI layout)
- Expression formulas (how to compute derived metrics)
- Parameter definitions (what can be customized)
- Documentation (what the system models, assumptions)

**Provides:**
- Template files (YAML format) in version control
- Instantiation logic (parameter substitution)
- Default values and validation rules

**Does NOT:**
- Query telemetry (references files, doesn't load them)
- Execute expressions (Engine's job)
- Enforce runtime behavior (declarative only)

##### FlowTime Engine

**Owns:**
- Model schema parsing and validation
- DAG construction and topological sorting
- Expression evaluation (const, expr, pmf nodes)
- Stateful operators (SHIFT with initial conditions)
- Artifact generation (run.json, series CSVs)
- Post-evaluation validation checks

**Provides:**
- REST API endpoints (/v1/runs, /v1/state, /v1/metrics)
- Immutable run artifacts in object storage
- Deterministic, reproducible evaluations

**Does NOT:**
- Query Gold MVs directly (uses TelemetryLoader)
- Apply business logic outside model expressions
- Infer topology or semantics
- Store production telemetry

##### FlowTime-Sim

**Owns:**
- Stochastic template definitions
- PMF sampling and distribution generation
- Model generation from templates with parameters
- Temporary model storage

**Provides:**
- API to generate models from templates
- Parameterized scenario exploration
- Integration with Engine for evaluation

**Does NOT:**
- Execute models (delegates to Engine)
- Store run artifacts (Engine's responsibility)
- Query telemetry (separate concern)

##### FlowTime UI

**Owns:**
- User authentication and authorization
- Workflow orchestration (Sim → Engine → UI)
- Graph visualization and layout
- Time-travel scrubber interactions
- Comparison view rendering

**Provides:**
- Web-based interface for all use cases
- Run management (create, list, share, delete)
- Metric dashboards and SLA tracking

**Does NOT:**
- Execute models or compute metrics
- Store artifacts (references Engine registry)
- Enforce data quality rules

---

### 1.5 Key Design Decisions

#### Decision 1: Capacity-Free Core

**Decision:** Engine does NOT require capacity as input. Capacity can be optionally inferred for visualization with clear labeling.

**Rationale:**
- Every capacity proxy (replicas, config, throughput ceiling) is flawed
- Observed `served` is ground truth for replay
- Inferred capacity useful for viz but not computation
- What-if scenarios specify capacity explicitly

**Implications:**
- Gold schema: capacity_proxy is optional
- Templates: capacity not required in semantics
- API: inferred capacity labeled with method and confidence
- Validation: capacity checks are warnings, not errors

**Tradeoff:** Lose pre-computed utilization, gain robustness.

#### Decision 2: Telemetry-as-Files

**Decision:** TelemetryLoader exports data to CSV/JSON files. Engine loads from files, not database.

**Rationale:**
- Decouples Engine from ADX (testable with fixtures)
- Immutable snapshots (files don't change)
- Simple interface (no connection strings, auth)
- Cacheable and portable

**Implications:**
- Temporary storage required (ephemeral disk or blob)
- File format must be documented and stable
- Cleanup policy needed for temp files

**Tradeoff:** Extra I/O step, but gains simplicity.

#### Decision 3: Templates Not Generated

**Decision:** Topology and expressions live in hand-authored templates, not generated from Catalog_Nodes.

**Rationale:**
- Topology is engineering artifact (belongs in git)
- Engineers need to review topology changes
- Templates enable parameterization and reuse
- No runtime dependency on metadata tables

**Implications:**
- Initial template authoring effort
- Templates must be kept in sync with production
- Template versioning and migration strategy needed

**Tradeoff:** Upfront work, but long-term maintainability.

#### Decision 4: Single Evaluation Mode

**Decision:** Engine has one evaluation mode. Data source (telemetry vs simulation) is transparent to Engine.

**Rationale:**
- Eliminates dual-mode complexity
- Testability improves (same code paths)
- Reduces surface area for bugs
- Simpler mental model

**Implications:**
- Models must be self-contained (all data in files or expressions)
- No special "Gold" code paths
- API responses don't distinguish source (provenance does)

**Tradeoff:** Less automatic behavior, more explicit configuration.

---

**End of Chapter 1**

Continue to Chapter 2 for Data Contracts (Gold Schema, Model Schema, API).
