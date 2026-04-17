# FlowTime as a Headless Service & Integration Architecture

Research notes on FlowTime's readiness for headless operation, single-executable
distribution, container deployment, and integration with external orchestration
systems (Liminara, OTP/BEAM, general pipeline tooling).

---

## Current State: What's Already There

FlowTime's architecture is layered cleanly enough that headless operation is not
aspirational — it's mostly done:

| Layer | Project | Headless-ready |
|-------|---------|----------------|
| Pure computation | `FlowTime.Core` | Yes — no I/O, no side effects, deterministic |
| Orchestration | `FlowTime.Generator` | Yes — template → model → evaluate → artifacts |
| REST API | `FlowTime.API` (40+ endpoints) | Yes — minimal API, health checks, OpenAPI |
| CLI | `FlowTime.Cli` | Yes — fully headless, all features via flags |
| Storage | `FlowTime.Contracts` | Pluggable — filesystem + SQLite backends |

The API already binds to `0.0.0.0:8081`, exposes `/healthz` endpoints, accepts
config via environment variables, and has pluggable storage. The CLI can execute
models from YAML with deterministic seeds and no UI involvement.

### API Surface (summary)

- **Run management:** create, list, get, import, export (CSV/Parquet/NDJSON)
- **State queries:** time-series queries with bin/time filtering, windowed queries
- **Metrics:** SLA compliance, utilization, latency — computed per run
- **Graph:** DAG visualization data (nodes, edges)
- **Telemetry:** capture generation from completed runs
- **Artifacts:** full CRUD with provenance relationships, bulk operations, search
- **Templates:** refresh, generate from parameters
- **Health:** simple + enhanced (git hash, version, build time)

### CLI Capabilities

```bash
flowtime run <model.yaml> --out ./results --verbose --seed 42
flowtime run --template-id capacity-v2 --mode simulation --param-file params.json
flowtime artifacts list --type telemetry --after 2026-01-01
```

No UI dependency. All features accessible via flags. Deterministic run IDs
available via `--deterministic-run-id`.

---

## Deployment Options

### 1. Docker Container (works today)

Standard multi-stage .NET build. No code changes required.

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/FlowTime.API -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://0.0.0.0:8081
EXPOSE 8081
ENTRYPOINT ["dotnet", "FlowTime.API.dll"]
```

Mount a volume for `/data/runs` or configure storage via environment variables.

For batch pipeline use, containerize the CLI instead:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/FlowTime.Cli -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:9.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "flowtime.dll", "run"]
```

Input: model YAML + optional param file (mounted or piped).
Output: artifacts directory.

### 2. Single Self-Contained Executable

.NET 9 supports this natively — no code changes needed:

```bash
# API as single binary
dotnet publish src/FlowTime.API -c Release \
  --self-contained -r linux-x64 \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true

# CLI as single binary
dotnet publish src/FlowTime.Cli -c Release \
  --self-contained -r linux-x64 \
  -p:PublishSingleFile=true
```

Produces a single ~80-100MB binary (includes .NET runtime). Zero dependencies
on the target machine. Can be `scp`'d anywhere and run.

Target RIDs: `linux-x64`, `linux-arm64`, `osx-arm64`, `win-x64`.

### 3. NativeAOT (future — smallest footprint)

.NET 9 supports NativeAOT for minimal APIs. Would produce:
- ~20-30MB binary
- Sub-100ms startup (vs. ~1-2s with JIT)
- No .NET runtime dependency at all

Requires auditing dependencies for AOT compatibility:
- YamlDotNet — needs source generators or reflection-free mode
- JsonSchema.Net — uses reflection, may need work
- SQLite provider — native interop, generally AOT-compatible

Estimate: a few days of work to resolve trimming warnings and validate. Not
urgent — the self-contained single-file approach covers most use cases.

---

## Integration Patterns

### Pattern A: Sidecar API

Run FlowTime API alongside another system. Communicate via HTTP.

```
┌────────────────────┐     HTTP      ┌──────────────┐
│  Orchestrator      │──────────────→│ FlowTime API │
│  (any language)    │←──────────────│  :8081       │
└────────────────────┘               └──────────────┘
```

- Works today with zero changes
- ~10-50ms per request (fine for anything above 1-second intervals)
- Stateless per request; state lives in filesystem/storage backend
- Container orchestrators (Docker Compose, K8s) handle lifecycle

### Pattern B: OTP Port (stdin/stdout)

Compile FlowTime CLI as a single executable. Erlang/Elixir opens a Port.

```elixir
port = Port.open({:spawn_executable, "./flowtime"},
  [:binary, :exit_status, {:args, ["--port-mode"]}])

# Send model, receive results
Port.command(port, Jason.encode!(model) <> "\n")
receive do
  {^port, {:data, result}} -> Jason.decode!(result)
end
```

Requires adding a `--port-mode` flag to the CLI:
- Read JSON model definitions from stdin (one per line, or length-prefixed)
- Write JSON results to stdout
- Stay alive between evaluations (amortize startup cost)
- Clean shutdown on stdin EOF

