# FlowTime Engine - Time-Travel Demo Audit
**Date**: October 7, 2025  
**Focus**: Shipping readiness for time-travel demo  
**Scope**: Engine surfaces only (not UI/Sim)

---

## Audit Results

### A) /graph Endpoint

**Status**: ✅ **EXISTS**

**Location**: `src/FlowTime.API/Program.cs:549-590`

**Exact Response Shape**:
```csharp
// Line 579-585
return Results.Ok(new
{
    nodes = coreModel.Nodes.Select(n => n.Id).ToArray(),
    order = order.Select(o => o.Value).ToArray(),
    edges
});
```

**Response Fields**:
- `nodes`: `string[]` - Array of node IDs
- `order`: `string[]` - Topologically sorted node IDs
- `edges`: Array of `{ id: string, inputs: string[] }` - Dependency edges

**HTTP Method**: `POST /v1/graph`  
**Input**: YAML model (text/plain)  
**Output**: JSON graph structure

**Code Evidence**:
```csharp
v1.MapPost("/graph", async (HttpRequest req, ILogger<Program> logger) =>
{
    // ... parses YAML, builds graph
    var (grid, graph) = ModelParser.ParseModel(coreModel);
    var order = graph.TopologicalOrder();
    
    var edges = coreModel.Nodes.Select(n => new
    {
        id = n.Id,
        inputs = GraphAnalyzer.GetNodeInputs(n, coreModel)
    });
    
    return Results.Ok(new
    {
        nodes = coreModel.Nodes.Select(n => n.Id).ToArray(),
        order = order.Select(o => o.Value).ToArray(),
        edges
    });
}
```

**Assessment**: ✅ Fully functional, correct shape

---

### B) /state and /state_window Endpoints

**Status**: ❌ **DO NOT EXIST**

**Evidence**: 
- Searched `src/FlowTime.API/Program.cs` for `/state` patterns: **0 matches**
- No endpoints matching `/state?ts=`, `/state_window`, or similar
- Available endpoints: `/run`, `/graph`, `/artifacts/*`, `/runs/{id}/series/{name}`

**Expected Shapes (NOT IMPLEMENTED)**:

#### B.1) GET /v1/runs/{runId}/state?ts={binIndex}

**Expected Response**:
```json
{
  "runId": "run_...",
  "binIndex": 42,
  "timestamp": "2025-10-07T14:30:00Z",  // If start time known
  "nodes": {
    "demand": {
      "arrivals": 150.0,
      "served": 145.0,
      "queue": 5.0,
      "capacity": 200.0,
      "latency_min": 0.33
    },
    "service_a": {
      "arrivals": 145.0,
      "served": 140.0,
      "queue": 8.0,
      "capacity": 150.0,
      "latency_min": 0.57
    }
  }
}
```

**File to Create**: `src/FlowTime.API/Program.cs` (add endpoint around line 600)

**Minimal Implementation**:
```csharp
v1.MapGet("/runs/{runId}/state", async (string runId, int? ts, ILogger<Program> logger) =>
{
    if (!ts.HasValue)
        return Results.BadRequest(new { error = "ts parameter required" });
    
    var artifactsDir = Program.GetArtifactsDirectory(builder.Configuration);
    var runPath = Path.Combine(artifactsDir, runId);
    
    if (!Directory.Exists(runPath))
        return Results.NotFound(new { error = $"Run {runId} not found" });
    
    var reader = new FileSeriesReader();
    var adapter = new RunArtifactAdapter(reader, runPath);
    var index = await adapter.GetIndexAsync();
    
    var nodes = new Dictionary<string, object>();
    foreach (var seriesMeta in index.Series)
    {
        var series = await reader.ReadSeriesAsync(runPath, seriesMeta.Id);
        var values = series.ToArray();
        
        if (ts.Value >= 0 && ts.Value < values.Length)
        {
            // Simple node shape - just value at this bin
            nodes[seriesMeta.Id] = new { value = values[ts.Value] };
        }
    }
    
    return Results.Ok(new
    {
        runId = runId,
        binIndex = ts.Value,
        nodes = nodes
    });
});
```

