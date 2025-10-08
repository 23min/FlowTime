# FlowTime Architecture: KISS Approach - Chapter 2

## Chapter 2: Data Contracts

This chapter defines all data formats and interfaces: Gold telemetry schema, Model YAML format, API request/response contracts, and file formats.

---

### 2.1 Gold Telemetry Schema

#### 2.1.1 NodeTimeBin (Primary Table)

**Purpose:** Pre-aggregated service-level metrics per time bin

**Grain:** `(node, ts)` where `ts` is bin start timestamp (UTC)

**Table Structure:**

| Column | Type | Required | Description |
|--------|------|----------|-------------|
| `ts` | datetime | ✅ | Bin start timestamp (UTC, aligned to bin boundary) |
| `node` | string | ✅ | Logical node identifier (e.g., "OrderService") |
| `arrivals` | long | ✅ | Count of work items arriving at node in this bin |
| `served` | long | ✅ | Count of work items successfully completed |
| `errors` | long | ✅ | Count of failures or exceptions |
| `external_demand` | long | ⚠️ | Demand arriving at upstream queue (if queue-based) |
| `queue_depth` | real | ⚠️ | Queue backlog (if applicable, from Service Bus) |
| `schema_version` | string | ✅ | Gold schema version (e.g., "2.0") |
| `extraction_ts` | datetime | ✅ | When this row was computed by ETL |
| `known_data_gaps` | string | ⚠️ | JSON array of gap intervals, e.g., `[{"start":"...", "end":"..."}]` |

**Column Semantics:**

**`arrivals`** (Required)
- For queue-based services: Count of messages dequeued from input queue (Service Bus CompletedMessages)
- For HTTP services: Count of incoming requests (App Insights requests)
- Represents work entering the processing node

**`served`** (Required)
- Count of work items successfully processed and outputs generated
- For queue-based: Messages enqueued to output queue
- For HTTP: Successful HTTP 2xx responses
- `served ≤ arrivals` (equality when error rate is zero)

**`errors`** (Required)
- Count of failed processing attempts
- Includes exceptions, timeouts, validation failures
- `errors = arrivals - served` (approximate, may differ due to retries)

**`external_demand`** (Optional but Recommended)
- For queue-based services: Inflow to upstream queue (Service Bus IncomingMessages)
- Represents external load on the system
- `external_demand - arrivals` accumulates as `queue_depth`
- Not applicable for HTTP services (set to NULL or same as arrivals)

**`queue_depth`** (Optional)
- Average or end-of-bin snapshot of queue backlog
- From Service Bus ActiveMessageCount metric
- Useful for validation and capacity inference
- Not applicable for stateless HTTP services

**Indexing:**
```sql
-- Primary index for time-range queries
INDEX idx_node_time ON NodeTimeBin (node, ts)

-- Covering index for common queries
INDEX idx_ts_node_metrics ON NodeTimeBin (ts, node) INCLUDE (arrivals, served, errors)
```

**Example Rows:**

```
Queue-Based Service:
| ts                   | node           | arrivals | served | errors | external_demand | queue_depth | extraction_ts           |
|----------------------|----------------|----------|--------|--------|-----------------|-------------|-------------------------|
| 2025-10-07T14:00:00Z | OrderService   | 150      | 145    | 5      | 180             | 250         | 2025-10-07T14:05:23Z    |
| 2025-10-07T14:05:00Z | OrderService   | 155      | 150    | 5      | 175             | 270         | 2025-10-07T14:10:18Z    |

HTTP Service:
| ts                   | node           | arrivals | served | errors | external_demand | queue_depth | extraction_ts           |
|----------------------|----------------|----------|--------|--------|-----------------|-------------|-------------------------|
| 2025-10-07T14:00:00Z | OrderAPI       | 320      | 315    | 5      | NULL            | NULL        | 2025-10-07T14:05:23Z    |
```

#### 2.1.2 QueueTimeBin (Optional)

**Purpose:** Dedicated queue metrics when queues are separate entities

**When to Use:** 
- When modeling queues as distinct nodes (not implicit in services)
- When queue has metrics not tied to specific service (e.g., shared queues)

**Grain:** `(queue_name, ts)`

