# FlowTime Engine Input Schema

## Overview

The FlowTime Engine accepts **YAML** input via the `POST /run` endpoint. This document defines the authoritative schema for model definitions that the engine can execute.

**Important**: This is the **Engine Input Schema**. FlowTime-Sim uses a different **Template Schema** with `binSize`/`binUnit` format, but translates to this engine format during model generation.

## Content Type

- **Input Format**: `text/plain` with YAML body
- **HTTP Method**: `POST /run`
- **Response Format**: `application/json`

## Schema Definition

### Root Model Structure

```yaml
schemaVersion: 1  # Optional, defaults to 1
grid:
  bins: 24          # Required: Number of time periods
  binMinutes: 60    # Required: Duration of each time period in minutes
nodes:              # Required: Array of node definitions
  - id: demand      # Required: Unique node identifier
    kind: const     # Required: Node type (const, expr, pmf)
    values: [...]   # Required for const: Array of values
  - id: served
    kind: expr
    expr: "demand * 0.8"  # Required for expr: Expression string
outputs:            # Optional: CSV output definitions (ignored by API)
  - series: served
    as: served.csv
rng:                # Optional: RNG config (ignored by Engine)
  kind: pcg32
  seed: 12345
```

### Grid Definition

```yaml
grid:
  bins: 24          # Integer: Number of time bins (1-8760)
  binMinutes: 60    # Integer: Minutes per bin (1-1440)
```

**Rules:**
- `bins`: Must be positive integer, practical limit ~8760 (1 year hourly)
- `binMinutes`: Must be positive integer, common values: 1, 5, 15, 60, 1440
- All node `values` arrays must have exactly `bins` elements

**Note**: The engine uses `binMinutes` format. FlowTime-Sim templates use `binSize`/`binUnit` format but translate to `binMinutes` during model generation.

### Node Types

#### Constant Nodes (`const`)

```yaml
- id: demand
  kind: const
  values: [10, 20, 30, 40]  # Must match grid.bins length
```

#### Expression Nodes (`expr`)

```yaml
- id: served
  kind: expr
  expr: "MIN(demand, capacity)"  # String expression
```

**Supported Operators:**
- Arithmetic: `+`, `-`, `*`, `/`
- Functions: `MIN(a,b)`, `MAX(a,b)`, `SHIFT(series,lag)`
- References: Use other node IDs directly

#### PMF Nodes (`pmf`)

```yaml
- id: random_arrivals
  kind: pmf
  pmf:
    values: [0, 1, 2, 3, 4]        # Possible values
    probabilities: [0.1, 0.2, 0.4, 0.2, 0.1]  # Must sum to 1.0
```

### Example Models

#### Minimal Model

```yaml
grid:
  bins: 4
  binMinutes: 60
nodes:
  - id: demand
    kind: const
    values: [10, 20, 30, 40]
  - id: served
    kind: expr
    expr: "demand * 0.8"
```

#### Complex Model with Dependencies

```yaml
schemaVersion: 1
grid:
  bins: 24
  binMinutes: 60
nodes:
  - id: arrivals
    kind: const
    values: [50, 30, 20, 15, 10, 15, 25, 45, 80, 120, 150, 180, 200, 190, 170, 160, 140, 110, 90, 75, 65, 60, 55, 50]
  
  - id: capacity
    kind: const
    values: [200, 200, 200, 200, 200, 200, 200, 200, 200, 200, 200, 200, 200, 200, 200, 200, 200, 200, 200, 200, 200, 200, 200, 200]
  
  - id: served
    kind: expr
    expr: "MIN(arrivals, capacity)"
  
  - id: backlog
    kind: expr
    expr: "MAX(0, arrivals - served + SHIFT(backlog, 1))"
```

## Validation Rules

### Required Fields
- `grid.bins` (integer > 0)
- `grid.binMinutes` (integer > 0)
- `nodes` (non-empty array)
- Each node must have `id` and `kind`

### Node-Specific Requirements
- **const nodes**: Must have `values` array with length = `grid.bins`
- **expr nodes**: Must have `expr` string
- **pmf nodes**: Must have `pmf.values` and `pmf.probabilities` arrays

### Constraints
- Node IDs must be unique
- Node IDs must be valid identifiers (alphanumeric + underscore)
- Expression references must point to existing nodes
- No circular dependencies (except via `SHIFT`)
- PMF probabilities must sum to 1.0

## Error Responses

The engine returns `400 Bad Request` with JSON error for invalid input:

```json
{
  "error": "Node 'served' references undefined node 'demand'"
}
```

Common errors:
- Missing required fields
- Invalid node references
- Array length mismatches
- Circular dependencies
- Invalid expressions

## Integration with FlowTime-Sim

FlowTime-Sim translates its template format to this engine schema:

1. **Template → Generation**: Sim generates values from stochastic parameters
2. **Schema Translation**: Converts to engine format
3. **Engine Execution**: Engine processes the YAML and returns telemetry

See [`template-schema.md`](template-schema.md) for template format differences.

## Schema Evolution

- **Current Version**: `schemaVersion: 1`
- **Backward Compatibility**: Engine supports models without `schemaVersion` (defaults to 1)
- **Future Versions**: Will include migration path and deprecation notices

## See Also

- [Template Schema](template-schema.md) - FlowTime-Sim template format
- [API Reference](/docs/api/engine-api.md) - Complete API documentation
- [Model Examples](/examples/) - Complete working examples