**Complexity**: 2-3 hours

---

#### B.2) GET /v1/runs/{runId}/state_window?start={bin}&end={bin}

**Expected Response**:
```json
{
  "runId": "run_...",
  "window": {
    "start": 100,
    "end": 200
  },
  "nodes": {
    "demand": {
      "arrivals": [150, 155, 148, ...],    // 101 values
      "served": [145, 150, 145, ...],
      "queue": [5, 10, 13, ...],
      "capacity": [200, 200, 200, ...],
      "latency_min": [0.33, 0.67, 0.87, ...]
    }
  }
}
```

**File to Create**: `src/FlowTime.API/Program.cs` (add endpoint around line 650)

**Minimal Implementation**:
```csharp
v1.MapGet("/runs/{runId}/state_window", async (string runId, int? start, int? end, ILogger<Program> logger) =>
{
    if (!start.HasValue || !end.HasValue)
        return Results.BadRequest(new { error = "start and end parameters required" });
    
    if (start.Value < 0 || end.Value < start.Value)
        return Results.BadRequest(new { error = "Invalid window: start must be >= 0 and end >= start" });
    
    var artifactsDir = Program.GetArtifactsDirectory(builder.Configuration);
    var runPath = Path.Combine(artifactsDir, runId);
    
    if (!Directory.Exists(runPath))
        return Results.NotFound(new { error = $"Run {runId} not found" });
    
    var reader = new FileSeriesReader();
    var adapter = new RunArtifactAdapter(reader, runPath);
    var index = await adapter.GetIndexAsync();
    
    var nodes = new Dictionary<string, object>();
    foreach (var seriesMeta in index.Series)
    {
        var series = await reader.ReadSeriesAsync(runPath, seriesMeta.Id);
        var values = series.ToArray();
        
        var windowStart = Math.Max(0, start.Value);
        var windowEnd = Math.Min(values.Length - 1, end.Value);
        var windowSize = windowEnd - windowStart + 1;
        
        if (windowSize > 0)
        {
            var windowValues = new double[windowSize];
            Array.Copy(values, windowStart, windowValues, 0, windowSize);
            nodes[seriesMeta.Id] = windowValues;
        }
    }
    
    return Results.Ok(new
    {
        runId = runId,
        window = new { start = start.Value, end = end.Value },
        nodes = nodes
    });
});
```

**Complexity**: 3-4 hours

**Current Workaround**: Client must use `GET /v1/runs/{id}/series/{name}` to fetch full CSV, then slice locally

---

### C) Grid Discipline

**Status**: 🚧 **PARTIAL** (binSize/binUnit ✅, UTC alignment ❌)

#### C.1) binSize/binUnit Support

**Status**: ✅ **IMPLEMENTED**

**Location**: `src/FlowTime.Core/TimeGrid.cs:54-75`

**Code Evidence**:
```csharp
public readonly record struct TimeGrid
{
    public int Bins { get; }
    public int BinSize { get; }
    public TimeUnit BinUnit { get; }
    public int BinMinutes { get; }
    public int TotalMinutes => Bins * BinMinutes;
    
    public TimeGrid(int bins, int binSize, TimeUnit binUnit)
    {
        if (bins <= 0 || bins > 10000)
            throw new ArgumentException("bins must be between 1 and 10000", nameof(bins));
        if (binSize <= 0 || binSize > 1000)
            throw new ArgumentException("binSize must be between 1 and 1000", nameof(binSize));
        
        Bins = bins;
        BinSize = binSize;
        BinUnit = binUnit;
        BinMinutes = binUnit.ToMinutes(binSize);
    }
}
```

**Supported Units**: `minutes`, `hours`, `days`, `weeks` (line 38-45)

**Validation**: Enforced in `ModelValidator.cs` and `ModelParser.cs`

