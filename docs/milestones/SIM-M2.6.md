# SIM-M2.6 — Charter-Aligned Model Authoring Platform

**Status:** 📋 Planned (Charter-Aligned)  
**Dependencies:** SIM-M2.1 (PMF Generator Support), FlowTime Engine M2.6 (Artifacts Registry)  
**Alignment:** FlowTime-Sim Charter v1.0 - Model authoring platform feeding charter ecosystem  
**Owner:** FlowTime-Sim Team

---

## Goal

Transform **FlowTime-Sim** from demonstration tool to **charter-compliant model authoring platform** that creates and validates model artifacts for the FlowTime-Engine ecosystem. This milestone establishes FlowTime-Sim as the primary path for creating model artifacts that flow seamlessly into the [Models] → [Runs] → [Artifacts] → [Learn] charter workflow.

## Context & Charter Alignment

The **FlowTime-Sim Charter v1.0** defines Sim as the **"modeling front-end"** that generates models but **never computes telemetry**. This milestone implements that vision by positioning FlowTime-Sim as the **model authoring platform**:

- **Model Creation**: Catalog-based model authoring with validation and export
- **Artifact Generation**: Models saved as artifacts compatible with Engine registry
- **Charter Compliance**: Zero telemetry generation - pure model authoring focus
- **Engine Integration**: Seamless handoff from Sim (model authoring) to Engine (execution platform)

This milestone establishes the **charter-compliant ecosystem flow**:
```
FlowTime-Sim (Create Models) → FlowTime-Engine (Execute & Analyze) → Compare & Learn
```

## Functional Requirements

### **FR-SIM-M2.6-1: Charter-Compliant Model Artifact System**
FlowTime-Sim generates Engine-compatible model artifacts while strictly avoiding telemetry generation.

**Charter-Compliant Artifact Creation:**
- **Storage Layout**: `/artifacts/models/{model_id}/v{n}/` following Engine registry specification
- **Metadata Generation**: Create complete metadata for each model with Engine compatibility markers
- **File Structure**: Include `model.yaml` (Engine format) and optional `preview.svg` DAG visualization
- **Unique IDs**: Generate deterministic model IDs based on template and parameters
- **Charter Boundary**: NO telemetry files, NO CSV outputs, NO execution results

**Enhanced CLI Commands (Charter-Aligned):**
```bash
# Create model artifact (new charter-compliant behavior)
flowtime sim new --template checkout --out artifacts --name "checkout-baseline"
# Creates: /artifacts/models/checkout-baseline_a1b2/v1/{model.yaml, metadata.json, preview.svg}

# Legacy file output (preserved for compatibility, schema-only)  
flowtime sim new --template checkout --out models/checkout.yaml
# Creates: models/checkout.yaml (Engine schema, no telemetry)
```

### **FR-SIM-M2.6-2: Engine-Compatible Model Schema**
Model artifacts use Engine-compatible schema for seamless integration with FlowTime Engine.

**Charter-Compliant Model Metadata:**
```json
{
  "apiVersion": "flowtime/v1",
  "kind": "Model",
  "metadata": {
    "id": "checkout-baseline_a1b2", 
    "title": "Checkout Baseline Model",
    "created": "2025-09-19T10:22:30Z",
    "author": "user@domain.com",
    "version": "1.0.0",
    "tags": ["e-commerce", "checkout", "baseline"],
    "sim_source": "template:checkout"
  },
  "spec": {
    "flowtime_model": {
      "grid": {"bins": 1440, "binMinutes": 1},
      "nodes": [
        {"id": "checkout_arrivals", "kind": "pmf", "pmf": {"10": 0.3, "20": 0.7}},
        {"id": "checkout_capacity", "kind": "const", "values": [100]}
      ],
      "outputs": [
        {"series": "checkout_throughput", "as": "throughput.csv"}
      ]
    },
    "sim_config": {
      "template": "checkout",
      "parameters": {"peak_factor": 1.5, "base_capacity": 100}
    },
    "validation": {
      "sim_version": "2.6.0",
      "quality_score": 0.94,
      "engine_compatibility": true
    }
  }
}
```

### **FR-SIM-M2.6-3: Charter-Compliant Service Integration** 
FlowTime-Sim API creates model artifacts without telemetry generation.

**Charter-Aligned API Endpoints:**
```http
POST /sim/models/export                  # Create model artifact from template (no execution)
POST /sim/models/validate               # Structure validation (no telemetry)
GET  /sim/models/{model_id}             # Retrieve model artifact
```

