# FlowTime-Sim Model Output Schema

## Overview

FlowTime-Sim generates **YAML models** that are compatible with the FlowTime Engine's input requirements. This document defines the schema for models produced by FlowTime-Sim's template generation process.

**Relationship**: This is the **output format** from FlowTime-Sim's `GenerateEngineModelAsync()` method, which becomes the **input format** for FlowTime Engine's `POST /run` endpoint.

## Key Differences from Template Schema

FlowTime-Sim templates use a richer authoring format that includes:
- `binSize` + `binUnit` (converted to `binMinutes`)
- `expression` field name (converted to `expr`)
- `source`/`filename` in outputs (converted to `series`/`as`)
- `metadata` and `parameters` sections (stripped during generation)

This document describes the **generated output** after all conversions.

## Content Type

- **Format**: YAML
- **Character Encoding**: UTF-8
- **Usage**: Direct input to FlowTime Engine `POST /run` endpoint

## Root Model Structure

```yaml
schemaVersion: 1
grid:
  bins: <integer>
  binMinutes: <integer>
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
- `schemaVersion` (required): Integer, always `1` in current version
- Used for future schema evolution and compatibility

**Rules:**
- Always included by FlowTime-Sim at the top of generated models
- Engine defaults to `1` if omitted

---

### 2. Grid Definition

```yaml
grid:
  bins: 24
  binMinutes: 60
```

**Properties:**
- `bins` (required): Integer, number of time periods (1-8760)
- `binMinutes` (required): Integer, duration of each period in minutes (1-1440)

**Conversion from Template:**
```yaml
# Template format:
grid:
  bins: 24
  binSize: 1
  binUnit: hours

# Generated output:
grid:
  bins: 24
  binMinutes: 60  # Calculated: 1 hour × 60 minutes
```

**Common binMinutes values:**
- `1` - One minute bins
- `5` - Five minute bins
- `15` - Fifteen minute bins (quarter hour)
- `60` - One hour bins
- `1440` - One day bins

**Rules:**
- All `const` node values arrays must have exactly `bins` elements
- `binMinutes` must be positive integer
- Practical `bins` limit ~8760 (one year of hourly data)

---

### 3. Nodes Array

```yaml
nodes:
  - id: demand
    kind: const
    values: [100, 120, 150, 130]
  
  - id: capacity
    kind: const
    values: [200, 200, 200, 200]
  
  - id: served
    kind: expr
    expr: "MIN(demand, capacity)"
  
  - id: random_failures
    kind: pmf
    pmf:
      values: [0, 1, 2]
      probabilities: [0.7, 0.2, 0.1]
```

**Common Properties:**
- `id` (required): String, unique node identifier, must match `^[a-zA-Z_][a-zA-Z0-9_]*$`
- `kind` (required): Enum - `const`, `expr`, or `pmf`

#### 3.1 Constant Nodes (`kind: const`)

```yaml
- id: demand
  kind: const
  values: [100, 120, 150, 130, 110, 90, 80, 100]
```

**Properties:**
- `values` (required): Array of numbers, must have exactly `grid.bins` elements

**Rules:**
- Each value can be integer or float
- Array length must match `grid.bins` exactly
- Used for deterministic time series data

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

**Conversion from Template:**
```yaml
# Template format:
- id: served
  kind: expr
  expression: "MIN(demand, capacity)"  # Note: 'expression'
  dependencies: [demand, capacity]      # Stripped during generation

# Generated output:
- id: served
  kind: expr
  expr: "MIN(demand, capacity)"        # Note: 'expr'
```

**Supported Expression Syntax:**
- **Arithmetic**: `+`, `-`, `*`, `/`
- **Functions**: 
  - `MIN(a, b)` - Minimum of two values
  - `MAX(a, b)` - Maximum of two values
  - `SHIFT(series, lag)` - Time-shifted reference (enables feedback loops)
- **References**: Use other node IDs directly (e.g., `demand`, `capacity`)

**Rules:**
- All referenced node IDs must exist
- No circular dependencies (except via `SHIFT`)
- Expression evaluated by Engine, not by Sim

#### 3.3 PMF Nodes (`kind: pmf`)

```yaml
- id: random_failures
  kind: pmf
  pmf:
    values: [0, 1, 2, 3]
    probabilities: [0.5, 0.3, 0.15, 0.05]