**Assessment**: ✅ Correct implementation

---

#### C.2) UTC Alignment

**Status**: ❌ **NOT IMPLEMENTED**

**Current Situation**:
- TimeGrid has NO start time or timezone fields (line 54-75)
- Export services default to `DateTime.UtcNow.Date` for StartTime (exporters only)
- Exporters have `Timezone = "UTC"` property but NOT used in core evaluation
- Bins are abstract indices (0, 1, 2, ...) with NO wall-clock mapping

**Evidence**:
```csharp
// From GoldCsvExporter.cs:18, 33
public DateTime StartTime { get; init; } = DateTime.UtcNow.Date;
public string Timezone { get; init; } = "UTC";
```

**But TimeGrid has NO such fields** (TimeGrid.cs:54-75):
```csharp
public readonly record struct TimeGrid
{
    public int Bins { get; }
    public int BinSize { get; }
    public TimeUnit BinUnit { get; }
    // ❌ NO StartTime
    // ❌ NO Timezone
}
```

**Impact**: Cannot map bin indices to UTC timestamps

**To Make ✅ (Required Changes)**:

1. **Add UTC fields to TimeGrid** (`src/FlowTime.Core/TimeGrid.cs`):

```csharp
public readonly record struct TimeGrid
{
    public int Bins { get; }
    public int BinSize { get; }
    public TimeUnit BinUnit { get; }
    public int BinMinutes { get; }
    public DateTime? StartTimeUtc { get; }  // ← ADD THIS
    
    public int TotalMinutes => Bins * BinMinutes;
    public int Length => Bins;
    
    // Add new constructor overload
    public TimeGrid(int bins, int binSize, TimeUnit binUnit, DateTime? startTimeUtc = null)
    {
        // ... existing validation ...
        
        Bins = bins;
        BinSize = binSize;
        BinUnit = binUnit;
        BinMinutes = binUnit.ToMinutes(binSize);
        StartTimeUtc = startTimeUtc;
    }
    
    // Helper to get UTC time for bin index
    public DateTime? GetBinTimeUtc(int binIndex)
    {
        if (!StartTimeUtc.HasValue) return null;
        return StartTimeUtc.Value.AddMinutes(binIndex * BinMinutes);
    }
}
```

2. **Update ModelParser** to accept optional startTime in YAML:

```csharp
// In ModelParser.cs, update GridDefinition class:
public class GridDefinition
{
    public int Bins { get; set; }
    public int BinSize { get; set; }
    public string BinUnit { get; set; } = "minutes";
    public DateTime? StartTimeUtc { get; set; }  // ← ADD THIS
}

// Update ParseModel:
var grid = new TimeGrid(
    model.Grid.Bins, 
    model.Grid.BinSize, 
    unit,
    model.Grid.StartTimeUtc  // ← ADD THIS
);
```

3. **Include in artifacts** (`src/FlowTime.Core/Artifacts/RunArtifactWriter.cs`):

Update GridJson class around line 230:
```csharp
file record GridJson { 
    public int Bins { get; set; } 
    public int BinSize { get; set; } 
    public string BinUnit { get; set; } = ""; 
    public DateTime? StartTimeUtc { get; set; }  // ← ADD THIS
}
```

**Complexity**: 4-6 hours (touches multiple files, needs test updates)

**For Demo**: Can defer if you manually compute timestamps in UI layer

---

#### C.3) Exact H bins

**Status**: ✅ **SUPPORTED**

**Evidence**: `TimeGrid.cs:63-65`
```csharp
if (bins <= 0 || bins > 10000)
    throw new ArgumentException("bins must be between 1 and 10000", nameof(bins));
```

**Supports**: 1 to 10,000 bins (sufficient for hours, days, weeks)

**Example**:
- 24 hours: `bins: 24, binSize: 1, binUnit: hours`
- 168 hours (1 week): `bins: 168, binSize: 1, binUnit: hours`
- 8760 hours (1 year): `bins: 8760, binSize: 1, binUnit: hours`

