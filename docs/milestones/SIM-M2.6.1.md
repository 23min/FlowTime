# SIM-M2.6.1 — Schema Evolution (Target Model Format)

> **📋 Charter Alignment**: This milestone implements schema convergence with Engine M2.9, removing Sim's conversion layer and adopting the unified target model schema.

**Status:** ✅ Complete  
**Dependencies:** SIM-M2.6-CORRECTIVE (v0.4.0), Engine M2.9 schema support  
**Target:** FlowTime-Sim outputs target schema format (binSize/binUnit)  
**Version:** 0.4.0 → 0.5.0 (breaking change)  
**Completed:** October 3, 2025

---

## Goal

Remove FlowTime-Sim's grid conversion layer and adopt the **unified target model schema** that Engine M2.9 supports. This simplifies the architecture by having Sim pass through the already-correct template format instead of converting it.

**Key Insight**: Templates already use the correct format (`binSize`/`binUnit`). We're **removing** conversion logic, not adding it.

---

## Context & Charter Alignment

### Current Architecture (v0.4.0)

```
Template (binSize/binUnit) → Sim converts → Model (binMinutes) → Engine
                              ❌ CONVERSION LAYER
```

**Problem**: Unnecessary conversion layer adds complexity and potential for errors.

### Target Architecture (v0.5.0)

```
Template (binSize/binUnit) → Sim passes through → Model (binSize/binUnit) → Engine
                              ✅ NO CONVERSION
```

**Benefit**: Simpler, templates already correct, Engine M2.9 accepts new format directly.

---

## Functional Requirements

### FR-SIM-M2.6.1-1: Remove Grid Conversion Logic

**Current Behavior**: Sim converts `binSize`/`binUnit` to `binMinutes`

**Target Behavior**: Sim passes through `binSize`/`binUnit` unchanged

**Implementation**:
- Delete `ConvertGridToEngineFormat()` method
- Remove all grid conversion helper methods
- Keep field stripping logic (metadata, parameters, dependencies)

**Acceptance**:
- ✅ No conversion code in codebase
- ✅ Templates with binSize/binUnit pass through unchanged
- ✅ All time units supported (minutes, hours, days, weeks)

---

### FR-SIM-M2.6.1-2: Add schemaVersion Field

**Requirement**: All generated models must include `schemaVersion: 1`

**Implementation**:
```csharp
if (!outputYaml.TrimStart().StartsWith("schemaVersion:"))
{
    outputYaml = "schemaVersion: 1\n" + outputYaml;
}
```

**Acceptance**:
- ✅ schemaVersion appears in all generated models
- ✅ schemaVersion is first field in output
- ✅ Templates with schemaVersion already present not duplicated

---

### FR-SIM-M2.6.1-3: Preserve Field Transformations

**Ensure existing transformations still work**:

