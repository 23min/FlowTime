# FlowTime Engine Roadmap

**Status:** Active  
**Last Updated:** October 4, 2025  
**Purpose:** Clear roadmap aligned with KISS architecture, recent milestones, and actual system capabilities

---

## Executive Summary

FlowTime Engine is a **deterministic execution and telemetry generation platform** that evaluates flow models on canonical time grids. It's part of a three-component ecosystem:

- **FlowTime-Sim**: Model authoring and template generation (generates models with provenance)
- **FlowTime Engine**: Model execution and artifact generation (executes models, stores artifacts permanently)
- **FlowTime UI**: Orchestration and visualization (coordinates Sim ↔ Engine workflows, visualizes results)

**Key Architecture Principle (KISS)**: 
- **Engine owns the single artifacts registry** (models + runs + telemetry)
- **Sim provides temporary model storage** for UI workflows
- **UI orchestrates everything** - Sim and Engine do NOT communicate directly

---

## Current State

### ✅ Completed Capabilities

| Milestone | Status | Key Achievements |
|-----------|--------|------------------|
| **M0-M2.0** | ✅ Complete | Core engine, expression evaluation, basic UI |
| **M2.6** | ✅ Complete (v0.6.0) | Export system (CSV, NDJSON, Parquet) |
| **M2.7** | ✅ Complete (v0.6.0) | Artifacts registry with file-based storage |
| **M2.8** | ✅ Complete | Enhanced registry APIs, query capabilities |
| **UI-M2.8 Phase 1** | ✅ Complete | Template API integration, Sim API migration |
| **SIM-M2.6.1** | ✅ Complete (v0.5.0) | Schema evolution (binSize/binUnit format, schemaVersion: 1) |
| **SIM-M2.7** | ✅ Complete (v0.6.0) | Provenance integration (metadata tracking, 132 passing tests) |
| **M2.9** | ✅ Complete | Schema evolution & provenance (binSize/binUnit, PMF compilation, PCG32 RNG) |

### 🔄 In Progress

| Milestone | Status | Current Phase |
|-----------|--------|---------------|
| **UI-M2.9** | 🔄 In Progress | UI schema migration for binSize/binUnit format |

### 📋 Upcoming

