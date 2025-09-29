# FlowTime-Sim CLI Usage Guide

## Overview

The FlowTime-Sim CLI provides template-based simulation model generation from the command line. It supports the new DAG-based template schema and integrates with the FlowTime Engine.

## Installation

```bash
# Build from source
cd src/FlowTime.Sim.Cli
dotnet build

# Run directly
dotnet run --project src/FlowTime.Sim.Cli -- [options]

# Create alias for convenience
alias flowtime-sim="dotnet run --project /path/to/FlowTime.Sim.Cli --"
```

## Command Structure

```bash
flowtime-sim <command> <action> [options]
```

## Template Commands

### template list

Lists all available templates.

```bash
flowtime-sim template list [options]
```

**Options:**
- `--format <json|table>`: Output format (default: table)
- `--tag <tag>`: Filter by tag
- `--verbose`: Show detailed information

**Examples:**
```bash
# List all templates
flowtime-sim template list

# List with JSON output
flowtime-sim template list --format json

# Filter by tag
flowtime-sim template list --tag microservices

# Verbose output with descriptions
flowtime-sim template list --verbose
```

**Output (table format):**
```
ID                        TITLE                         VERSION  TAGS
it-system-microservices   IT System with Microservices 1.0.0    microservices, web-scale
manufacturing-assembly    Manufacturing Assembly Line  1.2.1    manufacturing, assembly
network-routing          Network Packet Routing       0.9.0    networking, routing
```

**Output (JSON format):**
```json
{
  "templates": [
    {
      "id": "it-system-microservices",
      "title": "IT System with Microservices",
      "version": "1.0.0",
      "tags": ["microservices", "web-scale", "stochastic"],
      "parameterCount": 7,
      "nodeCount": 19,
      "outputCount": 8
    }
  ]
}
```

### template show

Displays complete template definition.

```bash
flowtime-sim template show --id <template-id> [options]
```

**Options:**
- `--id <id>`: Template identifier (required)
- `--format <yaml|json>`: Output format (default: yaml)
- `--section <metadata|parameters|nodes|outputs>`: Show specific section only
- `--with-params <file>`: Show processed template with parameter values

**Examples:**
```bash
# Show complete template
flowtime-sim template show --id it-system-microservices

# Show as JSON
flowtime-sim template show --id it-system-microservices --format json

# Show only parameters section
flowtime-sim template show --id it-system-microservices --section parameters

# Show processed template with parameter values
flowtime-sim template show --id it-system-microservices --with-params params.json
```

### template validate

Validates template structure and parameters.

```bash
flowtime-sim template validate [options]
```

**Options:**
- `--id <id>`: Validate existing template by ID
- `--file <path>`: Validate template file
- `--params <file>`: Validate with specific parameters
- `--strict`: Enable strict validation (fail on warnings)

**Examples:**
```bash
# Validate existing template
flowtime-sim template validate --id it-system-microservices

# Validate template file
flowtime-sim template validate --file my-template.yaml

# Validate with parameters
flowtime-sim template validate --id it-system-microservices --params test-params.json

# Strict validation
flowtime-sim template validate --file my-template.yaml --strict
```

**Output:**
```
✓ Template structure valid
✓ All required fields present
✓ Parameter types consistent
✓ Node references valid
✓ PMF probabilities sum to 1.0
✓ No circular dependencies
✓ Output series reference valid nodes

Validation passed: 7/7 checks
```

### template generate

Generates FlowTime Engine model from template and parameters.

```bash
flowtime-sim template generate --id <template-id> [options]
```

**Options:**
- `--id <id>`: Template identifier (required)
- `--params <file>`: Parameter file (JSON/YAML)
- `--output <path>`: Output file path (default: stdout)
- `--format <yaml|json>`: Output format (default: yaml)
- `--validate`: Validate parameters before generation

**Parameter File Formats:**

**JSON format:**
```json
{
  "bins": 24,
  "binSize": 1,
  "binUnit": "hours",
  "requestPattern": [100, 150, 200, 180, 120],
  "capacity": 300
}
```

**YAML format:**
```yaml
bins: 24
binSize: 1
binUnit: hours
requestPattern: [100, 150, 200, 180, 120]
capacity: 300
```

**Examples:**
```bash
# Generate with JSON parameters
flowtime-sim template generate --id it-system-microservices --params params.json

# Generate with YAML parameters and save to file
flowtime-sim template generate \
  --id it-system-microservices \
  --params params.yaml \
  --output model.yaml

# Generate with validation
flowtime-sim template generate \
  --id it-system-microservices \
  --params params.json \
  --validate \
  --output engine-model.yaml

# Interactive parameter input
flowtime-sim template generate --id it-system-microservices --interactive
```

## Simulation Commands

### sim run

Executes complete simulation pipeline: template → model → FlowTime Engine → results.

```bash
flowtime-sim sim run --template <id> [options]
```