| Column | Type | Required | Description |
|--------|------|----------|-------------|
| `ts` | datetime | ✅ | Bin start timestamp |
| `queue_name` | string | ✅ | Queue identifier (e.g., "orders-in") |
| `incoming_messages` | long | ✅ | Messages arriving at queue |
| `completed_messages` | long | ✅ | Messages consumed from queue |
| `active_count` | long | ✅ | Current queue depth |
| `oldest_message_age_s` | real | ⚠️ | Age of oldest message in seconds |
| `dlq_count` | long | ⚠️ | Dead-letter queue count |

**Usage Pattern:**
```sql
-- Join service metrics with queue metrics
NodeTimeBin n
| join kind=leftouter (QueueTimeBin q) on 
    $left.ts == $right.ts 
    AND $left.node == strcat($right.queue_name, "_Service")
```

#### 2.1.3 Provenance Columns

**Purpose:** Track data lineage and quality

**All Gold tables MUST include:**

| Column | Description | Example |
|--------|-------------|---------|
| `schema_version` | Gold schema version | "2.0" |
| `extraction_ts` | When ETL computed this row | "2025-10-07T14:05:23Z" |
| `known_data_gaps` | Detected gaps in source data | `[{"start":"2025-10-07T14:00:00Z", "end":"2025-10-07T14:15:00Z", "reason":"App Insights lag"}]` |

**Gap Detection Logic:**
```sql
-- ETL detects gaps by checking for missing bins
let ExpectedBins = range(start, end, binSize);
let ActualBins = distinctBins(rawData);
let Gaps = ExpectedBins - ActualBins;
```

**Gap Handling:**
- Gaps < 15 minutes: Zero-fill with warning
- Gaps > 15 minutes: Mark as known_data_gaps
- Consumer decides how to handle (skip, interpolate, or error)

#### 2.1.4 What Gold Does NOT Store

**Derived Metrics (Computed by Engine):**
- ❌ `latency_min` or latency percentiles
- ❌ `utilization`
- ❌ `capacity_proxy` or capacity estimates
- ❌ `sla_breach` flags
- ❌ Stateful computations (cumulative sums, rolling windows)

**Rationale:** 
- Power BI can compute these if needed (simple formulas)
- Engine recomputes from base observations (ensures consistency)
- Reduces Gold schema complexity
- Avoids dual source-of-truth problems

**Exception for Power BI:**
If Power BI requires pre-computed metrics for performance, create a separate view:
```sql
-- Optional: Power BI optimization view
NodeTimeBin_BI AS
SELECT 
    ts, node,
    arrivals, served, errors,
    served * 1.0 / NULLIF(arrivals, 0) AS success_rate,
    queue_depth,
    CASE WHEN queue_depth > 100 THEN 1 ELSE 0 END AS high_backlog_flag
FROM NodeTimeBin
```

But `NodeTimeBin` (base table) remains clean.

---

### 2.2 Model Schema

#### 2.2.1 Top-Level Structure

```yaml
schemaVersion: 1              # Schema version (breaking changes increment)

window:                       # Absolute time anchoring
  start: "2025-10-07T00:00:00Z"  # ISO-8601 UTC (bin 0 timestamp)
  timezone: "UTC"             # MUST be "UTC"

grid:                         # Discrete time grid
  bins: 288                   # Number of time periods
  binSize: 5                  # Duration magnitude
  binUnit: "minutes"          # Enum: minutes|hours|days|weeks

topology:                     # Logical system structure
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
  generatedAt: "2025-10-07T14:30:00Z"
  generator: "telemetry-loader|flowtime-sim|manual"
  version: "2.0.0"
  source: "adx://cluster.region.kusto.windows.net/database"
```

#### 2.2.2 Topology Section

```yaml
topology:
  nodes:
    - id: "OrderService"              # Unique identifier
      kind: "service"                 # Enum: service|queue|router|external
      group: "Orders"                 # Logical grouping for UI
      ui:                             # UI layout hints
        x: 120
        y: 260
      semantics:                      # Mapping to series IDs
        arrivals: "orders_arrivals"       # REQUIRED for service/queue
        served: "orders_served"           # REQUIRED for service/queue
        errors: "orders_errors"           # OPTIONAL
        queue: "orders_queue"             # REQUIRED for kind=queue, NULL for service
        external_demand: "orders_demand"  # OPTIONAL (for capacity inference)
        capacity: "orders_capacity"       # OPTIONAL (if inferred)
        latency_min: null                 # NULL = engine derives
        sla_min: 5.0                      # Constant (for coloring)
        
    - id: "OrderQueue"
      kind: "queue"
      group: "Orders"
      ui: { x: 340, y: 260 }
      semantics:
        arrivals: "queue_inflow"
        served: "queue_outflow"
        queue: "queue_depth"          # REQUIRED for queue
        q0: 0                         # Initial queue depth
  
  edges:
    - id: "e1"
      from: "OrderService:out"        # Format: nodeId:port
      to: "OrderQueue:in"
```

