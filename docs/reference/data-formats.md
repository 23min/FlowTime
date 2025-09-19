# Data Formats Reference

**Version:** 1.0  
**Date:** September 19, 2025  
**Status:** Active Reference

---

## Overview

FlowTime uses different data formats for different purposes, following a consistent pattern based on **authorship, audience, and processing requirements**. This document defines when to use JSON vs YAML vs other formats across the FlowTime ecosystem.

## Format Selection Matrix

| **Use Case** | **Format** | **Rationale** | **Examples** |
|--------------|------------|---------------|--------------|
| **Human-authored configuration** | **YAML** | Readable, editable, version control friendly | System catalogs, model specs, templates |
| **Machine-generated metadata** | **JSON** | Precise, structured, web standard | Artifact catalogs, API responses |
| **Tabular data** | **CSV/Parquet** | Analytics tools, efficient storage | Time series, telemetry exports |
| **Event streams** | **NDJSON** | Line-by-line processing | Event logs, streaming data |
| **Binary artifacts** | **Native** | Tool-specific optimization | SVG diagrams, compressed archives |

## YAML Usage

### **When to Use YAML**
- Files that humans read, write, and maintain
- Configuration files and templates  
- Version-controlled system definitions
- Multi-line content and documentation

### **YAML File Types**
```yaml
# System Catalog (docs/examples/catalogs/)
version: 1
metadata:
  id: "checkout-system" 
  title: "E-commerce Checkout System"
  description: |
    Customer checkout and payment processing system
    with inventory validation and fraud detection.
components:
  - id: CHECKOUT_SVC
    label: "Checkout Service"
    description: "Manages shopping cart and checkout flow"
    type: "service"

# Model Specification (user-authored)
nodes:
  - id: user_requests
    kind: pmf
    pmf: { "100": 0.3, "150": 0.5, "200": 0.2 }
  - id: auth_service  
    kind: expr
    expr: "user_requests * 0.95"

# Template Definition (docs/examples/templates/)
template: e-commerce
description: Standard e-commerce checkout flow
parameters:
  peak_factor: 
    default: 1.5
    description: "Peak load multiplier for traffic spikes"
  base_capacity:
    default: 100
    description: "Base processing capacity (requests/minute)"

# CLI Configuration (~/.flowtime/config.yaml)
api:
  base_url: "http://localhost:8080"
  version: "v1"
  timeout: 30s
artifacts:
  root: "/home/user/flowtime/artifacts"
  auto_cleanup: true
  retention_days: 30
```

### **YAML Content-Type**
```http
POST /v1/catalogs
Content-Type: application/yaml
Accept: application/json

# Request body is YAML, response is JSON
```

## JSON Usage

### **When to Use JSON**
- Machine-generated metadata and artifacts
- API requests and responses  
- Structured data with precise types
- Content that requires hashing for integrity

### **JSON File Types**
```json
// Artifact Catalog (machine-generated)
{
  "kind": "model",
  "id": "checkout-baseline_a1b2",
  "name": "checkout-baseline",
  "created_utc": "2025-09-19T10:22:30Z",
  "schema_version": "treehouse.binned.v0",
  "system_catalog": {
    "catalog_id": "checkout-system",
    "catalog_version": "v1.2"
  },
  "topology": {
    "nodes": [
      {
        "id": "CHECKOUT_SVC",
        "label": "Checkout Service", 
        "type": "service"
      }
    ],
    "edges": []
  },
  "behavior": {
    "template_id": "e-commerce",
    "parameters": {
      "peak_factor": 1.5,
      "base_capacity": 100
    }
  },
  "capabilities": ["counts", "flows"],
  "tags": ["e-commerce", "checkout", "baseline"],
  "inputs_hash": "sha256:c4f2a8d1e5f6...",
  "owner": "user@domain.com",
  "visibility": "private"
}

// API Response (machine-generated)
{
  "artifacts": [
    {
      "id": "run_2025-09-19T10:22Z_c3d4",
      "kind": "run",
      "name": "checkout-baseline-run",
      "created_utc": "2025-09-19T11:30:00Z",
      "model_id": "checkout-baseline_a1b2",
      "tags": ["execution", "validated"]
    }
  ],
  "pagination": {
    "total": 156,
    "page": 1,
    "size": 20,
    "has_next": true
  }
}

// Schema Definition (docs/schemas/)
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "type": "object",
  "title": "FlowTime Artifact Catalog",
  "required": ["kind", "id", "created_utc"],
  "properties": {
    "kind": {
      "type": "string",
      "enum": ["model", "run", "telemetry"]
    },
    "id": {
      "type": "string",
      "pattern": "^[a-zA-Z][a-zA-Z0-9_-]*$"
    }
  }
}
```

