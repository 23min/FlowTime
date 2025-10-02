# Run Provenance Architecture

## Overview

This document defines how FlowTime Engine accepts and stores model provenance metadata to maintain complete traceability from template generation (FlowTime-Sim) through execution (FlowTime Engine) to results.

**Architecture Note:** Engine is the single source of truth for all artifacts (models + runs + telemetry). FlowTime-Sim provides temporary storage for UI workflows, but Engine stores everything permanently. UI orchestrates the workflow - Sim and Engine do NOT communicate directly. See [Registry Integration Architecture](../../flowtime-sim-vnext/docs/architecture/registry-integration.md) for complete system design.

## The Provenance Gap

### Current State (M2.7)

**Engine receives:**
```yaml
POST /v1/run
Content-Type: application/x-yaml

schemaVersion: 1
grid:
  bins: 12
  binSize: 1
  binUnit: hours
nodes:
  # ...
```

**Engine creates:**
```
/data/run_20250925T120500Z_xyz789/
├── spec.yaml         # Model content only
├── manifest.json     # Execution metadata (hash, timing, grid)
├── run.json          # Run info (status, timestamps)
└── series/           # Results
```

**Problem:** ❌ No connection to model origin (which template? which parameters?)

### With Provenance (Target State)

**Engine receives:**
```yaml
POST /v1/run
X-Model-Provenance: model_20250925T120000Z_abc123def
Content-Type: application/x-yaml

schemaVersion: 1
provenance:  # Optional embedded provenance
  source: flowtime-sim
  model_id: model_20250925T120000Z_abc123def
  template_id: it-system-microservices
  generated_at: "2025-09-25T12:00:00Z"

grid:
  bins: 12
  # ...
```

**Engine creates:**
```
/data/run_20250925T120500Z_xyz789/
├── spec.yaml         # Model content
├── manifest.json     # Execution metadata
├── run.json          # Run info
├── provenance.json   # NEW: Model provenance reference
└── series/           # Results
```

**Result:** ✅ Complete traceability from template → model → run → results

## Architecture Requirements

### 1. Accept Provenance Metadata

**Option A: HTTP Header (Recommended)**
```http
POST /v1/run
X-Model-Provenance: model_20250925T120000Z_abc123def
Content-Type: application/x-yaml

schemaVersion: 1
grid:
  # ... (no provenance in body)
```

**Pros:**
- Clean separation of provenance from model schema
- Doesn't pollute model YAML
- Easy to add/remove without changing model format
- Standard HTTP header pattern

**Option B: Embedded in Model YAML**
```yaml
POST /v1/run
Content-Type: application/x-yaml

schemaVersion: 1
provenance:
  source: flowtime-sim
  model_id: model_20250925T120000Z_abc123def
  template_id: it-system-microservices
  generated_at: "2025-09-25T12:00:00Z"

grid:
  bins: 12
  # ...
```

**Pros:**
- Self-contained model file
- Provenance travels with model
- No header parsing needed

**Cons:**
- Adds fields to model schema
- Provenance mixed with execution spec

**Decision:** Support **both** for flexibility:
- Header takes precedence if both present
- Embedded provenance is fallback
- Either is optional (backward compatible)
- **Storage**: Provenance stripped from `spec.yaml`, stored only in `provenance.json`
- **Hash calculation**: `model_hash` excludes provenance (only grid + nodes)
- **Warning**: Log if both header and embedded provenance present

### 2. Store Provenance in Run Artifacts

**New file: `provenance.json`**

```json
{
  "source": "flowtime-sim",
  "model_id": "model_20250925T120000Z_abc123def",
  "template_id": "it-system-microservices",
  "template_version": "1.0",
  "generated_at": "2025-09-25T12:00:00Z",
  "received_at": "2025-09-25T12:05:00Z",
  "sim_version": "0.4.0",
  "parameters": {
    "bins": 12,
    "binSize": 1,
    "binUnit": "hours",
    "loadBalancerCapacity": 300
  },
  "links": {
    "model_artifact": "/api/v1/models/model_20250925T120000Z_abc123def",
    "template_artifact": "/api/v1/templates/it-system-microservices"
  },
  "_meta": {
    "source_type": "header | embedded",
    "note": "Provenance accepted from X-Model-Provenance header OR embedded in model YAML. Header takes precedence if both present."
  }
}
```

**Storage location:**
```
/data/run_{run_id}/provenance.json
```

