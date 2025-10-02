# FlowTime Model Schema (Target)

## Overview

This document defines the **target schema** for FlowTime models - the unified format that both FlowTime-Sim will generate and FlowTime Engine will execute. This schema is the convergence point for M2.9 schema evolution.

**Key Principles**:
- **Engine-First**: Optimized for execution, not authoring
- **Human-Readable**: Uses `binSize`/`binUnit` instead of `binMinutes`
- **Minimal & Complete**: Only fields needed for execution
- **Extensible**: Supports future engine capabilities
- **Version-Safe**: Explicit schema versioning for evolution

## Relationship to Other Schemas

```
┌─────────────────────┐
│  Template Schema    │  (FlowTime-Sim authoring format)
│  - metadata         │  - Rich authoring features
│  - parameters       │  - Parameter substitution
│  - dependencies     │  - Documentation fields
│  - descriptions     │
└──────────┬──────────┘
           │ Generate
           ▼
┌─────────────────────┐
│   Model Schema      │  (This document - execution format)
│   - schemaVersion   │  ✅ Engine executes this
│   - grid            │  ✅ Sim outputs this
│   - nodes           │  ✅ Unified format
│   - outputs         │
│   - rng             │
└──────────┬──────────┘
           │ Execute
           ▼
┌─────────────────────┐
│  FlowTime Engine    │
│  POST /run          │
└─────────────────────┘
```

**Migration Impact**:
- FlowTime-Sim: Remove `binSize`/`binUnit` → `binMinutes` conversion
- FlowTime Engine: Add `binSize`/`binUnit` → minutes conversion internally

## Content Type

- **Format**: YAML (primary), JSON (supported)
- **Character Encoding**: UTF-8
- **HTTP Content-Type**: `text/plain` (YAML body) or `application/json`

## Root Model Structure

```yaml
schemaVersion: 1
grid:
  bins: <integer>
  binSize: <integer>
  binUnit: <minutes|hours|days|weeks>
nodes:
  - id: <string>
    kind: <const|expr|pmf>
    # ... kind-specific fields
outputs:
  - series: <string>
    as: <string>
rng:
  kind: <string>
  seed: <integer>
```

## Schema Components

### 1. Schema Version

```yaml
schemaVersion: 1
```

**Properties:**
- `schemaVersion` (required): Integer, currently `1`
- Enables schema evolution and backward compatibility detection

**Rules:**
- Must be first field in YAML for clarity
- Engine must validate and reject unsupported versions
- Future versions may add/change fields

**Version History:**
- `1`: Initial unified schema with `binSize`/`binUnit` format (M2.9)

---

### 2. Grid Definition

```yaml
grid:
  bins: 24
  binSize: 1
  binUnit: hours
```

**Properties:**
- `bins` (required): Integer, number of time periods (1-10000)
- `binSize` (required): Integer, duration of each period (1-1000)
- `binUnit` (required): Enum, time unit for `binSize`

**Supported Time Units:**
- `minutes` - Minutes (1-1440 practical range)
- `hours` - Hours (1-24 practical range)
- `days` - Days (1-365 practical range)
- `weeks` - Weeks (1-52 practical range)

**Common Patterns:**
```yaml
# Hourly for a day
bins: 24
binSize: 1
binUnit: hours

# 15-minute intervals for 4 hours
bins: 16
binSize: 15
binUnit: minutes

# Daily for a year
bins: 365
binSize: 1
binUnit: days

# Weekly for a year
bins: 52
binSize: 1
binUnit: weeks
```

**Conversion to Minutes (Engine Internal):**
```
totalMinutes = binSize × unitMultiplier
where:
  minutes → 1
  hours   → 60
  days    → 1440
  weeks   → 10080
```

**Rules:**
- `bins` must be positive integer
- `binSize` must be positive integer
- `binUnit` must be one of supported values
- All `const` node values arrays must have exactly `bins` elements
- Practical limit: `bins × binSize` should not exceed reasonable simulation horizons

