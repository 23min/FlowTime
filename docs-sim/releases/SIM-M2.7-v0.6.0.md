# SIM-M2.7-v0.6.0 Release Notes

**Release Date:** October 7, 2025  
**Milestone:** SIM-M2.7 - Model Provenance Integration  
**Version:** 0.6.0  
**Status:** ✅ Complete

---

## 🎯 Milestone Goal

Enable **complete provenance traceability** from template to run. Every generated model can be traced back to its source template, parameter values, and generation context. Sim generates provenance metadata that Engine stores with run artifacts, enabling reproducibility and workflow tracking.

---

## ✅ What's New in v0.6.0

### 🔍 Provenance Metadata Generation
- **Model ID Generation**: Every model gets unique, deterministic ID (`model_{timestamp}_{hash}`)
- **Template Tracking**: Captures template ID, version, and title
- **Parameter Recording**: All generation parameters preserved in metadata
- **Generator Attribution**: Tracks Sim version used for generation (`flowtime-sim/0.6.0`)

**Provenance Schema** (JSON with camelCase):
```json
{
  "source": "flowtime-sim",
  "modelId": "model_20251007T120000Z_abc123",
  "templateId": "it-system-microservices",
  "templateVersion": "1.0",
  "templateTitle": "IT System - Microservices",
  "parameters": {
    "bins": 12,
    "binSize": 1,
    "binUnit": "hours"
  },
  "generatedAt": "2025-10-07T12:00:00Z",
  "generator": "flowtime-sim/0.6.0",
  "schemaVersion": "1"
}
```

### 🔌 API Enhancements

#### Enhanced `/api/v1/templates/{id}/generate` Endpoint
- Returns both model and provenance in single response:
  ```json
  {
    "model": "schemaVersion: 1\n...",
    "provenance": { ... }
  }
  ```
- **Query Parameter**: `?embed_provenance=true` embeds provenance in model YAML
- **Backward Compatible**: Existing callers can ignore provenance field

### 💻 CLI Enhancements

#### New Provenance Flags
- `--provenance <file>`: Save provenance metadata to separate JSON file
- `--embed-provenance`: Embed provenance directly in model YAML

**Examples**:
```bash
# Separate provenance file
flow-sim generate --template it-system --params bins=12 \
  --output model.yaml --provenance provenance.json

# Embedded provenance
flow-sim generate --template it-system --params bins=12 \
  --output model.yaml --embed-provenance
```

### 🔗 Engine Integration

#### Provenance Delivery Methods
1. **HTTP Header**: `X-Model-Provenance` (JSON string)
2. **Embedded in YAML**: Optional `provenance:` section after `schemaVersion`

#### Engine Storage
- `provenance.json` stored in run artifacts (`/data/run_*/provenance.json`)
- Registry includes provenance metadata (templateId, modelId)
- Query support: `?source=flowtime-sim`, `?metadata.templateId=it-system`

---

## 🏗️ Architecture & Design

### KISS Architecture Maintained
- **Sim**: Stateless generator (no storage)
- **Engine**: Single source of truth (stores everything)
- **UI**: Orchestrator (calls Sim, then Engine)

### Integration Workflow
```
UI/CLI → Sim API → Generate Model + Provenance
      ↓
UI/CLI → Engine API → Execute + Store (model + provenance + results)
      ↓
Engine Registry → Query by template/model/source
```

### Self-Contained Models
Models with embedded provenance are self-documenting:
- Template source preserved
- Parameter values recorded
- Generation context captured
- No external metadata needed

---

## 🧪 Test Coverage

### Unit Tests: 128 Passing
- Provenance generation logic
- Model ID uniqueness and determinism
- API endpoint behavior
- CLI flag handling
- Template parameter substitution
- Schema validation

### Integration Tests: 4/4 Passing
- ✅ Basic workflow (Sim → Engine)
- ✅ Provenance storage validation
- ✅ Optional provenance (backward compatibility)
- ✅ Old schema rejection (breaking change validation)

**Integration Test Suite**:
- Automated bash script (`scripts/test-sim-engine-integration.sh`)
- Manual HTTP examples (`FlowTime.Sim.Service.http`)
- Comprehensive documentation (`docs/testing/sim-engine-integration.md`)

---

## 🔧 Technical Implementation

### Core Components Added/Modified

**New Files**:
- `src/FlowTime.Sim.Core/Services/ProvenanceService.cs` - Provenance generation logic
- `src/FlowTime.Sim.Core/Models/ProvenanceMetadata.cs` - Provenance data model
- `docs/testing/sim-engine-integration.md` - Integration test documentation
- `docs/testing/engine-m2.9-provenance-spec.md` - Engine coordination spec
- `scripts/test-sim-engine-integration.sh` - Automated integration tests

