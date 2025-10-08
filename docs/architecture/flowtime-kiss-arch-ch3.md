# FlowTime Architecture: KISS Approach - Chapter 3

## Chapter 3: Component Design

This chapter details the design of each component: TelemetryLoader, Template System, Engine extensions, and integration patterns.

---

### 3.1 TelemetryLoader

#### 3.1.1 Overview

**Purpose:** Extract telemetry from Azure Data Explorer and write to flat files for Engine consumption.

**Design Philosophy:** 
- Stateless utility (no internal state)
- Single responsibility (query → files)
- Fail-fast validation
- Comprehensive logging

**Location:** `src/FlowTime.Telemetry/Loader/`

**Estimated Size:** ~200-300 lines of code

#### 3.1.2 Public Interface

```
TelemetryLoader.Load(request: LoadRequest) → LoadResult

LoadRequest:
  - window: TimeWindow (start, bins, binSize, binUnit)
  - selection: NodeSelection (nodes[], includeQueues)
  - outputDir: string (path for CSV files)
  - options: LoadOptions (zeroFill, gapWarning, checksums)

LoadResult:
  - success: boolean
  - manifest: TelemetryManifest
  - warnings: Warning[]
  - errors: Error[]
```

#### 3.1.3 Execution Flow

**Phase 1: Validation**
```
1. Validate window:
   - start is UTC (ends with 'Z')
   - start aligns to bin boundary
   - bins > 0 and bins <= 10000
   - binSize > 0 and binSize <= 1000
   - binUnit in {minutes, hours, days, weeks}

2. Validate selection:
   - nodes list not empty
   - node names match pattern: [a-zA-Z0-9_-]+

3. Validate output:
   - outputDir exists or can be created
   - Write permissions available
```

**Phase 2: Query Construction**
```
Build KQL query:
  let startTime = datetime({window.start});
  let endTime = startTime + {bins} * {binMinutes}m;
  let selectedNodes = dynamic([{node1}, {node2}, ...]);
  
  NodeTimeBin
  | where ts >= startTime and ts < endTime
  | where node in (selectedNodes)
  | project ts, node, arrivals, served, errors, 
            external_demand, queue_depth,
            schema_version, extraction_ts, known_data_gaps
  | order by node asc, ts asc
```

**Phase 3: Execution**
```
1. Connect to ADX:
   - Use connection string from config
   - Apply retry policy (3 attempts, exponential backoff)
   - Timeout: 30s for queries <1000 bins

2. Execute query:
   - Stream results to avoid memory pressure
   - Track progress (rows processed)
   - Detect early termination or throttling

3. Handle errors:
   - Network failures → retry with backoff
   - Authorization errors → fail immediately
   - Query timeout → adjust window or fail
```

**Phase 4: Aggregation and Dense Filling**
```
For each node in selection:
  1. Group results by (node, ts)
  
  2. Create dense bin array:
     expectedBins = [start + i*binMinutes for i in 0..bins)
     actualBins = set(results.ts)
     missingBins = expectedBins - actualBins
  
  3. Fill strategy:
     If options.zeroFill:
       For each missing bin: insert 0 for all metrics
       Add warning: "Zero-filled bins: {missingBins}"
     Else:
       For each missing bin: insert NaN
       Add warning: "Missing bins: {missingBins}"
  
  4. Validate length:
     Assert len(dense_array) == grid.bins
```

**Phase 5: File Writing**
```
For each metric (arrivals, served, errors, queue_depth):
  For each node:
    filename = "{outputDir}/{node}_{metric}.csv"
    
    Open file for writing:
      For bin in 0..bins:
        Write value[bin] as string + newline
      
      If options.checksums:
        Compute SHA-256 of file
        Store in manifest
```

**Phase 6: Manifest Generation**
```
Create TelemetryManifest:
  window: { start, end, timezone }
  grid: { bins, binSize, binUnit }
  files: [
    { node, metric, path, rows, checksum }
    for each written file
  ]
  warnings: [ all accumulated warnings ]
  provenance: {
    extraction_ts: now(),
    source: ADX connection string,
    loader_version: "2.0.0"
  }

Write manifest.json to outputDir
```

#### 3.1.4 Error Handling

