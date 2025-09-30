# Template Schemas

## Overview
Templates define computation models. They are authored in YAML for human readability and can be served as either YAML or JSON via content negotiation. Templates are converted to models by FlowTime-Sim and models are sent to the Engine for computation.

## Validation Responsibilities

**FlowTime-Sim validates**: Template syntax, schema compliance, parameter constraints
**FlowTime Engine validates**: Model semantics, node dependencies, expression syntax, DAG structure

See [SIM-Engine Architectural Boundaries](../architecture/sim-engine-boundaries.md) for complete details.

## Storage and Processing

Storage: YAML format (preserves comments and formatting)
Processing: Convert to JSON internally
API Output: Both YAML and JSON supported

## Content Negotiation
```http
GET /api/v1/templates/{id}
Accept: application/json       # Returns JSON (default)
Accept: application/x-yaml     # Returns YAML
```

## Schema Definition (schema.yaml)

```yaml
# JSON Schema compatible definition for node-based templates
schemaVersion: 1.0
type: object
required: [metadata, grid, nodes, outputs]
properties:
  metadata:
    type: object
    required: [id, title]
    properties:
      id: 
        type: string
        pattern: "^[a-z0-9-]+$"
      title: 
        type: string
      description: 
        type: string
      tags: 
        type: array
        items: 
          type: string

  parameters:
    type: array
    items:
      type: object
      required: [name, type]
      properties:
        name:
          type: string
          pattern: "^[a-zA-Z_][a-zA-Z0-9_]*$"
        type:
          enum: [integer, number, boolean, string, array]
        title:
          type: string
        description:
          type: string
        default:
          # Type must match declared type
        min:
          type: number
        max:
          type: number

  grid:
    type: object
    required: [bins, binSize, binUnit]
    properties:
      bins:
        type: integer
        minimum: 1
      binSize:
        type: integer
        minimum: 1
      binUnit:
        enum: [minutes, hours, days]

  nodes:
    type: array
    items:
      type: object
      required: [id, kind]
      properties:
        id:
          type: string
          pattern: "^[a-zA-Z_][a-zA-Z0-9_]*$"
        kind:
          enum: [const, pmf, expr]
        # For const nodes
        values:
          # Can be array or single value (number, boolean, string)
        # For pmf nodes
        pmf:
          type: object
          required: [values, probabilities]
          properties:
            values:
              type: array
              items:
                type: number
            probabilities:
              type: array
              items:
                type: number
                minimum: 0
                maximum: 1
        # For expr nodes  
        expr:
          type: string

  outputs:
    type: array
    items:
      type: object
      required: [series, filename]
      properties:
        series:
          type: string
        filename:
          type: string
          pattern: "^[a-zA-Z0-9_-]+\\.csv$"
```

## Template Example (YAML version)

