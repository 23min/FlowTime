# FlowTime-Sim Time-Travel Readiness Audit

**Repository:** flowtime-sim-vnext  
**Audit Date:** October 9, 2025  
**Auditor:** AI Assistant  
**Reference Architecture:** FlowTime KISS Architecture (Chapters 1-6)

---

## Executive Summary

### Current Status: ⚠️ **PARTIALLY READY**

FlowTime-Sim generates Engine-compatible models for **simulation-only** use cases (M2.10 compatibility). However, it **does NOT generate models** with the schema extensions required for **time-travel functionality** (M3.0+).

**Key Finding:** FlowTime-Sim's current template and model generation system is missing critical schema sections (window, topology, semantics) that are required by the KISS architecture for time-travel APIs, telemetry replay, and state snapshots.

### Compatibility Matrix

| Feature | M2.10 (Current) | M3.0+ (Time-Travel) | FlowTime-Sim Status |
|---------|----------------|---------------------|---------------------|
| **Pure Simulation** | ✅ Supported | ✅ Supported | ✅ **READY** |
| **Expression Evaluation** | ✅ Supported | ✅ Supported | ✅ **READY** |
| **PMF Stochastic Modeling** | ✅ Supported | ✅ Supported | ✅ **READY** |
| **Absolute Time (window.start)** | ❌ Not Required | ✅ Required | ❌ **NOT READY** |
| **Topology Section** | ❌ Not Required | ✅ Required | ❌ **NOT READY** |
| **Semantics Mapping** | ❌ Not Required | ✅ Required | ❌ **NOT READY** |
| **Node Kinds** | ❌ Not Required | ✅ Required | ❌ **NOT READY** |
| **Initial Conditions** | ⚠️ Optional | ✅ Required | ⚠️ **PARTIAL** |
| **File Sources (telemetry)** | ⚠️ Optional | ✅ Required | ⚠️ **PARTIAL** |

---

## 1. Current State Assessment

### 1.1 What Works Well ✅

#### Model Generation System
- **Template-based generation**: Metadata-driven templates with parameter substitution
- **Node-based schema**: const, expr, pmf nodes for series definition
- **Grid configuration**: bins, binSize, binUnit correctly implemented
- **RNG support**: Deterministic simulation with PCG32 RNG
- **Provenance tracking**: Schema version and generation metadata
- **Output control**: Selective artifact generation via outputs section

**Example of Current Output:**
```yaml
schemaVersion: 1

grid:
  bins: 6
  binSize: 60
  binUnit: minutes

nodes:
  - id: passenger_demand
    kind: const
    values: [10, 15, 20, 25, 18, 12]
  
  - id: passengers_served
    kind: expr
    expr: "MIN(passenger_demand, vehicle_capacity)"

outputs:
  - id: passenger_demand
    series: passenger_demand
    as: demand.csv
```

**Assessment:** This works perfectly for **simulation-only** models targeting Engine M2.10.

#### Template Library
- **5 Templates Available:**
  1. `transportation-basic` - Simple vehicle capacity system
  2. `manufacturing-line` - Multi-stage production pipeline
  3. `it-system-microservices` - Modern web application with services
  4. `supply-chain-multi-tier` - Multi-tier supply chain
  5. `network-reliability` - Network with node failures

- **Parameter System:**
  - Type validation (integer, array, etc.)
  - Default values
  - Min/max constraints
  - Metadata (title, description, tags)

**Assessment:** Template system is solid and extensible.

#### Code Quality
- **Clean separation:** Template parsing, parameter substitution, schema conversion
- **Error handling:** Clear exceptions (TemplateParsingException, ParameterSubstitutionException)
- **Logging:** Comprehensive logging for debugging
- **Testing:** Unit tests for template service

**Assessment:** Well-architected, ready for extension.

---

### 1.2 Critical Gaps ❌

#### Gap 1: No Window Section
**KISS Requirement (Ch2, §2.2.1):**
```yaml
window:
  start: "2025-10-07T00:00:00Z"  # Absolute time anchor
  timezone: "UTC"                # Must be UTC
```

**Current State:**
- Grid has optional `start` field: `grid.start: "2025-01-01T00:00:00Z"`
- But NOT in separate `window` section
- Not consistently used across templates

**Impact:**
- ❌ Cannot compute absolute timestamps for bins
- ❌ /state API cannot return `bin.startUtc` and `bin.endUtc`
- ❌ Time-travel scrubber cannot show real timestamps
- ❌ Telemetry cannot be aligned to absolute time

**Required For:**
- M3.0: Foundation (Window support)
- M3.1: /state APIs (timestamp computation)

---

#### Gap 2: No Topology Section
**KISS Requirement (Ch2, §2.2.2):**
```yaml
topology:
  nodes:
    - id: "OrderService"
      kind: "service"              # service|queue|router|external
      group: "Orders"              # Logical grouping
      semantics:
        arrivals: "orders_arrivals"
        served: "orders_served"
        errors: "orders_errors"
        capacity: "orders_capacity"
  
  edges:
    - from: "OrderService:out"
      to: "OrderQueue:in"
      weight: 1.0
```

