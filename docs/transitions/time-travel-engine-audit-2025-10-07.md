# FlowTime Engine - Current State Audit Report
**Date**: October 7, 2025  
**Repository**: flowtime-vnext (main branch)  
**Auditor**: Repository Analysis Bot  
**Scope**: Code-level audit focusing on time-travel readiness

---

## Executive Summary

FlowTime Engine is a **deterministic, discrete-time, graph-based engine** at post-M2.6 maturity. Core evaluation works reliably. **Time-travel API endpoints (`/state`, `/state_window`) are NOT implemented**. The system produces excellent artifacts but lacks runtime state query capabilities needed for 5-day time-travel demo.

**Critical Gap**: No backlog/queue/capacity node types exist. Only stateless const/expr/pmf nodes + stateful SHIFT.

---

## 1. Build/Run Status ✅

### Language & Framework
- **Language**: C# / .NET 9
- **Projects**: 
  - `src/FlowTime.Core/FlowTime.Core.csproj` - Core engine (src/FlowTime.Core/TimeGrid.cs:1-77, src/FlowTime.Core/Graph.cs:1-90)
  - `src/FlowTime.API/FlowTime.API.csproj` - HTTP API (src/FlowTime.API/Program.cs:1-930)
  - `src/FlowTime.Cli/FlowTime.Cli.csproj` - CLI runner (src/FlowTime.Cli/Program.cs:1-261)
  - `src/FlowTime.Contracts/` - Shared contracts
  - `src/FlowTime.Adapters.Synthetic/` - Artifact readers

### Entry Points
**CLI**: `dotnet run --project src/FlowTime.Cli -- run <model.yaml> --out <dir> [--verbose] [--deterministic-run-id] [--seed N]`
- Located: src/FlowTime.Cli/Program.cs:1-50
- Validates YAML schema (src/FlowTime.Core/Models/ModelValidator.cs:47-60)
- Parses to internal model (src/FlowTime.Core/Models/ModelParser.cs:1-165)
- Evaluates graph
- Writes artifacts via `RunArtifactWriter` (src/FlowTime.Core/Artifacts/RunArtifactWriter.cs:34-204)

