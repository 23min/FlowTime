# Milestone SIM-M3.0 — Charter-Aligned Model Authoring Platform

> **📋 Charter Alignment**: This milestone number has been updated to align with [FlowTime-Engine Charter Roadmap](../../../flowtime-vnext/docs/milestones/CHARTER-ROADMAP.md) which references SIM-M3.0 as the Sim charter milestone.

**Status:** 📋 Planned (Charter-Aligned)  
**Dependencies:** SIM-M2.1 (PMF Generator Support), FlowTime Engine M2.7 (Artifacts Registry Foundation)  
**Target:** FlowTime-Sim as charter-compliant model authoring platform  
**Date:** 2025-10-01

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

**Model Artifact Schema:**
```yaml
# /artifacts/models/model-{id}.yaml
apiVersion: "flowtime/v1"
kind: "Model"
metadata:
  id: "manufacturing_line_2024"
  title: "Manufacturing Line Model Q4 2024"
  description: "Production line with seasonal demand patterns and capacity constraints"
  created: "2024-09-20T10:30:00Z"
  author: "operations@acme.com"
  version: "1.2.0"
  tags: ["manufacturing", "capacity", "seasonal"]
  sim_source: "template:manufacturing-line"

spec:
  # FlowTime Engine Model Definition (schemaVersion 1 compatible)
  flowtime_model:
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
    template: "manufacturing-line"
    parameters:
      demand_pattern: "seasonal"
      capacity_utilization: 0.85
      maintenance_schedule: "weekly"
      buffer_size: 100
    
  # Validation metadata from Sim
  validation:
    sim_version: "2.6.0"
    validation_date: "2024-09-20T10:25:00Z"
    test_scenarios: 
      - "baseline_validation"
      - "parameter_bounds_check"
    quality_score: 0.94
    engine_compatibility: true

# Model assets bundle
assets:
  - path: "template/manufacturing-line.yaml"
    type: "template"
  - path: "validation/test-results.json"
    type: "validation_results"
  - path: "flowtime-model.yaml"
    type: "engine_model"
```

**Charter-Compliant Export Service:**

The model export service converts FlowTime-Sim configurations into Engine-compatible model artifacts without generating any telemetry:

• **Core Export Functions:**
  - Export current Sim configuration as complete model artifact
  - Convert Sim template/parameters into FlowTime engine model (nodes and edges)
  - Validate exported model for Engine schemaVersion 1 compatibility
  - **Charter Compliance**: NO telemetry computation - validation only checks model structure
  - Bundle all assets (templates, engine YAML) for transport
  - Prepare model artifacts for Engine registry via API

• **Model Artifact Structure:**
  - **Metadata**: Title, description, author, version, tags, creation date
  - **FlowTime Engine Model**: Complete nodes/edges definition with grid, expressions, outputs
  - **Sim Configuration**: Original template and parameters for reference
  - **Validation Results**: Structure validation, compatibility checks (no execution)
  - **Asset Bundle**: All source files (templates, generated engine YAML)

• **Charter-Compliant Export Options:**
  - Configurable title, description, and tags for model identification
  - Adjustable time horizon and time step for engine execution planning
  - Selectable output series definitions (schema only, no data generation)
  - Structure validation inclusion for quality assurance
  - Asset bundling control for complete or minimal exports

**FlowTime-Sim API Endpoints:**

New REST API endpoints enable charter-compliant model artifact creation:

• **POST /v1/models/export** - Export Model Artifact (Charter-Compliant)
  - Input: Sim template ID and export options
  - Process: Convert Sim config to engine model schema, validate structure compatibility
  - **Charter Boundary**: NO execution, NO telemetry generation, structure validation only
  - Output: Complete model artifact bundle with engine YAML schema
  - Optional: Prepare for Engine registry upload (metadata only)

• **POST /v1/models/validate** - Validate Engine Compatibility
  - Input: Model artifact with FlowTime engine model definition
  - Process: Validate nodes/edges schema, expression syntax, grid configuration
  - **Charter Boundary**: Structure validation only, no execution or telemetry
  - Output: Validation results with specific error messages and suggestions

