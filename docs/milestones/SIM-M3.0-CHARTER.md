# Milestone SIM-M3.0 — FlowTime-Sim Charter Alignment

**Status:** 📋 Planned (Charter-Aligned)  
**Dependencies:** M2.7 (Artifacts Registry), M2.9 (Compare Workflow)  
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

**Model Export Service Requirements:**

The model export service converts FlowTime-Sim configurations into charter-compatible model artifacts:

• **Core Export Functions:**
  - Export current Sim configuration as complete model artifact
  - Convert Sim catalog/scenario into FlowTime engine model (nodes and edges)
  - Validate exported model for Engine schemaVersion 1 compatibility
  - Test engine model execution before export to ensure quality
  - Bundle all assets (catalogs, scenarios, engine YAML) for transport
  - Upload model artifacts directly to Engine registry via API

• **Model Artifact Structure:**
  - **Metadata**: Title, description, author, version, tags, creation date
  - **FlowTime Engine Model**: Complete nodes/edges definition with grid, expressions, outputs
  - **Sim Configuration**: Original catalog, scenario, and parameters for reference
  - **Validation Results**: Quality scores, compatibility checks, test execution results
  - **Asset Bundle**: All source files (catalogs, scenarios, generated engine YAML)

• **Export Options:**
  - Configurable title, description, and tags for model identification
  - Adjustable time horizon and time step for engine execution
  - Selectable output series for specific analysis needs
  - Optional validation inclusion for quality assurance
  - Asset bundling control for complete or minimal exports

**FlowTime-Sim API Endpoints:**

New REST API endpoints enable model artifact creation and Engine integration:

• **POST /v1/models/export** - Export Model Artifact
  - Input: Sim configuration ID and export options
  - Process: Convert Sim config to engine model, validate compatibility, test execution
  - Quality Gate: Reject models with quality score below 0.8 threshold
  - Output: Complete model artifact bundle with engine YAML
  - Optional: Direct upload to Engine registry if Engine API URL provided

• **POST /v1/models/validate** - Validate Engine Compatibility
  - Input: Model artifact with FlowTime engine model definition
  - Process: Validate nodes/edges schema, expression syntax, grid configuration
  - Output: Validation results with specific error messages and suggestions

• **POST /v1/models/test-engine** - Test Engine Execution
  - Input: FlowTime engine model (nodes, grid, outputs)
  - Process: Convert to YAML, submit to Engine API, analyze results
  - Output: Test results including success status, quality metrics, series data

### **FR-SIM-M3.0-2: Charter-Aware Model Creation UI**
Enhanced FlowTime-Sim UI for charter ecosystem model authoring workflow.

**Model Authoring UI Requirements:**

Charter-aware model creation interface with guided workflow for Engine compatibility:

• **Model Authoring Dashboard** - `/models/create`
  - Header with "Model Authoring" title and "Create and validate models for FlowTime Engine" subtitle
  - "New Model" button to start fresh model creation
  - Template gallery showing pre-built model templates (Manufacturing, Supply Chain, Service Queue, IT Infrastructure)

• **Model Configuration Tabs:**
  - **Basic Info Tab**: Model title, description, author, tags, version information
  - **System Tab**: Catalog selection and component definition for Sim model structure
  - **Scenario Tab**: Parameter configuration for specific model scenarios and use cases  
  - **Execution Tab**: Time horizon, time step, output series selection for Engine execution

• **Actions Panel** (Right Side):
  - **Validation Section**: "Validate Model" button with results display showing compatibility status
  - **Test Run Section**: "Test in Sim" button (enabled after validation) with quality score results
  - **Export Section**: "Create Model Artifact" button (enabled after quality gate) with Engine upload

• **Export Workflow Requirements:**
  - Export only available after successful validation AND quality score > 0.8
  - Export creates complete model artifact with Engine-compatible YAML
  - Success shows artifact ID and optional navigation to Engine UI
  - Error handling with specific guidance for resolution

**Model Template System:**

Pre-built model templates accelerate model creation with domain-specific configurations:

• **Template Categories and Examples:**
  - **Manufacturing**: Production systems with capacity constraints and seasonal demand patterns
  - **Service Systems**: Customer service queues with variable arrival rates and service times
  - **Supply Chain**: Multi-stage logistics networks with inventory and transportation constraints
  - **IT Infrastructure**: Server capacity and performance modeling with load balancing

