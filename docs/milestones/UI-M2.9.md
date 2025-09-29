# UI-M2.9 — New Template Schema Migration

**Status:** 📋 Planned  
**Dependencies:** FlowTime-Sim new template schema implementation  
**Target:** Complete migration to DAG-based template system  
**Date:** 2025-10-07

---

## Overview
Complete migration to the new DAG-based template schema for FlowTime-Sim integration. This milestone replaces the current mustache-style template system with a structured JSON/YAML format supporting formal parameters, node types (const, pmf, expr), and explicit outputs.

**Breaking Change**: No backwards compatibility with existing template endpoints, schemas, or artifacts.

## Strategic Context
- **Motivation**: Enable advanced simulation capabilities with DAG computation models, formal parameter validation, and structured outputs
- **Impact**: Complete overhaul of template system from simple string substitution to rich computational modeling
- **Dependencies**: FlowTime-Sim service must implement new schema support first

## Schema Architecture

**Important**: This migration involves **two different schema formats**:

### 1. Template Schema (FlowTime-Sim Input)
```yaml
grid:
  bins: 24
  binSize: 60        # ← New flexible format
  binUnit: "minutes" # ← Supports minutes, hours, days
```

### 2. Engine Schema (FlowTime Engine Input)  
```yaml
grid:
  bins: 24
  binMinutes: 60     # ← Engine still uses binMinutes internally
```

**FlowTime-Sim Responsibility**: Convert between these formats during model generation.

### Migration Scope
- **Template Schema**: Full migration to `binSize`/`binUnit` format ✅
- **Engine Schema**: Remains `binMinutes` (no engine changes needed)
- **UI Template Forms**: Support new `binSize`/`binUnit` parameters
- **Translation Logic**: FlowTime-Sim handles schema conversion

## Five-Phase Migration Plan

### Phase 0: Test-Driven Development Setup
**Goal**: Establish comprehensive test coverage before migration to ensure safe refactoring

**TDD Strategy**: Write tests first, then implement changes to ensure nothing breaks during the migration.

#### 0.1 Legacy Test Isolation
**Approach**: Move existing UI tests to `.Legacy` namespaces before building new test suite.

**Namespace Migration:**
- `FlowTime.UI.Tests` → `FlowTime.UI.Tests.Legacy`
- `FlowTime.UI.Services.Tests` → `FlowTime.UI.Services.Tests.Legacy`

**Benefits:**
- **Clear Separation**: Old vs new UI tests clearly distinguished
- **Preservation**: Legacy tests remain functional during UI migration
- **Clean Architecture**: New tests start with proper structure for new template schema

#### 0.2 New UI Test Implementation
**Focus**: Write tests for new template schema UI integration before implementing changes.

**New Test Structure:**
```
FlowTime.UI.Tests/
├── Services/
│   ├── TemplateServiceTests.cs      # New template API integration
│   └── FlowTimeSimApiClientTests.cs # New API client tests
├── Components/
│   └── TemplateFormTests.cs         # New parameter form tests
├── Schema/
│   └── TemplateValidationTests.cs   # New schema validation tests
└── Legacy/                          # Moved legacy tests
    └── [existing test files]
```

#### 0.3 Test-First UI Development
**Goal**: Write failing tests that define UI success criteria before implementation.

**UI TDD Focus:**
- Template form generation from new schema
- API client integration with new endpoints
- Parameter validation and error handling
- Schema compatibility during M2.9 evolution

**Benefits of TDD Approach:**
- ✅ **Safe Refactoring**: Tests ensure no functionality is lost
- ✅ **Clear Requirements**: Tests define exactly what new system should do
- ✅ **Confidence**: Green tests mean migration is successful
- ✅ **Documentation**: Tests serve as living specification

### Phase 1: Core Infrastructure & Schema Support
**Goal**: Establish foundation for new template format without breaking existing functionality

#### 1.1 FlowTime-Sim Schema Support (Blocking Dependency)
**Requirement**: FlowTime-Sim service must implement the new template schema as defined in `docs/schemas/template-schema.md`.

