# Telemetry Capture Guide

The telemetry capture CLI converts canonical engine runs into telemetry bundles that FlowTime-Sim templates (and, later, live loaders) can consume. Use this guide to run the tooling, inspect the outputs, and understand how warnings surface.

## Prerequisites

- A deterministic engine run directory produced by `flowtime run` (for example `data/runs/run_deterministic_72ca609c`).
- `.NET 9.0` SDK (already required for the FlowTime CLI).
- Optional: set `FLOWTIME_DATA_DIR` to override the default data root (`./data`).
- API deployments can configure `TelemetryRoot` when they want to maintain a shared library of telemetry bundles; by default, generated bundles live alongside the source run under `data/run_<id>/model/telemetry/`.

## Explicit API Flow (Recommended)

Milestone TT-M-03.17 introduces a first-class API for telemetry generation. Operators (or the UI) request a bundle by calling `POST /v1/telemetry/captures` with a source run id and an output target:

```http
POST /v1/telemetry/captures
Content-Type: application/json

{
  "source": { "type": "run", "runId": "RUN_123" },
  "output": {
    "overwrite": false
  }
}
```

- When `captureKey` and `directory` are omitted, the service writes into the run's own `model/telemetry/` folder. Provide either field only if you want to copy the bundle to a shared library (for example, a curated directory managed by `TelemetryRoot`).
- If a bundle already exists and `overwrite` is `false`, the endpoint returns `409 Conflict` and surfaces the previously recorded metadata.
- Successful requests emit two files next to the generated CSVs inside `model/telemetry/`:
  - `manifest.json` — bundle inventory + warning list (unchanged from earlier tooling).
  - `autocapture.json` — `{ templateId, captureKey?, sourceRunId, generatedAtUtc, rngSeed, parametersHash, scenarioHash }` for provenance.
- When `rng` is not provided, simulation runs default to seed `123`. The UI exposes an “RNG Seed” input so operators can supply a deterministic override.
- The response includes a telemetry summary (`generated`, `alreadyExists`, `generatedAtUtc`, `warningCount`, `sourceRunId`) that is mirrored in `/v1/runs` and UI summaries. No filesystem paths are ever exposed.

The explicit endpoint is now the primary integration point for UI flows. The CLI command described below remains available for local capture or regression scripts. The repository still ships sample bundles under `examples/time-travel/` for templates and documentation, but newly generated telemetry defaults to each run's `model/telemetry/` directory.

## Basic Command

```bash
flowtime telemetry capture \
  --run-dir data/runs/run_deterministic_72ca609c
```

Key behaviour:

- If `--output` is omitted, the tool writes directly into `<run-dir>/model/telemetry/`.
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
  --telemetry-dir data/runs/run_deterministic_72ca609c/model/telemetry \
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
    CaptureDirectory = "data/runs/run_deterministic_72ca609c/model/telemetry",
    ModelPath = "out/templates/it-system-microservices/model.yaml",
    OutputRoot = "data/runs",
    DeterministicRunId = true
});
```

This produces `data/runs/<runId>/model/model.yaml` where every telemetry binding is normalised to `file://telemetry/<file>.csv`. The generator copies the captured CSVs into `model/telemetry/` and emits `telemetry-manifest.json` alongside the canonical model so `StateQueryService` can replay the bundle without touching the original capture directory.

Once the bundle is written, the run is ready for `/state` queries and UI inspection just like any engine-produced run.

## Run Orchestration (Replay)

With explicit telemetry generation in place, `/v1/runs` no longer creates capture bundles. Telemetry runs must reference an existing bundle — either one generated via the API flow above or a hand-produced directory that follows the bundle contract.

### API workflow

`POST /v1/runs` accepts a template id, parameter overrides, telemetry bindings, and orchestration options. For telemetry mode, provide a `captureDirectory` that resolves to an existing bundle (relative capture key or absolute path):