**Validation Rules:**

1. **Node ID Uniqueness:**
   - `topology.nodes[*].id` must be unique
   - No two nodes can share the same ID

2. **Kind-Specific Requirements:**
   ```
   kind=service → MUST have: arrivals, served
                  OPTIONAL: capacity, errors
   
   kind=queue   → MUST have: arrivals, served, queue
                  OPTIONAL: q0 (initial depth)
   
   kind=router  → MUST have: arrivals, served
                  SHOULD have: edges showing split ratios
   
   kind=external → No requirements (boundary node)
   ```

3. **Semantic References:**
   - All `semantics.*` values MUST reference existing `nodes[*].id`
   - Exception: `null` values (means "not applicable")

4. **Edge Validity:**
   - `edges[*].from` and `edges[*].to` must reference existing topology nodes
   - No self-loops (from = to)
   - No cycles in edge graph (DAG validation)

#### 2.2.3 Series Nodes

**Const Node (From Telemetry):**
```yaml
- id: orders_arrivals
  kind: const
  source: "file://telemetry/OrderService_arrivals.csv"
  # File format: single column of numbers, no header
  # Length MUST equal grid.bins
```

**Const Node (Inline):**
```yaml
- id: fixed_capacity
  kind: const
  values: [200, 200, 200, ...]  # Array of length grid.bins
```

**Expr Node (Computation):**
```yaml
- id: orders_served
  kind: expr
  expr: "MIN(orders_arrivals, 200)"
  # Expression can reference other node IDs
```

**Expr Node (Stateful with SHIFT):**
```yaml
- id: queue_depth
  kind: expr
  expr: "MAX(0, SHIFT(queue_depth, 1) + inflow - outflow)"
  initial: 0  # REQUIRED for self-referencing SHIFT
```

**PMF Node (Simulation):**
```yaml
- id: stochastic_arrivals
  kind: pmf
  pmf:
    type: "poisson"
    lambda: 150
  # Engine samples or computes expectation
```

**Expression Grammar:**

```
Expr := Literal | Reference | UnaryOp | BinaryOp | FunctionCall

Literal := Number (e.g., 42, 3.14)

Reference := NodeId (e.g., arrivals, capacity)

UnaryOp := '-' Expr | '+' Expr

BinaryOp := Expr ('+' | '-' | '*' | '/') Expr

FunctionCall := 
  | MIN(Expr, Expr)
  | MAX(Expr, Expr)
  | SHIFT(Reference, Integer)
  | ABS(Expr)
  | SQRT(Expr)
  | POW(Expr, Expr)
  | IF(Condition, ThenExpr, ElseExpr)  # Future
  | ROLLING_MAX(Reference, Integer)    # Future

Operators: Standard precedence (* / before + -)
```

**SHIFT Semantics:**
```yaml
SHIFT(series, k):
  If k > 0 (lag):
    - SHIFT(x, k)[t] = x[t-k] if t-k >= 0
    - SHIFT(x, k)[t] = initial if t-k < 0
  
  If k < 0 (lead):
    - SHIFT(x, k)[t] = x[t-k] if t-k < bins
    - SHIFT(x, k)[t] = 0 if t-k >= bins
  
  If k = 0:
    - SHIFT(x, 0)[t] = x[t] (identity)

Self-Reference Rule:
  If expr contains SHIFT(id, k) where k>0 AND id == current node id:
    - REQUIRE: initial field is present
    - ERROR if missing: "Self-referencing SHIFT requires explicit 'initial'"
```

#### 2.2.4 Outputs Section

```yaml
outputs:
  - series: "orders_arrivals"
    as: "OrderService_arrivals"    # Output filename
  
  - series: "queue_depth"
    as: "OrderQueue_depth"
  
  - series: "*"                    # Wildcard: all series
    exclude: ["temp_*"]            # Exclude pattern
```