**Modified Files**:
- `src/FlowTime.Sim.Core/Services/NodeBasedTemplateService.cs` - Provenance integration
- `src/FlowTime.Sim.Service/Program.cs` - Enhanced `/generate` endpoint
- `src/FlowTime.Sim.Cli/Commands/GenerateCommand.cs` - Provenance CLI flags
- `tests/FlowTime.Sim.Tests/` - Comprehensive provenance test suite

### API Changes
- **Additive Only**: No breaking changes
- `/api/v1/templates/{id}/generate` response expanded
- New query parameter: `?embed_provenance=true`
- Backward compatible with existing consumers

### Breaking Changes
- **None**: All changes are additive
- Old clients continue working (provenance ignored if not needed)
- New clients opt-in to provenance features

---

## 📊 Performance & Quality

### Model ID Generation
- **Deterministic**: Same template + parameters → same hash
- **Unique**: Timestamp ensures global uniqueness
- **Fast**: SHA256 hashing, milliseconds to generate

### Provenance Size
- Typical provenance: ~500 bytes JSON
- Negligible overhead on model generation
- No performance impact on Engine execution

### Test Results
- **Build Time**: ~18 seconds (clean build)
- **Test Time**: ~6 seconds (128 tests)
- **Integration Tests**: ~30 seconds (4 scenarios)
- **All Tests**: 100% passing (132 total)

---

## 🚀 Development Impact

### For Template Authors
- Full traceability of generated models
- Reproducible model generation
- File-based workflows supported (CLI + embedded provenance)
- No external dependencies for provenance tracking

### For System Integrators
- Complete audit trail (template → model → run)
- Query runs by template/model/source
- Backward compatible (opt-in)
- Both header and embedded delivery supported

### For UI Developers
- Simple orchestration (call Sim, pass to Engine)
- Provenance automatically captured
- Query support for filtering/grouping runs
- No additional storage needed

---

## 📝 Migration Notes

### Upgrading from v0.5.0

**No Breaking Changes**: All changes are additive.

#### API Consumers
- Existing `/generate` calls continue working
- Response now includes `provenance` field (can be ignored)
- Opt-in to embedded provenance with `?embed_provenance=true`

#### CLI Users
- All existing commands work unchanged
- New optional flags: `--provenance`, `--embed-provenance`
- Old model files remain valid

#### Engine Integration
- Engine M2.9+ required for provenance storage
- Old models (no provenance) still execute normally
- Provenance is optional (graceful degradation)

### Recommended Actions
1. Update to Engine M2.9+ (for provenance storage)
2. Update UI to pass provenance from Sim to Engine
3. Use `--embed-provenance` for self-contained model files
4. Query runs by `templateId` to group related executions

---

## 🔗 Related Work

### Dependencies Satisfied
- ✅ **SIM-M2.6.1**: Schema Evolution (binSize/binUnit format)
- ✅ **Engine M2.9**: Provenance Acceptance (X-Model-Provenance header support)

### Enables Future Work
- **SIM-M2.8**: Template enhancements (provenance for template versioning)
- **SIM-M3.0**: Charter-aligned model authoring (provenance for complex workflows)
- **UI Workflows**: Template library management, run comparisons, reproducibility

---

## 📚 Documentation

### New Documentation
- Integration test suite with automated validation
- Engine coordination specification
- Provenance schema reference
- API examples with provenance
- CLI usage examples with new flags

### Updated Documentation
- API endpoint documentation (enhanced `/generate` response)
- CLI command reference (new provenance flags)
- Integration architecture (Sim ↔ Engine workflow)
- Milestone completion summary (SIM-M2.7.md)

---

## 🎉 Achievements

- ✅ **Zero Sim-side storage**: Stateless design maintained
- ✅ **Single source of truth**: Engine registry owns all data
- ✅ **Complete traceability**: Template → model → run
- ✅ **Backward compatible**: No breaking changes
- ✅ **Dual delivery**: Header + embedded methods
- ✅ **Reproducible**: Same inputs → same model ID hash
- ✅ **Self-documenting**: Embedded provenance makes models portable

---

## 📈 Statistics

- **Commits**: 15+ commits across feature branch
- **Files Changed**: 25+ files (new + modified)
- **Lines Added**: ~2,000 lines (code + tests + docs)
- **Test Coverage**: 128 unit + 4 integration tests
- **Integration Validated**: 4/4 scenarios passing
- **Documentation**: 5 new docs, 10+ updated

---

**Previous Version:** 0.5.0  
**Upgrade Path**: Direct upgrade, no migration needed (additive changes only)

---

**Branch**: `feature/core-m2.7/provenance-integration`  
**Merged to**: `main`  
**Tag**: `v0.6.0`
