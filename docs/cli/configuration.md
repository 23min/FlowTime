# CLI Configuration

The FlowTime-Sim CLI supports configuration files to specify template and model directories, making it easier to work from any location in your repository.

## Quick Start

Create a configuration file in your project directory:

```bash
flow-sim init
```

This creates `.flow-sim.yaml` with smart defaults based on your current directory. You can also specify custom paths:

```bash
flow-sim init --templates-dir /path/to/templates --models-dir /path/to/models
```

## Configuration Files

The CLI searches for `.flow-sim.yaml` in the following locations (in priority order):

1. **Project-local**: `./.flow-sim.yaml` (current directory and parent directories)
2. **User-level**: `~/.flow-sim.yaml` (user home directory)

Configuration is loaded by walking up the directory tree from the current working directory until a `.flow-sim.yaml` file is found.

## Configuration Format

```yaml
templates:
  directory: /absolute/path/to/templates

data:
  models: /absolute/path/to/data/models

defaults:
  format: yaml  # or csv
  verbose: false
```

### Supported Options

- **`templates.directory`**: Absolute path to templates directory
- **`data.models`**: Absolute path to models data directory  
- **`defaults.format`**: Default output format (`yaml` or `csv`)
- **`defaults.verbose`**: Enable verbose output by default

## Environment Variables

You can also use environment variables to configure the CLI:

- `FLOW_SIM_TEMPLATES_DIR`: Override templates directory
- `FLOW_SIM_DATA_DIR`: Override models directory
- `FLOW_SIM_VERBOSE`: Enable verbose output (`true` or `1`)

## Priority Order

Configuration is resolved in the following priority order (highest to lowest):

1. **Command-line flags**: `--templates-dir`, `--models-dir`, `--verbose`
2. **Environment variables**: `FLOW_SIM_TEMPLATES_DIR`, `FLOW_SIM_DATA_DIR`, `FLOW_SIM_VERBOSE`
3. **Project config**: `./.flow-sim.yaml` (found by walking up directory tree)
4. **User config**: `~/.flow-sim.yaml`
5. **Defaults**: `./templates` and `./data/models`

## Example: Repository Configuration

For a typical repository structure, create `.flow-sim.yaml` in the repository root:

```yaml
templates:
  directory: /workspaces/flowtime-sim-vnext/templates

data:
  models: /workspaces/flowtime-sim-vnext/data/models
```

This allows you to run commands from any subdirectory:

```bash
# From anywhere in the repo
cd /workspaces/flowtime-sim-vnext/src
flow-sim list templates  # Uses config from repo root

cd /workspaces/flowtime-sim-vnext/examples
flow-sim list models     # Same config still found
```

## Example: User-Level Configuration

Create `~/.flow-sim.yaml` to set defaults for all projects:

```yaml
defaults:
  format: yaml
  verbose: true
```

## Troubleshooting

Use `--verbose` to see which configuration file is loaded:

```bash
flow-sim list templates --verbose
# Output shows: Loading config from: /path/to/.flow-sim.yaml
```

If no configuration file is found, the CLI falls back to default paths (`./templates` and `./data/models` relative to current directory).

## No Configuration Required

The CLI works without any configuration file if:
- You run commands from the repository root, OR
- You specify paths via command-line flags:
  ```bash
  flow-sim list templates --templates-dir /path/to/templates
  ```