**When to create:**
- Always created if provenance metadata received
- Omitted if no provenance provided (backward compatible)
- Populated from header OR embedded YAML (header takes precedence)

### 3. Update Manifest to Reference Provenance

**Enhanced `manifest.json`:**
```json
{
  "run_id": "run_20250925T120500Z_xyz789",
  "model_hash": "sha256:abc123...",
  "execution_time_ms": 1250,
  "grid": {
    "bins": 12,
    "bin_size": 1,
    "bin_unit": "hours"
  },
  "provenance": {
    "has_provenance": true,
    "model_id": "model_20250925T120000Z_abc123def",
    "source": "flowtime-sim"
  },
  "created_at": "2025-09-25T12:05:00Z"
}
```

**Benefits:**
- Quick check if run has provenance (without reading provenance.json)
- Model_id available in manifest for fast queries
- Backward compatible (provenance section optional)

### 4. Update Artifact Registry Index

**Enhanced `registry-index.json`:**
```json
{
  "artifacts": [
    {
      "id": "run_20250925T120500Z_xyz789",
      "type": "run",
      "title": "IT System Capacity Analysis",
      "created": "2025-09-25T12:05:00Z",
      "tags": ["capacity", "microservices"],
      "provenance": {
        "model_id": "model_20250925T120000Z_abc123def",
        "template_id": "it-system-microservices",
        "source": "flowtime-sim"
      },
      "path": "run_20250925T120500Z_xyz789"
    }
  ]
}
```

**Enables queries like:**
```bash
GET /v1/artifacts?model_id=model_20250925T120000Z_abc123def
GET /v1/artifacts?template_id=it-system-microservices
GET /v1/artifacts?source=flowtime-sim
```

## API Changes

### `/v1/run` Endpoint Enhancement

**Request:**
```http
POST /v1/run
X-Model-Provenance: model_20250925T120000Z_abc123def
Content-Type: application/x-yaml

schemaVersion: 1
grid:
  bins: 12
  binSize: 1
  binUnit: hours
nodes:
  # ...
```

**Response (Enhanced):**
```json
{
  "run_id": "run_20250925T120500Z_xyz789",
  "status": "completed",
  "execution_time_ms": 1250,
  "provenance": {
    "model_id": "model_20250925T120000Z_abc123def",
    "has_provenance": true
  },
  "artifacts": {
    "spec": "/v1/runs/run_20250925T120500Z_xyz789/spec.yaml",
    "manifest": "/v1/runs/run_20250925T120500Z_xyz789/manifest.json",
    "provenance": "/v1/runs/run_20250925T120500Z_xyz789/provenance.json"
  }
}
```

### New Query Endpoints

**Filter runs by provenance:**
```bash
# All runs from a specific model
GET /v1/runs?model_id=model_20250925T120000Z_abc123def

# All runs from a specific template
GET /v1/runs?template_id=it-system-microservices

# All runs from Sim (vs manual YAML uploads)
GET /v1/runs?source=flowtime-sim
```

**Get provenance file:**
```bash
GET /v1/runs/{run_id}/provenance.json
```

**Response:**
```json
{
  "source": "flowtime-sim",
  "model_id": "model_20250925T120000Z_abc123def",
  "template_id": "it-system-microservices",
  "generated_at": "2025-09-25T12:00:00Z",
  "parameters": { ... }
}
```

## Implementation Details

### C# Code Changes

**1. Update RunRequest model:**
```csharp
public class RunRequest
{
    // Existing
    public string ModelYaml { get; set; }
    
    // NEW: Optional provenance from header or embedded
    public ProvenanceMetadata? Provenance { get; set; }
}

public class ProvenanceMetadata
{
    public string Source { get; set; } = "unknown";
    public string? ModelId { get; set; }
    public string? TemplateId { get; set; }
    public string? TemplateVersion { get; set; }
    public DateTime? GeneratedAt { get; set; }
    public string? SimVersion { get; set; }
    public Dictionary<string, object>? Parameters { get; set; }
}
```