**Purpose:** 
- Controls which series are written to artifacts
- Allows renaming for readability
- Reduces artifact size by excluding intermediate computations

**Default Behavior:**
- If `outputs` section is absent: Write all series referenced in topology.semantics
- If `outputs` present: Only write specified series

---

### 2.3 Telemetry File Formats

#### 2.3.1 CSV Format (Preferred)

**File Structure:**
```
# Single-column format (no header)
120
135
140
...
```

**Requirements:**
- One value per line
- No header row
- Length MUST equal `grid.bins`
- Values are float64
- Missing values represented as empty line or `NaN`

**Filename Convention:**
```
{node}_{metric}.csv

Examples:
OrderService_arrivals.csv
OrderService_served.csv
OrderService_errors.csv
OrderQueue_depth.csv
```

#### 2.3.2 JSON Format (Alternative)

```json
{
  "node": "OrderService",
  "metric": "arrivals",
  "bins": 288,
  "values": [120, 135, 140, ...]
}
```

**Use Case:** 
- When metadata needs to travel with data
- For debugging or manual inspection
- Less efficient than CSV

#### 2.3.3 Manifest File

**Purpose:** Describe all extracted telemetry files

**Format:**
```json
{
  "window": {
    "start": "2025-10-07T00:00:00Z",
    "end": "2025-10-08T00:00:00Z",
    "timezone": "UTC"
  },
  "grid": {
    "bins": 288,
    "binSize": 5,
    "binUnit": "minutes"
  },
  "files": [
    {
      "node": "OrderService",
      "metric": "arrivals",
      "path": "telemetry/OrderService_arrivals.csv",
      "rows": 288,
      "checksum": "sha256:abc123..."
    },
    {
      "node": "OrderService",
      "metric": "served",
      "path": "telemetry/OrderService_served.csv",
      "rows": 288,
      "checksum": "sha256:def456..."
    }
  ],
  "warnings": [
    {
      "type": "data_gap",
      "bins": [12, 13, 14],
      "message": "Missing data 14:00-14:15 UTC (zero-filled)"
    }
  ],
  "provenance": {
    "extraction_ts": "2025-10-07T14:30:00Z",
    "source": "adx://cluster.region.kusto.windows.net/database",
    "loader_version": "2.0.0"
  }
}
```

**Validation:**
- Engine reads manifest first
- Verifies all referenced files exist
- Checks row counts match `grid.bins`
- Optionally validates checksums

---

### 2.4 API Contracts

#### 2.4.1 POST /v1/runs (Create Run)

**Request (Option 1: From Telemetry)**
```http
POST /v1/runs
Content-Type: application/json

{
  "mode": "telemetry",
  "template": "order-system",
  "window": {
    "start": "2025-10-07T00:00:00Z",
    "bins": 288,
    "binSize": 5,
    "binUnit": "minutes"
  },
  "selection": {
    "nodes": ["OrderService", "OrderQueue"]
  },
  "parameters": {
    "q0": 0
  },
  "provenance": {
    "request_id": "uuid-...",
    "purpose": "investigation"
  }
}
```

**Request (Option 2: From Model)**
```http
POST /v1/runs
Content-Type: application/yaml

schemaVersion: 1
window: { ... }
grid: { ... }
nodes: [ ... ]
```

**Response (202 Accepted for async, 200 OK for sync):**
```json
{
  "runId": "run_20251007T143000Z_abc123",
  "status": "pending|running|completed|failed",
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
  "mode": "telemetry|model",
  "artifacts": {
    "root": "https://storage.blob.core.windows.net/flowtime/runs/run_20251007T143000Z_abc123/",
    "run_json": ".../run.json",
    "model_yaml": ".../model.yaml",
    "topology_json": ".../topology.json"
  },
  "createdAt": "2025-10-07T14:30:00Z",
  "completedAt": "2025-10-07T14:30:45Z"
}
```

