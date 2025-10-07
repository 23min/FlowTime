grid:
  bins: 168          # 7 days of hourly data
  binSize: 1
  binUnit: "hours"
nodes:
  - id: orders_arrivals
    kind: const
    role: "arrivals"     # NEW: Optional hint for linting
    values: [10, 15, 12, ...]
  
  - id: orders_capacity
    kind: const
    role: "capacity"
    values: [20, 20, 20, ...]
  
  - id: orders_served
    kind: expr
    role: "served"
    expr: "MIN(orders_arrivals, orders_capacity)"
  
  - id: queue_backlog
    kind: expr
    role: "queue"
    expr: "MAX(0, SHIFT(queue_backlog, 1) + queue_inflow - billing_capacity)"
  
  - id: queue_outflow
    kind: expr
    role: "served"
    expr: "MIN(SHIFT(queue_backlog, 1) + queue_inflow, billing_capacity)"
nodes:
  - id: service_arrivals
    kind: const
    role: arrivals
    values: [250, 300, 280, ...]  # Spiky demand
  
  - id: service_capacity
    kind: const
    role: capacity
    values: [200, 200, 200, ...]  # Fixed capacity
  
  - id: service_served
    kind: expr
    role: served
    expr: "MIN(service_arrivals, service_capacity)"
  
  - id: service_overflow
    kind: expr
    role: overflow
    expr: "MAX(0, service_arrivals - service_capacity)"
```
graph LR
    A[Arrivals] --> CAP[CapacityNode]
    C[Capacity] --> CAP
    CAP --> S[Served]
    CAP --> O[Overflow]
    O --> Q[QueueNode]
    
    Q --> LAT[Latency Derivation]
    S --> LAT
    LAT --> SLA[SLA Check]
    
    style SLA fill:#f99,stroke:#333,stroke-width:2px
    style CAP fill:#9f9,stroke:#333,stroke-width:2px
    "binUnit": "hours",
    "binMinutes": 60
  },
  "window": {
    "start": "2025-10-07T00:00:00Z",
    "end": "2025-10-08T00:00:00Z",
    "binCount": 24,
    "timezone": "UTC"
  },
  "flows": {
    "Orders": {
      "sla_min": 5.0,
      "bins_meeting_sla": 23,
      "bins_total": 24,
      "sla_pct": 95.8,
      "worst_latency_min": 6.2,
      "avg_latency_min": 1.4,
      "total_errors": 12
    },
    "Billing": {
      "sla_min": 3.0,
      "bins_meeting_sla": 21,
      "bins_total": 24,
      "sla_pct": 87.5,
      "worst_latency_min": 4.5,
      "avg_latency_min": 2.1,
      "total_errors": 8
    }
  }
}
```

**Aggregation Rules**:
- `bins_meeting_sla`: count bins where `latency_min <= sla_min` for queue nodes in flow
- `sla_pct = 100.0 * bins_meeting_sla / bins_total`
- `worst_latency_min = max(latency_min)` across all queue nodes in flow
- `avg_latency_min = mean(latency_min)` across all queue nodes in flow
- `total_errors = sum(errors)` across all nodes in flow

## P3.4: Data Flow

```mermaid
graph TB
    LB[LoadBalancer<br/>arrivals=100] --> A[ServiceA<br/>ratio=0.6<br/>gets 60]
    LB --> B[ServiceB<br/>ratio=0.4<br/>gets 40]
    
    A --> QA[QueueA]
    B --> QB[QueueB]
    
    QA --> CHECK1[Conservation:<br/>60 - 58 - ΔQ = 0?]
    QB --> CHECK2[Conservation:<br/>40 - 39 - ΔQ = 0?]
    
    style CHECK1 fill:#9f9,stroke:#333,stroke-width:2px
    style CHECK2 fill:#9f9,stroke:#333,stroke-width:2px
```

## P3.5: Success Criteria

- [ ] Routing via expressions works (e.g., `lb_arrivals * 0.6`)
- [ ] Optional validation: warn if routing ratios don't sum to ~1.0
- [ ] Conservation residuals computed per bin for all queue nodes
- [ ] Warnings logged when |residual| > tolerance
- [ ] GET /v1/runs/{id}/metrics returns SLA aggregates per flow
- [ ] /metrics response includes grid/binMinutes and bin bounds
- [ ] bins_meeting_sla and sla_pct calculated correctly
- [ ] worst_latency_min and avg_latency_min aggregated correctly

---

# P4: Demo Scenario

**Goal**: End-to-end time-travel demo with realistic scenario