**Current State:**
- ❌ No topology section at all
- ❌ No node classification (kind)
- ❌ No edges between nodes
- ❌ No UI hints for layout (x, y coordinates)

**Impact:**
- ❌ /state API cannot identify which series represents arrivals vs served
- ❌ UI cannot distinguish services from queues
- ❌ Node coloring rules cannot be applied (no kind)
- ❌ No visual graph topology

**Required For:**
- M3.0: Foundation (Topology support)
- M3.1: /state APIs (semantics mapping, derived metrics)

---

#### Gap 3: No Semantics Mapping
**KISS Requirement (Ch2, §2.2.2):**
```yaml
semantics:
  arrivals: "orders_arrivals"      # Which series is arrivals?
  served: "orders_served"          # Which series is served?
  errors: "orders_errors"          # Which series is errors?
  capacity: "orders_capacity"      # Which series is capacity?
  queue: "queue_depth"             # Which series is queue depth?
```

**Current State:**
- ❌ No semantic mapping at all
- ❌ Series IDs are arbitrary (e.g., "passenger_demand", "passengers_served")
- ❌ No standard naming convention

**Impact:**
- ❌ /state API cannot compute utilization (needs capacity)
- ❌ /state API cannot compute latency via Little's Law (needs queue)
- ❌ Cannot distinguish arrivals from served without parsing series names

**Required For:**
- M3.1: /state APIs (derived metrics)
- M3.2: TelemetryLoader (map Gold columns to semantics)

---

#### Gap 4: No Node Kinds
**KISS Requirement (Ch2, §2.2.2):**
```yaml
kind: "service"  # Enum: service | queue | router | external
```

**Current State:**
- ❌ All nodes are untyped (just const/expr/pmf)
- ❌ No distinction between services and queues

**Impact:**
- ❌ Node coloring cannot be applied (services use utilization, queues use latency)
- ❌ Validation cannot enforce kind-specific requirements
- ❌ UI cannot show different icons for services vs queues

**Required For:**
- M3.1: /state APIs (node-specific derived metrics)

---

### 1.3 Medium Gaps ⚠️

#### Gap 5: Initial Conditions Not Enforced
**KISS Requirement (Ch2, §2.2.3):**
```yaml
- id: queue_depth
  kind: expr
  expr: "MAX(0, SHIFT(queue_depth, 1) + inflow - outflow)"
  initial: 0  # REQUIRED for self-referencing SHIFT
```

**Current State:**
- ⚠️ SHIFT operator exists in Engine
- ⚠️ Templates do NOT consistently use `initial` field
- ⚠️ No validation for self-referencing SHIFT

**Impact:**
- ⚠️ Stateful nodes may have undefined behavior at bin 0
- ⚠️ Queue models may produce incorrect results

**Example from Current Templates:**
```yaml
# transportation-basic.yaml does NOT have initial conditions
# (no stateful nodes, so not a problem for this template)
```

**Required For:**
- M3.0: Foundation (Initial condition validation)
- M3.3: Validation framework

---

#### Gap 6: File Sources Not Consistent
**KISS Requirement (Ch2, §2.3.1):**
```yaml
- id: arrivals
  kind: const
  source: "file://telemetry/arrivals.csv"  # file: URI scheme
```

**Current State:**
- ⚠️ Templates use inline `values: [...]` arrays
- ⚠️ No templates use `source: "file://..."` pattern
- ⚠️ File sources are supported by Engine, but not used by Sim templates

**Impact:**
- ⚠️ Cannot generate telemetry-replay templates
- ⚠️ All data must be inline (not scalable for large models)

**Required For:**
- M3.2: TelemetryLoader (templates reference CSV files)

---

#### Gap 7: No Edges Defined
**KISS Requirement (Ch2, §2.2.2):**
```yaml
topology:
  edges:
    - from: "OrderService:out"
      to: "OrderQueue:in"
      weight: 1.0
```

**Current State:**
- ❌ No edges at all
- ❌ Node relationships implicit in expressions only

**Impact:**
- ⚠️ UI cannot draw flow topology graph
- ⚠️ Cannot validate flow conservation across edges

**Required For:**
- M3.0: Foundation (Topology with edges)
- UI: Topology visualization

---

## 2. KISS Architecture Requirements

### 2.1 Schema Requirements from KISS Ch2

Based on FlowTime KISS Architecture (Chapter 2: Data Contracts), here are the **mandatory** schema extensions for time-travel:

#### Window Section (§2.2.1)
```yaml
window:
  start: "2025-10-07T00:00:00Z"  # ISO-8601 UTC timestamp
  timezone: "UTC"                # MUST be "UTC"
```

**Purpose:** Convert bin indices to absolute timestamps.