```yaml
# IT System with Microservices Template
metadata:
  id: it-system-microservices
  title: "IT System with Microservices"
  description: "Web application with load balancer, auth service, and database with stochastic failures"
  tags: [microservices, web-scale, probabilistic]

parameters:
  - name: bins
    type: integer
    title: "Time Periods"
    description: "Number of time periods to simulate"
    default: 12
    min: 1
    max: 100

  - name: binSize
    type: integer
    title: "Period Duration"
    description: "Duration of each period"
    default: 1
    min: 1
    max: 24

  - name: binUnit
    type: string
    title: "Period Unit"
    description: "Unit of time for each period"
    default: "hours"

  - name: requestPattern
    type: string
    title: "Request Pattern"
    description: "Traffic pattern type"
    default: "uniform"
    enum: [uniform, peak, random]

  - name: rngSeed
    type: integer
    title: "RNG Seed"
    description: "Seed for deterministic random generation"
    default: 12345
    min: 1
    max: 2147483647

  - name: loadBalancerCapacity
    type: number
    title: "Load Balancer Capacity"
    description: "Max requests load balancer can handle per period"
    default: 300
    min: 1
    max: 10000

  - name: authCapacity
    type: number
    title: "Auth Service Capacity"
    description: "Max requests auth service can handle per period"
    default: 250
    min: 1
    max: 10000

  - name: databaseCapacity
    type: number
    title: "Database Capacity"
    description: "Max queries database can handle per period"
    default: 180
    min: 1
    max: 10000

grid:
  bins: ${bins}
  binSize: ${binSize}
  binUnit: ${binUnit}

nodes:
  # Input traffic
  - id: user_requests
    kind: const
    values: ${requestPattern}

  # System component capacities
  - id: load_balancer_capacity
    kind: const
    values: ${loadBalancerCapacity}

  - id: auth_capacity
    kind: const
    values: ${authCapacity}

  - id: database_capacity
    kind: const
    values: ${databaseCapacity}

  # Probabilistic factors (PMF nodes compiled by Engine)
  - id: network_reliability
    kind: pmf
    pmf:
      values: [1.0, 0.9, 0.7, 0.5]
      probabilities: [0.7, 0.15, 0.1, 0.05]
      policy: repeat

  # Database performance variation
  - id: db_performance
    kind: pmf
    pmf:
      values: [0.5, 0.8, 1.0, 1.1]  # Sometimes slow, sometimes cached (faster)
      probabilities: [0.05, 0.25, 0.6, 0.1]
      policy: repeat

  # Effective capacities with probabilistic factors
  - id: effective_lb_capacity
    kind: expr
    expr: "load_balancer_capacity * network_reliability"

  - id: effective_db_capacity
    kind: expr
    expr: "database_capacity * db_performance"

  # Request processing chain
  - id: load_balanced
    kind: expr
    expr: "MIN(user_requests, effective_lb_capacity)"

  - id: authenticated
    kind: expr
    expr: "MIN(load_balanced, auth_capacity)"

  - id: processed
    kind: expr
    expr: "MIN(authenticated, effective_db_capacity)"

  - id: failed
    kind: expr
    expr: "user_requests - processed"

  # Metrics
  - id: utilization
    kind: expr
    expr: "processed / MAX(user_requests, 1) * 100"

  - id: failure_rate
    kind: expr
    expr: "failed / MAX(user_requests, 1) * 100"

  # Summary statistics
  - id: total_requests
    kind: expr
    expr: "SUM(user_requests)"

  - id: total_processed
    kind: expr
    expr: "SUM(processed)"

  - id: total_failed
    kind: expr
    expr: "SUM(failed)"

  - id: avg_utilization
    kind: expr
    expr: "AVG(utilization)"

  - id: max_failure_rate
    kind: expr
    expr: "MAX(failure_rate)"

outputs:
  - series: user_requests
    filename: requests.csv

  - series: processed
    filename: processed.csv

  - series: failed
    filename: failed.csv

  - series: utilization
    filename: utilization.csv

  - series: failure_rate
    filename: failure_rate.csv

  - series: total_requests
    filename: total_requests.csv

  - series: total_processed
    filename: total_processed.csv

  - series: avg_utilization
    filename: avg_utilization.csv

  - series: max_failure_rate
    filename: max_failure_rate.csv

# Optional: RNG configuration for PMF compilation by Engine
rng:
  kind: pcg32
  seed: ${rngSeed}     # Deterministic seed for reproducible results
```

## Node Types

### Const Nodes (Static Values)
Define constant values or arrays:
```yaml
nodes:
  - id: capacity
    kind: const
    values: 100              # Single value (expanded to array)
  
  - id: schedule
    kind: const 
    values: [100, 150, 200]  # Array of values
```

### PMF Nodes (Engine-Compiled Distributions)
Define probability distributions that the Engine compiles to deterministic series:

```yaml
# Template format (FlowTime-Sim)
nodes:
  - id: network_reliability
    kind: pmf
    pmf:
      values: [1.0, 0.9, 0.7, 0.5]
      probabilities: [0.7, 0.15, 0.1, 0.05]
      policy: repeat  # Optional: repeat (default) | error
```

**Engine Model Format** (what FlowTime-Sim generates):
```yaml
# Generated for Engine (preserves PMF spec)
nodes:
  - id: network_reliability
    kind: pmf
    spec:
      length: 4
      values: [1.0, 0.9, 0.7, 0.5]
      probabilities: [0.7, 0.15, 0.1, 0.05]
      policy: "repeat"        # repeat|error (no resampling in v1)
```

**Grid Contract:**
- **Length alignment**: PMF length must divide evenly into grid.bins when using `repeat` policy
- **Error policy**: If length ≠ bins and policy is `error`, Engine fails with clear message
- **No resampling**: v1 supports only `repeat` and `error` policies for simplicity
- **Deterministic compilation**: Engine compiles PMF to const series during ingest

### Expression Nodes (Mathematical Operations)
Define computed values using expressions:
```yaml
nodes:
  - id: utilization
    kind: expr
    expr: "processed / capacity * 100"
    
  - id: effective_capacity
    kind: expr
    expr: "capacity * reliability_factor"
```

## JSON Version (API Response)