**Charter-Compliant Request/Response Examples:**
```json
// POST /sim/models/export - Create model artifact (no execution)
{
  "template": "checkout",
  "name": "checkout-baseline",
  "parameters": {"peak_factor": 1.5},
  "tags": ["baseline", "e-commerce"],
  "metadata": {
    "title": "Checkout Baseline Model",
    "description": "E-commerce checkout with baseline parameters"
  }
}

// Response (Charter-Compliant)
{
  "model_id": "checkout-baseline_a1b2",
  "artifact_metadata": {
    "id": "checkout-baseline_a1b2",
    "title": "Checkout Baseline Model",
    "engine_compatible": true,
    "quality_score": 0.94
  },
  "validation": {
    "structure_valid": true,
    "engine_compatible": true,
    "warnings": []
  }
}

// POST /sim/models/validate - Structure validation only
{
  "model_schema": {
    "grid": {"bins": 1440, "binMinutes": 1},
    "nodes": [{"id": "checkout", "kind": "pmf", "pmf": {"10": 0.5, "20": 0.5}}],
    "outputs": [{"series": "checkout_throughput", "as": "throughput.csv"}]
  }
}
```

### **FR-SIM-M2.6-4: Charter-Compliant Template System & Model Variations**
Support model artifact versioning and template-based variations without execution.

**Charter-Aligned Versioning Strategy:**
- **Template Evolution**: New template version → new model artifact schema
- **Parameter Variations**: Different parameters → new model artifact  
- **Explicit Versioning**: Version control for model schema iterations
- **Variation Creation**: Create new artifacts based on existing model structures
- **Charter Boundary**: Schema versioning only, no execution versioning

**Charter-Compliant CLI Usage:**
```bash
# Create versioned model artifact (schema only)
flowtime sim new --template checkout --version v2 --out artifacts --name "checkout-optimized"

# Create variation of existing model (structure derivation)
flowtime sim derive --from checkout-baseline_a1b2 --name "checkout-peak" --set peak_factor=2.0 --out artifacts

# List available model artifacts (metadata only)
flowtime sim list --kind model --template checkout

# Validate model structure (no execution)
flowtime sim validate --model checkout-baseline_a1b2
```

## Technical Architecture (Charter-Compliant)

### **Charter-Aligned Model Creation Pipeline**
```
Template Selection → Parameter Resolution → Schema Generation → Artifact Creation → Engine Handoff
        ↓                    ↓                    ↓                    ↓                    ↓
    checkout.yaml      peak_factor=1.5     model.yaml      metadata.json      Engine Registry
    Parameters UI      User/Default        Engine Schema   Charter Metadata   Ready for Execution
```

### **Charter-Compliant Integration with FlowTime Engine**
```
FlowTime-Sim Creates → Engine Discovers → Engine UI Displays → Engine Executes
       ↓                       ↓                ↓                     ↓
   Model Artifacts        Registry Search    Model Catalog      Execution & Telemetry
   metadata.json          Engine API         Engine UI          (Charter Boundary)
```

### **Charter-Aligned Storage Architecture**
```
/artifacts/                              # Shared artifacts root
  models/                                # Model artifacts (from Sim - Charter Compliant)
    checkout-baseline_a1b2/v1/
      model.yaml                         # Engine-compatible model schema
      metadata.json                      # Charter-compliant metadata
      preview.svg                        # Optional DAG visualization
      validation.json                    # Structure validation results
  # Engine Responsibility (Charter Boundary):
  # runs/                              # Run artifacts (Engine only)
  # telemetry/                         # Execution results (Engine only)
```

## Implementation Phases

### **Phase 1: Charter-Compliant Model Artifact System**
- Extend `flowtime sim new` command for charter-compliant artifact creation
- Implement Engine-compatible metadata generation 
- Add model artifact storage following `/artifacts/models/{id}/v{n}/` layout
- **Charter Compliance**: Zero telemetry generation, structure validation only

### **Phase 2: Engine Integration API**
- Add `POST /sim/models/export` endpoint for charter-compliant artifact creation
- Add `POST /sim/models/validate` for structure validation (no execution)
- Add `GET /sim/models/{id}` endpoint for artifact retrieval
- **Charter Boundary**: Model preparation only, Engine handles execution

