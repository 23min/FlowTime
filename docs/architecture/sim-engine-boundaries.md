# SIM-Engine Architectural Boundaries

## Overview

FlowTime-Sim and the FlowTime Engine have clearly defined responsibilities to maintain clean architectural boundaries and enable independent development.

## FlowTime-Sim Responsibilities ✅

FlowTime-Sim is a **modeling front-end** that focuses on template authoring and model generation:

- ✅ **Template parsing and schema validation** - Parse YAML templates, validate schema compliance
- ✅ **Parameter substitution** - Convert `${param}` references to actual values  
- ✅ **Model generation** - Transform templates into Engine-compatible model format
- ✅ **Template authoring support** - Provide clean APIs for template manipulation

**Key Principle**: SIM creates model artifacts that are later consumed by other components (UI, CLI tools, etc.) which then post these artifacts to the Engine API. SIM itself never performs computation or calls Engine APIs directly - it operates in an **artifacts-first** approach.

## Engine Responsibilities ✅

The FlowTime Engine is responsible for **semantic validation and computation**:

### Model Validation
- ✅ **Semantic validation**
  - Validate node references exist and are properly typed
  - Ensure model consistency and completeness
  - Validate expression syntax and semantics

- ✅ **DAG construction and cycle detection**
  - Build dependency graphs from node references
  - Detect circular dependencies
  - Validate computational order

- ✅ **Expression parsing and dependency analysis**
  - Parse expression syntax to extract node dependencies
  - Analyze dependency relationships
  - Validate expression semantics

### Computation
- ✅ **Model execution** - Run simulations and generate telemetry
- ✅ **RNG integration** - Use PCG32 with provided seed configuration
- ✅ **PMF compilation** - Compile PMF specifications into executable forms

## API Boundaries

### SIM API Pattern
```
POST /api/v1/templates/{id}/generate
Content-Type: application/json

{
  "parameters": { "param1": "value1" }
}

Response: Engine-compatible model
```

### Engine API Pattern
```
POST /api/v1/validate  # Semantic validation
POST /api/v1/run       # Model execution
```

## Implementation Guidelines

### For SIM Developers
- Focus on template authoring experience
- Validate syntax, not semantics
- Generate clean models for Engine consumption
- Never implement dependency analysis or cycle detection

### For Engine Developers
- Accept SIM-generated models as input
- Perform all semantic validation
- Handle all computational responsibilities
- Provide clear validation error messages

### For UI Developers
- Call SIM for template → model conversion
- Call Engine for model validation and execution
- Present validation errors from Engine to users
- Orchestrate the complete workflow

## Charter Compliance

This boundary ensures:
- **SIM Charter**: "Modeling front-end that creates model artifacts but never computes telemetry"
- **Engine Charter**: "Receives models and performs all computation and validation"
- **Clean Separation**: Each component has focused, well-defined responsibilities

## Related Documentation

- [Template Schema](../schemas/template-schema.md) - Template syntax and structure
- [FlowTime-Sim Charter](../flowtime-sim-charter.md) - SIM's overall mission
- [Engine API Documentation](../api/) - Engine endpoint specifications