**Validation Rules:**
1. `start` must be ISO-8601 format
2. `start` must end with 'Z' (UTC)
3. `timezone` must be "UTC" (no other timezones supported)
4. `start` must align to bin boundary

**Usage:**
```
bin_start_time = window.start + (bin_index * grid.binSize * grid.binUnit)
```

---

#### Topology Section (§2.2.2)
```yaml
topology:
  nodes:
    - id: string              # REQUIRED: Unique identifier
      kind: enum              # REQUIRED: service|queue|router|external
      group: string           # OPTIONAL: Logical grouping for UI
      ui:                     # OPTIONAL: UI layout hints
        x: number
        y: number
      semantics:              # REQUIRED: Mapping to series IDs
        arrivals: string      # REQUIRED for service/queue
        served: string        # REQUIRED for service/queue
        errors: string        # OPTIONAL
        capacity: string      # OPTIONAL
        queue: string         # REQUIRED for kind=queue
        external_demand: string  # OPTIONAL
        latency_min: null     # NULL = engine derives
        sla_min: number       # OPTIONAL: SLA threshold
        q0: number            # OPTIONAL: Initial queue depth
  
  edges:
    - from: string            # Format: "nodeId:port"
      to: string              # Format: "nodeId:port"
      weight: number          # Default: 1.0
```

**Purpose:** Define system structure and data sources for time-travel APIs.

**Validation Rules:**
1. `topology.nodes[*].id` must be unique
2. `semantics.*` values must reference existing `nodes[*].id`
3. `kind=service` requires: arrivals, served
4. `kind=queue` requires: arrivals, served, queue
5. `edges[*].from` and `edges[*].to` must reference existing topology nodes
6. No self-loops (from = to)
7. No cycles in edge graph (DAG)

---

#### Node Kinds (§2.2.2)
```
service:  Processing node (HTTP API, worker service)
queue:    Backlog node (Service Bus, database queue)
router:   Flow splitter (load balancer, conditional routing)
external: Boundary node (external demand source)
```

**Kind-Specific Requirements:**
- `service`: MUST have arrivals, served; OPTIONAL capacity, errors
- `queue`: MUST have arrivals, served, queue; OPTIONAL q0 (initial depth)
- `router`: MUST have arrivals, served; SHOULD have edges with weights
- `external`: No requirements (boundary condition)

---

#### Initial Conditions (§2.2.3)
```yaml
- id: queue_depth
  kind: expr
  expr: "MAX(0, SHIFT(queue_depth, 1) + inflow - outflow)"
  initial: 0  # REQUIRED for self-referencing SHIFT
```

**Purpose:** Enforce conservation law at bin 0 for stateful nodes.

**Validation Rules:**
1. If `expr` contains `SHIFT(id, k)` where `k > 0` AND `id == current node id`:
   - `initial` field is REQUIRED
   - Validation ERROR if missing

---

### 2.2 File Format Requirements from KISS Ch2

#### CSV Format (§2.3.1)
```csv
120
135
140
...
```

**Schema:**
- Single column (no header, no bin_index column)
- One value per line
- Length MUST equal grid.bins
- Sparse data NOT allowed (must have all bins)

**URI Format:**
```
file:<path>
```

**Resolution Rules:**
1. Relative path → relative to model YAML directory
2. Absolute path → used as-is
3. Example: `file:fixtures/arrivals.csv` → `{model_dir}/fixtures/arrivals.csv`

---

## 3. Gap Analysis Summary

### 3.1 Critical Gaps (P0 - Blocking Time-Travel)

| Gap | KISS Requirement | Current State | Impact | Required For |
|-----|------------------|---------------|--------|--------------|
| **Window Section** | window.start, timezone | ❌ Not implemented | Cannot compute absolute timestamps | M3.0 |
| **Topology Section** | nodes, edges, semantics | ❌ Not implemented | /state APIs cannot map series to roles | M3.0 |
| **Semantics Mapping** | arrivals → series_id | ❌ Not implemented | Cannot compute derived metrics | M3.1 |
| **Node Kinds** | service\|queue\|router\|external | ❌ Not implemented | Node coloring broken | M3.1 |

**Assessment:** FlowTime-Sim **CANNOT** generate time-travel-ready models without these 4 changes.

---

### 3.2 Medium Gaps (P1 - Needed for Full Functionality)

| Gap | KISS Requirement | Current State | Impact | Required For |
|-----|------------------|---------------|--------|--------------|
| **Initial Conditions** | initial field for SHIFT | ⚠️ Partial | Stateful nodes may be incorrect at bin 0 | M3.0 |
| **File Sources** | source: "file://..." | ⚠️ Partial | Cannot reference telemetry CSVs | M3.2 |
| **Edges** | from, to, weight | ❌ Not implemented | UI cannot draw topology | M3.0 |

**Assessment:** Templates work for simulation, but missing features for telemetry integration.

---

### 3.3 Low Gaps (P2 - Nice to Have)

