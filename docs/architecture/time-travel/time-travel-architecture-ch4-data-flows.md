# FlowTime Architecture: KISS Approach - Chapter 4

## Chapter 4: Data Flows and Examples

This chapter provides detailed end-to-end flows with concrete examples, including ETL pipeline design and edge case handling.

---

### 4.1 End-to-End Flow: Telemetry Replay

#### 4.1.1 Scenario

**User Request:** "Show me OrderService behavior on October 7, 2025 (5-minute bins)"

**System Components Involved:**
1. Production system (emits telemetry)
2. Azure Data Explorer (Gold layer)
3. TelemetryLoader
4. Template system
5. FlowTime Engine
6. FlowTime UI

---

#### 4.1.2 Flow Diagram

```
[Production]
    ↓ telemetry events
[App Insights]
    ↓ ingestion
[ADX Raw Events]
    ↓ ETL aggregation
[Gold NodeTimeBin]
    ↓ KQL query
[TelemetryLoader]
    ↓ CSV files
[Template + Parameters]
    ↓ instantiation
[model.yaml]
    ↓ evaluation
[Engine]
    ↓ artifacts
[Object Storage]
    ↓ API response
[UI Visualization]
```

---

#### 4.1.3 Step-by-Step Execution

**Step 1: User Initiates Request (UI)**

```
User Action: Click "Time Travel" → Select date 2025-10-07 → Select nodes: OrderService

UI Action:
  POST https://api.flowtime.com/v1/runs
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
      "nodes": ["OrderService"]
    },
    "parameters": {
      "q0": 0,
      "enable_capacity_inference": true
    }
  }
```

**Step 2: API Gateway Validates Request**

```
Validation Checks:
  1. Authentication: User has valid JWT token
  2. Authorization: User can access "order-system" template
  3. Window format: start is UTC, aligned to 5-minute boundary
  4. Template exists: "order-system" in templates/telemetry/
  5. Parameters: q0 is number, enable_capacity_inference is boolean

If validation passes:
  Generate runId: "run_20251007T000000Z_abc123"
  Return 202 Accepted
  Enqueue background job
```

**Step 3: Background Worker Picks Up Job**

```
Worker receives message:
  {
    "runId": "run_20251007T000000Z_abc123",
    "request": { /* original request */ }
  }

Worker creates work directory:
  /tmp/flowtime/runs/run_20251007T000000Z_abc123/
    telemetry/  (for loader output)
    model/      (for instantiated model)
    artifacts/  (for engine output)
```

**Step 4: TelemetryLoader Extracts Data**

```
Loader Configuration:
  ADX Cluster: https://cluster.region.kusto.windows.net
  Database: Telemetry
  Auth: Managed Identity

KQL Query Construction:
  let startTime = datetime(2025-10-07T00:00:00Z);
  let endTime = datetime(2025-10-08T00:00:00Z);
  let selectedNodes = dynamic(["OrderService"]);
  
  NodeTimeBin
  | where ts >= startTime and ts < endTime
  | where node in (selectedNodes)
  | project ts, node, arrivals, served, errors, 
            external_demand, queue_depth
  | order by node asc, ts asc

Query Execution:
  - Connection established
  - Query submitted
  - Streaming results (288 rows expected)
  - Rows received: 286 (bins 12 and 13 missing)

Dense Fill:
  Expected bins: [0, 1, 2, ..., 287]
  Actual bins: [0, 1, ..., 11, 14, ..., 287]
  Missing: [12, 13]
  Action: Zero-fill bins 12, 13
  Warning: "Data gap: bins 12-13 (14:00-14:10 UTC)"

File Writing:
  telemetry/OrderService_arrivals.csv:
    120
    135
    140
    ...
    0    ← bin 12 (zero-filled)
    0    ← bin 13 (zero-filled)
    155
    ...
  
  telemetry/OrderService_served.csv
  telemetry/OrderService_errors.csv
  telemetry/OrderService_demand.csv
  
  manifest.json:
    {
      "window": {...},
      "files": [...],
      "warnings": [
        {
          "type": "data_gap",
          "bins": [12, 13],
          "message": "Missing data 14:00-14:10 UTC (zero-filled)"
        }
      ]
    }

Loader returns: Success with 1 warning
```