**Query Failures:**
```
Error Type: ADX query timeout
Action: 
  - Log query and window size
  - Suggest: Reduce window or split into chunks
  - Return error: "Query timeout after 30s"

Error Type: ADX throttling
Action:
  - Log throttle response
  - Retry with exponential backoff (up to 3 times)
  - If still failing: Return error with retry advice

Error Type: Node not found in Gold
Action:
  - Log which nodes were found vs requested
  - Return warning for missing nodes
  - Continue with available nodes
  - If no nodes found: Return error
```

**Data Quality Issues:**
```
Issue: Large gap (>15 minutes)
Action:
  - Add warning with gap details
  - If options.zeroFill: Fill with zeros
  - Else: Fill with NaN

Issue: Negative values (arrivals, served, errors)
Action:
  - Add warning with bin indices
  - Clamp to 0
  - Continue processing

Issue: served > arrivals (conservation violation)
Action:
  - Add warning (not error)
  - Keep actual values (Engine validates later)
```

#### 3.1.5 Configuration

**Connection String:**
```json
{
  "adx": {
    "cluster": "https://cluster.region.kusto.windows.net",
    "database": "Telemetry",
    "auth": {
      "type": "ManagedIdentity|ServicePrincipal|AzureCLI",
      "clientId": "...",
      "clientSecret": "...",
      "tenantId": "..."
    }
  }
}
```

**Loader Options:**
```json
{
  "telemetryLoader": {
    "zeroFill": true,
    "checksums": true,
    "maxBins": 10000,
    "queryTimeout": 30,
    "retryCount": 3,
    "gapWarningThreshold": 15
  }
}
```

#### 3.1.6 Testing Strategy

**Unit Tests:**
```
Test: ValidateWindow_ValidInput_ReturnsSuccess
  Input: { start: "2025-10-07T00:00:00Z", bins: 288, ... }
  Expected: Validation passes

Test: ValidateWindow_NonUTC_ReturnsError
  Input: { start: "2025-10-07T00:00:00" }  // Missing Z
  Expected: Error "window.start must be UTC"

Test: ValidateWindow_MisalignedStart_ReturnsError
  Input: { start: "2025-10-07T00:03:27Z", binSize: 5 }
  Expected: Error "window.start must align to bin boundary"

Test: DenseFill_MissingBins_FillsWithZeros
  Input: Results missing bins [12, 13, 14]
  Options: { zeroFill: true }
  Expected: Array[12] = 0, Array[13] = 0, Array[14] = 0
  Warnings: "Zero-filled bins: 12-14"
```

**Integration Tests:**
```
Test: LoadFromRealADX_ValidWindow_ProducesFiles
  Setup: Mock ADX with fixture data
  Input: { window: {...}, nodes: ["OrderService"] }
  Expected:
    - OrderService_arrivals.csv exists
    - OrderService_served.csv exists
    - manifest.json exists
    - All files have correct row count

Test: LoadFromRealADX_MissingNode_ReturnsWarning
  Setup: Mock ADX without "NonExistentService"
  Input: { nodes: ["OrderService", "NonExistentService"] }
  Expected:
    - OrderService files created
    - Warning: "Node 'NonExistentService' not found in Gold"
```

**Golden Tests:**
```
Test: Load_FixedInput_ProducesConsistentOutput
  Setup: ADX fixture with known data
  Input: Fixed window and nodes
  Expected: Output files match golden CSVs byte-for-byte
  Purpose: Regression detection
```

---

### 3.2 Template System

#### 3.2.1 Overview

**Purpose:** Define reusable system topologies and expression formulas.

**Design Philosophy:**
- Declarative YAML (no code)
- Version-controlled (Git)
- Parameterized for flexibility
- Self-documenting

**Location:** `templates/`

**Structure:**
```
templates/
  telemetry/
    order-system.yaml
    billing-system.yaml
    ...
  simulation/
    microservices.yaml
    retail-flow.yaml
    ...
  shared/
    common-topology.yaml
    standard-expressions.yaml
```

#### 3.2.2 Template Schema

