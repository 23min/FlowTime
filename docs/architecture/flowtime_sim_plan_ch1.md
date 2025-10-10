## 1.7 Implementation Milestones

**Note:** M0-M2 already completed (simulation-only functionality). All time-travel work is M3.x.

### M3.0: Schema Foundation
**Objective:** Implement Window and Topology schema classes

**Deliverables:**
- ✅ TemplateWindow class with Start, Timezone properties
- ✅ TemplateTopology class with Nodes, Edges
- ✅ TopologyNode, NodeSemantics, TopologyEdge classes
- ✅ UIHint class for layout hints
- ✅ Updated Template class with Window, Topology properties
- ✅ Comprehensive unit tests for serialization
- ✅ Schema version 1.1 specification

**Success Criteria:**
- All schema classes compile and serialize correctly
- Round-trip YAML serialization works
- Nullable types handled properly
- Tests achieve high coverage

**Dependencies:** None (can start immediately)

---

### M3.1: Generator Updates
**Objective:** Update template service to preserve window/topology in generated models

**Deliverables:**
- ✅ Updated ConvertToEngineSchema() method
- ✅ Parameter substitution in nested objects verified
- ✅ Updated TemplateNode with Initial, Source properties
- ✅ End-to-end generation tests
- ✅ Performance benchmarks

**Success Criteria:**
- Generated models include window and topology sections
- Parameters work in all YAML sections (including nested)
- Old schema 1.0 templates still function
- Performance regression within acceptable limits

**Dependencies:** M3.0 complete

---

### M3.2: Template Updates
**Objective:** Update all 5 built-in templates to schema 1.1

**Deliverables:**
- ✅ transportation-basic updated (1 service node)
- ✅ manufacturing-line updated (pipeline topology)
- ✅ it-system-microservices updated (services + queue)
- ✅ supply-chain-multi-tier updated (complex routing)
- ✅ network-reliability updated (graph topology)
- ✅ Template test suite
- ✅ Template documentation updates

**Success Criteria:**
- All templates generate valid schema 1.1 models
- Each template demonstrates unique topology pattern
- Templates include proper semantic mappings
- All template tests pass

**Dependencies:** M3.1 complete

---

### M3.3: Validation Framework
**Objective:** Implement comprehensive topology validation

**Deliverables:**
- ✅ IValidator interface and ValidationResult classes
- ✅ WindowValidator (5 error codes)
- ✅ TopologyValidator (13 error codes)
- ✅ SemanticValidator (3 error codes)
- ✅ EdgeValidator (5 error codes)
- ✅ ParameterValidator (3 error codes)
- ✅ ValidationOrchestrator
- ✅ Validation error catalog documentation

**Success Criteria:**
- All error categories implemented
- Error messages include context and suggestions
- Validation performance acceptable
- Integration with template service complete

**Dependencies:** M3.2 complete

---

### M3.4: Integration & Testing
**Objective:** Verify generated models work with Engine M3.x

**Deliverables:**
- ✅ Integration tests with Engine M3.x
- ✅ /state API verification tests
- ✅ Performance benchmarks
- ✅ Test documentation

**Success Criteria:**
- Generated models load in Engine without errors
- /state API returns correct topology data
- Performance targets met
- All integration tests pass

**Dependencies:** M3.3 complete, Engine M3.x available

---

### M3.5: Documentation & Release
**Objective:** Complete documentation and prepare for release

**Deliverables:**
- ✅ Updated template authoring guide
- ✅ API documentation updates
- ✅ Validation error reference
- ✅ Schema 1.1 specification document
- ✅ Release notes

**Success Criteria:**
- All API changes documented
- Template examples demonstrate time-travel features
- Validation errors cataloged with fixes
- Documentation reviewed and approved

**Dependencies:** M3.4 complete

---

## 1.8 Success Criteria Summary# FlowTime-Sim Time-Travel Implementation Plan
## Chapter 1: Executive Summary & Objectives

**Document Version:** 1.1  
**Repository:** flowtime-sim-vnext  
**Dependencies:** FlowTime Engine M3.x

---

## 1.1 Purpose of This Document

This planning document provides comprehensive guidance for extending FlowTime-Sim to support time-travel functionality as defined in the FlowTime KISS Architecture (Chapters 1-6). It addresses:

- **What:** Complete specification of changes needed in FlowTime-Sim
- **Why:** Technical justification for each change
- **How:** Detailed implementation guidance without source code
- **Risk:** Risk assessment and mitigation strategies

---

## 1.2 Document Structure

This plan is organized into chapters:

