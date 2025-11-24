# FlowTime Engine Capabilities (Authoritative Snapshot — Nov 2025)

This document describes the **shipped** FlowTime Engine surfaces and behaviors as of November 24, 2025. It is descriptive, not a roadmap. Everything listed here is implemented in code and covered by current schemas/tests. Explicitly omitted items (classes, edge fact tables, streaming, catalog APIs, export/import registry) are **not** supported today.

## Execution model
- **Deterministic, discrete-time DAG** on a fixed grid `{ bins, binSize, binUnit }` (UTC, left-aligned).
- **Node kinds**: const (inline values), expr (limited expression set), backlog/queue semantics via topology semantics (`queueDepth`, `arrivals/served/errors`, optional `externalDemand`, `capacity`, retry fields), and template-derived computed nodes preserved in artifacts.
- **Expression support** (engine evaluator): arithmetic `+ - * /`, functions `SHIFT`, `CONV`, `MIN`, `MAX`, `CLAMP`. No IF/EMA/ABS/SQRT/POW/routers/autoscale nodes yet.
- **Retry/backoff**: Supports attempts/failures/retry echo series; `RetryKernelPolicy` normalizes kernels; derived retry echo and exhaustion warnings recorded when missing.
- **Backlog/latency**: Queue depth/backlog recurrence with optional initial conditions; derived `latencyMinutes`, `throughputRatio`, `flowLatencyMs` in state responses.
- **Classes**: Single class (`DEFAULT`) emitted; no per-class metrics or filtering.
- **Edges**: Only retry-dependency derived edges in `/state_window` (attempts/failures/retryRate from source semantics). No EdgeTimeBin fact tables.

## Artifacts & hashing
- Layout (per run): `spec.yaml`, `model/{model.yaml, metadata.json, provenance.json?}`, `run.json`, `manifest.json`, `series/index.json`, per-series CSVs under `series/`, placeholder `aggregates/` directory.
- `seriesId` format: `measure@componentId[@class]`; allowed chars `[A-Za-z0-9._\-@]`.
- Hashes: `scenarioHash`, optional `modelHash` in `run.json`/`manifest.json`; per-series SHA-256 in `manifest.json` and `series/index.json`.
- CSVs: `t,value` with LF newlines, invariant-culture floats. Determinism tests assert repeatable hashes (see `tests/FlowTime.Cli.Tests` and `tests/FlowTime.Api.Tests`).
- Schemas: `docs/schemas/run.schema.json`, `manifest.schema.json`, `series-index.schema.json`, `model.schema.yaml`, `template.schema.json`, `telemetry-manifest.schema.json`.

## API surfaces (engine)
- `POST /v1/runs` — create a run from a model (supports provenance header/body; writes artifacts).
- `GET /v1/runs/{runId}/graph` — compiled DAG + semantics/aliases.
- `GET /v1/runs/{runId}/state` — single-bin snapshot with derived metrics/warnings.
- `GET /v1/runs/{runId}/state_window` — windowed series; includes retry-derived edges when applicable.
- `GET /v1/runs/{runId}/metrics` — aggregate metrics over a bin range.
- `GET /v1/runs/{runId}/index` — `series/index.json`.
- `GET /v1/runs/{runId}/series/{seriesId}` — CSV stream (URL-decoded `seriesId`).
- No streaming endpoints; no catalog/export/import/registry endpoints.

## CLI surfaces
- Engine CLI (`src/FlowTime.Cli`):
  - `flowtime run <model.yaml> [--out --verbose --deterministic-run-id --seed]`
  - Orchestrated runs: `flowtime run --template-id <id> --mode simulation|telemetry --capture-dir ... [--param-file --bind --run-id --overwrite --dry-run --deterministic-run-id]`
  - `flowtime artifacts list [--template-id --model-id --limit --skip --data-dir]`
  - Output root precedence: `FLOWTIME_DATA_DIR` > `--out` (per run) > defaults (`<repo>/data`).
  - RNG: templates declaring `rng` require a seed in orchestration; the CLI currently lacks an explicit `--rng` flag (run fails if template requires it).
- Sim CLI (`src/FlowTime.Sim.Cli`):
  - `list templates|models`, `show template|model`, `generate [--id --params --out --mode]`, `validate template|params`.
  - `generate` runs `TemplateInvariantAnalyzer` and prints warnings; `validate` checks parameter shapes/bounds.

## State & metrics
- Node metrics exposed in state/state_window: arrivals, served, errors, attempts, failures, exhaustedFailures, retryEcho, queue/backlog, capacity, externalDemand, processingTimeMsSum, servedCount, retryBudgetRemaining, maxAttempts.
- Derived metrics: utilization, latencyMinutes, serviceTimeMs, flowLatencyMs, throughputRatio, retryTax, color (UI aid).
- Edge series (state_window): retry dependency edges only (`attemptsLoad`, `failuresLoad`, `retryRate`, optional `exhaustedFailuresLoad`) based on node semantics and edge multiplier/lag hints.
- Warnings: invariants (conservation, missing series, retry kernel policy, telemetry missing), mode validation, retry kernel adjustments; recorded per-node/global in state responses and `run.json`.

## Telemetry & time-travel
- Canonical run artifacts are the source of truth; `/state` and `/state_window` read from artifacts on disk.
- Telemetry capture/loader service is not implemented; telemetry mode expects prebuilt bundles via orchestration (template + capture directory).
- Time-travel schemas: `docs/schemas/time-travel-state.schema.json` governs state responses; API matches current state/state_window payloads.

## Out of scope / not implemented
- Streaming delivery.
- Per-class metrics or filters.
- Edge fact tables (EdgeTimeBin) or path analytics.
- Catalog APIs and registry/export/import loop.
- Advanced expressions (IF/EMA/ABS/SQRT/POW), routers, autoscale nodes.

## Validation & analyzers
- Engine invariants run during artifact writing; warnings persist in `run.json`.
- Template invariant analyzer runs in Sim CLI generation and in orchestration paths; warnings surface on stdout (Sim CLI) and in manifests for runs.
- Configuration precedence is tested in `FlowTime.Api.Tests.ConfigurationTests` and `FlowTime.Cli.Tests.OutputDirectoryConfigurationTests`.