**Top-Level Fields:**
```yaml
templateType: "telemetry|simulation"
version: "1.0"
description: "Human-readable description"

parameters:
  - name: "param_name"
    type: "string|number|boolean|enum"
    default: value
    required: true|false
    description: "What this parameter controls"

includes:
  - path: "shared/common-topology.yaml"
  - path: "shared/standard-expressions.yaml"

topology:
  nodes: [ ... ]
  edges: [ ... ]

expressions:
  - id: "series_id"
    kind: "const|expr|pmf"
    ...

validation:
  - type: "conservation|capacity_check|..."
    ...

outputs:
  - series: "..."
    as: "..."
```

#### 3.2.3 Parameter System

**Parameter Types:**
```yaml
parameters:
  # String parameter
  - name: data_source
    type: string
    default: "telemetry"
    allowed: ["telemetry", "simulation"]
  
  # Number parameter
  - name: q0
    type: number
    default: 0
    min: 0
    max: 1000
  
  # Boolean parameter
  - name: enable_capacity_inference
    type: boolean
    default: true
  
  # Enum parameter
  - name: bin_size
    type: enum
    values: [1, 5, 15, 60]
    default: 5
```

**Parameter Substitution:**
```yaml
# In template:
expressions:
  - id: queue_depth
    kind: expr
    expr: "MAX(0, SHIFT(queue_depth, 1) + arrivals - served)"
    initial: "{{q0}}"  # Parameter reference

# After instantiation (q0=25):
expressions:
  - id: queue_depth
    kind: expr
    expr: "MAX(0, SHIFT(queue_depth, 1) + arrivals - served)"
    initial: 25  # Substituted value
```

**Special Parameters:**
```yaml
# Reserved parameters (auto-provided):
{{window.start}}         # From API request
{{window.bins}}
{{grid.binSize}}
{{grid.binUnit}}
{{telemetry_dir}}        # Path to extracted telemetry
{{output_dir}}           # Path for artifacts
```

#### 3.2.4 Template Instantiation

**Process:**
```
1. Load template file (YAML parse)

2. Resolve includes:
   - Load referenced templates
   - Merge topology nodes (union)
   - Merge expressions (later definitions override)

3. Validate parameters:
   - Check all required parameters provided
   - Validate types and ranges
   - Apply defaults for missing optional parameters

4. Substitute parameters:
   - Walk template tree (topology, expressions)
   - Replace {{param_name}} with values
   - Evaluate nested references ({{params.q0}})

5. Validate topology:
   - Node ID uniqueness
   - Edge references valid
   - Kind-specific requirements met

6. Validate expressions:
   - All referenced node IDs exist
   - No cycles (except SHIFT self-ref)
   - Self-referencing SHIFT has initial

7. Generate model.yaml:
   - Write schemaVersion: 1
   - Write window (from request)
   - Write grid (from request)
   - Write topology (instantiated)
   - Write nodes (instantiated expressions)
   - Write outputs
   - Write provenance (template name, version, parameters)
```

#### 3.2.5 Example Templates

**Telemetry Template (Queue-Based Service):**
```yaml
templateType: "telemetry"
version: "1.0"
description: "Order processing service with upstream queue"

parameters:
  - name: telemetry_dir
    type: string
    required: true
  
  - name: q0
    type: number
    default: 0
    description: "Initial queue depth"
  
  - name: enable_capacity_inference
    type: boolean
    default: true

topology:
  nodes:
    - id: "OrderService"
      kind: "service"
      group: "Orders"
      ui: { x: 120, y: 260 }
      semantics:
        arrivals: "orders_arrivals"
        served: "orders_served"
        errors: "orders_errors"
        external_demand: "orders_demand"
        capacity: "{{#if enable_capacity_inference}}capacity_inferred{{else}}null{{/if}}"
    
    - id: "OrderQueue"
      kind: "queue"
      group: "Orders"
      ui: { x: 340, y: 260 }
      semantics:
        arrivals: "queue_inflow"
        served: "queue_outflow"
        queue: "queue_depth"
        q0: "{{q0}}"
  
  edges:
    - from: "OrderService:out"
      to: "OrderQueue:in"

expressions:
  # Load telemetry
  - id: orders_arrivals
    kind: const
    source: "file://{{telemetry_dir}}/OrderService_arrivals.csv"
  
  - id: orders_served
    kind: const
    source: "file://{{telemetry_dir}}/OrderService_served.csv"
  
  - id: orders_errors
    kind: const
    source: "file://{{telemetry_dir}}/OrderService_errors.csv"
  
  - id: orders_demand
    kind: const
    source: "file://{{telemetry_dir}}/OrderService_demand.csv"
  
  - id: queue_inflow
    kind: const
    source: "file://{{telemetry_dir}}/OrderQueue_inflow.csv"
  
  - id: queue_outflow
    kind: const
    source: "file://{{telemetry_dir}}/OrderQueue_outflow.csv"
  
  # Compute queue depth
  - id: queue_depth
    kind: expr
    expr: "MAX(0, SHIFT(queue_depth, 1) + queue_inflow - queue_outflow)"
    initial: "{{q0}}"
  
  # Optional capacity inference
  - id: capacity_inferred
    kind: expr
    expr: "infer_capacity_from_saturation(orders_served, orders_demand, queue_depth)"
    enabled: "{{enable_capacity_inference}}"

validation:
  - type: conservation
    node: OrderQueue
    formula: "queue_inflow - queue_outflow - (queue_depth[t] - queue_depth[t-1])"
    tolerance: 0.01

outputs:
  - series: "*"
    exclude: ["temp_*"]
```

