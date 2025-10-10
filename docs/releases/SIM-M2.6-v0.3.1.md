# FlowTime-Sim M2.6-v0.3.1 Release Notes

**Release Date**: September 24, 2025  
**Version**: 0.3.1  
**Milestone**: SIM-M2.6 foundational work  
**Charter Context**: Metadata-driven template system for charter-aligned model authoring  
**Type**: Minor Release (architectural improvement)  
**Git Tag**: `v0.3.1`

## Overview

This release implements foundational infrastructure for the SIM-M2.6 charter-aligned model authoring platform. It transforms FlowTime-Sim from a hardcoded parameter system to a proper metadata-driven template platform, preparing for the charter-compliant model artifact system.

## SIM-M2.6 Milestone Context

This release provides **preparatory infrastructure** for the full SIM-M2.6 milestone: "Charter-Aligned Model Authoring Platform". Key connections:

- **Template Metadata Foundation**: The metadata-driven parameter system implemented here will support SIM-M2.6's charter-compliant model artifact creation
- **Dynamic Parameter Discovery**: The `/v1/sim/templates` API endpoints establish the foundation for model authoring workflows
- **Charter Preparation**: This system moves away from hardcoded parameters toward the flexible, template-driven approach required for charter-compliant model authoring
- **Engine Integration Ready**: Template metadata provides the structure needed for generating Engine-compatible model artifacts

See [SIM-M2.6 milestone documentation](../milestones/SIM-M2.6.md) for the complete charter-aligned model authoring vision.

## 🚀 What's New

### Metadata-Driven Parameter System
- **Template-Specific Parameters**: Each template now defines its own parameters via `metadata.parameters` YAML sections
- **Rich Parameter Types**: Support for `integer`, `number`, and `numberArray` parameter types
- **Comprehensive Validation**: Parameter definitions include titles, descriptions, defaults, minimum/maximum values
- **Better UX**: Templates provide contextually appropriate parameters instead of generic hardcoded ones

### Enhanced Template Library
Four comprehensive templates with metadata-defined parameters:

- **Transportation Network** (`transportation-basic.yaml`)
  - Passenger demand patterns, vehicle capacity patterns
  - Time period configuration with appropriate ranges

- **Manufacturing Production Line** (`manufacturing-line.yaml`)  
  - Raw material schedules, assembly capacity, quality control
  - Production rates, defect rates with manufacturing-specific ranges

- **IT System with Microservices** (`it-system-microservices.yaml`)
  - Request patterns, load balancer capacity, auth service capacity
  - Database capacity with IT system appropriate ranges

- **Multi-Tier Supply Chain** (`supply-chain-multi-tier.yaml`)
  - Demand patterns, supplier/distributor/retailer capacities
  - Buffer sizes with supply chain specific parameters

## 🔧 Technical Improvements

### Architecture
- **Fixed Design Flaw**: Replaced inappropriate `CreateParameterDefinition` hardcoded approach
- **YamlDotNet Integration**: Robust YAML metadata parsing with comprehensive error handling
- **Backward Compatibility**: Templates without metadata still work via fallback placeholder extraction
- **Type Safety**: Proper parameter type conversion and validation

### API Enhancements
- **Enhanced Template Endpoint**: `/v1/sim/templates` now returns rich parameter metadata
- **Improved Generation**: `/v1/sim/templates/{id}/generate` uses template-specific parameter definitions
- **Parameter Validation**: Runtime validation of parameter types and ranges

## 🛠️ Breaking Changes

**None** - This release maintains full backward compatibility.

## 🧪 Testing

- **88 Tests Passing**: All existing functionality validated
- **New Template Coverage**: Comprehensive testing of metadata-driven parameter system
- **API Compatibility**: All existing API contracts maintained

## 📋 Milestone Context

This release supports **SIM-M2.6-CHARTER** milestone goals around "Parameterized Template System Integration" by providing the foundation for:
- Template-specific parameter forms in the UI
- Proper model previews with user-configured parameters  
- Context-appropriate parameter validation and UX

## 🔄 Migration Guide

**No migration required** - existing templates and API usage continue to work unchanged.

For new templates, consider adding `metadata.parameters` sections to provide better user experience:

```yaml
metadata:
  title: 'Your Template Name'
  description: 'Template description'
  templateId: 'your-template-id'
  parameters:
    - name: bins
      type: integer
      title: "Time Periods"
      description: "Number of time periods to simulate"
      defaultValue: 12
      minimum: 3
      maximum: 48
```

## 🎯 What's Next

- **UI Integration**: Frontend consumption of metadata-driven parameters
- **SIM-M2.6 Completion**: Charter-compliant model authoring platform
- **Enhanced Validation**: More sophisticated parameter validation rules

---

**Contributors**: GitHub Copilot  
**Commit**: `10842df`  
**Previous Version**: v0.3.0