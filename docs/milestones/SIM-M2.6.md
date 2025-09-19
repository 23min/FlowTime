# SIM-M2.6 — Model Artifacts Registry Integration

**Status:** 📋 Planned  
**Dependencies:** SIM-M2.1 (PMF Generator Support), FlowTime M2.6 (Export/Import Loop)  
**Alignment:** Enables FlowTime M2.6 artifacts registry with persistent model artifacts  
**Owner:** FlowTime-Sim Team

---

## Goal

Enable FlowTime-Sim to create persistent Model artifacts that integrate seamlessly with FlowTime M2.6's artifacts registry. When users generate models via FlowTime-Sim templates, the models become discoverable, reusable artifacts in the unified catalog system, ensuring the UI "never forgets" previously created models.

## Context & Problem

FlowTime M2.6 introduces a persistent artifacts registry where all models, runs, and imported telemetry become discoverable across sessions. Currently, FlowTime-Sim generates ephemeral model files that disappear after use, breaking the persistence loop. Users lose track of previously created models and cannot easily rerun or iterate on past work.

**Missing Integration:**
- FlowTime-Sim models are not persisted as artifacts with catalog metadata
- No integration between `flowtime sim new` and the artifacts registry
- Models cannot be referenced by artifact ID for runs
- UI cannot display FlowTime-Sim generated models in the artifacts drawer

## Functional Requirements

### **FR-SIM-M2.6-1: Model Artifact Creation**
FlowTime-Sim creates persistent Model artifacts when generating models from templates.

**Artifact Creation:**
- **Storage Layout**: `/artifacts/models/{model_id}/v{n}/` following M2.6 specification
- **Catalog Generation**: Create `catalog.json` with full metadata for each model
- **File Structure**: Include `model.yaml` and optional `preview.svg` DAG visualization
- **Unique IDs**: Generate deterministic model IDs based on template and parameters

**Enhanced CLI Commands:**
```bash
# Create model artifact (new behavior)
flowtime sim new --template checkout --out artifacts --name "checkout-baseline"
# Creates: /artifacts/models/checkout-baseline_a1b2/v1/{model.yaml, catalog.json, preview.svg}

# Legacy file output (preserved for compatibility)  
flowtime sim new --template checkout --out models/checkout.yaml
# Creates: models/checkout.yaml (unchanged behavior)
```

### **FR-SIM-M2.6-2: Catalog Schema Integration**
Model artifacts use the standardized catalog.json schema compatible with FlowTime M2.6.

**Catalog Metadata:**
```json
{
  "kind": "model",
  "id": "checkout-baseline_a1b2", 
  "name": "checkout-baseline",
  "created_utc": "2025-09-19T10:22:30Z",
  "schema_version": "treehouse.binned.v0",
  "template_id": "checkout",
  "template_version": "v1",
  "parameters": {
    "peak_factor": 1.5,
    "base_capacity": 100
  },
  "topology": {
    "nodes": [{"id": "CHECKOUT", "kind": "pmf"}],
    "edges": []
  },
  "capabilities": ["counts", "flows"],
  "tags": ["e-commerce", "checkout", "baseline"],
  "source": "sim",
  "inputs_hash": "sha256:c4f2a8d1...",
  "owner": "user@domain.com",
  "visibility": "private"
}
```

### **FR-SIM-M2.6-3: Service Integration** 
FlowTime-Sim API creates artifacts and accepts artifact IDs for runs.

**Enhanced API Endpoints:**
```http
POST /sim/new                            # Create model artifact from template
POST /sim/run                           # Accept model_id OR raw YAML spec
GET  /sim/models/{model_id}             # Retrieve model artifact
```

**Request/Response Examples:**
```json
// POST /sim/new - Create model artifact
{
  "template": "checkout",
  "name": "checkout-baseline",
  "parameters": {"peak_factor": 1.5},
  "tags": ["baseline", "e-commerce"],
  "artifacts_root": "/artifacts"
}

// Response
{
  "model_id": "checkout-baseline_a1b2",
  "artifact_path": "/artifacts/models/checkout-baseline_a1b2/v1",
  "catalog_url": "/sim/models/checkout-baseline_a1b2"
}

// POST /sim/run - Run using artifact ID
{
  "model_id": "checkout-baseline_a1b2",  // NEW: Use artifact ID
  "seed": 42,
  "artifacts_root": "/artifacts"
}
```

### **FR-SIM-M2.6-4: Template Versioning & Reuse**
Support model artifact versioning and template-based variations.