**Simulation Template (Stochastic):**
```yaml
templateType: "simulation"
version: "1.0"
description: "Stochastic order processing model"

parameters:
  - name: arrival_rate
    type: number
    default: 150
    min: 1
    max: 1000
  
  - name: capacity
    type: number
    default: 200
  
  - name: q0
    type: number
    default: 0

topology:
  nodes:
    - id: "OrderService"
      kind: "service"
      group: "Orders"
      ui: { x: 120, y: 260 }
      semantics:
        arrivals: "orders_arrivals"
        served: "orders_served"
        capacity: "orders_capacity"

expressions:
  # Stochastic arrivals
  - id: orders_arrivals
    kind: pmf
    pmf:
      type: poisson
      lambda: "{{arrival_rate}}"
  
  # Fixed capacity
  - id: orders_capacity
    kind: const
    values: ["{{capacity}}" for _ in range(bins)]  # Repeat capacity
  
  # Served = MIN(arrivals, capacity)
  - id: orders_served
    kind: expr
    expr: "MIN(orders_arrivals, orders_capacity)"
```

#### 3.2.6 Template Validation

**Syntax Validation:**
```
- YAML is well-formed
- Required top-level fields present
- Parameters have valid types
- Includes point to existing files
```

**Semantic Validation:**
```
- Topology nodes have unique IDs
- Edges reference existing nodes
- Expressions reference existing series or parameters
- No cycles (except allowed SHIFT self-ref)
- Kind-specific requirements met (e.g., queue has 'queue' semantic)
```

**Parameter Validation:**
```
- All required parameters have defaults or are marked required
- Enum values are from allowed list
- Number parameters within min/max ranges
- Boolean parameters are true/false
```

#### 3.2.7 Template Versioning

**Version Changes:**
```
Major (1.x → 2.x): Breaking changes (remove parameters, change topology)
Minor (1.1 → 1.2): Additive changes (new parameters, new expressions)
Patch (1.1.0 → 1.1.1): Bug fixes (no schema changes)
```

**Compatibility:**
```
Engine checks template version:
  If template.version > engine.maxSupportedVersion:
    Error: "Template requires engine version {template.version}"
  
  If template.version < engine.minSupportedVersion:
    Warning: "Template is deprecated, consider upgrading"
```

---

### 3.3 Engine Extensions

#### 3.3.1 File Source Support

**New Feature:** const nodes can load from files

**Implementation:**
```
Node Schema:
  kind: const
  source: "file://path/to/data.csv"  # NEW
  values: [...]                       # Existing (for inline)

Parser Logic:
  If source is present:
    1. Resolve file path (relative to model directory)
    2. Check file exists and is readable
    3. Load file contents
    4. Parse based on extension:
       - .csv → CSV parser (single column, no header)
       - .json → JSON parser ({ values: [...] })
    5. Validate length == grid.bins
    6. Store as values array
  Else if values is present:
    Use inline values
  Else:
    Error: "const node must have 'source' or 'values'"
```

**File Path Resolution:**
```
source: "file://relative/path.csv"
  → Resolve relative to model.yaml directory

source: "file:///absolute/path.csv"
  → Use absolute path (three slashes)

source: "https://storage.blob.core.windows.net/..."
  → Future: HTTP source (not M1)
```

