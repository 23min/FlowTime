# FlowTime-Sim Roadmap (Clean Slate)

**Current Version:** v0.6.0 (Released 2025-10-03)  
**Active Branch:** `feature/core-m2.7/provenance-integration` (ready for merge)  
**Next Version:** v1.0.0 (stable release)  
**Last Updated:** October 3, 2025

---

## Current State (v0.6.0)

### ✅ What's Working
- **SIM-M2.6-CORRECTIVE**: Node-based template system with proper schema foundation
- **SIM-M2.6.1**: Schema convergence (binSize/binUnit format, schemaVersion field)
- **SIM-M2.7**: Provenance integration (metadata tracking, embedded provenance)
- **Template Generation**: `/api/v1/templates/{id}/generate` creates Engine-compatible models
- **Provenance Tracking**: Model identity and lineage from template → run
- **PMF Support**: Probability mass functions for stochastic patterns
- **CLI**: Charter-compliant `flowtime-sim` tool with provenance flags
- **Hash Storage**: Deterministic model storage in `/data/models/{templateId}/{hashPrefix}/`
- **Content Negotiation**: Both YAML and JSON formats supported
- **132 Passing Tests**: 128 unit tests + 4 integration tests (Sim ↔ Engine validated)

### 📦 What We Ship
FlowTime-Sim is a **model authoring platform** that generates Engine-compatible model artifacts:
- YAML model files (template → model with parameters resolved)
- Metadata files (provenance tracking)
- Template library (reusable model patterns)
- Validation tooling (structural validation before Engine execution)

### 🚫 What We Don't Do
- **NO telemetry generation**: Engine computes results, not Sim
- **NO model execution**: Engine evaluates expressions and runs computations
- **NO run management**: Engine handles run lifecycle, Sim handles templates

---

## Completed Milestones

### ✅ SIM-M2.6.1 - Schema Evolution (v0.4.0 → v0.5.0)
**Completed**: October 3, 2025  
**Breaking Change**: Removed `binMinutes`, adopted Engine `binSize`/`binUnit` format

**Delivered**:
- Removed conversion layer (direct Engine format generation)
- Added `schemaVersion: 1` to all models
- Updated all tests and fixtures
- Engine M2.9 validated (accepts new format)

### ✅ SIM-M2.7 - Provenance Integration (v0.5.0 → v0.6.0)
**Completed**: October 3, 2025  
**Branch**: `feature/core-m2.7/provenance-integration`  
**Minor Feature**: Added complete provenance traceability

**Delivered**:
- ProvenanceService with model ID generation (timestamp + hash)
- Template metadata capture (id, version, title, parameters)
- API enhancement: `/generate` returns provenance, `?embed_provenance=true` support
- CLI flags: `--provenance <file>` and `--embed-provenance`
- Two delivery methods: X-Model-Provenance header OR embedded in YAML
- Engine integration validated: 4/4 integration tests passing
- Total test coverage: 128 unit tests + 4 integration tests

**Integration Validated**:
- ✅ Sim generates models with provenance metadata
- ✅ Engine accepts X-Model-Provenance header
- ✅ Engine stores provenance.json in run artifacts
- ✅ Old schema (arrivals/route) correctly rejected
- ✅ All Engine response fields validated
- ✅ End-to-end Sim → Engine workflow operational

**Test Results**:
- Basic workflow (generation → execution): PASS
- Provenance storage: PASS
- Optional provenance (backward compatibility): PASS
- Old schema rejection: PASS

---

## Active Work

### 🎯 Next Milestone: v1.0.0 Stable Release

**Goal**: Production-ready stable release with API contracts and backward compatibility commitments

**Remaining Tasks**:
- Merge `feature/core-m2.7/provenance-integration` to main
- API contract documentation and stability review
- Backward compatibility policy definition
- Production-ready defaults validation
- Complete user documentation
- Release preparation

**Timeline**: 1-2 weeks after provenance merge

---

## Milestone Sequence