**Step 5: Template Instantiation**

```
Load Template:
  Read: templates/telemetry/order-system.yaml
  Parse YAML
  Resolve includes: shared/common-topology.yaml

Apply Parameters:
  {{telemetry_dir}} = "/tmp/.../telemetry"
  {{q0}} = 0
  {{enable_capacity_inference}} = true

Generate model.yaml:
  schemaVersion: 1
  window:
    start: "2025-10-07T00:00:00Z"
    timezone: "UTC"
  grid:
    bins: 288
    binSize: 5
    binUnit: "minutes"
  topology:
    nodes:
      - id: "OrderService"
        kind: "service"
        semantics:
          arrivals: "orders_arrivals"
          served: "orders_served"
          errors: "orders_errors"
          external_demand: "orders_demand"
          capacity: "capacity_inferred"
  nodes:
    - id: orders_arrivals
      kind: const
      source: "file:///tmp/.../telemetry/OrderService_arrivals.csv"
    
    - id: orders_served
      kind: const
      source: "file:///tmp/.../telemetry/OrderService_served.csv"
    
    - id: orders_errors
      kind: const
      source: "file:///tmp/.../telemetry/OrderService_errors.csv"
    
    - id: orders_demand
      kind: const
      source: "file:///tmp/.../telemetry/OrderService_demand.csv"
    
    - id: capacity_inferred
      kind: expr
      expr: "infer_capacity_from_saturation(orders_served, orders_demand, null)"
  
  provenance:
    generatedAt: "2025-10-07T14:30:00Z"
    generator: "template-instantiator"
    template:
      name: "order-system"
      version: "1.0"
    parameters:
      q0: 0
      enable_capacity_inference: true

Write: model/model.yaml
```

**Step 6: Engine Evaluation**

```
Engine receives: model/model.yaml

Phase 1: Parse and Validate
  - Schema version: 1 (supported)
  - Window: valid UTC, aligned
  - Grid: 288 bins × 5 minutes
  - Topology: 1 node (OrderService)
  - Nodes: 5 series (4 const, 1 expr)
  
  Validation:
    ✓ All topology.semantics references exist
    ✓ No cycles in series DAG
    ✓ File sources exist and readable
    ✓ No self-referencing SHIFT (capacity_inferred is not recursive)

Phase 2: Load const series
  - Read OrderService_arrivals.csv → array[288]
  - Read OrderService_served.csv → array[288]
  - Read OrderService_errors.csv → array[288]
  - Read OrderService_demand.csv → array[288]
  
  Validation:
    ✓ All arrays length = 288
    ✓ All values are numeric

Phase 3: Build DAG
  Topological order:
    1. orders_arrivals (no dependencies)
    2. orders_served (no dependencies)
    3. orders_errors (no dependencies)
    4. orders_demand (no dependencies)
    5. capacity_inferred (depends on orders_served, orders_demand)
  
  No cycles detected

Phase 4: Evaluate expressions
  For capacity_inferred:
    For each bin t in 0..287:
      If orders_demand[t] > orders_served[t]:
        capacity[t] = orders_served[t]  # Saturated
      Else:
        capacity[t] = ROLLING_MAX(orders_served, 12)[t]
  
  Result: capacity_inferred = [200, 200, 195, 190, 185, ...]

Phase 5: Derive metrics (for API only, not persisted)
  # These are computed on-demand by /state endpoint
  # Not written to artifacts

Phase 6: Write artifacts
  artifacts/run.json:
    {
      "runId": "run_20251007T000000Z_abc123",
      "status": "completed",
      "grid": {...},
      "window": {...},
      "mode": "telemetry",
      "provenance": {...},
      "warnings": [
        {
          "type": "data_gap",
          "bins": [12, 13],
          "message": "Missing telemetry 14:00-14:10 UTC (zero-filled)"
        }
      ]
    }
  
  artifacts/model.yaml:
    # Copy of evaluated model
  
  artifacts/topology.json:
    # Extracted topology for UI
  
  artifacts/series/orders_arrivals.csv
  artifacts/series/orders_served.csv
  artifacts/series/orders_errors.csv
  artifacts/series/capacity_inferred.csv
  
  artifacts/debug/dag.json:
    # Series DAG for debugging

Upload to Blob Storage:
  Source: /tmp/.../artifacts/
  Destination: https://storage.blob.core.windows.net/flowtime/runs/run_20251007T000000Z_abc123/

Cleanup:
  Delete: /tmp/flowtime/runs/run_20251007T000000Z_abc123/

Engine returns: Success
```

