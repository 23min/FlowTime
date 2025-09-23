# FlowTime-Sim

[![Build](https://github.com/23min/FlowTime-Sim/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/23min/FlowTime-Sim/actions/workflows/build.yml)
![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)

FlowTime-Sim is a model authoring platform that creates model templates and stochastic input definitions for system analysis. It helps define realistic arrival patterns, service times, and flow behaviors that can be used for capacity planning, SLA modeling, and "what-if" scenarios.

## What it does

FlowTime-Sim helps you model systems like:
- API services with varying load patterns  
- Queue-based workflows with backlog dynamics
- Multi-stage processing pipelines
- Retry and circuit breaker behaviors

Currently, it can generate synthetic time-series data (CSV) and metadata (JSON), but is transitioning to a charter-compliant model authoring focus that creates model artifacts for FlowTime-Engine execution.

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

## HTTP API (Legacy)

Start the service:
```bash
dotnet run --project src/FlowTime.Sim.Service
```

Get available templates:
```bash
curl http://localhost:8090/v1/sim/templates
```

Run a simulation (legacy - generates synthetic data):
```bash
curl -X POST http://localhost:8090/v1/sim/run \
  -H "Content-Type: application/json" \
  -d '{"templateId": "basic", "parameters": {}}'
```

**Note**: The `/v1/sim/run` endpoint generates synthetic data (legacy behavior). Future charter-compliant versions will focus on model template export instead.

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

FlowTime-Sim is currently transitioning to a "model authoring" focus as part of the FlowTime ecosystem reorganization. The current version generates synthetic data, but future versions will focus on creating model templates for FlowTime-Engine execution.

See [docs/CHARTER-TRANSITION-PLAN.md](docs/CHARTER-TRANSITION-PLAN.md) for the complete development strategy.

## Documentation

- [Roadmap](docs/ROADMAP.md) - Development milestones and timeline
- [Charter](docs/flowtime-sim-charter.md) - Project scope and boundaries  
- [Development Setup](docs/development-setup.md) - Environment configuration

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/new-feature`)
3. Make your changes
4. Run tests (`dotnet test`)
5. Commit your changes (`git commit -am 'Add new feature'`)
6. Push to the branch (`git push origin feature/new-feature`)  
7. Create a Pull Request

## License

MIT License - see [LICENSE](LICENSE) for details.

---

## Docs & roadmap

- **[Charter Transition Strategic Plan](docs/CHARTER-TRANSITION-PLAN.md)** - Engine-first development strategy
- **[Development Status](docs/DEVELOPMENT-STATUS.md)** - Current phase tracking and progress  
- **[Roadmap](docs/ROADMAP.md)** - Legacy milestone timeline and charter evolution
- **[FlowTime-Sim Charter v1.0](docs/flowtime-sim-charter.md)** - Scope, boundaries, and role definition
- **[Development Setup](docs/development-setup.md)** - Service configuration and environment setup
- **[Architecture Documentation](docs/architecture/)** - Technical reference and design patterns

---

## Contributing

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/amazing-feature`  
3. Run tests: `dotnet test`
4. Commit changes: `git commit -m 'Add amazing feature'`
5. Push to branch: `git push origin feature/amazing-feature`
6. Open a Pull Request

See [Development Status](docs/DEVELOPMENT-STATUS.md) for current strategic priorities and charter alignment progress.

---

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
Run:
```bash
dotnet run --project src/FlowTime.Sim.Cli -- --model examples/m0.const.yaml --out runs
```
Outputs (SIM-M2):
```
run.json         # run summary (schemaVersion, runId, grid, hashes via series listing)
manifest.json    # integrity document (currently identical to run.json)
series/index.json# discovery (series list, units, per-series hashes)
series/*.csv     # canonical per-series time series
[events.ndjson]  # optional events (may be absent)
```
Determinism: identical spec (including seed & rng) ⇒ identical per-series CSV bytes and hashes.

## Docs & roadmap

- **CLI Guide**: [`docs/guides/CLI.md`](docs/guides/CLI.md) - Complete command-line usage and examples
- **Configuration**: [`docs/guides/configuration.md`](docs/guides/configuration.md) - Setup and configuration guide
- **Roadmap**: [`docs/ROADMAP.md`](docs/ROADMAP.md) - Project roadmap and future plans
- **Development**: [`docs/development/`](docs/development/) - Development setup, testing, and devcontainer guides
- **Reference**: [`docs/reference/`](docs/reference/) - Technical contracts and specifications
- **Milestones**: [`docs/milestones/`](docs/milestones/) - SIM-M0, SIM-M1, SIM-M2 milestone tracking
- **Releases**: [`docs/releases/`](docs/releases/) - Release notes and documentation

## License

MIT. A `LICENSE` may be added later.