**Why**: Validate entire stack, prepare for UI integration

## P4.1: Demo Topology

**Scenario**: Order processing system with queue backlog

```yaml
System:
  - OrderService (service)
    - Normal load: 100 req/h
    - Spike at t=50: 300 req/h (3x)
    - Capacity: 150 req/h
  
  - OrderQueue (queue)
    - Receives overflow from OrderService
    - Capacity: 120 req/h
  
  - BillingService (service)
    - Processes from OrderQueue
    - Capacity: 120 req/h
```

**Expected Behavior**:
- **t=0-49**: Normal operation, no queue buildup
- **t=50-59**: Spike causes OrderService saturation → overflow to queue
- **t=60-80**: Queue drains slowly, latency elevated
- **t=81+**: Back to normal

```mermaid
graph LR
    D[Demand<br/>Spike at t=50] --> OS[OrderService<br/>cap=150]
    OS -->|overflow| Q[OrderQueue<br/>backlog]
    Q --> BS[BillingService<br/>cap=120]
    
    OS -.->|latency spike| SLA[SLA Breach<br/>t=50-60]
    Q -.->|queue buildup| LAT[High Latency<br/>t=60-80]
    
    style D fill:#f99,stroke:#333,stroke-width:2px
    style SLA fill:#f99,stroke:#333,stroke-width:2px
    style LAT fill:#ff9,stroke:#333,stroke-width:2px
```

## P4.2: Demo Model Template

**File**: `flowtime-sim-vnext/templates/demo-time-travel.yaml`

```yaml
schemaVersion: "1.0"
modelFormat: "1.1"

window:
  start: "2025-10-07T00:00:00Z"
  timezone: "UTC"

grid:
  bins: 168
  binSize: 1
  binUnit: "hours"

topology:
  classes: ["*"]
  
  nodes:
    - id: "OrderService"
      kind: "service"
      group: "Orders"
      ui: { x: 100, y: 200 }
      semantics:
        arrivals: "orders_demand"
        capacity: "orders_capacity"
        served: "orders_served"
        errors: "orders_errors"
        sla_min: 5.0
    
    - id: "OrderQueue"
      kind: "queue"
      group: "Orders"
      ui: { x: 300, y: 200 }
      semantics:
        arrivals: "queue_inflow"
        served: "queue_outflow"
        queue: "queue_backlog"
    
    - id: "BillingService"
      kind: "service"
      group: "Billing"
      ui: { x: 500, y: 200 }
      semantics:
        arrivals: "billing_arrivals"
        capacity: "billing_capacity"
        served: "billing_served"
        sla_min: 3.0
  
  edges:
    - { id: "e1", from: "OrderService:out", to: "OrderQueue:in" }
    - { id: "e2", from: "OrderQueue:out", to: "BillingService:in" }

nodes:
  # Demand with spike (const vector - no t/AND operators needed)
  - id: orders_demand
    kind: const
    role: arrivals
    # 168 hourly bins: bins 0-49 = 100, bins 50-59 = 300 (spike), bins 60-167 = 100
    values: >-
      [100]*50 + [300]*10 + [100]*108
    # Note: flowtime-sim generator can expand this notation, or emit full array
  
  # Service capacity
  - id: orders_capacity
    kind: const
    role: capacity
    values: >-
      [150]*168
  
  # Served = min(demand, capacity) - using expr, not capacity node
  - id: orders_served
    kind: expr
    role: served
    expr: "MIN(orders_demand, orders_capacity)"
  
  # Queue inflow = overflow from OrderService
  - id: queue_inflow
    kind: expr
    role: arrivals
    expr: "MAX(0, orders_demand - orders_capacity)"
  
  # Billing capacity (downstream capacity for queue)
  - id: billing_capacity
    kind: const
    role: capacity
    values: >-
      [120]*168
  
  # Queue backlog (stateful) - using expr with SHIFT
  - id: queue_backlog
    kind: expr
    role: queue
    expr: "MAX(0, SHIFT(queue_backlog, 1) + queue_inflow - billing_capacity)"
  
  # Queue outflow (derived from backlog + inflow, clamped by downstream capacity)
  - id: queue_outflow
    kind: expr
    role: served
    expr: "MIN(SHIFT(queue_backlog, 1) + queue_inflow, billing_capacity)"
  
  # Billing receives queue outflow
  - id: billing_arrivals
    kind: expr
    role: arrivals
    expr: "queue_outflow"
  
  # Billing served (clamped by capacity)
  - id: billing_served
    kind: expr
    role: served
    expr: "MIN(billing_arrivals, billing_capacity)"

outputs:
  - orders_demand
  - orders_served
  - queue_backlog
  - billing_served
```

