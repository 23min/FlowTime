# SIM-M2.9 — Compare Integration Support

**Status:** 📋 Planned (Charter-Aligned)  
**Dependencies:** SIM-M2.8 (Model Authoring Service & API), Engine M2.9  
**Target:** FlowTime-Sim support for Engine compare workflows and integration testing  
**Date:** 2025-09-30

---

## Goal

Prepare **FlowTime-Sim** to support Engine M2.9 Compare Workflow by implementing model validation, quality assurance, and Engine integration testing capabilities. This milestone ensures Sim-created models integrate seamlessly with Engine comparison and analysis workflows.

## Context & Charter Alignment

**Parallel Development**: This milestone runs parallel to Engine M2.9 Compare Workflow, ensuring Sim models are compatible with Engine comparison capabilities.

**Compare Preparation**: Establishes validation and testing infrastructure that enables:
- Model quality assessment for reliable comparisons
- Engine integration testing to ensure model compatibility
- Baseline model creation for comparison scenarios
- Quality metadata that supports comparison analysis

## Functional Requirements

### **FR-SIM-M2.9-1: API Consolidation & Legacy Cleanup**
Clean up deprecated scenarios endpoints following successful UI-M2.8 template API integration.

**Background**: UI-M2.8 successfully migrated FlowTime UI from hardcoded template generation to FlowTime-Sim `/v1/sim/templates` API integration. The legacy `/v1/sim/scenarios` endpoints are now obsolete.

**API Cleanup:**
- **Remove deprecated endpoints**: `/v1/sim/scenarios`, `/v1/sim/scenarios/categories`
- **Update test scripts**: Migrate validation scripts from scenarios to templates endpoints  
- **Documentation cleanup**: Remove scenarios references, update integration guides
- **Backward compatibility plan**: Define deprecation timeline and migration guidance

**Integration Validation:**
- **UI compatibility**: Ensure UI-M2.8 template integration remains unaffected
- **External dependencies**: Identify and migrate any remaining scenarios endpoint usage
- **API versioning**: Maintain clean v1 API surface focused on templates architecture

### **FR-SIM-M2.9-2: Model Validation & Quality Assurance**
Comprehensive validation system ensuring Sim models work reliably in Engine compare workflows.

**Validation Framework:**
- **Syntax Validation**: YAML structure, field types, required properties
- **Semantic Validation**: Node dependencies, expression validity, logical consistency  
- **Engine Compatibility**: SchemaVersion 1 compliance, supported node kinds, expression syntax
- **Integration Testing**: End-to-end test execution via Engine API calls

**Quality Assessment:**
- **Completeness Score**: Configuration detail, metadata completeness, documentation quality
- **Realism Score**: Parameter values appropriate for domain, realistic constraints and ranges
- **Performance Score**: Expected Engine execution efficiency, memory usage, computation time
- **Comparison Readiness**: Model suitability for comparison scenarios and baseline usage

### **FR-SIM-M2.9-3: Engine Integration Testing**
Testing framework to validate model compatibility with Engine execution and comparison workflows.

**Integration Test Framework:**
- **Engine API Testing**: Validate model execution via Engine API endpoints
- **Comparison Compatibility**: Test models work correctly in Engine compare scenarios
- **Performance Validation**: Ensure models execute within acceptable time and resource limits
- **Error Handling**: Test graceful handling of model execution failures and edge cases

**Test Scenarios:**
- **Baseline Execution**: Verify model executes successfully and produces expected outputs
- **Comparison Baseline**: Validate model can serve as comparison baseline for other models/runs
- **Parameter Sensitivity**: Test model behavior with varied parameter values
- **Edge Case Handling**: Validate model behavior at parameter boundaries and unusual inputs

### **FR-SIM-M2.9-4: Compare Workflow Readiness**
Features that enable Sim models to participate effectively in Engine compare workflows.

**Comparison Metadata:**
- **Baseline Indicators**: Mark models as suitable for comparison baselines
- **Comparison Tags**: Categorize models for comparison grouping and filtering
- **Quality Metrics**: Provide quality scores that inform comparison reliability
- **Version Tracking**: Support model versioning for comparison evolution tracking

