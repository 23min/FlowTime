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

#### 0.1 Test Analysis & Planning
**Approach**: Analyze existing test coverage and identify gaps before making any changes.

**Key Activities:**
- **Current Test Audit**: Review existing template, API, and engine tests
- **Test Gap Analysis**: Identify missing coverage for schema validation, API contracts, UI components
- **Test Strategy**: Define test patterns for new schema formats
- **Breaking Change Tests**: Create tests that will fail with old schema, pass with new schema

#### 0.2 New Test Implementation
**Focus**: Write tests for target state before implementing changes.

**Test Categories:**
- **Schema Validation Tests**: New template format validation
- **API Contract Tests**: FlowTime-Sim new endpoints and responses  
- **Engine Schema Tests**: `binSize`/`binUnit` format support
- **UI Integration Tests**: New parameter forms and validation
- **Translation Tests**: Schema conversion between formats
- **Regression Tests**: Ensure existing functionality preservation

#### 0.3 Legacy Test Identification
**Goal**: Mark obsolete tests for removal and update tests that need modification.

**Activities:**
- **Obsolete Test Marking**: Identify tests that will become invalid
- **Update Planning**: Plan modifications for tests that need schema updates
- **Test Cleanup Strategy**: Document which tests to remove vs. update

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

### Phase 2: FlowTime Engine Schema Migration
**Goal**: Migrate FlowTime Engine to use `binSize`/`binUnit` format for schema unification

**Strategic Benefits:**
- **Schema Unification**: Single schema across Template and Engine
- **Enhanced Flexibility**: Support for hours, days, etc. natively
- **Future-Proofing**: Extensible time unit system
- **Reduced Complexity**: No translation layer needed

#### 2.1 Core Engine Updates (flowtime-vnext)
**Files to Update:**
- `src/FlowTime.Core/TimeGrid.cs` → Update to support binSize/binUnit
- `src/FlowTime.Core/Models/ModelParser.cs` → Add unit conversion logic
- `src/FlowTime.Contracts/Dtos/ModelDtos.cs` → Schema changes

**TimeGrid Migration:**
```csharp
// Before
public readonly record struct TimeGrid(int Bins, int BinMinutes)

// After
public readonly record struct TimeGrid(int Bins, int BinSize, string BinUnit)
{
    public int BinMinutes => BinUnit switch
    {
        "minutes" => BinSize,
        "hours" => BinSize * 60,
        "days" => BinSize * 1440,
        _ => throw new ArgumentException($"Unknown unit: {BinUnit}")
    };
}
```

#### 2.2 API & Service Updates (flowtime-vnext)
**Files to Update:**
- `src/FlowTime.API/Program.cs` → Update API responses
- `src/FlowTime.API/Services/*.cs` → Update exporters (3-4 files)
- `src/FlowTime.Cli/Program.cs` → Update CLI output

#### 2.3 Examples & Tests Migration (flowtime-vnext)
**Files to Update:**
- `examples/hello/model.yaml` → Update grid format
- `examples/it-system/model.yaml` → Update grid format
- `examples/transportation/model.yaml` → Update grid format
- `examples/shift-test/model.yaml` → Update grid format
- All test files referencing `binMinutes`

**Example Migration:**
```yaml
# Before
grid:
  bins: 24
  binMinutes: 60

# After  
grid:
  bins: 24
  binSize: 1
  binUnit: "hours"
```

#### 2.4 Documentation Updates
**Files to Update:**
- `docs/schemas/engine-input-schema.md` → Update to new format
- `docs/schemas/README.md` → Update examples
- `docs/guides/CLI.md` → Update model examples

### Phase 3: UI API Integration & Service Updates
**Goal**: Update FlowTime UI to consume new template API format

#### 3.1 API Client Updates (flowtime-vnext)
**Files to Update:**
- `ui/FlowTime.UI/Services/FlowTimeSimApiClient.cs` → Major restructure
- `ui/FlowTime.UI/Services/TemplateServices.cs` → Interface updates

**ApiTemplateInfo Replacement:**
```csharp
// Replace current ApiTemplateInfo with new structure
public record NewApiTemplateInfo(
    TemplateMetadata Metadata,
    TemplateParameter[] Parameters,
    int NodeCount,
    int OutputCount
);

// Update API methods
public async Task<Result<List<NewApiTemplateInfo>>> GetTemplatesAsync(CancellationToken ct = default);
public async Task<Result<NewTemplateDefinition>> GetTemplateAsync(string templateId, CancellationToken ct = default);
public async Task<Result<TemplateGenerationResponse>> GenerateModelAsync(string templateId, Dictionary<string, object> parameters, CancellationToken ct = default);
```

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

### Phase 1 Complete
- [ ] New template types defined and validated
- [ ] FlowTime-Sim service supports new schema
- [ ] Basic API endpoints functional
- [ ] Template validation logic implemented

### Phase 2 Complete  
- [ ] FlowTime Engine uses `binSize`/`binUnit` format
- [ ] All examples updated to new schema
- [ ] TimeGrid class migration complete
- [ ] API responses use new format
- [ ] All tests pass with new schema

### Phase 3 Complete
- [ ] FlowTime UI API client updated
- [ ] Template service supports new format
- [ ] Parameter handling modernized
- [ ] Demo templates deprecated (API-only approach)

### Phase 4 Complete
- [ ] Full UI integration working
- [ ] All documentation updated
- [ ] Comprehensive test coverage
- [ ] Migration guide published

### Milestone 2.9 Complete
- [ ] **Schema Unification**: Single `binSize`/`binUnit` format across ecosystem
- [ ] New template schema fully integrated
- [ ] All legacy template code removed
- [ ] Complete test coverage
- [ ] Performance validation passed
- [ ] Documentation complete and accurate

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