### **JSON Content-Type**
```http
GET /v1/artifacts
Accept: application/json
Content-Type: application/json

POST /v1/runs
Content-Type: application/json
```

## Data Formats

### **CSV: Time Series and Exports**
```csv
# Gold Standard Format (export/import)
time_bin,component_id,measure,value
2025-09-19T10:00:00Z,CHECKOUT_SVC,count,150
2025-09-19T10:00:00Z,CHECKOUT_SVC,utilization,0.75
2025-09-19T10:00:00Z,PAYMENT_SVC,count,142
2025-09-19T10:00:00Z,PAYMENT_SVC,latency_p50,45.2

# Series Export (per-component files)
time_bin,value
2025-09-19T10:00:00Z,150
2025-09-19T10:01:00Z,165
2025-09-19T10:02:00Z,148
```

### **NDJSON: Event Streams**
```ndjson
{"timestamp":"2025-09-19T10:00:00Z","component":"CHECKOUT_SVC","event":"request_start","request_id":"req_001"}
{"timestamp":"2025-09-19T10:00:01Z","component":"PAYMENT_SVC","event":"payment_initiated","request_id":"req_001","amount":49.99}
{"timestamp":"2025-09-19T10:00:03Z","component":"PAYMENT_SVC","event":"payment_completed","request_id":"req_001","status":"success"}
{"timestamp":"2025-09-19T10:00:04Z","component":"CHECKOUT_SVC","event":"request_complete","request_id":"req_001","duration_ms":4250}
```

### **Parquet: Analytics Data**
```bash
# Efficient columnar storage for large datasets
flowtime export --run checkout_baseline_run --format parquet --out analysis.parquet

# Optimized for analytics tools
import pandas as pd
df = pd.read_parquet('analysis.parquet')
df.groupby('component_id')['value'].agg(['mean', 'max', 'std'])
```

## Conversion and Processing

### **YAML → JSON Processing**
```typescript
// Runtime conversion for API processing
import yaml from 'js-yaml';

// Load YAML configuration
const yamlContent = fs.readFileSync('system-catalog.yaml', 'utf8');
const systemCatalog = yaml.load(yamlContent);

// Generate JSON artifact catalog
const artifactCatalog = {
  kind: 'model',
  created_utc: new Date().toISOString(),
  system_catalog: {
    catalog_id: systemCatalog.metadata.id,
    catalog_version: systemCatalog.version
  },
  topology: {
    nodes: systemCatalog.components,
    edges: systemCatalog.connections
  },
  source: 'sim',
  inputs_hash: generateHash(yamlContent)
};

// Save as JSON
fs.writeFileSync('catalog.json', JSON.stringify(artifactCatalog, null, 2));
```

### **Schema Validation**
```typescript
// Validate both YAML-sourced and JSON data with same schema
import Ajv from 'ajv';

const schema = {
  type: 'object',
  required: ['kind', 'id', 'created_utc'],
  properties: {
    kind: { enum: ['model', 'run', 'telemetry'] },
    id: { type: 'string' },
    created_utc: { type: 'string', format: 'date-time' }
  }
};

const ajv = new Ajv();
const validate = ajv.compile(schema);

// Validate YAML-parsed data
const yamlData = yaml.load(yamlContent);
if (!validate(yamlData)) {
  console.error('YAML validation failed:', validate.errors);
}

// Validate JSON artifact  
if (!validate(jsonArtifact)) {
  console.error('JSON validation failed:', validate.errors);
}
```

## File Naming Conventions

### **Extension Guidelines**
```bash
# YAML files - human-authored
system-catalog.yaml           # System definitions
model-specification.yaml      # Model configs  
template-definition.yaml      # Templates
cli-config.yaml              # User configuration

# JSON files - machine-generated  
catalog.json                 # Artifact metadata
run-manifest.json            # Execution metadata
api-response.json            # API data
schema.json                  # Schema definitions

# Data files
time-series.csv              # Tabular time series
export-data.parquet          # Analytics export
event-stream.ndjson          # Event logs
system-diagram.svg           # Generated visualizations

# Archives and bundles
model-bundle.zip             # Complete artifact packages
backup-2025-09-19.tar.gz     # Compressed backups
```

