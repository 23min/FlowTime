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

# Run a simple example
dotnet run --project src/FlowTime.Sim.Cli -- --model examples/m0.const.yaml --out output/

# Check the results
ls output/  # Contains run.json, manifest.json, and CSV files
```

## Basic example

Create a file called `my-model.yaml`:

```yaml
schemaVersion: 1
grid: 
  bins: 24
  binMinutes: 60
  start: 2025-01-01T00:00:00Z
seed: 42
rng: pcg

arrivals:
  kind: pmf
  pmf:
    - { value: 100, probability: 0.6 }  # 60% chance of 100 requests/hour
    - { value: 200, probability: 0.3 }  # 30% chance of 200 requests/hour  
    - { value: 500, probability: 0.1 }  # 10% chance of 500 requests/hour (spike)

route: { id: api-service }
catalog: catalogs/demo-system.yaml
```

Run it:
```bash
dotnet run --project src/FlowTime.Sim.Cli -- --model my-model.yaml --out results/
```

This creates a 24-hour model specification with realistic traffic patterns including occasional spikes. Currently generates synthetic data, but will transition to model artifact creation.

## What you get

FlowTime-Sim currently outputs structured data you can analyze:

- **run.json** - Metadata about the simulation run
- **manifest.json** - File integrity hashes 
- **series/{metric}.csv** - Time-series data (arrivals, flows, backlogs, etc.)

Each CSV has simple `t,value` format where `t` is the time bin (0, 1, 2...) and `value` is the metric.

**Note**: This synthetic data generation is legacy functionality. Future charter-compliant versions will output model artifacts for FlowTime-Engine execution instead.

## HTTP API with Template Support

Start the service:
```bash
dotnet run --project src/FlowTime.Sim.Service
```

**New in v0.3.1**: Template metadata endpoints

List available templates with metadata:
```bash
curl http://localhost:8090/v1/sim/templates
```

Generate scenario from template (uses template-specific parameters):
```bash
curl -X POST http://localhost:8090/v1/sim/templates/transportation-basic/generate \
  -H "Content-Type: application/json" \
  -d '{
    "demandPeakHour": 8,
    "demandPeakMultiplier": 2.5,
    "capacityBaseLevel": 150
  }'
```

Legacy simulation endpoint (generates synthetic data):
```bash
curl -X POST http://localhost:8090/v1/sim/run \
  -H "Content-Type: application/json" \
  -d '{"templateId": "basic", "parameters": {}}'
```

## CLI options

```bash
# Basic usage
dotnet run --project src/FlowTime.Sim.Cli -- --model path/to/model.yaml --out output/

# Options:
--model <file>       YAML model file
--out <directory>    Output directory  
--format csv|json    Output format
--mode engine|sim    Integration mode (engine posts to FlowTime-Engine, sim generates locally)
--flowtime <url>     FlowTime-Engine URL for integration mode
--verbose            Detailed logging
```

## Integration with FlowTime-Engine

FlowTime-Sim can send data directly to a FlowTime-Engine instance:

```bash
dotnet run --project src/FlowTime.Sim.Cli -- \
  --model examples/m0.const.yaml \
  --flowtime http://localhost:8080 \
  --out results.csv \
  --format csv
```

This posts the model to FlowTime-Engine for deterministic execution rather than generating synthetic data locally. This integration approach aligns with the charter-compliant direction.

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
- **[CLI Guide](docs/guides/CLI.md)** - Complete command-line usage and examples
- **[Configuration Guide](docs/guides/configuration.md)** - Setup and configuration reference
- **[Architecture Documentation](docs/architecture/)** - Technical design patterns and reference
- **[API Reference](docs/reference/)** - Technical contracts and specifications

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

