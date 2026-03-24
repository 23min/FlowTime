# FlowTime-Sim M2.6.1-v0.5.0 Release Notes

**Release Date**: October 3, 2025  
**Version**: 0.5.0  
**Milestone**: SIM-M2.6.1 — Schema Evolution (Target Model Format)  
**Type**: Major Release (breaking change - schema convergence)  
**Git Tag**: `v0.5.0` (pending merge)  
**Branch**: `feature/core-m2.7/provenance-integration`

## Overview

This release implements schema convergence with FlowTime Engine M2.9, removing Sim's grid conversion layer and adopting the unified target model schema. Sim now passes through the template's native `binSize`/`binUnit` format instead of converting to `binMinutes`, simplifying the architecture and eliminating a source of potential errors.

**Key Insight**: Templates already used the correct format. We're **removing** unnecessary conversion logic, not adding complexity.

## 🎯 Schema Convergence

### Architecture Transformation

**Old Architecture (v0.4.0)**:
```
Template (binSize/binUnit) → Sim converts → Model (binMinutes) → Engine
                              ❌ CONVERSION LAYER
```

**New Architecture (v0.5.0)**:
```
Template (binSize/binUnit) → Sim passes through → Model (binSize/binUnit) → Engine
                              ✅ NO CONVERSION
```

### Why This Matters

- **Simpler**: Removes ~50-100 lines of conversion logic
- **Safer**: Eliminates conversion errors and edge cases
- **Cleaner**: Templates and models now use the same format
- **Aligned**: Direct compatibility with Engine M2.9 target schema

## 🚀 What's New

### 1. Schema Version Field

All generated models now include `schemaVersion: 1` as the first field:

```yaml
schemaVersion: 1
grid:
  bins: 24
  binSize: 1
  binUnit: hours
nodes:
  # ... rest of model
```

**Purpose**: Enables Engine to validate and version model formats

### 2. No Grid Conversion

Grid configuration now passes through unchanged from templates:

**Template**:
```yaml
grid:
  bins: 24
  binSize: 1
  binUnit: hours
```

**Generated Model** (same format):
```yaml
schemaVersion: 1
grid:
  bins: 24
  binSize: 1
  binUnit: hours
```

**Removed**: `ConvertGridToEngineFormat()` and all grid conversion helper methods

### 3. All Time Units Supported

Direct support for all time units without conversion:

- `minutes` - Fine-grained time periods
- `hours` - Common business hours
- `days` - Daily cycles
- `weeks` - Weekly planning periods

**Example**:
```yaml
# Weekly planning model (no conversion needed)
grid:
  bins: 52
  binSize: 1
  binUnit: weeks
```

### 4. Field Transformations Preserved

All existing template-to-model transformations continue to work:

| Template Field | Model Field | Transformation |
|----------------|-------------|----------------|
| `expression` | `expr` | ✅ Renamed |
| `filename` | `as` | ✅ Renamed |
| `metadata` | (none) | ✅ Stripped |
| `parameters` | (none) | ✅ Stripped |
| `dependencies` | (none) | ✅ Stripped |
| `description` | (none) | ✅ Stripped |
| `outputs[].id` | (none) | ✅ Stripped |
| `binSize` | `binSize` | ✅ **Pass through** (was: convert to binMinutes) |
| `binUnit` | `binUnit` | ✅ **Pass through** (was: convert to binMinutes) |

## 🔧 Technical Implementation

### Simplified Generation Pipeline

```
┌──────────────┐
│   Template   │  (binSize/binUnit - correct format)
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
│ Add schemaVersion    │  🆕 NEW (v0.5.0)
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

### Code Changes

**Removed**:
- `ConvertGridToEngineFormat()` method
- Grid conversion helper methods
- All `binMinutes` calculation logic in Sim codebase

**Added**:
- `schemaVersion: 1` insertion logic
- Schema version validation

**Preserved**:
- Field stripping (metadata, parameters, dependencies)
- Field transformations (expression→expr, filename→as)
- Parameter substitution
- Node processing

## 🛠️ Breaking Changes

### Output Format Change

**What Changed**:
```yaml
# Old Format (v0.4.0)
grid:
  bins: 24
  binMinutes: 60

# New Format (v0.5.0)
schemaVersion: 1
grid:
  bins: 24
  binSize: 1
  binUnit: hours