**Step 7: Worker Updates Run Status**

```
Update run record:
  status: "completed"
  completedAt: "2025-10-07T14:31:23Z"
  artifacts: {
    root: "https://...",
    run_json: "https://.../run.json",
    model_yaml: "https://.../model.yaml",
    topology_json: "https://.../topology.json"
  }
```

**Step 8: UI Polls and Renders**

```
UI polls: GET /v1/runs/run_20251007T000000Z_abc123
  status: "completed" → Stop polling

UI loads graph: GET /v1/runs/run_20251007T000000Z_abc123/graph
  - Render topology (1 node: OrderService)
  - Display node at coordinates (120, 260)

User scrubs timeline: 14:00 UTC (bin 168)
  
  UI requests: GET /v1/runs/.../state?ts=2025-10-07T14:00:00Z
  
  Engine computes on-demand:
    - Load series values at bin 168
    - arrivals[168] = 0 (zero-filled gap)
    - served[168] = 0
    - demand[168] = 0
    - capacity_inferred[168] = 190 (from rolling max)
    
    - Derive demand_gap = demand - arrivals = 0 - 0 = 0
    - Color: green (no backlog)
  
  Response:
    {
      "bin": { "index": 168, "startUtc": "2025-10-07T14:00:00Z" },
      "nodes": {
        "OrderService": {
          "kind": "service",
          "metrics": {
            "arrivals": 0,
            "served": 0,
            "errors": 0,
            "external_demand": 0
          },
          "inferred": {
            "capacity": 190,
            "capacity_method": "rolling_max",
            "capacity_confidence": "low"
          },
          "derived": {
            "demand_gap": 0
          },
          "coloring": {
            "color": "green",
            "reason": "No backlog"
          }
        }
      },
      "dataQuality": {
        "complete": false,
        "gaps": [{"bins": [12, 13], "reason": "Telemetry gap"}]
      }
    }

UI displays:
  - OrderService node colored green
  - Tooltip: "Arrivals: 0, Served: 0"
  - Warning icon: "Data gap at this timestamp"
```

---

### 4.2 ETL Pipeline: App Insights → Gold

#### 4.2.1 Production Telemetry Events

**Service Code (OrderProcessor):**

```
Service receives message from queue "orders-in"

Event 1: Message Received
  Timestamp: 2025-10-07T14:00:05.123Z
  EventType: MessageReceived
  Properties:
    node: "OrderService"
    messageId: "msg-abc-123"
    queueName: "orders-in"

Process message (call database, call downstream API)
  Duration: 2.3 seconds

Event 2: Message Processed
  Timestamp: 2025-10-07T14:00:07.456Z
  EventType: MessageProcessed
  Properties:
    node: "OrderService"
    messageId: "msg-abc-123"
    outputQueue: "orders-out"
    duration: 2300

Enqueue to "orders-out" queue
Commit transaction
```

**Service Bus Metrics:**

```
Queue: orders-in
  Metric: IncomingMessages
    Timestamp: 2025-10-07T14:00:00Z (1-minute aggregate)
    Value: 30 messages

  Metric: CompletedMessages
    Timestamp: 2025-10-07T14:00:00Z
    Value: 28 messages

  Metric: ActiveMessageCount
    Timestamp: 2025-10-07T14:00:59Z (end of minute)
    Value: 252 messages
```

