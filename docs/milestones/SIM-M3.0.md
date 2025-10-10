# Milestone SIM-M3.0 — FlowTime-Sim Charter Alignment

**Status:** 📋 Planned (Charter-Aligned)  
**Dependencies:** SIM-M2.7 (Registry Preparation), SIM-M2.8 (Model Authoring Service & API), SIM-M2.9 (Compare Integration), Engine M2.7 (Artifacts Registry), Engine M2.9 (Compare Infrastructure)  
**Target:** FlowTime-Sim as model authoring platform feeding charter ecosystem  
**Date:** 2025-10-01

---

## Goal

Transform **FlowTime-Sim** from demonstration tool to **model authoring platform** that creates and validates model artifacts for the FlowTime-Engine charter ecosystem. This milestone establishes FlowTime-Sim as the primary path for creating model artifacts that flow seamlessly into the [Models] → [Runs] → [Artifacts] → [Learn] charter workflow.

## Context & Charter Alignment

The **FlowTime-Engine Charter** positions model artifacts as the **input to the ecosystem**, but doesn't specify how models are created. FlowTime-Sim becomes the **model authoring platform**:

- **Model Creation**: Catalog-based model authoring with validation and export
- **Artifact Generation**: Models saved as artifacts compatible with charter registry
- **Integration Testing**: Sim validates models work correctly before export to engine
- **Charter Bridge**: Seamless handoff from Sim (model authoring) to Engine (execution platform)

This milestone establishes the **complete ecosystem flow**:
```
FlowTime-Sim (Create Models) → FlowTime-Engine (Execute & Analyze) → Compare & Learn
```

## Functional Requirements

### **FR-SIM-M3.0-1: Model Artifact Export System**
FlowTime-Sim generates charter-compatible model artifacts for seamless engine integration.

**Model Artifact Schema:**
```yaml
# /artifacts/models/model-{id}.yaml
kind: "Model"
schemaVersion: 1
metadata:
  id: "manufacturing_line_2024"
  title: "Manufacturing Line Model Q4 2024"
  description: "Production line with seasonal demand patterns and capacity constraints"
  created: "2024-09-20T10:30:00Z"
  author: "operations@acme.com"
  version: "1.2.0"
  tags: ["manufacturing", "capacity", "seasonal"]

spec:
    grid:
      bins: 2160        # 90 days * 24 hours = 2160 bins
      binMinutes: 60    # 1-hour time steps
      
    nodes:
      # Input demand with seasonal pattern
      - id: demand_pattern
        kind: pmf
        pmf: 
          "80": 0.1    # Low demand (10% of time)
          "100": 0.6   # Normal demand (60% of time)  
          "150": 0.3   # High demand (30% of time)
        
      # Seasonal multiplier
      - id: seasonal_factor
        kind: const
        values: [1.0, 1.0, 1.2, 1.2, 1.5, 1.5, 1.3, 1.3, 1.1, 1.1, 1.0, 1.0]  # Monthly pattern
        
      # Combined seasonal demand
      - id: seasonal_demand
        kind: expr
        expr: "demand_pattern * seasonal_factor"
        
      # Production capacity constraint
      - id: capacity_limit
        kind: const
        values: [120]  # Max 120 units/hour
        
      # Actual throughput (min of demand and capacity)
      - id: throughput
        kind: expr
        expr: "min(seasonal_demand, capacity_limit)"
        
      # Utilization percentage
      - id: utilization
        kind: expr
        expr: "throughput / capacity_limit"
        
      # Queue buildup when demand exceeds capacity
      - id: queue_length
        kind: expr  
        expr: "max(0, seasonal_demand - capacity_limit)"
        
    outputs:
      - series: throughput
        as: throughput.csv
      - series: utilization  
        as: utilization.csv
      - series: queue_length
        as: queue_length.csv
  
  # Original Sim configuration metadata
  sim_config:
    catalog: "manufacturing-systems.yaml"
    scenario: "q4-forecast"
    parameters:
      demand_pattern: "seasonal"
      capacity_utilization: 0.85
      maintenance_schedule: "weekly"
      buffer_size: 100
    
  # Validation metadata from Sim
  validation:
    sim_version: "3.0.1"
    validation_date: "2024-09-20T10:25:00Z"
    test_scenarios: 
      - "baseline_validation"
      - "capacity_stress_test"
    quality_score: 0.94
    engine_compatibility: true

# Model assets bundle
assets:
  - path: "catalog/manufacturing-systems.yaml"
    type: "catalog"
  - path: "scenarios/q4-forecast.yaml"  
    type: "scenario"
  - path: "validation/test-results.json"
    type: "validation_results"
  - path: "flowtime-model.yaml"
    type: "engine_model"
```

**Registry Integration Architecture:**

FlowTime-Sim integrates with Engine artifacts registry through API abstraction:

• **API-Based Access Pattern:**
  - Sim uses Engine registry REST API endpoints (no direct file system access)
  - Engine registry API abstracts file-based storage implementation
  - Clean separation between Sim (model authoring) and Engine (model execution/storage)
  - Future database migration transparent to Sim integration