**Versioning Strategy:**
- **Template Changes**: New template version → new model artifact
- **Parameter Changes**: Different parameters → new model artifact  
- **Explicit Versioning**: `--version` flag for manual version control
- **Variation Creation**: Create new artifacts based on existing models

**Advanced CLI Usage:**
```bash
# Create versioned model artifact
flowtime sim new --template checkout --version v2 --out artifacts --name "checkout-optimized"

# Create variation of existing model
flowtime sim derive --from checkout-baseline_a1b2 --name "checkout-peak" --set peak_factor=2.0 --out artifacts

# List available model artifacts
flowtime sim list --kind model --template checkout

# Run model by artifact ID
flowtime run --model checkout-baseline_a1b2 --out artifacts --seed 42
```

## Technical Architecture

### **Model Artifact Creation Pipeline**
```
Template Selection → Parameter Resolution → Model Generation → Artifact Creation → Registry Integration
        ↓                    ↓                    ↓                    ↓                    ↓
    checkout.yaml      peak_factor=1.5     model.yaml      catalog.json        /v1/artifacts index
    Parameters UI      User/Default        YAML Content     Metadata           Global Registry
```

### **Integration with FlowTime M2.6**
```
FlowTime-Sim Creates → FlowTime API Discovers → FlowTime UI Displays → User Reruns
       ↓                       ↓                       ↓                   ↓
   Model Artifacts        GET /v1/artifacts      Artifacts Drawer    POST /v1/runs  
   catalog.json           Registry Search         Model Cards         Run by ID
```

### **Storage Architecture Integration**
```
/artifacts/                              # Shared artifacts root
  models/                                # Model artifacts (from Sim)
    checkout-baseline_a1b2/v1/
      model.yaml                         # Generated model specification
      catalog.json                       # M2.6-compatible metadata
      preview.svg                        # Optional DAG visualization
  runs/                                  # Run artifacts (from Engine)
    checkout-run_2025-09-19_c3d4/
      binned_v0.csv                      # Engine execution results
      catalog.json                       # Run metadata with model_id reference
  telemetry/                             # Imported artifacts (from M2.6 import)
    prod-data_2025-09-19_e5f6/
      binned_v0.csv                      # Imported external data
      catalog.json                       # Import metadata
```

## Implementation Phases

### **Phase 1: Core Artifact Creation**
- Extend `flowtime sim new` command to support `--out artifacts` mode
- Implement catalog.json generation with M2.6-compatible schema
- Add model artifact storage following `/artifacts/models/{id}/v{n}/` layout
- Generate deterministic model IDs based on template and parameters

### **Phase 2: Service API Integration**
- Add `POST /sim/new` endpoint for artifact creation via API
- Enhance `POST /sim/run` to accept `model_id` parameter
- Add `GET /sim/models/{id}` endpoint for artifact retrieval
- Implement artifact validation and error handling

### **Phase 3: Advanced Features**
- Add model artifact versioning and derivation capabilities
- Implement `flowtime sim derive` command for model variations
- Add `flowtime sim list` command for artifact discovery
- Generate optional DAG preview SVG files

### **Phase 4: Integration Testing**
- End-to-end testing with FlowTime M2.6 artifacts registry
- Validate catalog.json compatibility across FlowTime-Sim and FlowTime API
- Test UI integration with FlowTime-Sim generated artifacts
- Performance testing with artifact creation and discovery

## New Code/Files

```
src/FlowTime.Sim.Core/Artifacts/        # NEW: Artifacts integration
  ModelArtifactCreator.cs                # Create model artifacts with catalog
  ArtifactIdGenerator.cs                 # Generate deterministic model IDs
  CatalogGenerator.cs                    # Generate M2.6-compatible catalog.json
  PreviewRenderer.cs                     # Optional: Generate DAG preview SVG
  
src/FlowTime.Sim.Core/Templates/        # Enhanced template system
  TemplateVersioning.cs                  # Handle template versioning
  ParameterResolver.cs                   # Resolve template parameters
  ModelDerivation.cs                     # Create model variations
  
src/FlowTime.Sim.Cli/Commands/          # Enhanced CLI commands  
  NewCommand.cs                          # Enhanced: --out artifacts support
  DeriveCommand.cs                       # NEW: Create model variations
  ListCommand.cs                         # NEW: List model artifacts
  
src/FlowTime.Sim.Service/Endpoints/     # Enhanced API endpoints
  ModelsEndpoints.cs                     # NEW: POST /sim/new, GET /sim/models/{id}
  RunEndpoints.cs                        # Enhanced: Accept model_id parameter
  
tests/FlowTime.Sim.Tests/Artifacts/     # NEW: Artifacts testing
  ModelArtifactCreatorTests.cs           # Test artifact creation
  CatalogGeneratorTests.cs               # Test catalog.json generation  
  ArtifactIdGeneratorTests.cs            # Test ID generation
  IntegrationTests.cs                    # Test M2.6 compatibility
```

