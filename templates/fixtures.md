# Telemetry Fixtures (WS4)

Templates share a common storage layout when we generate telemetry-mode models:

```
data/models/{templateId}/schema-1/mode-<simulation|telemetry>/{hash8}/
  model.yaml          # Embedded provenance, canonical artifact
  provenance.json     # CamelCase sidecar (identical to embedded block)
  metadata.json       # camelCase summary (mode, flags, hash)
```

For telemetry-mode runs we expect synthetic (Engine generated) CSVs under `data/telemetry/{templateId}/` until real telemetry sources are defined. Template parameters reference these via `file://` URIs, e.g.:

```
file://{telemetry_dir}/order-service_arrivals.csv
```

## Pending work
- Populate example CSV stubs once Engine TelemetryLoader outputs are finalized.
- Align naming with Engine manifest (M-03.02) so UI/runtime agree on paths.

