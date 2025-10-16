# Telemetry Capture Guide

The telemetry capture CLI converts canonical engine runs into telemetry bundles that FlowTime-Sim templates (and, later, live loaders) can consume. Use this guide to run the tooling, inspect the outputs, and understand how warnings surface.

## Prerequisites

- A deterministic engine run directory produced by `flowtime run` (for example `data/runs/run_deterministic_72ca609c`).
- `.NET 9.0` SDK (already required for the FlowTime CLI).
- Optional: set `FLOWTIME_DATA_DIR` to override the default data root (`./data`).

## Basic Command

```bash
flowtime telemetry capture \
  --run-dir data/runs/run_deterministic_72ca609c
```

Key behaviour:

- If `--output` is omitted, the tool writes to `$FLOWTIME_DATA_DIR/telemetry/<runId>/`.
- `manifest.json` is emitted using [`docs/schemas/telemetry-manifest.schema.json`](../schemas/telemetry-manifest.schema.json).
- Per-node CSVs are converted to the `bin_index,value` shape expected by FlowTime-Sim telemetry bindings.

### Dry Run

To review the capture plan without writing files:

```bash
flowtime telemetry capture --run-dir data/runs/run_deterministic_72ca609c --dry-run
```

This prints the output directory, planned files, and any warnings detected in the source series.

### Gap Handling

Pass `--gap-fill-nan` to replace `NaN` / `Infinity` values with `0` and emit a `nan_fill` warning in the manifest:

```bash
flowtime telemetry capture \
  --run-dir data/runs/run_deterministic_72ca609c \
  --gap-fill-nan
```

Without the flag the CLI surfaces `nan_detected` warnings but preserves the original values.

Additional gap tooling:

- `--gap-detect` emits `data_gap` warnings for empty bins while preserving the gap (CSV writes an empty value).
- `--gap-fill-gaps` both records `data_gap` warnings and fills empty bins with `0`, keeping downstream consumers aligned with canonical series length.

## Output Layout

```
telemetry/<runId>/
  manifest.json
  OrderService_arrivals.csv
  OrderService_served.csv
  ...
```

Each CSV follows the capture schema (`bin_index,value`) and the manifest lists the full file inventory plus provenance (run id, scenario hash, captured timestamp).

## Template Generation

Use the helper script to call FlowTime-Sim with parameters that reference the captured bundle:

```bash
scripts/time-travel/run-sim-template.sh \
  --template-id it-system-microservices \
  --telemetry-dir data/telemetry/run_deterministic_72ca609c \
  --telemetry-param telemetryRequestsSource=OrderService_arrivals.csv \
  --out-dir out/templates/it-system-microservices
```

Key notes:

- `--telemetry-param` resolves relative paths inside the capture directory and applies the required `file://` prefix.
- Use `--literal-param` for string overrides and `--json-param` for arrays/numeric values when templates need additional inputs.
- The script writes `model.yaml` and `provenance.json` to the chosen output directory; pass `--verbose` to surface Sim CLI diagnostics.

## Bundle for Engine

Use the generator to combine the capture bundle and Sim model into a canonical engine run:

```csharp
var builder = new TelemetryBundleBuilder();
var result = await builder.BuildAsync(new TelemetryBundleOptions
{
    CaptureDirectory = "data/telemetry/run_deterministic_72ca609c",
    ModelPath = "out/templates/it-system-microservices/model.yaml",
    OutputRoot = "data/runs",
    DeterministicRunId = true
});
```

This produces `data/runs/<runId>/model/model.yaml` where every telemetry binding is normalised to `file://telemetry/<file>.csv`. The generator copies the captured CSVs into `model/telemetry/` and emits `telemetry-manifest.json` alongside the canonical model so `StateQueryService` can replay the bundle without touching the original capture directory.

Once the bundle is written, the run is ready for `/state` queries and UI inspection just like any engine-produced run.

## Run Orchestration (API + CLI)

Milestone M-03.04 layers the capture + bundling steps behind a shared `RunOrchestrationService`. Operators can now create canonical runs directly through the API or the CLI wrapper without scripting the intermediate steps.