### **Directory Structure**
```
/artifacts/
  models/                    # Model artifacts
    model_id/v1/
      model.yaml            # YAML: Human-readable model spec
      catalog.json          # JSON: Machine-generated metadata
      preview.svg           # SVG: Generated diagram
  runs/
    run_id/  
      binned_v0.csv         # CSV: Time series data
      catalog.json          # JSON: Execution metadata
      events.ndjson         # NDJSON: Event stream (optional)
  telemetry/
    telemetry_id/
      imported_data.parquet # Parquet: Large dataset
      catalog.json          # JSON: Import metadata

/catalogs/                   # System catalog registry
  system_id/v1/
    catalog.yaml            # YAML: System definition
    metadata.json           # JSON: Version/lineage info
```

## API Integration

### **Request/Response Patterns**
```http
# YAML input, JSON response
POST /v1/catalogs
Content-Type: application/yaml
Accept: application/json

version: 1
metadata:
  id: new-system
# YAML body...

HTTP/1.1 201 Created
Content-Type: application/json
{
  "catalog_id": "new-system",
  "version": "v1", 
  "status": "created"
}

# JSON throughout
GET /v1/artifacts?kind=model
Accept: application/json

HTTP/1.1 200 OK  
Content-Type: application/json
{
  "artifacts": [...],
  "pagination": {...}
}

# Binary data download
GET /v1/runs/run_123/export/parquet
Accept: application/octet-stream

HTTP/1.1 200 OK
Content-Type: application/octet-stream
Content-Disposition: attachment; filename="run_123.parquet"
```

## Best Practices

### **1. Format Consistency**
- **Human authoring** → YAML (configs, templates, system definitions)
- **Machine generation** → JSON (metadata, API responses, artifacts)  
- **Tabular data** → CSV/Parquet (time series, analytics)
- **Event streams** → NDJSON (logs, real-time data)

### **2. Content-Type Headers**
Always specify correct Content-Type headers for HTTP requests:
```http
Content-Type: application/yaml      # YAML uploads
Content-Type: application/json      # JSON APIs
Content-Type: text/csv              # CSV exports  
Content-Type: application/x-ndjson  # NDJSON streams
```

### **3. Schema Validation**
- Use JSON Schema for validation regardless of source format
- Validate YAML-parsed data with same schemas as JSON
- Provide clear error messages for validation failures

### **4. File Size Considerations**
- **YAML**: Optimize for readability over size
- **JSON**: Balance readability with processing efficiency  
- **CSV**: Use for medium datasets (< 100MB)
- **Parquet**: Use for large datasets (> 100MB)

### **5. Version Control**
- **YAML files**: Commit to git, diff-friendly
- **JSON artifacts**: Generally exclude from git (generated)
- **Data files**: Use git-lfs for large files
- **Schemas**: Version control JSON schema definitions

## Common Pitfalls

### **1. Format Mixing**
```bash
# ❌ Wrong: Using JSON for human-authored config
{
  "template": "e-commerce",
  "parameters": {
    "peak_factor": 1.5
  }
}

# ✅ Correct: Use YAML for human config  
template: e-commerce
parameters:
  peak_factor: 1.5
```

### **2. Precision Loss**
```yaml
# ❌ Wrong: YAML number precision issues
probability: 0.333333333333333333

# ✅ Correct: Explicit precision in JSON context
pmf: { "100": 0.333, "200": 0.667 }
```

### **3. Content-Type Mismatch**  
```http
# ❌ Wrong: Sending YAML with JSON Content-Type
POST /v1/catalogs
Content-Type: application/json
# YAML body causes parsing errors

# ✅ Correct: Match Content-Type to body format
POST /v1/catalogs  
Content-Type: application/yaml
```

---

## Related Documentation

- [Catalog Architecture](../architecture/catalog-architecture.md) - Overall catalog system design
- [Schema Specifications](../schemas/) - JSON schema definitions  
- [API Contracts](./contracts.md) - HTTP API specifications
- [Configuration Reference](./configuration.md) - System configuration options

## Revision History

| Date | Version | Changes | Author |
|------|---------|---------|--------|
| 2025-09-19 | 1.0 | Initial data formats reference | Assistant |