**2. Update endpoint to extract provenance:**
```csharp
app.MapPost("/v1/run", async (HttpRequest req, IRunService runService) =>
{
    var modelYaml = await ReadModelYaml(req);
    
    // NEW: Extract provenance from header OR embedded in YAML
    var provenance = ExtractProvenance(req, modelYaml);
    
    var result = await runService.ExecuteAsync(modelYaml, provenance);
    
    return Results.Ok(new
    {
        run_id = result.RunId,
        status = result.Status,
        provenance = provenance != null ? new
        {
            model_id = provenance.ModelId,
            has_provenance = true
        } : null
    });
});

private ProvenanceMetadata? ExtractProvenance(HttpRequest req, string modelYaml)
{
    // Priority 1: HTTP header
    if (req.Headers.TryGetValue("X-Model-Provenance", out var modelId))
    {
        return new ProvenanceMetadata { ModelId = modelId.ToString() };
    }
    
    // Priority 2: Embedded in YAML
    var yamlDoc = YamlParser.Parse(modelYaml);
    if (yamlDoc.ContainsKey("provenance"))
    {
        return DeserializeProvenance(yamlDoc["provenance"]);
    }
    
    // No provenance provided (backward compatible)
    return null;
}
```

**3. Store provenance during run creation:**
```csharp
public async Task<RunResult> ExecuteAsync(string modelYaml, ProvenanceMetadata? provenance)
{
    var runId = GenerateRunId();
    var runDir = Path.Combine(_dataDir, runId);
    Directory.CreateDirectory(runDir);
    
    // Existing files
    await File.WriteAllTextAsync(Path.Combine(runDir, "spec.yaml"), modelYaml);
    
    // NEW: Write provenance if provided
    if (provenance != null)
    {
        provenance.ReceivedAt = DateTime.UtcNow;
        var provenanceJson = JsonSerializer.Serialize(provenance, _jsonOptions);
        await File.WriteAllTextAsync(
            Path.Combine(runDir, "provenance.json"), 
            provenanceJson
        );
    }
    
    // Execute model...
    var result = await _engine.Execute(modelYaml);
    
    // Write manifest with provenance reference
    var manifest = CreateManifest(result, provenance);
    await File.WriteAllTextAsync(
        Path.Combine(runDir, "manifest.json"),
        JsonSerializer.Serialize(manifest)
    );
    
    return result;
}
```

**4. Update registry to index by provenance:**
```csharp
private ArtifactIndexEntry CreateRegistryEntry(string runId, RunResult result, ProvenanceMetadata? provenance)
{
    return new ArtifactIndexEntry
    {
        Id = runId,
        Type = "run",
        Created = DateTime.UtcNow,
        Provenance = provenance != null ? new ProvenanceReference
        {
            ModelId = provenance.ModelId,
            TemplateId = provenance.TemplateId,
            Source = provenance.Source
        } : null,
        Path = runId
    };
}
```

### Schema Updates

**Add provenance to target-model-schema (optional section):**
```yaml
# target-model-schema.yaml
properties:
  schemaVersion:
    type: integer
    const: 1
    
  provenance:  # NEW: Optional provenance section
    type: object
    description: "Optional model provenance metadata"
    properties:
      source:
        type: string
        examples: ["flowtime-sim", "manual", "import"]
      model_id:
        type: string
      template_id:
        type: string
      generated_at:
        type: string
        format: date-time
      
  grid:
    type: object
    # ... existing
```

## Backward Compatibility

### No Breaking Changes

**Old behavior still works:**
```yaml
POST /v1/run

schemaVersion: 1
grid:
  bins: 12
  # ...
```

**Result:**
- Run executes normally
- No provenance.json created
- manifest.json has no provenance section
- Registry entry has null provenance

**New behavior (opt-in):**
```yaml
POST /v1/run
X-Model-Provenance: model_abc123

schemaVersion: 1
grid:
  bins: 12
  # ...
```

**Result:**
- Run executes normally
- provenance.json created with model_id
- manifest.json includes provenance reference
- Registry entry includes provenance metadata

### Migration Strategy

1. **Phase 1**: Engine accepts provenance (optional, non-breaking) - **M2.9 or M3.0**
2. **Phase 2**: UI starts sending provenance for new runs - **UI-M3.x**
3. **Phase 3**: All new runs have provenance (old runs grandfathered) - **Standard practice**

## Benefits

### Complete Traceability

**Before (M2.7):**
```
User sees run → ??? → No idea which template or parameters
```

**After:**
```
User sees run → provenance.json → model_id, template_id, parameters
                                          ↓
                         All metadata stored in Engine artifacts
```

**Note:** UI can retrieve model from Sim temporary storage (`/api/v1/models`) before sending to Engine, but Engine stores the permanent record with provenance.

### Enhanced Queries