| Gap | KISS Requirement | Current State | Impact | Required For |
|-----|------------------|---------------|--------|--------------|
| **UI Hints** | ui.x, ui.y | ❌ Not implemented | Auto-layout required | UI |
| **Topology Validation** | Check semantic refs | ⚠️ Partial | Invalid models may pass | M3.3 |
| **Group Field** | Logical grouping | ❌ Not implemented | No visual grouping | UI |

---

## 4. Required Changes for Time-Travel Support

### 4.1 Schema Extensions (FlowTime.Sim.Core)

#### New Classes Needed

**Window.cs:**
```csharp
public class TemplateWindow
{
    public string Start { get; set; } = string.Empty;  // ISO-8601 UTC
    public string Timezone { get; set; } = "UTC";      // Always UTC
}
```

**Topology.cs:**
```csharp
public class TemplateTopology
{
    public List<TopologyNode> Nodes { get; set; } = new();
    public List<TopologyEdge> Edges { get; set; } = new();
}

public class TopologyNode
{
    public string Id { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;  // service|queue|router|external
    public string? Group { get; set; }
    public UIHint? Ui { get; set; }
    public NodeSemantics Semantics { get; set; } = new();
}

public class NodeSemantics
{
    public string? Arrivals { get; set; }
    public string? Served { get; set; }
    public string? Errors { get; set; }
    public string? Capacity { get; set; }
    public string? Queue { get; set; }
    public string? ExternalDemand { get; set; }
    public double? Q0 { get; set; }
    public double? SlaMin { get; set; }
}

public class TopologyEdge
{
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public double Weight { get; set; } = 1.0;
}

public class UIHint
{
    public int X { get; set; }
    public int Y { get; set; }
}
```

**Update Template.cs:**
```csharp
public class Template
{
    public int SchemaVersion { get; set; } = 1;
    public TemplateMetadata Metadata { get; set; } = new();
    public List<TemplateParameter> Parameters { get; set; } = new();
    public TemplateWindow? Window { get; set; } = null;  // NEW
    public TemplateGrid Grid { get; set; } = new();
    public TemplateTopology? Topology { get; set; } = null;  // NEW
    public List<TemplateNode> Nodes { get; set; } = new();
    public List<TemplateOutput> Outputs { get; set; } = new();
    public TemplateRng? Rng { get; set; } = null;
}
```

---

#### Update TemplateNode.cs
```csharp
public class TemplateNode
{
    public string Id { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    
    // For const nodes
    public double[]? Values { get; set; }
    public string? Source { get; set; }  // NEW: file://path
    
    // For pmf nodes
    public PmfSpec? Pmf { get; set; }
    
    // For expr nodes
    public string? Expression { get; set; }
    public string[]? Dependencies { get; set; }
    public double? Initial { get; set; }  // NEW: For SHIFT
}
```

---

### 4.2 Generator Changes (NodeBasedTemplateService)

#### Update ConvertToEngineSchema()

**Current Behavior:**
- Removes `metadata` and `parameters` sections
- Preserves `grid`, `nodes`, `outputs`, `rng`

**Required Behavior:**
- ALSO preserve `window` section
- ALSO preserve `topology` section
- Do NOT remove these sections

**Change:**
```csharp
private string ConvertToEngineSchema(string yaml)
{
    // ...existing code...
    
    // Detect section starts
    if (trimmed == "parameters:")
    {
        inParametersSection = true;
        sectionIndentLevel = indentLevel;
        continue;
    }
    
    if (trimmed == "metadata:")
    {
        inMetadataSection = true;
        sectionIndentLevel = indentLevel;
        continue;
    }
    
    // NEW: Do NOT skip window section
    if (trimmed == "window:")
    {
        inWindowSection = false;  // Keep this section
    }
    
    // NEW: Do NOT skip topology section
    if (trimmed == "topology:")
    {
        inTopologySection = false;  // Keep this section
    }
    
    // ...rest of logic...
}
```

---

### 4.3 Template Updates

#### Minimum Viable Template (transportation-basic)

**Before (Current):**
```yaml
metadata:
  id: transportation-basic
  title: Basic Transportation System
  description: Simple vehicle capacity modeling

parameters:
  - name: bins
    type: integer
    default: 6

grid:
  bins: ${bins}
  binSize: 60
  binUnit: minutes

nodes:
  - id: passenger_demand
    kind: const
    values: [10, 15, 20, 25, 18, 12]
  
  - id: vehicle_capacity
    kind: const
    values: [15, 18, 25, 30, 22, 16]
  
  - id: passengers_served
    kind: expr
    expr: "MIN(passenger_demand, vehicle_capacity)"

outputs:
  - series: passenger_demand
    as: demand.csv
  - series: passengers_served
    as: served.csv
```