### Phase 1: Foundation & Integration ✅ COMPLETE
```
v0.4.0 (SIM-M2.6-CORRECTIVE) ✅ Released Sep 24, 2025
  ↓
v0.5.0 (SIM-M2.6.1 Schema Evolution) ✅ Completed Oct 3, 2025
  ↓
v0.6.0 (SIM-M2.7 Provenance) ✅ Completed Oct 3, 2025
  ↓
[ready for merge to main]
  ↓
v0.5.0 (SIM-M2.6.1) ⏸️ (blocked on Engine M2.9)
  ↓
v0.6.0 (SIM-M2.7) ⏸️ (blocked on Engine M2.9)
```

### Phase 2: Stable Release (Future)
```
v0.6.0 (SIM-M2.7) 
  ↓
v1.0.0 (Stable API Contracts) 📋
```

---

## Version Strategy

### Development Phase (0.x.x)
- **0.x.0**: Milestone completions (breaking changes acceptable)
- **0.x.y**: Bug fixes and improvements within milestone
- **Focus**: Capability delivery over backward compatibility

### Upcoming Versions
- **v0.5.0**: SIM-M2.6.1 (schema evolution, breaking change)
- **v0.6.0**: SIM-M2.7 (provenance, minor feature)
- **v1.0.0**: First stable release with API stability commitments

### Production Phase (1.x.x+)
- **1.0.0**: First stable release
- **1.x.0**: New capabilities with backward compatibility
- **x.0.0**: Breaking changes for architecture evolution (rare)

---

## Completed Milestones

### ✅ SIM-M0 - Core Foundations (Pre-v0.1.0)
- Canonical grid, Series<T>, deterministic evaluation
- Basic synthetic data generation
- Cycle detection, unit tests

### ✅ SIM-M1 - Contracts Parity (Pre-v0.1.0)
- Dual-write artifacts (run.json, manifest.json, series/index.json)
- JSON schema validation
- Deterministic hashing

### ✅ SIM-M2 - Artifact Structure (v0.2.0)
- Standardized artifact structure
- SHA-256 integrity checks
- Per-series CSV files

### ✅ SIM-CAT-M2 - Catalog Required (v0.2.1)
- Catalog.v1 requirement
- Stable component IDs
- API endpoints and validation

### ✅ SIM-SVC-M2 - Minimal Service (v0.3.0)
- HTTP service with `/api/v1` endpoints
- Template generation endpoints
- Content negotiation (YAML/JSON)

### ✅ SIM-M2.1 - PMF Support (v0.3.0)
- PMF (Probability Mass Function) generators
- Stochastic arrival patterns
- 88 passing tests

### ✅ SIM-M2.6-CORRECTIVE - Node Schema Foundation (v0.4.0)
- Node-based template system
- Charter-compliant CLI (verb+noun pattern)
- Hash-based model storage
- Template library with 5 domain examples
- Comprehensive test coverage

---

## Future Milestones (Post-Engine M2.9)

### 🚀 Stable API Release (v1.0.0)
**Goal**: First stable release with API contract guarantees

**Features**:
- API versioning and deprecation policy
- Backward compatibility commitments
- Complete documentation
- Production-ready defaults

**Dependencies**:
- SIM-M2.8 complete
- All breaking changes resolved
- Integration tests passing

**Effort**: 1-2 weeks (mostly stabilization)

### 🚀 Post-v1.0.0: Incremental Feature Additions

After v1.0.0 stable release, add features **only as needed** based on actual usage:

**Possible v1.1.0+ Features:**
- Enhanced validation (if users struggle with model errors)
- Additional template domains (supply chain, healthcare, network)
- Template versioning (if template evolution becomes complex)
- Model comparison helpers (if users need comparison support)
- Performance optimizations (if generation becomes slow)
- API enhancements (based on user feedback)

**Philosophy**: Ship stable foundation first, then add features incrementally based on real needs, not speculation. Prioritize by actual user demand.

---

## Decision Log

### Why Schema Convergence First?
**Problem**: Sim used `binMinutes`, Engine uses `binSize`/`binUnit`. Conversion layer was brittle.  
**Decision**: Remove conversion, adopt Engine format directly.  
**Impact**: Breaking change (v0.4.0 → v0.5.0), but cleaner long-term.

### Why Provenance Before Registry?
**Problem**: Models need identity and tracking before registry makes sense.  
**Decision**: Add provenance (M2.7) before registry integration (M2.8).  
**Impact**: Clear model lineage from creation through execution.