```typescript
// Find all runs from a specific template configuration
const runs = await engineClient.get('/v1/runs', {
  params: { template_id: 'it-system', model_id: 'model_abc123' }
});

// Compare runs from same template, different parameters
const modelA_runs = await engineClient.get('/v1/runs?model_id=model_a');
const modelB_runs = await engineClient.get('/v1/runs?model_id=model_b');
```

### Compare Workflow

```typescript
// User selects baseline run
const baseline = await engineClient.get('/v1/runs/run_xyz');

// UI shows "Compare with other runs from same template"
const candidates = await engineClient.get('/v1/runs', {
  params: { template_id: baseline.provenance.template_id }
});
```

## Testing Strategy

### Unit Tests

```csharp
[Test]
public async Task Run_WithProvenanceHeader_StoresProvenanceFile()
{
    // Arrange
    var request = new HttpRequestMessage(HttpMethod.Post, "/v1/run");
    request.Headers.Add("X-Model-Provenance", "model_test123");
    request.Content = new StringContent(ValidModelYaml, Encoding.UTF8, "application/x-yaml");
    
    // Act
    var response = await _client.SendAsync(request);
    var result = await response.Content.ReadFromJsonAsync<RunResponse>();
    
    // Assert
    var provenancePath = Path.Combine(_dataDir, result.RunId, "provenance.json");
    Assert.That(File.Exists(provenancePath), Is.True);
    
    var provenance = JsonSerializer.Deserialize<ProvenanceMetadata>(
        await File.ReadAllTextAsync(provenancePath)
    );
    Assert.That(provenance.ModelId, Is.EqualTo("model_test123"));
}

[Test]
public async Task Run_WithoutProvenance_SkipsProvenanceFile()
{
    // Arrange
    var request = new HttpRequestMessage(HttpMethod.Post, "/v1/run");
    request.Content = new StringContent(ValidModelYaml, Encoding.UTF8, "application/x-yaml");
    
    // Act
    var response = await _client.SendAsync(request);
    var result = await response.Content.ReadFromJsonAsync<RunResponse>();
    
    // Assert
    var provenancePath = Path.Combine(_dataDir, result.RunId, "provenance.json");
    Assert.That(File.Exists(provenancePath), Is.False);
}
```

### Integration Tests

```csharp
[Test]
public async Task EndToEnd_SimToEngine_PreservesProvenance()
{
    // 1. Generate model via Sim
    var simResponse = await _simClient.PostAsync("/api/v1/templates/test-template/generate");
    var model = await simResponse.Content.ReadFromJsonAsync<GenerateResponse>();
    
    // 2. Execute via Engine with provenance
    var runRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/run");
    runRequest.Headers.Add("X-Model-Provenance", model.ModelId);
    runRequest.Content = new StringContent(model.Model, Encoding.UTF8, "application/x-yaml");
    
    var runResponse = await _engineClient.SendAsync(runRequest);
    var run = await runResponse.Content.ReadFromJsonAsync<RunResponse>();
    
    // 3. Verify provenance preserved
    var provenance = await _engineClient.GetFromJsonAsync<ProvenanceMetadata>(
        $"/v1/runs/{run.RunId}/provenance.json"
    );
    
    Assert.That(provenance.ModelId, Is.EqualTo(model.ModelId));
    Assert.That(provenance.TemplateId, Is.EqualTo("test-template"));
}
```

## Related Documentation

- **KISS Architecture**: See `flowtime-sim-vnext/docs/architecture/registry-integration.md` (**SUPERSEDES** old model-provenance.md dual-registry approach)
- **Sim-Side Implementation**: FlowTime-Sim generates provenance and provides temporary storage (SIM-M2.7)
- **Engine-Side Implementation**: FlowTime Engine accepts and stores provenance permanently (M2.9 section 2.6)
- **Target Schema**: See `docs/schemas/target-model-schema.md` (provenance schema definition)
- **Artifact Registry**: See `docs/milestones/M2.7.md` (registry architecture)
- **UI Orchestration**: UI retrieves models from Sim and sends to Engine (UI-M3.x)

## Architecture Principles (KISS)

1. **Single Registry**: Engine owns the artifact registry (single source of truth)
2. **Temporary Sim Storage**: Sim stores models temporarily for UI retrieval only
3. **UI Orchestration**: UI coordinates workflow - Sim and Engine do NOT talk directly
4. **Stateless Sim**: Can scale horizontally, no permanent storage responsibility
5. **Standard HTTP**: No custom protocols, just REST APIs and headers

---

**Status**: Architecture defined per KISS principles, Engine-side implementation in M2.9 section 2.6