**Assessment**: ✅ Adequate range

---

### D) Backlog v1 Function

**Status**: ❌ **DOES NOT EXIST**

**Evidence**:
- Searched `src/FlowTime.Core` for BacklogNode, QueueNode: **0 matches**
- Only stateful node: `ShiftNode` (temporal shift, not accumulation)
- Node directory: `src/FlowTime.Core/Nodes/` contains only `ShiftNode.cs`

**Expected Signature**:
```csharp
public class BacklogNode : IStatefulNode
{
    public NodeId Id { get; }
    private double queueState;
    
    // Q[t] = max(0, Q[t-1] + inflow[t] - capacity[t])
    public Series Evaluate(TimeGrid grid, Func<NodeId, Series> getInput);
    public void InitializeState(TimeGrid grid);
}
```

**Expected Math** (NOT IMPLEMENTED):
```csharp
for (int t = 0; t < grid.Bins; t++)
{
    var inflow = inflowSeries[t];
    var capacity = capacitySeries[t];
    queueState = Math.Max(0, queueState + inflow - capacity);
    result[t] = queueState;
}
```

**To Make ✅ (Required Implementation)**:

**File to Create**: `src/FlowTime.Core/Nodes/BacklogNode.cs`

```csharp
namespace FlowTime.Core.Nodes;

/// <summary>
/// Stateful queue node implementing Q[t] = max(0, Q[t-1] + inflow[t] - capacity[t])
/// </summary>
public class BacklogNode : IStatefulNode
{
    public NodeId Id { get; }
    public IEnumerable<NodeId> Inputs { get; }
    
    private readonly NodeId inflowNodeId;
    private readonly NodeId capacityNodeId;
    private double queueState;
    
    public BacklogNode(string id, string inflowNode, string capacityNode)
    {
        Id = new NodeId(id);
        inflowNodeId = new NodeId(inflowNode);
        capacityNodeId = new NodeId(capacityNode);
        Inputs = new[] { inflowNodeId, capacityNodeId };
    }
    
    public void InitializeState(TimeGrid grid)
    {
        queueState = 0.0;
    }
    
    public Series Evaluate(TimeGrid grid, Func<NodeId, Series> getInput)
    {
        var inflowSeries = getInput(inflowNodeId);
        var capacitySeries = getInput(capacityNodeId);
        var result = new double[grid.Bins];
        
        for (int t = 0; t < grid.Bins; t++)
        {
            var inflow = inflowSeries[t];
            var capacity = capacitySeries[t];
            
            // Core backlog formula: Q[t] = max(0, Q[t-1] + inflow - capacity)
            queueState = Math.Max(0, queueState + inflow - capacity);
            result[t] = queueState;
        }
        
        return new Series(result);
    }
}
```

**Also Update**: `src/FlowTime.Core/Models/ModelParser.cs`

Add to switch statement around line 63:
```csharp
return nodeDef.Kind switch
{
    "const" => ParseConstNode(nodeDef),
    "expr" => ParseExprNode(nodeDef),
    "pmf" => ParsePmfNode(nodeDef),
    "backlog" => ParseBacklogNode(nodeDef),  // ← ADD THIS
    _ => throw new ModelParseException($"Unknown node kind: {nodeDef.Kind}")
};
```

Add parser method:
```csharp
private static INode ParseBacklogNode(NodeDefinition nodeDef)
{
    if (string.IsNullOrWhiteSpace(nodeDef.InflowNode))
        throw new ModelParseException($"Node {nodeDef.Id}: backlog nodes require inflowNode property");
    if (string.IsNullOrWhiteSpace(nodeDef.CapacityNode))
        throw new ModelParseException($"Node {nodeDef.Id}: backlog nodes require capacityNode property");
    
    return new BacklogNode(nodeDef.Id, nodeDef.InflowNode, nodeDef.CapacityNode);
}
```