• **POST /v1/models/preview** - Generate Model Preview (Charter-Compliant)
  - Input: FlowTime engine model (nodes, grid, outputs)
  - Process: Generate DAG visualization, parameter summary, configuration preview
  - **Charter Boundary**: Visual representation only, no execution or data generation
  - Output: Preview results including DAG SVG, parameter table, model summary

### **FR-SIM-M2.6-2: Charter-Aware Model Creation UI**
Enhanced FlowTime-Sim UI for charter ecosystem model authoring workflow.

**Model Authoring UI Requirements:**

Charter-aware model creation interface with guided workflow for Engine compatibility:

• **Model Authoring Dashboard** - `/models/create`
  - Header with "Model Authoring" title and "Create and validate models for FlowTime Engine" subtitle
  - "New Model" button to start fresh model creation
  - Template gallery showing pre-built model templates (Manufacturing, Service Queue, IT Infrastructure)

• **Model Configuration Tabs:**
  - **Basic Info Tab**: Model title, description, author, tags, version information
  - **Template Tab**: Template selection and parameter configuration for Sim model structure
  - **Parameters Tab**: Stochastic inputs, PMF definitions, and system parameters
  - **Schema Tab**: Time horizon, time step, output series definitions for Engine planning

• **Actions Panel** (Right Side):
  - **Validation Section**: "Validate Model" button with results display showing compatibility status
  - **Preview Section**: "Generate Preview" button (enabled after validation) with DAG visualization
  - **Export Section**: "Create Model Artifact" button (enabled after quality gate) with Engine upload option

• **Charter-Compliant Export Workflow:**
  - Export only available after successful structure validation AND quality score > 0.8
  - Export creates complete model artifact with Engine-compatible YAML schema
  - **Charter Boundary**: Success shows artifact metadata, NO execution or telemetry generation
  - Error handling with specific guidance for model structure resolution

**Model Template System:**

Pre-built model templates accelerate model creation with domain-specific configurations:

• **Template Categories and Examples:**
  - **Manufacturing**: Production systems with capacity constraints and seasonal demand patterns
  - **Service Systems**: Customer service queues with variable arrival rates and service times
  - **IT Infrastructure**: Server capacity and performance modeling with load balancing
  - **Supply Chain**: Multi-stage logistics networks with inventory and transportation constraints

• **Template Structure:**
  - **Basic Information**: Template name, description, and category classification
  - **Parameter Schema**: Domain-specific parameter definitions (capacity utilization, arrival rates, etc.)
  - **Engine Model**: Pre-configured FlowTime engine model structure with parameterized expressions
  - **Validation Rules**: Template-specific validation logic for parameter bounds and relationships

• **Charter-Compliant Template Usage:**
  - User selects template from gallery showing template cards with descriptions
  - Template creates new model session with parameterized configuration
  - User customizes parameters, schema settings, and metadata
  - Template provides validated starting point reducing configuration errors
  - **Charter Boundary**: Templates define model structure only, no execution or telemetry

### **FR-SIM-M2.6-3: Model Structure Validation & Quality Assurance**
Comprehensive validation system ensuring exported models are structurally sound for Engine.

**Model Validation & Quality Assurance (Charter-Compliant):**

Comprehensive validation ensures exported models are structurally correct for FlowTime Engine:

• **Validation Levels:**
  - **Syntax Validation**: YAML structure, field types, required properties
  - **Semantic Validation**: Node dependencies, expression validity, logical consistency
  - **Engine Compatibility**: SchemaVersion 1 compliance, supported node kinds, expression syntax
  - **Structure Validation**: Model completeness, parameter bounds, output definitions

• **Charter-Compliant Validation Scope:**
  - **Included**: Schema validation, structure checks, compatibility verification
  - **Excluded**: NO model execution, NO telemetry generation, NO performance testing
  - **Boundary**: Validation logic only, Engine handles all execution validation

• **Validation Issues Classification:**
  - **Errors**: Block export (undefined nodes, invalid expressions, schema violations)
  - **Warnings**: Allow export with caveats (unusual parameter ranges, complex expressions)
  - **Info**: Suggestions for improvement (parameter optimization, structure simplification)

