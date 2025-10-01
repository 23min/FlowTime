# Model Provenance Architecture

## Overview

This document defines how model provenance is tracked across FlowTime-Sim (model generation) and FlowTime Engine (model execution), ensuring complete traceability from template to run results.

## The Provenance Problem

### Current Gap

**FlowTime-Sim generates:**
```
/data/models/{model_id}/
├── model.yaml        ← Model content
└── metadata.json     ← Template provenance (template_id, parameters, timestamp)
```

**FlowTime Engine receives:**
```yaml
# Only the model YAML content (no Sim metadata!)
schemaVersion: 1
grid:
  bins: 12
  binSize: 1
  binUnit: hours
nodes:
  # ...
```

**FlowTime Engine creates:**
```
/data/run_{id}/
├── spec.yaml         ← Copy of model
├── manifest.json     ← Execution metadata only
└── run.json          ← Run info only
```

**Result:** ❌ **Lost provenance** - Can't trace which template/parameters created the model!

### Critical Questions We Can't Answer Without Provenance

- Which template generated this model?
- What parameter values were used?
- Can I regenerate this exact model?
- Which runs came from the same template configuration?
- How do I compare runs from different parameter sets?

## Architecture Solution

### Principle: Separate Registries with Cross-References

**Key Insight:** Models are Sim artifacts, Runs are Engine artifacts, but they must be linked.

```
┌──────────────────┐      ┌──────────────────┐
│  FlowTime-Sim    │      │ FlowTime-Engine  │
│                  │      │                  │
│  Model Registry  │      │  Run Registry    │
│  /data/models/   │      │  /data/runs/     │
│                  │      │                  │
│  model_abc123    │◄─────┤  run_xyz         │
│  ├─ model.yaml   │  ref │  ├─ spec.yaml    │
│  └─ metadata.json│      │  ├─ manifest.json│
│     (provenance) │      │  └─ provenance   │
│                  │      │     (model_id!)  │
└──────────────────┘      └──────────────────┘
         ▲                         ▲
         │                         │
         └─────────┬───────────────┘
                   │
            ┌──────────────┐
            │      UI      │
            │ Queries both │
            │  registries  │
            └──────────────┘
```

## FlowTime-Sim Responsibilities

### 1. Generate Unique Model IDs

Every model generation creates a unique, stable identifier:

```
model_id format: model_{timestamp}_{hash}
Example: model_20250925T120000Z_abc123def
```

**Generation logic:**
```csharp
public string GenerateModelId(string templateId, Dictionary<string, object> parameters)
{
    var timestamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ");
    var contentHash = ComputeHash(templateId, parameters); // First 8 chars
    return $"model_{timestamp}_{contentHash}";
}
```

### 2. Store Complete Model Artifacts

**File structure:**
```
/data/models/{model_id}/
├── model.yaml          # Generated model content
├── metadata.json       # Complete provenance metadata
└── catalog.json        # Model catalog for registry
```

**metadata.json schema:**
```json
{
  "model_id": "model_20250925T120000Z_abc123def",
  "created_at": "2025-09-25T12:00:00Z",
  "source": "flowtime-sim",
  "template": {
    "template_id": "it-system-microservices",
    "template_version": "1.0",
    "template_title": "IT System with Microservices"
  },
  "parameters": {
    "bins": 12,
    "binSize": 1,
    "binUnit": "hours",
    "loadBalancerCapacity": 300,
    "authCapacity": 250
    // ... all parameter values used
  },
  "generation": {
    "timestamp": "2025-09-25T12:00:00Z",
    "sim_version": "0.4.0",
    "user": "system"
  },
  "model_hash": "sha256:abc123...",
  "schema_version": "1"
}
```

### 3. Update API Response to Include Model ID

**Current `/api/v1/templates/{id}/generate` response:**
```json
{
  "model": "schemaVersion: 1\ngrid:\n  ..."
}
```

**Enhanced response (NEW):**
```json
{
  "model_id": "model_20250925T120000Z_abc123def",
  "model": "schemaVersion: 1\ngrid:\n  ...",
  "metadata": {
    "template_id": "it-system-microservices",
    "parameters": { "bins": 12, "binSize": 1, "binUnit": "hours" },
    "created_at": "2025-09-25T12:00:00Z"
  },
  "path": "/data/models/model_20250925T120000Z_abc123def/model.yaml"
}
```

### 4. Implement Model Registry

**Model Registry Index (`/data/models/registry-index.json`):**
```json
{
  "version": "1.0.0",
  "updated": "2025-09-25T12:00:00Z",
  "models": [
    {
      "model_id": "model_20250925T120000Z_abc123def",
      "template_id": "it-system-microservices",
      "template_title": "IT System with Microservices",
      "created_at": "2025-09-25T12:00:00Z",
      "parameters_summary": "bins=12, binSize=1h, capacity=300",
      "tags": ["microservices", "capacity-planning"],
      "path": "models/model_20250925T120000Z_abc123def"
    }
  ]
}
```

**Registry API Endpoints:**
```bash
# List all models
GET /api/v1/models
GET /api/v1/models?template_id=it-system

# Get specific model
GET /api/v1/models/{model_id}

# Get model files
GET /api/v1/models/{model_id}/files/model.yaml
GET /api/v1/models/{model_id}/files/metadata.json
```

## FlowTime Engine Integration

### Engine Must Accept Provenance Metadata

**Enhanced `/v1/run` endpoint:**