**After (Time-Travel Ready):**
```yaml
metadata:
  id: transportation-basic
  title: Basic Transportation System
  description: Simple vehicle capacity modeling

parameters:
  - name: bins
    type: integer
    default: 6
  - name: startTime
    type: string
    default: "2025-10-07T00:00:00Z"

window:
  start: ${startTime}
  timezone: "UTC"

grid:
  bins: ${bins}
  binSize: 60
  binUnit: minutes

topology:
  nodes:
    - id: TransportService
      kind: service
      group: Transportation
      semantics:
        arrivals: passenger_demand
        served: passengers_served
        capacity: vehicle_capacity
        errors: null
  
  edges: []

nodes:
  - id: passenger_demand
    kind: const
    values: [10, 15, 20, 25, 18, 12]
  
  - id: vehicle_capacity
    kind: const
    values: [15, 18, 25, 30, 22, 16]
  
  - id: passengers_served
    kind: expr
    expr: "MIN(passenger_demand, vehicle_capacity)"
  
  - id: errors_zero
    kind: const
    values: [0, 0, 0, 0, 0, 0]

outputs:
  - series: passenger_demand
    as: demand.csv
  - series: passengers_served
    as: served.csv
```

**Changes:**
1. ✅ Added `window` section with startTime parameter
2. ✅ Added `topology` section with 1 service node
3. ✅ Added `semantics` mapping (arrivals, served, capacity)
4. ✅ Added `kind: service`
5. ✅ Added errors series (even if zero)

---

#### Advanced Template (it-system-microservices)

**After (Time-Travel Ready):**
```yaml
metadata:
  id: it-system-microservices
  title: IT System with Microservices
  description: Modern web application with queues and services

parameters:
  - name: bins
    type: integer
    default: 6
  - name: startTime
    type: string
    default: "2025-10-07T00:00:00Z"
  - name: requestPattern
    type: array
    default: [100, 150, 200, 180, 120, 80]

window:
  start: ${startTime}
  timezone: "UTC"

grid:
  bins: ${bins}
  binSize: 60
  binUnit: minutes

topology:
  nodes:
    - id: LoadBalancer
      kind: service
      group: Infrastructure
      ui: { x: 100, y: 200 }
      semantics:
        arrivals: user_requests
        served: load_balanced_requests
        capacity: load_balancer_capacity
        errors: lb_errors
    
    - id: AuthService
      kind: service
      group: Authentication
      ui: { x: 300, y: 200 }
      semantics:
        arrivals: load_balanced_requests
        served: authenticated_requests
        capacity: auth_capacity
        errors: auth_errors
    
    - id: DatabaseQueue
      kind: queue
      group: Database
      ui: { x: 500, y: 200 }
      semantics:
        arrivals: authenticated_requests
        served: processed_requests
        queue: db_queue_depth
        capacity: database_capacity
        q0: 0
        sla_min: 5.0
  
  edges:
    - from: LoadBalancer:out
      to: AuthService:in
      weight: 1.0
    - from: AuthService:out
      to: DatabaseQueue:in
      weight: 1.0

nodes:
  - id: user_requests
    kind: const
    values: ${requestPattern}
  
  - id: load_balancer_capacity
    kind: const
    values: [300, 300, 300, 300, 300, 300]
  
  - id: load_balanced_requests
    kind: expr
    expr: "MIN(user_requests, load_balancer_capacity)"
  
  - id: lb_errors
    kind: expr
    expr: "MAX(0, user_requests - load_balanced_requests)"
  
  - id: auth_capacity
    kind: const
    values: [250, 250, 250, 250, 250, 250]
  
  - id: authenticated_requests
    kind: expr
    expr: "MIN(load_balanced_requests, auth_capacity)"
  
  - id: auth_errors
    kind: expr
    expr: "MAX(0, load_balanced_requests - authenticated_requests)"
  
  - id: database_capacity
    kind: const
    values: [180, 180, 180, 180, 180, 180]
  
  - id: db_queue_depth
    kind: expr
    expr: "MAX(0, SHIFT(db_queue_depth, 1) + authenticated_requests - processed_requests)"
    initial: 0
  
  - id: processed_requests
    kind: expr
    expr: "MIN(authenticated_requests, database_capacity)"

outputs:
  - series: user_requests
    as: requests.csv
  - series: processed_requests
    as: served.csv
  - series: db_queue_depth
    as: queue.csv
```

**Changes:**
1. ✅ Added `window` section
2. ✅ Added `topology` with 3 nodes (2 services, 1 queue)
3. ✅ Added `edges` showing flow
4. ✅ Added `semantics` for each node
5. ✅ Added `initial: 0` for stateful node (db_queue_depth)
6. ✅ Added UI hints (x, y coordinates)
7. ✅ Added SLA threshold for queue

---

### 4.4 Validation Updates

#### New Validation Rules