### **Phase 3: Advanced Model Authoring**
- Add model artifact versioning and derivation capabilities
- Implement `flowtime sim derive` command for model variations
- Add `flowtime sim validate` command for structure checking
- Generate optional DAG preview without execution requirements

### **Phase 4: Charter Compliance Testing**
- End-to-end testing with Engine artifacts registry integration
- Validate Engine-compatible schema across all model generation
- Test charter compliance - verify zero telemetry generation
- Performance testing with model preparation and validation

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

## Acceptance Criteria (Charter-Compliant)

### **Charter-Compliant Model Creation**
- ✅ `flowtime sim export` creates Engine-compatible model artifacts in `/artifacts/models/{id}/v1/`
- ✅ Generated metadata passes Engine schema validation  
- ✅ Model IDs are deterministic based on template and parameters
- ✅ **Charter Compliance**: Zero telemetry files generated during artifact creation

### **Charter-Aligned Service Integration**
- ✅ `POST /sim/models/export` API creates model artifacts via HTTP (no execution)
- ✅ `POST /sim/models/validate` validates model structure (no telemetry generation)
- ✅ `GET /sim/models/{id}` returns model artifact metadata and files
- ✅ **Charter Boundary**: All endpoints respect no-telemetry generation rule

### **Engine Integration Compatibility**
- ✅ Engine API discovers FlowTime-Sim model artifacts via registry
- ✅ Engine UI displays model artifacts in model catalog
- ✅ Engine can execute FlowTime-Sim model artifacts (execution in Engine only)
- ✅ **Charter Workflow**: Sim creates → Engine executes → Engine provides telemetry

### **Charter-Compliant Advanced Features**
- ✅ Model artifact versioning supports template schema evolution
- ✅ `flowtime sim derive` creates model variations with parameter changes (structure only)
- ✅ `flowtime sim validate` validates model structure without execution
- ✅ Generated artifacts include optional DAG preview (no telemetry visualization)

## Success Metrics (Charter-Aligned)

### **Charter Compliance Success**
- **Boundary Respect**: 100% compliance with charter - zero telemetry generation in Sim
- **Engine Compatibility**: >95% of Sim-created models execute successfully in Engine
- **Structure Quality**: >90% of models pass Engine structure validation
- **User Understanding**: Users clearly understand Sim vs Engine responsibilities

### **Performance Metrics**
- **Model Preparation**: <3 seconds to create Engine-compatible model artifact
- **API Response**: <500ms for `GET /sim/models/{id}` and `POST /sim/models/export`
- **Storage Efficiency**: Model metadata files remain under 5KB for typical templates
- **Validation Speed**: Structure validation completes in <1 second for standard models

## Charter-Compliant Coordination

### **Engine Integration Synchronization**
- **Model Schema**: Use Engine-compatible model schema for seamless integration
- **Artifact Storage**: Follow Engine registry layout specification
- **API Contracts**: Ensure Engine can discover FlowTime-Sim model artifacts
- **Charter Boundaries**: Clear separation between Sim preparation and Engine execution

### **Implementation Dependencies**
- **Engine Schema**: Requires finalized Engine model schema specification
- **Storage Layout**: Must align with Engine artifacts registry architecture  
- **API Integration**: Coordinate with Engine artifacts API endpoint specifications
- **Charter Compliance**: Validate all features respect charter boundaries

## Charter Compliance Questions

1. **Validation Scope**: Should Sim validation include execution testing or remain structure-only?

2. **Engine Integration**: How should model preparation hand off to Engine execution?

3. **Preview Generation**: Can DAG preview be generated without violating charter boundaries?

4. **Quality Metrics**: What quality assessments are charter-compliant (no execution required)?

5. **Template Evolution**: How should template versioning work within charter constraints?

## Next Steps

1. **Charter Validation**: Ensure all features comply with FlowTime-Sim Charter v1.0
2. **Engine Coordination**: Align model schema with Engine requirements
3. **API Design**: Finalize charter-compliant API endpoints and contracts
4. **Integration Testing**: Plan Sim→Engine workflow testing scenarios
5. **Documentation**: Create user guides emphasizing charter boundaries and workflow

---

## Revision History

| Date | Change | Author |
|------|--------|--------|
| 2025-09-19 | Initial SIM-M2.6 milestone specification created | Assistant |
| 2025-09-20 | Rewritten for FlowTime-Sim Charter v1.0 compliance | Assistant |