| Chapter | Focus |
|---------|-------|
| **1. Executive Summary** | Goals, scope, success criteria |
| **2. Architecture & Design** | System design, patterns, principles |
| **3. Implementation Phases** | Milestones and dependencies |
| **4. Schema Extensions** | Data models, validation rules |
| **5. Validation Framework** | Validation logic, error handling |
| **6. Testing Strategy** | Test plans, coverage requirements |

---

## 1.3 Current State

### FlowTime-Sim Status
FlowTime-Sim generates simulation models for FlowTime Engine (M0-M2 completed):
- ✅ Pure simulation scenarios (synthetic data)
- ✅ Expression-based computation graphs
- ✅ Stochastic modeling via PMF distributions
- ❌ No absolute time anchoring
- ❌ No telemetry replay capabilities
- ❌ No time-travel UI support

### Target State (M3.x)
FlowTime-Sim will generate time-travel-ready models supporting:
- ✅ Absolute timestamp computation via window section
- ✅ Topological graph structure with typed nodes
- ✅ Semantic mapping for derived metrics (utilization, latency)
- ✅ Telemetry data integration via file sources
- ✅ Time-travel scrubber UI with real timestamps
- ✅ State snapshot APIs at any bin

---

## 1.4 Goals & Success Criteria

### Primary Goals

#### G1: Schema Compatibility
**Goal:** FlowTime-Sim generates models fully compatible with FlowTime KISS Architecture Ch2 (Data Contracts).

**Success Criteria:**
- ✅ All generated models include required `window` section
- ✅ All generated models include required `topology` section (schema 1.1)
- ✅ Engine M3.x can load and execute generated models without errors
- ✅ `/v1/runs/{id}/state` API returns correct topology data
- ✅ All KISS validation rules are satisfied

---

#### G2: Template Quality
**Goal:** All 5 built-in templates are updated to support time-travel with high-quality examples.

**Success Criteria:**
- ✅ `transportation-basic`: Single service with capacity constraints
- ✅ `it-system-microservices`: Multi-tier system with queues and services
- ✅ `manufacturing-line`: Pipeline with sequential stages
- ✅ `supply-chain-multi-tier`: Complex flow with weighted edges
- ✅ `network-reliability`: Graph topology with failure modes

---

#### G3: Schema Evolution
**Goal:** Clean schema version progression from 1.0 (simulation) to 1.1 (time-travel).

**Success Criteria:**
- ✅ Schema 1.0 templates remain valid for simulation-only use cases
- ✅ Schema 1.1 requires window and topology sections
- ✅ Clear distinction between simulation and time-travel modes
- ✅ Breaking changes are acceptable and documented

---

#### G4: Validation Quality
**Goal:** Comprehensive validation catches errors early with actionable error messages.

**Success Criteria:**
- ✅ Topology validation detects all error categories (defined in Ch5)
- ✅ Error messages include context, suggestions, and fixes
- ✅ Validation is performant for typical topologies
- ✅ Validation supports warning mode for non-critical issues

---

#### G5: Performance
**Goal:** Schema extensions maintain acceptable performance.

**Success Criteria:**
- ✅ Template generation latency remains reasonable
- ✅ Validation overhead is minimal
- ✅ Memory usage stays within acceptable limits

---

## 1.5 Scope

### In Scope ✅

**Schema Extensions:**
- Window section (start, timezone)
- Topology section (nodes, edges) - REQUIRED for schema 1.1
- Node semantics (arrivals, served, capacity, etc.)
- Node kinds (service, queue, router, external)
- Initial conditions for stateful nodes
- File source URIs for telemetry replay

**Template Updates:**
- All 5 built-in templates updated to schema 1.1 (time-travel)
- New example templates for telemetry replay scenarios
- Updated template documentation

**Validation:**
- Topology validation rules (all error categories)
- Semantic reference checking
- Kind-specific validation
- Cycle detection with delay analysis
- Parameter type validation

**Testing:**
- Unit tests for all new schema classes
- Integration tests with Engine M3.x
- Performance benchmarks

**Documentation:**
- Template authoring guide updates
- API documentation updates
- Validation error reference

---

### Out of Scope ❌

**UI Implementation:**
- Time-travel scrubber (handled by FlowTime UI project)
- Topology visualization (handled by FlowTime UI project)
- Interactive template editor (future work)

**Engine Changes:**
- /state API implementation (handled by FlowTime Engine)
- Telemetry loader (handled by FlowTime Engine)
- Snapshot storage (handled by FlowTime Engine)

**Advanced Features:**
- Auto-layout algorithm for UI hints (deferred to Engine)
- Topology inference from expressions (future work)
- Template marketplace (future work)
- Visual template designer (future work)

