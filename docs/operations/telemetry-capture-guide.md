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

## Output Layout

```
telemetry/<runId>/
  manifest.json
  OrderService_arrivals.csv
  OrderService_served.csv
  ...
```

Each CSV follows the capture schema (`bin_index,value`) and the manifest lists the full file inventory plus provenance (run id, scenario hash, captured timestamp).

## Next Steps

- Feed the captured bundle into FlowTime-Sim via `/templates/{id}/generate` for telemetry-mode model authoring.
- Extend integration tests to replay captured bundles through `StateQueryService` (tracked under M-03.02).
- When ADX-based loaders arrive, keep this CLI as a regression aid to produce deterministic telemetry fixtures.
