# FlowTime-Sim CLI Usage Guide

**Charter-Compliant Model Authoring Tool**

## Overview

FlowTime-Sim CLI is a template-based model authoring tool that generates FlowTime Engine-compatible models. It uses `NodeBasedTemplateService` from FlowTime.Sim.Core directly - no API calls, pure local model generation.

## What FlowTime-Sim CLI Does

✅ **Lists** available templates  
✅ **Shows** template details and parameters with **defaults**  
✅ **Generates** Engine-compatible models from templates  
✅ **Validates** parameter overrides  
✅ **Lists** generated models  

❌ **Does NOT** call any APIs  
❌ **Does NOT** generate telemetry or execute models  
❌ **Does NOT** have engine or sim modes  

This is a **model authoring tool only**. To execute models and generate telemetry, use FlowTime Engine.

## Installation

```bash
# Build from source
cd src/FlowTime.Sim.Cli
dotnet build

# Or build the entire solution
dotnet build
```

## Usage

```bash
flow-sim <command> [options]
```

## Commands

### 1. List Templates

List all available templates:

```bash
flow-sim list
```

**Output:**
```
Available Templates (5):

ID: transportation-basic
  Title: Transportation Network
  Description: Simple transportation flow with demand and capacity constraints
  Tags: beginner, transportation, logistics, capacity
  Parameters: 4

[... more templates ...]
```

### 2. Show Template Details

Show detailed information about a template, including its parameters with their **default values**:

```bash
flow-sim show template --id transportation-basic
```

This displays:
- Template metadata (title, description, tags)
- All available parameters with their types and **default values**
- Grid configuration
- Node definitions
- Output specifications

**Note**: Templates are self-contained with parameter defaults. You don't need separate parameter files unless you want to **override** specific defaults.

### 3. List Generated Models

List all models that have been generated:

```bash
flow-sim list models
```

**Output:**
```
Available models in /workspaces/flowtime-sim-vnext/data/models:

  it-system-microservices
  manufacturing-line
```

### 4. Show Model Details

Show detailed information about a generated model:

```bash
flow-sim show model --id it-system-microservices
```

**Output:**
```
Model: it-system-microservices
Location: /workspaces/flowtime-sim-vnext/data/models/it-system-microservices

Metadata:
  Template ID:      it-system-microservices
  Generated:        2025-10-01T08:33:06.1564019Z
  Model Hash:       sha256:ab4586517323e9774824c63ea80a8ee6290f97aa7053a703589dc88ba2268e81
  Parameters:       (none - using template defaults)

Model Preview:
  schemaVersion: 1
  
  grid:
    bins: 12
    binMinutes: 60
  
  nodes:
    - id: user_requests
      kind: const
      values: [100, 150, 200, 180, 120, 80]
  ... (36 more lines)

Full model: /workspaces/flowtime-sim-vnext/data/models/it-system-microservices/model.yaml
```

This displays:
- Model location and metadata
- Template used to generate the model
- Generation timestamp and hash
- Parameters that were used (or defaults)
- Preview of the model content (first 20 lines)
- Full path to the model file

### 5. Generate Engine Model

Generate a FlowTime Engine model from a template:

```bash
# Generate with default parameters (output to stdout)
flow-sim generate --id transportation-basic

# Generate with default parameters to file
flow-sim generate --id transportation-basic --out model.yaml

# Generate with parameter overrides
flow-sim generate --id transportation-basic --params overrides.json --out model.yaml

# Generate in JSON format
flow-sim generate --id transportation-basic --format json --out model.json
```

#### Parameter Override File Format

Templates have **built-in default values** for all parameters. You only need a parameter file if you want to **override** specific defaults:

```json
{
  "bins": 24,
  "binMinutes": 30,
  "demandPattern": [20, 30, 40, 35, 25, 15]
}
```

**Note**: You don't need to specify all parameters - only the ones you want to override. Unspecified parameters use their template defaults.

### 6. Validate Parameters

Validate that parameter overrides meet template requirements:

```bash
# Validate parameter overrides
flow-sim validate --id transportation-basic --params overrides.json

# Show all template parameters with their defaults
flow-sim validate --id transportation-basic
```

This checks:
- Provided parameters exist in the template
- Types match specifications
- Values are within defined ranges (min/max constraints)
- Shows which parameters use defaults vs. overrides