## P4.3: Demo Verification Steps

**1. Generate Model**:

```bash
cd /workspaces/flowtime-sim-vnext
dotnet run --project src/FlowTime.Sim.Cli -- \
  --template templates/demo-time-travel.yaml \
  --out demo-spike.yaml
```

**2. Run Model**:

```bash
cd /workspaces/flowtime-vnext
curl -X POST http://localhost:8080/v1/run \
  -H "Content-Type: text/plain" \
  -d @demo-spike.yaml
```

**3. Query State at Spike**:

```bash
curl "http://localhost:8080/v1/runs/{runId}/state?ts=2025-10-07T50:00:00Z"
```

**Expected Output**:

```json
{
  "binIndex": 50,
  "nodes": {
    "OrderService": {
      "arrivals": 300.0,
      "served": 150.0,
      "capacity": 150.0,
      "utilization": 1.0,
      "sla_breach": true
    },
    "OrderQueue": {
      "queue": 150.0,
      "latency_min": 1.25
    }
  }
}
```

**4. Query Window (Spike Recovery)**:

```bash
curl "http://localhost:8080/v1/runs/{runId}/state_window?start=2025-10-07T45:00:00Z&end=2025-10-07T85:00:00Z"
```

**Expected**: Queue builds from 0 → 150 at t=50-59, then drains to 0 by t=85

## P4.4: Success Criteria