**TopologyValidator.cs:**
```csharp
public class TopologyValidator
{
    public void Validate(Template template)
    {
        // 1. Check node ID uniqueness
        var nodeIds = template.Topology?.Nodes.Select(n => n.Id).ToList();
        if (nodeIds != null && nodeIds.Count != nodeIds.Distinct().Count())
        {
            throw new ValidationException("Duplicate node IDs in topology");
        }
        
        // 2. Check semantic references
        var allNodeIds = template.Nodes.Select(n => n.Id).ToHashSet();
        foreach (var topNode in template.Topology?.Nodes ?? Enumerable.Empty<TopologyNode>())
        {
            ValidateSemanticReference(topNode.Semantics.Arrivals, allNodeIds, "arrivals");
            ValidateSemanticReference(topNode.Semantics.Served, allNodeIds, "served");
            ValidateSemanticReference(topNode.Semantics.Capacity, allNodeIds, "capacity");
            ValidateSemanticReference(topNode.Semantics.Queue, allNodeIds, "queue");
        }
        
        // 3. Check kind-specific requirements
        foreach (var topNode in template.Topology?.Nodes ?? Enumerable.Empty<TopologyNode>())
        {
            if (topNode.Kind == "service")
            {
                if (string.IsNullOrEmpty(topNode.Semantics.Arrivals))
                    throw new ValidationException($"Service node {topNode.Id} requires 'arrivals'");
                if (string.IsNullOrEmpty(topNode.Semantics.Served))
                    throw new ValidationException($"Service node {topNode.Id} requires 'served'");
            }
            
            if (topNode.Kind == "queue")
            {
                if (string.IsNullOrEmpty(topNode.Semantics.Queue))
                    throw new ValidationException($"Queue node {topNode.Id} requires 'queue'");
            }
        }
        
        // 4. Check edge validity
        var topologyNodeIds = template.Topology?.Nodes.Select(n => n.Id).ToHashSet();
        foreach (var edge in template.Topology?.Edges ?? Enumerable.Empty<TopologyEdge>())
        {
            var fromNode = edge.From.Split(':')[0];
            var toNode = edge.To.Split(':')[0];
            
            if (!topologyNodeIds.Contains(fromNode))
                throw new ValidationException($"Edge references unknown node: {fromNode}");
            if (!topologyNodeIds.Contains(toNode))
                throw new ValidationException($"Edge references unknown node: {toNode}");
            
            if (fromNode == toNode)
                throw new ValidationException($"Self-loop not allowed: {edge.From} → {edge.To}");
        }
    }
}
```

---

## 5. Implementation Roadmap

### 5.1 Alignment with FlowTime M3.0-M3.2

FlowTime-Sim changes should align with FlowTime Engine milestones:

| FlowTime Milestone | FlowTime-Sim Changes | Priority |
|--------------------|----------------------|----------|
| **M3.0: Foundation** | Add Window, Topology classes; Update 1 template | P0 |
| **M3.1: /state APIs** | Update all 5 templates with semantics | P0 |
| **M3.2: TelemetryLoader** | Add file: URI support; Test with fixtures | P0 |
| **M3.3: Validation** | Add topology validation rules | P1 |

---

### 5.2 Phased Implementation Plan

#### Phase 1: Schema Extensions

**Files to Create:**
- `src/FlowTime.Sim.Core/Templates/Window.cs`
- `src/FlowTime.Sim.Core/Templates/Topology.cs`
- `src/FlowTime.Sim.Core/Templates/TopologyNode.cs`
- `src/FlowTime.Sim.Core/Templates/NodeSemantics.cs`
- `src/FlowTime.Sim.Core/Templates/TopologyEdge.cs`
- `src/FlowTime.Sim.Core/Templates/UIHint.cs`

**Files to Modify:**
- `src/FlowTime.Sim.Core/Templates/Template.cs` (add Window, Topology properties)
- `src/FlowTime.Sim.Core/Templates/TemplateNode.cs` (add Source, Initial properties)

**Tests:**
- `tests/FlowTime.Sim.Tests/Templates/WindowTests.cs`
- `tests/FlowTime.Sim.Tests/Templates/TopologyTests.cs`

**Acceptance Criteria:**
- ✅ Window class with Start, Timezone properties
- ✅ Topology class with Nodes, Edges properties
- ✅ NodeSemantics class with Arrivals, Served, etc.
- ✅ YAML serialization/deserialization works
- ✅ Unit tests pass

---

#### Phase 2: Generator Updates

**Files to Modify:**
- `src/FlowTime.Sim.Core/Services/NodeBasedTemplateService.cs`
  - Update `ConvertToEngineSchema()` to preserve window and topology
  - Update parameter substitution to work with topology

**Tests:**
- `tests/FlowTime.Sim.Tests/Services/NodeBasedTemplateServiceTests.cs`
  - Test window preservation
  - Test topology preservation
  - Test parameter substitution in topology

**Acceptance Criteria:**
- ✅ Generated models include window section
- ✅ Generated models include topology section
- ✅ Parameters work in topology (e.g., ${startTime})
- ✅ Integration tests pass

---

#### Phase 3: Template Updates

**Templates to Update (Priority Order):**

1. **transportation-basic.yaml** (Simplest)
   - Add window section
   - Add topology with 1 service node
   - Add semantics (arrivals, served, capacity)

