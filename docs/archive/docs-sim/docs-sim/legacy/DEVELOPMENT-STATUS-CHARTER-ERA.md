# FlowTime-Sim Development Status & Strategic Plan

**Last Updated:** September 21, 2025  
**Status:** 🎯 **Charter Transition Phase** - Engine Foundation First  
**Next Review:** After Engine M2.7 completion

---

## Executive Summary

FlowTime-Sim development follows a **strategic two-phase approach** prioritizing Engine charter foundation before completing Sim charter alignment. This ensures proper infrastructure dependencies and minimizes UI disruption.

## Current Development Status

### ✅ **Phase 0: Pre-Charter Foundation (Completed)**

| Milestone | Status | Release | Notes |
|-----------|--------|---------|--------|
| **SIM-M0** | ✅ Complete | Pre-Charter | Core foundations, deterministic evaluation |
| **SIM-M1** | ✅ Complete | Pre-Charter | Contracts parity, artifact standardization |
| **SIM-M2** | ✅ Complete | 2025-09-02 | `run.json`, `manifest.json`, series structure |
| **SIM-CAT-M2** | ✅ Complete | 2025-09-03 | Catalog.v1 required, stable component IDs |
| **SIM-SVC-M2** | ✅ Complete | 2025-09-03 | HTTP service, artifact endpoints |
| **SIM-M2.1** | ✅ Complete | 2025-09-11 | PMF generator support, 88 passing tests |

**Foundation Status**: ✅ **Solid & Stable** - All pre-charter milestones complete and functional.

### ⚠️ **Current Transition Challenge: SIM-M2.6 Charter Violation**

| Issue | Current State | Charter Requirement | Impact |
|-------|---------------|---------------------|---------|
| **Telemetry Generation** | ❌ Service generates data via `/api/v1/run` | ✅ NO telemetry generation | Charter violation |
| **Template Behavior** | ❌ Creates simulation data | ✅ Export model artifacts | Wrong output type |
| **API Endpoints** | ❌ Execution-focused | ✅ Model authoring focused | API restructure needed |
| **UI Integration** | ❌ Expects telemetry | ✅ Expects model artifacts | UI contract mismatch |

**Charter Compliance**: ❌ **Non-Compliant** - Requires significant refactor for charter alignment.

### 📋 **Strategic Decision: Engine Foundation First**

**Rationale**:
1. **Infrastructure Dependency**: SIM-M2.6 **requires** Engine M2.7 Registry for meaningful completion
2. **UI Breaking Changes**: Charter UI restructures everything - better to break once than twice  
3. **Ecosystem Dependencies**: Model authoring without registry integration is incomplete

---

## Development Phases

### **🔄 Phase 1: Engine Charter Foundation (Current Priority)**
**Timeline:** Next 4-6 weeks  
**Repository:** `/workspaces/flowtime-vnext`  
**Status:** **IN PROGRESS**

#### **Engine Milestones:**
- [ ] **Engine M2.7 - Artifacts Registry** 
  - File-based registry with JSON metadata
  - Registry discovery and health monitoring
  - KISS implementation with extension points
  
- [ ] **Engine M2.8 - Registry Integration**
  - Enhanced registry management capabilities
  - Registry synchronization and conflict resolution
  - API improvements for registry operations

#### **UI Milestones:**
- [ ] **UI-M2.7 - Artifacts Registry UI**
  - Registry browser and management interface
  - Artifact discovery and selection workflows
  - Registry health monitoring dashboard

- [ ] **UI-M2.8 - Charter Navigation**
  - Charter-compliant navigation structure
  - Tab system: Models → Runs → Artifacts → Learn
  - Unified registry integration

#### **Phase 1 Success Criteria:**
- ✅ Engine has stable file-based registry system
- ✅ UI can browse and manage registry artifacts
- ✅ Charter navigation structure fully implemented  
- ✅ Existing Engine workflows preserved and enhanced
- ✅ Zero dependency on Sim charter violations

### **🚀 Phase 2: Charter-Compliant SIM Integration (Future)**
**Timeline:** After Phase 1 completion  
**Repository:** `/workspaces/flowtime-sim-vnext`  
**Status:** **PLANNED**

#### **SIM Milestones:**
- [ ] **SIM-M2.6 Charter Completion**
  - Remove telemetry generation from service
  - Implement model export (no execution)
  - Add structure validation (no computation)
  - Update CLI for model authoring workflow