• **Quality Assessment Metrics (Structure-Only):**
  - **Completeness Score**: Configuration detail, metadata completeness, documentation quality
  - **Consistency Score**: Parameter relationships, expression logic, node dependencies
  - **Compatibility Score**: Engine schema compliance, supported features, API alignment

• **Charter-Compliant Quality Gate:**
  - Overall structure quality score must exceed 0.8 threshold for Engine export
  - Zero validation errors (warnings acceptable with user confirmation)
  - **Charter Boundary**: Structure validation only, Engine validates execution compatibility
  - Schema compatibility verification via static analysis only

• **Validation Feedback:**
  - Specific error messages with schema references and suggested corrections
  - Quality recommendations for model structure improvement
  - Compatibility estimates based on Engine schema requirements

### **FR-SIM-M2.6-4: Engine Integration Workflow (Charter-Compliant)**
Model artifact preparation and Engine integration without telemetry generation.

**Charter-Compliant Engine Integration:**

Seamless integration between FlowTime-Sim model authoring and Engine execution platform:

• **Model Artifact Preparation:**
  - Package complete model bundles for Engine registry transfer
  - Include model metadata, FlowTime engine specification, and all asset files
  - Generate unique artifact IDs for tracking and reference in Engine workflows
  - **Charter Boundary**: Prepare artifacts only, Engine handles all upload and execution

• **Engine Integration Status:**
  - Track model preparation progress and completion status
  - Display Engine API connection status for integration readiness
  - **Charter Boundary**: Status monitoring only, no direct execution or telemetry access
  - Provide user feedback for integration preparation steps

**Charter Integration UI Panel:**

Visual integration panel showing Engine connection status and model preparation progress:

• **Engine Connection Section:**
  - Display Engine API base URL and connection status indicator
  - Green "Connected" / Red "Disconnected" status chip
  - Connection health monitoring (ping only, no data transfer)

• **Model Preparation Section:**
  - "Prepare Model Artifact" button (enabled only when model validated)
  - Preparation progress indicator during artifact generation
  - Success/failure messages with artifact ID or error details

• **Charter-Compliant Actions:**
  - "Open Engine Registry" link button for navigation to Engine UI
  - Action buttons appropriately disabled based on workflow state
  - **Charter Boundary**: Links and navigation only, no execution control

• **Model Preparation Timeline:**
  - Visual timeline showing model preparation progress
  - Step 1: "Model Created in FlowTime-Sim" (always complete)
  - Step 2: "Model Validated" (complete after validation)
  - Step 3: "Artifact Prepared" (complete after export)
  - Step 4: "Ready for Engine Transfer" (preparation complete)

• **Charter-Compliant State Management:**
  - Panel only shown when model artifact is ready for preparation
  - Info message when validation incomplete: "Complete model validation to prepare Engine artifact"
  - **Charter Boundary**: Preparation status only, Engine handles execution lifecycle

## Integration Points

### **FlowTime Engine M2.6 Artifacts Registry Integration**
- Model artifacts created by Sim are prepared for Engine artifacts registry
- Model metadata includes Sim validation results and quality metrics
- Registry enables model discovery and reuse across Engine workflows

### **SIM-M2.1 PMF Generator Integration**
- Leverage existing PMF generation capabilities for stochastic model inputs
- PMF definitions flow seamlessly into Engine model artifact schemas
- Template system builds on PMF foundations for parameterized model creation

### **Charter Ecosystem Compatibility**
- Models exported from Sim integrate with Engine execution workflows
- Sim validation results provide quality baseline for Engine processing
- Charter boundaries respected: Sim creates, Engine executes

### **Parameterized Template System Integration**
- Build on existing template infrastructure from feature branch
- Extend JSON generation to full model artifact creation
- Maintain backward compatibility with existing template endpoints

## Acceptance Criteria

### **Charter-Compliant Model Creation**
- ✅ Models created in Sim can be exported as Engine-compatible artifacts
- ✅ Exported models include all necessary assets (templates, validation, schema)
- ✅ Model artifacts validate successfully for Engine structure requirements
- ✅ **Charter Compliance**: NO telemetry generation during export process