**Error Responses:**
```json
400 Bad Request:
{
  "error": "invalid_window",
  "message": "window.start must be UTC (end with 'Z')",
  "field": "window.start",
  "value": "2025-10-07T00:00:00"
}

400 Bad Request:
{
  "error": "bin_alignment",
  "message": "window.start must align to bin boundary",
  "expected": "2025-10-07T14:00:00Z",
  "actual": "2025-10-07T14:03:27Z"
}

404 Not Found:
{
  "error": "template_not_found",
  "message": "Template 'order-system' does not exist",
  "available": ["order-system", "billing-system"]
}

500 Internal Server Error:
{
  "error": "evaluation_failed",
  "message": "Cycle detected in series DAG",
  "path": ["queue_depth", "inflow", "queue_depth"]
}
```

#### 2.4.2 GET /v1/runs/{runId}

**Response:**
```json
{
  "runId": "run_20251007T143000Z_abc123",
  "status": "completed",
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
  "mode": "telemetry",
  "template": "order-system",
  "artifacts": { ... },
  "warnings": [
    {
      "type": "data_gap",
      "bins": [12, 13],
      "message": "Missing telemetry 14:00-14:10 UTC (zero-filled)"
    },
    {
      "type": "conservation_violation",
      "node": "OrderQueue",
      "bins": [45],
      "residual": 2.3,
      "message": "Flow conservation violated by 2.3 units"
    }
  ],
  "provenance": {
    "source_window_start": "2025-10-07T00:00:00Z",
    "source_window_end": "2025-10-08T00:00:00Z",
    "extraction_ts": "2025-10-07T14:30:00Z",
    "loader_version": "2.0.0",
    "engine_version": "2.1.0"
  },
  "createdAt": "2025-10-07T14:30:00Z",
  "completedAt": "2025-10-07T14:30:45Z"
}
```

#### 2.4.3 GET /v1/runs/{runId}/graph

**Purpose:** Retrieve topology and series DAG for visualization

**Response:**
```json
{
  "runId": "run_abc123",
  "grid": { "bins": 288, "binSize": 5, "binUnit": "minutes", "binMinutes": 5 },
  "window": { "start": "...", "end": "...", "timezone": "UTC" },
  
  "topology": {
    "nodes": [
      {
        "id": "OrderService",
        "kind": "service",
        "group": "Orders",
        "ui": { "x": 120, "y": 260 },
        "semantics": {
          "arrivals": "orders_arrivals",
          "served": "orders_served",
          "capacity": "capacity_inferred"
        }
      },
      {
        "id": "OrderQueue",
        "kind": "queue",
        "group": "Orders",
        "ui": { "x": 340, "y": 260 },
        "semantics": {
          "arrivals": "queue_inflow",
          "served": "queue_outflow",
          "queue": "queue_depth"
        }
      }
    ],
    "edges": [
      {
        "id": "e1",
        "from": "OrderService:out",
        "to": "OrderQueue:in"
      }
    ]
  },
  
  "seriesDag": {
    "nodes": [
      "orders_arrivals",
      "orders_served",
      "orders_errors",
      "queue_inflow",
      "queue_outflow",
      "queue_depth",
      "capacity_inferred"
    ],
    "edges": [
      { "from": "orders_arrivals", "to": "orders_served" },
      { "from": "queue_inflow", "to": "queue_depth" },
      { "from": "queue_depth", "to": "queue_depth" }  // Self-loop via SHIFT
    ]
  }
}
```

#### 2.4.4 GET /v1/runs/{runId}/state?ts={timestamp}

**Purpose:** Single-bin snapshot with derived metrics

**Request:**
```
GET /v1/runs/run_abc123/state?ts=2025-10-07T14:00:00Z
```

**Response:**
```json
{
  "runId": "run_abc123",
  "mode": "telemetry",
  "grid": { "binMinutes": 5 },
  "window": { "start": "...", "end": "...", "timezone": "UTC" },
  
  "bin": {
    "index": 168,
    "startUtc": "2025-10-07T14:00:00Z",
    "endUtc": "2025-10-07T14:05:00Z"
  },
  
  "nodes": {
    "OrderService": {
      "kind": "service",
      "metrics": {
        "arrivals": 150,
        "served": 145,
        "errors": 5
      },
      "inferred": {
        "capacity": 200,
        "capacity_method": "demand-adjusted",
        "capacity_confidence": "medium"
      },
      "derived": {
        "success_rate": 0.967,
        "demand_gap": 0
      },
      "coloring": {
        "metric": "demand_gap",
        "value": 0,
        "color": "green",
        "reason": "No backlog"
      }
    },
    
    "OrderQueue": {
      "kind": "queue",
      "metrics": {
        "arrivals": 145,
        "served": 140,
        "queue": 250
      },
      "derived": {
        "latency_min": 8.93,
        "queue_growth": 5
      },
      "coloring": {
        "metric": "latency_min",
        "value": 8.93,
        "threshold": 5.0,
        "color": "red",
        "reason": "Latency 8.93min exceeds SLA 5.0min"
      },
      "validation": {
        "conservation": {
          "residual": 0.0,
          "tolerance": 0.01,
          "valid": true
        }
      }
    }
  },
  
  "dataQuality": {
    "complete": true,
    "gaps": []
  }
}
```