```

**Properties:**
- `pmf` (required): Object containing probability mass function
  - `values` (required): Array of numbers, possible values
  - `probabilities` (required): Array of numbers 0-1, must sum to 1.0

**Rules:**
- `values` and `probabilities` arrays must have same length
- Probabilities must sum to 1.0 (within floating-point tolerance)
- PMF compiled by Engine, not pre-sampled by Sim
- Each bin gets independent sample from PMF distribution

**Usage Pattern:**
```yaml
# Stochastic arrival rate with discrete probability distribution
- id: arrival_rate
  kind: pmf
  pmf:
    values: [50, 100, 150, 200]      # Possible rates
    probabilities: [0.1, 0.4, 0.4, 0.1]  # Low/Normal/Normal/High
```

---

### 4. Outputs Array

```yaml
outputs:
  - series: demand
    as: demand.csv
  
  - series: served
    as: served.csv
  
  - series: backlog
    as: backlog.csv
```

**Properties:**
- `series` (required): String, node ID to output
- `as` (required): String, output filename (must end in `.csv`)

**Conversion from Template:**
```yaml
# Template format:
outputs:
  - id: served                # Stripped during generation
    source: served
    filename: served.csv
    description: "..."        # Stripped during generation

# Generated output:
outputs:
  - series: served
    as: served.csv
```

**Rules:**
- `series` must reference an existing node ID
- `as` must be valid filename ending in `.csv`
- Outputs section is optional (Engine may ignore for API usage)

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

**Rules:**
- Entire `rng` section is optional
- Engine may ignore RNG config (reserved for future use)
- FlowTime-Sim passes through from templates unchanged

---

## Complete Example

```yaml
schemaVersion: 1

grid:
  bins: 8
  binMinutes: 60

nodes:
  # Constant demand pattern (hourly)
  - id: demand
    kind: const
    values: [100, 120, 150, 180, 160, 140, 110, 100]
  
  # Fixed capacity
  - id: capacity
    kind: const
    values: [200, 200, 200, 200, 200, 200, 200, 200]
  
  # Served demand (capped by capacity)
  - id: served
    kind: expr
    expr: "MIN(demand, capacity)"
  
  # Unserved demand
  - id: unserved
    kind: expr
    expr: "MAX(0, demand - served)"
  
  # Random system failures (PMF)
  - id: failures
    kind: pmf
    pmf:
      values: [0, 1, 2]
      probabilities: [0.8, 0.15, 0.05]

outputs:
  - series: served
    as: served.csv
  
  - series: unserved
    as: unserved.csv

rng:
  kind: pcg32
  seed: 42
```

---

## Validation Rules

### Structural Validation

1. **Required top-level fields**: `grid`, `nodes`
2. **Grid requirements**: Both `bins` and `binMinutes` must be present
3. **Nodes array**: Must have at least one node
4. **Node identifiers**: Must be unique within model

### Node-Specific Validation

1. **Constant nodes**: `values` array length must equal `grid.bins`
2. **Expression nodes**: Must have `expr` field (not `expression`)
3. **PMF nodes**: `values` and `probabilities` must have equal length, probabilities sum to 1.0

### Reference Validation

1. **Expression references**: All node IDs in `expr` strings must exist
2. **Output references**: All `series` values must reference existing node IDs
3. **Circular dependencies**: Not allowed except through `SHIFT` function

### Type Validation

1. **bins**: Positive integer (1-8760)
2. **binMinutes**: Positive integer (1-1440)
3. **values**: Array of numbers
4. **probabilities**: Array of numbers between 0 and 1

---

## Generation Process

FlowTime-Sim's `NodeBasedTemplateService.GenerateEngineModelAsync()` performs these transformations:

### Step 1: Parameter Substitution
```yaml
# Template with parameters:
values: [${min_value}, ${max_value}]