### 7. Initialize Configuration

Create a `.flow-sim.yaml` configuration file:

```bash
# Create config with smart defaults
flow-sim init

# Create config with custom paths
flow-sim init --templates-dir /path/to/templates --models-dir /path/to/models
```

**Output:**
```
Created configuration file: /workspaces/flowtime-sim-vnext/.flow-sim.yaml

Configured values:
  Templates directory: /workspaces/flowtime-sim-vnext/templates
  Models directory:    /workspaces/flowtime-sim-vnext/data/models
  Default format:      yaml
  Default verbose:     False

✓ All directories exist.
```

See [`docs/cli/configuration.md`](configuration.md) for details on configuration files and priority.

## Options

| Option | Description | Default |
|--------|-------------|---------|
| `--id <id>` | Template identifier | - |
| `--params <file>` | JSON file with parameter overrides (optional) | None |
| `--out <file>` | Output file path | stdout |
| `--format yaml\|json` | Output format | yaml |
| `--templates-dir <path>` | Templates directory | `./templates` |
| `--models-dir <path>` | Models directory | `./data/models` |
| `--provenance <file>` | Save provenance metadata to separate JSON file | None |
| `--embed-provenance` | Embed provenance metadata in model YAML | false |
| `--verbose, -v` | Verbose output | false |
| `--help, -h` | Show help | - |

**Provenance Options** (SIM-M2.7):
- `--provenance` and `--embed-provenance` are **mutually exclusive**
- Use `--provenance` to save metadata in a separate JSON file for Engine storage
- Use `--embed-provenance` to include metadata as structured YAML comments in the model
- Without either flag, no provenance metadata is generated (backward compatibility)

## Model Provenance (SIM-M2.7)

FlowTime-Sim can generate **provenance metadata** that tracks the origin and parameters of generated models. This enables traceability from template to execution run.

### Provenance Metadata Structure

```json
{
  "source": "flowtime-sim",
  "modelId": "model_20250925T103045Z_a3f8c2d1",
  "templateId": "transportation-basic",
  "templateVersion": "1.0",
  "templateTitle": "Transportation Network",
  "parameters": {
    "bins": 12,
    "binMinutes": 60,
    "demandPattern": [20, 30, 40, 35, 25, 15]
  },
  "generatedAt": "2025-09-25T10:30:45.1234567Z",
  "generator": "flowtime-sim/0.5.0",
  "schemaVersion": "1"
}
```

### Separate Provenance File Mode

Save provenance metadata to a separate JSON file:

```bash
flow-sim generate --id transportation-basic \
  --out model.yaml \
  --provenance provenance.json
```

**Output files:**
- `model.yaml` - Clean YAML model for Engine
- `provenance.json` - Metadata for Engine storage

**Use case**: When Engine needs to store provenance separately (recommended for M2.9 integration)

### Embedded Provenance Mode

Embed provenance metadata directly in the model YAML:

```bash
flow-sim generate --id transportation-basic \
  --out model.yaml \
  --embed-provenance
```

**Output:** Single YAML file with provenance as structured comments:

```yaml
schemaVersion: 1

# === Model Provenance (flowtime-sim) ===
# Generated: 2025-09-25T10:30:45.1234567Z
# Model ID: model_20250925T103045Z_a3f8c2d1
# Template: transportation-basic (v1.0) - Transportation Network
# Generator: flowtime-sim/0.5.0
provenance:
  source: flowtime-sim
  modelId: model_20250925T103045Z_a3f8c2d1
  templateId: transportation-basic
  # ... full provenance structure ...

grid:
  bins: 12
  binMinutes: 60
# ... rest of model ...
```

**Use case**: When UI needs to display provenance inline or for debugging

### Provenance Best Practices

1. **Mutual Exclusivity**: Cannot use both `--provenance` and `--embed-provenance` together
2. **Model ID Format**: `model_{timestamp}_{hash}` ensures uniqueness and determinism
3. **Deterministic Hashing**: Same template + parameters = same hash portion
4. **Timestamp Precision**: UTC ISO8601 format with subsecond precision
5. **Engine Integration**: Use `--provenance` mode for clean separation (recommended)

### Provenance Without Parameters

If generating with all template defaults (no `--params`), provenance captures an empty parameters object:

```bash
flow-sim generate --id transportation-basic \
  --out model.yaml \
  --provenance provenance.json
```