**Migration:**
- Migration guides (no current users)
- Backward compatibility tooling (breaking changes accepted)
- Template conversion utilities (not needed)

---

## 1.6 Assumptions & Dependencies

### Assumptions

**A1: Engine M3.x Readiness**
- FlowTime Engine M3.x will be available for integration testing
- Engine API contracts match KISS documentation

**A2: Template System Architecture**
- Current template system (metadata-driven with parameter substitution) is sufficient
- No major refactoring of template engine required
- YamlDotNet serialization supports nested object parameter substitution

**A3: Schema Breaking Changes**
- Breaking changes from schema 1.0 to 1.1 are acceptable
- Topology is REQUIRED for time-travel functionality (schema 1.1)
- Simulation-only templates can remain at schema 1.0

---

### Dependencies

**D1: FlowTime Engine M3.x (CRITICAL PATH)**
- **Status:** In development
- **Risk:** High (blocks integration testing)
- **Mitigation:** Develop against KISS spec, validate with Engine team regularly

**D2: KISS Architecture Stability**
- **Status:** Chapters 1-6 finalized
- **Risk:** Low (spec is stable)
- **Mitigation:** Track spec changes via architecture review meetings

**D3: YamlDotNet Library**
- **Current Version:** 13.7.1
- **Required Features:** Nested object serialization, nullable type handling
- **Risk:** Low (library is mature)
- **Mitigation:** Verify features in initial spike

**D4: Testing Infrastructure**
- **Status:** Existing test suite adequate
- **Risk:** Low
- **Mitigation:** Add performance benchmarking framework early

---

## 1.7 Implementation Milestones

### Must-Have (P0)

1. ✅ All generated models pass Engine M3.0 schema validation
2. ✅ 5/5 built-in templates support time-travel
3. ✅ Zero breaking changes to existing API
4. ✅ Integration tests with Engine M3.0 pass at 100%
5. ✅ Backward compatibility tests pass at 100%

### Should-Have (P1)

6. ✅ Validation framework with 12 error categories
7. ✅ Performance benchmarks show <10% regression
8. ✅ Documentation complete for all changes
9. ✅ Error messages include actionable suggestions
10. ✅ Migration guide for custom templates

### Nice-to-Have (P2)

11. ⭐ JSON Schema for IDE autocomplete
12. ⭐ Visual examples in documentation
13. ⭐ Performance optimization for large topologies
14. ⭐ Telemetry replay template examples

---

## 1.11 Risk Summary

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| **Engine M3.0 Delay** | Medium | High | Weekly sync, develop against spec |
| **Parameter Substitution Complexity** | Low | High | Early spike, fallback to manual substitution |
| **Template Maintenance Burden** | High | Medium | Start with 2 templates, expand gradually |
| **Performance Regression** | Medium | Medium | Benchmark early, optimize hot paths |
| **Validation False Positives** | Medium | Medium | Tunable validation, warning mode |

**Top 3 Risks to Monitor:**
1. Engine M3.0 delivery schedule
2. Parameter substitution in nested structures
3. Validation performance with large topologies

---

## 1.12 Communication Plan

### Weekly Status Updates
- **To:** Engineering leadership, Product Manager
- **Format:** Email with red/yellow/green status
- **Content:** Progress, blockers, risks

### Architecture Reviews
- **Frequency:** End of Phase 1, End of Phase 4
- **Attendees:** Architect, Senior Engineers, PM
- **Content:** Design decisions, trade-offs, approvals

### Demo Sessions
- **Frequency:** End of Week 2, End of Week 4, End of Week 6
- **Attendees:** Engineering team, stakeholders
- **Content:** Working software, feedback collection

### Retrospectives
- **Frequency:** End of Week 3, End of Week 6
- **Attendees:** Core team
- **Content:** What went well, what to improve

---

## 1.10 Next Steps

**Immediate Actions:**
1. ☐ Review this planning document
2. ☐ Set up project tracking
3. ☐ Create feature branch: `feature/time-travel-m3`

**M3.0 Kickoff:**
4. ☐ Verify parameter substitution in nested objects (spike)
5. ☐ Set up performance benchmarking framework
6. ☐ Begin implementation of schema classes

**Ongoing:**
7. ☐ Regular sync with FlowTime Engine team
8. ☐ Track milestone progress

---

**End of Chapter 1**

**Next:** Chapter 2 - Architecture & Design Principles

---

## Document Navigation

- **Current:** Chapter 1 - Executive Summary & Objectives
- **Next:** Chapter 2 - Architecture & Design
- **Related:** Audit Analysis Document (reference for gaps)