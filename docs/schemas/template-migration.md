# Template Schema Migration Guide

## Overview

This guide covers the transition from legacy `arrivals`/`route` schema to the new node-based template schema. **Note**: Legacy templates will not be supported - this is a complete replacement of the template system.

## Schema Comparison

### Legacy Schema (Deprecated)
```yaml
schemaVersion: 1
grid:
  bins: 12
  binMinutes: 60
arrivals:
  kind: const
  values: [100, 150, 200]
route:
  id: requests
```

### New Template Schema
```yaml
metadata:
  id: simple-requests
  title: "Simple Request Pattern"
  
parameters:
  - name: bins
    type: integer
    default: 12
  - name: requestPattern
    type: array
    default: [100, 150, 200]

grid:
  bins: ${bins}
  binSize: 60
  binUnit: minutes

nodes:
  - id: user_requests
    kind: const
    values: ${requestPattern}

outputs:
  - series: user_requests
    filename: requests.csv
```

## Key Changes

### 1. Grid Configuration
```yaml
# Legacy
grid:
  bins: 12
  binMinutes: 60

# New
grid:
  bins: 12
  binSize: 60
  binUnit: minutes
```

### 2. Arrivals → Nodes
```yaml
# Legacy
arrivals:
  kind: const
  values: [100, 150, 200]

# New
nodes:
  - id: user_requests
    kind: const
    values: [100, 150, 200]
```

### 4. Added Template Features
- **Metadata**: Template identification and description
- **Parameters**: Type-safe user customization
- **PMF Nodes**: Probability mass functions for stochastic modeling
- **Expression Nodes**: Mathematical operations on other nodes

## Migration Patterns

### Simple Constant Values
```yaml
# Legacy
arrivals:
  kind: const
  values: [10, 20, 30]

# New Template
parameters:
  - name: pattern
    type: array
    default: [10, 20, 30]

nodes:
  - id: arrivals
    kind: const
    values: ${pattern}
```

### PMF Distributions
```yaml
# Legacy (limited PMF support)
arrivals:
  kind: pmf
  values: [1, 2, 3]
  probabilities: [0.5, 0.3, 0.2]

# New Template (enhanced PMF)
nodes:
  - id: arrivals
    kind: pmf
    pmf:
      values: [1, 2, 3]
      probabilities: [0.5, 0.3, 0.2]
```

### Complex Scenarios (New Capability)
```yaml
# Not possible in legacy schema
# New Template enables complex modeling
nodes:
  - id: base_requests
    kind: const
    values: ${basePattern}
    
  - id: system_reliability
    kind: pmf
    pmf:
      values: [1.0, 0.9, 0.7, 0.0]
      probabilities: [0.8, 0.15, 0.04, 0.01]
      
  - id: actual_processed
    kind: expr
    expr: "MIN(base_requests * system_reliability, ${maxCapacity})"

outputs:
  - series: base_requests
    filename: requested.csv
  - series: actual_processed
    filename: processed.csv
```

## Template Recreation

Legacy templates will be recreated (not migrated) using the new node-based schema:

### One-Time Conversion Process
1. **Implement new schema support**: Core node-based template processing
2. **Create conversion utility**: Throw-away console application for template recreation
3. **Convert existing templates**: Place recreated templates in `/templates-new/`
4. **Replace template system**: Remove `/templates/`, rename `/templates-new/` to `/templates/`
5. **Archive examples**: Move `/examples/` content to archive location

## Template Recreation Guidelines

When recreating templates with the new schema:

1. **Analyze legacy functionality**: Understand what the legacy template accomplished
2. **Design node structure**: Plan how to represent the logic using const/pmf/expr nodes
3. **Define parameters**: Extract configurable values with appropriate validation
4. **Structure outputs**: Determine useful CSV outputs for the scenario
5. **Test thoroughly**: Ensure recreated template produces equivalent or better results

## Validation Checklist

- [ ] All required metadata fields present
- [ ] Parameter types match usage
- [ ] Time configuration valid
- [ ] Node IDs unique
- [ ] PMF probabilities sum to 1.0
- [ ] Output series reference valid nodes
- [ ] No circular dependencies in expressions

## Benefits of New Schema

1. **Parameterization**: Templates can be customized without editing YAML
2. **Type Safety**: Parameter validation prevents runtime errors
3. **Complex Modeling**: Expression nodes enable sophisticated scenarios
4. **Better Organization**: Clear separation of metadata, parameters, and logic
5. **Content Negotiation**: Support for both YAML and JSON formats via API
6. **Clear Responsibilities**: Structural validation by FlowTime-Sim, semantic validation by Engine
7. **Extensibility**: Schema designed for future enhancements

## Implementation Approach

- **Complete replacement**: No legacy template support
- **One-time conversion**: Recreate shipping templates using new schema
- **Clean cutover**: Replace entire template system at once

## Support

For migration assistance:
- Review template examples in `/examples/templates/`
- Check schema validation with CLI tools
- Consult API documentation for new template endpoints