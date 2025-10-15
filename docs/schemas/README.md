# FlowTime Schema Reference

This index lists the active schemas maintained across FlowTime Engine and FlowTime-Sim. Use the sub-sections below to jump to engine vs. sim artefacts.

````{tab-set}
```{tab-item} Engine
| Schema | File | Introduced | Notes |
|--------|------|------------|-------|
| Model definition | [model.schema.yaml](model.schema.yaml) / [model.schema.md](model.schema.md) | M-02.09 | Canonical `POST /v1/run` payload (grid, nodes, topology, provenance) |
| Manifest | [manifest.schema.json](manifest.schema.json) | M-02.09 | Run metadata (hashes, RNG, provenance ref) |
| Series index | [series-index.schema.json](series-index.schema.json) | M-02.09 | Per-series metadata (id, component, hash) |
| Time-travel responses | [time-travel-state.schema.json](time-travel-state.schema.json) | M-03.01 | `/state` & `/state_window` JSON envelope |
| Legacy input | [engine-input.schema.json](engine-input.schema.json) | Deprecated | Kept for backward compatibility tests |

**Quick Reference – Model Schema Snippet**

```yaml
schemaVersion: 1
grid:
  bins: 24
  binSize: 5
  binUnit: minutes
  startTimeUtc: "2025-01-01T00:00:00Z"
nodes:
  - id: demand
    kind: const
    values: [120, 118, 122]
topology:
  nodes:
    - id: OrderService
      semantics:
        arrivals: "file:OrderService_arrivals.csv"
        served: "file:OrderService_served.csv"
provenance:
  source: flowtime-sim
  templateId: order-system
```

**Schema Validation**

- Engine responses are validated by integration tests (see `StateEndpointTests` and `StateResponseSchemaTests`).
- Canonical run artefacts written by `RunArtifactWriter` are structured as:

```mermaid
graph TD
    Template[FlowTime-Sim Template]
    Template -->|instantiate| ModelYaml(model/model.yaml)
    ModelYaml -->|RunArtifactWriter| Metadata(metadata.json)
    ModelYaml -->|RunArtifactWriter| Provenance(provenance.json)
    ModelYaml -->|RunArtifactWriter| RunJson(run.json)
    ModelYaml -->|RunArtifactWriter| Manifest(manifest.json)
    ModelYaml -->|RunArtifactWriter| SeriesIndex(series/index.json)
    SeriesIndex --> CSVs(series/*.csv)
    RunJson --> State[/state, /state_window]
```

```

```{tab-item} FlowTime-Sim
| Schema | File | Introduced | Notes |
|--------|------|------------|-------|
| Template generation | [flowtime-sim-vnext/docs/schemas/template-schema.yaml](../../flowtime-sim-vnext/docs/schemas/template-schema.yaml) | SIM-M-03.00 | Template definition (window, topology, parameters) |
| Template docs | [template-schema.md](template-schema.md) | SIM-M-03.00 | Overview of template fields and generation rules |

FlowTime-Sim emits the same model schema consumed by Engine. Its provenance output feeds directly into `model/provenance.json` once Engine executes the run.
```
````

## Error Handling

Invalid model YAML submitted to Engine returns `400 Bad Request`, for example:

```json
{
  "error": "Node 'served' references undefined node 'demand'"
}
```

## See Also

- [Run Provenance Architecture](../architecture/run-provenance.md)
- [Template Schema Reference](template-schema.md)
- [API Reference](/docs/api/)