**Trade-offs vs. sidecar:**

| | Port | Sidecar |
|---|---|---|
| Latency per eval | ~1-5ms | ~10-50ms |
| State management | Long-lived process, can hold state | Stateless per request |
| Supervision | BEAM supervises directly | Separate lifecycle |
| Streaming | Bidirectional stream, natural backpressure | Polling pattern |
| Complexity | Needs `--port-mode` addition | Works today |
| Fault recovery | BEAM auto-restarts | Container orchestrator restarts |

**Recommendation:** Start with sidecar. Add port mode when latency or
supervision integration matters.

### Pattern C: Embedded Library (NuGet)

For .NET orchestrators, FlowTime.Core can be referenced directly as a library:

```csharp
var model = ModelService.ParseAndConvert(yaml);
var (grid, graph) = ModelParser.ParseModel(model);
var results = RouterAwareGraphEvaluator.Evaluate(grid, graph);
// results: IReadOnlyDictionary<NodeId, Series>
```

No HTTP, no process boundary. Pure in-process computation. This is how the
CLI and API already use the engine internally.

---

## Liminara FlowTime Pack

### Architecture

FlowTime runs as a **sidecar container** alongside the Liminara BEAM node.
The pack's Ops call the FlowTime API via HTTP. The BEAM supervises the
container lifecycle.

```
┌─────────────────────────────────────────────────────┐
│  Liminara Node (BEAM)                               │
│                                                     │
│  ┌─────────────────────────────────────────────┐    │
│  │  flowtime.capacity_watch.v1 (Pack)          │    │
│  │                                             │    │
│  │  ingest ──→ build_model ──→ evaluate ──→    │    │
│  │  detect_anomalies ──→ [Decision] ──→        │    │
│  │  recommend                                  │    │
│  └──────────────────────┬──────────────────────┘    │
│                         │ HTTP                      │
│  ┌──────────────────────▼──────────────────────┐    │
│  │  FlowTime API (sidecar, :8081)              │    │
│  │  - single-file binary or container          │    │
│  │  - /data mounted as shared volume           │    │
│  └─────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────┘
```

### Ops

#### `ft.ingest_telemetry.v1`

Pull metrics from an external source (Prometheus, OTEL collector, CSV export,
database query). Write a telemetry artifact.

- Input: source config (endpoint, query, time range)
- Output: telemetry artifact (time series data, CSV or NDJSON)
- Type: side-effecting (reads external state)

#### `ft.build_model.v1`

Construct a FlowTime model from telemetry + system topology. Can be
AI-assisted (LLM proposes node structure, routing, queue capacities).

- Input: telemetry artifact, topology hints (optional), prior model (optional)
- Output: FlowTime model definition artifact (YAML)
- Type: nondeterministic (AI/human decisions involved)
- Decisions recorded: model structure, parameter choices, calibration

#### `ft.evaluate.v1`

Run FlowTime evaluation on a model. Deterministic — same model + seed = same
output, always.

- Input: model artifact, seed (optional), time range
- Output: state series artifact (per-node time series), metrics artifact
- Type: pure (deterministic computation)
- Calls: `POST /v1/runs` or `POST /v1/run` on sidecar API

#### `ft.detect_anomalies.v1`

Compare current evaluation results against baseline thresholds. Identify
queues building up, SLA breaches forming, utilization spikes.

- Input: state series artifact, baseline config (thresholds, historical norms)
- Output: anomaly report artifact (list of detected issues with severity)
- Type: pure (threshold comparison)

#### `ft.recommend.v1`

Given anomalies, suggest remediations. Can be AI-assisted.

- Input: anomaly report, model definition, system context
- Output: recommendation artifact (ranked actions with predicted impact)
- Type: nondeterministic (AI reasoning)

### Pack Definitions

#### `flowtime.capacity_watch.v1` — Continuous Monitoring

```
Pipeline:
  ingest(loop=5m) → build_model → evaluate → detect → [Decision: act?] → recommend

Purpose:
  Continuous flow health monitoring. Ingests telemetry on an interval,
  evaluates the current model, detects emerging bottlenecks, surfaces
  recommendations.

Loop behavior:
  - ingest runs every 5 minutes (configurable)
  - build_model re-runs only when topology changes are detected
  - evaluate runs on each new telemetry window
  - detect compares against rolling baseline
  - recommend triggers only when anomalies exceed threshold
```

#### `flowtime.capacity_planner.v1` — Batch What-If Analysis

```
Pipeline:
  model_artifact → generate_scenarios → batch_evaluate → compare → [Decision: select] → report

Purpose:
  Given an existing model, generate parameter variations (double capacity at
  node X, triple traffic, change routing weights), run all scenarios, compare
  results, present ranked options for human selection.

No loop — runs once per invocation. Each scenario is a separate FlowTime run
with a different seed/parameter set. Results cached as artifacts.
```

#### `flowtime.incident_analyzer.v1` — Reactive Analysis