### **Engine Integration Preparation**
- ✅ Model preparation from Sim creates Engine-ready artifact bundles
- ✅ Prepared models include correct metadata for Engine artifacts registry
- ✅ Model structure validation ensures Engine compatibility
- ✅ **Charter Boundary**: Preparation only, Engine handles execution lifecycle

### **Quality & Validation (Structure-Only)**
- ✅ Sim validation catches common model configuration errors
- ✅ Quality assessment provides meaningful scores for structure completeness
- ✅ **Charter Compliance**: Validation covers structure only, no execution testing
- ✅ Validation reports help users improve model structure quality

### **User Experience**
- ✅ Model authoring workflow is intuitive and charter-aligned
- ✅ Engine integration status is clearly communicated
- ✅ Error messages provide actionable guidance for structure issues
- ✅ Workflow supports iterative model development without execution dependencies

## Implementation Plan

### **Phase 1: Charter-Compliant Model Artifact System (Week 1)**
1. **Model artifact schema** definition compatible with Engine registry
2. **Charter-compliant export service** with structure validation only
3. **Template system enhancement** for Engine model generation
4. **Structure validation framework** with syntax and semantic checks

### **Phase 2: Engine Integration Preparation (Week 2)**
1. **Model preparation service** for Engine artifact creation
2. **Integration status endpoints** in Sim Service
3. **Artifact bundling system** for Engine transfer readiness
4. **Connection monitoring** for Engine API availability

### **Phase 3: Enhanced UI & Validation (Week 3)**
1. **Model authoring dashboard** with template-based creation
2. **Charter-compliant validation** with structure quality assessment
3. **Engine integration panel** showing preparation status
4. **Model preview generation** without execution requirements

### **Phase 4: Workflow Polish & Testing (Week 4)**
1. **End-to-end model preparation** from Sim to Engine readiness
2. **Error handling and recovery** for preparation failures
3. **Performance optimization** for large model artifacts
4. **Charter compliance documentation** and user guides

## Risk Mitigation

### **Charter Boundary Risk**
**Risk:** Temptation to add execution or telemetry features that violate charter boundaries  
**Mitigation:**
- Clear charter compliance checks in all validation and export processes
- Structure-only validation explicitly excludes execution testing
- UI clearly communicates charter boundaries and Engine handoff points

### **Model Quality Risk**
**Risk:** Models created in Sim have structural issues that cause Engine execution problems  
**Mitigation:**
- Comprehensive structure validation framework with Engine schema compatibility
- Quality assessment focuses on completeness and consistency without execution
- Template system provides validated starting points reducing configuration errors

### **Integration Complexity Risk**
**Risk:** Model preparation and Engine integration creates complex failure modes  
**Mitigation:**
- Clear separation between Sim preparation and Engine execution responsibilities
- Staged integration with fallback modes for each preparation step
- Comprehensive error handling with specific guidance for resolution

## Success Metrics

### **Charter Compliance Success**
- **Boundary Respect**: 100% compliance with charter - zero telemetry generation in Sim
- **Model Quality**: 95% of Sim-prepared models validate successfully in Engine
- **User Understanding**: Users clearly understand Sim vs Engine responsibilities

### **Model Authoring Success**  
- **Template Usage**: 80% of models created using Sim template system
- **Preparation Completion**: 90% of validated models successfully prepared for Engine
- **Quality Improvement**: Average model structure quality score improves through iteration

### **Technical Success**
- **Performance**: Model preparation and validation complete in < 30 seconds
- **Scalability**: System handles concurrent model development by multiple users
- **Reliability**: Model preparation maintains 99%+ success rate for valid inputs

---

## Next Steps

1. **SIM-M3**: Enhanced template system with advanced modeling patterns
2. **Engine Integration**: Complete charter loop with Engine execution capabilities
3. **Advanced Validation**: Domain-specific validation rules and quality metrics

This milestone establishes **FlowTime-Sim as the charter-compliant model authoring platform** for the FlowTime ecosystem, creating seamless flow from model creation to execution while respecting architectural boundaries.
