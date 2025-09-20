# SIM-M3.0-PART2 — Artifacts Registry Integration

> **📋 Charter Alignment**: This milestone is now part of SIM-M3.0 charter milestone to align with [FlowTime-Engine Charter Roadmap](../../../flowtime-vnext/docs/milestones/CHARTER-ROADMAP.md).

**Status:** 📋 Planned (Charter-Aligned)  
**Dependencies:** SIM-M3.0-PART1 (Charter-Aligned Model Authoring), FlowTime Engine M2.7 (Artifacts Registry Foundation)  
**Target:** FlowTime-Sim model artifacts discoverable in Engine registry system  
**Date:** 2025-10-15

---

## Goal

Enable **FlowTime-Sim model artifacts** to integrate seamlessly with the **FlowTime Engine M2.7 Artifacts Registry**. This milestone ensures that model artifacts created by Sim become discoverable, searchable, and selectable within the Engine's charter UI workflows, particularly the Runs wizard "Select Input" step.

## Context & Charter Alignment

The **FlowTime Engine M2.7 Registry** provides the persistent backbone for the charter UI workflow. FlowTime-Sim must integrate with this registry to ensure:

- **Model Discovery**: Sim-created models appear in Engine artifact listings
- **Charter UI Support**: Models become selectable inputs for Engine Runs wizard
- **Persistent Integration**: "Never forget" principle extends to Sim model artifacts
- **Charter Boundaries**: Registry integration respects Sim authoring vs Engine execution separation

This milestone completes the **charter ecosystem integration**:
```
FlowTime-Sim (Create + Register Models) → Engine Registry (Discover Models) → Engine Execution (Run Models)
```

## Functional Requirements

### **FR-SIM-M2.7-1: Registry-Compatible Model Artifacts**
Ensure Sim model artifacts integrate seamlessly with Engine M2.7 Registry structure.

**Registry Integration Schema:**
```json
// Model artifact metadata for Engine Registry
{
  "id": "manufacturing_line_v1_a1b2",
  "type": "model",
  "title": "Manufacturing Line Model v1.0",
  "description": "Production line with seasonal demand patterns",
  "created": "2025-09-20T10:30:00Z",
  "source": "flowtime-sim",
  "schema_version": "v1",
  "capabilities": ["executable", "parameterizable", "validatable"],
  "tags": ["manufacturing", "capacity", "seasonal"],
  "metadata": {
    "sim_template": "manufacturing-line",
    "sim_version": "2.7.0",
    "engine_compatible": true,
    "parameter_count": 4,
    "validation_score": 0.94
  },
  "relationships": {
    "template_source": "template:manufacturing-line",
    "derived_from": null,
    "generates": []
  },
  "files": [
    {
      "name": "model.yaml",
      "type": "engine_model",
      "size_bytes": 2048,
      "content_type": "application/x-yaml"
    },
    {
      "name": "metadata.json", 
      "type": "metadata",
      "size_bytes": 1024,
      "content_type": "application/json"
    },
    {
      "name": "preview.svg",
      "type": "visualization", 
      "size_bytes": 4096,
      "content_type": "image/svg+xml"
    }
  ]
}
```

**Registry Storage Integration:**
```
/data/                              # Engine Registry root
├── registry-index.json             # Engine registry index
├── models/                         # Model artifacts (from Sim)
│   └── manufacturing_line_v1_a1b2/
│       ├── model.yaml              # Engine-compatible model
│       ├── metadata.json           # Registry metadata
│       └── preview.svg             # Optional DAG preview
├── runs/                           # Run artifacts (from Engine)
│   └── run_20250920T080707Z_*/
└── telemetry/                      # Imported telemetry (from Engine)
```

### **FR-SIM-M2.7-2: Auto-Registration Service**
Automatically register Sim model artifacts with Engine Registry upon creation.

**Auto-Registration Workflow:**
```
1. Sim creates model artifact (SIM-M2.6)
2. Registry service detects new model
3. Extract metadata from Sim artifact
4. Generate registry-compatible entry
5. Update Engine registry index
6. Model becomes discoverable in Engine
```

**Registry Integration Endpoints:**
```http
# Registry integration (called by Sim after model creation)
POST /sim/v1/registry/models         # Register model with Engine registry
PUT  /sim/v1/registry/models/{id}    # Update model registry entry
GET  /sim/v1/registry/status         # Check registry integration health

# Registry query (proxy to Engine registry)
GET  /sim/v1/registry/artifacts      # List all artifacts (proxy to Engine)
GET  /sim/v1/registry/models         # List model artifacts specifically
```

**Auto-Registration Implementation:**
```csharp
public class SimRegistryIntegrationService
{
    private readonly IEngineRegistryClient _engineRegistry;
    private readonly IModelArtifactExporter _modelExporter;

    public async Task RegisterModelAsync(string modelId)
    {
        // Get model artifact from Sim
        var modelArtifact = await _modelExporter.GetModelArtifactAsync(modelId);
        
        // Transform to registry-compatible format
        var registryEntry = new RegistryArtifact
        {
            Id = modelId,
            Type = "model",
            Source = "flowtime-sim",
            Metadata = ExtractRegistryMetadata(modelArtifact),
            Files = ListArtifactFiles(modelArtifact)
        };
        
        // Register with Engine registry
        await _engineRegistry.RegisterArtifactAsync(registryEntry);
    }
}
```

