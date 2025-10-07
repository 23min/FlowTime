# FlowTime-Sim Schema Reference

## Overview

This directory contains schema documentation for FlowTime-Sim service, which generates models for FlowTime Engine execution. These schemas document the formats that Sim **produces** and **consumes**.

## Schema Files

### Model Definition Schemas
- **[model.schema.md](model.schema.md)** - Complete model definition specification
- **[model.schema.yaml](model.schema.yaml)** - YAML schema for model format
- **[template-schema.md](template-schema.md)** - Template definition format

### Output/Artifact Schemas
- **[run.schema.json](run.schema.json)** - Run summary format (Sim output)
- **[series-index.schema.json](series-index.schema.json)** - Series index format (Sim output)
- **[manifest.schema.json](manifest.schema.json)** - Run manifest format

### Sim-Specific Schemas
- **[catalog.schema.json](catalog.schema.json)** - System catalog format for component diagrams

## Quick Reference

### Model Format (Sim Output → Engine Input)

FlowTime-Sim generates models in this format:

```yaml
schemaVersion: 1

grid:
  bins: 24
  binSize: 1
  binUnit: hours

nodes:
  - id: demand
    kind: const
    values: [100, 150, 200]
  - id: served
    kind: expr
    expr: "demand * 0.8"

outputs:
  - series: served
```

### Template Format (Sim Input)

Templates use parameter substitution:

```yaml
metadata:
  id: example-template
  title: "Example Template"

parameters:
  - name: bins
    type: integer
    default: 24
  - name: pattern
    type: array
    default: [100, 150, 200]

grid:
  bins: ${bins}
  binSize: 1
  binUnit: hours

nodes:
  - id: demand
    kind: const
    values: ${pattern}
```

## Schema Philosophy

**Important**: These schemas document what **FlowTime-Sim outputs**, not strict validation rules. They are intentionally simpler and more permissive than Engine schemas:

- **Sim Schemas** (this repo): Document Sim's output format, `additionalProperties: true`
- **Engine Schemas** (flowtime-vnext): Strict validation, comprehensive, `additionalProperties: false`

This difference is **intentional** - Sim focuses on generation, Engine focuses on validation.

## Integration Flow

```
Template (YAML/JSON)
    ↓
FlowTime-Sim Service
    ├─ Parse template
    ├─ Substitute parameters
    └─ Generate model
        ↓
Model (YAML) → FlowTime Engine
    ↓
Run Results (JSON/CSV)
```

## API Endpoints

FlowTime-Sim Service provides:

- `GET /api/v1/templates` - List available templates
- `GET /api/v1/templates/{id}` - Get template details with parameters
- `POST /api/v1/templates/{id}/generate` - Generate model from template
- `GET /api/v1/health` - Service health check
- `GET /api/v1/version` - Service version info

## Schema Evolution

**Current Version (M2.9+)**: Uses `binSize`/`binUnit` format

```yaml
grid:
  bins: 24
  binSize: 1      # Duration magnitude
  binUnit: hours  # minutes|hours|days|weeks
```

**Legacy Format** (deprecated): Used `binMinutes`

## See Also

- **FlowTime Engine Schemas**: `flowtime-vnext/docs/schemas/` - Authoritative validation schemas
- **API Documentation**: See `/docs/` for complete API reference
- **Examples**: See `/examples/` and `/templates/` for sample models and templates
- **Architecture**: See `/docs/architecture/` for design documentation