- [ ] **SIM-M2.7 - Registry Integration**
  - Integrate with established Engine Registry
  - Auto-register generated model artifacts
  - Registry sync and health monitoring
  - Cross-platform model discovery

#### **UI Integration:**
- [ ] **UI-M2.9 - Compare Workflow**
  - Enhanced compare interface with charter backend
  - Scenario comparison via Engine execution
  - Charter-compliant workflow integration

- [ ] **SIM-M3.0 - Complete Charter Alignment**
  - Full charter compliance verification
  - Cross-platform workflows operational
  - Template library and wizard integration

#### **Phase 2 Success Criteria:**
- ✅ Sim creates model artifacts (NO telemetry)
- ✅ Models discoverable in Engine registry
- ✅ UI provides model authoring in charter navigation
- ✅ Cross-platform workflows fully operational
- ✅ Charter boundaries strictly maintained

---

## Current Work Status

### **Immediate Actions (This Week)**
- [x] Document strategic plan and charter transition approach
- [ ] Focus development effort on `/workspaces/flowtime-vnext`
- [ ] Begin Engine M2.7 Artifacts Registry implementation
- [ ] Define registry file format and discovery patterns

### **Short-Term Goals (2-3 weeks)**
- [ ] Complete Engine M2.7 Registry foundation
- [ ] Implement UI-M2.7 Registry browser interface
- [ ] Validate registry health monitoring
- [ ] Test registry artifact discovery workflows

### **Medium-Term Goals (4-6 weeks)**
- [ ] Complete UI-M2.8 Charter navigation implementation
- [ ] Finalize Engine M2.8 registry enhancements
- [ ] Validate charter navigation workflows
- [ ] Prepare SIM-M2.6 charter refactor planning

### **Current State Management**
During Phase 1, FlowTime-Sim maintains:
- ✅ **Working SIM-M2.1 functionality** for current PMF generation needs
- ⚠️ **SIM-M2.6 marked as incomplete** with clear charter violation documentation
- 📋 **UI guidance**: "Use Engine features for current workflows, Sim features upgrading for charter compliance"

---

## Risk Management

### **Risk Assessment**

| Risk Level | Risk | Mitigation | Status |
|------------|------|------------|--------|
| **HIGH** | Charter violations block Engine development | Phase 1 establishes Engine independence | ✅ Managed |
| **MEDIUM** | Multiple UI breaking changes | Single transition to charter navigation | ✅ Planned |
| **LOW** | Registry integration complexity | KISS file-based with proven patterns | ✅ Mitigated |

### **Dependency Management**
- **Engine Independence**: Phase 1 ensures Engine works without Sim charter violations
- **UI Continuity**: Mode toggle preserves Engine functionality during Sim refactor
- **Infrastructure First**: Registry foundation before integration prevents rework

---

## Success Metrics

### **Overall Project Success:**
- [ ] **Single Major UI Transition** instead of multiple breaking changes
- [ ] **Stable Charter Ecosystem** with Engine+Sim integration
- [ ] **Infrastructure-First Validation** of dependency management approach
- [ ] **Charter Compliance** maintained across all components

### **Phase 1 Validation:**
- [ ] Engine M2.7-M2.8 operational without Sim dependencies
- [ ] UI charter navigation functional and intuitive  
- [ ] Registry system robust and extensible
- [ ] Zero charter violations in Engine ecosystem

### **Phase 2 Validation:**
- [ ] SIM charter compliance (NO telemetry generation)
- [ ] Cross-platform model discovery operational
- [ ] Charter workflow complete: Models → Runs → Artifacts → Learn
- [ ] Template authoring integrated with registry system

---

## References & Documentation

### **Strategic Planning**
- [Charter Transition Strategic Plan](CHARTER-TRANSITION-PLAN.md) ← **This Document**
- [FlowTime-Sim Charter v1.0](flowtime-sim-charter.md)
- [FlowTime-Engine Charter](../../flowtime-vnext/docs/flowtime-engine-charter.md)

### **Technical Documentation**  
- [SIM-M2.6 Milestone](milestones/SIM-M2.6.md)
- [FlowTime-Sim Roadmap](ROADMAP.md)
- [Engine Charter Roadmap](../../flowtime-vnext/docs/milestones/CHARTER-ROADMAP.md)

### **Development Resources**
- [Development Setup](development-setup.md)
- [Testing Guidelines](development/testing.md)
- [Architecture Documentation](architecture/)

---

**Next Update**: After Engine M2.7 Registry completion  
**Owner**: FlowTime Development Team  
**Stakeholders**: Engine, Sim, and UI development teams