**Rationale for binSize/binUnit over binMinutes:**
1. **Human-readable**: "1 hour" is clearer than "60 minutes"
2. **Flexible**: Supports weeks, months (future) natively
3. **Intuitive**: Matches how users think about time
4. **Extensible**: Easy to add new units (months, years)

---

### 3. Nodes Array

```yaml
nodes:
  - id: demand
    kind: const
    values: [100, 120, 150, 130]
  - id: served
    kind: expr
    expr: "MIN(demand, capacity)"
```

**Common Properties:**
- `id` (required): String, unique node identifier
  - Pattern: `^[a-zA-Z_][a-zA-Z0-9_]*$`
  - Examples: `demand`, `served_customers`, `avg_latency`
- `kind` (required): Enum, node type: `const`, `expr`, `pmf`

**Node ID Rules:**
- Must be unique within model
- Must be valid identifier (no spaces, start with letter/underscore)
- Referenced by other nodes via expressions
- Case-sensitive

#### 3.1 Constant Nodes (`kind: const`)

```yaml
- id: demand
  kind: const
  values: [100, 120, 150, 130, 110, 90, 80, 100]
```

**Properties:**
- `values` (required): Array of numbers

**Rules:**
- Array length must equal `grid.bins` exactly
- Values can be integers or floats
- Represents deterministic time series

**Use Cases:**
- Known demand patterns
- Fixed capacity constraints
- Historical data
- Deterministic inputs

#### 3.2 Expression Nodes (`kind: expr`)

```yaml
- id: served
  kind: expr
  expr: "MIN(demand, capacity)"

- id: backlog
  kind: expr
  expr: "MAX(0, arrivals - served + SHIFT(backlog, 1))"
```

**Properties:**
- `expr` (required): String, expression formula

**Supported Syntax:**
- **Arithmetic**: `+`, `-`, `*`, `/`
- **Functions**:
  - `MIN(a, b)` - Minimum of two values
  - `MAX(a, b)` - Maximum of two values
  - `SHIFT(series, lag)` - Time-shifted reference (enables feedback loops)
  - `ABS(a)` - Absolute value
  - `SQRT(a)` - Square root
  - `POW(a, b)` - Power (a^b)
- **References**: Use node IDs directly (e.g., `demand`, `capacity`)
- **Literals**: Numeric constants (e.g., `100`, `0.5`)

**Expression Evaluation:**
- Evaluated bin-by-bin by Engine
- Dependencies resolved via topological sort
- `SHIFT` enables feedback loops (previous bin value)
- All referenced node IDs must exist

**Rules:**
- No circular dependencies (except via `SHIFT`)
- Expression syntax validated by Engine
- Type errors detected at runtime
- Division by zero returns `NaN` or `Infinity`

**Use Cases:**
- Derived metrics (utilization, throughput)
- Capacity constraints (MIN/MAX)
- Stateful calculations (backlog tracking with SHIFT)
- Business logic (pricing, allocation)

#### 3.3 PMF Nodes (`kind: pmf`)

```yaml
- id: random_failures
  kind: pmf
  pmf:
    values: [0, 1, 2, 3]
    probabilities: [0.7, 0.2, 0.08, 0.02]
```

**Properties:**
- `pmf` (required): Object containing probability mass function
  - `values` (required): Array of numbers, possible outcomes
  - `probabilities` (required): Array of numbers 0-1, must sum to 1.0

**Rules:**
- `values` and `probabilities` arrays must have same length
- Probabilities must sum to 1.0 (within tolerance: ±0.0001)
- Each bin gets independent sample from distribution
- Sampling performed by Engine (not pre-sampled by Sim)

**Sampling Behavior:**
- Each time bin gets a fresh random sample
- Uses RNG configuration if provided
- Independent across bins (no correlation)

**Use Cases:**
- Stochastic arrivals (Poisson-like distributions)
- Random failures/downtime
- Variable processing times
- Uncertainty modeling