```json
{
  "metadata": {
    "id": "it-system-microservices",
    "title": "IT System with Microservices",
    "description": "Web application with load balancer, auth service, and database with stochastic failures",
    "tags": ["microservices", "web-scale", "probabilistic"]
  },
  "parameters": [
    {
      "name": "bins",
      "type": "integer",
      "title": "Time Periods",
      "description": "Number of time periods to simulate",
      "default": 12,
      "min": 1,
      "max": 100
    },
    {
      "name": "binSize",
      "type": "integer",
      "title": "Period Duration",
      "description": "Duration of each period",
      "default": 1,
      "min": 1,
      "max": 24
    },
    {
      "name": "binUnit",
      "type": "string",
      "title": "Period Unit",
      "description": "Unit of time for each period",
      "default": "hours"
    },
    {
      "name": "requestPattern",
      "type": "array",
      "title": "Request Pattern",
      "description": "Requests per period (cycles if shorter than bins)",
      "default": [100, 150, 200, 250, 300, 280, 240, 200, 180, 150, 120, 100]
    },
    {
      "name": "loadBalancerCapacity",
      "type": "number",
      "title": "Load Balancer Capacity",
      "description": "Max requests load balancer can handle per period",
      "default": 300,
      "min": 1,
      "max": 10000
    },
    {
      "name": "authCapacity",
      "type": "number",
      "title": "Auth Service Capacity",
      "description": "Max requests auth service can handle per period",
      "default": 250,
      "min": 1,
      "max": 10000
    },
    {
      "name": "databaseCapacity",
      "type": "number",
      "title": "Database Capacity",
      "description": "Max queries database can handle per period",
      "default": 180,
      "min": 1,
      "max": 10000
    }
  ],
  "grid": {
    "bins": "${bins}",
    "binSize": "${binSize}",
    "binUnit": "${binUnit}"
  },
  "nodes": [
    {
      "id": "user_requests",
      "kind": "const",
      "values": "${requestPattern}"
    },
    {
      "id": "load_balancer_capacity",
      "kind": "const",
      "values": "${loadBalancerCapacity}"
    },
    {
      "id": "auth_capacity",
      "kind": "const",
      "values": "${authCapacity}"
    },
    {
      "id": "database_capacity",
      "kind": "const",
      "values": "${databaseCapacity}"
    },
    {
      "id": "network_reliability",
      "kind": "pmf",
      "pmf": {
        "values": [1.0, 0.9, 0.7, 0.5, 0.0],
        "probabilities": [0.7, 0.15, 0.1, 0.04, 0.01]
      }
    },
    {
      "id": "db_performance",
      "kind": "pmf",
      "pmf": {
        "values": [0.5, 0.8, 1.0, 1.1],
        "probabilities": [0.05, 0.25, 0.6, 0.1]
      }
    },
    {
      "id": "effective_lb_capacity",
      "kind": "expr",
      "expr": "load_balancer_capacity * network_reliability"
    },
    {
      "id": "effective_db_capacity",
      "kind": "expr",
      "expr": "database_capacity * db_performance"
    },
    {
      "id": "load_balanced",
      "kind": "expr",
      "expr": "MIN(user_requests, effective_lb_capacity)"
    },
    {
      "id": "authenticated",
      "kind": "expr",
      "expr": "MIN(load_balanced, auth_capacity)"
    },
    {
      "id": "processed",
      "kind": "expr",
      "expr": "MIN(authenticated, effective_db_capacity)"
    },
    {
      "id": "failed",
      "kind": "expr",
      "expr": "user_requests - processed"
    },
    {
      "id": "utilization",
      "kind": "expr",
      "expr": "processed / MAX(user_requests, 1) * 100"
    },
    {
      "id": "failure_rate",
      "kind": "expr",
      "expr": "failed / MAX(user_requests, 1) * 100"
    },
    {
      "id": "total_requests",
      "kind": "expr",
      "expr": "SUM(user_requests)"
    },
    {
      "id": "total_processed",
      "kind": "expr",
      "expr": "SUM(processed)"
    },
    {
      "id": "total_failed",
      "kind": "expr",
      "expr": "SUM(failed)"
    },
    {
      "id": "avg_utilization",
      "kind": "expr",
      "expr": "AVG(utilization)"
    },
    {
      "id": "max_failure_rate",
      "kind": "expr",
      "expr": "MAX(failure_rate)"
    }
  ],
  "outputs": [
    {
      "series": "user_requests",
      "filename": "requests.csv"
    },
    {
      "series": "processed",
      "filename": "processed.csv"
    },
    {
      "series": "failed",
      "filename": "failed.csv"
    },
    {
      "series": "utilization",
      "filename": "utilization.csv"
    },
    {
      "series": "failure_rate",
      "filename": "failure_rate.csv"
    },
    {
      "series": "total_requests",
      "filename": "total_requests.csv"
    },
    {
      "series": "total_processed",
      "filename": "total_processed.csv"
    },
    {
      "series": "avg_utilization",
      "filename": "avg_utilization.csv"
    }
  ]
}
```