#### 4.2.2 ETL Aggregation Logic

**KQL ETL Pipeline (runs every 5 minutes):**

```
// Step 1: Extract App Insights events
let StartTime = ago(10m);  // Process last 10 minutes for overlap
let EndTime = ago(5m);      // Up to 5 minutes ago

let ServiceEvents = 
    customEvents
    | where timestamp >= StartTime and timestamp < EndTime
    | where name in ("MessageReceived", "MessageProcessed", "MessageFailed")
    | extend 
        node = tostring(customDimensions["node"]),
        messageId = tostring(customDimensions["messageId"]),
        eventType = name
    | project timestamp, node, messageId, eventType;

// Step 2: Aggregate to 5-minute bins per node
let ServiceMetrics =
    ServiceEvents
    | summarize
        arrivals = countif(eventType == "MessageReceived"),
        served = countif(eventType == "MessageProcessed"),
        errors = countif(eventType == "MessageFailed")
      by node, bin(timestamp, 5m);

// Step 3: Get Service Bus metrics
let QueueMetrics =
    AzureServiceBus_Metrics
    | where TimeGenerated >= StartTime and TimeGenerated < EndTime
    | where MetricName in ("IncomingMessages", "CompletedMessages", "ActiveMessageCount")
    | extend 
        queueName = ResourceId,
        metricType = MetricName,
        value = Total
    | summarize
        external_demand = sumif(value, metricType == "IncomingMessages"),
        queue_completions = sumif(value, metricType == "CompletedMessages"),
        queue_depth = avgif(value, metricType == "ActiveMessageCount")
      by queueName, bin(TimeGenerated, 5m);

// Step 4: Join and map queue to service node
let ServiceNodeMapping = datatable(node:string, inputQueue:string) [
    "OrderService", "orders-in",
    "BillingService", "billing-in"
];

let CombinedMetrics =
    ServiceMetrics
    | join kind=leftouter (
        QueueMetrics
        | join kind=inner (ServiceNodeMapping) on $left.queueName == $right.inputQueue
        | project bin_timestamp=bin_TimeGenerated, node, external_demand, queue_depth
    ) on node, $left.bin_timestamp == $right.bin_timestamp
    | extend
        ts = bin_timestamp,
        arrivals = coalesce(arrivals, 0),
        served = coalesce(served, 0),
        errors = coalesce(errors, 0),
        external_demand = coalesce(external_demand, arrivals),  // Fallback
        queue_depth = coalesce(queue_depth, 0.0);

// Step 5: Add provenance and materialize
CombinedMetrics
| extend
    schema_version = "2.0",
    extraction_ts = now(),
    known_data_gaps = ""  // Detect gaps in separate query
| project 
    ts, node, 
    arrivals, served, errors, 
    external_demand, queue_depth,
    schema_version, extraction_ts, known_data_gaps
| materialize as NodeTimeBin
```

**Gap Detection (separate query):**

```
// Run hourly to detect and flag gaps
let HourStart = ago(1h);
let HourEnd = now();

let ExpectedBins = 
    range ts from HourStart to HourEnd step 5m
    | project ts;

let ActualBins =
    NodeTimeBin
    | where ts >= HourStart and ts < HourEnd
    | distinct ts;

let MissingBins =
    ExpectedBins
    | join kind=leftanti (ActualBins) on ts;

MissingBins
| summarize 
    missing_bins = make_list(ts),
    gap_count = count()
  by node
| extend known_data_gaps = tostring(missing_bins)
| join kind=inner (NodeTimeBin) on node
| project-away missing_bins, gap_count
```

#### 4.2.3 Example Data Transformation

**Input (App Insights raw events):**