Provenance will show: `"parameters": {}`

## Typical Workflows

### Quick Start: Generate with Defaults

1. **Discover templates**
   ```bash
   flow-sim list
   ```

2. **Examine a template** (shows default parameters)
   ```bash
   flow-sim show --id transportation-basic
   ```

3. **Generate model with defaults**
   ```bash
   flow-sim generate --id transportation-basic --out model.yaml
   ```

4. **Use model with FlowTime Engine**
   ```bash
   # Via Engine CLI (when available)
   flowtime run --model model.yaml
   
   # Or via Engine API
   curl -X POST http://localhost:8080/api/v1/run \
     -H "Content-Type: application/x-yaml" \
     --data-binary @model.yaml
   ```

### Advanced: Override Specific Parameters

1. **Examine template defaults**
   ```bash
   flow-sim show --id transportation-basic
   ```

2. **Create overrides file** (`overrides.json`) - only override what you want to change
   ```json
   {
     "bins": 24,
     "demandPattern": [25, 30, 35, 40, 35, 30]
   }
   ```
   *Note: `binMinutes` and `capacityPattern` will use template defaults*

3. **Validate overrides** (optional but recommended)
   ```bash
   flow-sim validate --id transportation-basic --params overrides.json
   ```

4. **Generate model with overrides**
   ```bash
   flow-sim generate --id transportation-basic --params overrides.json --out model.yaml
   ```

5. **List generated models**
   ```bash
   flow-sim models
   ```

## CLI/API Parity

The CLI provides the same core functionality as the FlowTime-Sim API:

| Functionality | CLI Command | API Endpoint |
|--------------|-------------|--------------|
| List templates | `flow-sim list` | `GET /api/v1/templates` |
| Show template | `flow-sim show --id <id>` | `GET /api/v1/templates/{id}` |
| Generate model | `flow-sim generate --id <id>` | `POST /api/v1/templates/{id}/generate` |
| List models | `flow-sim models` | `GET /api/v1/models` |

**Not in CLI** (API-specific config management):
- Catalog operations (`GET /api/v1/catalogs`, `PUT /api/v1/catalogs/{id}`)
- Health/version endpoints

## Charter Compliance

✅ **Model authoring only** - Generates models, doesn't execute them  
✅ **No API calls** - Uses Core services directly  
✅ **No telemetry generation** - That's FlowTime Engine's job  
✅ **Template-driven** - All models come from parameterized templates  
✅ **Clear separation** - Sim creates, Engine executes  

## Troubleshooting

### Templates directory not found

```bash
Templates directory not found: ./templates
Specify with --templates-dir <path> or ensure ./templates exists.
```

**Solution**: Specify the correct templates directory:
```bash
flow-sim list --templates-dir /path/to/templates
```

### Template not found

```bash
Error: Template 'unknown-template' not found
```

**Solution**: List available templates first:
```bash
flow-sim list
```

### Parameter validation failed

```bash
✗ Validation failed:
  - Parameter 'bins' must be between 3 and 48
```

**Solution**: Check template constraints with `show` and adjust your overrides.

## Examples

### Generate multiple models with different parameters

```bash
# Create parameter override files
cat > low-demand.json <<EOF
{"demandPattern": [5, 8, 10, 12, 8, 5]}
EOF

cat > high-demand.json <<EOF
{"demandPattern": [30, 40, 50, 45, 35, 25]}
EOF

# Generate models
flow-sim generate --id transportation-basic --params low-demand.json --out model-low.yaml
flow-sim generate --id transportation-basic --params high-demand.json --out model-high.yaml

# List generated models
flow-sim models
```

### Pipeline integration

```bash
#!/bin/bash
# Generate model and run simulation

# Generate model
flow-sim generate --id transportation-basic --out model.yaml

# Execute with FlowTime Engine
curl -X POST http://localhost:8080/api/v1/run \
  -H "Content-Type: application/x-yaml" \
  --data-binary @model.yaml \
  -o results.json

# Analyze results
jq '.series[] | select(.id == "utilization") | .values' results.json
```

## See Also

- [FlowTime-Sim API Documentation](../api/)
- [Template Schema Reference](../schemas/template-schema.md)
- [FlowTime Engine Documentation](https://github.com/23min/flowtime-vnext/docs/)
