# Release: SIM‑M0

Date: 2025‑08‑26

## Summary

First working slice of FlowTime‑Sim focused on model‑driven runs. The CLI reads a FlowTime YAML model, calls the FlowTime API `/run`, and writes CSV/JSON outputs. Deterministic and culture‑invariant. Includes an example model and basic tests.

## Features

- CLI (FlowTime.Sim.Cli)
  - Post YAML model to `/run` (Content‑Type: text/plain).
  - Emit CSV (aligned to order and grid) or JSON.
  - `--verbose` prints grid/order and summary.
  - Defaults: FlowTime URL `http://localhost:8080`.
- Example model: `examples/m0.const.yaml` (demand/served).
- Deterministic CSV formatting (InvariantCulture).

## Developer experience

- VS Code tasks: build, test, run examples (localhost or container API).
- Tests (xUnit): CSV writer, arg parsing defaults/verbose, API error surfacing.
- Docs: README updated; milestone notes at `docs/milestones/M0.md`.

## How to try it

1) Run FlowTime.API (sibling repo) on 8080, then from this repo:

```bash
# run example model
 dotnet run --project src/FlowTime.Sim.Cli -- \
  --model examples/m0.const.yaml \
  --flowtime http://flowtime-api:8080 \
  --out out/m0.csv \
  --format csv
```

2) Inspect `out/m0.csv`.

## Known limitations

- No async run flow yet (submit/poll/result).
- No telemetry ingestion (NDJSON/Parquet).
- Minimal node set assumed by FlowTime API M0.

## Next

- SIM‑M1: async run pattern and more negative tests.
- Scenario templates and model generator.
- Future: ingestion of events/"Gold" series.
