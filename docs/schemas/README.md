# FlowTime Engine API Schema Reference

## Quick Reference

**FlowTime Engine** expects **YAML** input on `POST /run` with the following structure:

```yaml
schemaVersion: 1    # Required: Schema version (always 1 for current)

grid:                # Required: Time grid definition
  bins: 24           # Required: Number of time periods
  binSize: 1         # Required: Duration magnitude
  binUnit: hours     # Required: minutes|hours|days|weeks

nodes:               # Required: Array of node definitions
  - id: demand       # Required: Unique identifier
    kind: const      # Required: const|expr|pmf
    values: [...]    # For const: array of numbers
  - id: served
    kind: expr
    expr: "demand * 0.8"  # For expr: expression string
```

**⚠️ Schema Evolution**: FlowTime M-02.09 uses `binSize`/`binUnit` format (not legacy `binMinutes`). See [model.schema.md](model.schema.md) for complete specification.

## Content Format

- **Input**: `POST /run` with `Content-Type: text/plain`
- **Body**: YAML document (not JSON)
- **Response**: JSON with run results and telemetry data

## Why YAML?

1. **Human Readable**: YAML is more readable for model definitions
2. **Industry Standard**: Common for configuration and model specs
3. **FlowTime-Sim Integration**: Maintains compatibility with Sim service
4. **Expression Clarity**: Expressions like `"MIN(demand, capacity)"` are clearer in YAML

## Schema Validation

- **Current Schema (M-02.09)**: [`model.schema.yaml`](model.schema.yaml) - Unified format with `binSize`/`binUnit`
- **Documentation**: [`model.schema.md`](model.schema.md) - Complete specification
- **Legacy Schemas**: `engine-input.schema.json` and `engine-input-schema.md` are **deprecated** (use model.schema instead)
- **Examples**: [`/examples/`](/examples/) directory
- **Time-Travel Responses**: [`time-travel-state.schema.json`](time-travel-state.schema.json) — canonical JSON schema for `/v1/runs/{id}/state` and `/state_window` payloads (validated in `StateResponseSchemaTests`)

## Integration with FlowTime-Sim

FlowTime-Sim **generates** this YAML format from template parameters:

1. **Template Input** (JSON/YAML) → FlowTime-Sim
2. **Model Generation** → Produces engine-compatible YAML
3. **Engine Execution** → FlowTime Engine processes the model
4. **Telemetry Output** → CSV results for analysis

## Error Handling

Invalid YAML returns `400 Bad Request` with JSON error:

```json
{
  "error": "Node 'served' references undefined node 'demand'"
}
```

## See Also

- [Complete Engine Schema](engine-input-schema.md) - Full schema documentation
- [Template Schema](template-schema.md) - FlowTime-Sim template format  
- [API Reference](/docs/api/) - Complete API documentation