**Current:**
```yaml
POST /v1/run
Content-Type: application/x-yaml

schemaVersion: 1
grid:
  bins: 12
  # ...
```

**Enhanced (NEW):**
```yaml
POST /v1/run
Content-Type: application/x-yaml

schemaVersion: 1

# NEW: Optional provenance section
provenance:
  source: flowtime-sim
  model_id: model_20250925T120000Z_abc123def
  template_id: it-system-microservices
  generated_at: "2025-09-25T12:00:00Z"

grid:
  bins: 12
  # ...
```

**OR via separate header/metadata:**
```yaml
POST /v1/run
X-Model-Provenance: model_20250925T120000Z_abc123def
Content-Type: application/x-yaml

schemaVersion: 1
grid:
  # ... (no provenance in body)
```

### Engine Must Store Provenance Reference

**Run artifact structure (ENHANCED):**
```
/data/run_20250925T120500Z_xyz789/
├── spec.yaml                  # Model content
├── manifest.json             # Execution metadata
├── run.json                  # Run info
├── provenance.json           # NEW: Provenance reference
└── series/
    └── ...
```

**provenance.json schema:**
```json
{
  "source": "flowtime-sim",
  "model_id": "model_20250925T120000Z_abc123def",
  "template_id": "it-system-microservices",
  "generated_at": "2025-09-25T12:00:00Z",
  "received_at": "2025-09-25T12:05:00Z",
  "link": "/api/v1/models/model_20250925T120000Z_abc123def"
}
```

## UI Orchestration Flow

### Complete Provenance Chain

```typescript
// 1. Generate model via Sim
const generateResponse = await simClient.post('/api/v1/templates/it-system/generate', {
  parameters: { bins: 12, binSize: 1, binUnit: 'hours' }
});

const { model_id, model, metadata } = generateResponse.data;

// 2. Execute model via Engine WITH provenance
const runResponse = await engineClient.post('/v1/run', model, {
  headers: {
    'X-Model-Provenance': model_id,
    'Content-Type': 'application/x-yaml'
  }
});

const { run_id } = runResponse.data;

// 3. UI can now query complete provenance
const modelInfo = await simClient.get(`/api/v1/models/${model_id}`);
const runInfo = await engineClient.get(`/v1/runs/${run_id}`);

// Result: Complete traceability
console.log(`Run ${run_id} came from model ${model_id}`);
console.log(`Model ${model_id} came from template ${modelInfo.template_id}`);
console.log(`Parameters used: ${JSON.stringify(modelInfo.parameters)}`);
```

### Provenance Queries Enabled

```typescript
// Find all runs from a specific model
const runs = await engineClient.get('/v1/runs', {
  params: { model_id: 'model_20250925T120000Z_abc123def' }
});

// Find all models from a specific template
const models = await simClient.get('/api/v1/models', {
  params: { template_id: 'it-system-microservices' }
});

// Compare runs from same template, different parameters
const template_runs = await engineClient.get('/v1/runs', {
  params: { template_id: 'it-system-microservices' }
});
```

## Implementation Phases

### Phase 1: Sim Model Registry (Sim-Side)
- [ ] Implement unique model_id generation
- [ ] Store model artifacts with metadata.json
- [ ] Create model registry index
- [ ] Update `/generate` endpoint to return model_id
- [ ] Implement `/api/v1/models` endpoints

### Phase 2: Engine Provenance Support (Engine-Side)
- [ ] Update `/v1/run` to accept provenance metadata
- [ ] Store provenance.json in run artifacts
- [ ] Update manifest.json to include model_id reference
- [ ] Update registry to index runs by model_id
- [ ] Implement `/v1/runs?model_id=` query filter

### Phase 3: UI Integration
- [ ] Update Sim client to capture model_id
- [ ] Update Engine client to send provenance header
- [ ] Implement provenance display in UI
- [ ] Enable run filtering by template/model
- [ ] Build provenance visualization (template → model → run)

## Benefits

### Complete Traceability
- ✅ Track every run back to template and parameters
- ✅ Reproduce exact models by re-generating with same parameters
- ✅ Compare runs across parameter variations systematically

### Independent Services
- ✅ Sim and Engine remain loosely coupled
- ✅ No shared storage required
- ✅ Clear ownership (Sim owns models, Engine owns runs)

### Enhanced Workflows
- ✅ Compare runs from same template
- ✅ Parameter sensitivity analysis
- ✅ Model versioning and evolution tracking
- ✅ Audit trails for compliance

## Migration Strategy

### Backward Compatibility

**Runs without provenance:**
- Engine continues to accept models without provenance metadata
- `provenance.json` is optional (missing for old runs)
- Registry marks these as `source: unknown`

**New runs with provenance:**
- UI automatically includes model_id when available
- Engine stores provenance when provided
- Full traceability for all new runs

### Gradual Rollout

1. **Phase 1**: Sim adds model_id to responses (non-breaking)
2. **Phase 2**: Engine accepts provenance (optional, non-breaking)
3. **Phase 3**: UI starts sending provenance (enhanced experience)
4. **Phase 4**: Provenance becomes standard practice (full traceability)

## Related Documentation

- **Engine**: See `/docs/architecture/run-provenance.md` (Engine-side implementation)
- **API**: See `/docs/api/model-registry.md` (Model registry API spec)
- **Charter**: See `/docs/flowtime-sim-charter.md` (Artifact registry vision)

---

**Status**: Architecture defined, implementation planned for SIM-M3.x