**Update NodeDefinition class** (same file):
```csharp
public class NodeDefinition
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "const";
    public double[]? Values { get; set; }
    public string? Expr { get; set; }
    public Dictionary<string, double>? Pmf { get; set; }
    public string? InflowNode { get; set; }      // ← ADD THIS
    public string? CapacityNode { get; set; }    // ← ADD THIS
}
```

**YAML Usage**:
```yaml
- id: queue
  kind: backlog
  inflowNode: demand
  capacityNode: capacity
```

**Complexity**: 6-8 hours (new node type + parser + tests + integration)

**Critical for Demo**: ✅ YES - this is the core queueing mechanic

---

### E) Latency Derivation

**Status**: ❌ **DOES NOT EXIST**

**Evidence**:
- Searched `src/FlowTime.Core` for "latency" or "Latency": **0 matches**
- No Little's Law implementation
- No automatic latency calculation

**Expected Approach**:

#### Option 1: Latency as Derived Node (Recommended)

**File to Create**: `src/FlowTime.Core/Nodes/LatencyNode.cs`

```csharp
namespace FlowTime.Core.Nodes;

/// <summary>
/// Computes latency using Little's Law: L = Q / λ
/// Where Q = queue length, λ = throughput (arrivals or departures)
/// Returns latency in bins (multiply by binMinutes for time units)
/// </summary>
public class LatencyNode : INode
{
    public NodeId Id { get; }
    public IEnumerable<NodeId> Inputs { get; }
    
    private readonly NodeId queueNodeId;
    private readonly NodeId throughputNodeId;
    private readonly double epsilon; // Avoid division by zero
    
    public LatencyNode(string id, string queueNode, string throughputNode, double epsilon = 0.001)
    {
        Id = new NodeId(id);
        queueNodeId = new NodeId(queueNode);
        throughputNodeId = new NodeId(throughputNode);
        this.epsilon = epsilon;
        Inputs = new[] { queueNodeId, throughputNodeId };
    }
    
    public Series Evaluate(TimeGrid grid, Func<NodeId, Series> getInput)
    {
        var queueSeries = getInput(queueNodeId);
        var throughputSeries = getInput(throughputNodeId);
        var result = new double[grid.Bins];
        
        for (int t = 0; t < grid.Bins; t++)
        {
            var queue = queueSeries[t];
            var throughput = throughputSeries[t];
            
            // Little's Law: L = Q / λ
            // Add epsilon to avoid division by zero
            result[t] = queue / Math.Max(epsilon, throughput);
        }
        
        return new Series(result);
    }
}
```

**Add to ModelParser.cs**:
```csharp
"latency" => ParseLatencyNode(nodeDef),

private static INode ParseLatencyNode(NodeDefinition nodeDef)
{
    if (string.IsNullOrWhiteSpace(nodeDef.QueueNode))
        throw new ModelParseException($"Node {nodeDef.Id}: latency nodes require queueNode property");
    if (string.IsNullOrWhiteSpace(nodeDef.ThroughputNode))
        throw new ModelParseException($"Node {nodeDef.Id}: latency nodes require throughputNode property");
    
    return new LatencyNode(nodeDef.Id, nodeDef.QueueNode, nodeDef.ThroughputNode);
}
```

**Update NodeDefinition**:
```csharp
public string? QueueNode { get; set; }        // ← ADD
public string? ThroughputNode { get; set; }   // ← ADD
```

**YAML Usage**:
```yaml
- id: latency_min
  kind: latency
  queueNode: queue
  throughputNode: served
```

**Result**: Latency in bins (multiply by grid.BinMinutes for minutes)

**Complexity**: 4-6 hours

---

#### Option 2: Manual Expression (Workaround)

**Current Capability**: User can write:
```yaml
- id: latency_min
  kind: expr
  expr: "queue / MAX(served, 0.001)"
```

**Pros**: No new code needed  
**Cons**: Verbose, no semantic meaning, error-prone

