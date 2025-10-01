# FlowTime-Sim

[![Build](https://github.com/23min/FlowTime-Sim/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/23min/FlowTime-Sim/actions/workflows/build.yml)
![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)

FlowTime-Sim is a model authoring platform for creating stochastic simulation models with metadata-driven template support. It generates realistic arrival patterns, service times, and flow behaviors for capacity planning, SLA modeling, and system analysis.

## Latest Release: v0.3.1 (Template Metadata System)

🚀 **Major improvement**: Complete metadata-driven parameter system with template-specific configurations
- **Template Metadata Support**: Each template now defines its own parameter types, defaults, and validation rules
- **Dynamic Template Discovery**: API endpoints for listing templates and generating model scenarios
- **Backward Compatibility**: Existing models continue to work while benefiting from enhanced metadata
- **4 Enhanced Templates**: Transportation, manufacturing, IT systems, and supply chain scenarios

See [M2.6-v0.3.1 Release Notes](docs/releases/M2.6-v0.3.1.md) for complete technical details and migration guide.

## What it does

FlowTime-Sim helps you model systems like:
- API services with varying load patterns  
- Queue-based workflows with backlog dynamics
- Multi-stage processing pipelines
- Transportation and logistics systems
- Manufacturing workflows
- Multi-tier supply chains

The platform generates structured simulation data and is evolving toward charter-compliant model authoring for FlowTime-Engine integration.

## Quick start

**Prerequisites**: .NET 9 SDK

```bash
# Clone and build
git clone https://github.com/23min/FlowTime-Sim.git
cd FlowTime-Sim
dotnet build

# List available templates
dotnet run --project src/FlowTime.Sim.Cli -- list templates

# Generate a model from a template
dotnet run --project src/FlowTime.Sim.Cli -- generate --id transportation-basic --out my-model.yaml
```

## Basic example

Generate a model from a template:

```bash
# List available templates
dotnet run --project src/FlowTime.Sim.Cli -- list templates

# Show a template with its parameters
dotnet run --project src/FlowTime.Sim.Cli -- show template --id transportation-basic

# Generate a model using template defaults
dotnet run --project src/FlowTime.Sim.Cli -- generate --id transportation-basic --out my-model.yaml

# Or generate with custom parameters
echo '{ "bins": 24, "demandPattern": [100, 200, 150, 180] }' > params.json
dotnet run --project src/FlowTime.Sim.Cli -- generate --id transportation-basic --params params.json --out my-model.yaml
```

The generated model is a complete YAML specification ready for simulation with FlowTime-Engine.

## What you get

FlowTime-Sim generates structured YAML model files ready for simulation:

- **Model YAML** - Complete node-based model specification with:
  - Grid configuration (time periods, bin size, start time)
  - RNG configuration for deterministic simulation
  - Node definitions (source, processing, sink nodes)
  - Grid data (arrival patterns, service times)
  - Output specifications

These models are designed for execution by FlowTime-Engine, which will produce time-series results and telemetry.

## HTTP API with Template Support

Start the service:
```bash
dotnet run --project src/FlowTime.Sim.Service
```

The API provides endpoints for template management and catalog validation:

```bash
# List available catalogs
curl http://localhost:8090/api/v1/catalogs

# Get a specific catalog
curl http://localhost:8090/api/v1/catalogs/demo-system

# Validate a catalog YAML
curl -X POST http://localhost:8090/api/v1/catalogs/validate \
  -H "Content-Type: text/plain" \
  --data-binary @my-catalog.yaml

# Health check
curl http://localhost:8090/health
```

## CLI commands

```bash
# List templates
flow-sim list templates

# List generated models
flow-sim list models

# Show template details
flow-sim show template --id <template-id>

# Show generated model
flow-sim show model --id <model-hash>

# Generate model from template
flow-sim generate --id <template-id> [--params <params.json>] [--out <file>]

# Validate template or catalog
flow-sim validate template --id <template-id>
flow-sim validate catalog <file.yaml>

# Initialize config
flow-sim init

# Options:
--id <id>                Template or model ID
--params <file>          JSON file with parameter overrides
--out <file>             Output file (default: stdout)
--format yaml|json       Output format (default: yaml)
--templates-dir <path>   Templates directory (default: ./templates)
--models-dir <path>      Models directory (default: ./data/models)
--verbose, -v            Verbose output
--help, -h               Show help
```

## Integration with FlowTime-Engine

FlowTime-Sim generates model YAML files that can be executed by FlowTime-Engine:

```bash
# Generate a model
flow-sim generate --id transportation-basic --out model.yaml

# Execute with FlowTime-Engine (using its CLI or API)
flowtime run --model model.yaml
```

The node-based model format aligns with FlowTime-Engine's charter-compliant execution model.

## Development status

**Current Release**: v0.3.1 - Metadata-driven template parameter system  
**Phase**: SIM-M2 milestone development with enhanced template support  
**Architecture**: Transitioning to charter-compliant model authoring for FlowTime-Engine integration

### Recent Improvements
- ✅ **Template Metadata System**: Dynamic parameter discovery and validation  
- ✅ **Template Repository**: File-based template management with YAML metadata parsing
- ✅ **Enhanced Templates**: 4 domain-specific templates with proper parameter definitions
- ✅ **Backward Compatibility**: Existing models work seamlessly with new system
- 🔄 **Charter Alignment**: Evolving toward FlowTime-Engine model artifact creation

### Output Structure (SIM-M2)
```bash
# CLI Usage
dotnet run --project src/FlowTime.Sim.Cli -- --model examples/m0.const.yaml --out runs

# Generated Output
run.json         # run summary (schemaVersion, runId, grid, hashes)
manifest.json    # integrity document 
series/index.json# discovery (series list, units, per-series hashes)
series/*.csv     # canonical per-series time series
[events.ndjson]  # optional events (may be absent)
```

**Determinism**: Identical specifications (including seed & rng) produce identical CSV bytes and hashes.

## Documentation

### Core Documentation
- **[Roadmap](docs/ROADMAP.md)** - Project roadmap and milestone timeline
- **[Charter](docs/flowtime-sim-charter.md)** - Project scope, boundaries, and role definition  
- **[Development Setup](docs/development-setup.md)** - Environment configuration and service setup

### Guides & Reference
- **[Configuration Guide](docs/guides/configuration.md)** - Setup and configuration reference
- **[Architecture Documentation](docs/architecture/)** - Technical design patterns and reference
- **[API Reference](docs/reference/)** - Technical contracts and specifications
- **CLI Usage**: Run `flow-sim --help` for complete command reference

### Development Resources
- **[Development Guides](docs/development/)** - Setup, testing, and devcontainer guides
- **[Milestone Tracking](docs/milestones/)** - SIM-M0, SIM-M1, SIM-M2 progress
- **[Release Notes](docs/releases/)** - Version history and migration guides

## Contributing

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/amazing-feature`  
3. Run tests: `dotnet test`
4. Commit changes: `git commit -m 'Add amazing feature'`
5. Push to branch: `git push origin feature/amazing-feature`
6. Open a Pull Request

See [Development Setup](docs/development-setup.md) for environment configuration and [Roadmap](docs/ROADMAP.md) for current priorities.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