- [ ] Demo model generates without errors (no t/AND operators, only const vectors)
- [ ] Spike at bins 50-59 causes queue buildup (using expr with SHIFT)
- [ ] /state?ts={spike-time} shows saturation + SLA breach + utilization_band=red
- [ ] /state_window shows queue ramp-up and drain-down over [45, 85)
- [ ] Conservation residuals near zero throughout (validated automatically)
- [ ] Latency correlates with queue depth (Little's Law)
- [ ] /metrics aggregates SLA correctly over spike window
- [ ] All API responses include grid.binMinutes
- [ ] Capacity inference applied where capacity series missing (warnings logged)
- [ ] UI can render graph + scrub timeline (manual verification)

---

## Implementation Checklist

### P0: Foundation
- [ ] **flowtime-sim**: Add window, topology, classes to schema
- [ ] **flowtime-sim**: Implement template generator for topology
- [ ] **flowtime-sim**: Add validation rules (window.start, semantic refs)
- [ ] **flowtime-vnext**: Extend TimeGrid with StartTimeUtc
- [ ] **flowtime-vnext**: Add Topology model classes
- [ ] **flowtime-vnext**: Update ModelParser for window + topology
- [ ] **flowtime-vnext**: Extend /v1/graph response with topology
- [ ] **flowtime-vnext**: Extend /v1/run response with window
- [ ] **Tests**: Schema validation (10 tests)
- [ ] **Tests**: API responses (5 tests)

### P1: Queue Expressions + State
- [ ] **flowtime-vnext**: Support SHIFT(x, n) in expressions (n > 0)
- [ ] **flowtime-vnext**: Validate SHIFT self-references (reject n <= 0)
- [ ] **flowtime-vnext**: Parse and evaluate expr nodes with SHIFT
- [ ] **flowtime-vnext**: Implement q0 (initial queue state) semantics
- [ ] **flowtime-vnext**: Implement GET /v1/runs/{id}/state (with grid, bin bounds)
- [ ] **flowtime-vnext**: Implement GET /v1/runs/{id}/state_window (with grid)
- [ ] **flowtime-vnext**: Timestamp → bin index conversion (validate ts = bin start)
- [ ] **flowtime-vnext**: Derive latency for queue nodes in state API
- [ ] **flowtime-vnext**: Add utilization_band calculation (green/yellow/red)
- [ ] **flowtime-vnext**: Implement conservation check (arrivals - served - ΔQ)
- [ ] **Tests**: SHIFT expression tests (self-ref, reject n<=0) (4 tests)
- [ ] **Tests**: Conservation residual tests (3 tests)
- [ ] **Tests**: State API integration tests (grid, bin bounds, ts validation) (10 tests)
- [ ] **Tests**: State window API tests (end-exclusive ranges, binCount) (6 tests)
- [ ] **Tests**: Bin alignment edge cases (2 tests)

### P2: Latency Derivation + Capacity Inference
- [ ] **flowtime-vnext**: Implement capacity inference (Q[t-1] > 0 → capacity = served)
- [ ] **flowtime-vnext**: Implement smooth envelope fallback (piecewise max)
- [ ] **flowtime-vnext**: Clarify latency only for queue nodes (not service nodes)
- [ ] **flowtime-vnext**: Add SLA breach detection to state API
- [ ] **flowtime-vnext**: Add sla_headroom calculation
- [ ] **flowtime-vnext**: Add sla_min to node metadata
- [ ] **Tests**: Capacity inference tests (binding constraint, envelope) (5 tests)
- [ ] **Tests**: SLA breach tests (3 tests)
- [ ] **Tests**: Latency derivation for queues only (2 tests)

### P3: Routing Expressions + Conservation
- [ ] **flowtime-vnext**: Validate routing expr patterns (arrivals * ratio)
- [ ] **flowtime-vnext**: Add routing ratio validation (sum of ratios = 1)
- [ ] **flowtime-vnext**: Add conservation residual to state API response
- [ ] **flowtime-vnext**: Implement GET /v1/runs/{id}/metrics (SLA aggregates)
- [ ] **flowtime-vnext**: Add per-flow and per-node SLA% calculation
- [ ] **Tests**: Routing expression tests (5 tests)
- [ ] **Tests**: Conservation residual in API response (4 tests)
- [ ] **Tests**: /metrics API tests (aggregates, per-flow) (6 tests)

### P4: Demo
- [ ] **flowtime-sim**: Create demo-time-travel.yaml template (expr-based nodes)
- [ ] **flowtime-sim**: Generate spike scenario model (const vector, no t/AND)
- [ ] **flowtime-vnext**: Run demo model end-to-end
- [ ] **Manual**: Verify /state at spike (bins 50-59) shows saturation
- [ ] **Manual**: Verify /state_window shows queue dynamics (end-exclusive ranges)
- [ ] **Manual**: Verify conservation residuals logged
- [ ] **Manual**: Verify capacity inference triggers during spike
- [ ] **Docs**: Write demo README with curl commands (include /metrics)

---

## Risk Mitigation

### High Risk
- **Timestamp → bin index edge cases**: Test window boundaries, timezone mismatches
- **Stateful node evaluation order**: BacklogNode must evaluate after dependencies
- **Conservation tolerance**: May need adjustment based on real data

### Medium Risk
- **CSV read performance**: Monitor state_window with large bins (1000+)
- **Schema backward compatibility**: Ensure old models still work

### Low Risk
- **UI coordinate layout**: Can hardcode for demo, defer to layout engine later

---

## UI Contract: Time-Travel Visualization APIs

This section defines the API surface needed for time-travel UI visualizations. For detailed UX specs, see `UI-VISUALIZATIONS.md`.

### Required UI Views

1. **SLA% Dashboard**: Per-flow SLA compliance over time window
2. **Flow Graph**: Topology with colored nodes (by latency/SLA breach)
3. **Node Details**: Per-bin metrics (latency, queue, arrivals, served, utilization, errors)
4. **Mini Graphs**: Sparklines showing metric trends per node

### API: GET /v1/runs/{runId}/metrics

**Purpose**: Aggregate metrics for SLA dashboard and flow-level summaries

**Query Parameters**:
- `start` (ISO-8601): Window start timestamp
- `end` (ISO-8601): Window end timestamp
- `flow` (optional): Filter by flow name

**Response**:

```json
{
  "runId": "run_20251007T143000Z_abc123",
  "window": {
    "start": "2025-10-07T00:00:00Z",
    "end": "2025-10-07T23:59:59Z",
    "binCount": 24
  },
  "flows": {
    "Orders": {
      "sla_pct": 95.8,
      "bins_meeting_sla": 23,
      "bins_total": 24,
      "bins_breaching_sla": 1,
      "worst_latency_min": 6.2,
      "avg_latency_min": 1.4,
      "total_arrivals": 3450,
      "total_served": 3380,
      "total_errors": 12,
      "peak_queue": 45,
      "nodes": {
        "OrderService": {
          "sla_pct": 100.0,
          "sla_min": 5.0,
          "bins_meeting_sla": 24,
          "worst_latency_min": 2.1,
          "avg_latency_min": 0.8
        },
        "OrderQueue": {
          "sla_pct": 91.7,
          "sla_min": 3.0,
          "bins_meeting_sla": 22,
          "worst_latency_min": 6.2,
          "avg_latency_min": 2.1
        }
      }
    }
  }
}
```

**Calculation Rules**:

```yaml
Per Node:
  sla_pct = (bins_meeting_sla / bins_total) × 100
  bins_meeting_sla = COUNT(latency_min[t] <= sla_min)
  worst_latency_min = MAX(latency_min[t])
  avg_latency_min = AVG(latency_min[t])

Per Flow:
  sla_pct = MIN(node.sla_pct for node in flow)  # Worst node determines flow SLA
  total_arrivals = SUM(node.arrivals[t] for all t, all nodes)
  peak_queue = MAX(node.queue[t] for all t, all nodes)
```

**Implementation**: P2 (after /state_window exists)

---

### API: GET /v1/runs/{runId}/flow/{flowName}

**Purpose**: Flow topology with per-node current state (for graph coloring)

**Query Parameters**:
- `ts` (ISO-8601): Timestamp for node state snapshot
- `window_start`, `window_end` (optional): For mini-graph data

**Response**:

```json
{
  "runId": "run_20251007T143000Z_abc123",
  "flow": "Orders",
  "timestamp": "2025-10-07T14:00:00Z",
  "binIndex": 14,
  "topology": {
    "nodes": [
      {
        "id": "OrderService",
        "kind": "service",
        "group": "Orders",
        "ui": { "x": 120, "y": 260 },
        "state": {
          "arrivals": 150,
          "served": 145,
          "capacity": 200,
          "errors": 2,
          "utilization": 0.725,
          "latency_min": 0.5,
          "sla_min": 5.0,
          "sla_breach": false,
          "color": "green"
        },
        "miniGraph": {
          "metric": "latency_min",
          "bins": [0.3, 0.5, 0.8, 1.2, 0.9, 0.5, 0.4],
          "timestamps": [
            "2025-10-07T08:00:00Z",
            "2025-10-07T09:00:00Z",
            "2025-10-07T10:00:00Z",
            "2025-10-07T11:00:00Z",
            "2025-10-07T12:00:00Z",
            "2025-10-07T13:00:00Z",
            "2025-10-07T14:00:00Z"
          ]
        }
      },
      {
        "id": "OrderQueue",
        "kind": "queue",
        "group": "Orders",
        "ui": { "x": 340, "y": 260 },
        "state": {
          "arrivals": 145,
          "served": 140,
          "queue": 25,
          "capacity": 150,
          "latency_min": 10.7,
          "sla_min": 3.0,
          "sla_breach": true,
          "color": "red"
        },
        "miniGraph": {
          "metric": "queue",
          "bins": [0, 5, 10, 18, 25, 22, 25],
          "timestamps": ["..."]
        }
      }
    ],
    "edges": [
      {
        "id": "e1",
        "from": "OrderService",
        "to": "OrderQueue",
        "flow_rate": 145.0
      }
    ]
  }
}
```

**Color Rules** (suggested, UI can override):

```yaml
Node Coloring by SLA:
  green:  latency <= sla_min
  yellow: sla_min < latency <= sla_min * 1.5
  red:    latency > sla_min * 1.5

Node Coloring by Utilization:
  green:  utilization < 0.7
  yellow: 0.7 <= utilization < 0.9
  red:    utilization >= 0.9
```

**Implementation**: P2 (reuses /state + /state_window data)

---

### API Enhancement: /state_window with Aggregates

**Add optional `aggregate` parameter** to existing `/state_window` endpoint:

**Request**:
```http
GET /v1/runs/{runId}/state_window?start=2025-10-07T00:00:00Z&end=2025-10-07T23:59:59Z&aggregate=true
```

**Response** (with aggregates added):

```json
{
  "runId": "run_20251007T143000Z_abc123",
  "window": {
    "start": "2025-10-07T00:00:00Z",
    "end": "2025-10-07T23:59:59Z",
    "binCount": 24
  },
  "nodes": {
    "OrderService": {
      "kind": "service",
      "timestamps": ["2025-10-07T00:00:00Z", "2025-10-07T01:00:00Z", ...],
      "arrivals": [120, 135, 150, ...],
      "served": [120, 135, 145, ...],
      "latency_min": [0.3, 0.5, 0.8, ...],
      "aggregates": {
        "arrivals_total": 3450,
        "arrivals_avg": 143.75,
        "arrivals_max": 180,
        "served_total": 3380,
        "latency_min_avg": 0.8,
        "latency_min_max": 2.1,
        "sla_pct": 100.0,
        "bins_meeting_sla": 24
      }
    }
  }
}
```

**Implementation**: P2 (compute during /state_window query)

---

### Data Flow for UI

```mermaid
sequenceDiagram
    participant UI as UI Client
    participant Metrics as GET /metrics
    participant Flow as GET /flow/{name}
    participant Window as GET /state_window
    
    UI->>Metrics: Load SLA dashboard
    Metrics-->>UI: { flows: { Orders: { sla_pct: 95.8 } } }
    UI->>UI: Render SLA% boxes per flow
    
    UI->>Flow: Load flow graph (ts=14:00)
    Flow-->>UI: { topology, state, miniGraphs }
    UI->>UI: Render graph with colored nodes
    
    UI->>UI: User clicks node "OrderQueue"
    UI->>Window: Load detail (start=08:00, end=16:00)
    Window-->>UI: { arrivals: [...], queue: [...], ... }
    UI->>UI: Render detail panel + sparklines
```

---

### UI Implementation Priorities

**P1 (Core Time-Travel)**:
- Time scrubber (bin slider)
- Single-bin state view (`GET /state?ts=`)
- Flow graph with basic coloring

**P2 (SLA Dashboard)**:
- `GET /metrics` endpoint
- SLA% boxes per flow
- Per-node SLA% in graph

**P3 (Rich Visualization)**:
- Mini graphs per node (sparklines)
- `GET /flow/{name}` with mini-graph data
- Node detail panel with full time series

**P4 (Polish)**:
- Animation (play/pause through time)
- Compare mode (baseline vs scenario)
- Export views (PNG, PDF)

---

**For detailed UX specs**, see `UI-VISUALIZATIONS.md` (to be created)

---

## Future Extensions (Post-P4)

### Engine Purity Principle

**Core Invariant**: FlowTime engine remains pure (spreadsheet-like, deterministic, expression-based). All data source complexity lives in **adapters** that generate model.yaml.

```
┌─────────────────────────────────────┐
│  Data Sources (Outside Engine)     │
│  • Gold Tables (NodeTimeBin)        │
│  • Sim Templates                    │
│  • Hand-written YAML                │
└─────────────┬───────────────────────┘
              │
              ↓
┌─────────────────────────────────────┐
│  Adapters (Outside Engine)          │
│  • GoldToModelAdapter               │
│  • TemplateGenerator (sim)          │
│  • OverlayMerger                    │
└─────────────┬───────────────────────┘
              │
              ↓ (Always model.yaml)
┌─────────────────────────────────────┐
│  FlowTime Engine (PURE)             │
│  • Parse model.yaml                 │
│  • Build series DAG                 │
│  • Evaluate expressions             │
│  • Write artifacts                  │
│  NO telemetry awareness             │
│  NO mode switching                  │
└─────────────────────────────────────┘
```

### P5: GoldToModelAdapter (Telemetry Analysis)

**Purpose**: Convert Treehouse Gold tables to model.yaml for engine evaluation

**Location**: `FlowTime.Adapters.Gold` (new project, outside Core)

**Input**:
- Gold tables: `NodeTimeBin(flow, node, time_bin, arrivals, served, errors, capacity_proxy, backlog_est, latency_p50, latency_p95)`
- Catalog: `Catalog_Nodes(node_id, flow, kind, group, ui_x, ui_y)`
- Time window: `(start_time, end_time, bin_unit)`

**Output**: `model.yaml` with:
- `window.start` from query start time
- `topology.nodes` from Catalog
- `nodes[*]` as `const` kind with `values` from Gold columns
- Derived `expr` nodes for computed metrics (utilization, latency if missing)

**Example Transformation**:

```yaml
# Input: Gold Query Result
# NodeTimeBin WHERE flow='Orders' AND node='OrderService'
# time_bin | arrivals | served | capacity_proxy | backlog_est
#    0     |   120    |  115   |      200       |      5
#    1     |   135    |  130   |      200       |     10

# Output: model.yaml
window:
  start: "2025-10-07T00:00:00Z"
  timezone: "UTC"

grid:
  bins: 168
  binSize: 1
  binUnit: "hours"
  binMinutes: 60

topology:
  nodes:
    - id: "OrderService"
      kind: "service"
      catalog: { flow: "Orders", node: "OrderService" }  # From Catalog
      semantics:
        arrivals: "orders_svc_arrivals_gold"
        served: "orders_svc_served_gold"
        capacity: "orders_svc_capacity_gold"
        queue: "orders_svc_queue_gold"
        latency_min: "orders_svc_latency_derived"  # Computed by engine

nodes:
  # From Gold columns (const nodes)
  - id: orders_svc_arrivals_gold
    kind: const
    role: "arrivals"
    values: [120, 135, ...]  # From Gold.arrivals
  
  - id: orders_svc_served_gold
    kind: const
    role: "served"
    values: [115, 130, ...]  # From Gold.served
  
  - id: orders_svc_capacity_gold
    kind: const
    role: "capacity"
    values: [200, 200, ...]  # From Gold.capacity_proxy
  
  - id: orders_svc_queue_gold
    kind: const
    role: "queue"
    values: [5, 10, ...]  # From Gold.backlog_est
  
  # Derived by engine (expr nodes) - SPREADSHEET-LIKE!
  - id: orders_svc_latency_derived
    kind: expr
    role: "latency"
    expr: "orders_svc_queue_gold / MAX(0.001, orders_svc_served_gold) * 60"
  
  - id: orders_svc_utilization
    kind: expr
    expr: "orders_svc_served_gold / orders_svc_capacity_gold"
```

**Key Insight**: Engine computes derived metrics (latency, utilization) even though base facts came from Gold. All computations remain visible and reproducible.

**Benefits**:
- ✅ Engine stays pure (no Gold schema knowledge)
- ✅ Spreadsheet-like transparency preserved
- ✅ Can re-evaluate if Gold values change
- ✅ Derived metrics computable via expressions
- ✅ Time-travel UI works identically (reads artifacts)

**Effort**: 8-10 hours
- Define Gold schema types
- Implement catalog reader
- Generate const nodes from columns
- Generate derived expr nodes
- Write integration tests

---

### P8: OverlayMerger (Scenario Analysis)

**Purpose**: Merge Gold baseline with scenario changes for what-if analysis

**Location**: `FlowTime.Adapters.Overlay` (new project, outside Core)

**Input**:
- `baseline.yaml`: Generated by GoldToModelAdapter (real telemetry)
- `overlay.yaml`: Scenario changes (capacity adjustments, routing changes)

**Output**: `merged.yaml` with:
- Baseline series (const nodes with `_baseline` suffix)
- Scenario series (expr nodes with `_scenario` suffix)
- Comparison series (expr nodes with `_delta`, `_improvement_pct` suffixes)

**Example Overlay**:

```yaml
# overlay.yaml (scenario definition)
scenario:
  name: "2x_capacity"
  description: "What if we doubled OrderService capacity?"
  window:
    start: "2025-10-07T00:00:00Z"
    bins: 168
  changes:
    - node: "OrderService"
      field: "capacity"
      operation: "multiply"
      factor: 2.0
```

**Generated Merged Model**:

```yaml
nodes:
  # Baseline (from Gold)
  - id: demand_baseline
    kind: const
    values: [150, 180, 200, 220, ...]
  
  - id: capacity_baseline
    kind: const
    values: [200, 200, 200, 200, ...]
  
  - id: served_baseline
    kind: const
    values: [145, 180, 180, 180, ...]  # From Gold (saturated)
  
  - id: queue_baseline
    kind: const
    values: [0, 5, 20, 40, ...]  # From Gold
  
  # Scenario (computed by engine)
  - id: capacity_scenario
    kind: expr
    expr: "capacity_baseline * 2"  # 2x capacity (400)
  
  - id: served_scenario
    kind: expr
    expr: "MIN(demand_baseline, capacity_scenario)"  # No saturation
  
  - id: queue_scenario
    kind: expr
    expr: "MAX(0, SHIFT(queue_scenario, 1) + demand_baseline - served_scenario)"
    q0: 0  # Initial queue state
  
  - id: latency_scenario
    kind: expr
    expr: "queue_scenario / MAX(0.001, served_scenario) * 60"
  
  # Comparison (delta metrics)
  - id: served_delta
    kind: expr
    expr: "served_scenario - served_baseline"
  
  - id: served_improvement_pct
    kind: expr
    expr: "(served_delta / MAX(0.001, served_baseline)) * 100"
  
  - id: queue_reduction
    kind: expr
    expr: "queue_baseline - queue_scenario"
  
  - id: latency_improvement_min
    kind: expr
    expr: "latency_baseline - latency_scenario"

topology:
  nodes:
    - id: "OrderService"
      semantics:
        arrivals: "demand_baseline"
        capacity_baseline: "capacity_baseline"
        capacity_scenario: "capacity_scenario"
        served_baseline: "served_baseline"
        served_scenario: "served_scenario"
        queue_baseline: "queue_baseline"
        queue_scenario: "queue_scenario"
```

**State API Response** (with comparison):

```json
{
  "runId": "scenario_2x_capacity_abc123",
  "scenario": {
    "name": "2x_capacity",
    "baseline": "gold_20251007_baseline"
  },
  "timestamp": "2025-10-07T14:00:00Z",
  "nodes": {
    "OrderService": {
      "baseline": {
        "served": 180,
        "queue": 20,
        "latency_min": 6.67
      },
      "scenario": {
        "served": 200,
        "queue": 0,
        "latency_min": 0.0
      },
      "delta": {
        "served_improvement": 20,
        "served_improvement_pct": 11.1,
        "queue_reduction": 20,
        "latency_improvement_min": 6.67
      }
    }
  }
}
```

**Key Insight**: Engine evaluates scenario changes as pure expressions. Overlay adapter generates the comparison DAG, but engine just sees `expr` nodes.

**Benefits**:
- ✅ Engine stays pure (no overlay logic)
- ✅ Scenario changes are expressions (transparent)
- ✅ Can stack multiple overlays (overlay of overlay)
- ✅ Reproducible ("re-run scenario X on date Y")
- ✅ UI can scrub through scenario timeline

**Effort**: 6-8 hours
- Define overlay schema
- Implement node rewriting (baseline → scenario)
- Generate comparison expr nodes
- Handle topology updates
- Write integration tests

---

### P9+: Additional Node Types

Once adapters prove the architecture, add these to engine (as new node kinds):

- **P9**: Multi-class flows (replace classes: ["*"] with ["Order", "Refund"])
  - Node outputs become `[class, t]` arrays
  - Expressions handle class dimension
  
- **P10**: Retry/delay nodes (convolution kernels)
  - `RetryNode(errors, retry_kernel)` → future arrivals
  - `DelayNode(series, delay_kernel)` → shifted distribution
  
- **P11**: Autoscale nodes (threshold policies)
  - `AutoscaleNode(utilization, target, lag_bins, cooldown_bins)` → replicas
  - `CapacityNode(replicas, cap_per_replica)` → total capacity
  
- **P12**: Finite buffers + DLQ (queue caps + spill)
  - `BacklogNode` with `qcap` and `abandon_after_min`
  - `DlqNode` receives spill from capped queues

**All remain pure**: Just new expression node types, no mode switching

---

### Adapter Architecture Benefits

**For Engine**:
- ✅ Single evaluation model (no conditionals)
- ✅ Testable in isolation (unit tests with model.yaml)
- ✅ No external dependencies (no Gold client, no telemetry SDK)
- ✅ Complexity stays O(nodes × bins)

**For Adapters**:
- ✅ Independent evolution (Gold schema changes don't break engine)
- ✅ Composable (chain adapters: Gold → Overlay → Engine)
- ✅ Testable separately (adapter tests don't need engine)
- ✅ Multiple implementations (CSV adapter, Parquet adapter, API adapter)

**For Users**:
- ✅ Same time-travel UI for all data sources (sim, Gold, overlays)
- ✅ Can save and share scenarios (model.yaml is portable)
- ✅ Audit trail (model.yaml shows exact inputs and computations)
- ✅ Reproducible analysis ("re-run this exact scenario")

---

## Appendix: Key Files

### flowtime-sim-vnext
- `schemas/model-v1.1.yaml` - Extended schema definition
- `src/FlowTime.Sim.Core/ModelGenerator.cs` - Add topology generation
- `src/FlowTime.Sim.Core/Validation.cs` - Add window + topology validation
- `templates/demo-time-travel.yaml` - Demo scenario template

### flowtime-vnext
- `src/FlowTime.Core/TimeGrid.cs` - Add StartTimeUtc property
- `src/FlowTime.Core/Models/Topology.cs` - NEW: Topology model classes
- `src/FlowTime.Core/Models/ModelParser.cs` - Parse window + topology + classes
- `src/FlowTime.Core/Expressions/ShiftFunction.cs` - NEW: SHIFT(x, n) support
- `src/FlowTime.Core/Expressions/ExpressionValidator.cs` - Validate SHIFT self-refs
- `src/FlowTime.Core/Evaluation/ConservationCheck.cs` - NEW: Residual calculation
- `src/FlowTime.Core/Evaluation/CapacityInference.cs` - NEW: Infer capacity from binding
- `src/FlowTime.API/Program.cs` - Add /state, /state_window, /metrics endpoints
- `tests/FlowTime.Core.Tests/Expressions/ShiftTests.cs` - NEW
- `tests/FlowTime.Core.Tests/Evaluation/ConservationTests.cs` - NEW
- `tests/FlowTime.API.Tests/StateApiTests.cs` - NEW

---

**End of Plan**