### **FR-SIM-M2.7-3: Charter-Compliant Registry Discovery**
Enable Engine to discover and utilize Sim model artifacts while respecting charter boundaries.

**Engine Registry Integration:**
- **Model Discovery**: Engine registry scanning detects Sim model artifacts automatically
- **Metadata Extraction**: Registry reads Sim metadata without requiring execution
- **Charter Boundaries**: Registry provides metadata only, Engine handles all execution
- **File Access**: Engine can access model files for execution via registry API

**Registry Query Integration:**
```typescript
// Engine UI: Select model for run (charter UI workflow)
const modelArtifacts = await registryApi.getArtifacts({
  type: 'model',
  source: 'flowtime-sim',
  capabilities: ['executable']
});

// Display model cards with Sim metadata
modelArtifacts.forEach(model => {
  displayModelCard({
    title: model.title,
    description: model.description,
    template: model.metadata.sim_template,
    validationScore: model.metadata.validation_score,
    created: model.created
  });
});
```

**Charter-Compliant File Access:**
```csharp
// Engine accessing Sim model for execution
public async Task<ModelDefinition> LoadSimModelAsync(string modelId)
{
    // Get model artifact info from registry
    var artifact = await _registry.GetArtifactAsync(modelId);
    
    // Charter boundary: Engine loads model file, never accesses Sim execution
    var modelYaml = await _registry.ReadFileAsync(modelId, "model.yaml");
    
    // Parse and return for Engine execution
    return ModelDefinition.FromYaml(modelYaml);
}
```

### **FR-SIM-M2.7-4: Registry Synchronization & Health Monitoring**
Maintain synchronization between Sim and Engine registries with health monitoring.

**Registry Synchronization:**
- **Automatic Sync**: New Sim models appear in Engine registry within 30 seconds
- **Health Monitoring**: Track registry integration status and connection health  
- **Conflict Resolution**: Handle duplicate model IDs and metadata conflicts
- **Cleanup Integration**: Deleted Sim models removed from Engine registry appropriately

**Health Monitoring Dashboard:**
```json
// GET /sim/v1/registry/health
{
  "engine_registry": {
    "status": "connected|disconnected|error",
    "last_sync": "2025-09-20T10:30:00Z",
    "pending_registrations": 2,
    "sync_errors": []
  },
  "sim_models": {
    "total_models": 15,
    "registered_models": 13,
    "unregistered_models": 2,
    "failed_registrations": 0
  },
  "performance": {
    "avg_registration_time_ms": 150,
    "registry_query_time_ms": 45,
    "last_health_check": "2025-09-20T10:29:45Z"
  }
}
```

**Registry Maintenance Operations:**
```bash
# CLI commands for registry maintenance
flowtime sim registry sync               # Force sync all models to Engine registry
flowtime sim registry status            # Display registry integration health  
flowtime sim registry resync {model_id} # Resync specific model
flowtime sim registry validate          # Validate all registered models
```

## Integration Points

### **SIM-M2.6 Model Authoring Integration**
- Build on model artifact creation from SIM-M2.6
- Extend export workflow to include automatic registry integration
- Maintain charter compliance from SIM-M2.6 - no execution, only registration

### **Engine M2.7 Registry Integration**
- Utilize Engine registry API endpoints for model registration
- Follow Engine registry schemas and metadata requirements
- Integrate with Engine file serving for model access during execution

### **Charter UI Workflow Support**
- Enable "Select Input" step in Engine Runs wizard
- Support model browsing and selection via registry
- Provide model metadata for informed selection decisions

### **Template System Integration**
- Link registered models back to their source templates
- Support template-based model discovery and categorization
- Enable template versioning through registry metadata

## Technical Architecture

### **Registry Integration Service**
```csharp
public interface ISimRegistryService
{
    // Model registration
    Task RegisterModelAsync(string modelId);
    Task UnregisterModelAsync(string modelId);
    Task UpdateModelMetadataAsync(string modelId, ModelMetadata metadata);
    
    // Registry synchronization
    Task SyncAllModelsAsync();
    Task<RegistryHealth> GetHealthStatusAsync();
    
    // Registry queries (proxy to Engine)
    Task<IEnumerable<RegistryArtifact>> ListArtifactsAsync(ArtifactQuery query);
    Task<RegistryArtifact> GetArtifactAsync(string artifactId);
}
```

