# Gold-First Time-Travel Architecture

**Version:** 1.0  
**Date:** October 8, 2025  
**Status:** Architectural Design  
**Authors:** FlowTime Architecture Team

---

## Executive Summary

### The Core Insight

FlowTime's time-travel capability is built on a **dual-mode architecture** that separates concerns between "what actually happened" (Gold telemetry) and "what could happen" (simulation and counterfactuals). This architecture enables both **replay** (time-travel over historical data) and **exploration** (what-if scenarios) through a single, unified API and execution model.

**Key Principle:** Gold Materialized Views own descriptive facts; Engine owns stateful computations.

### Why Gold-First?

Traditional approaches couple telemetry ingestion with model evaluation, creating tight dependencies and complex logic within the engine. The Gold-First architecture achieves clean separation:

```
┌─────────────────────────────────────────────────────────────┐
│ GOLD LAYER (Materialized Views)                            │
│ • Pre-computes: arrivals, served, errors, capacity_proxy   │
│ • Aggregates: queue_depth, latency_p50/p95, utilization    │
│ • Stable, Non-parametric, Reproducible                     │
│ • Query latency: <100ms for 1000+ bins                     │
└─────────────────────────────────────────────────────────────┘
                            ↓
            ┌──────────────────────────────┐
            │ GoldToModel Adapter          │
            │ • Queries Gold MVs           │
            │ • Generates model.yaml       │
            │ • Creates `const *_gold`     │
            └──────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ ENGINE LAYER (Pure Evaluation)                             │
│ • Evaluates: const | expr | pmf nodes only                 │
│ • Computes: stateful dynamics (SHIFT, queues)              │
│ • Derives: counterfactuals, overlays, scenarios            │
│ • No telemetry logic, No DB queries, No side effects       │
└─────────────────────────────────────────────────────────────┘
                            ↓
            ┌──────────────────────────────┐
            │ API LAYER (Unified)          │
            │ • /v1/runs (both modes)      │
            │ • /v1/state (time-travel)    │
            │ • /v1/metrics (aggregates)   │
            └──────────────────────────────┘
```

### Benefits

| Aspect | Gold-First Approach | Traditional Approach |
|--------|-------------------|---------------------|
| **Engine Complexity** | Pure spreadsheet evaluation | Telemetry ingest + evaluation |
| **Reproducibility** | Snapshot runs freeze Gold state | Moving target (late arrivals) |
| **Performance** | Pre-computed MVs (fast) | Query raw events (slow) |
| **Testing** | Deterministic, no external deps | Requires telemetry infrastructure |
| **Counterfactuals** | Overlay expressions on Gold | Requires full re-simulation |
| **Dual Purpose** | Replay + Simulation unified | Separate code paths |

### What This Document Covers

1. **Architecture Principles** - Design philosophy and constraints
2. **System Boundaries** - What each component owns
3. **Data Contracts** - Gold tables, Model schema, API contracts
4. **Dual-Mode Architecture** - Gold snapshot vs Model-only modes
5. **Component Design** - Adapter, Engine extensions, API changes
6. **Implementation Roadmap** - 5 milestones from foundation to production
7. **Decision Log** - Key tradeoffs and rationales
8. **Migration Path** - From current state to gold-first

**Audience:**
- **Junior Engineers:** Clear milestones with TDD guidance, acceptance criteria, and code patterns
- **Senior Engineers:** Component boundaries, contracts, and integration patterns
- **Architects:** Design rationale, tradeoffs, and system evolution strategy

---

## Table of Contents