**Expected API Endpoints:**

```http
GET /api/templates
→ Returns list with new metadata structure

GET /api/templates/{id}  
Accept: application/json       # Default - returns JSON
Accept: application/x-yaml     # Returns YAML

POST /api/templates/{id}/generate
→ Uses structured parameters

POST /api/templates/{id}/validate (NEW)
→ Template validation endpoint
```

#### 1.2 UI Type Definitions
**Goal**: Define corresponding types in FlowTime UI to match the new schema.

### Phase 2: UI API Integration & Service Updates
**Goal**: Update FlowTime UI to consume new template API format and support FlowTime Engine schema evolution

**Note**: This phase runs in parallel with M2.9 (FlowTime Engine Schema Migration). The engine migration is handled separately in M2.9.
- **Reduced Complexity**: No translation layer needed

#### 2.1 API Client Updates
**Goal**: Update FlowTime UI to consume new template API format and support engine schema evolution.

**Files to Update:**
- `ui/FlowTime.UI/Services/FlowTimeSimApiClient.cs` → Major restructure for new API
- `ui/FlowTime.UI/Services/TemplateServices.cs` → Interface updates
- `ui/FlowTime.UI/Services/TemplateServiceImplementations.cs` → Schema support updates

**API Integration Updates:**
- Support new structured template metadata and parameters
- Handle both `binSize`/`binUnit` (template) and `binMinutes` (engine) formats during transition
- Update template generation request/response handling
- Implement new template validation endpoints

#### 2.2 Schema Format Support
**Goal**: Update UI forms and validation to support both schema formats during engine migration.

**Template Form Updates:**
- Update grid parameter forms to use `binSize`/`binUnit` format
- Add time unit selection (minutes, hours, days)
- Maintain backward compatibility during M2.9 engine migration
- Update validation logic for new parameter types

#### 2.3 Engine Integration Strategy
**Goal**: Ensure UI leverages new engine capabilities when M2.9 completes.

**Integration Strategy:**
- UI templates use `binSize`/`binUnit` format (already implemented)
- FlowTime-Sim produces Template Schema format (current capability)
- M2.9 will evolve engine to accept Template Schema directly
- Clean integration when M2.9 engine evolution completes

### Phase 3: Template Form & Validation Updates

#### 3.2 Template Service Overhaul (flowtime-vnext)
**Files to Update:**
- `ui/FlowTime.UI/Services/TemplateServiceImplementations.cs` → Complete rewrite

**Key Changes:**
- Remove `ConvertTemplateInfoToTemplate()` - no longer needed
- Remove `CreateParameterSchemaForTemplate()` - parameters come from API
- **Deprecate demo templates** - new schema requires API integration; demo mode becomes API-only for UI testing
- Simplify template loading logic to be purely API-driven

#### 3.3 Parameter Form Generation Updates
**Files to Analyze/Update:**
- `ui/FlowTime.UI/Components/Templates/DynamicParameterForm.razor` (if exists)
- Any parameter form generation logic

**Changes Needed:**
- Handle new parameter types (integer, number, boolean, string, array)
- Support min/max validation from parameter metadata
- Handle array parameter types properly

### Phase 4: Final Integration & Documentation
**Goal**: Complete UI integration, comprehensive testing, and documentation updates

#### 4.1 Template Runner Updates
**Files to Update**:
- `ui/FlowTime.UI/Pages/TemplateRunner.razor` → Parameter handling updates
- Template selection and execution workflows

**Key Changes:**
- Parameter forms now driven by API metadata
- Enhanced validation feedback
- Support for new parameter types

#### 4.2 Documentation Updates
**Files to Update:**

**Major Updates Required:**
- `docs/ui/template-integration-spec.md` → Complete rewrite
- `docs/guides/template-api-integration.md` → Update API examples

**Minor Updates Required:**
- `docs/guides/template-categories.md` → Update examples
- `docs/reference/data-formats.md` → Document new JSON schema
- `docs/reference/contracts.md` → Update template contracts

