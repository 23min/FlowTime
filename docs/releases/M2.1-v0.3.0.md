# FlowTime-Sim Release M2.1-v0.3.0

**Release Date**: September 11, 2025  
**Milestone**: SIM-M2.1 - Probability Mass Function (PMF) Generator Support  
**Version**: 0.3.0  

## Overview

This release implements comprehensive support for Probability Mass Function (PMF) arrival generation in FlowTime-Sim, completing the SIM-M2.1 milestone. The system now supports discrete probability distributions for arrival patterns, providing more sophisticated modeling capabilities beyond constant rates and Poisson distributions.

## Major Features

### PMF Arrival Generation
- **Core Implementation**: Complete PMF generator in `SimulationSpec.cs` with discrete probability distribution support
- **Validation**: Robust PMF validation ensuring probabilities sum to 1.0 and all values are non-negative
- **Generation**: Efficient PMF sampling using cumulative distribution function (CDF) approach
- **Schema Integration**: Full YAML schema support for PMF arrival patterns

### Enhanced CLI and Configuration
- **Environment Variable Support**: 
  - `FLOWTIME_API_BASEURL` for flexible FlowTime API endpoint configuration
  - `FLOWTIME_API_VERSION` for API version selection
  - Proper precedence: CLI args > Environment variables > Hardcoded defaults
- **Argument Parsing**: Improved argument parsing with comprehensive test coverage
- **Help System**: Dynamic help messages showing current configuration

### Container Networking and API Integration
- **Cross-Container Communication**: Full support for Docker container networking using service names
- **API Versioning**: Consistent `/v1/` endpoint usage across FlowTime-Sim and FlowTime API
- **Schema Compatibility**: Enhanced FlowTime API integration with proper ModelDto schema support
- **Port Configuration**: Separated service ports (FlowTime API: 8080, FlowTime-Sim API: 8090)

### Testing and Quality Assurance
- **Comprehensive Test Suite**: 88 passing tests covering all PMF functionality
- **Integration Testing**: Cross-container integration validation scripts
- **Environment Isolation**: Tests properly handle environment variable isolation
- **Edge Case Coverage**: Extensive validation of PMF edge cases and error conditions

## Technical Implementation Details

### PMF Generator Architecture
```csharp
public class SimulationSpec
{
    // PMF probabilities for arrival generation
    public double[]? Probabilities { get; init; }
    
    // Core PMF validation and generation methods
    private void ValidatePmf()
    private List<ArrivalEvent> GeneratePmf(...)
    private int SampleFromPmf(double[] probabilities, double u)
}
```

### Environment Variable Configuration
- **FLOWTIME_API_BASEURL**: Default FlowTime API endpoint (e.g., `http://flowtime-api:8080`)
- **FLOWTIME_API_VERSION**: API version for endpoint construction (e.g., `v1`)
- **Precedence**: Command line arguments override environment variables override hardcoded defaults

### API Integration Enhancements
- **Schema Compatibility**: Added `SchemaVersion` and `RngDto` to FlowTime API `ModelDto`
- **Endpoint Consistency**: All services use `/v1/` API versioning
- **Error Handling**: Robust error handling for network connectivity and API compatibility

## Validation and Testing

### Test Coverage
- **Unit Tests**: 88 tests covering PMF generation, validation, and CLI functionality
- **Integration Tests**: Cross-container networking and API communication validation
- **Environment Tests**: Proper isolation and configuration of environment variables

### Performance Characteristics
- **PMF Generation**: Efficient O(n) sampling using cumulative distribution approach
- **Memory Usage**: Optimized memory allocation for large probability distributions
- **Container Startup**: Fast service initialization with proper health checks

## Breaking Changes

### Environment Variable Behavior
- **Previous**: Hardcoded `http://localhost:8080` default
- **Current**: Environment variable `FLOWTIME_API_BASEURL` takes precedence when set
- **Migration**: Set environment variables or use explicit CLI arguments for custom endpoints

### API Versioning
- **Previous**: Implicit API version handling
- **Current**: Explicit `/v1/` endpoint usage with configurable API version
- **Migration**: Ensure FlowTime API supports `/v1/` endpoints

## Configuration Examples

### Container Environment
```bash
# Environment variables for container deployment
FLOWTIME_API_BASEURL=http://flowtime-api:8080
FLOWTIME_API_VERSION=v1
```

### CLI Usage Examples
```bash
# Using environment defaults
flow-sim --model examples/m2.pmf.yaml --mode engine --out results/

# Explicit API configuration
flow-sim --model examples/m2.pmf.yaml --flowtime http://custom-api:8080 --api-version v1

# Simulation mode (no API required)
flow-sim --model examples/m2.pmf.yaml --mode sim --out results/
```

### PMF Model Example
```yaml
schemaVersion: "m2.1"
rng:
  kind: lcg
  seed: 42
arrivals:
  - nodeId: "COMP_A"
    generator: pmf
    probabilities: [0.1, 0.2, 0.3, 0.4]  # Discrete probability distribution
    parameters:
      binCount: 4
      binMinutes: 60
```

## Dependencies and Compatibility

### Runtime Requirements
- **.NET 9.0**: Latest .NET runtime with nullable reference types
- **Docker Network**: `flowtime-dev` network for container communication
- **FlowTime API**: Compatible with v1 API endpoints

### Development Requirements
- **VS Code**: Development container configuration with proper port forwarding
- **Docker**: Container networking and service orchestration
- **PowerShell**: Cross-platform shell support for development scripts

## Known Issues and Limitations

### Container Networking
- **Localhost Access**: Port forwarding may allow localhost access from containers (expected behavior)
- **Service Discovery**: Relies on Docker network service name resolution

### PMF Constraints
- **Probability Sum**: PMF probabilities must sum to exactly 1.0 (validated)
- **Non-negative Values**: All probability values must be >= 0.0
- **Array Size**: PMF array size determines the number of discrete outcomes

## Future Enhancements

### SIM-M3 Roadmap
- **Additional Generators**: Exponential, uniform, and custom distribution support
- **Advanced Validation**: Schema validation improvements and error reporting
- **Performance Optimization**: Large-scale simulation performance enhancements

### API Evolution
- **Model Management**: Server-side model storage and management
- **Batch Operations**: Multi-model simulation execution
- **Real-time Monitoring**: Live simulation progress and metrics

## Migration Guide

### From M1 to M2.1
1. **Update Environment**: Set `FLOWTIME_API_BASEURL` and `FLOWTIME_API_VERSION` environment variables
2. **Schema Updates**: Update YAML models to include `schemaVersion: "m2.1"`
3. **API Endpoints**: Ensure FlowTime API supports `/v1/` endpoint structure
4. **Container Network**: Verify Docker network configuration for cross-container communication

### Backward Compatibility
- **M0/M1 Models**: Existing constant and Poisson models remain fully supported
- **CLI Interface**: All existing CLI arguments and flags maintained
- **Output Format**: Generated CSV and JSON formats unchanged

## Contributors

This release represents the completion of SIM-M2.1 milestone with comprehensive PMF generator support, enhanced container networking, and robust testing coverage. The implementation provides a solid foundation for advanced simulation capabilities while maintaining backward compatibility with existing FlowTime-Sim functionality.

---

**Next Milestone**: SIM-M3 - Advanced Distribution Support and Performance Optimization