1. [Architecture Principles](#architecture-principles)
2. [System Context and Boundaries](#system-context-and-boundaries)
3. [Data Contracts](#data-contracts)
4. [Dual-Mode Architecture](#dual-mode-architecture)
5. [Component Design](#component-design)
6. [Implementation Roadmap](#implementation-roadmap)
7. [Decision Log](#decision-log)
8. [Migration Path](#migration-path)
9. [Examples and Patterns](#examples-and-patterns)
10. [Appendices](#appendices)

---

## 1. Architecture Principles

### 1.1 Design Philosophy

#### P1: Engine Purity (No Telemetry Logic)

**Principle:** The Engine evaluates DAGs of `const|expr|pmf` nodes. It does NOT query databases, parse telemetry formats, or apply business logic for metric derivation.

**Rationale:**
- Keeps Engine testable without external dependencies
- Makes evaluation deterministic and reproducible
- Simplifies engine evolution (add operators, not integrations)
- Enables multiple adapters (Gold, Synthetic, CSV imports) without engine changes

**Example:**
```yaml
# ✅ Engine receives this (pure data)
nodes:
  - id: orders_arrivals_gold
    kind: const
    values: [120, 135, 140, ...]  # From Gold MV
  
  - id: orders_served
    kind: expr
    expr: "MIN(orders_arrivals_gold, capacity)"

# ❌ Engine does NOT do this
nodes:
  - id: orders_arrivals
    kind: kusto_query  # NO - adapter responsibility
    query: "NodeTimeBin | where node == 'orders'"
```

#### P2: Gold as Source of Truth for "What-Was"

**Principle:** Materialized Views in the Gold layer are the authoritative source for observed telemetry (arrivals, served, errors, capacity proxies, queue depth, latency percentiles).

**Rationale:**
- Pre-computation is cheap (ETL once, query many times)
- Aggregations are stable (no late-arrival drift)
- Multiple consumers benefit (Engine, BI, alerts)
- Provenance is explicit (extraction timestamp, source windows)

**Example:**
```sql
-- Gold MV (pre-computed, indexed)
NodeTimeBin
| where node == "OrderService" and ts >= start and ts < end
| project ts, arrivals, served, errors, capacity_proxy, queue_depth, latency_p50_ms
```

#### P3: Dual-Mode Support (Unified API)

**Principle:** The same API and model schema serve both Gold snapshot mode (replay) and Model-only mode (simulation).

**Rationale:**
- UI can switch modes without code changes
- Developers learn one API surface
- Test fixtures work for both modes
- Overlay scenarios (Gold baseline + modeled changes) are natural

**Contract:**
```yaml
# Gold Mode: semantics → *_gold const series
topology:
  nodes:
    - id: "OrderService"
      semantics:
        arrivals: "orders_arrivals_gold"  # const from Gold
        served: "orders_served_gold"      # const from Gold

# Model Mode: semantics → modeled series
topology:
  nodes:
    - id: "OrderService"
      semantics:
        arrivals: "orders_arrivals"       # expr or pmf
        served: "orders_served"           # expr
```

#### P4: Stateful Computation in Engine Only

**Principle:** Gold provides snapshots (bin-level observations). Engine computes stateful dynamics (queues with SHIFT, feedback loops, time-lagged effects).

**Rationale:**
- Gold MVs are stateless aggregates (SUM, AVG, P95 per bin)
- Stateful logic (Q[t] = Q[t-1] + arrivals - served) requires causal ordering
- Engine's DAG evaluator handles SHIFT and temporal dependencies correctly
- Separates "facts" (Gold) from "dynamics" (Engine)

**Example:**
```yaml
# Gold provides: inflow and capacity (facts)
- id: queue_inflow_gold
  kind: const
  values: [10, 20, 15, ...]

# Engine computes: queue depth (stateful)
- id: queue_depth
  kind: expr
  expr: "MAX(0, SHIFT(queue_depth, 1) + queue_inflow_gold - capacity)"
```

#### P5: Snapshot Runs for Reproducibility

**Principle:** When creating a run from Gold, the adapter queries a specific time window and freezes it as a model artifact. Subsequent queries against this run return the same data (immutable snapshot).

**Rationale:**
- Gold MVs are continuously updated (late arrivals, reprocessing)
- Investigations require stable data (compare yesterday vs today)
- Audit trails need frozen provenance (what data was used)
- Caching and access control work on immutable artifacts

**Flow:**
```
Request: Gold snapshot for [2025-10-07 00:00, 2025-10-08 00:00)
         ↓
Adapter: Query Gold MVs at extraction_ts = 2025-10-08 10:30:00Z
         ↓
Model:   Generated with provenance.source_window_start/end
         ↓
Run:     Artifacts frozen in /data/run_20251008T103000Z_abc123/
         ↓
Future:  GET /v1/state?ts=... returns same data (snapshot)
```

#### P6: UTC and End-Exclusive Windows

**Principle:** All timestamps are UTC. Windows are `[start, end)` (start inclusive, end exclusive).

**Rationale:**
- Matches Kusto `bin()` semantics
- Prevents off-by-one errors in time ranges
- Aligns with ISO-8601 interval notation
- Simplifies bin arithmetic (no timezone conversions)

**Example:**
```yaml
window:
  start: "2025-10-07T00:00:00Z"  # Bin 0 starts here
  end: "2025-10-08T00:00:00Z"    # Bin 287 ends here (288 bins total)
  timezone: "UTC"

# Bin 0: [2025-10-07T00:00:00Z, 2025-10-07T00:05:00Z)
# Bin 1: [2025-10-07T00:05:00Z, 2025-10-07T00:10:00Z)
# Bin 287: [2025-10-07T23:55:00Z, 2025-10-08T00:00:00Z)
```

#### P7: `binMinutes` Everywhere

**Principle:** Engine derives `binMinutes = binSize × toMinutes(binUnit)` and includes it in all API responses alongside grid.

**Rationale:**
- Single scalar for timestamp arithmetic: `ts = window.start + (idx × binMinutes × 60_000)`
- Little's Law latency: `latency_min = queue / max(ε, served) × binMinutes`
- Rate normalization: `rate_per_min = count / binMinutes`
- Prevents unit bugs across 3+ client implementations (UI, CLI, notebooks)

**Conversion Table:**
```yaml
binUnit: "minutes" → 1
binUnit: "hours"   → 60
binUnit: "days"    → 1440
binUnit: "weeks"   → 10080
```

### 1.2 Non-Goals

What this architecture explicitly does NOT do:

- ❌ **Real-time telemetry ingest** - Gold MVs are batch-updated (typical lag: 5-15 minutes)
- ❌ **Interactive query rewrites** - Engine evaluates pre-defined models, not ad-hoc KQL
- ❌ **Schema evolution of Gold tables** - Gold schema is external (owned by telemetry team)
- ❌ **Multi-tenant isolation** - Engine runs are single-tenant artifacts (access control at UI layer)
- ❌ **Distributed execution** - Engine is single-node, in-process (scales via multiple runs, not sharding)

---

## 2. System Context and Boundaries

### 2.1 Ecosystem Overview

FlowTime operates within a larger telemetry and modeling ecosystem:

```
┌─────────────────────────────────────────────────────────────────┐
│ PRODUCTION SYSTEMS                                              │
│ • Services emit telemetry (App Insights, custom events)        │
│ • Queues report depth, age, throughput                         │
│ • Infrastructure exposes capacity metrics                       │
└────────────────────────┬────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────────┐
│ TELEMETRY LAYER (Azure Data Explorer)                          │
│ • Raw events ingested continuously                             │
│ • ETL pipelines normalize and validate                         │
│ • Gold Materialized Views (NodeTimeBin, EdgeTimeBin)          │
│   - Pre-aggregated per 5-minute bins                           │
│   - Indexed on (flow, node, ts)                                │
│   - Columns: arrivals, served, errors, queue_depth, etc.       │
└────────────────────────┬────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────────┐
│ FLOWTIME ECOSYSTEM                                              │
│                                                                 │
│ ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐ │
│ │ FlowTime-Sim    │  │ FlowTime Engine │  │ FlowTime UI     │ │
│ │ (Generator)     │  │ (Evaluator)     │  │ (Orchestrator)  │ │
│ └─────────────────┘  └─────────────────┘  └─────────────────┘ │
│         ↓                     ↓                     ↓          │
│   Templates          GoldToModel Adapter      Coordinates      │
│   Parameters         Model Parser             Sim ↔ Engine     │
│   Provenance         DAG Evaluator            Visualizes       │
│                      Artifacts Writer                           │
│                                                                 │
│ ┌─────────────────────────────────────────────────────────────┐ │
│ │ ADAPTERS LAYER                                              │ │
│ │ • FlowTime.Adapters.Gold (ADX queries → model.yaml)        │ │
│ │ • FlowTime.Adapters.Synthetic (fixtures for testing)       │ │
│ │ • FlowTime.Adapters.CSV (import external data)             │ │
│ └─────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
```

### 2.2 Component Responsibilities

#### Gold Layer (Azure Data Explorer)

**Owns:**
- Raw event ingestion from production systems
- ETL pipelines for normalization and validation
- Materialized Views (NodeTimeBin, EdgeTimeBin, Catalog_Nodes)
- Index maintenance and query optimization
- Schema versioning and backward compatibility

**Provides:**
- KQL query interface for time-windowed slices
- Pre-computed aggregates (arrivals, served, errors, capacity, queue_depth, latency percentiles)
- Provenance metadata (extraction_ts, source_window, known_gaps)

**Does NOT:**
- Stateful computations (queue dynamics, feedback loops)
- Counterfactual analysis (what-if scenarios)
- Model execution or DAG evaluation

#### GoldToModel Adapter

**Owns:**
- KQL query construction for requested time windows
- Validation of Gold schema compatibility
- Transformation of Gold columns to `const` series
- Topology mapping (from Catalog_Nodes to model topology)
- Provenance enrichment (adapter_version, extraction_ts)

**Provides:**
- `model.yaml` generation from Gold slices
- Dense bin filling (zero-fill for missing bins)
- Schema translation (Gold columns → series IDs)

**Does NOT:**
- Store state (stateless transformer)
- Execute models (delegates to Engine)
- Expose KQL interface to end users

#### FlowTime Engine

**Owns:**
- Model schema validation and parsing
- DAG construction and topological sorting
- Node evaluation (const, expr, pmf)
- Stateful operators (SHIFT, CONV, queue dynamics)
- Artifact generation (run.json, series CSVs, manifest)
- Registry management (run indexing, provenance storage)

**Provides:**
- `POST /v1/runs` (create run from model or Gold request)
- `GET /v1/graph` (topology + grid + series DAG)
- `GET /v1/state` (single-bin snapshot with derived metrics)
- `GET /v1/state_window` (dense time-series slice)
- `GET /v1/metrics` (SLA aggregates per flow)

**Does NOT:**
- Query Gold MVs directly (uses adapter)
- Apply business logic for metric definitions (uses model expressions)
- Store telemetry data (consumes Gold snapshots)

#### FlowTime-Sim

**Owns:**
- Template definitions (parameters, distributions)
- Stochastic sampling (PMF, Poisson, retry kernels)
- Model generation from templates
- Provenance tracking (template_id, model_id, parameters)

**Provides:**
- `POST /api/v1/templates/{id}/generate` (create model from template)
- `GET /api/v1/models/{id}` (retrieve generated model)
- Temporary model storage (in-memory or short-lived filesystem)

**Does NOT:**
- Execute models (delegates to Engine via UI)
- Store run artifacts (Engine's responsibility)
- Query Gold telemetry (separate concern)

#### FlowTime UI

**Owns:**
- User workflows (explore, compare, investigate)
- Orchestration (Sim → Engine coordination)
- Visualization (graph rendering, sparklines, scrubbers)
- Access control (user authentication, run permissions)

**Provides:**
- Graph view with topology layout
- Time-travel scrubber (bin-by-bin navigation)
- SLA dashboards and metric aggregates
- Comparison views (baseline vs scenario)

**Does NOT:**
- Execute models (calls Engine API)
- Store artifacts (references Engine registry)
- Compute metrics (consumes Engine-derived values)

### 2.3 Data Flow Examples

#### Flow 1: Gold Snapshot Run

```
1. User (via UI): "Show me OrderService on Oct 7, 2025 (hourly bins)"
2. UI → Engine: POST /v1/runs
   {
     "runKind": "gold-snapshot",
     "window": { "start": "2025-10-07T00:00:00Z", "bins": 24, "binSize": 1, "binUnit": "hours" },
     "selection": { "flows": ["*"], "nodes": ["OrderService", "OrderQueue"] }
   }
3. Engine → GoldToModel Adapter: query_gold_slice(window, selection)
4. Adapter → ADX: KQL query against NodeTimeBin
   NodeTimeBin
   | where node in ("OrderService", "OrderQueue")
   | where ts >= datetime(2025-10-07T00:00:00Z) and ts < datetime(2025-10-08T00:00:00Z)
   | summarize arrivals=sum(arrivals), served=sum(served) by bin(ts, 1h), node
5. Adapter: Transform to model.yaml with const *_gold series
6. Engine: Parse model → Evaluate DAG → Write artifacts
7. Engine → UI: { "runId": "run_...", "mode": "gold", ... }
8. User: Navigate to /v1/state?ts=2025-10-07T14:00:00Z
9. Engine: Read artifacts → Compute derived metrics → Return snapshot
```

#### Flow 2: Model-Only Run (Sim)

```
1. User (via UI): "Generate model from 'microservices-template' with arrivalRate=150"
2. UI → Sim: POST /api/v1/templates/microservices-template/generate
   { "parameters": { "arrivalRate": 150, "numServices": 5 } }
3. Sim: Sample PMFs → Build model.yaml → Store temporarily
4. Sim → UI: { "modelId": "model_...", "provenance": { "template_id": "microservices-template" } }
5. UI → Sim: GET /api/v1/models/model_...
6. Sim → UI: model.yaml content
7. UI → Engine: POST /v1/runs (body: model.yaml)
8. Engine: Parse → Evaluate → Write artifacts
9. Engine → UI: { "runId": "run_...", "mode": "model", ... }
```

#### Flow 3: Overlay Scenario (Gold + Modeled Changes)

```
1. Start with Gold baseline (Flow 1)
2. User: "What if capacity doubled?"
3. UI → Engine: POST /v1/runs
   {
     "runKind": "gold-snapshot",
     "window": { ... },
     "selection": { ... },
     "overlays": [
       { "nodeId": "capacity_modeled", "kind": "expr", "expr": "capacity_gold * 2" }
     ],
     "topologyOverride": {
       "nodes": [{ "id": "OrderService", "semantics": { "capacity": "capacity_modeled" } }]
     }
   }
4. Adapter: Generate model with capacity_gold + capacity_modeled
5. Engine: Evaluate with modeled capacity
6. API responses include both observed (Gold) and modeled fields
```

---

## 3. Data Contracts

### 3.1 Gold Table Schemas

#### 3.1.1 NodeTimeBin (Primary Gold MV)

**Purpose:** Pre-aggregated telemetry for logical nodes (services, queues) at fixed time bins.

**Grain:** `(flow, node, ts)` where `ts` is bin start (UTC, end-exclusive)

**Required Columns:**

| Column | Type | Description |
|--------|------|-------------|
| `ts` | datetime | Bin start timestamp (UTC) |
| `node` | string | Logical node identifier (e.g., "OrderService") |
| `flow` | string | Flow class (e.g., "Order", "*" for single-class) |
| `arrivals` | long | Count of entities arriving in this bin |
| `served` | long | Count of entities successfully processed |
| `errors` | long | Count of failed attempts |

**Recommended Columns:**

| Column | Type | Description |
|--------|------|-------------|
| `queue_depth` | real | Average queue depth in bin (or end-of-bin snapshot) |
| `oldest_age_s` | real | Maximum age of queued entity (seconds) |
| `capacity_proxy` | real | Observed capacity indicator (e.g., active workers, throughput limit) |
| `replicas` | real | Average replica count (for autoscaling context) |
| `latency_p50_ms` | real | 50th percentile latency from entry to exit |
| `latency_p95_ms` | real | 95th percentile latency |
| `latency_p99_ms` | real | 99th percentile latency |
| `dlq_count` | long | Dead-letter queue entries |
| `stuck_flag` | bool | Alert flag for stuck entities |

**Provenance Columns (MUST include):**

| Column | Type | Description |
|--------|------|-------------|
| `schema_version` | string | Gold schema version (e.g., "1.0") |
| `adapter_version` | string | ETL pipeline version |
| `source_window_start` | datetime | Telemetry coverage start |
| `source_window_end` | datetime | Telemetry coverage end |
| `extraction_ts` | datetime | When this MV was computed |
| `known_data_gaps` | string | JSON array of gap intervals (if any) |

**Indexing:**
```sql
-- Primary index for time-range queries
INDEX idx_node_time ON NodeTimeBin (node, ts)

-- Secondary index for flow-based filtering
INDEX idx_flow_node_time ON NodeTimeBin (flow, node, ts)
```

**Example Row:**
```json
{
  "ts": "2025-10-07T14:00:00Z",
  "node": "OrderService",
  "flow": "*",
  "arrivals": 150,
  "served": 145,
  "errors": 3,
  "queue_depth": 8.5,
  "capacity_proxy": 200.0,
  "latency_p50_ms": 125.0,
  "latency_p95_ms": 480.0,
  "schema_version": "1.0",
  "extraction_ts": "2025-10-07T14:05:00Z"
}
```

#### 3.1.2 EdgeTimeBin (Optional, for routing analysis)

**Purpose:** Flow between nodes (for multi-hop topologies)

**Grain:** `(flow, from_node, to_node, ts)`

**Columns:**

| Column | Type | Description |
|--------|------|-------------|
| `ts` | datetime | Bin start timestamp (UTC) |
| `from_node` | string | Source node |
| `to_node` | string | Destination node |
| `flow` | string | Flow class |
| `routed_count` | long | Entities routed in this bin |
| `hop_latency_p95_ms` | real | Latency for this hop |
| `errors` | long | Routing failures |

**Use Cases:**
- Validate routing policies (does split ratio match expectation?)
- Identify bottleneck hops (high latency edges)
- Trend analysis (is routing changing over time?)

#### 3.1.3 Catalog_Nodes (Topology Metadata)

**Purpose:** Logical topology definition (node kinds, groups, UI layout)

**Grain:** `node_id` (unique per deployment)

**Columns:**

| Column | Type | Description |
|--------|------|-------------|
| `node_id` | string | Unique node identifier |
| `flow` | string | Associated flow class ("*" for shared) |
| `kind` | string | Node type: `service`, `queue`, `router`, `external` |
| `group` | string | Logical grouping (e.g., "Orders", "Billing") |
| `ui_x` | int | UI layout X coordinate |
| `ui_y` | int | UI layout Y coordinate |
| `sla_min` | real | SLA target in minutes (optional) |
| `description` | string | Human-readable description |

**Example:**
```json
{
  "node_id": "OrderService",
  "flow": "*",
  "kind": "service",
  "group": "Orders",
  "ui_x": 120,
  "ui_y": 260,
  "sla_min": 5.0,
  "description": "Order processing service"
}
```

### 3.2 Engine Model Schema

The Engine model schema is the **unified format** for both Gold-derived and Sim-generated models. See [`docs/schemas/model.schema.md`](../schemas/model.schema.md) for complete specification.

**Key Sections:**

#### 3.2.1 Top Level

```yaml
schemaVersion: 1              # Schema version for evolution
modelFormat: "1.1"            # Model format version (optional, default "1.0")

window:                       # Absolute time anchoring (REQUIRED for time-travel)
  start: "2025-10-07T00:00:00Z"  # ISO-8601 UTC (bin 0 timestamp)
  timezone: "UTC"             # MUST be "UTC"

grid:                         # Discrete time grid
  bins: 288                   # Number of time periods
  binSize: 5                  # Duration magnitude
  binUnit: "minutes"          # Time unit

classes: ["*"]                # Flow classes (single-class for M3.0)

topology:                     # Logical topology (nodes + edges + semantics)
  nodes: [ ... ]
  edges: [ ... ]

nodes:                        # Series definitions (const|expr|pmf)
  - id: "..."
    kind: "const"
    values: [ ... ]

outputs:                      # Series to include in artifacts
  - series: "..."
    as: "..."

provenance:                   # Metadata about model origin
  generatedAt: "..."
  generator: "flowtime-sim" | "gold-adapter"
  version: "..."
```

#### 3.2.2 Topology Section (M3.0 Extension)

```yaml
topology:
  nodes:
    - id: "OrderService"
      kind: "service"         # service|queue|router|external
      group: "Orders"         # UI grouping
      ports: ["in", "out"]    # UI hint
      ui: { x: 120, y: 260 }  # Coordinates
      semantics:              # Semantic mapping to series IDs
        arrivals: "orders_arrivals_gold"   # → const series
        served: "orders_served_gold"       # → const series
        capacity: "orders_capacity_gold"   # → const series
        errors: "orders_errors_gold"       # → const series (optional)
        queue: null                        # null for service, REQUIRED for queue
        latency_min: null                  # Derived by engine if absent
        sla_min: 5.0                       # Constant SLA target
        replicas: "orders_replicas_gold"   # → const series (optional)
        oldest_age_s: null                 # NOT IMPLEMENTED in M3.0
    
    - id: "OrderQueue"
      kind: "queue"
      group: "Orders"
      ui: { x: 340, y: 260 }
      semantics:
        arrivals: "queue_inflow_gold"
        served: "queue_outflow_gold"
        queue: "queue_depth_gold"    # REQUIRED for queue kind
        q0: 0                        # Initial queue state (optional)
  
  edges:
    - id: "e1"
      from: "OrderService:out"
      to: "OrderQueue:in"
```

**Validation Rules:**
- `topology.nodes[kind=service]` MUST have: `arrivals`, `capacity`, `served`
- `topology.nodes[kind=queue]` MUST have: `arrivals`, `served`, `queue`
- `topology.nodes[*].semantics.*` MUST reference existing `nodes[*].id`
- `topology.edges[*].from/to` MUST reference existing topology nodes
- No cycles in edges (DAG validation)

#### 3.2.3 Series Nodes

**Gold Mode Pattern:**
```yaml
nodes:
  # Const series from Gold columns
  - id: orders_arrivals_gold
    kind: const
    values: [120, 135, 140, ...]  # From Gold.arrivals
  
  - id: orders_capacity_gold
    kind: const
    values: [200, 200, 200, ...]  # From Gold.capacity_proxy
  
  # Optional: Derived expressions
  - id: utilization
    kind: expr
    expr: "orders_served_gold / MAX(1, orders_capacity_gold)"
```

**Model Mode Pattern:**
```yaml
nodes:
  # Stochastic arrivals
  - id: orders_arrivals
    kind: pmf
    pmf:
      values: [100, 150, 200]
      probabilities: [0.2, 0.6, 0.2]
  
  # Fixed capacity
  - id: orders_capacity
    kind: const
    values: [200, 200, ...]
  
  # Derived throughput
  - id: orders_served
    kind: expr
    expr: "MIN(orders_arrivals, orders_capacity)"
```

### 3.3 API Contracts

#### 3.3.1 POST /v1/runs (Create Run)

**Gold Snapshot Mode:**
```json
{
  "runKind": "gold-snapshot",
  "window": {
    "start": "2025-10-07T00:00:00Z",
    "bins": 288,
    "binSize": 5,
    "binUnit": "minutes"
  },
  "selection": {
    "flows": ["*"],
    "nodes": ["OrderService", "OrderQueue"]
  },
  "topology": {
    // Optional override (default: from Catalog_Nodes)
  },
  "provenance": {
    "request_id": "uuid",
    "purpose": "investigation|demo|report"
  }
}
```

**Model-Only Mode:**
```yaml
# Body is model.yaml (as YAML or JSON)
schemaVersion: 1
grid: { ... }
nodes: [ ... ]
outputs: [ ... ]
```

**Response (both modes):**
```json
{
  "runId": "run_20251007T143000Z_abc123",
  "status": "completed",
  "mode": "gold" | "model",
  "grid": {
    "bins": 288,
    "binSize": 5,
    "binUnit": "minutes",
    "binMinutes": 5
  },
  "window": {
    "start": "2025-10-07T00:00:00Z",
    "end": "2025-10-08T00:00:00Z",
    "timezone": "UTC"
  },
  "topology": {
    "nodes": [ ... ],
    "edges": [ ... ]
  },
  "provenance": {
    "source_window_start": "2025-10-07T00:00:00Z",
    "source_window_end": "2025-10-08T00:00:00Z",
    "extraction_ts": "2025-10-07T14:30:00Z",
    "adapter_version": "1.0.0",
    "generator": "gold-adapter"
  }
}
```

#### 3.3.2 GET /v1/state?ts={timestamp}

**Purpose:** Single-bin snapshot with derived metrics and coloring hints

**Request:**
```
GET /v1/state?ts=2025-10-07T14:00:00Z&runId=run_abc123
```

**Response:**
```json
{
  "runId": "run_abc123",
  "mode": "gold",
  "grid": { "binMinutes": 5, ... },
  "window": { "start": "...", "end": "...", "timezone": "UTC" },
  "bin": {
    "index": 168,
    "startUtc": "2025-10-07T14:00:00Z",
    "endUtc": "2025-10-07T14:05:00Z"
  },
  "nodes": {
    "OrderService": {
      "kind": "service",
      "observed": {
        "arrivals": 150,
        "served": 145,
        "errors": 3,
        "capacity_proxy": 200
      },
      "modeled": null,  // Only present in overlay scenarios
      "utilization": 0.725,
      "color": "yellow"
    },
    "OrderQueue": {
      "kind": "queue",
      "observed": {
        "arrivals": 145,
        "served": 140,
        "queue": 8
      },
      "latency_min": 0.057,  // Derived: (8 / 140) * 5 = 0.286 min
      "color": "green"
    }
  }
}
```

**Coloring Rules:**
- **Services (utilization-based):**
  - Green: `< 0.7`
  - Yellow: `0.7 - 0.9`
  - Red: `>= 0.9`
- **Queues (SLA-based):**
  - Green: `latency_min <= sla_min`
  - Yellow: `latency_min <= 1.5 × sla_min`
  - Red: `latency_min > 1.5 × sla_min`

#### 3.3.3 GET /v1/state_window?start={ts}&end={ts}

**Purpose:** Dense time-series slice for sparklines and trend analysis

**Request:**
```
GET /v1/state_window?start=2025-10-07T00:00:00Z&end=2025-10-07T12:00:00Z&runId=run_abc123
```

**Response:**
```json
{
  "runId": "run_abc123",
  "mode": "gold",
  "grid": { "binMinutes": 5 },
  "window": { "start": "...", "end": "..." },
  "slice": {
    "start": "2025-10-07T00:00:00Z",
    "end": "2025-10-07T12:00:00Z",
    "bins": 144
  },
  "timestamps": [
    "2025-10-07T00:00:00Z",
    "2025-10-07T00:05:00Z",
    ...
  ],
  "nodes": {
    "OrderService": {
      "kind": "service",
      "series": {
        "arrivals": [120, 125, 130, ...],  // 144 values
        "served": [118, 124, 128, ...],
        "utilization": [0.59, 0.62, 0.64, ...]
      }
    },
    "OrderQueue": {
      "kind": "queue",
      "series": {
        "queue": [5, 6, 8, ...],
        "latency_min": [0.042, 0.048, 0.063, ...]
      }
    }
  }
}
```

#### 3.3.4 GET /v1/metrics?start={ts}&end={ts}

**Purpose:** Aggregated SLA metrics per flow and per node

**Request:**
```
GET /v1/metrics?start=2025-10-07T00:00:00Z&end=2025-10-08T00:00:00Z&runId=run_abc123
```

**Response:**
```json
{
  "runId": "run_abc123",
  "mode": "gold",
  "window": { "start": "...", "end": "..." },
  "flows": {
    "Orders": {
      "bins_total": 288,
      "bins_meeting_sla": 275,
      "sla_pct": 95.5,
      "worst_latency_min": 6.2,
      "avg_latency_min": 1.4,
      "total_errors": 42,
      "total_served": 41500
    }
  },
  "nodes": {
    "OrderQueue": {
      "bins_total": 288,
      "bins_meeting_sla": 275,
      "sla_pct": 95.5,
      "worst_latency_min": 6.2,
      "avg_latency_min": 1.4
    }
  }
}
```

---


## 4. Dual-Mode Architecture

### 4.1 Mode Comparison

| Aspect | Gold Snapshot Mode | Model-Only Mode |
|--------|-------------------|-----------------|
| **Data Source** | ADX Gold Materialized Views | Templates (Sim) or hand-authored |
| **Ingress** | POST /v1/runs with runKind gold-snapshot | POST /v1/runs with model.yaml body |
| **Series Types** | const *_gold from Gold plus optional expr | const expr pmf authored |
| **Use Cases** | Replay investigation baseline | Exploration what-if capacity planning |

### 4.2 Gold Snapshot Mode

Gold MVs provide pre-computed facts (arrivals, served, capacity_proxy). Adapter transforms these into const series.

### 4.3 Overlay Scenarios

Hybrid mode: Gold baseline plus modeled changes for what-if analysis.

---

## 5. Component Design

### 5.1 GoldToModel Adapter

**Location:** src/FlowTime.Adapters.Gold/ (new project)

**Key Responsibilities:**
- Query ADX NodeTimeBin for requested windows
- Zero-fill missing bins
- Transform Gold columns to const series
- Build topology from Catalog_Nodes
- Generate model.yaml with provenance

### 5.2 Engine Extensions

**TimeGrid:** Add StartTimeUtc and GetBinStartUtc() helper

**Topology Models:** TopologyDefinition, TopologyNode, SemanticMapping, NodeKind

**ModelParser:** Add ParseWindow() and ParseTopology()

### 5.3 API Layer

**POST /v1/runs:** Detect Gold vs Model mode, delegate to adapter or direct parse

**GET /v1/state:** Return single-bin snapshot with derived metrics

**GET /v1/state_window:** Return dense time-series arrays

**GET /v1/metrics:** Return aggregated SLA metrics

---

## 6. Implementation Roadmap

### Milestones

- **M3.0 (5 days):** Foundation - Window and Topology schema
- **M3.1 (3 days):** Time-Travel APIs - state and state_window
- **M3.2 (3 days):** Gold Adapter - KQL queries and transformation
- **M3.3 (3 days):** SLA and Metrics - Derived metrics and aggregation
- **M3.4 (2 days):** Overlay Scenarios - Hybrid mode

**Total: ~16 days for full gold-first capability**

---


## 7. Decision Log

### 7.1 Why Gold-First vs Engine-First?

**Decision:** Position Gold MVs as source of truth for observed telemetry rather than having Engine query raw events.

**Rationale:**
- **Performance:** MVs pre-compute aggregates once; engine queries them many times
- **Reproducibility:** Snapshots freeze MV state; raw events have late arrivals
- **Simplicity:** Engine stays pure (evaluates DAG); no telemetry logic
- **Reusability:** Multiple consumers benefit (Engine, BI, alerts)

**Tradeoff:** Requires ETL pipeline investment, but pays off at scale

### 7.2 Why Dual-Mode vs Separate Systems?

**Decision:** Support both Gold snapshot and Model-only through same API/schema.

**Rationale:**
- **User Experience:** Same UI for replay and exploration
- **Code Reuse:** Single engine implementation
- **Overlay Scenarios:** Natural to combine Gold baseline with modeled changes
- **Learning Curve:** Developers learn one API surface

**Tradeoff:** More complex request routing, but manageable with clear mode detection

### 7.3 Why Snapshot Runs vs Live Queries?

**Decision:** Freeze Gold slice as immutable run artifact rather than querying Gold on every /state request.

**Rationale:**
- **Consistency:** Investigations see stable data across sessions
- **Performance:** Read from local artifacts vs ADX queries
- **Access Control:** Runs can be shared with specific permissions
- **Audit Trail:** Provenance captures exact Gold state used

**Tradeoff:** Storage cost for artifacts, but necessary for reproducibility

### 7.4 Why binMinutes Everywhere?

**Decision:** Engine derives and exposes binMinutes in all API responses alongside grid.

**Rationale:**
- **Client Simplicity:** Single scalar for timestamp math
- **Little Law:** Latency requires binMinutes for unit conversion
- **Rate Normalization:** Convert counts to per-minute rates
- **Bug Prevention:** Avoid unit conversion errors across multiple clients

**Tradeoff:** Slightly redundant (computable from binSize/binUnit), but ergonomic

### 7.5 Why UTC Only?

**Decision:** Require UTC timestamps, reject timezone-aware or local times.

**Rationale:**
- **Simplicity:** No timezone conversion logic in Engine
- **Kusto Alignment:** ADX bin() uses UTC
- **Global Teams:** Unambiguous for distributed teams
- **Storage Efficiency:** No timezone metadata in artifacts

**Tradeoff:** UI must handle timezone display, but keeps Engine simple

### 7.6 Why Topology as Metadata vs Execution?

**Decision:** Topology provides semantic labels for series; Engine evaluates series DAG (not topology graph).

**Rationale:**
- **Engine Purity:** DAG evaluation is well-understood, debuggable
- **Flexibility:** Same topology can map to different series
- **Evolution:** Add topology features without changing Engine
- **Testing:** Series evaluation testable without topology

**Tradeoff:** Topology and series must stay synchronized, requires validation

### 7.7 Why Observed vs Modeled Fields?

**Decision:** API responses separate observed (from Gold) and modeled (from expressions) rather than merging.

**Rationale:**
- **Clarity:** User knows what is fact vs hypothesis
- **Comparison:** Side-by-side enables visual comparison
- **Overlay Scenarios:** Natural to show baseline vs scenario
- **Provenance:** Clear attribution to source

**Tradeoff:** Slightly larger payloads, but worth the clarity

---

## 8. Migration Path

### 8.1 Current State (M2.10)

- Engine evaluates models (const, expr, pmf)
- No absolute time anchoring (bins are relative)
- No topology/semantics (series only)
- No Gold integration

### 8.2 Phase 1: M3.0 Foundation

**Changes:**
- Add window and topology to model schema
- Extend TimeGrid with StartTimeUtc
- Update API responses to include window and topology
- Backward compatible (legacy models work)

**Impact:**
- Sim must generate models with window/topology
- UI can display absolute time
- No breaking changes for existing models

### 8.3 Phase 2: M3.1 Time-Travel APIs

**Changes:**
- Add GET /v1/state endpoint
- Add GET /v1/state_window endpoint
- Implement derived metrics (utilization, latency)

**Impact:**
- UI can implement time-travel scrubber
- Requires run artifacts with window
- Legacy runs without window return 400

### 8.4 Phase 3: M3.2 Gold Adapter

**Changes:**
- Create FlowTime.Adapters.Gold project
- Implement KQL query and transformation logic
- Integrate with POST /v1/runs

**Impact:**
- Requires ADX connection configuration
- Requires Catalog_Nodes populated
- Optional feature (can deploy without Gold)

### 8.5 Phase 4: M3.3-M3.4 Complete

**Changes:**
- Add /metrics endpoint
- Add overlay scenario support

**Impact:**
- Full gold-first capability
- UI can show SLA dashboards
- Users can create what-if scenarios

---

## 9. Examples and Patterns

### 9.1 Example: Simple Gold Snapshot

**Request:**
```json
{
  "runKind": "gold-snapshot",
  "window": {
    "start": "2025-10-07T00:00:00Z",
    "bins": 24,
    "binSize": 1,
    "binUnit": "hours"
  },
  "selection": {
    "nodes": ["OrderService"]
  }
}
```

**Generated Model (excerpt):**
```yaml
window:
  start: "2025-10-07T00:00:00Z"
  timezone: "UTC"

grid:
  bins: 24
  binSize: 1
  binUnit: "hours"

topology:
  nodes:
    - id: "OrderService"
      kind: "service"
      semantics:
        arrivals: "OrderService_arrivals_gold"
        capacity: "OrderService_capacity_gold"

nodes:
  - id: OrderService_arrivals_gold
    kind: const
    values: [120, 135, 140, 150, ...]
```

### 9.2 Example: Overlay Scenario

**Request:**
```json
{
  "runKind": "gold-snapshot",
  "window": { ... },
  "selection": { "nodes": ["OrderService"] },
  "overlays": [
    {
      "nodeId": "capacity_doubled",
      "kind": "expr",
      "expr": "OrderService_capacity_gold * 2"
    }
  ],
  "topologyOverride": {
    "nodes": [
      {
        "id": "OrderService",
        "semantics": { "capacity": "capacity_doubled" }
      }
    ]
  }
}
```

**API Response (/v1/state):**
```json
{
  "nodes": {
    "OrderService": {
      "observed": {
        "arrivals": 150,
        "capacity": 200,
        "served": 145
      },
      "modeled": {
        "capacity": 400,
        "served": 150
      },
      "utilization_observed": 0.725,
      "utilization_modeled": 0.375
    }
  }
}
```

---

## 10. Appendices

### A. Glossary

- **Gold MV:** Materialized View in Azure Data Explorer with pre-aggregated telemetry
- **NodeTimeBin:** Primary Gold table with per-node, per-bin telemetry
- **Bin:** Discrete time period (e.g., 5 minutes, 1 hour)
- **Window:** Absolute time range [start, end) for a run
- **Topology:** Logical structure of nodes and edges
- **Semantics:** Mapping from topology roles (arrivals, capacity) to series IDs
- **Observed:** Values from Gold telemetry (what actually happened)
- **Modeled:** Values from expressions/PMFs (what could happen)
- **Snapshot Run:** Immutable artifact created from Gold slice at specific extraction_ts

### B. Related Documents

- **Model Schema:** docs/schemas/model.schema.md
- **Current Plan:** docs/architecture/time-travel-architecture-plan.md
- **M3.0 Milestone:** docs/milestones/M3.0.md
- **Whitepaper:** docs/architecture/whitepaper.md
- **Roadmap:** docs/ROADMAP.md

### C. Open Questions

1. **Gold Schema Evolution:** How to handle breaking changes in NodeTimeBin schema?
   - **Proposed:** Adapter versioning with schema_version validation
   
2. **Multi-Tenant Isolation:** How to scope Gold queries per tenant?
   - **Proposed:** Pass tenant_id in selection; filter in KQL

3. **Large Windows:** How to handle runs with 10K+ bins?
   - **Proposed:** Pagination for /state_window, Parquet for artifacts

4. **Real-Time:** Can we support near-real-time (< 5 min lag)?
   - **Proposed:** Accept eventual consistency; provenance indicates lag

### D. Performance Targets

| Operation | Target | Measurement |
|-----------|--------|-------------|
| Gold query (1000 bins) | <100ms | P95 latency |
| Model generation | <500ms | Gold → model.yaml |
| Model execution (288 bins) | <1s | Parse → Evaluate → Write |
| GET /v1/state | <50ms | Single-bin snapshot |
| GET /v1/state_window (288 bins) | <200ms | Dense arrays |
| GET /v1/metrics (full day) | <100ms | Aggregation |

### E. Test Strategy

**Unit Tests:**
- TimeGrid helpers (bin arithmetic, UTC conversion)
- Topology validation (DAG, semantic references)
- Adapter transformation (Gold → model)
- Derived metrics (utilization, latency)

**Integration Tests:**
- End-to-end Gold snapshot flow
- API response structure validation
- Backward compatibility (legacy models)
- Overlay scenario generation

**Performance Tests:**
- Large window handling (10K bins)
- Concurrent runs (100+ users)
- Gold query latency

**Golden Tests:**
- Fixed Gold fixture → consistent artifacts
- Regression detection across releases

---

## Document History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2025-10-08 | FlowTime Architecture Team | Initial comprehensive architecture |

---

**End of Document**

