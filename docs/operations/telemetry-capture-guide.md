# Telemetry Capture Guide

The telemetry capture CLI converts canonical engine runs into telemetry bundles that FlowTime-Sim templates (and, later, live loaders) can consume. Use this guide to run the tooling, inspect the outputs, and understand how warnings surface.

## Prerequisites

- A deterministic engine run directory produced by `flowtime run` (for example `data/runs/run_deterministic_72ca609c`).
- `.NET 9.0` SDK (already required for the FlowTime CLI).
- Optional: set `FLOWTIME_DATA_DIR` to override the default data root (`./data`).
- API deployments can configure `TelemetryRoot` when they want to maintain a shared library of telemetry bundles; by default, generated bundles live alongside the source run under `data/run_<id>/model/telemetry/`.
- **Classes (forward reference):** Runs now emit declared `classes` in `run.json`/`manifest.json`; telemetry bundles will include the same class inventory so downstream loaders can align per-class series in CL-M-04.04.

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
- `manifest.json` — bundle inventory + warning list (schema version **2**, now includes `supportsClassMetrics`, `classes`, and per-file `classId` metadata).
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
- The CLI prints a class-metrics summary (`supportsClassMetrics`, coverage, declared classes) and merges any loader warnings (missing classes, conservation mismatches) into the run manifest so you can catch issues immediately.

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

### Manifest (Schema v2) Quick Reference

Telemetry manifests now follow schema version **2**. Two additions matter for class-aware telemetry:

- `supportsClassMetrics` (boolean, required): declares whether every CSV includes per-class data. When `true`, the manifest must also include `classes`, `classCoverage`, and every entry in `files[]` must name the `classId` the CSV represents.
- `classCoverage` (enum: `full|partial|missing`): mirrors the run metadata so ingestion/loop validation can fail fast when a class is absent.

Example excerpt:

```json
{
  "schemaVersion": 2,
  "supportsClassMetrics": true,
  "classes": ["Retail", "Wholesale"],
  "classCoverage": "full",
  "files": [
    {
      "nodeId": "OrderService",
      "metric": "Arrivals",
      "path": "OrderService_arrivals_Retail.csv",
      "classId": "Retail",
      "hash": "sha256:…",
      "points": 288
    },
    {
      "nodeId": "OrderService",
      "metric": "Arrivals",
      "path": "OrderService_arrivals_Wholesale.csv",
      "classId": "Wholesale",
      "hash": "sha256:…",
      "points": 288
    }
  ]
}
```

Legacy bundles (without class metrics) set `supportsClassMetrics=false` and omit the `classes`/`classCoverage` block; their CSV inventory continues to list totals-only files.

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

## Run Import (Replay)

Template-driven orchestration now lives in FlowTime-Sim (`POST /api/v1/orchestration/runs`). Once a canonical bundle exists, the Engine API simply ingests it. `/v1/runs` no longer accepts `templateId` or parameter payloads — it expects either a bundle directory that already contains `model/`, `run.json`, `telemetry/`, etc., or a base64-encoded zip archive of that directory.

### API workflow

Use `bundlePath` when the engine host can see the canonical bundle on disk:

```http
POST http://localhost:8080/v1/runs
Content-Type: application/json

{
  "bundlePath": "/sim-data/runs/run_sim-order_ea2cfcb7532f49d6b1ab98b9c7e4f5f5",
  "overwriteExisting": false
}
```

- `bundlePath` must point to a directory that already looks like `runs/<id>/` (contains `run.json`, `model/model.yaml`, telemetry manifest, etc.). The Engine copies the directory into its `ArtifactsDirectory`/data root.
- If the bundle lives on another machine, zip it and send the contents via `bundleArchiveBase64`. The archive should contain a single canonical run folder.
- `overwriteExisting=true` replaces an existing run directory; otherwise the import fails with `409 Conflict`.

Example archive request:

```http
POST http://localhost:8080/v1/runs
Content-Type: application/json

{
  "bundleArchiveBase64": "<base64 zip payload>",
  "overwriteExisting": true
}
```

`GET /v1/runs` and `GET /v1/runs/{runId}` continue to include the `telemetry` availability block, so the UI and CLI still know whether replay artifacts exist. Configure `ArtifactsDirectory` (or `FLOWTIME_DATA_DIR`) on the Engine host so imports land alongside the rest of your bundles; FlowTime-Sim should point at the same physical directory (via `FLOWTIME_SIM_DATA_DIR`) if you want new runs to appear automatically.

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