2. **it-system-microservices.yaml** (Most Complex)
   - Add window section
   - Add topology with 3 nodes (2 services, 1 queue)
   - Add edges
   - Add semantics for each node
   - Add initial conditions for queue

3. **manufacturing-line.yaml**
   - Add window section
   - Add topology with pipeline stages
   - Add edges showing production flow

4. **supply-chain-multi-tier.yaml**
   - Add window section
   - Add topology with suppliers, warehouses, retailers
   - Add edges with weights (split ratios)

5. **network-reliability.yaml**
   - Add window section
   - Add topology with network nodes
   - Add edges with failure probabilities

**Tests:**
- Generate model from each template
- Validate against KISS schema
- Test with Engine M3.0

**Acceptance Criteria:**
- ✅ All 5 templates generate valid time-travel models
- ✅ Models load in Engine without errors
- ✅ /state API works with generated models

---

#### Phase 4: Validation (Optional for P1)

**Files to Create:**
- `src/FlowTime.Sim.Core/Templates/TopologyValidator.cs`

**Files to Modify:**
- `src/FlowTime.Sim.Core/Templates/TemplateParser.cs` (call TopologyValidator)

**Tests:**
- `tests/FlowTime.Sim.Tests/Templates/TopologyValidatorTests.cs`

**Acceptance Criteria:**
- ✅ Validation catches duplicate node IDs
- ✅ Validation catches invalid semantic references
- ✅ Validation catches missing required fields
- ✅ Validation catches invalid edges

---

### 5.3 Backward Compatibility

**Strategy:**
- Old templates (without window/topology) still work for simulation-only
- New templates (with window/topology) work for both simulation and time-travel
- Engine supports both schema versions

**Migration Path:**
1. FlowTime-Sim M3.0: Add window/topology support (opt-in)
2. FlowTime-Sim M3.1: Update all built-in templates
3. FlowTime-Sim M3.2: Deprecate old schema (warning)
4. FlowTime-Sim M4.0: Remove old schema support (breaking)

---

## 6. Testing Strategy

### 6.1 Unit Tests

**Template Parsing:**
- Parse template with window section
- Parse template with topology section
- Parse template with semantics
- Parse template with edges
- Parse template with UI hints

**Schema Validation:**
- Validate node ID uniqueness
- Validate semantic references
- Validate kind-specific requirements
- Validate edge validity
- Validate window format

**Generator:**
- Generate model with window preserved
- Generate model with topology preserved
- Generate model with parameter substitution in topology

---

### 6.2 Integration Tests

**End-to-End:**
1. Generate model from template
2. Load model in Engine
3. Call /state API
4. Verify response includes topology

**Template Tests:**
- Generate from transportation-basic → validate window
- Generate from it-system-microservices → validate topology with 3 nodes
- Generate from manufacturing-line → validate edges

---

### 6.3 Compatibility Tests

**With FlowTime Engine:**
- Generate model from Sim
- POST /v1/runs in Engine
- GET /v1/runs/{id}/state → verify works
- GET /v1/runs/{id}/state_window → verify works

---

## 7. Risk Assessment

### 7.1 High Risks

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| **Schema complexity creep** | Medium | High | Keep KISS, minimal topology |
| **Backward compat break** | Low | High | Opt-in new features, deprecate slowly |
| **Template maintenance burden** | High | Medium | Start with 1-2 templates, expand gradually |

---

### 7.2 Medium Risks

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| **Validation false positives** | Medium | Medium | Make validation rules configurable |
| **UI hints auto-layout** | High | Low | Defer to UI, provide reasonable defaults |
| **Parameter explosion** | Medium | Medium | Group parameters by section |

---

### 7.3 Low Risks

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| **File source performance** | Low | Low | Stream CSVs, benchmark early |
| **Edge weight semantics** | Low | Medium | Document clearly, test edge cases |

---

## 8. Open Questions

### 8.1 Questions for FlowTime Team

1. **Window startTime parameter:**
   - Should all templates have a `startTime` parameter?
   - Or should it default to "now" if not provided?
   - Recommendation: Make it a parameter with default

2. **Topology auto-generation:**
   - Should FlowTime-Sim infer topology from node expressions?
   - Or require explicit topology in templates?
   - Recommendation: Explicit (better for clarity)

3. **File sources in templates:**
   - Should templates use file: URIs or inline values by default?
   - Recommendation: Inline for examples, file: for telemetry-replay templates

4. **Edge weights:**
   - Are edge weights used by Engine?
   - Or just metadata for UI?
   - Recommendation: Clarify in KISS Ch2

5. **Initial conditions default:**
   - Should initial: 0 be assumed if missing?
   - Or should it be an error?
   - Recommendation: Error (explicit is better)

---

## 9. Recommendations

### 9.1 Immediate Actions (P0)

