# FlowTime Architecture: KISS Approach - Chapter 6

## Chapter 6: Decision Log and Appendices

This final chapter documents key architectural decisions, provides reference materials, and includes examples for common scenarios.

---

### 6.1 Decision Log

#### Decision 1: Capacity-Free Core Model

**Date:** October 8, 2025  
**Status:** Accepted  
**Deciders:** Architecture Team

**Context:**

Original Gold-First architecture required `capacity_proxy` in Gold schema. Analysis revealed:
- Capacity is emergent and unknowable until saturation
- All proxy methods (replicas × throughput, configured concurrency, P95 ceiling) are flawed
- Observed `served` is ground truth for replay

**Decision:**

Engine does NOT require capacity as input. Capacity can be optionally inferred for visualization with clear labeling of method and confidence.

**Consequences:**

✅ **Positive:**
- Eliminates unreliable capacity estimation from critical path
- Simplifies Gold schema (no capacity_proxy required)
- Makes telemetry extraction more robust
- Users can provide explicit capacity for what-if scenarios

⚠️ **Negative:**
- Cannot pre-compute utilization in Gold MVs
- Inferred capacity has uncertainty (labeled appropriately)
- Power BI dashboards need adjustment if they relied on capacity

**Alternatives Considered:**

1. **Require capacity_proxy everywhere** - Rejected: Too fragile
2. **Use multiple capacity heuristics** - Rejected: Adds complexity without improving accuracy
3. **Capacity-independent metrics only** - Accepted as primary, inference as optional

**Implementation:**

- Gold schema: capacity_proxy is optional (not required)
- Templates: capacity not required in semantics
- Engine: Optional inference function `infer_capacity_from_saturation()`
- API: Label inferred capacity with method and confidence

---

#### Decision 2: Telemetry-as-Files (Not Database)

**Date:** October 8, 2025  
**Status:** Accepted  
**Deciders:** Architecture Team

**Context:**

Two approaches for Engine to consume telemetry:
1. **Direct:** Engine queries ADX directly
2. **Indirect:** TelemetryLoader extracts to files, Engine loads files

**Decision:**

Use indirect approach: TelemetryLoader writes CSV files, Engine loads from files.

**Consequences:**