#### 3.3.2 Initial Condition Enforcement

**New Validation Rule:**

```
During Expression Parsing:
  If expr contains SHIFT(node_id, k) where k > 0:
    If node_id == current_node.id:  // Self-reference
      If initial field is absent:
        Error: "Self-referencing SHIFT requires explicit 'initial' value"
      Else:
        Store initial value for evaluation

During Evaluation:
  For SHIFT(x, k)[t]:
    If t-k < 0:  // Out of range
      Return initial value (or 0 if non-self-reference)
```

**Initial Value Types:**
```yaml
# Scalar
initial: 5

# Reference to another series (uses series[0])
initial: "q0_from_telemetry"

# Expression (evaluated once before time loop)
initial: "AVG(queue_depth_gold[0:10])"  # Future
```

#### 3.3.3 Capacity Inference Functions

**New Built-In Function:**
```
infer_capacity_from_saturation(served, external_demand, queue_depth)

Logic:
  For each bin t:
    If queue_depth[t] > 10 AND external_demand[t] > served[t]:
      capacity[t] = served[t]  # Saturated
    Else:
      capacity[t] = ROLLING_MAX(served, window=12)[t]  # Historical max
  
  Return capacity series
```

**Alternative Functions (future):**
```
infer_capacity_from_throughput(served, window)
  → Rolling maximum of served over window

infer_capacity_from_config(replicas, per_replica_rate)
  → replicas * per_replica_rate

infer_capacity_from_percentile(served, percentile)
  → P95 or P99 of served when demand > 0
```

#### 3.3.4 Post-Evaluation Validation

**New Validation Runner:**

```
After Model Evaluation:
  For each validation in model.validation:
    Execute validation check
    Collect results (pass/fail/warning)
    Append to run.json warnings section

Validation Types:
  1. Conservation:
     formula: "arrivals - served - (queue[t] - queue[t-1])"
     tolerance: 0.01
     For each bin: compute residual
     If |residual| > tolerance: add warning
  
  2. Capacity Check:
     formula: "served <= capacity"
     For each bin: check inequality
     If violated: add warning
  
  3. Custom:
     formula: "user-defined expression"
     expected: true|false
     For each bin: evaluate formula
     If result != expected: add warning
```

**Warning Structure:**
```json
{
  "type": "conservation_violation",
  "validation_id": "conservation_OrderQueue",
  "node": "OrderQueue",
  "bins_violated": [45, 67, 123],
  "worst_residual": 2.3,
  "message": "Flow conservation violated at 3 bins (worst: 2.3 units)"
}
```

#### 3.3.5 Provenance Tracking

**Enhanced run.json:**
```json
{
  "runId": "...",
  "status": "completed",
  "grid": { ... },
  "window": { ... },
  "mode": "telemetry|model",
  
  "provenance": {
    "model": {
      "source": "template|inline|file",
      "template": {
        "name": "order-system",
        "version": "1.0",
        "path": "templates/telemetry/order-system.yaml"
      },
      "parameters": {
        "q0": 0,
        "enable_capacity_inference": true
      }
    },
    "telemetry": {
      "extraction_ts": "2025-10-07T14:30:00Z",
      "source": "adx://cluster.region.kusto.windows.net/database",
      "loader_version": "2.0.0",
      "files": [
        {
          "node": "OrderService",
          "metric": "arrivals",
          "rows": 288,
          "checksum": "sha256:..."
        }
      ]
    },
    "engine": {
      "version": "2.1.0",
      "evaluation_time_ms": 450,
      "series_count": 12,
      "expression_count": 8
    }
  },
  
  "warnings": [ ... ],
  
  "artifacts": { ... }
}
```

---

### 3.4 Integration Patterns

#### 3.4.1 API Orchestration Flow

**POST /v1/runs (Telemetry Mode):**

```
Request arrives → API Controller

1. Validate request:
   - Check template exists
   - Validate window (UTC, aligned)
   - Validate selection (nodes not empty)

2. Load template:
   - Read template YAML from disk
   - Resolve includes
   - Validate syntax

3. Extract telemetry:
   - Call TelemetryLoader.Load(request)
   - Check success
   - Log warnings from loader

4. Instantiate model:
   - Merge template + parameters
   - Substitute {{vars}}
   - Validate generated model

5. Evaluate model:
   - Call Engine.Evaluate(model)
   - Write artifacts to storage
   - Generate run.json

6. Return response:
   - runId
   - status
   - artifacts URLs
   - warnings
```