1. **Create schema extension branch** in flowtime-sim-vnext
2. **Add Window and Topology classes** (Phase 1)
3. **Update ConvertToEngineSchema** to preserve new sections (Phase 2)
4. **Update 1 template** (transportation-basic) as proof of concept (Phase 3)
5. **Test with FlowTime Engine M3.0** (once M3.0 is ready)

**Estimated Effort:**
- Phase 1: 1-2 hours (schema classes)
- Phase 2: 2-3 hours (generator updates)
- Phase 3: 1 hour (1 template update)
- Testing: 2 hours
- **Total: 6-8 hours** for MVP

---

### 9.2 Follow-Up Actions (P1)

6. **Update remaining 4 templates** (Phase 3 complete)
7. **Add topology validation** (Phase 4)
8. **Add file: URI examples** for telemetry-replay
9. **Document migration guide** for custom templates

**Estimated Effort:**
- 4 templates: 3-4 hours
- Validation: 2-3 hours
- Documentation: 2 hours
- **Total: 7-9 hours**

---

### 9.3 Future Work (P2)

10. **Auto-layout algorithm** for UI hints
11. **Template marketplace** (share templates across teams)
12. **Topology inference** (optional auto-generation from expressions)
13. **Visual template editor** (drag-and-drop node placement)

---

## 10. Conclusion

### 10.1 Summary

FlowTime-Sim is **well-architected** and **ready for extension** to support time-travel. The current template system, parameter substitution, and code quality are solid foundations.

However, FlowTime-Sim **does NOT currently generate** models with the schema extensions required by the KISS architecture (window, topology, semantics). This is a **blocking issue** for time-travel APIs (/state, /state_window).

### 10.2 Path Forward

**Short Term (Next 1-2 weeks):**
1. Implement schema extensions (Window, Topology classes)
2. Update generator to preserve new sections
3. Update 1-2 templates as proof of concept
4. Test with FlowTime Engine M3.0 (when ready)

**Medium Term (Next month):**
5. Update all 5 built-in templates
6. Add topology validation
7. Add telemetry-replay template examples
8. Document migration guide

**Long Term (Q1 2026):**
9. Deprecate old schema (warning only)
10. Add advanced features (auto-layout, inference)
11. Consider visual template editor

### 10.3 Effort Estimate

**MVP (P0):** 6-8 hours
**Full Support (P0 + P1):** 13-17 hours
**Total with Testing:** ~20 hours (2.5 days)

**Assessment:** Manageable effort, low risk, high value.

---

## Appendix A: Example Generated Models

### A.1 Current Model (Simulation-Only)

```yaml
schemaVersion: 1

grid:
  bins: 6
  binSize: 60
  binUnit: minutes

nodes:
  - id: passenger_demand
    kind: const
    values: [10, 15, 20, 25, 18, 12]
  
  - id: passengers_served
    kind: expr
    expr: "MIN(passenger_demand, vehicle_capacity)"

outputs:
  - series: passenger_demand
    as: demand.csv
```

**Status:** ✅ Works with Engine M2.10, ❌ NOT ready for time-travel

---

### A.2 Future Model (Time-Travel Ready)

```yaml
schemaVersion: 1

window:
  start: "2025-10-07T00:00:00Z"
  timezone: "UTC"

grid:
  bins: 6
  binSize: 60
  binUnit: minutes

topology:
  nodes:
    - id: TransportService
      kind: service
      semantics:
        arrivals: passenger_demand
        served: passengers_served
        capacity: vehicle_capacity

nodes:
  - id: passenger_demand
    kind: const
    values: [10, 15, 20, 25, 18, 12]
  
  - id: vehicle_capacity
    kind: const
    values: [15, 18, 25, 30, 22, 16]
  
  - id: passengers_served
    kind: expr
    expr: "MIN(passenger_demand, vehicle_capacity)"

outputs:
  - series: passenger_demand
    as: demand.csv
```

**Status:** ✅ Ready for Engine M3.0+, ✅ Ready for time-travel

---

## Appendix B: KISS Architecture Reference

**Source Documents (in flowtime-vnext):**
- `docs/architecture/flowtime-kiss-arch-ch1.md` - Principles
- `docs/architecture/flowtime-kiss-arch-ch2.md` - Data Contracts (CRITICAL)
- `docs/architecture/flowtime-kiss-arch-ch3.md` - Components
- `docs/architecture/flowtime-kiss-arch-ch4.md` - Data Flows
- `docs/architecture/flowtime-kiss-arch-ch5.md` - Roadmap
- `docs/architecture/flowtime-kiss-arch-ch6.md` - Decisions

**Key Sections for FlowTime-Sim:**
- Ch2, §2.2.1: Window Section (absolute time)
- Ch2, §2.2.2: Topology Section (nodes, edges, semantics)
- Ch2, §2.2.3: Initial Conditions (SHIFT with initial field)
- Ch2, §2.3.1: CSV File Format (for telemetry sources)

---

**End of Audit**

**Next Action:** Review with team, prioritize changes, create implementation branch.