• **Integration Flow:**
  ```
  FlowTime-Sim → Engine Registry API → File-Based Storage
       ↓                 ↓                    ↓
  Model Export    POST /v1/artifacts    Structured Files
       ↓                 ↓                    ↓
  Engine Access   Artifact Metadata     Registry Index
  ```

**Model Export Service Requirements:**

• **Core Export Functions:**
  - Export current Sim configuration as complete model artifact
  - Convert Sim catalog/scenario into FlowTime engine model (nodes and edges)
  - Validate exported model for Engine schemaVersion 1 compatibility
  - Test engine model execution before export to ensure quality
  - Bundle all assets (catalogs, scenarios, engine YAML) for transport
  - Upload model artifacts directly to Engine registry via REST API endpoints

• **Export Options:**
  - Configurable title, description, and tags for model identification
  - Adjustable time horizon and time step for engine execution
  - Selectable output series for specific analysis needs
  - Optional validation inclusion for quality assurance
  - Asset bundling control for complete or minimal exports

### **FR-SIM-M3.0-2: Charter-Aware Model Creation UI**
Enhanced FlowTime-Sim UI for charter ecosystem model authoring workflow.

### **FR-SIM-M3.0-3: Model Validation & Quality Assurance**
Comprehensive validation system ensuring exported models work correctly in Engine.

### **FR-SIM-M3.0-4: Charter Integration Workflow**
Complete workflow from Sim model creation to Engine execution with artifacts registry integration.

## Integration Points

### **SIM-M2.7 Registry Integration Foundation**
- Model artifact creation and metadata management built on SIM-M2.7 foundation
- Registry API client developed in SIM-M2.7 enables M3.0 model upload functionality

### **SIM-M2.8 Model Authoring UI Enhancement**
- Charter-aware model creation UI built on SIM-M2.8 UI improvements
- Model template system and authoring dashboard from SIM-M2.8 supports M3.0 workflows

### **SIM-M2.9 Compare Integration Support**
- Model validation and quality assurance from SIM-M2.9 enables M3.0 export quality gates
- Engine integration testing from SIM-M2.9 supports M3.0 charter workflow integration

### **Engine M2.7 Artifacts Registry Integration**
- Model artifacts uploaded via API are stored in Engine's file-based registry
- Registry API provides abstraction layer for model artifact management
- Registry enables model discovery and reuse across Engine workflows

### **Engine M2.9 Compare Workflow Integration**
- Models exported from Sim can be used as comparison inputs in Engine Compare workflow
- Charter Compare workflow supports fresh model execution from Sim artifacts

## Acceptance Criteria

### **Model Artifact Creation**
- ✅ Models created in Sim can be exported as charter-compatible artifacts
- ✅ Exported models include all necessary assets (catalogs, scenarios, validation)
- ✅ Model artifacts validate successfully for Engine execution
- ✅ Model metadata captures Sim validation and quality assessment

### **Charter Workflow Integration**
- ✅ Model upload from Sim to Engine completes successfully via API
- ✅ Uploaded models appear correctly in Engine artifacts registry
- ✅ Models can be executed in Engine using charter workflow (Models → Runs)
- ✅ Execution results flow back through charter pipeline (Artifacts → Learn)

### **Quality & Validation**
- ✅ Sim validation catches common model configuration errors
- ✅ Quality assessment provides meaningful scores and recommendations
- ✅ Models that pass Sim validation execute successfully in Engine
- ✅ Validation reports help users improve model quality

## Implementation Plan

### **Phase 1: Model Artifact System**
1. **Model artifact schema** definition and serialization
2. **Model export service** with validation and bundling
3. **Model templates** system for common use cases
4. **Basic validation framework** with syntax and semantic checks

### **Phase 2: Charter Integration API**
1. **Charter integration service** with Engine API client
2. **Model upload endpoints** in Sim Service
3. **Engine integration** for model artifact reception
4. **Artifact registry integration** for model storage and discovery

### **Phase 3: Enhanced UI & Validation**
1. **Model authoring dashboard** with template-based creation
2. **Comprehensive validation** with quality assessment
3. **Charter workflow panel** showing integration status
4. **Test execution** and results visualization in Sim

### **Phase 4: Workflow Polish & Testing**
1. **End-to-end workflow testing** from Sim to Engine
2. **Error handling and recovery** for integration failures
3. **Performance optimization** for large model artifacts
4. **Documentation** and user guides for charter workflow

---

## Next Steps

1. **SIM-M3.1**: Advanced catalog authoring and sharing capabilities
2. **SIM-M3.2**: Enhanced model analytics and comparison features
3. **Cross-platform integration**: Deep Engine + Sim workflow integration

This milestone establishes **FlowTime-Sim as the model authoring platform** for the charter ecosystem, creating seamless flow from model creation to execution to analysis within the FlowTime platform family.