✅ **Positive:**
- Engine decoupled from ADX (testable with CSV fixtures)
- Immutable snapshots (files don't change after creation)
- No connection strings or auth in Engine
- Artifacts are self-contained and portable
- Caching is simple (file system)

⚠️ **Negative:**
- Extra I/O step (ADX → CSV → Engine)
- Temporary storage required
- Cleanup policy needed

**Alternatives Considered:**

1. **Engine queries ADX directly** - Rejected: Tight coupling, hard to test
2. **Streaming (no files)** - Rejected: Breaks reproducibility, no caching
3. **In-memory buffer** - Rejected: Not portable, lost if process crashes

**Implementation:**

- TelemetryLoader writes to temp directory
- Engine file source: `source: "file://path/to/data.csv"`
- Artifacts include original telemetry CSVs
- Cleanup: Delete temp files after run completes

---

#### Decision 3: Templates Not Generated

**Date:** October 8, 2025  
**Status:** Accepted  
**Deciders:** Architecture Team

**Context:**

Two approaches for topology definition:
1. **Generated:** Query Catalog_Nodes table, infer semantics
2. **Authored:** Hand-written YAML templates in version control

**Decision:**

Use hand-authored templates in Git, not generated from Catalog_Nodes.

**Consequences:**

✅ **Positive:**
- Topology is engineering artifact (belongs in Git)
- Changes require code review
- Templates enable parameterization and reuse
- No runtime dependency on metadata tables
- Clear ownership and versioning

⚠️ **Negative:**
- Initial template authoring effort required
- Templates must be kept in sync with production topology
- Template migration needed when topology changes

**Alternatives Considered:**

1. **Generate from Catalog_Nodes** - Rejected: Loses review process, tight coupling
2. **Hybrid (generate base, manual refinement)** - Rejected: Complexity, unclear ownership
3. **UI-based template editor** - Deferred: Post-M4, doesn't replace version control

**Implementation:**

- Templates stored in `templates/` directory
- Version-controlled in Git
- TemplateParser loads and validates
- Template versioning (major.minor.patch)

---

#### Decision 4: Single Evaluation Mode

**Date:** October 8, 2025  
**Status:** Accepted  
**Deciders:** Architecture Team

**Context:**

Original Gold-First had dual modes:
- "Gold mode": Special handling for observed vs modeled
- "Model mode": Simulation-only

This created complexity in Engine and API.

**Decision:**

Engine has single evaluation mode. Data source (telemetry vs simulation) is transparent to Engine.

**Consequences:**

✅ **Positive:**
- Eliminates branching logic in Engine
- Simpler testing (one code path)
- Reduced surface area for bugs
- Easier mental model for developers

⚠️ **Negative:**
- Less automatic behavior (more explicit configuration)
- API responses don't distinguish observed vs modeled in series names

**Alternatives Considered:**

1. **Keep dual modes** - Rejected: Complexity not justified
2. **Three modes (Gold, Sim, Hybrid)** - Rejected: Even worse
3. **Mode as template parameter** - Accepted: Template type distinguishes, not Engine

**Implementation:**

- Engine evaluates `model.yaml` identically regardless of source
- Provenance tracks whether from telemetry or simulation
- API uses `mode` field for client understanding, not Engine behavior

---

#### Decision 5: UTC Only, No Timezone Support

**Date:** October 8, 2025  
**Status:** Accepted  
**Deciders:** Architecture Team

**Context:**

Supporting multiple timezones adds complexity:
- Timezone conversion logic
- DST transitions
- Storage overhead
- Test complexity

**Decision:**

All timestamps are UTC. No timezone support in Engine or Gold.

**Consequences:**

✅ **Positive:**
- Simple timestamp arithmetic
- No DST bugs
- Aligns with ADX bin() semantics
- Efficient storage (no timezone metadata)

⚠️ **Negative:**
- UI must handle timezone display
- Users in non-UTC zones need mental conversion
- Can't easily query "business hours" without conversion

**Alternatives Considered:**

1. **Support arbitrary timezones** - Rejected: Complexity too high
2. **Store UTC + original timezone** - Rejected: Adds confusion
3. **UI handles conversion** - Accepted: Separation of concerns

**Implementation:**

- Gold: ts column is UTC
- Model: window.start must end with 'Z'
- Validation: Reject non-UTC timestamps
- API: timezone field is always "UTC"
- UI: Convert to user's local timezone for display

---

#### Decision 6: Post-Evaluation Validation

**Date:** October 8, 2025  
**Status:** Accepted  
**Deciders:** Architecture Team

**Context:**

When to validate conservation laws, capacity constraints, etc:
1. **Pre-evaluation:** Block evaluation if data invalid
2. **Post-evaluation:** Warn after evaluation completes

**Decision:**

Validation runs post-evaluation and produces warnings, not errors.

**Consequences:**

✅ **Positive:**
- Real telemetry is messy (gaps, inconsistencies)
- Blocking prevents investigation of the problem
- Warnings highlight issues without stopping analysis
- Users decide how to handle violations

⚠️ **Negative:**
- Invalid runs can complete (potential confusion)
- Warnings might be ignored
- Need clear UI presentation of warnings

**Alternatives Considered:**

1. **Pre-evaluation validation** - Rejected: Too strict for telemetry
2. **Configurable (strict mode)** - Deferred: Post-M4 if needed
3. **No validation** - Rejected: Miss data quality issues

**Implementation:**

- Validation rules in model.yaml
- PostEvaluationValidator runs after Engine evaluation
- Warnings collected in run.json
- API returns warnings in response
- UI displays warning indicators on nodes/bins

---

#### Decision 7: Bin-Minutes Everywhere

**Date:** October 8, 2025  
**Status:** Accepted  
**Deciders:** Architecture Team

**Context:**

Grid uses `binSize` and `binUnit` (e.g., 5 minutes, 1 hour). Clients need to do timestamp math and Little's Law calculations.

**Decision:**

Engine derives `binMinutes = binSize × toMinutes(binUnit)` and includes in all API responses.

**Consequences:**

✅ **Positive:**
- Single scalar for all calculations
- Prevents unit conversion bugs across clients
- Simplifies Little's Law: `latency_min = queue / (served/binMinutes)`
- No confusion about "is this in minutes or hours?"

⚠️ **Negative:**
- Slightly redundant (computable from binSize/binUnit)
- One more field in responses

**Alternatives Considered:**

1. **Clients compute themselves** - Rejected: Error-prone, duplicated logic
2. **Only expose binMinutes** - Rejected: Loses original units
3. **Both (redundant)** - Accepted: Small cost for big benefit

**Implementation:**

- TimeGrid derives binMinutes on construction
- All API responses include `grid.binMinutes`
- Documentation shows conversion table

---

#### Decision 8: Queue Depth from Gold (Not Computed)

**Date:** October 8, 2025  
**Status:** Accepted  
**Deciders:** Architecture Team

**Context:**

For queue-based services, queue depth can be:
1. **Observed:** From Service Bus metrics
2. **Computed:** Via `Q[t] = Q[t-1] + arrivals - served`

**Decision:**

Gold stores observed queue_depth from Service Bus. Engine optionally computes for validation but doesn't replace observed.

**Consequences:**

✅ **Positive:**
- Observed queue depth is ground truth
- Can validate computed vs observed
- Handles cases where computation doesn't match reality

⚠️ **Negative:**
- Queue depth optional (not all services have it)
- Two representations (observed and computed) can differ

**Alternatives Considered:**

1. **Only computed (no observed)** - Rejected: Loses ground truth
2. **Only observed (no computed)** - Rejected: Can't do counterfactuals
3. **Both with validation** - Accepted: Best of both worlds

**Implementation:**

- Gold: queue_depth is optional column (from Service Bus)
- TelemetryLoader: Extracts if present
- Engine: Can compute via expressions for validation
- API: Returns both if both exist, with delta

---

### 6.2 Glossary

| Term | Definition |
|------|------------|
| **ADX** | Azure Data Explorer, Microsoft's big data analytics platform (Kusto) |
| **Bin** | Discrete time period (e.g., 5 minutes, 1 hour) for time-series aggregation |
| **BinMinutes** | Derived scalar: binSize converted to minutes for consistent calculations |
| **Conservation** | Flow law: arrivals - served = change in queue depth |
| **Const Series** | Time-series with fixed values (telemetry or hand-authored) |
| **DAG** | Directed Acyclic Graph, used for series dependencies |
| **Dense Fill** | Filling missing time bins with zeros or NaN |
| **Expr Series** | Time-series computed via expression (formula) |
| **External Demand** | Arrival rate at upstream queue (input to system) |
| **Gold Layer** | Pre-aggregated telemetry in materialized views (NodeTimeBin) |
| **Grid** | Discrete time structure: bins, binSize, binUnit |
| **Initial Condition** | Starting value for self-referencing SHIFT expressions (q0) |
| **Latency** | Time entities spend in queue (derived via Little's Law) |
| **Manifest** | Metadata file describing extracted telemetry files |
| **Overlay** | What-if scenario by replacing series with alternatives |
| **PMF** | Probability Mass Function, for stochastic simulation |
| **Provenance** | Metadata tracking data origin and transformations |
| **Saturation** | State where queue builds because demand exceeds capacity |
| **Semantics** | Mapping from topology roles (arrivals, capacity) to series IDs |
| **Series** | Array of values across time bins (length = bins) |
| **SHIFT** | Time-lag operator: SHIFT(x, k)[t] = x[t-k] |
| **Template** | Reusable topology and expression definitions (YAML) |
| **Topology** | Logical graph of nodes (services, queues) and edges |
| **UTC** | Coordinated Universal Time (timezone-free) |
| **Window** | Absolute time range [start, end) for a run |

---

### 6.3 Conversion Tables

#### Time Units

| binUnit | Minutes | Hours | Days | Weeks |
|---------|---------|-------|------|-------|
| minutes | 1 | 60 | 1440 | 10080 |
| hours | 1/60 | 1 | 24 | 168 |
| days | 1/1440 | 1/24 | 1 | 7 |
| weeks | 1/10080 | 1/168 | 1/7 | 1 |

#### Common Grid Configurations

| Use Case | Bins | BinSize | BinUnit | Total Duration |
|----------|------|---------|---------|----------------|
| Intraday (5-min) | 288 | 5 | minutes | 1 day |
| Intraday (15-min) | 96 | 15 | minutes | 1 day |
| Hourly (1 day) | 24 | 1 | hours | 1 day |
| Hourly (1 week) | 168 | 1 | hours | 1 week |
| Daily (1 month) | 30 | 1 | days | 1 month |
| Weekly (1 year) | 52 | 1 | weeks | 1 year |

---

### 6.4 Example Scenarios

#### Scenario 1: First-Time Telemetry Replay

**Goal:** Visualize OrderService behavior for one day

**Steps:**

1. **Ensure Gold Data Exists**
   ```sql
   -- Verify data in ADX
   NodeTimeBin
   | where node == "OrderService"
   | where ts >= datetime(2025-10-07T00:00:00Z)
   | summarize count()
   
   -- Expected: 288 rows (5-min bins for 24 hours)
   ```

2. **Create Template** (if doesn't exist)
   ```yaml
   # templates/telemetry/order-system.yaml
   templateType: "telemetry"
   version: "1.0"
   
   parameters:
     - name: telemetry_dir
       type: string
       required: true
   
   topology:
     nodes:
       - id: "OrderService"
         kind: "service"
         semantics:
           arrivals: "arrivals"
           served: "served"
   
   expressions:
     - id: arrivals
       kind: const
       source: "file://{{telemetry_dir}}/OrderService_arrivals.csv"
     
     - id: served
       kind: const
       source: "file://{{telemetry_dir}}/OrderService_served.csv"
   ```

3. **Submit Request**
   ```bash
   curl -X POST https://api.flowtime.com/v1/runs \
     -H "Content-Type: application/json" \
     -d '{
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
       }
     }'
   ```

4. **Response**
   ```json
   {
     "runId": "run_20251007T000000Z_abc123",
     "status": "completed",
     "artifacts": {
       "root": "https://storage.../run_20251007T000000Z_abc123/"
     }
   }
   ```

5. **Query State**
   ```bash
   curl https://api.flowtime.com/v1/runs/run_abc123/state?ts=2025-10-07T14:00:00Z
   ```

---

#### Scenario 2: What-If Analysis (Capacity Doubled)

**Goal:** Compare baseline telemetry to scenario with 2x capacity

**Steps:**

1. **Create Baseline Run** (from Scenario 1)
   - runId: `run_baseline`

2. **Submit Overlay Request**
   ```json
   {
     "mode": "overlay",
     "baseRunId": "run_baseline",
     "overlays": [
       {
         "nodeId": "capacity_doubled",
         "kind": "expr",
         "expr": "capacity_inferred * 2"
       },
       {
         "nodeId": "served_scenario",
         "kind": "expr",
         "expr": "MIN(arrivals, capacity_doubled)"
       }
     ],
     "topologyOverride": {
       "nodes": [{
         "id": "OrderService",
         "semantics": {
           "capacity": "capacity_doubled",
           "served": "served_scenario"
         }
       }]
     }
   }
   ```

3. **Compare Results**
   ```bash
   # Baseline
   curl /v1/runs/run_baseline/state?ts=2025-10-07T14:00:00Z
   
   # Scenario
   curl /v1/runs/run_scenario/state?ts=2025-10-07T14:00:00Z
   ```

4. **Analyze Delta**
   ```json
   {
     "baseline": {
       "served": 145,
       "capacity": 200,
       "utilization": 0.725
     },
     "scenario": {
       "served": 150,
       "capacity": 400,
       "utilization": 0.375
     },
     "delta": {
       "served": +5,
       "utilization": -0.350
     }
   }
   ```

---

#### Scenario 3: Debugging Data Quality Issues

**Goal:** Investigate why queue depth doesn't match conservation law

**Steps:**

1. **Create Run with Validation**
   ```yaml
   # model.yaml includes:
   validation:
     - type: conservation
       node: OrderQueue
       formula: "arrivals - served - (queue[t] - queue[t-1])"
       tolerance: 0.01
   ```

2. **Check Warnings**
   ```bash
   curl /v1/runs/run_abc123
   ```
   
   Response:
   ```json
   {
     "warnings": [
       {
         "type": "conservation_violation",
         "node": "OrderQueue",
         "bins_violated": [45, 67, 123],
         "worst_residual": 12.3,
         "message": "Flow conservation violated at 3 bins"
       }
     ]
   }
   ```

3. **Inspect Specific Bin**
   ```bash
   curl /v1/runs/run_abc123/state?ts=2025-10-07T03:45:00Z  # Bin 45
   ```
   
   Response:
   ```json
   {
     "bin": { "index": 45 },
     "nodes": {
       "OrderQueue": {
         "metrics": {
           "arrivals": 150,
           "served": 140,
           "queue": 275
         },
         "validation": {
           "conservation": {
             "residual": 12.3,
             "expected_queue": 262.7,
             "actual_queue": 275,
             "valid": false
           }
         }
       }
     }
   }
   ```

4. **Root Cause Investigation**
   - Check telemetry: `SELECT * FROM NodeTimeBin WHERE ts = ... AND node = ...`
   - Possible causes:
     - Late arrivals counted in wrong bin
     - Retries counted multiple times
     - Queue depth snapshot timing issue
   - Action: Fix ETL or adjust tolerance

---

### 6.5 Performance Targets

| Operation | Target | Rationale |
|-----------|--------|-----------|
| **ADX Query (1000 bins)** | <100ms P95 | Gold MVs are indexed |
| **TelemetryLoader (1 node, 288 bins)** | <500ms | Simple aggregation + file write |
| **Template Instantiation** | <100ms | YAML parsing + substitution |
| **Engine Evaluation (50 nodes, 288 bins)** | <1s | Vectorized ops, O(nodes × bins) |
| **POST /v1/runs (end-to-end)** | <2s | For synchronous response |
| **GET /v1/state (single bin)** | <50ms | Read artifacts, compute metrics |
| **GET /v1/state_window (100 bins)** | <200ms | Stream from artifacts |

**Scaling Targets:**

- Support 300 nodes per model
- Support 10,000 bins (one week at 1-min resolution)
- Handle 100 concurrent runs (with worker queue)
- Store 10,000 runs (with 30-day retention)

---

### 6.6 Troubleshooting Guide

#### Issue: "File not found" Error

**Symptoms:** Engine fails with "Cannot read source file"

**Causes:**
1. TelemetryLoader didn't write files (check loader logs)
2. File permissions issue
3. Path resolution incorrect

**Resolution:**
```bash
# Check temp directory
ls -la /tmp/flowtime/runs/{runId}/telemetry/

# Check loader logs
grep "TelemetryLoader" /var/log/flowtime/app.log

# Verify file paths in manifest
cat /tmp/flowtime/runs/{runId}/telemetry/manifest.json
```

---

#### Issue: "Conservation Violated" Warning

**Symptoms:** Warning in run.json about conservation

**Causes:**
1. Telemetry data quality issue
2. Late arrivals in wrong bin
3. Retry logic counted multiple times

**Resolution:**
```bash
# Check raw telemetry in ADX
NodeTimeBin
| where node == "OrderQueue"
| where ts == datetime(2025-10-07T14:00:00Z)
| project ts, arrivals, served, queue_depth

# Check for duplicate events
customEvents
| where timestamp between (datetime(...) .. datetime(...))
| where node == "OrderQueue"
| summarize count() by messageId
| where count_ > 1

# Adjust tolerance if acceptable:
validation:
  - type: conservation
    tolerance: 5.0  # Increased from 0.01
```

---

#### Issue: "Window Not Aligned" Error

**Symptoms:** 400 Bad Request "window.start must align to bin boundary"

**Causes:** Timestamp not multiple of binSize

**Resolution:**
```python
# Python example: Round to nearest bin
from datetime import datetime, timedelta

def align_to_bin(ts, bin_minutes):
    epoch = datetime(1970, 1, 1)
    minutes_since_epoch = (ts - epoch).total_seconds() / 60
    aligned_minutes = (minutes_since_epoch // bin_minutes) * bin_minutes
    return epoch + timedelta(minutes=aligned_minutes)

# Example
ts = datetime(2025, 10, 7, 14, 3, 27)  # 14:03:27
aligned = align_to_bin(ts, 5)           # 14:00:00
```

---

#### Issue: Inferred Capacity Seems Wrong

**Symptoms:** Capacity inferred as 150 but user knows it's 200

**Causes:**
1. Service never hit capacity in telemetry window
2. Inference algorithm limitation

**Resolution:**
```yaml
# Option 1: Provide explicit capacity in overlay
overlays:
  - nodeId: capacity_explicit
    kind: const
    values: [200, 200, 200, ...]  # Repeat for all bins

topologyOverride:
  nodes:
    - id: OrderService
      semantics:
        capacity: capacity_explicit

# Option 2: Adjust inference (if available)
parameters:
  capacity_inference_method: "rolling_max"
  capacity_inference_window: 24  # Look back 24 bins
```

---

### 6.7 Future Enhancements (Post-M4)

**Priority 1 (Next 3 Months):**

1. **Overlay UI** - Visual what-if scenario builder
2. **Comparison View** - Side-by-side baseline vs scenario
3. **Capacity Suggestions** - ML-based capacity recommendations
4. **Export to Power BI** - One-click export with schema

**Priority 2 (Next 6 Months):**

5. **Multi-Class Flows** - Support flow segmentation (priority queues)
6. **Advanced Expressions** - IF/THEN, CUMSUM, CONV
7. **Template Marketplace** - Share templates across teams
8. **Real-Time Mode** - Near real-time telemetry (<1 min lag)

**Priority 3 (Future):**

9. **Distributed Evaluation** - Horizontal scaling for large models
10. **Auto-Topology Discovery** - Infer topology from telemetry
11. **Anomaly Detection** - ML-based outlier detection
12. **Cost Optimization** - Suggest capacity adjustments for cost

---

### 6.8 Related Documents

**Internal:**
- M1 Implementation Plan: `docs/milestones/M01.00.md`
- M2 Implementation Plan: `docs/milestones/M02.00.md`
- M3 Implementation Plan: `docs/milestones/M3.md`
- M4 Implementation Plan: `docs/milestones/M4.md`
- API Reference: `docs/api/openapi.yaml`
- Template Reference: `docs/templates/README.md`

**External:**
- Azure Data Explorer: https://docs.microsoft.com/en-us/azure/data-explorer/
- Kusto Query Language: https://docs.microsoft.com/en-us/azure/data-explorer/kusto/query/
- OpenTelemetry: https://opentelemetry.io/docs/
- Prometheus Metrics: https://prometheus.io/docs/concepts/metric_types/

---

### 6.9 Document History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2025-10-08 | FlowTime Architecture Team | Initial KISS architecture (6 chapters) |

---

### 6.10 Acknowledgments

**Contributors:**
- Architecture Team: System design, decision log
- Engineering Team: Technical feasibility review
- Data Team: Gold schema design, ETL guidance
- Product Team: Use case validation

**Reviewers:**
- Senior Engineers: Code review and implementation guidance
- Architects: System design review
- Junior Engineers: Documentation clarity feedback

---

## Appendix A: Quick Reference

### A.1 Common Commands

```bash
# Create run from telemetry
curl -X POST https://api.flowtime.com/v1/runs \
  -H "Content-Type: application/json" \
  -d @request.json

# Get run status
curl https://api.flowtime.com/v1/runs/{runId}

# Get graph topology
curl https://api.flowtime.com/v1/runs/{runId}/graph

# Get state at timestamp
curl https://api.flowtime.com/v1/runs/{runId}/state?ts=2025-10-07T14:00:00Z

# Get time window
curl https://api.flowtime.com/v1/runs/{runId}/state_window?start=...&end=...

# Get metrics
curl https://api.flowtime.com/v1/runs/{runId}/metrics?start=...&end=...
```

### A.2 File Formats

**CSV (Telemetry):**
```
120
135
140
...
```

**Manifest JSON:**
```json
{
  "window": { "start": "...", "end": "..." },
  "grid": { "bins": 288, "binSize": 5, "binUnit": "minutes" },
  "files": [
    { "node": "OrderService", "metric": "arrivals", "path": "...", "rows": 288 }
  ],
  "warnings": []
}
```

**Model YAML:**
```yaml
schemaVersion: 1
window: { start: "2025-10-07T00:00:00Z", timezone: "UTC" }
grid: { bins: 288, binSize: 5, binUnit: "minutes" }
topology:
  nodes: [ ... ]
nodes:
  - id: arrivals
    kind: const
    source: "file://..."
```

### A.3 Error Codes

| Code | Message | Resolution |
|------|---------|------------|
| 400 | Invalid window | Check UTC format, bin alignment |
| 404 | Template not found | List available templates |
| 502 | ADX unavailable | Check connection, retry |
| 500 | Evaluation failed | Check model syntax, logs |

---

## Appendix B: ETL Example

### B.1 Complete ETL Pipeline

```kql
// FlowTime Gold ETL Pipeline
// Runs every 5 minutes
// Aggregates App Insights + Service Bus → NodeTimeBin

.create-or-alter function FlowTimeETL() {
    
    // Configuration
    let WindowStart = ago(10m);
    let WindowEnd = ago(5m);
    let BinSize = 5m;
    
    // Step 1: Extract service events
    let ServiceEvents = 
        customEvents
        | where timestamp >= WindowStart and timestamp < WindowEnd
        | where name in ("MessageReceived", "MessageProcessed", "MessageFailed")
        | extend 
            node = tostring(customDimensions["node"]),
            messageId = tostring(customDimensions["messageId"]),
            eventType = name
        | project timestamp, node, messageId, eventType;
    
    // Step 2: Aggregate per node per bin
    let ServiceMetrics =
        ServiceEvents
        | summarize
            arrivals = countif(eventType == "MessageReceived"),
            served = countif(eventType == "MessageProcessed"),
            errors = countif(eventType == "MessageFailed")
          by node, bin_timestamp = bin(timestamp, BinSize);
    
    // Step 3: Get Service Bus metrics
    let QueueMetrics =
        AzureServiceBus_Metrics
        | where TimeGenerated >= WindowStart and TimeGenerated < WindowEnd
        | where MetricName in ("IncomingMessages", "CompletedMessages", "ActiveMessageCount")
        | summarize
            external_demand = sumif(Total, MetricName == "IncomingMessages"),
            queue_completions = sumif(Total, MetricName == "CompletedMessages"),
            queue_depth = avgif(Total, MetricName == "ActiveMessageCount")
          by QueueName, bin_timestamp = bin(TimeGenerated, BinSize);
    
    // Step 4: Map queues to nodes
    let NodeQueueMapping = datatable(node:string, inputQueue:string) [
        "OrderService", "orders-in",
        "BillingService", "billing-in"
    ];
    
    // Step 5: Join and finalize
    ServiceMetrics
    | join kind=leftouter (
        QueueMetrics
        | join kind=inner (NodeQueueMapping) on $left.QueueName == $right.inputQueue
        | project bin_timestamp, node, external_demand, queue_depth
    ) on node, bin_timestamp
    | extend
        ts = bin_timestamp,
        arrivals = coalesce(arrivals, 0),
        served = coalesce(served, 0),
        errors = coalesce(errors, 0),
        external_demand = coalesce(external_demand, arrivals),
        queue_depth = coalesce(queue_depth, 0.0),
        schema_version = "2.0",
        extraction_ts = now(),
        known_data_gaps = ""
    | project 
        ts, node, 
        arrivals, served, errors, 
        external_demand, queue_depth,
        schema_version, extraction_ts, known_data_gaps
}

// Schedule ETL
.alter table NodeTimeBin policy update
@'[{"Source": "FlowTimeETL", "Query": "FlowTimeETL()", "IsEnabled": true, "IsTransactional": true}]'
```

---

## Appendix C: Complete Example Run

### C.1 Request

```json
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
    "q0": 0,
    "enable_capacity_inference": true
  }
}
```

### C.2 Response

```json
{
  "runId": "run_20251007T000000Z_abc123",
  "status": "completed",
  "mode": "telemetry",
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
      { "from": "OrderService:out", "to": "OrderQueue:in" }
    ]
  },
  "warnings": [
    {
      "type": "data_gap",
      "bins": [12, 13],
      "message": "Missing telemetry 14:00-14:10 UTC (zero-filled)"
    }
  ],
  "provenance": {
    "model": {
      "source": "template",
      "template": {
        "name": "order-system",
        "version": "1.0"
      },
      "parameters": {
        "q0": 0,
        "enable_capacity_inference": true
      }
    },
    "telemetry": {
      "extraction_ts": "2025-10-07T14:30:00Z",
      "source": "adx://cluster.region.kusto.windows.net/Telemetry",
      "loader_version": "2.0.0"
    },
    "engine": {
      "version": "2.1.0",
      "evaluation_time_ms": 450
    }
  },
  "artifacts": {
    "root": "https://storage.blob.core.windows.net/flowtime/runs/run_20251007T000000Z_abc123/",
    "run_json": ".../run.json",
    "model_yaml": ".../model.yaml",
    "topology_json": ".../topology.json"
  },
  "createdAt": "2025-10-07T14:30:00Z",
  "completedAt": "2025-10-07T14:30:45Z"
}
```

---

**End of Document**

This completes the FlowTime KISS Architecture specification across all 6 chapters. The document provides comprehensive guidance for architects, senior engineers, and junior engineers to implement the capacity-free, telemetry-as-files, template-based architecture.
