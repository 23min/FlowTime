# SIM-CAT-M2 — Catalog.v1 (Structural Source of Truth)

**Status**: ✅ **COMPLETED** (September 3, 2025)  
**Goal**: Provide a domain-neutral catalog that both the simulator and UI can consume to render system diagrams and stamp component IDs into artifacts.

## Overview

SIM-CAT-M2 introduces a structural catalog format that defines the system topology independently from simulation scenarios. This enables:
- UI diagram rendering with elk/react-flow
- Consistent component ID mapping between catalog and Gold artifacts  
- Reusable system definitions across multiple scenarios

## Deliverables

### 1. Catalog.v1 Schema

YAML format defining system structure:

```yaml
version: 1
metadata:
  id: "demo-system"
  title: "Demo Processing System"
  description: "Simple two-component system for testing"
components:
  - id: COMP_A
    label: "Component A"
    description: "Entry point component"
  - id: COMP_B
    label: "Component B"  
    description: "Processing component"
connections:
  - from: COMP_A
    to: COMP_B
    label: "Primary flow"
classes: 
  - "DEFAULT"
layoutHints:
  rankDir: LR
  spacing: 100
```

### 2. API Extensions

Extend existing SIM-SVC-M2 service with catalog management:

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/sim/catalogs` | List available catalogs (id, title, hash) |
| GET | `/sim/catalogs/{id}` | Retrieve specific catalog |
| POST | `/sim/catalogs/validate` | Validate catalog schema + integrity |

### 3. Enhanced Run Endpoint

Update `POST /sim/run` to accept catalog references:

```json
{
  "catalogId": "demo-system",
  "scenario": { /* scenario spec */ },
  "seed": 12345
}
```

OR inline catalog:

```json
{
  "catalog": { /* Catalog.v1 inline */ },
  "scenario": { /* scenario spec */ },
  "seed": 12345  
}
```

### 4. Component ID Mapping

**Critical Requirement**: `components[].id` MUST match Gold `component_id` exactly
- Case-sensitive matching
- Stable, trimmed IDs
- Deterministic normalization if applied

## Acceptance Criteria

- [x] **Schema Validation**: Catalog.v1 schema enforced
- [x] **ID Consistency**: Each `component.id` maps 1:1 to Gold `component_id`
- [x] **UI Compatibility**: elk/react-flow can render structure from catalog
- [x] **Deterministic Layout**: Same catalog + hints → consistent diagram layout
- [x] **API Parity**: CLI and service produce identical artifacts when using catalogs
- [x] **Validation**: Schema and referential integrity checks pass

## Implementation Plan

### Phase 1: Core Schema & Types
1. Define `Catalog.v1` C# types in `FlowTime.Sim.Core`
2. Add JSON schema for validation
3. Create catalog reader/writer utilities

### Phase 2: Service Integration  
1. Add catalog endpoints to `FlowTime.Sim.Service`
2. Extend `/sim/run` endpoint for catalog support
3. Update scenario processing to use catalog component IDs

### Phase 3: CLI Integration
1. Add catalog support to CLI simulation mode
2. Ensure CLI/API parity for catalog-based runs
3. Add validation commands

### Phase 4: Testing & Documentation
1. Component ID mapping tests
2. Schema validation tests  
3. CLI/API parity tests with catalogs
4. Update documentation and examples

## Files to Create/Modify

### New Files
- `src/FlowTime.Sim.Core/Catalog.cs` - Core types
- `schemas/catalog.schema.json` - JSON schema
- `catalogs/demo-system.yaml` - Example catalog
- `tests/FlowTime.Sim.Tests/CatalogTests.cs` - Core tests
- `tests/FlowTime.Sim.Tests/ServiceCatalogTests.cs` - API tests

### Modified Files  
- `src/FlowTime.Sim.Service/Program.cs` - Add catalog endpoints
- `src/FlowTime.Sim.Core/SimulationSpec.cs` - Add catalog reference support
- `src/FlowTime.Sim.Cli/Program.cs` - Add catalog CLI support

## Dependencies

- **Completed**: SIM-M2 (artifact format), SIM-SVC-M2 (service foundation)
- **Enables**: UI diagram rendering, consistent component references
- **Blocks**: Advanced scenario management, multi-system support

## Risk Mitigation

- **ID Normalization**: Document exact normalization rules early
- **Schema Evolution**: Use `version` field for future schema changes  
- **Backward Compatibility**: Ensure non-catalog scenarios continue working
- **Performance**: Cache catalog validation results for repeated use