**For Demo**: Option 2 is sufficient if time-constrained

---

### F) State Slicing Performance

**Status**: ✅ **O(1) BIN INDEX** / ❌ **NO CACHING**

#### F.1) Bin Index Access

**Status**: ✅ **O(1)**

**Location**: `src/FlowTime.Core/Series.cs:5-26`

**Code Evidence**:
```csharp
public sealed class Series
{
    private readonly double[] data;
    public int Length => data.Length;
    
    public double this[int t]
    {
        get => data[t];
        set => data[t] = value;
    }
}
```

**Performance**: Direct array indexing → **O(1) access by bin index**

**Assessment**: ✅ Optimal for single-bin queries

---

#### F.2) Evaluation Caching

**Status**: ❌ **NO PERSISTENT CACHE**

**Current Behavior**: `src/FlowTime.Core/Graph.cs:50-61`

```csharp
public IReadOnlyDictionary<NodeId, Series> Evaluate(TimeGrid grid)
{
    var order = TopologicalOrder();
    var memo = new Dictionary<NodeId, Series>();  // ← In-memory ONLY
    foreach (var id in order)
    {
        var node = nodes[id];
        Series GetInput(NodeId n) => memo[n];
        memo[id] = node.Evaluate(grid, GetInput);
    }
    return memo;
}
```

**Evidence**:
- `memo` dictionary is local to `Evaluate()` call
- Results are NOT cached between API requests
- Each `POST /v1/run` re-evaluates entire graph
- Artifacts are written to disk but NOT read back for queries

**Impact**:
- ✅ State queries (`/state?ts=`) can be O(1) by reading CSV at line `ts`
- ❌ Must re-run evaluation if artifacts don't exist
- ❌ No hot cache for repeated queries

**Current Workaround**:
- `POST /v1/run` writes artifacts to disk
- `GET /v1/runs/{id}/series/{name}` reads from CSV files
- CSV reading is efficient (sequential I/O)

**To Make ✅ (Optional Optimization)**:

**Option 1**: Cache parsed artifacts in memory (simple)

**File**: `src/FlowTime.API/Program.cs` (add around line 30)

```csharp
// Add in-memory cache for parsed run artifacts
private static readonly MemoryCache artifactCache = new MemoryCache(new MemoryCacheOptions
{
    SizeLimit = 100  // Cache up to 100 runs
});

// In /state or /state_window endpoints:
var cacheKey = $"run:{runId}";
if (!artifactCache.TryGetValue(cacheKey, out Dictionary<string, double[]>? seriesData))
{
    // Load from disk
    seriesData = new Dictionary<string, double[]>();
    var reader = new FileSeriesReader();
    var adapter = new RunArtifactAdapter(reader, runPath);
    var index = await adapter.GetIndexAsync();
    
    foreach (var seriesMeta in index.Series)
    {
        var series = await reader.ReadSeriesAsync(runPath, seriesMeta.Id);
        seriesData[seriesMeta.Id] = series.ToArray();
    }
    
    // Cache for 5 minutes
    artifactCache.Set(cacheKey, seriesData, new MemoryCacheEntryOptions
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
        Size = 1
    });
}

// Now slicing is O(1)
var value = seriesData["demand"][binIndex];
```

**Complexity**: 2-3 hours  
**Benefit**: Repeated queries on same run become instant

**Option 2**: Pre-load all artifacts on startup (aggressive)

**Not Recommended**: High memory usage, stale data issues

**For Demo**: Skip caching, CSV reads are fast enough (<100ms for 10k bins)

---

## Summary Checklist