### Why Block on Engine M2.9?
**Problem**: Can't validate schema changes without Engine accepting them.  
**Decision**: Verify Engine readiness before implementing Sim changes.  
**Impact**: Prevents wasted work and ensures end-to-end compatibility.

### Why KISS Architecture?
**Problem**: Original charter planning was overengineered with dual-registry, sync, health monitoring, etc.  
**Decision**: Single registry (Engine), temporary storage (Sim), UI orchestrates. Add features only as needed.  
**Impact**: ~85% less complexity. Simple, clear, maintainable.

### Why Remove SIM-M2.8 and SIM-M2.9?
**Problem**: Overengineered charter milestones that contradict KISS architecture.  
**Decision**: After SIM-M2.7, the workflow is complete. Go straight to v1.0.0 stable release.  
**Impact**: Focus on stability and templates instead of unnecessary complexity.

---

## Success Criteria

### SIM-M2.6.1 Success
- ✅ Engine accepts models without conversion
- ✅ All tests pass with new format
- ✅ No `binMinutes` references remain
- ✅ `schemaVersion: 1` in all models

### SIM-M2.7 Success ✅ ACHIEVED
- ✅ Models have unique IDs (timestamp + content hash)
- ✅ Provenance tracks creation metadata (template, parameters, generator)
- ✅ CLI supports both delivery methods (`--provenance`, `--embed-provenance`)
- ✅ API supports both methods (header response, `?embed_provenance=true`)
- ✅ Engine accepts and stores provenance (validated via integration tests)
- ✅ Complete end-to-end workflow operational (4/4 integration tests passing)

### v1.0.0 Success
- ✅ API contracts stable and documented
- ✅ Backward compatibility policy defined
- ✅ All tests passing with good coverage
- ✅ Production-ready defaults
- ✅ Complete user documentation

---

## What Changed from Old Roadmap?

### Removed Confusion
- ❌ **No more "charter violation" language** - Sim is evolving, not broken
- ❌ **No more "two-phase strategy"** - Just clear milestone sequence
- ❌ **No more "Engine M2.7/M2.8 as current priority"** - Focus on Sim milestones
- ❌ **No more SIM-M2.8/M2.9** - Overengineered charter milestones removed

### Added Clarity
- ✅ **Actual current state**: v0.4.0 released, schema-convergence in progress
- ✅ **Clear blocking dependency**: Engine M2.9 verification needed
- ✅ **Specific version progression**: v0.4.0 → v0.5.0 → v0.6.0 → v1.0.0
- ✅ **KISS architecture**: Sim generates, Engine stores, UI orchestrates

### Simplified Structure
- ✅ **One roadmap** (not three overlapping docs)
- ✅ **Linear milestone sequence** (not parallel tracks)
- ✅ **Clear status indicators**: ✅ Done, 📝 Docs Ready, ⏸️ Blocked, 🚀 Future
- ✅ **Focus on essentials**: Only implement what's needed, add features incrementally

---

## Next Actions (Priority Order)

1. **Review NEW-ROADMAP.md** with team - Get feedback on clarity
2. **Verify Engine M2.9 Status** - Check if Engine supports new schema
3. **Merge schema-convergence** - Get documentation into main branch
4. **Start SIM-M2.6.1 Implementation** - If Engine ready, begin schema evolution
5. **Update Old Docs** - Deprecate or consolidate ROADMAP.md, DEVELOPMENT-STATUS.md, CHARTER-TRANSITION-PLAN.md

---

## Questions This Roadmap Answers

✅ **Where are we now?** v0.4.0 with solid foundation, schema-convergence docs ready  
✅ **What's blocking us?** Engine M2.9 verification needed  
✅ **What's next?** SIM-M2.6.1 (schema evolution) then SIM-M2.7 (provenance)  
✅ **How long will it take?** 1-2 days for M2.6.1, 1-2 weeks for M2.7  
✅ **When do we hit v1.0?** After SIM-M3.0 (stable API milestone)  
✅ **How do we get there?** Clear milestone sequence with dependency checks

---

## Document Status

**Status**: 🆕 DRAFT FOR DISCUSSION  
**Replaces**: ROADMAP.md, DEVELOPMENT-STATUS.md, CHARTER-TRANSITION-PLAN.md (eventually)  
**Feedback Needed**: Clarity, completeness, actionability  
**Next Update**: After team review and Engine M2.9 verification