```

### Who's Affected

- **API consumers**: Parsing generated models must handle new format
- **Integration systems**: Expecting old format will break
- **Engine versions**: Requires Engine 0.5.0+ (Engine M2.9)

### Compatibility Matrix

| Engine Version | Sim Version | Status |
|----------------|-------------|--------|
| Engine 0.4.x | Sim 0.4.x | ✅ Works (old format) |
| Engine 0.5.0+ | Sim 0.4.x | ❌ Broken (format mismatch) |
| Engine 0.4.x | Sim 0.5.0+ | ❌ Broken (format mismatch) |
| Engine 0.5.0+ | Sim 0.5.0+ | ✅ Works (new format) |

**Recommendation**: Coordinated upgrade of both Sim and Engine to 0.5.0+

## 🧪 Testing

### Test Coverage

All tests updated to validate new format:

- **Grid Format Tests**: Verify binSize/binUnit (not binMinutes)
- **Schema Version Tests**: Verify schemaVersion always present
- **Time Unit Tests**: All units (minutes, hours, days, weeks) work
- **Field Stripping Tests**: Authoring fields correctly removed
- **Field Transformation Tests**: expression→expr, filename→as work

### Integration Validation

- ✅ End-to-end testing with Engine M2.9
- ✅ All templates generate correctly in new format
- ✅ schemaVersion field always present as first field
- ✅ No binMinutes in generated models
- ✅ Engine accepts and validates new format
- ✅ Old schema correctly rejected by Engine (HTTP 400)

### Codebase Verification

```bash
# Verify no binMinutes conversion in Sim codebase
grep -r "binMinutes" src/
# Result: No conversion code (only comments/docs)

# Verify schemaVersion implementation
grep -r "schemaVersion" src/
# Result: Proper implementation in ModelGenerator
```

## 📊 Impact Analysis

### Code Reduction
- **Removed**: ~50-100 lines of grid conversion logic
- **Added**: ~10 lines of schemaVersion insertion logic
- **Net**: Simpler, cleaner codebase

### Architecture Benefits
- **Simpler Pipeline**: One less transformation step
- **Fewer Edge Cases**: No time unit conversion errors
- **Better Alignment**: Direct Engine compatibility
- **Easier Maintenance**: Less code to maintain and test

### Performance
- **Slightly Faster**: Removed conversion step
- **Lower Memory**: No intermediate conversion objects
- **Minimal Impact**: Overall performance similar

## 🔄 Migration Guide

### For API Consumers

**Update model parsers**:

```csharp
// Old parsing (v0.4.x)
var binMinutes = model.Grid.BinMinutes;
var hours = binMinutes / 60;

// New parsing (v0.5.0)
var schemaVersion = model.SchemaVersion; // Always 1
var binSize = model.Grid.BinSize;
var binUnit = model.Grid.BinUnit; // "minutes", "hours", "days", "weeks"

// Convert to minutes if needed
var binMinutes = binUnit switch {
    "minutes" => binSize,
    "hours" => binSize * 60,
    "days" => binSize * 1440,
    "weeks" => binSize * 10080,
    _ => throw new InvalidOperationException($"Unknown binUnit: {binUnit}")
};
```

### For Template Authors

**No changes required** - templates already use correct format!

All existing templates with binSize/binUnit continue to work unchanged.

### For System Integrators

**Coordinated upgrade required**:

1. **Upgrade Engine** to 0.5.0+ (Engine M2.9)
2. **Upgrade Sim** to 0.5.0 (SIM-M2.6.1)
3. **Update parsers** to handle new format
4. **Test end-to-end** workflow

**Timeline**: Both upgrades should happen in same deployment window

## 🎯 What's Next

### Immediate: SIM-M2.7 (Provenance Integration)

Schema convergence enables provenance integration:
- Model identity and traceability
- Template → model → run lineage
- Complete provenance metadata

### Future: v1.0.0 (Stable Release)

After provenance integration:
- API contract stability
- Backward compatibility policy
- Production-ready release

## 📚 Documentation

### Updated Documentation

- **Target Schema**: `docs/schemas/target-model-schema.md`
- **Target Schema (YAML)**: `docs/schemas/target-model-schema.yaml`
- **Analysis**: `docs/schemas/M2.9-ANALYSIS.md`
- **Milestone**: `work/milestones/SIM-M2.6.1.md` marked complete

### Engine Coordination

- **Engine Milestone**: Engine M2.9 implemented parallel changes
- **Integration Validated**: End-to-end testing confirms compatibility
- **Breaking Change**: Coordinated release strategy

## 🙏 Acknowledgments

This release required close coordination with FlowTime Engine M2.9 for schema alignment and validation. The schema convergence work simplifies both codebases and establishes a cleaner architectural foundation.

---

**Contributors**: GitHub Copilot  
**Branch**: `feature/core-m2.7/provenance-integration` (includes schema evolution)  
**Previous Version**: v0.4.0 (SIM-M2.6-CORRECTIVE)  
**Next Version**: v0.6.0 (SIM-M2.7 Provenance Integration)  
**Status**: Complete and validated

---

## Related Documents

- [SIM-M2.6.1 Milestone](../milestones/SIM-M2.6.1.md)
- [Target Model Schema](../schemas/target-model-schema.md)
- [M2.9 Analysis](../schemas/M2.9-ANALYSIS.md)
- [Engine M2.9 Milestone](../../flowtime-vnext/work/milestones/Engine-M2.9.md)
- [Registry Integration Architecture](../architecture/registry-integration.md)
