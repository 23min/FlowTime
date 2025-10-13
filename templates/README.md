# FlowTime Template Notes

## telemetry-model-test

- Provides telemetry-mode coverage with const node `source` bindings so we can assert TemplateService wiring without committing to real data yet.
- **Parameters**
  - `telemetry_dir`: optional base path for telemetry CSVs (default empty). When populated, overrides inline values via `file://` bindings.
  - All other parameters mirror the simulation defaults (`bins`, `binSize`, request/capacity arrays).
- **Usage**
  - Simulation: `flow-sim generate --id telemetry-model-test`
  - Telemetry: `flow-sim generate --id telemetry-model-test --mode telemetry --params telemetry.json`
- `telemetry.json` minimal example:
  ```json
  {
    "telemetry_dir": "file:///workspaces/flowtime-vnext/data/telemetry"
  }
  ```