| Template Field | Model Field | Status |
|----------------|-------------|---------|
| `expression` | `expr` | ✅ Keep |
| `filename` | `as` | ✅ Keep |
| `metadata` | (stripped) | ✅ Keep |
| `parameters` | (stripped) | ✅ Keep |
| `dependencies` | (stripped) | ✅ Keep |
| `description` | (stripped) | ✅ Keep |
| `outputs[].id` | (stripped) | ✅ Keep |
| `binSize` | `binSize` | 🆕 Pass through (don't convert) |
| `binUnit` | `binUnit` | 🆕 Pass through (don't convert) |

**Acceptance**:
- ✅ All template-to-model transformations work correctly
- ✅ binSize/binUnit NOT converted to binMinutes
- ✅ Authoring fields stripped correctly

---

### FR-SIM-M2.6.1-4: Update Test Suite

**Test Coverage Required**:
- Grid format tests (binSize/binUnit, not binMinutes)
- Schema version tests (always present, first field)
- Time unit preservation tests (minutes, hours, days, weeks)
- Field stripping tests (verify authoring fields removed)
- Integration tests (end-to-end with Engine M2.9)

**Acceptance**:
- ✅ All tests updated to expect new format
- ✅ No tests checking for binMinutes
- ✅ 100% test pass rate
- ✅ Comprehensive schema validation coverage

---

## Integration Points

### Engine M2.9 Schema Support

**Dependency**: Engine must support new schema format first

**Coordination**:
- Engine M2.9 accepts `binSize`/`binUnit` format
- Engine M2.9 requires `schemaVersion: 1` field
- Engine M2.9 rejects old `binMinutes` format

**Testing**: End-to-end validation with Engine M2.9

---

### Template System (No Changes)

**Good News**: Templates already use target format!

**Verification**:
```yaml
# All templates already have:
grid:
  bins: 24
  binSize: 1
  binUnit: hours
```

**Acceptance**: ✅ No template changes required

---

### CLI and API (Output Format Change)

**Breaking Change**: Generated model format changes

**API Response Change**:
```yaml
# Old (v0.4.x)
grid:
  bins: 24
  binMinutes: 60

# New (v0.5.0)
schemaVersion: 1
grid:
  bins: 24
  binSize: 1
  binUnit: hours
```

**Acceptance**:
- ✅ CLI outputs new format
- ✅ API /generate endpoint returns new format
- ✅ Documentation updated
- ✅ Migration guide provided

---

## Technical Architecture

### Simplified Generation Pipeline

```
┌──────────────┐
│   Template   │  (binSize/binUnit already correct)
└──────┬───────┘
       │
       ▼
┌──────────────┐
│ Parse YAML   │
└──────┬───────┘
       │
       ▼
┌──────────────────────┐
│ Parameter Subst.     │
└──────┬───────────────┘
       │
       ▼
┌──────────────────────┐
│ Add schemaVersion    │  🆕 NEW
└──────┬───────────────┘
       │
       ▼
┌──────────────────────┐
│ Transform Fields     │  (expression→expr, filename→as)
└──────┬───────────────┘
       │
       ▼
┌──────────────────────┐
│ Strip Authoring      │  (metadata, parameters, dependencies)
└──────┬───────────────┘
       │
       ▼
┌──────────────────────┐
│ Output Model YAML    │  (binSize/binUnit passed through)
└──────────────────────┘

❌ REMOVED: ConvertGridToEngineFormat() step
```

---

## Acceptance Criteria

### Code Quality
- ✅ All conversion logic removed
- ✅ No `binMinutes` calculations in codebase
- ✅ schemaVersion addition logic implemented
- ✅ Field stripping logic preserved

### Test Coverage
- ✅ 100% test pass rate
- ✅ Grid format tests updated (binSize/binUnit)
- ✅ Schema version tests added
- ✅ Time unit preservation tests added
- ✅ No tests asserting on binMinutes

### Output Validation
- ✅ All generated models include `schemaVersion: 1`
- ✅ All generated models use binSize/binUnit
- ✅ No generated models contain binMinutes
- ✅ Field transformations work correctly

### Integration
- ✅ End-to-end testing with Engine M2.9 successful
- ✅ All examples generate correctly
- ✅ CLI and API produce new format

### Documentation
- ✅ Migration guide created
- ✅ Target schema documented
- ✅ API documentation updated
- ✅ CHANGELOG updated
- ✅ README updated with breaking change notice

---

## Implementation Plan

### Phase 1: Code Changes (2 hours)
1. Remove `ConvertGridToEngineFormat()` and helpers
2. Add `schemaVersion` insertion logic
3. Verify field stripping logic correct
4. Verify field transformation logic correct

### Phase 2: Test Updates (3 hours)
1. Update grid format tests (binMinutes → binSize/binUnit)
2. Add schema version tests
3. Add time unit preservation tests
4. Update integration tests
5. Remove obsolete conversion tests

### Phase 3: Example Verification (1 hour)
1. Verify examples already use target format
2. Generate models from all examples
3. Validate output format correct
4. Update example documentation

### Phase 4: Documentation (2 hours)
1. Create migration guide
2. Update schema documentation
3. Update API documentation
4. Update CHANGELOG
5. Update README

### Phase 5: Validation (2 hours)
1. Run full test suite
2. Manual integration testing
3. End-to-end with Engine M2.9
4. Validate all examples

### Phase 6: Release (1 hour)
1. Version bump (0.4.0 → 0.5.0)
2. Create PR
3. Code review
4. Merge and tag

**Total Estimated Time**: 11 hours (1-2 days)

---

## Breaking Changes

### Output Format Change

**What Changed**:
- Grid format: `binMinutes` → `binSize`/`binUnit`
- Schema version: Now always present (`schemaVersion: 1`)

**Who's Affected**:
- API users parsing generated models
- Systems expecting old format
- Integration tests with Engine < 0.5.0

**Migration Path**:
1. Upgrade Engine to 0.5.0 (Engine M2.9)
2. Upgrade Sim to 0.5.0 (SIM-M2.6.1)
3. Update any parsers expecting old format

**Compatibility Matrix**:
```
Engine 0.4.x + Sim 0.4.x = ✅ Works (old format)
Engine 0.5.0 + Sim 0.4.x = ❌ Broken (format mismatch)
Engine 0.4.x + Sim 0.5.0 = ❌ Broken (format mismatch)
Engine 0.5.0 + Sim 0.5.0 = ✅ Works (new format)
```

**Recommendation**: Coordinated release of both Sim and Engine

---

## Risk Mitigation

### Risk: Engine M2.9 Not Ready

**Impact**: Cannot release SIM-M2.6.1 without Engine support

**Mitigation**:
- Coordinate with Engine team on timeline
- Block SIM-M2.6.1 release until Engine M2.9 complete
- Test with Engine M2.9 preview builds

---

### Risk: Breaking Change Impact

**Impact**: Users' systems break on upgrade

**Mitigation**:
- Clear migration guide
- Version bump to 0.5.0 (signals breaking change)
- Detailed CHANGELOG entry
- Coordinated release announcement

---

### Risk: Missed Conversion Logic

**Impact**: Some code still converts to binMinutes

**Mitigation**:
- Code review for all conversion-related code
- Search codebase for "binMinutes"
- Comprehensive test coverage
- Manual validation of all examples

---

## Success Metrics

### Technical Metrics
- **Code Reduction**: Remove ~50-100 lines of conversion logic
- **Test Pass Rate**: 100%
- **Schema Compliance**: 100% of outputs match target schema
- **Integration Success**: All examples work with Engine M2.9

### Quality Metrics
- **Breaking Change Communication**: Migration guide complete
- **Documentation Coverage**: All docs updated
- **Backward Compatibility**: Clear compatibility matrix provided

---

## Related Documents

- **Target Schema**: `docs/schemas/target-model-schema.md`
- **Target Schema (YAML)**: `docs/schemas/target-model-schema.yaml`
- **Analysis**: `docs/schemas/M2.9-ANALYSIS.md`
- **Engine Milestone**: `docs/milestones/Engine-M2.9.md` (flowtime-vnext)
- **Registry Integration**: `docs/architecture/registry-integration.md`

---

## Next Steps

**After SIM-M2.6.1 Complete**:
1. **SIM-M2.6.2**: Add model provenance metadata (optional)
2. **SIM-M2.7**: Engine integration and KISS registry architecture
3. **SIM-M2.8**: Template enhancements and model authoring

---

## Revision History

| Date | Change | Author |
|------|--------|--------|
| 2025-10-01 | Initial milestone created | Assistant |
| 2025-10-01 | Added KISS architecture references | Assistant |
| 2025-10-03 | Marked complete with validation | Assistant |

---

## Completion Summary

**Completed**: October 3, 2025  
**Branch**: `feature/core-m2.7/provenance-integration` (includes schema evolution work)  

### Delivered Changes

✅ **Schema Version Field**: All generated models include `schemaVersion: 1`  
✅ **No Grid Conversion**: Removed conversion layer, templates pass through binSize/binUnit unchanged  
✅ **Field Transformations Preserved**: All existing transformations (expression→expr, filename→as) work correctly  
✅ **Test Suite Updated**: All tests use new format, no binMinutes references in Sim codebase  
✅ **Engine Integration Validated**: End-to-end testing with Engine M2.9 confirms acceptance of new format

### Validation Results

- No `binMinutes` conversion code in Sim codebase (verified via grep)
- `schemaVersion: 1` added to all generated models
- Templates with binSize/binUnit pass through unchanged
- Integration tests validate Engine accepts new format
- Old schema (arrivals/route with binMinutes) correctly rejected by Engine

### Follow-Up Schema Consistency Fix

**Note**: Initial completion (October 3, 2025) left template parameter names as `binMinutes` for backward compatibility during transition. Follow-up fix (October 6, 2025) completed full schema convergence:

- ✅ All 4 template files: renamed parameter `binMinutes` → `binSize` (matches field name)
- ✅ Test files: updated to use `binSize` parameter and current node-based schema
- ✅ JSON Schema: updated `run.schema.json` to define new `binSize`/`binUnit` format
- ✅ All 128 tests passing after schema consistency updates

The two-phase approach allowed Engine integration to proceed while templates used temporary parameter name mappings. Full consistency achieved in schema fix commit.

**Status**: Complete and validated (including schema consistency)  
**Priority**: High (enabled SIM-M2.7)  
**Effort**: Completed as part of provenance integration work  
**Impact**: Breaking change (major output format change) - coordinated with Engine M2.9
