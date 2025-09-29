# Template Development Guide

## Creating FlowTime-Sim Templates

This guide covers developing new templates using the node-based template schema.

## Template Structure Overview

Every template consists of six main sections:

```yaml
metadata:      # Template identification and description
parameters:    # User-configurable values with validation  
grid:          # Simulation time grid configuration
nodes:         # Template nodes (const, pmf, expr)
outputs:       # CSV file specifications
rng:           # Optional: Random number generation configuration
```

## Section-by-Section Guide

### 1. Metadata Section

Define template identity and categorization:

```yaml
metadata:
  id: my-template                    # Required: Unique identifier (kebab-case)
  title: "My Template"               # Required: Human-readable title
  description: "Template for..."     # Optional: Detailed description
  version: "1.0.0"                   # Optional: Template version (semver)
  tags: ["category", "type"]         # Optional: Categorization tags
```

**Best Practices:**
- Use kebab-case for IDs: `it-system-microservices`
- Choose descriptive titles for UI display
- Include domain-specific tags: `["web-systems", "microservices", "stochastic"]`

### 2. Parameters Section

Define user-configurable values with type safety:

```yaml
parameters:
  - name: capacity                   # Parameter name (used in ${} substitution)
    type: number                     # Required: number|string|boolean|array
    title: "System Capacity"         # Optional: UI display name  
    description: "Max requests..."   # Optional: Help text
    default: 100                     # Optional: Default value
    min: 1                          # Optional: Validation constraints
    max: 10000                      # Optional: For number types
```

#### Parameter Types

**Number Parameters:**
```yaml
- name: capacity
  type: number
  min: 1
  max: 10000
  default: 100
```

**String Parameters:**
```yaml
- name: environment  
  type: string
  enum: ["dev", "staging", "prod"]  # Restrict to specific values
  default: "dev"
```

**Boolean Parameters:**
```yaml
- name: enableCaching
  type: boolean
  default: true
```

**Array Parameters:**
```yaml
- name: requestPattern
  type: array
  elementType: number              # Type of array elements
  minLength: 1                     # Minimum array size
  maxLength: 100                   # Maximum array size  
  default: [100, 150, 200]
```

#### Parameter Validation

All parameters support these validation rules:
- **Required vs Optional**: Parameters without `default` are required
- **Type Constraints**: Automatic type checking
- **Range Validation**: `min`/`max` for numbers
- **Length Validation**: `minLength`/`maxLength` for strings/arrays  
- **Enum Validation**: `enum` for restricted value sets

### 3. Grid Section

Define simulation time grid:

```yaml
grid:
  bins: ${bins}          # Number of time intervals (parameter or literal)
  binSize: 60            # Duration per interval  
  binUnit: minutes       # seconds|minutes|hours|days
```

**Parameter Integration:**
```yaml
parameters:
  - name: simulationHours
    type: number
    default: 24
    
grid:
  bins: ${simulationHours}
  binSize: 1
  binUnit: hours
```

### 4. Nodes Section

Define DAG nodes for data sources and transformations:

#### Const Nodes (Static Values)
```yaml
nodes:
  - id: user_requests               # Unique node identifier
    kind: const
    values: ${requestPattern}       # Array of values or parameter reference
```

**Array Expansion:**
If `requestPattern = [10, 20, 30]` and `bins = 7`, the result is `[10, 20, 30, 10, 20, 30, 10]` (cycles through array).

#### PMF Nodes (Probability Mass Functions)
```yaml
nodes:
  - id: network_reliability
    kind: pmf
    pmf:
      values: [1.0, 0.9, 0.7, 0.0]           # Possible outcomes
      probabilities: [0.7, 0.2, 0.09, 0.01]  # Must sum to 1.0 (±0.001)
```

**PMF Best Practices:**
- Include failure modes: `[1.0, 0.5, 0.0]` for normal/degraded/failed
- Use realistic probabilities based on domain knowledge
- Always validate probabilities sum to 1.0

#### Expression Nodes (Mathematical Operations)
```yaml
nodes:
  - id: processed_requests
    kind: expr
    expr: "MIN(user_requests * reliability, capacity)"
```

**Supported Operations:**
- **Arithmetic**: `+`, `-`, `*`, `/`
- **Functions**: `MIN()`, `MAX()`, `SUM()`, `AVG()`
- **Node References**: Use node IDs directly in expressions
- **Constants**: Numeric literals and parameter references

**Expression Examples:**
```yaml
# Bottleneck modeling
- id: bottleneck
  kind: expr
  expr: "MIN(requests, capacity)"

# Load balancing  
- id: distributed_load
  kind: expr
  expr: "(requests * 0.7) + (overflow * 0.3)"

# Failure calculation
- id: failed_requests
  kind: expr  
  expr: "requests - processed"

# Utilization percentage
- id: utilization
  kind: expr
  expr: "processed / MAX(capacity, 1) * 100"
```

### 5. Outputs Section

Define CSV files to generate:

```yaml
outputs:
  - series: user_requests           # Node ID to output
    filename: requests.csv          # Output filename
    
  - series: processed_requests
    filename: processed.csv
    
  - series: utilization
    filename: utilization.csv
```