| Milestone | Status | Focus |
|-----------|--------|-------|
| **M2.10** | ✅ Complete | Provenance query support (API & CLI filters) |
| **M3.0** | 📋 Next | Backlog & Latency Modeling (single-queue, Little's Law) |

---

## Architecture Context

### KISS Principle: Single Registry

**Who Owns What:**

```
FlowTime-Sim (/workspaces/flowtime-sim-vnext)
├── Generates models from templates
├── Creates provenance metadata (template_id, parameters, model_id)
├── Stores models TEMPORARILY in /data/models/
└── Exposes GET /api/v1/models/{id} for UI retrieval

FlowTime UI (:5219)
├── ORCHESTRATES everything (Sim ↔ Engine)
├── Gets model + provenance from Sim
├── Posts model + provenance to Engine
└── Queries Engine for results

FlowTime Engine (/workspaces/flowtime-vnext)
├── SINGLE SOURCE OF TRUTH for all artifacts
├── Executes models deterministically
├── Stores PERMANENTLY: models + runs + telemetry
├── Registry: /data/registry-index.json
└── Artifacts: /data/{run_*|models/*}/
```

**No Direct Communication**: Sim and Engine do NOT talk directly. UI coordinates all workflows.

### Communication Flow

```mermaid
sequenceDiagram
    participant UI as FlowTime UI
    participant Sim as FlowTime-Sim
    participant Engine as FlowTime Engine
    
    Note over Sim,Engine: NO DIRECT CONNECTION
    
    UI->>Sim: POST /api/v1/templates/{id}/generate
    Sim-->>UI: Return model YAML + metadata
    
    UI->>Sim: GET /api/v1/models/{id}
    Sim-->>UI: Return {model, provenance}
    
    UI->>Engine: POST /v1/run<br/>X-Model-Provenance: {model_id}<br/>Body: model YAML
    Engine-->>UI: Return {runId, artifacts}
    
    UI->>Engine: GET /v1/artifacts
    Engine-->>UI: Return artifact list
```

---

## Milestone Details

### M2.9: Schema Evolution & Provenance (✅ Complete)

**Goal**: Complete schema evolution and provenance tracking for full model traceability.

**Completion Date:** October 2025

#### Status: All Phases Complete ✅

**Completed Work:**
- ✅ 158 comprehensive tests created (4,148 lines of test code)
- ✅ TDD setup with RED state confirmed (tests compile, fail as expected)
- ✅ Schema documentation updated (target-model-schema.yaml/md)
- ✅ Deprecation notices on legacy schemas
- ✅ Architecture documents aligned with KISS

**Test Coverage:**
- TimeGrid evolution (binSize/binUnit): 18 tests
- Schema validation: 33 tests  
- PMF compilation pipeline: 34 tests
- RNG/PCG32 algorithm: 33 tests
- API provenance support: 40 tests

#### Phase 1: Schema Documentation ✅ Complete

**Completed:**
- ✅ target-model-schema.yaml with provenance field
- ✅ target-model-schema.md with provenance documentation
- ✅ manifest.schema.json with provenance reference
- ✅ Deprecated legacy schemas (engine-input-schema.*, sim-model-output-schema.*)
- ✅ README.md updated to reference unified schema

#### Phase 2: Engine Implementation (✅ Complete)

**Completed Implementation:**
1. **TimeGrid Evolution** - Add TimeUnit enum, binSize/binUnit constructor
2. **Model Parser** - Parse new schema format, validate schemaVersion
3. **PMF Compilation** - 4-phase pipeline (validation, grid alignment, compilation, provenance)
4. **RNG/PCG32** - Deterministic random number generator (see [`docs/architecture/rng-algorithm.md`](architecture/rng-algorithm.md))
5. **Provenance Support** - Parse header/embedded provenance, store provenance.json
6. **Hash Calculation** - Exclude provenance from model_hash

**Expected Outcome**: All 158 tests pass (GREEN state)

#### Phase 3: FlowTime-Sim Alignment (✅ Complete)

**Completed Coordination:**
- ✅ Sim updated to output target-model-schema format directly (SIM-M2.6.1)
- ✅ Provenance integration in Sim completed (SIM-M2.7)
- ✅ Engine accepts and stores provenance metadata
- ✅ Integration tests validated (132 passing tests in Sim, 221/224 in Engine)

#### Phase 4: Validation & Documentation (✅ Complete)

**Completed:**
- ✅ Integration testing between Sim and Engine (4 integration tests passing)
- ✅ Schema documentation updated (target-model-schema.md)
- ✅ Examples updated to new format
- ✅ Migration guides provided in milestone docs

---

### M2.10: Provenance Query Support (✅ Complete)

**Goal**: Enable efficient querying of artifacts by provenance metadata through API and CLI.

**Status**: Complete

**Key Features:**
- API query parameters: `?source=`, `?templateId=`, `?modelId=`
- Convenience endpoint: `GET /v1/artifacts/{id}/provenance`
- CLI commands: `flowtime artifacts list --template-id <id>`
- CLI command: `flowtime artifacts provenance <runId>`

**Use Cases Enabled:**
- "Show all runs from template X"
- "Find runs with specific model for comparison"
- "List all Sim-generated runs"
- Quick provenance inspection via CLI

**Why Now:**
- M2.9 completed provenance storage and indexing
- Data is already indexed in registry
- Small scope (2-4 hours) enables quick delivery
- Unblocks future UI compare workflows

**Scope:**
- ✅ API provenance query parameters
- ✅ API provenance retrieval endpoint
- ✅ CLI provenance query commands
- ✅ Performance optimization (<200ms for 1000+ artifacts)
- ✅ TDD approach with comprehensive tests
- ❌ Comparison workflows (separate milestone)
- ❌ Advanced analytics
- ❌ UI integration

See [`docs/milestones/M2.10.md`](milestones/M2.10.md) for detailed requirements.

---

## Future Milestones

### Advanced Engine Capabilities

**M3.0: Backlog & Latency Modeling**
- Single-queue backlog tracking
- Little's Law latency calculations
- Service time distribution modeling

**M3.1: Scenarios & Parameter Sweeps**
- Multi-scenario execution
- Parameter sweep workflows
- Statistical comparison

### Cross-Platform Integration

**UI-M3.0: Cross-Platform Charter**
- Unified UI spanning Engine and Sim
- Embedded model authoring
- Cross-platform workflows

**SIM-M3.0: Charter Alignment**
- Model artifacts integration
- Seamless Engine ↔ Sim workflows

### Advanced Capabilities (Future)

**M4.0+: Advanced Modeling**
- Routing & network modeling
- Multi-queue systems
- Batch processing & temporal windows

**M5.0+: Enterprise Features**
- Real-time data integration
- Machine learning integration
- Cloud-native deployment

---

## Technology Stack

### Backend (.NET 9)
- **API Style**: Minimal APIs (`.MapPost()`, `.MapGet()`) in Program.cs
- **No Controllers**: Direct route handlers with dependency injection
- **Storage**: File-based artifacts (/data/ directory)
- **Registry**: JSON index (registry-index.json)

### Frontend (Blazor Server)
- **UI Framework**: Blazor Server with MudBlazor
- **Architecture**: Orchestrator pattern (UI coordinates Sim ↔ Engine)
- **API Integration**: HTTP client calls to both services

### Testing Strategy
- **Core Tests**: No web dependencies, deterministic, fast
- **API Tests**: WebApplicationFactory<Program> for Minimal APIs
- **Integration Tests**: Full workflow tests (UI → Sim → Engine)
- **TDD Approach**: Tests first (RED), then implementation (GREEN)

---

## Development Principles

### 1. KISS Architecture
- Single registry (Engine owns it)
- No direct service-to-service communication
- UI orchestrates workflows
- Temporary vs permanent storage clearly separated

### 2. Determinism
- Same model + seed → same results
- Reproducible artifacts
- Exact numeric assertions in tests

### 3. Test-Driven Development
- Comprehensive tests before implementation
- RED → GREEN → REFACTOR cycle
- No time estimates in documentation

### 4. Schema Evolution
- Versioned schemas (schemaVersion: 1)
- Additive changes preferred
- Clear deprecation strategy

### 5. Provenance Tracking
- Complete model lineage (template → model → run)
- Metadata in artifacts
- Permanent storage in Engine registry

---

## Success Metrics

### Technical Metrics
- **Registry Performance**: <200ms queries for 10K+ artifacts
- **Determinism**: 100% reproducible runs
- **Test Coverage**: >90% for core execution engine
- **API Reliability**: 99%+ success rate

### User Experience Metrics
- **Workflow Continuity**: Zero regression in existing features
- **Artifact Discoverability**: All runs/models findable in registry
- **Cross-Platform Integration**: Seamless Sim → Engine workflows

---

## Archived Documents

For historical context, see `docs/archive/`:
- **ROADMAP-2025-01.md** - January 2025 charter-focused roadmap (outdated status)
- **ROADMAP-LEGACY.md** - Pre-charter milestone structure
- **CHARTER-ROADMAP.md** - Charter implementation sequence (superseded by this roadmap)

### Superseded Schema Documents

The following schema documents are deprecated:
- **engine-input-schema.md** - Use `target-model-schema.md` instead
- **engine-input.schema.json** - Use `target-model-schema.yaml` instead
- **sim-model-output-schema.md** - Unified to `target-model-schema.md`

---

## Getting Started

### For Developers
1. Review this ROADMAP.md for current direction
2. Check M2.9 milestone document for detailed implementation plan
3. See tests in tests/FlowTime.Tests/ and tests/FlowTime.Api.Tests/Provenance/
4. Follow TDD: tests exist, make them pass
5. Use Minimal APIs pattern (not controllers)

### For Stakeholders
1. **Current Focus**: M2.10 complete - provenance query support
2. **Architecture**: KISS principle with single registry
3. **Next Milestone**: M3.0 (Backlog & Latency Modeling) - focus on modeling capabilities
4. **Integration**: Sim ↔ UI ↔ Engine coordination via UI orchestration
5. **Integration**: Sim ↔ UI ↔ Engine coordination via UI orchestration

### For Users
1. Continue using existing workflows (no disruption)
2. ✅ New provenance features available (M2.9 complete)
3. Backlog & latency modeling coming in M3.0 (next focus)
4. Cross-platform integration (future)

---

**Next Actions:**
1. ✅ Review and discuss ROADMAP.md
2. ✅ Update/retire outdated documents (archived)
3. ✅ Complete M2.9 implementation (all phases)
4. ✅ Implement M2.10 provenance queries
5. 📋 Plan M3.0 backlog & latency modeling

---

**Roadmap Status:** ✅ Active  
**Current Milestone:** M2.10 ✅ Complete, M3.0 📋 Next  
**FlowTime-Sim Status:** SIM-M2.7 ✅ Complete (v0.6.0), ready for v1.0.0 stable release  
**Architecture:** KISS - Single Registry in Engine