```
Timestamp                   | Event              | Node         | MessageId
----------------------------|--------------------|--------------|----------
2025-10-07T14:00:05.123Z    | MessageReceived    | OrderService | msg-001
2025-10-07T14:00:07.456Z    | MessageProcessed   | OrderService | msg-001
2025-10-07T14:01:12.789Z    | MessageReceived    | OrderService | msg-002
2025-10-07T14:01:15.234Z    | MessageProcessed   | OrderService | msg-002
2025-10-07T14:02:08.567Z    | MessageReceived    | OrderService | msg-003
2025-10-07T14:02:10.890Z    | MessageFailed      | OrderService | msg-003
...
```

**Output (NodeTimeBin after aggregation):**

```
ts                       | node         | arrivals | served | errors | external_demand | queue_depth
-------------------------|--------------|----------|--------|--------|-----------------|-------------
2025-10-07T14:00:00Z     | OrderService | 35       | 33     | 2      | 38              | 250
2025-10-07T14:05:00Z     | OrderService | 32       | 30     | 2      | 35              | 255
2025-10-07T14:10:00Z     | OrderService | 0        | 0      | 0      | 0               | 255
2025-10-07T14:15:00Z     | OrderService | 37       | 35     | 2      | 40              | 260
...
```

**Note:** Bin 14:10 has zeros (telemetry gap) which ETL detects and flags in `known_data_gaps`.

---

### 4.3 Scenario: Overlay (What-If)

#### 4.3.1 User Request

**Scenario:** "What if OrderService capacity doubled?"

**Starting Point:** Existing run from telemetry replay (run_abc123)

#### 4.3.2 Overlay Request

```
POST /v1/runs
{
  "mode": "overlay",
  "baseRunId": "run_abc123",  # Start from telemetry run
  "overlays": [
    {
      "nodeId": "capacity_doubled",
      "kind": "expr",
      "expr": "capacity_inferred * 2"
    }
  ],
  "topologyOverride": {
    "nodes": [
      {
        "id": "OrderService",
        "semantics": {
          "capacity": "capacity_doubled"  # Use new capacity
        }
      }
    ]
  },
  "parameters": {
    "scenario_name": "Capacity Doubled"
  }
}
```

#### 4.3.3 Engine Processing

```
Step 1: Load base run
  - Read run_abc123/model.yaml
  - Already has telemetry const series
  - Already has capacity_inferred

Step 2: Apply overlays
  - Add new expression node: capacity_doubled = capacity_inferred * 2
  - Update topology: OrderService.semantics.capacity → capacity_doubled

Step 3: Re-evaluate
  - Keep all const series as-is (telemetry unchanged)
  - Recompute capacity_doubled:
      capacity_doubled[t] = capacity_inferred[t] * 2
      = [200*2, 200*2, 195*2, ...] = [400, 400, 390, ...]
  
  - If template has served = MIN(arrivals, capacity):
      Recompute served with new capacity
      served_scenario[t] = MIN(arrivals[t], capacity_doubled[t])
    
    But original template uses observed served from telemetry!
    So we need to add a new expression:
      served_scenario = MIN(arrivals, capacity_doubled)

Step 4: Generate overlay model
  nodes:
    # Original telemetry
    - id: arrivals
      kind: const
      source: "file://.../arrivals.csv"
    
    - id: served_observed
      kind: const
      source: "file://.../served.csv"
    
    - id: capacity_inferred
      kind: expr
      expr: "..."
    
    # Overlay additions
    - id: capacity_doubled
      kind: expr
      expr: "capacity_inferred * 2"
    
    - id: served_scenario
      kind: expr
      expr: "MIN(arrivals, capacity_doubled)"
  
  topology:
    nodes:
      - id: OrderService
        semantics:
          arrivals: "arrivals"
          served: "served_scenario"         # Scenario
          capacity: "capacity_doubled"      # Scenario
          served_baseline: "served_observed" # Keep baseline

Step 5: Evaluate and write artifacts
  - New runId: run_20251007T143000Z_xyz789
  - Artifacts include both baseline and scenario series
```

#### 4.3.4 API Response Comparison

