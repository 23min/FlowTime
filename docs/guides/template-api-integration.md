# Template API Integration Guide

> ⚠️ **SCHEMA MIGRATION IN PROGRESS**  
> This document contains legacy `binMinutes` references.  
> **Current Implementation**: Use `grid: { bins, binSize, binUnit }` format.  
> **See**: `docs/schemas/template-schema.md` for authoritative schema.  
> **Status**: Documentation update pending post-UI-M2.9.

This guide explains how to integrate with FlowTime-Sim's parameterized template system for building user interfaces that allow dynamic model generation.

## Overview

FlowTime-Sim provides **parameterized templates** that generate customized simulation models based on user input. This enables rich UI experiences where users can configure parameters through forms rather than editing raw YAML.

## Core Concepts

### Templates and Models
- **Template**: A parameterized blueprint with `{{placeholder}}` syntax and parameter schema
- **Model**: A concrete YAML definition ready for Engine execution
- **Generation**: The process of template + parameters → model

### Parameter Schema
Each template includes a parameter schema defining:
- **Type**: `number`, `integer`, `string`, `boolean`, `enum`
- **Validation**: `minimum`, `maximum`, `allowedValues`
- **Metadata**: `title`, `description`, `defaultValue`

## API Endpoints

### 1. Template Discovery: `GET /api/v1/templates`

**Purpose**: Get available templates with parameter schemas for UI form generation.

**Response Structure**:
```json
[
  {
    "id": "it-system-microservices",
    "title": "IT System with Microservices", 
    "description": "Modern web application with configurable load patterns",
    "category": "domain",
    "tags": ["intermediate", "microservices", "web-scale"],
    "parameters": [
      {
        "name": "requestRate",
        "type": "number",
        "title": "Request Rate (req/min)",
        "description": "Incoming API requests per minute",
        "defaultValue": 100,
        "minimum": 10,
        "maximum": 10000,
        "allowedValues": null
      }
    ],
    "preview": { /* YAML structure preview */ }
  }
]
```

**Query Parameters**:
- `?category=domain` - Filter by category (`theoretical`, `domain`)

### 2. Scenario Generation: `POST /api/v1/templates/{id}/generate`

**Purpose**: Convert template + parameters into concrete YAML model.

**Request Body**:
```json
{
  "requestRate": 200,
  "bins": 8,
  "binMinutes": 45,
  "seed": 999
}
```

**Query Parameters**:
- `?embed_provenance=true` - Include provenance metadata embedded in model YAML (optional)

**Response**:
```json
{
  "model": "schemaVersion: 1\nrng: pcg\nseed: 999\ngrid:\n  bins: 8\n  binMinutes: 45\narrivals:\n  kind: poisson\n  rate: 200\nroute:\n  id: LOAD_BALANCER",
  "provenance": {
    "source": "flowtime-sim",
    "modelId": "model_20251002T103045Z_a3f8c2d1",
    "templateId": "it-system-microservices",
    "templateVersion": "1.0",
    "templateTitle": "IT System with Microservices",
    "parameters": { /* submitted parameters */ },
    "generatedAt": "2025-10-02T10:30:45.1234567Z",
    "generator": "flowtime-sim/0.5.0",
    "schemaVersion": "1"
  }
}
```

**Response Fields**:
- `model` - Generated YAML model ready for Engine execution
- `provenance` - Metadata for traceability (template ID, parameters, model ID, etc.)

> **Note:** For complete provenance metadata schema and integration patterns, see [`docs/architecture/model-lifecycle.md`](../architecture/model-lifecycle.md).

**Error Handling**:
- `400 Bad Request` with `{"error": "Parameter 'requestRate' must be a number"}` for validation failures
- `404 Not Found` for unknown template IDs

## UI Integration Flow

```mermaid
sequenceDiagram
    participant UI
    participant Sim as FlowTime-Sim API
    participant Engine as FlowTime Engine
    
    UI->>Sim: GET /api/v1/templates
    Sim-->>UI: Templates with parameter schemas
    
    Note over UI: User selects template<br/>UI renders parameter form
    
    UI->>Sim: POST /api/v1/templates/{id}/generate
    Sim-->>UI: {model, provenance}
    
    UI->>Engine: POST /v1/run (model YAML)
    Engine-->>UI: Run results
```

### Workflow Steps

1. **Template Discovery**: Query `/api/v1/templates` to get available templates with parameter schemas
2. **Parameter Form**: Build dynamic form from `template.parameters` schema with validation (min/max/type constraints)
3. **Model Generation**: POST parameters to `/api/v1/templates/{id}/generate` to get model + provenance
4. **Execution**: Send model YAML to FlowTime Engine `/v1/run` endpoint

> **Note:** For complete workflow including provenance tracking and Engine integration, see [`docs/architecture/model-lifecycle.md`](../architecture/model-lifecycle.md).

## Parameter Types & Validation

### Parameter Schema

Each template parameter has the following structure:

```json
{
  "name": "requestRate",
  "type": "number",
  "title": "Request Rate (req/min)",
  "description": "Incoming API requests per minute",
  "defaultValue": 100,
  "minimum": 10,
  "maximum": 10000,
  "allowedValues": null
}
```

**Parameter Types:**
- `number` - Numeric values (can be decimal)
- `integer` - Whole numbers only
- `string` - Text values
- `boolean` - True/false flags
- `enum` - Selection from predefined list (`allowedValues`)

**Validation Fields:**
- `minimum` / `maximum` - Range constraints for `number` and `integer` types
- `allowedValues` - Array of valid choices for `enum` type
- `defaultValue` - Default value if parameter not provided

## Template Categories

### Theoretical Templates
- **Purpose**: Educational, mathematical concepts
- **Examples**: `const-quick`, `poisson-demo`  
- **Parameters**: Usually minimal (seed, basic timing)
- **Target Users**: Learning queue theory, testing concepts

### Domain Templates  
- **Purpose**: Real-world system modeling
- **Examples**: `it-system-microservices`, `transportation-basic`, `manufacturing-basic`
- **Parameters**: Rich, domain-specific (request rates, capacity, throughput)
- **Target Users**: Business analysis, system design

## Error Handling

### Parameter Validation Errors
```json
{
  "error": "Parameter 'requestRate' must be a number"
}
```

### Template Not Found
```json
{
  "error": "Template 'unknown-id' not found"
}
```

## UI Best Practices

- **Progressive Disclosure**: Show basic parameters first, advanced in collapsible sections
- **Real-time Validation**: Validate parameters as user types using schema constraints
- **Default Values**: Pre-populate forms with `defaultValue` from schema
- **Help Text**: Use parameter `description` for tooltips/help text
- **Parameter Grouping**: Group related parameters (timing, capacity, etc.) visually
- **Units in Labels**: Include units in titles ("Request Rate (req/min)")
- **Validation Feedback**: Show clear error messages tied to specific parameters
- **Preset Configurations**: Allow saving/loading parameter combinations
- **Template Caching**: Cache template metadata, regenerate models on demand
- **Debounced Generation**: Avoid regenerating on every keystroke

This template system provides the foundation for rich, interactive model configuration interfaces.