**New Documentation:**
- `docs/schemas/template-schema.md` → Already created
- `docs/migration/template-schema-migration.md` → Migration guide

#### 4.3 Comprehensive Testing
**Test Areas:**
1. **Unit Tests**: New template types, validation logic
2. **Integration Tests**: API client with new endpoints
3. **E2E Tests**: Full template workflow in UI
4. **Validation Tests**: PMF probability sums, circular dependency detection
5. **Content Negotiation Tests**: YAML vs JSON responses
6. **Engine Schema Tests**: New binSize/binUnit format validation

### Post-M2.9 Documentation Cleanup
**Goal**: Address remaining documentation inconsistencies and legacy references

#### Broader Documentation Updates
- **Legacy schema references** → Update remaining `binMinutes` references in examples and documentation (separate from schema migration)
- **API documentation** → Ensure all FlowTime-Sim API endpoints are properly documented
- **Integration guide updates** → Update guides to reflect new template workflow

**Note**: The core schema migration from `binMinutes` to `binSize`/`binUnit` is completed within M2.9 scope.

## Migration Risks & Mitigation

### High-Risk Areas
1. **Complete API Contract Change**: Templates API response format changes entirely
2. **Parameter System Overhaul**: From simple key-value to structured objects
3. **UI Form Generation**: Parameter forms need complete revision

### Mitigation Strategies
1. **Parallel Development**: Implement new system alongside old (temporarily)
2. **Feature Flags**: Enable gradual rollout of new template system
3. **Comprehensive Testing**: Extensive validation before deprecating old system
4. **Documentation First**: Update specs before implementation

## Dependencies & Prerequisites

### External Dependencies
1. **FlowTime-Sim Service**: Must implement new template schema first
2. **Engine Compatibility**: Engine must support new template format

### Internal Dependencies
1. **Feature Flags**: For gradual rollout
2. **Testing Infrastructure**: For validation of new format

## Success Criteria

### Phase 1 Complete ✅
- [x] New template types defined and validated
- [x] FlowTime-Sim service supports new schema  
- [x] Basic API endpoints functional
- [x] Template validation logic implemented

### Phase 2 Complete 📋 PLANNED
- [ ] FlowTime UI API client updated
- [ ] Template service supports new format
- [ ] UI prepared for M2.9 engine evolution
- [ ] Clean integration path established

### Phase 3 Complete 📋 PLANNED  
- [ ] Parameter forms support new schema types
- [ ] Template selection and execution workflows updated
- [ ] Enhanced validation feedback implemented
- [ ] Demo templates updated for new API

### Phase 4 Complete 📋 PLANNED
- [ ] Full UI integration working
- [ ] All UI documentation updated
- [ ] Comprehensive UI test coverage
- [ ] Template workflow guide published

### UI-M2.9 Complete
- [ ] **UI Schema Support**: FlowTime UI fully supports new template schema
- [ ] **API Integration**: New FlowTime-Sim API fully integrated
- [ ] **Template Workflows**: All template workflows use new structured format
- [ ] **Documentation**: UI integration documentation complete
- [ ] **M2.9 Ready**: UI prepared to leverage M2.9 engine evolution

**Note**: Engine evolution (schema, PMF, RNG) is handled separately in M2.9 milestone.

## File Impact Summary

### Files to Replace Completely
- `ui/FlowTime.UI/Services/TemplateServiceImplementations.cs`

### Files Requiring Major Updates
- `ui/FlowTime.UI/Services/FlowTimeSimApiClient.cs`
- `docs/ui/template-integration-spec.md`

### Files Requiring Minor Updates
- `ui/FlowTime.UI/Services/TemplateServices.cs`
- `ui/FlowTime.UI/Pages/TemplateRunner.razor`
- Various documentation files

### New Files to Create
- `docs/migration/template-schema-migration.md`
- Template validation tests
- Integration tests for new API format

This migration represents a fundamental architectural upgrade that will enable advanced simulation capabilities while providing a much more robust and extensible template system.