• **Template Structure:**
  - **Basic Information**: Template name, description, and category classification
  - **Catalog Reference**: Pre-configured catalog file with appropriate system components
  - **Default Parameters**: Domain-specific parameter values (capacity utilization, arrival rates, etc.)
  - **Execution Configuration**: Appropriate time horizons, time steps, and output series for domain

• **Template Usage Workflow:**
  - User selects template from gallery showing template cards with descriptions
  - Template creates new model session with pre-populated configuration
  - User can customize parameters, execution settings, and metadata
  - Template provides validated starting point reducing configuration errors

### **FR-SIM-M3.0-3: Model Validation & Quality Assurance**
Comprehensive validation system ensuring exported models work correctly in Engine.

**Model Validation & Quality Assurance:**

Comprehensive validation ensures exported models work correctly in FlowTime Engine:

• **Validation Levels:**
  - **Syntax Validation**: YAML structure, field types, required properties
  - **Semantic Validation**: Node dependencies, expression validity, logical consistency
  - **Engine Compatibility**: SchemaVersion 1 compliance, supported node kinds, expression syntax
  - **Integration Validation**: End-to-end test execution via Engine API

• **Validation Issues Classification:**
  - **Errors**: Block export (undefined nodes, invalid expressions, schema violations)
  - **Warnings**: Allow export with caveats (long time horizons, performance concerns)
  - **Info**: Suggestions for improvement (parameter ranges, optimization opportunities)

• **Quality Assessment Metrics:**
  - **Completeness Score**: Configuration detail, metadata completeness, documentation quality
  - **Realism Score**: Parameter values appropriate for domain, realistic constraints and ranges
  - **Performance Score**: Expected Engine execution efficiency, memory usage, computation time

• **Quality Gate Requirements:**
  - Overall quality score must exceed 0.8 threshold for Engine export
  - Zero validation errors (warnings acceptable with user confirmation)
  - Successful test execution in Sim environment before export
  - Engine compatibility verification via API test call

• **Validation Feedback:**
  - Specific error messages with code references and suggested corrections
  - Quality recommendations for model improvement
  - Performance estimates and optimization suggestions

### **FR-SIM-M3.0-4: Charter Integration Workflow**
Complete workflow from Sim model creation to Engine execution with artifacts registry integration.

**Charter Integration Service:**

Seamless integration between FlowTime-Sim model authoring and Engine execution platform:

• **Model Upload Integration:**
  - Upload complete model bundles to Engine artifacts registry via multipart form data
  - Include model metadata, FlowTime engine specification, and all asset files
  - Receive artifact ID for tracking and future reference in Engine workflows

• **Engine Execution Integration:**
  - Submit model artifacts for execution in Engine with configurable options
  - Monitor execution status and receive completion notifications
  - Retrieve run results and artifacts for analysis in charter workflow

• **Bidirectional Data Flow:**
  - Export Sim-authored models to Engine for production execution
  - Import Engine run results back to Sim for detailed analysis and model refinement
  - Maintain connection between Sim model versions and Engine execution history

• **Integration Workflow Status:**
  - Track model upload progress and completion status
  - Monitor Engine execution stages (queued, running, completed, failed)
  - Provide user feedback and error handling for integration failures

**Charter Integration UI Panel:**

Visual integration panel showing Engine connection status and charter workflow progress:

• **Engine Connection Section:**
  - Display Engine API base URL and connection status indicator
  - Green "Connected" / Red "Disconnected" status chip
  - Real-time connection health monitoring

• **Model Upload Section:**
  - "Upload Model" button (enabled only when Engine connected and model validated)
  - Upload progress indicator with spinning loader during transfer
  - Success/failure alert messages with artifact ID or error details

• **Charter Actions:**
  - "Execute in Engine" button (enabled after successful upload)
  - "Open in Engine" link button for direct navigation to Engine UI
  - Action buttons disabled appropriately based on workflow state

• **Charter Workflow Timeline:**
  - Visual timeline showing charter workflow progress
  - Step 1: "Model Created in FlowTime-Sim" (always complete)
  - Step 2: "Uploaded to Engine Registry" (complete after upload)
  - Step 3: "Executed in Engine" (complete after execution)
  - Step 4: "Results available for analysis" (complete when artifacts ready)

• **Workflow State Management:**
  - Panel only shown when model artifact is ready for export
  - Info message when validation/testing incomplete: "Complete model validation and testing to enable Engine integration"
  - Real-time status updates during upload and execution processes