# After substitution:
values: [100, 200]
```

### Step 2: Grid Conversion
```yaml
# Template format:
grid:
  bins: 24
  binSize: 2
  binUnit: hours

# After conversion:
grid:
  bins: 24
  binMinutes: 120  # 2 hours × 60 minutes
```

### Step 3: Schema Cleanup
- Strip `metadata:` section
- Strip `parameters:` section
- Convert `expression:` → `expr:`
- Convert `source:`/`filename:` → `series:`/`as:` in outputs
- Remove `dependencies:` fields from nodes
- Remove `id:` fields from outputs
- Remove `description:` fields
- Ensure `schemaVersion: 1` is present

---

## Error Handling

### Invalid Output Examples

#### Missing binMinutes
```yaml
# ❌ INVALID - missing binMinutes
grid:
  bins: 24
  binSize: 1
  binUnit: hours  # Not valid in output schema
```

#### Wrong expression field name
```yaml
# ❌ INVALID - should be 'expr' not 'expression'
- id: served
  kind: expr
  expression: "demand * 0.8"
```

#### Invalid output format
```yaml
# ❌ INVALID - should be 'series' and 'as'
outputs:
  - source: served
    filename: served.csv
```

#### Extra fields from template
```yaml
# ❌ INVALID - dependencies not allowed in output
- id: served
  kind: expr
  expr: "demand * 0.8"
  dependencies: [demand]  # Should be stripped
```

---

## Relationship to Other Schemas

```
┌─────────────────────────┐
│  Template Schema        │
│  (Authoring Format)     │
│  - binSize/binUnit      │
│  - expression           │
│  - source/filename      │
│  - metadata             │
│  - parameters           │
│  - dependencies         │
└───────────┬─────────────┘
            │
            │ GenerateEngineModelAsync()
            │ (FlowTime-Sim)
            ▼
┌─────────────────────────┐
│  Model Output Schema    │  ◄── THIS DOCUMENT
│  (Generated Format)     │
│  - binMinutes           │
│  - expr                 │
│  - series/as            │
│  - No metadata          │
│  - No parameters        │
│  - No dependencies      │
└───────────┬─────────────┘
            │
            │ POST /run
            │ (FlowTime Engine)
            ▼
┌─────────────────────────┐
│  Engine Execution       │
│  - Compiles PMF nodes   │
│  - Evaluates expressions│
│  - Generates telemetry  │
└─────────────────────────┘
```

---

## Version History

### Version 1 (Current)
- Initial schema definition
- Support for `const`, `expr`, and `pmf` node types
- Grid with `bins` and `binMinutes`
- Optional `outputs` and `rng` sections
- `schemaVersion: 1` marker

---

## See Also

- **[Template Schema](../../flowtime-sim-vnext/docs/schemas/template-schema.md)** - FlowTime-Sim template authoring format
- **[Engine Input Schema](engine-input-schema.md)** - FlowTime Engine's input requirements (same as this output)
- **[Template Migration Guide](../../flowtime-sim-vnext/docs/schemas/template-migration.md)** - Converting legacy formats
- **[API Documentation](../api/)** - FlowTime Engine API reference

---

## Appendix: Field Mapping Reference

| Template Field | Generated Field | Notes |
|---|---|---|
| `metadata.*` | *(removed)* | Stripped entirely |
| `parameters.*` | *(removed)* | Stripped entirely |
| `grid.binSize` + `grid.binUnit` | `grid.binMinutes` | Calculated: binSize × unit multiplier |
| `nodes[].expression` | `nodes[].expr` | Renamed |
| `nodes[].dependencies` | *(removed)* | Implicit from expression parsing |
| `outputs[].id` | *(removed)* | Not needed in output |
| `outputs[].source` | `outputs[].series` | Renamed |
| `outputs[].filename` | `outputs[].as` | Renamed |
| `outputs[].description` | *(removed)* | Documentation only |
| `rng.*` | `rng.*` | Passed through unchanged |
| *(none)* | `schemaVersion` | Always added if missing |