```http
POST http://localhost:8080/v1/runs
Content-Type: application/json

{
  "templateId": "it-system-microservices",
  "mode": "telemetry",
  "telemetry": {
    "captureDirectory": "data/runs/run_deterministic_72ca609c/model/telemetry",
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

- Omit `telemetry.captureDirectory` → `422 Unprocessable Entity` (bundle must exist first).
- `options.dryRun = true` returns a plan without filesystem changes (resolved bindings, warnings, run directory).
- `GET /v1/runs` and `GET /v1/runs/{runId}` now include a `telemetry` block summarising availability (`available`, `generatedAtUtc`, `warningCount`, `sourceRunId`). The UI relies on this metadata to toggle replay actions without revealing directories.
- Configure `TelemetryRoot` in `appsettings.json` (or via environment) only if you want to maintain a shared library of capture bundles. When unset, absolute `captureDirectory` paths (such as the run-local path above) continue to work.

### Telemetry availability metadata

`/v1/runs` responses surface the telemetry summary generated alongside bundles. When the capture endpoint creates a bundle it writes `autocapture.json`; the API mirrors the relevant fields (timestamp, warning count, sourceRunId, rngSeed) and sets `available=true`. If a bundle is deleted out-of-band, the summary reverts to `available=false` the next time the run is scanned.

### CLI parity

`flowtime telemetry run` still shells into the orchestration service, but it now assumes bundles already exist. Use `flowtime telemetry capture` (local CLI) or `POST /v1/telemetry/captures` (API) first, then point the CLI at the capture directory:

```bash
# Preview with an existing bundle (no writes)
flowtime telemetry run \
  --template-id it-system-microservices \
  --capture-dir data/runs/run_deterministic_72ca609c/model/telemetry \
  --bind telemetryRequestsSource=LoadBalancer_arrivals.csv \
  --dry-run

# Replay using a generated bundle
flowtime telemetry run \
  --template-id it-system-microservices \
  --capture-dir data/runs/run_deterministic_72ca609c/model/telemetry \
  --bind telemetryRequestsSource=LoadBalancer_arrivals.csv \
  --deterministic-run-id \
  --overwrite
```

When `--dry-run` is omitted the command prints the created run id, run directory, and telemetry resolution flag. Supply `--run-id` (optionally with `--overwrite`) to control the folder name, matching the `options.runId`/`options.overwriteExisting` fields on the API.

> **Shared bundles:** If you maintain a curated library, point `--capture-dir` at a path under your configured `TelemetryRoot` (or supply `output.captureKey` on the API). Otherwise, the run-local path shown above keeps capture artifacts self-contained.

## Next Steps

- Extend integration tests to replay captured bundles through `StateQueryService` (tracked under M-03.02).
- When ADX-based loaders arrive, keep this CLI as a regression aid to produce deterministic telemetry fixtures.

## Observability & Troubleshooting

Telemetry capture and bundling surface their status in three places:

- **CLI output** – `flowtime telemetry capture` prints every planned CSV along with any structured warning codes (for example `nan_detected`, `data_gap`). When warnings appear, check `manifest.json` for the same entries so downstream systems can reason about them.
- **Bundle manifest** – `manifest.json` includes `warnings[]` and per-file hashes. A quick `jq '.warnings' manifest.json` should be part of your runbook; if the array is non-empty, `/state` will echo the underlying issues.
- **State API** – `/state` responses mirror telemetry provenance. `metadata.telemetrySourcesResolved` reports whether all sources were found, while each `node.telemetry.warnings[]` contains the warnings generated during capture. The new golden tests (`TelemetryStateGoldenTests`) confirm that these flags stay aligned with the manifest.

If a capture fails, re-run with `--dry-run --verbose` to see the planned operations without deleting any files. Missing CSVs or mismatched hashes typically indicate an inconsistent engine run; regenerate the canonical artifacts before capturing again.