**API Server**: `dotnet run --project src/FlowTime.API` (port 8080)
- Located: src/FlowTime.API/Program.cs:1-930
- Endpoints implemented:
  - `GET /healthz` - basic health (src/FlowTime.API/Program.cs:76)
  - `GET /v1/healthz` - service info (src/FlowTime.API/Program.cs:79-84)
  - `POST /v1/run` - evaluate model, return series JSON (src/FlowTime.API/Program.cs:419-537)
  - `POST /v1/graph` - return DAG topology (src/FlowTime.API/Program.cs:540-577)
  - Artifact registry endpoints: GET/POST /v1/artifacts/* (src/FlowTime.API/Program.cs:88-416)
  - Export endpoints: POST/GET /v1/runs/{id}/export (src/FlowTime.API/Program.cs:689-829)

**Build Status**: ✅ `dotnet build` succeeds (0 warnings, 0 errors)

**Test Status**: 🚧 393 tests total, **390 passing, 3 failing** (performance benchmarks only)
- Failed tests are M2/M15 performance threshold tests (non-blocking)
- Core functionality tests: 100% pass rate
- Test files: 79 .cs files across 5 test projects

---

## 2. Core Data Model ✅

### TimeGrid
**Location**: src/FlowTime.Core/TimeGrid.cs:54-75

```csharp
public readonly record struct TimeGrid {
    public int Bins { get; }           // Number of time bins
    public int BinSize { get; }        // Duration per bin
    public TimeUnit BinUnit { get; }   // Unit (minutes/hours/days/weeks)
    public int BinMinutes { get; }     // Computed: bin duration in minutes
    public int TotalMinutes => Bins * BinMinutes;
}
```

**Features**:
- ✅ Supports `binSize` + `binUnit` (NEW schema): src/FlowTime.Core/TimeGrid.cs:63-74
- ❌ NO support for legacy `binMinutes` (correctly rejected per M2.9 schema policy)
- ✅ Validation: bins ∈ [1, 10000], binSize ∈ [1, 1000]
- ✅ Units: minutes, hours, days, weeks (TimeUnit enum)

### Series<T>
**Location**: src/FlowTime.Core/Series.cs:5-26

```csharp
public sealed class Series {
    private readonly double[] data;
    public int Length => data.Length;
    public double this[int t] { get; set; }
    public double[] ToArray() => (double[])data.Clone();
}
```

**Features**:
- ✅ Simple double array wrapper
- ✅ Immutable via Clone() export
- ✅ Zero-based bin indexing
- ⚠️ No metadata (units, node provenance) attached to Series object itself

### Hashing
**Location**: src/FlowTime.Core/Artifacts/RunArtifactWriter.cs:206-212

```csharp
private static string ComputeScenarioHash(string specText, int? rngSeed, double? startTimeBias)
{
    var content = $"{specText}|{rngSeed}|{startTimeBias}";
    var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
    return Convert.ToHexString(bytes).ToLowerInvariant();
}
```

**Features**:
- ✅ SHA256 hash of (spec + seed + bias)
- ✅ Used for deterministic run IDs and deduplication
- ✅ Exposed in artifacts as `modelHash` field

---

## 3. DAG/Expression Engine ✅

### Parser Features
**Location**: src/FlowTime.Core/Expressions/ExpressionParser.cs:29-309

**Operators**: `+`, `-`, `*`, `/` (binary ops)
- Implementation: src/FlowTime.Core/Expressions/ExpressionParser.cs:67-113
- BinaryOp evaluation: src/FlowTime.Core/Expressions/ExprNode.cs:45-65

**Functions Implemented**:
1. ✅ **SHIFT(series, lag)** - temporal shift with state
   - Parser: src/FlowTime.Core/Expressions/ExprNode.cs:79-114
   - Stateful node: src/FlowTime.Core/Nodes/ShiftNode.cs:1-76
   - Tests: tests/FlowTime.Tests/Nodes/ShiftNodeTests.cs (12 tests)
   
2. ✅ **MIN(a, b)** - element-wise minimum
   - Implementation: src/FlowTime.Core/Expressions/ExprNode.cs:116-133

3. ✅ **MAX(a, b)** - element-wise maximum
   - Implementation: src/FlowTime.Core/Expressions/ExprNode.cs:135-152

4. ✅ **CLAMP(x, min, max)** - constrain values
   - Implementation: src/FlowTime.Core/Expressions/ExprNode.cs:154-172

5. ❌ **ABS** - NOT implemented
6. ❌ **SQRT** - NOT implemented
7. ❌ **POW** - NOT implemented

**Additional Capabilities Searched But Not Found**:
- ❌ EMA (exponential moving average)
- ❌ DELAY (different from SHIFT)
- ❌ Conditional expressions (IF/ELSE)

### Topological Sort
**Location**: src/FlowTime.Core/Graph.cs:60-87

```csharp
public IReadOnlyList<NodeId> TopologicalOrder() {
    // Kahn's algorithm
    var inDegree = new Dictionary<NodeId, int>();
    // ... (computes in-degrees)
    var queue = new Queue<NodeId>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
    // BFS to produce order
    if (order.Count != nodes.Count) 
        throw new InvalidOperationException("Graph has a cycle");
    return order;
}
```

**Features**:
- ✅ Kahn's algorithm for topo sort
- ✅ Returns ordered list of NodeIds
- ✅ Used by `Graph.Evaluate()` to determine execution order

### Cycle Detection
**Location**: src/FlowTime.Core/Graph.cs:14-44

```csharp
private void ValidateAcyclic() {
    // Kahn's algorithm to detect cycles
    // ... (same structure as TopologicalOrder)
    if (visited != nodes.Count) 
        throw new InvalidOperationException("Graph has a cycle");
}
```

**Features**:
- ✅ Called in Graph constructor (fail-fast)
- ✅ Throws `InvalidOperationException` on cycles
- ✅ Tests: tests/FlowTime.Tests/ModelValidation/ValidationTests.cs (cycle detection tests)

---

## 4. Node Types Implemented ✅/❌

### Implemented Nodes

#### ConstSeriesNode ✅
**Location**: src/FlowTime.Core/Nodes.Const.cs:1-22

```csharp
public sealed class ConstSeriesNode : INode {
    public ConstSeriesNode(string id, double[] values) { ... }
    public Series Evaluate(TimeGrid grid, Func<NodeId, Series> getInput) {
        if (values.Length != grid.Length)
            throw new ArgumentException(...);
        return new Series((double[])values.Clone());
    }
}
```

**Usage**: Define constant time-series from YAML `values:` array

#### ExprNode ✅
**Location**: src/FlowTime.Core/Expressions/ExprNode.cs:6-173

**Features**:
- Evaluates parsed expression AST
- Supports operator overloading (+, -, *, /)
- Function dispatch (SHIFT, MIN, MAX, CLAMP)
- Node reference resolution via `getInput`

**Usage**: YAML `expr:` field with mathematical expressions

#### PmfNode ✅
**Location**: src/FlowTime.Core/Pmf/PmfNode.cs:6-74

```csharp
public class PmfNode : INode {
    public Pmf Pmf { get; }
    public Series Evaluate(TimeGrid grid, Func<NodeId, Series> getInput) {
        var values = new double[grid.Bins];
        Array.Fill(values, Pmf.ExpectedValue);  // Constant expected value
        return new Series(values);
    }
}
```

**Features**:
- ✅ Accepts PMF dictionary: `{ "value": probability }`
- ✅ Computes expected value: Σ(value × prob)
- ✅ Returns **constant series** (all bins = expected value)
- ⚠️ NO per-bin sampling yet (M2+ feature)
- ✅ RNG support exists: src/FlowTime.Core/Rng/Pcg32.cs (PCG32 PRNG)
- ⚠️ RNG not yet wired into PmfNode evaluation

**PMF Compiler**: src/FlowTime.Core/Pmf/PmfCompiler.cs:11-133
- Validates probabilities sum to ~1.0
- Renormalizes with warnings if needed
- Supports repeat policies for tiling short PMFs to grid length

#### ShiftNode ✅ (Stateful)
**Location**: src/FlowTime.Core/Nodes/ShiftNode.cs:8-74

```csharp
public class ShiftNode : IStatefulNode {
    private Queue<double> history = new();
    
    public void InitializeState(TimeGrid grid) {
        history.Clear();
        for (int i = 0; i < lag; i++) history.Enqueue(0.0);
    }
    
    public Series Evaluate(TimeGrid grid, Func<NodeId, Series> getInput) {
        // Shift series by 'lag' bins into future
        // result[i] = source[i - lag] if i >= lag else 0
    }
}
```

**Features**:
- ✅ Implements `IStatefulNode` interface (src/FlowTime.Core/IStatefulNode.cs:4-11)
- ✅ Maintains history queue
- ✅ `InitializeState()` resets between runs
- ✅ SHIFT(x, 0) = identity, SHIFT(x, 1) = [0, x[0], x[1], ...]

### Missing Node Types ❌

**Searched extensively. NONE of these exist:**

#### BacklogNode ❌
- **Expected location**: `src/FlowTime.Core/Nodes/BacklogNode.cs` (NOT FOUND)
- **Expected formula**: `Q[t] = max(0, Q[t-1] + inflow[t] - capacity[t])`
- **Search results**: Only references in UI templates and test fixtures, NO engine implementation
- Files mentioning "backlog": tests/FlowTime.Adapters.Synthetic.Tests/AdapterExampleTests.cs (test data only)

#### QueueNode ❌
- **Expected location**: `src/FlowTime.Core/Nodes/QueueNode.cs` (NOT FOUND)
- **Search results**: "queue" only appears in Graph.cs for Kahn's algorithm (topological sort)
- No queueing mechanics in engine

#### CapacityNode ❌
- **Expected location**: `src/FlowTime.Core/Nodes/CapacityNode.cs` (NOT FOUND)
- **Search results**: "capacity" only in UI template examples (ui/FlowTime.UI/Services/TemplateServiceImplementations.cs)
- No capacity constraint evaluation

#### RouterNode ❌
- **Expected location**: `src/FlowTime.Core/Nodes/RouterNode.cs` (NOT FOUND)
- No flow splitting/routing logic

#### BatchNode ❌
- **Expected location**: `src/FlowTime.Core/Nodes/BatchNode.cs` (NOT FOUND)
- No batching aggregation

#### DLQNode ❌ (Dead Letter Queue)
- **Expected location**: `src/FlowTime.Core/Nodes/DLQNode.cs` (NOT FOUND)

#### AutoscaleNode ❌
- **Expected location**: `src/FlowTime.Core/Nodes/AutoscaleNode.cs` (NOT FOUND)

#### RetryNode ❌
- **Expected location**: `src/FlowTime.Core/Nodes/RetryNode.cs` (NOT FOUND)

---

## 5. Backlog/Latency Math ❌

### Queue Dynamics: NOT IMPLEMENTED

**Expected Formula**:
```
Q[t] = max(0, Q[t-1] + inflow[t] - capacity[t])
```

**Status**: ❌ **NO stateful queue nodes exist**

**Evidence**:
- Searched all files for "Q[t" pattern: 0 matches
- Searched for "backlog" in src/FlowTime.Core: 0 matches
- Only stateful node is `ShiftNode` (temporal shift, not accumulation)

### Little's Law: NOT IMPLEMENTED

**Expected**: Latency = Queue Length / Throughput

**Status**: ❌ No latency calculation nodes

**Evidence**:
- Searched for "Little" or "latency" in Core: 0 matches in node implementations
- Latency only mentioned in UI template examples for demonstration purposes

### Utilization: NOT IMPLEMENTED

**Expected**: Utilization = Demand / Capacity

**Status**: ❌ No utilization calculation beyond manual expr nodes

**Current Workaround**:
- Users can write `expr: "demand / capacity"` manually
- No built-in capacity-aware nodes

### Conservation Laws: NOT ENFORCED

**Expected**: Total inflow = Total outflow + Total queued

**Status**: ❌ No validation or conservation checks

---

## 6. Time-Travel Readiness ❌

### State Query Endpoints: NOT IMPLEMENTED

Searched for `/state`, `state_window`, `/state?ts=` patterns:
- ❌ **No matches in src/FlowTime.API/Program.cs**

**Expected but Missing**:
```
GET /v1/runs/{runId}/state?ts=<binIndex>
GET /v1/runs/{runId}/state_window?start=<bin>&end=<bin>
```

### Implemented Endpoints (Post-Execution Only)

#### POST /v1/run ✅
**Location**: src/FlowTime.API/Program.cs:419-537

**Input**: YAML model definition (text/plain)

**Output**: JSON with complete evaluation results
```json
{
  "grid": { "bins": N, "binSize": X, "binUnit": "minutes" },
  "order": ["node1", "node2", ...],
  "series": {
    "node1": [v0, v1, v2, ...],
    "node2": [v0, v1, v2, ...]
  },
  "runId": "run_20251007T...",
  "artifactsPath": "/path/to/artifacts",
  "modelHash": "sha256:..."
}
```

**Features**:
- ✅ Full-grid evaluation (all bins computed at once)
- ✅ Returns all series data in single response
- ✅ Creates artifact directory automatically
- ✅ Adds to registry in background task
- ❌ NO partial evaluation (can't query single bin)
- ❌ NO state checkpointing (can't resume mid-evaluation)

#### POST /v1/graph ✅
**Location**: src/FlowTime.API/Program.cs:540-577

**Output**: DAG topology (nodes, edges, order)
```json
{
  "nodes": ["node1", "node2", ...],
  "order": ["node1", "node2", ...],  // topological order
  "edges": [
    { "id": "node1", "inputs": [] },
    { "id": "node2", "inputs": ["node1"] }
  ]
}
```

**Features**:
- ✅ Exposes graph structure
- ✅ Useful for visualization
- ❌ NO runtime state (static structure only)

#### GET /v1/runs/{runId}/series/{seriesId} ✅
**Location**: src/FlowTime.API/Program.cs:658-687

**Output**: CSV time-series for specific node
```csv
t,value
0,10.5
1,12.3
2,11.7
...
```

**Features**:
- ✅ Reads from artifact files
- ✅ Supports fuzzy matching (demand → demand@DEMAND@DEFAULT)
- ❌ NO window queries (must return full series)

### State Window API: NOT IMPLEMENTED ❌

**Expected Shape**:
```
GET /v1/runs/{runId}/state_window?start=100&end=200
```

**Should Return**:
```json
{
  "runId": "run_...",
  "window": {
    "start": 100,
    "end": 200
  },
  "series": {
    "demand": [v100, v101, ..., v200],
    "served": [v100, v101, ..., v200],
    "backlog": [v100, v101, ..., v200]
  }
}
```

**Status**: ❌ Endpoint does not exist

**Workaround**: Client must fetch full series via `/runs/{id}/series/{name}` and slice locally

---

## 7. Artifacts System ✅

### File Layout
**Written by**: src/FlowTime.Core/Artifacts/RunArtifactWriter.cs:34-204

**Structure**:
```
data/
  run_20251007T142315Z_a1b2c3d4/
    spec.yaml              # Input model definition
    run.json               # Run metadata + schema
    manifest.json          # Legacy metadata (kept for compatibility)
    provenance.json        # Optional: UI template provenance
    series/
      index.json           # Series catalog
      demand@DEMAND@DEFAULT.csv
      served@SERVED@DEFAULT.csv
      backlog@BACKLOG@DEFAULT.csv
      ...
    gold/                  # Export formats (M2.6)
      export.csv           # Wide-format CSV (all series)
      export.ndjson        # Newline-delimited JSON
      export.parquet       # Parquet columnar format
```

### run.json Schema ✅
**Location**: src/FlowTime.Core/Artifacts/RunArtifactWriter.cs:216-228

```json
{
  "schemaVersion": 1,
  "grid": { "bins": 24, "binSize": 1, "binUnit": "hours" },
  "runId": "run_...",
  "timestamp": "2025-10-07T14:23:15.123Z",
  "scenarioHash": "sha256:...",
  "specFile": "spec.yaml",
  "engineVersion": "0.1.0",
  "warnings": [],
  "series": [
    { "id": "demand", "kind": "DEMAND", "path": "series/demand@DEMAND@DEFAULT.csv" },
    ...
  ],
  "rng": { "algorithm": "pcg32", "seed": 42 },
  "provenance": { "path": "provenance.json" }  // Optional
}
```

**Features**:
- ✅ `schemaVersion: 1` enforced (src/FlowTime.Core/Models/ModelValidator.cs:58-60)
- ✅ Grid uses NEW schema: `binSize` + `binUnit` (not `binMinutes`)
- ✅ Scenario hash for deduplication
- ✅ RNG seed tracking (when provided)
- ✅ Provenance tracking (UI templates → Engine → Sim)

### series/index.json Schema ✅
**Location**: src/FlowTime.Core/Artifacts/RunArtifactWriter.cs:254

```json
{
  "schemaVersion": 1,
  "grid": { "bins": 24, "binSize": 1, "binUnit": "hours" },
  "series": [
    {
      "id": "demand@DEMAND@DEFAULT",
      "path": "demand@DEMAND@DEFAULT.csv",
      "unit": "entities",
      "format": "csv",
      "hash": "sha256:..."
    }
  ],
  "formats": {
    "gold": { "path": "gold/export.csv" },
    "ndjson": { "path": "gold/export.ndjson" },
    "parquet": { "path": "gold/export.parquet" }
  }
}
```

### Per-Series CSV Format ✅
**Location**: src/FlowTime.Core/Artifacts/RunArtifactWriter.cs:95-104

```csv
t,value
0,10.5
1,12.3
2,11.7
...
```

**Features**:
- ✅ Header row: `t,value`
- ✅ 0-based bin indexing
- ✅ Invariant culture formatting
- ✅ Line endings: `\n` (Unix-style)

### Parquet Support ✅
**Location**: src/FlowTime.API/Services/ParquetExporter.cs

**Features**:
- ✅ Columnar format with schema
- ✅ All series as columns, bins as rows
- ✅ Written to `gold/export.parquet`
- ✅ Accessible via `GET /v1/runs/{id}/export/parquet`

---

## 8. Target Model Schema Support ✅

### Schema Version
**Enforcement**: src/FlowTime.Core/Models/ModelValidator.cs:47-60

```csharp
if (!rawModel.ContainsKey("schemaVersion"))
    errors.Add("schemaVersion is required");
// ...
else if (schemaVersion != 1)
    errors.Add("schemaVersion must be 1");
```

**Status**: ✅ Enforces `schemaVersion: 1`

### binSize/binUnit Parsing ✅
**Location**: src/FlowTime.Core/Models/ModelParser.cs:23-27

```csharp
if (model.Grid.BinSize <= 0 || string.IsNullOrEmpty(model.Grid.BinUnit))
    throw new ModelParseException("Grid must specify binSize and binUnit");

var unit = TimeUnitExtensions.Parse(model.Grid.BinUnit);
var grid = new TimeGrid(model.Grid.Bins, model.Grid.BinSize, unit);
```

**Status**: ✅ Requires NEW schema (binSize + binUnit)

**Supported Units**:
- `minutes`, `hours`, `days`, `weeks`
- Case-insensitive parsing

### PMF Support ✅
**Location**: src/FlowTime.Core/Models/ModelParser.cs:95-122

**YAML Format**:
```yaml
- id: demand
  kind: pmf
  pmf:
    "10": 0.2
    "20": 0.5
    "30": 0.3
```

**Features**:
- ✅ Parses `pmf:` dictionary
- ✅ String keys converted to double values
- ✅ Validates probabilities
- ✅ Computes expected value
- ⚠️ Returns constant series (no per-bin variation yet)

### RNG Support ✅
**Implementation**: src/FlowTime.Core/Rng/Pcg32.cs:1-82

**Features**:
- ✅ PCG32 algorithm (fast, high-quality PRNG)
- ✅ Seeded initialization
- ✅ `NextDouble()`, `NextGaussian()` methods
- ✅ Seed written to `run.json`
- ⚠️ NOT yet used by PmfNode (M2+ feature)

### Provenance Support ✅
**Location**: src/FlowTime.API/Services/ProvenanceService.cs:1-135

**Features**:
- ✅ Extracts provenance from HTTP headers or embedded YAML
- ✅ Tracks: templateId, modelId, generatedBy, receivedAt
- ✅ Written to `provenance.json` in artifacts
- ✅ Referenced in `run.json`
- ✅ Powers UI template → Engine → Sim workflow

---

## 9. Tests Status 🚧

### Test Coverage

**Total Tests**: 393 tests across 79 test files

**Pass Rate**: 390/393 passing (99.2%)

**Failing Tests (3)**:
1. `FlowTime.Tests.Performance.M2PerformanceTests.Test_PMF_Grid_Size_Scaling` - performance threshold
2. `FlowTime.Tests.Performance.M2PerformanceTests.Test_PMF_Complexity_Scaling` - performance threshold
3. `FlowTime.Tests.Performance.M15PerformanceTests.Test_SmallScale_Performance` - performance threshold

**Status**: ✅ All functional tests passing; only perf benchmarks failing (non-blocking)

### Test Categories

#### Core Tests ✅
- **TimeGrid**: tests/FlowTime.Tests/TimeGridTests/ (8 tests)
- **Series**: tests/FlowTime.Tests/DeterminismTests.cs
- **Graph/DAG**: tests/FlowTime.Tests/ModelValidation/ValidationTests.cs
- **Topological Sort**: Covered in Graph tests
- **Cycle Detection**: tests/FlowTime.Tests/ModelValidation/ValidationTests.cs:23

#### Node Tests ✅
- **ShiftNode**: tests/FlowTime.Tests/Nodes/ShiftNodeTests.cs (12 tests)
  - Lag 0 (identity), Lag 1, Lag 2, Lag > length
  - Negative lag rejection
  - State initialization
- **BinaryOp**: tests/FlowTime.Tests/BinaryOpTests.cs (4 tests)
- **Expression**: tests/FlowTime.Tests/Expressions/ (multiple files)
  - Parser tests
  - Integration tests (SHIFT, MIN, MAX, CLAMP)

#### Artifact Tests ✅
- **RunArtifactWriter**: tests/FlowTime.Tests/RunArtifactWriterTests.cs (5 tests)
  - Deterministic run IDs
  - Schema compliance
  - Hash uniqueness
- **Adapter**: tests/FlowTime.Adapters.Synthetic.Tests/ (8 tests)
  - Reading artifacts
  - Index parsing
  - Series CSV parsing

#### API Tests ✅
- **Integration Tests**: tests/FlowTime.Api.Tests/ (99 tests)
  - `/run` endpoint
  - `/graph` endpoint
  - Artifact registry endpoints
  - Export endpoints (CSV, NDJSON, Parquet)
  - Provenance tracking

#### CLI Tests ✅
- **End-to-End**: tests/FlowTime.Cli.Tests/ (10 tests)
  - Output directory configuration
  - Artifact production

#### UI Tests ✅
- **Template Service**: tests/FlowTime.UI.Tests/ (58 tests)
  - Template metadata
  - Parameter conversion
  - Schema validation

### Coverage Hotspots

**Well-Covered**:
- ✅ Core evaluation (Grid, Series, Graph)
- ✅ Expression parsing and evaluation
- ✅ Artifact writing and reading
- ✅ API endpoints (run, graph, artifacts)

**Under-Covered**:
- ⚠️ Error handling edge cases
- ⚠️ Large-scale performance (10k+ nodes)
- ⚠️ Concurrent API usage

**Not Covered** (features don't exist):
- ❌ Stateful queue nodes
- ❌ Capacity constraints
- ❌ State window queries

---

## 10. Known TODOs/Tech Debt

### Search Results
**Pattern**: `TODO|FIXME|WARN|XXX|HACK`

**Count**: 50 matches across codebase

### Critical TODOs

#### src/FlowTime.Core/Artifacts/RunArtifactWriter.cs:139
```csharp
EngineVersion = "0.1.0", // TODO: derive from assembly
```
**Impact**: Version hardcoded, should read from AssemblyInfo

#### Warnings (Non-Blocking)
- PMF renormalization warnings (expected behavior)
- Registry index rebuild warnings (graceful recovery)
- File access warnings (handled exceptions)

### No Critical FIXMEs Found ✅

---

## 11. Gaps/Risks for 5-Day Time-Travel Demo

### 🔴 CRITICAL GAPS (Blocking)

#### 1. No State Query Endpoints ❌
**Required**: `GET /v1/runs/{id}/state_window?start=X&end=Y`

**Current Behavior**: 
- Must call POST /v1/run to evaluate entire grid
- Must download full CSV files via GET /v1/runs/{id}/series/{name}
- Client-side slicing required

**Impact**: Cannot demonstrate "query any time window" capability

**Suggested Implementation**:
- File: `src/FlowTime.API/Program.cs` (add new endpoint)
- Read artifact CSVs, slice [start:end] range
- Return JSON with windowed series data

**Estimated Complexity**: 2-4 hours (read existing CSVs, slice, JSON response)

#### 2. No Backlog/Queue Nodes ❌
**Required**: `Q[t] = max(0, Q[t-1] + inflow[t] - capacity[t])`

**Current Workaround**:
- Users must write complex expr with SHIFT: `expr: "MAX(0, SHIFT(queue, 1) + demand - capacity)"`
- Manual state management
- Error-prone

**Impact**: Cannot demo "backlog accumulates when demand > capacity"

**Suggested Implementation**:
- File: `src/FlowTime.Core/Nodes/BacklogNode.cs` (new file)
- Implement `IStatefulNode` (like ShiftNode)
- Formula: `Q[t] = max(0, Q[t-1] + inflow[t] - capacity[t])`
- Add to ModelParser switch case

**Estimated Complexity**: 4-8 hours (stateful node + tests)

#### 3. No Capacity Node Type ❌
**Required**: Explicit capacity constraint node

**Current Workaround**:
- Use const nodes with manual values
- Or expr nodes with MIN(demand, capacity)

**Impact**: Verbose models, no semantic distinction

**Suggested Implementation**:
- Could be a special case of ConstSeriesNode
- Or add `kind: capacity` with validation

**Estimated Complexity**: 2-4 hours (mostly naming/validation)

### 🟡 MEDIUM GAPS (Nice-to-Have)

#### 4. No Latency Calculation ⚠️
**Expected**: Automatic Little's Law: `Latency = Queue / Throughput`

**Current Workaround**: Manual expr node

**Impact**: Requires domain expertise

**Estimated Complexity**: 4-6 hours (new node type + validation)

#### 5. No Per-Bin PMF Sampling ⚠️
**Current**: PMF returns constant expected value

**Expected**: Per-bin random sampling with RNG

**Impact**: Less realistic stochastic behavior

**Estimated Complexity**: 8-12 hours (integrate Pcg32 RNG into PmfNode)

#### 6. No Conservation Validation ⚠️
**Expected**: Assert Σinflow = Σoutflow + Σqueued

**Impact**: Silent bugs in complex models

**Estimated Complexity**: 6-10 hours (post-evaluation validation pass)

### 🟢 LOW GAPS (Polish)

#### 7. Hardcoded Engine Version
**Location**: src/FlowTime.Core/Artifacts/RunArtifactWriter.cs:139

**Fix**: Read from AssemblyInfo

**Estimated Complexity**: 30 minutes

#### 8. No /state Endpoint Structure
**Even if implemented**, unclear what "state" means without stateful nodes

**Options**:
- Return series values at single bin: `{ "demand": 10, "served": 8 }`
- Return series slice: `{ "demand": [10, 11, 12] }`
- Return stateful node internal state: `{ "queue_history": [...] }`

**Recommendation**: Start with series slice (simplest)

---

## 12. Recommended 5-Day Plan

### Day 1-2: State Window API ✅
**Goal**: Enable `GET /v1/runs/{id}/state_window?start=X&end=Y`

**Steps**:
1. Add endpoint to src/FlowTime.API/Program.cs
2. Read series CSVs from artifact directory
3. Parse, slice [start:end], return JSON
4. Add integration tests

**Outcome**: Can query any time window from past runs

### Day 3-4: BacklogNode Implementation ✅
**Goal**: Enable `Q[t] = max(0, Q[t-1] + inflow - capacity)`

**Steps**:
1. Create src/FlowTime.Core/Nodes/BacklogNode.cs
2. Implement IStatefulNode
3. Add to ModelParser switch
4. Write unit tests (like ShiftNodeTests)
5. Add integration test with sample YAML

**Outcome**: Can demonstrate queue buildup/drain

### Day 5: Demo Scenario + Polish
**Goal**: End-to-end demo with backlog visualization

**Steps**:
1. Create example model: demand spike → queue builds → drain
2. Test state_window queries at key timestamps
3. Generate charts (client-side or via UI)
4. Document API usage

**Outcome**: Compelling time-travel demo

---

## 13. Summary Table

| Feature | Status | Location | Notes |
|---------|--------|----------|-------|
| **Build/Run** | ✅ | - | Builds clean, 99% tests pass |
| **TimeGrid (binSize/binUnit)** | ✅ | src/FlowTime.Core/TimeGrid.cs:54-75 | NEW schema enforced |
| **Series<T>** | ✅ | src/FlowTime.Core/Series.cs:5-26 | Simple double array |
| **SHA256 Hashing** | ✅ | src/FlowTime.Core/Artifacts/RunArtifactWriter.cs:206-212 | Scenario deduplication |
| **Expression Parser** | ✅ | src/FlowTime.Core/Expressions/ | +,-,*,/ operators |
| **SHIFT Function** | ✅ | src/FlowTime.Core/Nodes/ShiftNode.cs | Stateful, tested |
| **MIN/MAX/CLAMP** | ✅ | src/FlowTime.Core/Expressions/ExprNode.cs | Element-wise ops |
| **ABS/SQRT/POW** | ❌ | - | Not implemented |
| **Topological Sort** | ✅ | src/FlowTime.Core/Graph.cs:60-87 | Kahn's algorithm |
| **Cycle Detection** | ✅ | src/FlowTime.Core/Graph.cs:14-44 | Fail-fast in constructor |
| **ConstNode** | ✅ | src/FlowTime.Core/Nodes.Const.cs | Values array |
| **ExprNode** | ✅ | src/FlowTime.Core/Expressions/ExprNode.cs | Math expressions |
| **PmfNode** | ✅ | src/FlowTime.Core/Pmf/PmfNode.cs | Expected value only |
| **ShiftNode** | ✅ | src/FlowTime.Core/Nodes/ShiftNode.cs | Temporal lag |
| **BacklogNode** | ❌ | - | **CRITICAL GAP** |
| **QueueNode** | ❌ | - | No queueing mechanics |
| **CapacityNode** | ❌ | - | Use const/expr workaround |
| **RouterNode** | ❌ | - | No flow splitting |
| **Q[t] Formula** | ❌ | - | **CRITICAL GAP** |
| **Little's Law** | ❌ | - | Manual expr only |
| **Conservation Laws** | ❌ | - | No validation |
| **POST /v1/run** | ✅ | src/FlowTime.API/Program.cs:419-537 | Full evaluation |
| **POST /v1/graph** | ✅ | src/FlowTime.API/Program.cs:540-577 | DAG topology |
| **GET /v1/runs/{id}/series/{name}** | ✅ | src/FlowTime.API/Program.cs:658-687 | Full series CSV |
| **GET /state?ts=** | ❌ | - | **CRITICAL GAP** |
| **GET /state_window?start&end** | ❌ | - | **CRITICAL GAP** |
| **run.json** | ✅ | src/FlowTime.Core/Artifacts/ | SchemaVersion 1 |
| **series/index.json** | ✅ | src/FlowTime.Core/Artifacts/ | Series catalog |
| **Per-series CSV** | ✅ | src/FlowTime.Core/Artifacts/ | t,value format |
| **Parquet Export** | ✅ | src/FlowTime.API/Services/ParquetExporter.cs | Columnar format |
| **SchemaVersion=1** | ✅ | src/FlowTime.Core/Models/ModelValidator.cs:58 | Enforced |
| **PMF Parsing** | ✅ | src/FlowTime.Core/Models/ModelParser.cs:95-122 | Dict to Pmf |
| **RNG (Pcg32)** | ✅ | src/FlowTime.Core/Rng/Pcg32.cs | Not wired to PMF yet |
| **Provenance Tracking** | ✅ | src/FlowTime.API/Services/ProvenanceService.cs | Template → Engine → Sim |
| **Unit Tests** | ✅ | tests/ | 390/393 passing |
| **API Integration Tests** | ✅ | tests/FlowTime.Api.Tests/ | 99 tests |

---

## 14. Conclusion

**Strengths**:
- ✅ Rock-solid deterministic evaluation engine
- ✅ Clean expression language with SHIFT
- ✅ Excellent artifact system (M1/M2.6 compliant)
- ✅ Comprehensive test coverage
- ✅ API-first architecture
- ✅ NEW schema enforcement (binSize/binUnit)

**Weaknesses**:
- ❌ No backlog/queue node types (stateful accumulation)
- ❌ No time-window query endpoints
- ❌ PMF returns constant series (no per-bin sampling)
- ❌ Missing ABS/SQRT/POW functions (low priority)

**Time-Travel Demo Feasibility**:
- **Without new work**: ⚠️ **Partial** - Can show full-run results, no windowed queries
- **With 5-day sprint**: ✅ **Achievable** - Add state_window endpoint + BacklogNode

**Recommended Priority**:
1. **Day 1-2**: State window API (src/FlowTime.API/Program.cs)
2. **Day 3-4**: BacklogNode (src/FlowTime.Core/Nodes/BacklogNode.cs)
3. **Day 5**: Demo scenario + documentation

**Risk Assessment**: 🟢 **LOW RISK** if scope limited to above 2 items. Existing codebase is stable and well-tested.

---

**End of Audit Report**