## Key Design Points

- **Three node types currently supported**: `const`, `pmf`, `expr`
- **Grid configuration**: Clean and intuitive with bins, binSize, binUnit
- **No explicit dependencies**: node dependencies inferred from expressions
- **Template variables**: Using `${}` syntax
- **PMF nodes**: Simple values/probabilities structure
- **PMF sampling**: Uses RNG for probabilistic value selection
- **RNG configuration**: Optional `rng` section with PCG32 algorithm and seed
- **Deterministic behavior**: Same seed produces identical results
- **Flat parameters**: No nested objects, keeping it simple
- **CSV outputs only**: Single output format
- **Parameter substitution**: Replace `${variable}` with actual values
- **Expression Validation and Evaluation**: Handled by Engine

## Validation (/validate)

### Structural Validation (FlowTime-Sim)
- Required fields: metadata.id, metadata.title, grid, nodes, outputs
- ID patterns: lowercase alphanumeric with hyphens for metadata.id
- Node IDs: alphanumeric with underscores, must start with letter
- Filename pattern: alphanumeric with hyphens/underscores, .csv extension
- Parameter references: All `${}` variables have corresponding parameters
- PMF structure: Basic checks that values/probabilities arrays exist and same length
- RNG structure: Valid kind and seed format if specified

### Semantic Validation (Engine)
- PMF normalization: Probabilities sum to ≈ 1.0 (ε = 0.001)
- PMF non-negativity: All probabilities ≥ 0
- Grid alignment: PMF length compatibility with grid.bins and policy
- Expression syntax: Valid arithmetic and function calls
- Node dependencies: All referenced nodes exist, no circular dependencies
- Array compatibility: Expression operands have compatible lengths
- RNG seed within valid range (1 to 2147483647) if specified

## Engine Responsibilities

- **PMF compilation**: Convert PMF specs to deterministic const series during ingest
- **Grid alignment**: Handle PMF length vs grid.bins with repeat/error policies
- **PMF validation**: Probability normalization, non-negativity, length validation
- **RNG management**: Use PCG32 with specified seed for deterministic PMF compilation
- **Expression evaluation**: Process deterministic expressions including PMF references
- **Provenance tracking**: Record PMF specs and compiled series hashes
- **Array handling**: Cycle/expand arrays for expression operands
- **Node processing**: Parse expressions to determine node dependencies
- **Execution order**: Determine node evaluation sequence
- **Computation execution**: Run deterministic model computation
- **Output generation**: Create CSV files from computed results



## Validation (by Engine)

- **PMF compilation**: Probabilities sum to ≈ 1.0 (ε = 0.001) with optional renormalization
- **Grid alignment**: PMF length vs grid.bins compatibility with repeat/error policies
- **PMF properties**: Non-negativity, array length matching
- **Expression syntax**: Valid arithmetic and function calls
- **Node dependencies**: All referenced nodes exist, no circular dependencies
- **Array compatibility**: Expression operands have compatible lengths
- **RNG configuration**: Seed within valid range, kind is `pcg32`
- **Provenance**: Record PMF specs and compiled series hashes for explainability

## PMF Compilation Pipeline (Engine)

The Engine compiles PMF nodes to deterministic const series during model ingest:

### 1. Validation
- Non-negativity: All probabilities ≥ 0
- Normalization: Σ probabilities ≈ 1.0 within ε=0.001
- Length matching: values.length == probabilities.length
- Optional renormalization with warning in logs

### 2. Grid Alignment
- If `length == grid.bins`: use PMF as-is
- If `policy == repeat` and `bins % length == 0`: tile PMF to grid size
- If `policy == error`: fail with `TIMEGRID_MISMATCH` error
- No implicit resampling in v1

### 3. Compilation
- Produce internal const series with deterministic ordering
- Apply RNG seed for any stochastic sampling (if needed)
- Replace PMF node with compiled const series internally

### 4. Provenance
- Record original PMF spec in manifest.json
- Store compiled series hash for deterministic verification
- Enable UI to show both PMF spec and compiled series

### Example Compilation
```yaml
# Input PMF (7-day pattern for 14-bin grid)
kind: pmf
spec:
  length: 7
  values: [0.8, 1.2, 1.0, 0.9, 1.1, 0.7, 0.6]
  policy: "repeat"

# Compiled const series (internal)
kind: const
values: [0.8, 1.2, 1.0, 0.9, 1.1, 0.7, 0.6, 0.8, 1.2, 1.0, 0.9, 1.1, 0.7, 0.6]
```

## HTTP Status Codes

200 - Success
400 - Invalid template structure
404 - Template not found
406 - Unsupported content type requested
500 - Internal server error