**Error Handling:**
```
Step 1 fails → 400 Bad Request
Step 2 fails → 404 Template Not Found or 500 Template Invalid
Step 3 fails → 502 Telemetry Unavailable (ADX down) or 500 Loader Error
Step 4 fails → 500 Model Generation Failed
Step 5 fails → 500 Evaluation Failed
```

#### 3.4.2 File System Layout

**Temporary Telemetry:**
```
/tmp/flowtime/telemetry/{request_id}/
  manifest.json
  OrderService_arrivals.csv
  OrderService_served.csv
  OrderService_errors.csv
  ...

Cleanup: Delete after run completes (success or failure)
TTL: 1 hour (in case of orphaned requests)
```

**Persistent Artifacts:**
```
/data/flowtime/runs/{runId}/
  run.json
  model.yaml
  topology.json
  series/
    orders_arrivals.csv
    orders_served.csv
    queue_depth.csv
    ...
  debug/
    dag.json
    evaluation.log

Retention: 30 days default, configurable
```

**Templates:**
```
/app/templates/
  telemetry/
    order-system.yaml
    billing-system.yaml
  simulation/
    microservices.yaml
  shared/
    common-topology.yaml

Source: Baked into Docker image or mounted from Git repo
```

#### 3.4.3 Caching Strategy

**Template Caching:**
```
In-Memory Cache:
  Key: template_name + version
  Value: Parsed YAML object
  TTL: Indefinite (templates rarely change)
  Invalidation: On file modification (watch filesystem)
```

**Telemetry Caching:**
```
Shared Cache (Redis or in-memory):
  Key: hash(window + nodes + extraction_ts_bucket)
  Value: Telemetry manifest + file paths
  TTL: 15 minutes
  
  Rationale: Multiple users may request same window
  
  Example:
    User A requests: 2025-10-07, nodes=[OrderService]
    User B requests: 2025-10-07, nodes=[OrderService, BillingService]
    → Cache hit for OrderService, miss for BillingService
```

**Artifact Caching:**
```
No caching (artifacts are immutable and persistent)
Use CDN or blob storage caching for /series/*.csv downloads
```

#### 3.4.4 Async Execution Pattern

**For Long-Running Evaluations:**

```
POST /v1/runs → 202 Accepted (immediate return)

Background Worker:
  1. Dequeue run request
  2. Execute telemetry load
  3. Execute model evaluation
  4. Write artifacts
  5. Update run status → "completed" or "failed"

Client Polling:
  GET /v1/runs/{runId}
  → Check status field
  → If "completed": access artifacts
  → If "failed": read error details
```

**Worker Queue:**
```
Technology: Azure Service Bus or Redis Queue
Message: { runId, template, window, selection, parameters }
Worker Pool: 5-10 workers (configurable)
Timeout: 5 minutes per run (configurable)
Retry: 3 attempts with exponential backoff
```

#### 3.4.5 Monitoring and Observability

**Metrics to Emit:**
```
- telemetry_load_duration_ms (histogram)
- telemetry_load_row_count (counter)
- telemetry_load_errors (counter)
- model_evaluation_duration_ms (histogram)
- model_evaluation_series_count (histogram)
- run_creation_total (counter by status)
- run_artifacts_size_bytes (histogram)
```

**Logs to Capture:**
```
- TelemetryLoader: Query KQL, result row counts, warnings
- ModelBuilder: Template loaded, parameters applied
- Engine: DAG construction time, evaluation time, warnings
- API: Request/response, errors, user context
```

**Traces (OpenTelemetry):**
```
Span: POST /v1/runs
  |-- Span: Load Template
  |-- Span: Extract Telemetry
       |-- Span: ADX Query
       |-- Span: Dense Fill
       |-- Span: Write CSVs
  |-- Span: Instantiate Model
  |-- Span: Evaluate Model
       |-- Span: Parse
       |-- Span: Build DAG
       |-- Span: Evaluate Series
  |-- Span: Write Artifacts
```

---

**End of Chapter 3**

Continue to Chapter 4 for Data Flows and Examples.