### 6. RNG Section (Optional)

Configure random number generation for Engine PMF compilation:

```yaml
rng:
  kind: pcg32                       # Required: RNG algorithm (only pcg32 supported)
  seed: ${rngSeed}                  # Required: Deterministic seed (often parameterized)
```

**Usage:**
- **PMF compilation**: Engine uses RNG for deterministic PMF compilation
- **Deterministic behavior**: Same seed produces identical PMF compilation results
- **Parameter control**: Allow users to control randomness via seed parameters
- **Single source of truth**: Engine handles all RNG operations and PMF semantics

**Parameter Example:**
```yaml
parameters:
  - name: rngSeed
    type: integer
    title: "Random Seed"
    description: "Seed for deterministic PMF compilation"
    default: 12345
    min: 1
    max: 2147483647
```

## Template Development Workflow

### 1. Design Phase
- Identify the system/scenario to model
- List user-configurable parameters
- Sketch node dependencies (DAG structure)
- Plan output metrics

### 2. Implementation Phase
```yaml
# Start with metadata and parameters
metadata:
  id: my-new-template
  title: "..."

parameters:
  - name: key_param
    type: number
    default: 100

# Define time grid
grid:
  bins: 12
  binSize: 1  
  binUnit: hours

# Build nodes incrementally
nodes:
  # Start with inputs (const/pmf nodes)
  - id: input_data
    kind: const
    values: ${key_param}
    
  # Add transformations (expr nodes)
  - id: processed_data
    kind: expr
    expr: "input_data * 0.9"

# Define outputs
outputs:
  - series: processed_data
    filename: results.csv
```

### 3. Testing Phase
Use FlowTime-Sim CLI to validate templates:

```bash
# Validate template structure
flowtime-sim template validate --file my-template.yaml

# Test parameter processing
flowtime-sim template generate --template my-template --params test-params.json

# Generate and inspect model
flowtime-sim template show --id my-template --params params.json
```

## Advanced Patterns

### Multi-Stage Processing
```yaml
nodes:
  # Stage 1: Input processing
  - id: raw_requests
    kind: const
    values: ${requestPattern}
    
  - id: filtered_requests
    kind: expr
    expr: "raw_requests * ${filterRatio}"
    
  # Stage 2: System processing  
  - id: processed_stage1
    kind: expr
    expr: "MIN(filtered_requests, ${stage1Capacity})"
    
  - id: processed_stage2
    kind: expr
    expr: "MIN(processed_stage1, ${stage2Capacity})"
```

### Stochastic Modeling
```yaml
nodes:
  # Base load
  - id: nominal_requests
    kind: const
    values: ${basePattern}
    
  # Variable reliability
  - id: system_reliability
    kind: pmf
    pmf:
      values: [1.0, 0.8, 0.5, 0.0]
      probabilities: [0.7, 0.2, 0.08, 0.02]
      
  # Actual throughput
  - id: actual_processed  
    kind: expr
    expr: "nominal_requests * system_reliability"
```

### Metric Calculations
```yaml
nodes:
  # ... processing nodes ...
  
  # Summary metrics
  - id: total_processed
    kind: expr
    expr: "SUM(processed)"
    
  - id: avg_utilization
    kind: expr  
    expr: "AVG(processed / MAX(capacity, 1) * 100)"
    
  - id: peak_load
    kind: expr
    expr: "MAX(processed)"
```

## Validation Checklist

Before submitting templates:

**Structural Validation** (FlowTime-Sim):
- [ ] All required metadata fields present (`id`, `title`)
- [ ] ID follows kebab-case pattern (metadata.id)
- [ ] Node IDs follow underscore pattern and start with letter
- [ ] Parameter types match their usage in nodes
- [ ] Node IDs are unique
- [ ] Output series reference valid node IDs
- [ ] All `${}` references have corresponding parameters

**Semantic Validation** (FlowTime Engine):
- [ ] PMF probabilities sum to 1.0 (±0.001 tolerance)
- [ ] No circular dependencies in expressions
- [ ] Expression syntax is valid
- [ ] Parameter defaults provide working example

**Domain Validation:**
- [ ] Model represents realistic scenario
- [ ] Parameter ranges are sensible for domain
- [ ] Output metrics provide valuable insights
- [ ] Template documentation is clear and complete

## Template Repository Structure

Organize templates in a consistent structure:

```
templates/
├── web-systems/
│   ├── simple-web-app.yaml
│   ├── microservices.yaml  
│   └── load-balancer.yaml
├── manufacturing/
│   ├── assembly-line.yaml
│   └── quality-control.yaml
└── network/
    ├── packet-routing.yaml
    └── bandwidth-modeling.yaml
```

## Best Practices Summary

1. **Keep It Simple**: Start with essential parameters, add complexity gradually
2. **Validate Early**: Test templates with multiple parameter combinations  
3. **Document Thoroughly**: Clear descriptions help users understand the model
4. **Use Realistic Defaults**: Default values should produce meaningful results
5. **Plan for Failure**: Include failure modes and edge cases in stochastic models
6. **Test Edge Cases**: Validate with minimum/maximum parameter values
7. **Follow Conventions**: Consistent naming and structure across templates