### API workflow

`POST /v1/runs` accepts a template id, optional parameter overrides, telemetry bindings, and orchestration options. The example below replays the `it-system-microservices` template against the deterministic capture bundle checked into `examples/time-travel/it-system-telemetry/` (includes `manifest.json` plus the 9 CSVs required by the topology semantics):

```http
POST http://localhost:8080/v1/runs
Content-Type: application/json

{
  "templateId": "it-system-microservices",
  "mode": "telemetry",
  "telemetry": {
    "captureDirectory": "/workspaces/flowtime-vnext/examples/time-travel/it-system-telemetry",
    "bindings": {
      "telemetryRequestsSource": "LoadBalancer_arrivals.csv"
    }
  },
  "options": {
    "deterministicRunId": true,
    "overwriteExisting": true
  }
}
```

- Set `options.dryRun = true` to surface a plan without writing to disk. The response includes the resolved telemetry bindings (converted to `file://` URIs), the planned run directory, and any manifest warnings.
- `GET /v1/runs` returns the canonical summary list (run id, template metadata, creation timestamp, warning count).
- Add `mode`, `templateId`, `hasWarnings`, `page`, and `pageSize` query parameters to slice the listing (defaults: `page=1`, `pageSize=50`, capped at 200).
- `GET /v1/runs/{runId}` mirrors the metadata envelope returned by `/state`, making it safe for operators or UI clients to validate provenance before replaying a run.
- If the capture directory is missing `manifest.json`, the service returns `422` with a helpful error describing the missing artifact.
- `/v1/runs/{runId}/state` validates the generated model; the bundled it-system template includes the required semantics, but a custom template without `semantics.errors` on `service` nodes still triggers `409 Conflict`.
- Run orchestration emits `run_created` / `run_failed` structured logs (template id, run id, mode) so you can wire the API into existing monitoring pipelines.

### CLI parity

`flowtime telemetry run` delegates to the same orchestration service, so flags map directly to the JSON payload above:

```bash
# Preview the plan (no filesystem changes)
flowtime telemetry run \
  --template-id it-system-microservices \
  --capture-dir examples/time-travel/it-system-telemetry \
  --bind telemetryRequestsSource=LoadBalancer_arrivals.csv \
  --dry-run

# Create the canonical bundle with deterministic run id
flowtime telemetry run \
  --template-id it-system-microservices \
  --capture-dir examples/time-travel/it-system-telemetry \
  --bind telemetryRequestsSource=LoadBalancer_arrivals.csv \
  --deterministic-run-id \
  --overwrite
```

When `--dry-run` is omitted the command prints the created run id, run directory, and telemetry resolution flag. Supply `--run-id` (optionally with `--overwrite`) to control the folder name, matching the `options.runId`/`options.overwriteExisting` fields on the API.

## Next Steps

- Extend integration tests to replay captured bundles through `StateQueryService` (tracked under M-03.02).
- When ADX-based loaders arrive, keep this CLI as a regression aid to produce deterministic telemetry fixtures.

## Observability & Troubleshooting

Telemetry capture and bundling surface their status in three places:

- **CLI output** – `flowtime telemetry capture` prints every planned CSV along with any structured warning codes (for example `nan_detected`, `data_gap`). When warnings appear, check `manifest.json` for the same entries so downstream systems can reason about them.
- **Bundle manifest** – `manifest.json` includes `warnings[]` and per-file hashes. A quick `jq '.warnings' manifest.json` should be part of your runbook; if the array is non-empty, `/state` will echo the underlying issues.
- **State API** – `/state` responses mirror telemetry provenance. `metadata.telemetrySourcesResolved` reports whether all sources were found, while each `node.telemetry.warnings[]` contains the warnings generated during capture. The new golden tests (`TelemetryStateGoldenTests`) confirm that these flags stay aligned with the manifest.

If a capture fails, re-run with `--dry-run --verbose` to see the planned operations without deleting any files. Missing CSVs or mismatched hashes typically indicate an inconsistent engine run; regenerate the canonical artifacts before capturing again.