### **Registry Client Architecture**
```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│   FlowTime-Sim  │    │  Registry Sync   │    │ Engine Registry │
│                 │    │     Service      │    │                 │
│ Model Creation  ├───►│                  ├───►│ Artifact Index  │
│ (SIM-M2.6)      │    │ Auto-Registration│    │ Model Discovery │
└─────────────────┘    └──────────────────┘    └─────────────────┘
                                │                        │
                                ▼                        ▼
                       ┌──────────────────┐    ┌─────────────────┐
                       │ Health Monitoring│    │   Engine UI     │
                       │ Status Dashboard │    │ Model Selection │
                       └──────────────────┘    └─────────────────┘
```

## Acceptance Criteria

### **Registry Integration**
- ✅ All Sim model artifacts appear in Engine registry within 30 seconds of creation
- ✅ Engine UI can discover and select Sim models for execution
- ✅ Registry metadata includes all necessary information for informed model selection
- ✅ **Charter Compliance**: Registry integration never triggers model execution in Sim

### **Engine Workflow Support**
- ✅ Engine Runs wizard "Select Input" step displays Sim models correctly
- ✅ Engine can load and execute Sim models via registry file access
- ✅ Model selection UI shows Sim-specific metadata (template, validation score, etc.)
- ✅ **Charter Boundary**: Engine handles all execution, Sim provides model definitions only

### **Registry Synchronization**
- ✅ Registry health monitoring detects integration issues automatically
- ✅ Failed registrations are retried with exponential backoff
- ✅ Registry sync operations complete without data corruption
- ✅ Deleted Sim models are properly handled in Engine registry

### **Performance & Reliability**
- ✅ Model registration completes in < 500ms for typical models
- ✅ Registry queries respond in < 200ms for model discovery
- ✅ Registry integration survives Engine restarts without losing models
- ✅ Health dashboard provides actionable information for troubleshooting

## Implementation Plan

### **Phase 1: Registry Client & Integration (Week 1)**
1. **Engine registry client** - HTTP client for Engine M2.7 registry API
2. **Model registration service** - Auto-register Sim models with Engine
3. **Metadata transformation** - Convert Sim metadata to registry format
4. **Basic health monitoring** - Track registration success/failure

### **Phase 2: Auto-Registration Workflow (Week 2)**
1. **SIM-M2.6 integration** - Extend model export to trigger registration
2. **Registry synchronization** - Batch sync and conflict resolution
3. **File access integration** - Enable Engine to access Sim model files
4. **Error handling** - Robust retry and failure recovery

### **Phase 3: Health Monitoring & Management (Week 2-3)**
1. **Health dashboard** - Registry status monitoring and diagnostics
2. **CLI maintenance commands** - Manual sync and validation tools
3. **Performance monitoring** - Track registration and query performance
4. **Registry cleanup** - Handle model deletion and orphaned entries

### **Phase 4: Charter UI Integration Testing (Week 3)**
1. **Engine UI integration** - Test model selection in Runs wizard
2. **End-to-end workflow** - Validate Sim→Registry→Engine→Execution flow
3. **Charter compliance validation** - Ensure boundaries respected throughout
4. **Performance optimization** - Optimize for charter UI responsiveness

## Risk Mitigation

### **Registry Dependency Risk**
**Risk:** Engine registry unavailable breaks Sim model creation workflow  
**Mitigation:**
- Asynchronous registration - model creation succeeds even if registry fails
- Queue failed registrations for retry when registry becomes available
- Graceful degradation - Sim functions normally without registry integration

### **Charter Boundary Risk**
**Risk:** Registry integration inadvertently triggers execution in Sim  
**Mitigation:**
- Clear separation between registration (metadata) and execution (Engine only)
- Registry operations are pure metadata and file access - no model execution
- Comprehensive charter compliance testing throughout integration

### **Performance Risk**
**Risk:** Registry operations slow down Sim model creation  
**Mitigation:**
- Asynchronous registration after model creation completes
- Background sync processes don't block user workflows
- Performance monitoring with alerts for degradation

## Success Metrics

### **Integration Success**
- **Discovery Rate**: 100% of Sim models appear in Engine registry within 30 seconds
- **Workflow Completion**: Engine users can select and execute Sim models via registry
- **Metadata Accuracy**: Registry metadata enables informed model selection decisions
- **Charter Compliance**: Zero execution triggered in Sim during registry operations

### **Performance Metrics**
- **Registration Time**: < 500ms average for model registration
- **Query Performance**: < 200ms for registry queries from Engine UI
- **Sync Reliability**: 99%+ successful registration rate for valid models
- **Health Monitoring**: < 10 seconds to detect and alert on registry issues

## Next Steps

1. **SIM-M2.8**: Advanced model management and versioning via registry
2. **SIM-M3**: Charter-aligned backlog and queueing system integration
3. **Engine M2.8 UI**: Charter UI restructure with full registry integration

---

## Revision History

| Date | Change | Author |
|------|--------|--------|
| 2025-09-20 | Initial SIM-M2.7 milestone specification created | Assistant |

This milestone establishes **FlowTime-Sim as a fully integrated model authoring platform** within the Engine registry ecosystem, enabling seamless charter UI workflows while maintaining strict charter compliance boundaries.