**Example - Discrete Uniform:**
```yaml
- id: dice_roll
  kind: pmf
  pmf:
    values: [1, 2, 3, 4, 5, 6]
    probabilities: [0.1667, 0.1667, 0.1667, 0.1667, 0.1666, 0.1666]
```

**Example - Skewed Distribution:**
```yaml
- id: arrival_rate
  kind: pmf
  pmf:
    values: [50, 100, 150, 200]
    probabilities: [0.1, 0.4, 0.4, 0.1]  # Peak around 100-150
```

---

### 4. Outputs Array

```yaml
outputs:
  - series: demand
    as: demand.csv
  - series: served
    as: served.csv
```

**Properties:**
- `series` (required): String, node ID to output
- `as` (required): String, output filename

**Rules:**
- `series` must reference existing node ID
- `as` must be valid filename (typically `.csv` extension)
- Output section is optional (API may ignore for JSON response)
- Multiple outputs can reference same series with different names

**Use Cases:**
- CLI mode: Write results to CSV files
- API mode: May be ignored (results returned in response)
- Debugging: Export intermediate calculations

**Note**: Engine behavior varies by mode:
- **CLI**: Writes CSV files to specified locations
- **API**: Returns JSON response, may ignore `outputs` section

---

### 5. RNG Configuration

```yaml
rng:
  kind: pcg32
  seed: 12345
```

**Properties:**
- `kind` (optional): String, RNG algorithm name
- `seed` (optional): Integer, random seed for reproducibility

**Supported RNG Algorithms:**
- `pcg32` - PCG family, 32-bit output (default)
- `mt19937` - Mersenne Twister (future)
- `xoshiro256` - Xoshiro family (future)

**Rules:**
- Entire `rng` section is optional
- If omitted, engine uses default RNG with random seed
- Seed enables deterministic execution (same input → same output)
- Seed must be non-negative integer

**Use Cases:**
- **Reproducible simulations**: Same seed yields same results
- **Testing**: Deterministic behavior for unit tests
- **Sensitivity analysis**: Compare runs with different seeds
- **Debugging**: Isolate stochastic vs deterministic issues

**Future Extensions:**
- Per-node RNG configuration
- Multiple RNG streams
- Distribution-specific parameters

---

### 6. Provenance Metadata (Optional)

```yaml
provenance:
  source: flowtime-sim
  model_id: model_20250925T120000Z_abc123def
  template_id: it-system-microservices
  template_version: "1.0"
  generated_at: "2025-09-25T12:00:00Z"
  generator: "flowtime-sim/0.4.0"
  parameters:
    bins: 12
    binSize: 1
    binUnit: hours
    loadBalancerCapacity: 300
```

**Properties:**
- `source` (optional): String, system that generated this model
- `model_id` (optional): String, unique identifier for this model instance
- `template_id` (optional): String, template identifier if generated from template
- `template_version` (optional): String, template version used
- `generated_at` (optional): String (ISO 8601), generation timestamp
- `generator` (optional): String, generator name and version
- `parameters` (optional): Object, template parameters used during generation

**Purpose:**
- **Traceability**: Track model lineage from template to execution
- **Reproducibility**: Record parameters used for generation
- **Debugging**: Identify model source when issues occur
- **Audit**: Maintain complete execution history

**Engine Behavior:**
- Provenance accepted via **HTTP header** (`X-Model-Provenance`) OR **embedded in YAML**
- **Header takes precedence** if both present (logs warning)
- Provenance **stripped from `spec.yaml`** before storage
- Stored permanently in **`provenance.json`** artifact
- **Excluded from `model_hash`** calculation (models with same logic but different provenance have same hash)

**Alternative: HTTP Header**
```http
POST /v1/run
X-Model-Provenance: model_20250925T120000Z_abc123def
Content-Type: application/x-yaml

schemaVersion: 1
# ... model without provenance section
```