**Compare Integration:**
- **Model Export Preparation**: Prepare models for Engine registry integration (M3.0 preparation)
- **Comparison Documentation**: Generate model documentation supporting comparison analysis
- **Quality Reports**: Provide quality assessment reports for comparison context
- **Integration Status**: Track model readiness for Engine integration and comparison use

## Integration Points

### **SIM-M2.8 Model Authoring Enhancement**
- Quality validation leverages enhanced model authoring UI from SIM-M2.8
- Template system provides quality baselines and best practices for validation
- Model organization supports quality tracking and comparison preparation

### **Engine M2.9 Compare Workflow Integration** 
- Model validation ensures compatibility with Engine compare capabilities
- Quality assessment provides metadata supporting Engine comparison analysis
- Integration testing validates end-to-end workflow from Sim to Engine comparison

### **SIM-M3.0 Charter Preparation**
- Validation framework provides foundation for M3.0 model export quality gates
- Integration testing establishes patterns for M3.0 charter workflow integration
- Quality assurance ensures reliable model artifacts for charter ecosystem

## Acceptance Criteria

### **API Consolidation**
- ✅ Deprecated `/v1/sim/scenarios` and `/v1/sim/scenarios/categories` endpoints removed
- ✅ All test scripts migrated to use `/v1/sim/templates` endpoints exclusively
- ✅ Documentation updated to reflect templates-only API surface
- ✅ UI-M2.8 template integration remains fully functional after cleanup
- ✅ No breaking changes for active FlowTime UI integration

### **Model Validation**
- ✅ Validation framework catches 95%+ of model configuration errors before Engine integration
- ✅ Quality assessment provides meaningful scores and actionable improvement recommendations
- ✅ Engine compatibility testing ensures Sim models execute successfully in Engine
- ✅ Validation feedback helps users improve model quality iteratively

### **Engine Integration**
- ✅ Integration testing validates model compatibility with Engine execution
- ✅ Models passing Sim validation execute successfully in Engine compare workflows
- ✅ Performance testing ensures models meet Engine execution requirements
- ✅ Error handling provides clear guidance for integration issues

### **Compare Workflow Support**
- ✅ Models can serve as reliable baselines for Engine comparison scenarios
- ✅ Quality metadata supports informed comparison analysis and interpretation
- ✅ Model versioning enables comparison evolution tracking
- ✅ Integration status provides clear feedback on compare workflow readiness

## Implementation Plan

### **Phase 1: API Consolidation & Validation Framework**
1. **API cleanup**: Remove deprecated `/v1/sim/scenarios` endpoints and update dependent scripts
2. **Model validation service** with syntax, semantic, and compatibility checking
2. **Quality assessment metrics** with scoring and improvement recommendations
3. **Validation feedback UI** with specific error messages and guidance
4. **Quality dashboard** showing model assessment results and trends

### **Phase 2: Engine Integration Testing**
1. **Engine API client** for integration testing and validation
2. **Test execution framework** with baseline, comparison, and edge case scenarios
3. **Performance testing** with resource usage and execution time validation
4. **Integration status tracking** showing Engine compatibility and readiness

### **Phase 3: Compare Workflow Support**
1. **Comparison metadata system** with baseline indicators and quality metrics
2. **Model versioning** and comparison evolution tracking
3. **Quality reporting** with comparison context and reliability assessment
4. **Compare workflow preparation** for SIM-M3.0 charter integration

### **Phase 4: Testing and Optimization**
1. **End-to-end testing** with Engine M2.9 compare workflows
2. **Performance optimization** for validation and integration testing
3. **User experience refinement** based on validation and testing feedback
4. **Documentation** for model quality and Engine integration best practices

---

## Next Steps

1. **SIM-M3.0**: Full charter integration with validated model export and Engine registry integration
2. **Cross-platform testing**: Deep integration validation with Engine compare workflows

This milestone establishes **validation and integration capabilities** that ensure FlowTime-Sim models participate reliably in Engine compare workflows and charter ecosystem analysis.