**Coloring Rules:**

Services (by demand_gap):
```
green:  demand_gap <= 0 (keeping up)
yellow: demand_gap > 0 AND demand_gap <= 10 (small backlog)
red:    demand_gap > 10 (large backlog)
```

Queues (by SLA):
```
green:  latency_min <= sla_min
yellow: latency_min > sla_min AND latency_min <= 1.5 * sla_min
red:    latency_min > 1.5 * sla_min
```

#### 2.4.5 GET /v1/runs/{runId}/state_window

**Purpose:** Dense time-series slice for sparklines

**Request:**
```
GET /v1/runs/run_abc123/state_window?start=2025-10-07T00:00:00Z&end=2025-10-07T12:00:00Z&limit=100&offset=0
```

**Response:**
```json
{
  "runId": "run_abc123",
  "mode": "telemetry",
  "grid": { "binMinutes": 5 },
  "window": { "start": "...", "end": "..." },
  
  "slice": {
    "start": "2025-10-07T00:00:00Z",
    "end": "2025-10-07T12:00:00Z",
    "bins": 144,
    "limit": 100,
    "offset": 0,
    "hasMore": true
  },
  
  "timestamps": [
    "2025-10-07T00:00:00Z",
    "2025-10-07T00:05:00Z",
    ...  // 100 timestamps (limited)
  ],
  
  "nodes": {
    "OrderService": {
      "kind": "service",
      "series": {
        "arrivals": [120, 125, 130, ...],      // 100 values
        "served": [118, 124, 128, ...],
        "demand_gap": [0, 0, 2, ...]
      }
    },
    "OrderQueue": {
      "kind": "queue",
      "series": {
        "queue": [250, 255, 257, ...],
        "latency_min": [8.5, 8.7, 8.9, ...]
      }
    }
  },
  
  "pagination": {
    "next": "/v1/runs/run_abc123/state_window?start=...&end=...&limit=100&offset=100"
  }
}
```

**Pagination:**
- Default limit: 100 bins
- Max limit: 500 bins
- Use offset for subsequent pages
- Returns `hasMore: true` if more data available

#### 2.4.6 GET /v1/runs/{runId}/metrics

**Purpose:** Aggregated SLA metrics

**Request:**
```
GET /v1/runs/run_abc123/metrics?start=2025-10-07T00:00:00Z&end=2025-10-08T00:00:00Z
```

**Response:**
```json
{
  "runId": "run_abc123",
  "mode": "telemetry",
  "window": { "start": "...", "end": "..." },
  
  "aggregates": {
    "bins_total": 288,
    "bins_with_gaps": 2,
    "data_completeness": 0.993
  },
  
  "nodes": {
    "OrderService": {
      "totals": {
        "arrivals": 43200,
        "served": 42500,
        "errors": 700
      },
      "success_rate": 0.984,
      "avg_demand_gap": 2.3
    },
    
    "OrderQueue": {
      "totals": {
        "arrivals": 42500,
        "served": 41800
      },
      "sla": {
        "target_min": 5.0,
        "bins_meeting_sla": 275,
        "bins_total": 288,
        "sla_pct": 95.5
      },
      "latency": {
        "avg_min": 1.4,
        "p50_min": 0.8,
        "p95_min": 4.2,
        "p99_min": 8.7,
        "worst_min": 12.3
      },
      "queue": {
        "avg_depth": 250,
        "max_depth": 450,
        "bins_over_threshold": 15
      }
    }
  }
}
```

---

**End of Chapter 2**

Continue to Chapter 3 for Component Design (TelemetryLoader, Templates, Engine).
