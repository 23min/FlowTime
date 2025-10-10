# SIM-CAT-M2 Release — Catalog.v1 (Structural Source of Truth)

**Release Date**: September 3, 2025  
**Milestone**: SIM-CAT-M2  
**Status**: ✅ **COMPLETED**

---

## Overview

SIM-CAT-M2 introduces **Catalog.v1**, a domain-neutral structural format that defines system topology independently from simulation scenarios. This enables consistent component ID mapping between catalog definitions and simulation artifacts, while providing a foundation for UI diagram rendering.

## Key Features

### 1. Catalog.v1 Core Schema

New YAML format defining system structure:

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

### 2. Service API Extensions

Enhanced FlowTime-Sim service with catalog management endpoints:

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/sim/catalogs` | List available catalogs (id, title, hash) |
| GET | `/sim/catalogs/{id}` | Retrieve specific catalog |
| POST | `/sim/catalogs/validate` | Validate catalog schema + integrity |

### 3. Enhanced Run Endpoint

Updated `POST /sim/run` to accept catalog references:

```json
{
  "catalogId": "demo-system",
  "scenario": { /* scenario spec */ },
  "seed": 12345
}
```

Or inline catalog:

```json
{
  "catalog": { /* Catalog.v1 inline */ },
  "scenario": { /* scenario spec */ },
  "seed": 12345  
}
```

## Technical Implementation

### Core Components Added

- **`FlowTime.Sim.Core/Catalog.cs`** - Core Catalog.v1 types with validation
- **`FlowTime.Sim.Core/CatalogIO.cs`** - YAML serialization/deserialization
- **Service catalog endpoints** - Complete API integration
- **Comprehensive test coverage** - 24 catalog-specific tests

### Key Features

- **Component ID Consistency**: `components[].id` maps 1:1 to Gold `component_id`
- **Schema Validation**: JSON schema enforcement with detailed error messages
- **Referential Integrity**: Connection validation ensures referenced components exist
- **YAML Support**: Full round-trip YAML serialization with YamlDotNet
- **Service Integration**: RESTful API with consistent error handling

## Testing Coverage

- **67 total tests** passing (increased from 43 in SIM-SVC-M2)
- **24 catalog-specific tests** covering:
  - Core validation logic
  - YAML serialization/deserialization
  - Service API endpoints
  - Component ID consistency
  - Schema compliance

## Acceptance Criteria Met

- ✅ **Schema Validation**: Catalog.v1 schema enforced with comprehensive validation
- ✅ **ID Consistency**: Each `component.id` maps 1:1 to Gold `component_id`
- ✅ **UI Compatibility**: Structure format compatible with elk/react-flow rendering
- ✅ **Deterministic Layout**: Same catalog + hints produce consistent diagrams
- ✅ **API Parity**: CLI and service produce identical artifacts when using catalogs
- ✅ **Validation**: Schema and referential integrity checks implemented and tested

## Files Added/Modified

### New Files
- `src/FlowTime.Sim.Core/Catalog.cs` - Core Catalog.v1 types
- `src/FlowTime.Sim.Core/CatalogIO.cs` - YAML I/O utilities
- `catalogs/demo-system.yaml` - Example catalog
- `catalogs/tiny-demo.yaml` - Minimal example
- `tests/FlowTime.Sim.Tests/CatalogTests.cs` - Core validation tests
- `tests/FlowTime.Sim.Tests/CatalogIOTests.cs` - Serialization tests

### Modified Files  
- `src/FlowTime.Sim.Service/Program.cs` - Added catalog endpoints
- `tests/FlowTime.Sim.Tests/ServiceIntegrationTests.cs` - API integration tests
- `docs/ROADMAP.md` - Marked SIM-CAT-M2 as complete

## API Usage Examples

### List Available Catalogs
```bash
curl http://localhost:8080/sim/catalogs
```

### Get Specific Catalog
```bash
curl http://localhost:8080/sim/catalogs/demo-system
```

### Validate Catalog
```bash
curl -X POST http://localhost:8080/sim/catalogs/validate \
  -H "Content-Type: text/plain" \
  -d @catalog.yaml
```

### Run Simulation with Catalog
```bash
curl -X POST http://localhost:8080/sim/run \
  -H "Content-Type: application/json" \
  -d '{
    "catalogId": "demo-system",
    "scenario": { "duration": { "hours": 24 }},
    "seed": 42
  }'
```

## Future Enhancements

This release enables:
- **UI diagram rendering** with elk/react-flow integration
- **Template-based simulations** (UI-M1) using catalog structure
- **Multi-system support** with catalog-based component management
- **Advanced scenario management** with consistent component references

## Breaking Changes

None. This is a purely additive release that maintains full backward compatibility with existing simulation scenarios.

## Upgrade Notes

- Existing simulation workflows continue to work unchanged
- New catalog features are optional and can be adopted incrementally
- Example catalogs provided in `catalogs/` directory for reference

---

**Contributors**: Development Team  
**Commit**: `54b8f13` - feat(catalog): complete SIM-CAT-M2 catalog.v1 with service integration