```
GET /v1/runs/run_xyz789/state?ts=2025-10-07T14:00:00Z

Response:
{
  "mode": "overlay",
  "baseRunId": "run_abc123",
  "scenario": "Capacity Doubled",
  
  "nodes": {
    "OrderService": {
      "kind": "service",
      "baseline": {
        "arrivals": 150,
        "served": 145,
        "capacity": 200
      },
      "scenario": {
        "served": 150,          # Now can serve all arrivals
        "capacity": 400
      },
      "delta": {
        "served": +5,
        "utilization": -0.363   # (145/200) → (150/400)
      },
      "coloring": {
        "baseline": "yellow",
        "scenario": "green"
      }
    }
  }
}
```

---

### 4.4 Edge Cases and Error Handling

#### 4.4.1 Telemetry Gaps

**Scenario:** App Insights ingestion delayed, causing missing bins

**Detection:**
```
ETL detects: Bins [12, 13, 14] have no events
Action: Flag in known_data_gaps
```

**TelemetryLoader handling:**
```
Loader sees known_data_gaps: "[12, 13, 14]"
Action: Zero-fill with warning
Warning: "Telemetry gap: bins 12-14 (14:00-14:15 UTC)"
```

**Engine handling:**
```
Engine evaluates normally with zeros
Post-evaluation validation flags anomaly:
  Warning: "Low throughput: served=0 at bins 12-14"
```

**UI display:**
```
Timeline shows gap with warning icon
Tooltip: "Incomplete data at this timestamp"
Node color: gray (instead of green/yellow/red)
```

#### 4.4.2 Conservation Violations

**Scenario:** Telemetry data violates flow conservation

**Example:**
```
Queue at t=10: depth = 100
Queue at t=11: depth = 50
Arrivals[11] = 10
Served[11] = 20

Conservation: arrivals - served = depth[11] - depth[10]
              10 - 20 ≠ 50 - 100
              -10 ≠ -50
              Violation: -40 units
```

**Engine handling:**
```
Post-evaluation validation:
  For each bin t:
    residual = arrivals[t] - served[t] - (queue[t] - queue[t-1])
    If |residual| > 0.01:
      Add warning

Result:
  Warning: "Conservation violated at bin 11: residual = -40"
```

**Possible Causes:**
- Telemetry from different time windows (served from later window)
- Message retries counted multiple times
- Queue depth snapshot timing issues

**User Action:**
- Investigate telemetry quality
- Adjust tolerance if acceptable
- Re-run with corrected data

#### 4.4.3 Capacity Estimation Failure

**Scenario:** Inferred capacity unrealistically high

**Example:**
```
Served[t] = 150 (max observed)
Inferred capacity (rolling max) = 150

But user knows actual capacity = 200
Inference is underestimate
```

**Engine handling:**
```
Validation check:
  If capacity_inferred < MAX(served):
    Warning: "Inferred capacity may underestimate"
```

**User Action:**
- Provide explicit capacity in overlay:
  ```
  overlays:
    - nodeId: capacity_override
      kind: const
      values: [200, 200, 200, ...]
  ```

#### 4.4.4 Template Parameter Type Mismatch

**Scenario:** User provides string for number parameter

**Request:**
```
POST /v1/runs
{
  "parameters": {
    "q0": "zero"  # Should be number
  }
}
```

**Response:**
```
400 Bad Request
{
  "error": "parameter_type_mismatch",
  "parameter": "q0",
  "expected": "number",
  "actual": "string",
  "value": "zero"
}
```

#### 4.4.5 File Not Found

**Scenario:** TelemetryLoader writes files, but Engine can't read them

**Cause:** Permissions issue or cleanup happened too early

**Engine error:**
```
500 Internal Server Error
{
  "error": "file_not_found",
  "message": "Cannot read source file",
  "file": "file:///tmp/.../OrderService_arrivals.csv",
  "nodeId": "orders_arrivals"
}
```

**Mitigation:**
- Ensure temp files persist until after Engine evaluation
- Add file existence check before evaluation
- Use atomic rename for file writes

---

**End of Chapter 4**

Continue to Chapter 5 for Implementation Roadmap and Milestones.