## Integration Points

### **M2.7 Artifacts Registry Integration**
- Model artifacts created by Sim are automatically registered in Engine artifacts registry
- Model metadata includes Sim validation results and quality metrics
- Registry enables model discovery and reuse across Engine workflows

### **M2.9 Compare Workflow Integration**
- Models exported from Sim can be used as comparison inputs in Engine Compare workflow
- Sim validation results provide baseline for model comparison quality
- Charter Compare workflow supports fresh model execution from Sim artifacts

### **Engine API Extension**
- New `/v1/artifacts/models` endpoint for receiving Sim model artifacts
- Enhanced model execution API to support Sim-generated model specifications
- Model artifact validation in Engine using Sim validation schemas

### **Cross-Platform Workflow**
- Seamless handoff from Sim model authoring to Engine execution
- Bidirectional integration - Engine run results can be imported back to Sim for analysis
- Unified artifact registry spans both Sim and Engine platforms

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

### **User Experience**
- ✅ Model authoring workflow is intuitive and guided
- ✅ Charter integration status is clearly communicated
- ✅ Error messages provide actionable guidance
- ✅ Workflow supports iterative model development and testing

## Implementation Plan

### **Phase 1: Model Artifact System (Week 1)**
1. **Model artifact schema** definition and serialization
2. **Model export service** with validation and bundling
3. **Model templates** system for common use cases
4. **Basic validation framework** with syntax and semantic checks

### **Phase 2: Charter Integration API (Week 2)**
1. **Charter integration service** with Engine API client
2. **Model upload endpoints** in Sim Service
3. **Engine integration** for model artifact reception
4. **Artifact registry integration** for model storage and discovery

### **Phase 3: Enhanced UI & Validation (Week 3)**
1. **Model authoring dashboard** with template-based creation
2. **Comprehensive validation** with quality assessment
3. **Charter workflow panel** showing integration status
4. **Test execution** and results visualization in Sim

### **Phase 4: Workflow Polish & Testing (Week 4)**
1. **End-to-end workflow testing** from Sim to Engine
2. **Error handling and recovery** for integration failures
3. **Performance optimization** for large model artifacts
4. **Documentation** and user guides for charter workflow

## Risk Mitigation

### **Integration Complexity Risk**
**Risk:** Sim-Engine integration creates complex failure modes and debugging challenges  
**Mitigation:**
- Comprehensive validation at Sim export time catches most issues early
- Clear error messages with specific guidance for resolution
- Staged integration with fallback modes for each integration point

### **Model Quality Risk**
**Risk:** Models created in Sim don't perform well in Engine execution environment  
**Mitigation:**
- Robust validation framework with domain-specific quality checks
- Test execution in Sim environment before export to Engine
- Quality assessment with specific recommendations for improvement

### **User Experience Risk**
**Risk:** Charter workflow is too complex and users revert to manual processes  
**Mitigation:**
- Template-based model creation reduces configuration complexity
- Clear visual workflow status and progress indication
- Graceful degradation when integration services are unavailable

## Success Metrics

### **Charter Ecosystem Success**
- **Model Usage**: 80%+ of Engine runs use models created in Sim
- **Workflow Completion**: 90%+ of models created in Sim are successfully exported to Engine
- **Integration Reliability**: < 5% failure rate for Sim-to-Engine workflow

### **Model Quality Success**  
- **Validation Effectiveness**: 95%+ of models passing Sim validation execute successfully in Engine
- **Quality Improvement**: Average model quality score improves through iterative development
- **User Satisfaction**: Users prefer Sim-based model creation over manual configuration

### **Technical Success**
- **Performance**: Model upload and validation complete in < 30 seconds
- **Scalability**: System handles concurrent model development by multiple users
- **Reliability**: Charter integration maintains 99%+ uptime

---

## Next Steps

1. **M3.1-TELEMETRY-IMPORT**: Complete charter loop with telemetry import capabilities
2. **M3.2-COMPARISON-ANALYTICS**: Enhanced comparison analytics using Sim model baselines
3. **SIM-M3.1-CATALOG-MANAGEMENT**: Advanced catalog authoring and sharing capabilities

This milestone establishes **FlowTime-Sim as the model authoring platform** for the charter ecosystem, creating seamless flow from model creation to execution to analysis within the FlowTime platform family.