**Rules:**
- Entire `provenance` section is optional
- All fields within provenance are optional
- Free-form `parameters` object (template-specific)
- `model_id` should follow pattern: `model_YYYYMMDDTHHmmssZ_<hash>`

**Use Cases:**
- **Template-generated models**: FlowTime-Sim embeds provenance automatically
- **Manual models**: Users can add provenance for documentation
- **UI workflows**: UI can track model creation context
- **Self-contained files**: Model + provenance travels together

**See Also:**
- [Run Provenance Architecture](../architecture/run-provenance.md)
- [Registry Integration](../../flowtime-sim-vnext/docs/architecture/registry-integration.md)

---

## Complete Example

```yaml
schemaVersion: 1

grid:
  bins: 8
  binSize: 1
  binUnit: hours

nodes:
  # Constant demand pattern (hourly)
  - id: demand
    kind: const
    values: [100, 120, 150, 180, 160, 140, 110, 100]

  # Fixed capacity
  - id: capacity
    kind: const
    values: [200, 200, 200, 200, 200, 200, 200, 200]

  # Random failures (PMF)
  - id: failures
    kind: pmf
    pmf:
      values: [0, 1, 2]
      probabilities: [0.8, 0.15, 0.05]

  # Effective capacity accounting for failures
  - id: effective_capacity
    kind: expr
    expr: "capacity - failures"

  # Served requests (constrained by demand and effective capacity)
  - id: served
    kind: expr
    expr: "MIN(demand, effective_capacity)"

  # Utilization metric
  - id: utilization
    kind: expr
    expr: "served / capacity * 100"

  # Backlog tracking with feedback loop
  - id: backlog
    kind: expr
    expr: "MAX(0, demand - served + SHIFT(backlog, 1))"

outputs:
  - series: served
    as: served.csv
  - series: utilization
    as: utilization.csv
  - series: backlog
    as: backlog.csv

rng:
  kind: pcg32
  seed: 42
```

---

## Validation Rules

### Structural Validation

1. **Required fields**: `schemaVersion`, `grid`, `nodes`
2. **Grid requirements**: `bins`, `binSize`, `binUnit` all required
3. **Nodes array**: Must have at least one node
4. **Node uniqueness**: All node IDs must be unique

### Grid Validation

1. **bins**: Positive integer, practical limit 10000
2. **binSize**: Positive integer, practical limit 1000
3. **binUnit**: Must be one of: `minutes`, `hours`, `days`, `weeks`
4. **Total duration**: `bins × binSize × unitMultiplier` should be reasonable

### Node Validation

1. **const nodes**: `values` array length must equal `grid.bins`
2. **expr nodes**: Must have `expr` field with valid expression syntax
3. **pmf nodes**: `values` and `probabilities` must have equal length
4. **PMF probabilities**: Must sum to 1.0 (within tolerance ±0.0001)

### Reference Validation

1. **Expression references**: All node IDs in `expr` strings must exist
2. **Output references**: All `series` values must reference existing nodes
3. **Dependency order**: No circular dependencies (except via `SHIFT`)
4. **Topological sort**: Engine must be able to order nodes for evaluation

### Type Validation

1. **bins**: Integer 1-10000
2. **binSize**: Integer 1-1000
3. **binUnit**: Enum value
4. **values**: Array of numbers
5. **probabilities**: Array of numbers between 0 and 1
6. **seed**: Non-negative integer

---

## Error Handling

### Invalid Examples

#### Missing binUnit
```yaml
# ❌ INVALID - missing binUnit
grid:
  bins: 24
  binSize: 1
  # ERROR: binUnit required
```

#### Wrong node field names
```yaml
# ❌ INVALID - should be 'expr' not 'expression'
- id: served
  kind: expr
  expression: "demand * 0.8"  # ERROR: use 'expr'
```

#### Mismatched array lengths
```yaml
# ❌ INVALID - values length doesn't match bins
grid:
  bins: 8
  binSize: 1
  binUnit: hours
nodes:
  - id: demand
    kind: const
    values: [100, 120]  # ERROR: should have 8 elements
```