**Options:**
- `--template <id>`: Template identifier (required)
- `--params <file>`: Parameter file
- `--output <dir>`: Output directory for CSV files (default: ./output)
- `--flowtime-url <url>`: FlowTime Engine API URL (default: http://localhost:8080)
- `--engine-args <args>`: Additional FlowTime Engine arguments
- `--keep-model`: Keep generated model file

**Examples:**
```bash
# Basic simulation run
flowtime-sim sim run --template it-system-microservices --params params.json

# Custom output directory
flowtime-sim sim run \
  --template it-system-microservices \
  --params params.json \
  --output results/run-001

# Use remote FlowTime Engine
flowtime-sim sim run \
  --template it-system-microservices \
  --params params.json \
  --flowtime-url http://flowtime-engine:8080

# Keep generated model for inspection
flowtime-sim sim run \
  --template it-system-microservices \
  --params params.json \
  --keep-model \
  --output results/
```

**Output:**
```
Generating model from template 'it-system-microservices'...
✓ Template loaded
✓ Parameters validated
✓ Model generated (485 lines)

Executing simulation via FlowTime Engine...
✓ Model submitted to engine
✓ Simulation completed (2.3s)

Results written to: results/
- requests.csv (24 rows)
- processed.csv (24 rows)
- utilization.csv (24 rows)
- failure_rate.csv (24 rows)

Summary:
- Total requests: 4,320
- Total processed: 3,890 (90.1%)
- Peak utilization: 98.2%
- Max failure rate: 15.3%
```

## Template-Only Operation

**Note**: FlowTime-Sim operates exclusively with node-based templates. Legacy simulation files are not supported.

## Migration Commands

### migrate yaml-to-template

Converts legacy simulation YAML to new template format.

```bash
flowtime-sim migrate yaml-to-template --input <file> [options]
```

**Options:**
- `--input <file>`: Legacy YAML file (required)
- `--output <file>`: Output template file (default: <input>-template.yaml)
- `--template-id <id>`: Template ID for generated template
- `--extract-params`: Extract values as parameters

**Examples:**
```bash
# Basic conversion
flowtime-sim migrate yaml-to-template --input legacy.yaml

# Custom output and template ID
flowtime-sim migrate yaml-to-template \
  --input legacy.yaml \
  --output new-template.yaml \
  --template-id converted-template

# Extract values as parameters
flowtime-sim migrate yaml-to-template \
  --input legacy.yaml \
  --extract-params
```

## Configuration

### Global Configuration

Create `~/.flowtime-sim/config.yaml`:

```yaml
# Default FlowTime Engine settings
engine:
  url: http://localhost:8080
  timeout: 30s

# Default directories
directories:
  output: ./output
  templates: ./templates
  cache: ~/.flowtime-sim/cache

# CLI preferences
preferences:
  defaultFormat: yaml
  verboseOutput: false
  colorOutput: true
```

### Environment Variables

```bash
# FlowTime Engine URL
export FLOWTIME_ENGINE_URL=http://localhost:8080

# Default output directory
export FLOWTIME_OUTPUT_DIR=./results

# Enable debug logging
export FLOWTIME_DEBUG=true

# Template directory for custom templates
export FLOWTIME_TEMPLATE_DIR=./templates
```

## Advanced Usage

### Batch Processing

Process multiple parameter sets:

```bash
# Create parameter sets
echo '{"capacity": 100}' > params-small.json
echo '{"capacity": 500}' > params-large.json

# Batch process
for params in params-*.json; do
  flowtime-sim sim run \
    --template it-system-microservices \
    --params "$params" \
    --output "results/$(basename $params .json)"
done
```

### Pipeline Integration

Use in CI/CD pipelines:

```bash
#!/bin/bash
# simulation-pipeline.sh

set -e

# Validate all templates
for template in templates/*.yaml; do
  flowtime-sim template validate --file "$template" --strict
done

# Run regression tests
flowtime-sim sim run \
  --template baseline-performance \
  --params regression-params.json \
  --output regression-results/

# Compare with baseline
if ! diff -q regression-results/ baseline-results/; then
  echo "Performance regression detected!"
  exit 1
fi
```

### Custom Templates

Develop and test custom templates:

```bash
# Create new template
flowtime-sim template create \
  --id my-custom-template \
  --title "My Custom Template" \
  --output custom-template.yaml

# Edit template file...

# Validate custom template
flowtime-sim template validate --file custom-template.yaml

# Test with parameters
flowtime-sim template generate \
  --file custom-template.yaml \
  --params test-params.json

# Install template
flowtime-sim template install --file custom-template.yaml
```

## Output Formats

### Table Format (default)
Human-readable tables for terminal display.

### JSON Format
Structured data for programmatic processing:

```bash
flowtime-sim template list --format json | jq '.templates[].id'
```

### YAML Format
Human-readable structured data:

```bash
flowtime-sim template show --id my-template --format yaml > template.yaml
```

## Error Handling

CLI provides structured error reporting:

```bash
# Exit codes:
# 0 - Success
# 1 - General error
# 2 - Invalid arguments
# 3 - Template not found
# 4 - Parameter validation failed
# 5 - Model generation failed
# 6 - Engine communication error

# Error output includes:
# - Error description
# - Suggested fixes
# - Relevant documentation links
```

**Example error output:**
```
Error: Parameter validation failed

Details:
- Parameter 'capacity': Value -1 is below minimum allowed value of 1
- Parameter 'requestPattern': Array cannot be empty

Suggestions:
- Check parameter constraints in template definition
- Use 'flowtime-sim template show --id <template> --section parameters'
- Validate parameters with 'flowtime-sim template validate'

Documentation: https://docs.flowtime.dev/templates
```

## Debugging

Enable debug output:

```bash
# Environment variable
export FLOWTIME_DEBUG=true
flowtime-sim template generate --id my-template --params params.json

# Command line flag
flowtime-sim --debug template generate --id my-template --params params.json

# Verbose output
flowtime-sim --verbose template generate --id my-template --params params.json
```

Debug output includes:
- Template loading process  
- Parameter substitution details
- Model generation steps
- Engine communication logs
- Performance timing information