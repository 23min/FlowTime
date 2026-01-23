# FlowTime Engine Capabilities (Authoritative Snapshot — Jan 2026)

This document describes the **shipped** FlowTime Engine surfaces and behaviors as of November 24, 2025. It is descriptive, not a roadmap. Everything listed here is implemented in code and covered by current schemas/tests. Explicitly omitted items (classes, edge fact tables, streaming, catalog APIs, export/import registry) are **not** supported today.

## Execution model
- **Deterministic, discrete-time DAG** on a fixed grid `{ bins, binSize, binUnit }` (UTC, left-aligned).
- **Node kinds**: const (inline values), expr (limited expression set), **serviceWithBuffer** nodes that own queue/backlog semantics (`queueDepth`, arrivals/served/errors, optional `loss`, dispatch schedules), routers, sinks, and **dependency** nodes (arrivals/served/errors only). Template-derived computed nodes are preserved in artifacts. (See [`docs/architecture/service-with-buffer/service-with-buffer-architecture.md`](../architecture/service-with-buffer/service-with-buffer-architecture.md) for the full contract.)
- **Dependency constraints (Option B)**: services can reference shared constraints (registry) that cap served throughput per bin using proportional allocation; constraints are **not** flow nodes.
- **Expression support** (engine evaluator): arithmetic `+ - * /`, functions `SHIFT`, `CONV`, `MIN`, `MAX`, `CLAMP`, `MOD`, `FLOOR`, `CEIL`, `ROUND`, `STEP`, `PULSE`. No IF/EMA/ABS/SQRT/POW/routers/autoscale nodes yet.
- **Retry/backoff**: Supports attempts/failures/retry echo series; `RetryKernelPolicy` normalizes kernels; derived retry echo and exhaustion warnings recorded when missing.
- **Backlog/latency**: Queue depth/backlog recurrence with optional initial conditions; derived `latencyMinutes`, `throughputRatio`, `flowLatencyMs` in state responses.
- **Classes**: Multi-class flows supported. Templates/tagged nodes emit per-class series; artifacts and `/state(_window)` expose `byClass` arrays plus `classCoverage` metadata. `DEFAULT` remains the fallback for totals.
- **Edges**: Explicit topology edges support `throughput`, `effort`, and `terminal` types. `/state_window` emits edge series (`flowVolume`, `attemptsVolume`, `failuresVolume`, `retryRate`, plus derived `retryVolume` where applicable) and edge-quality metadata. No path analytics yet.

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
  - Analyzer/manifest warnings (e.g., router diagnostics, conservation failures) are printed to stdout after a run completes so authors can act before inspecting artifacts; up to five warnings are echoed with codes/node context.
- Sim CLI (`src/FlowTime.Sim.Cli`):
  - `list templates|models`, `show template|model`, `generate [--id --params --out --mode]`, `validate template|params`.
  - `generate` runs `TemplateInvariantAnalyzer` and prints warnings; `validate` checks parameter shapes/bounds.

## State & metrics
- Node metrics exposed in state/state_window: arrivals, served, errors, attempts, failures, exhaustedFailures, retryEcho, queue/backlog, capacity, externalDemand, processingTimeMsSum, servedCount, retryBudgetRemaining, maxAttempts.
- Derived metrics: utilization, latencyMinutes, serviceTimeMs, flowLatencyMs, throughputRatio, retryTax, color (UI aid).
- **Constraints in state/state_window**: per-node `constraints` series (arrivals/served/errors/latency + derived shortfall) and `constraintStatus` (limited flag per bin). Constraints are series-backed resources, not nodes.
- SLA descriptors: completion SLA (with dispatch carry-forward for gated releases), backlog age SLA (marked unavailable when telemetry is missing), and schedule-adherence SLA. SLA payloads include kind + status so UIs can surface "No data" without fabricating values.
- ServiceWithBuffer derivations use the same inputs as service/queue nodes: `latencyMinutes` requires `queueDepth` + `served`, `utilization` requires `capacity` + `served`, and `serviceTimeMs` requires `processingTimeMsSum` + `servedCount`. Missing inputs yield no derived series.
- Edge series (state_window): `flowVolume` for throughput/effort edges, retry dependency edges (`attemptsVolume`, `failuresVolume`, `retryRate`), plus derived `retryVolume` for retries attributed to incoming edges. Edge quality labels (`exact`, `approx`, `partialClass`, `missing`) are included in metadata.
- Warnings: invariants (conservation, queue depth mismatch, missing series, retry kernel policy, telemetry missing), backlog health signals (growth streak, overload ratio, age risk), mode validation, retry kernel adjustments; recorded per-node/global in state responses and `run.json`.

## Telemetry & time-travel
- Canonical run artifacts are the source of truth; `/state` and `/state_window` read from artifacts on disk.
- Telemetry capture/loader tooling ingests bundles that follow `docs/schemas/telemetry-manifest.schema.json` (v2 adds `supportsClassMetrics`, `classes`, `classCoverage`, and per-file `classId`). CLI orchestration can target telemetry mode today; the hosted loader service remains future work.
- Time-travel schemas: `docs/schemas/time-travel-state.schema.json` governs state responses; API matches current state/state_window payloads, including `byClass` series.

## Out of scope / not implemented
- Streaming delivery.
- Per-class filters in API query parameters (UI consumes the per-class data already exposed).
- Edge fact tables (EdgeTimeBin) or path analytics.
- Catalog APIs and registry/export/import loop.
- Advanced expressions (IF/EMA/ABS/SQRT/POW), routers, autoscale nodes.

## Validation & analyzers
- Engine invariants run during artifact writing; warnings persist in `run.json`.
- Template invariant analyzer runs in Sim CLI generation and in orchestration paths; warnings surface on stdout (Sim CLI) and in manifests for runs.
- Router diagnostics (`router_missing_class_route`, `router_class_leakage`) are emitted by the invariant analyzer so CLI, API, and UI surfaces all present the same warnings.
- Configuration precedence is tested in `FlowTime.Api.Tests.ConfigurationTests` and `FlowTime.Cli.Tests.OutputDirectoryConfigurationTests`.