#### Invalid PMF probabilities
```yaml
# ❌ INVALID - probabilities don't sum to 1.0
- id: failures
  kind: pmf
  pmf:
    values: [0, 1, 2]
    probabilities: [0.5, 0.3, 0.1]  # ERROR: sums to 0.9
```

#### Circular dependency
```yaml
# ❌ INVALID - circular reference
- id: a
  kind: expr
  expr: "b + 1"
- id: b
  kind: expr
  expr: "a + 1"  # ERROR: circular dependency
```

#### Invalid time unit
```yaml
# ❌ INVALID - unsupported time unit
grid:
  bins: 12
  binSize: 1
  binUnit: months  # ERROR: not supported (yet)
```

---

## Migration from Current Schema

### From binMinutes to binSize/binUnit

**Old Format (Current Engine Input):**
```yaml
grid:
  bins: 24
  binMinutes: 60
```

**New Format (Target Schema):**
```yaml
grid:
  bins: 24
  binSize: 1
  binUnit: hours
```

**Conversion Table:**

| Old (binMinutes) | New (binSize/binUnit) |
|------------------|----------------------|
| `binMinutes: 1` | `binSize: 1, binUnit: minutes` |
| `binMinutes: 5` | `binSize: 5, binUnit: minutes` |
| `binMinutes: 15` | `binSize: 15, binUnit: minutes` |
| `binMinutes: 60` | `binSize: 1, binUnit: hours` |
| `binMinutes: 120` | `binSize: 2, binUnit: hours` |
| `binMinutes: 1440` | `binSize: 1, binUnit: days` |
| `binMinutes: 10080` | `binSize: 1, binUnit: weeks` |

**Migration Strategy:**
1. Engine adds internal conversion: `binSize` × `binUnit` → minutes
2. Engine validates new format, rejects old format
3. FlowTime-Sim removes conversion, outputs new format directly
4. Examples updated to new format
5. Documentation updated throughout

---

## Implementation Guidance

### For FlowTime-Sim

**Changes Required:**
1. Remove `ConvertGridToEngineFormat()` conversion logic
2. Output `binSize`/`binUnit` directly from template
3. Update tests to expect new format
4. Update example models

**Before (Current):**
```csharp
// Convert template format to engine format
var binMinutes = ConvertToBinMinutes(binSize, binUnit);
yaml = yaml.Replace($"binSize: {binSize}", "");
yaml = yaml.Replace($"binUnit: {binUnit}", $"binMinutes: {binMinutes}");
```

**After (Target):**
```csharp
// No conversion needed - pass through binSize/binUnit
// Engine handles conversion internally
return yaml;
```

### For FlowTime Engine

**Changes Required:**
1. Add `TimeUnit` enum: `Minutes`, `Hours`, `Days`, `Weeks`
2. Add conversion method: `TimeUnit.ToMinutes(binSize, binUnit)`
3. Update parser to read `binSize`/`binUnit` instead of `binMinutes`
4. Update validation to check new fields
5. Internal execution still uses minutes (no change to core logic)

**New Code:**
```csharp
public enum TimeUnit
{
    Minutes,
    Hours,
    Days,
    Weeks
}

public static class TimeUnitExtensions
{
    public static int ToMinutes(this TimeUnit unit, int binSize)
    {
        return unit switch
        {
            TimeUnit.Minutes => binSize,
            TimeUnit.Hours => binSize * 60,
            TimeUnit.Days => binSize * 1440,
            TimeUnit.Weeks => binSize * 10080,
            _ => throw new ArgumentException($"Unsupported time unit: {unit}")
        };
    }
}
```

**Parser Update:**
```csharp
// Old
var binMinutes = gridNode["binMinutes"].GetValue<int>();

// New
var binSize = gridNode["binSize"].GetValue<int>();
var binUnit = Enum.Parse<TimeUnit>(gridNode["binUnit"].GetValue<string>(), ignoreCase: true);
var binMinutes = binUnit.ToMinutes(binSize);
```