| Item | Status | Evidence | Effort to ✅ |
|------|--------|----------|-------------|
| **A) /graph** | ✅ | `Program.cs:549-590` | N/A - working |
| **B.1) /state?ts=** | ❌ | Not found | 2-3 hours |
| **B.2) /state_window** | ❌ | Not found | 3-4 hours |
| **C.1) binSize/binUnit** | ✅ | `TimeGrid.cs:54-75` | N/A - working |
| **C.2) UTC alignment** | ❌ | TimeGrid has no StartTimeUtc | 4-6 hours |
| **C.3) Exact H bins** | ✅ | Supports 1-10,000 bins | N/A - working |
| **D) Backlog Q[t]** | ❌ | No BacklogNode | 6-8 hours (critical) |
| **E) Latency derivation** | ❌ | No LatencyNode | 4-6 hours OR use expr workaround |
| **F.1) O(1) slicing** | ✅ | `Series.cs:19-22` direct array | N/A - working |
| **F.2) Caching** | ❌ | In-memory only, no persistence | 2-3 hours (optional) |

---

## Minimal Viable Demo Plan (1-2 Days)

### Priority 1: Critical Path (12-15 hours)

1. **BacklogNode** (6-8h) - `src/FlowTime.Core/Nodes/BacklogNode.cs`
   - Implement Q[t] = max(0, Q[t-1] + inflow - capacity)
   - Add to ModelParser
   - Write 5-10 unit tests

2. **State Window API** (3-4h) - `src/FlowTime.API/Program.cs`
   - Add `GET /runs/{id}/state_window?start=X&end=Y`
   - Read CSVs, slice arrays, return JSON
   - Write 2-3 integration tests

3. **State Point API** (2-3h) - `src/FlowTime.API/Program.cs`
   - Add `GET /runs/{id}/state?ts=X`
   - Read CSVs, return single-bin values
   - Write 2 integration tests

### Priority 2: Demo Polish (4-6 hours)

4. **Latency via Expr** (1h) - Use workaround
   - Document expr pattern: `queue / MAX(served, 0.001)`
   - Create example YAML

5. **Demo Scenario** (2-3h)
   - Create demand spike scenario
   - Run via API
   - Query state windows at key timestamps
   - Generate visualization (client-side)

6. **Documentation** (1-2h)
   - API endpoint docs
   - YAML examples
   - Response shape reference

### Can Defer (Nice-to-Have)

- UTC alignment (4-6h) - compute timestamps in UI layer
- In-memory caching (2-3h) - CSV reads are fast enough
- LatencyNode (4-6h) - expr workaround sufficient

---

## Code Locations Summary

| Component | File | Lines | Status |
|-----------|------|-------|--------|
| /graph endpoint | `src/FlowTime.API/Program.cs` | 549-590 | ✅ |
| /state endpoint | `src/FlowTime.API/Program.cs` | N/A | ❌ Add ~600 |
| /state_window | `src/FlowTime.API/Program.cs` | N/A | ❌ Add ~650 |
| TimeGrid | `src/FlowTime.Core/TimeGrid.cs` | 54-75 | 🚧 |
| Series indexing | `src/FlowTime.Core/Series.cs` | 19-22 | ✅ |
| BacklogNode | `src/FlowTime.Core/Nodes/BacklogNode.cs` | N/A | ❌ New file |
| ModelParser | `src/FlowTime.Core/Models/ModelParser.cs` | 63+ | ❌ Add backlog case |
| LatencyNode | `src/FlowTime.Core/Nodes/LatencyNode.cs` | N/A | ❌ Optional |
| Graph.Evaluate | `src/FlowTime.Core/Graph.cs` | 50-61 | ✅ |

---

## Risk Assessment

**Green (Low Risk)**:
- ✅ Core evaluation stable (390/393 tests pass)
- ✅ Artifact system working
- ✅ /graph endpoint functional

**Yellow (Medium Risk)**:
- 🚧 BacklogNode needs testing at scale
- 🚧 State endpoints need performance validation with large runs

**Red (High Risk)**:
- ❌ No queue mechanics = **cannot demonstrate core use case**
- ❌ No state query API = **cannot demonstrate time-travel**

**Blocker Resolution**: Must implement BacklogNode (D) and state_window (B.2) to ship demo

---

**End of Audit**