```
Pipeline:
  alert → fetch_telemetry_window → build_model → evaluate → identify_bottleneck →
  [AI: propose_remediation] → [Decision: human_gate] → action

Purpose:
  Triggered by an external alert. Fetches telemetry for the incident window,
  builds/replays a FlowTime model for that window, identifies where flow broke
  down, proposes fixes.
```

#### `flowtime.model_builder.v1` — Assisted Model Construction

```
Pipeline:
  ingest_telemetry → detect_services → [AI: propose_model] → validate_against_history →
  [Decision: human_adjust] → validate_again → [AI: calibrate_parameters] → final_model

Purpose:
  The creative/nondeterministic process of building a FlowTime model from a
  real system. Every decision is recorded — which services were identified,
  what parameters were chosen, what the human adjusted. Enables branching
  ("what if we'd modeled the retry logic differently?") and replay with new
  telemetry.
```

---

## Streaming: What FlowTime Needs

FlowTime's engine currently evaluates an entire time grid in one pass. For
continuous/streaming operation, several capabilities would extend this:

### Incremental Evaluation

Evaluate only new bins without re-evaluating the entire history.

Current: `Graph.Evaluate(grid)` → all bins.
Needed: `Graph.Evaluate(grid, priorState, fromBin)` → only new bins.

Since nodes are pure functions of their inputs and the grid position, this is
architecturally clean. The prior state is just the output from the last
evaluation — pass it as context, start evaluating from where you left off.

Nodes that depend on cumulative state (queue depths, buffer levels) need their
last-known state as an initial condition. This is already implicit in the
evaluation order — making it explicit is the main work.

### Model Hot-Reload

If the system topology changes (new service discovered, routing rule changed),
the model needs to update without losing accumulated state.

Approach: treat model updates as a new "epoch." Prior state carries forward as
initial conditions. The model definition changes, but the time grid continues.

### Streaming Telemetry Ingestion

Current ingestion is batch (load CSV files, import archives). For continuous
operation, need a way to push data points incrementally.

Options:
- Append to existing telemetry files (simple, filesystem-based)
- POST endpoint that accepts incremental data points
- Watch a directory for new data files (simple producer/consumer)

### Threshold / Anomaly Detection

The engine computes state but doesn't currently classify it. A built-in
detection layer would:
- Compare queue depths, utilization, latency against configurable thresholds
- Detect trends (queue growing for N consecutive bins)
- Emit structured anomaly events

This could live in Core (pure computation on Series data) or as a separate
service. Keeping it in Core preserves the "geometry kernel" nature.

---

## Process Mining Integration

The process mining connection (pm4py as a `:port` from the BEAM) creates a
complete feedback loop:

```
Real system → event logs → Process Mining (discover model)
  → FlowTime (simulate, evaluate) → recommendations → Real system → ...
```

1. Process Mining discovers what actually happens from event logs
2. FlowTime simulates what *could* happen with the discovered model
3. Liminara orchestrates the chain, records decisions at every step

The Process Mining Pack's exported flow artifact is the bridge — a discovered
process model in a format FlowTime can ingest as a simulation model.

Meta-level: Liminara's own event logs (JSONL, timestamped) are themselves
minable. The system can analyze its own execution patterns.

---

## Recommended Sequence

1. **Now:** Write a Dockerfile for FlowTime.API. Validate it starts, responds
   to health checks, can execute a run via the API from outside the container.

2. **Now:** Test single-file self-contained publish for CLI. Verify the binary
   runs on a clean Linux machine with no .NET installed.

3. **First pack:** `flowtime.capacity_planner.v1` (batch, no loop). Proves the
   sidecar pattern, artifact flow, and Op interface without needing streaming.

4. **Second pack:** `flowtime.capacity_watch.v1` (loop). Adds the interval
   dimension. FlowTime still evaluates in batch per interval — no engine
   changes needed, just repeated calls.

5. **Engine streaming:** Add incremental evaluation to Core. This is an
   optimization — the pack architecture doesn't change, evaluations just get
   faster as the time grid grows.

6. **Port mode:** Add `--port-mode` to CLI when the sidecar latency becomes a
   bottleneck or when BEAM supervision of the FlowTime process is desired.

7. **NativeAOT:** When binary size or startup time matters (edge deployment,
   serverless, rapid scaling).

---

## Open Questions

- **Artifact format between systems:** Should FlowTime artifacts (run manifests,
  state series) map 1:1 to Liminara artifacts, or should there be a translation
  layer in the Ops?

- **Model versioning:** When the model-builder pack produces a new model version,
  how does the capacity-watch pack pick it up? Hot-swap, or explicit promotion
  via Decision?

- **Multi-model evaluation:** Can a single FlowTime instance serve multiple
  models concurrently (different systems being monitored)? The API supports this
  (each run is independent), but resource isolation may matter.

- **Telemetry source abstraction:** The ingest Op needs adapters for different
  telemetry sources (Prometheus, OTEL, CSV, database). Should these be separate
  Ops or configuration of a single Op?

- **Backpressure:** If FlowTime evaluation takes longer than the loop interval,
  what happens? Skip the interval? Queue? This is a Liminara runtime concern
  but affects pack design.