---

## Future Extensions

### Planned Enhancements (Post-M2.9)

1. **Additional Time Units**:
   - `months` - Calendar months (28-31 days)
   - `years` - Calendar years (365-366 days)

2. **Grid Anchoring**:
   ```yaml
   grid:
     bins: 24
     binSize: 1
     binUnit: hours
     start: "2025-01-01T00:00:00Z"  # Wall-clock alignment
   ```

3. **Advanced RNG**:
   ```yaml
   rng:
     kind: pcg32
     seed: 12345
     streams: 4  # Multiple independent streams
   ```

4. **Node Metadata** (optional documentation):
   ```yaml
   - id: demand
     kind: const
     description: "Daily customer demand"  # Optional
     values: [100, 120, 150]
   ```

5. **Expression Enhancements**:
   - `SUM(series)` - Sum across all bins
   - `AVG(series)` - Average across all bins
   - `CUMSUM(series)` - Cumulative sum
   - `LAG(series, n)` - Multi-bin lag

6. **PMF Policies**:
   ```yaml
   pmf:
     values: [0, 1, 2]
     probabilities: [0.7, 0.2, 0.1]
     policy: independent  # vs 'correlated', 'seasonal'
   ```

---

## Schema Versioning Strategy

### Version 1 (Current Target)

- `binSize`/`binUnit` grid format
- Three node types: `const`, `expr`, `pmf`
- Basic expression syntax
- Simple RNG configuration

### Future Versions

**Version 2** (Potential):
- Calendar-aware time units (`months`, `years`)
- Grid anchoring with `start` timestamp
- Advanced expression functions

**Version 3** (Potential):
- Conditional expressions (`IF`, `SWITCH`)
- Vector operations
- Custom distribution types beyond PMF

**Backward Compatibility**:
- Engine must validate `schemaVersion` field
- Reject unsupported versions with clear error
- Consider supporting N-1 version for gradual migration

---

## Rationale & Design Decisions

### Why binSize/binUnit over binMinutes?

1. **Human Readability**: "1 hour" is more intuitive than "60 minutes"
2. **Flexibility**: Native support for hours, days, weeks
3. **Extensibility**: Easy to add months, years, quarters
4. **User Mental Model**: Matches how people think about time
5. **Reduced Errors**: Less mental math converting to minutes

### Why expr not expression?

1. **Brevity**: Shorter field name, less typing
2. **Clarity**: Signals executable code, not English description
3. **Consistency**: Common in DSLs (SQL, GraphQL use short keywords)
4. **Parsing**: Easier to distinguish from metadata fields

### Why series/as in outputs?

1. **Clarity**: `series` clearly indicates source node
2. **Flexibility**: `as` allows custom output names
3. **Separation**: Distinct from node `id` field
4. **Extensibility**: Can add output formatting options later

### Why keep RNG section?

1. **Reproducibility**: Essential for testing and debugging
2. **Future-Proof**: Room for advanced RNG features
3. **Explicit**: Makes stochastic behavior visible
4. **Optional**: Doesn't burden simple models

---

## Related Documentation

- **Template Schema**: See `template-schema.md` in FlowTime-Sim docs
- **Engine Implementation**: See `docs/architecture/time-grid.md`
- **API Specification**: See `docs/api/endpoints.md`
- **Migration Guide**: See `docs/migrations/m2.9-schema-evolution.md`
- **Examples**: See `examples/` directory for model samples

---

## Changelog

### 2025-10-01 - Initial Target Schema (M2.9)
- Defined unified model schema for Engine and Sim convergence
- Specified `binSize`/`binUnit` format replacing `binMinutes`
- Documented three node types: `const`, `expr`, `pmf`
- Established validation rules and error handling
- Provided migration guidance from current schema