## Acceptance Criteria

### **Model Artifact Creation**
- ✅ `flowtime sim new --out artifacts` creates model artifacts in `/artifacts/models/{id}/v1/`
- ✅ Generated `catalog.json` passes M2.6 schema validation  
- ✅ Model IDs are deterministic based on template and parameters
- ✅ Legacy `--out file.yaml` behavior preserved for compatibility

### **Service Integration**
- ✅ `POST /sim/new` API creates model artifacts via HTTP
- ✅ `POST /sim/run` accepts `model_id` and runs artifact-based models
- ✅ `GET /sim/models/{id}` returns model artifact metadata and files
- ✅ Error handling provides clear guidance for invalid artifact IDs

### **FlowTime M2.6 Compatibility**
- ✅ FlowTime API discovers FlowTime-Sim model artifacts via `GET /v1/artifacts`
- ✅ FlowTime UI displays model artifacts in artifacts drawer
- ✅ FlowTime Engine can run FlowTime-Sim model artifacts by ID
- ✅ Round-trip workflow: Sim creates → Engine runs → UI displays works end-to-end

### **Advanced Features**
- ✅ Model artifact versioning supports template evolution
- ✅ `flowtime sim derive` creates model variations with parameter changes  
- ✅ `flowtime sim list` discovers existing model artifacts
- ✅ Generated artifacts include optional DAG preview images

## Success Metrics

### **Integration Success**
- **Discovery Rate**: >90% of FlowTime-Sim model artifacts appear in FlowTime UI within 30 seconds
- **Reuse Rate**: >60% of model artifacts are run multiple times across sessions  
- **ID Stability**: Model IDs remain consistent across identical template+parameter combinations
- **Compatibility**: 100% of generated catalog.json files validate against M2.6 schema

### **Performance Metrics**
- **Artifact Creation**: <3 seconds to create model artifact including catalog generation
- **API Response**: <500ms for `GET /sim/models/{id}` and `POST /sim/run` with model_id
- **Storage Efficiency**: Catalog.json files remain under 5KB for typical templates
- **Index Performance**: FlowTime registry discovers new artifacts within 10 seconds

## Coordination with FlowTime M2.6

### **Development Synchronization**
- **Catalog Schema**: Use identical catalog.json schema as FlowTime M2.6
- **Artifact Storage**: Follow exact same `/artifacts/` layout specification
- **API Contracts**: Ensure `GET /v1/artifacts` discovers FlowTime-Sim model artifacts
- **Testing**: Joint end-to-end testing with FlowTime M2.6 artifacts registry

### **Implementation Dependencies**
- **M2.6 Schema**: Requires finalized catalog.json schema from FlowTime M2.6
- **Storage Layout**: Must align with M2.6 artifacts storage architecture  
- **API Integration**: Coordinate with M2.6 artifacts API endpoint specifications
- **UI Testing**: Validate with UI-M2.6 artifacts drawer and model cards

## Questions for Review

1. **Storage Root**: Should FlowTime-Sim use the same `FLOWTIME_ARTIFACTS_ROOT` environment variable as FlowTime M2.6?

2. **Model Versioning**: How should template changes trigger new model artifact versions?

3. **Preview Generation**: Is DAG preview SVG generation worth the complexity for initial release?

4. **Artifact Ownership**: Should FlowTime-Sim set `owner` field based on authentication or environment?

5. **Cleanup Strategy**: How should old or unused model artifacts be managed?

## Next Steps

1. **Schema Alignment**: Confirm catalog.json schema compatibility with FlowTime M2.6 team
2. **Storage Configuration**: Define shared artifacts root configuration strategy
3. **API Contracts**: Finalize enhanced FlowTime-Sim API endpoints and error responses  
4. **Integration Testing**: Plan joint testing scenarios with FlowTime M2.6 development
5. **UI Coordination**: Ensure model artifact cards display correctly in UI-M2.6 artifacts drawer

---

## Revision History

| Date | Change | Author |
|------|--------|--------|
| 2025-09-19 | Initial SIM-M2.6 milestone specification created | Assistant |
