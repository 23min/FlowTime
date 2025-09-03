# FlowTime OSS Roadmap (Full Master Reference)

> **Version:** 1.0
> **Audience:** Engineers and architects implementing FlowTime.
> **Purpose:** Defines a sequenced, detailed roadmap with milestones M0–M15, interleaving Core/Engine, UI, Service/API, data adapters, scenarios, and calibration. Each milestone includes requirements, inputs/outputs, new code/files, and acceptance criteria.

---

## Principles

### Spreadsheet Metaphor

FlowTime behaves like a **spreadsheet for flows**:

* Deterministic evaluation, grid-aligned.
* Cells = time-bins, formulas = expressions, PMFs, built-ins.
* Graph nodes reference each other like spreadsheet cells.

## UI-M0 — Minimal Observer UI (Completed / Expanded)

### Visualization Early

* UI is not deferred — it’s introduced from the beginning.
* SPA (Blazor WASM). ✅
* Load outputs from API runs (CLI fallback deferred). ✅ (API + Simulation stub toggle)
* Display time-series in line chart. ✅
* Structural graph view (table of nodes + degrees). ➕ (pulled early)
* Micro-DAG visualization (compact SVG). ➕
* Persistent preferences (theme, simulation mode, selected model). ➕
* Simulation mode feature flag with deterministic stub. ➕
* Early visualization validates the model, helps debugging, and makes FlowTime accessible.
* Even a basic charting UI pays dividends for adoption.

### Acceptance Criteria (updated)

* `dotnet run` for API + UI shows demand/served. ✅
* Structural graph invariants test passes. ✅
* Micro-DAG renders sources/sinks distinctly. ✅
* Simulation vs API toggle switches data source. ✅
* Theme + model selection persist across reloads. ✅

### API-First

* All features must be callable via API first.
* CLI, UI, and automation layers build on the same API surface.
* Ensures end-to-end validation from the beginning.

### Expressions as Core

* Expressions (`expr:` fields) make FlowTime “spreadsheet-y.”
* They serve three roles:

  * **Modeling:** encode dependencies and math.
  * **Lineage:** transparent references across nodes.
  * **Teaching/Demo:** intuitive to explain to non-experts.

### Probabilistic Mass Functions (PMFs)

* PMFs can approximate arrival/attempt patterns.
* They are **optional**:

  * Replaceable by real telemetry.
  * Or, telemetry can be reduced to PMFs for input.
* Scoping:

  * Early milestones: simple expected value series.
  * Later: convolution/distribution propagation.

### Catalog.v1 (optional, domain-neutral)

A tiny **structural catalog** can be used by FlowTime (and FlowTime-Sim) for early diagramming and ID consistency:

```yaml
version: 1
components:
  - id: COMP_A
    label: "A"
  - id: COMP_B
    label: "B"
connections:
  - from: COMP_A
    to: COMP_B
classes: [ "DEFAULT" ]
layoutHints:
  rankDir: LR
```

**Acceptance:** `components[].id` maps bijectively (or by defined subset) to `component_id` in series; stable IDs enable elkjs/react-flow layouts.

---

# Milestones

---

## M0 — Foundation: Canonical Grid, Series, Graph Skeleton

### Goal

Establish the minimum viable FlowTime engine: arrays, DAG evaluation, deterministic output.
This is the “Hello World” of FlowTime.

### Functional Requirements

* **FR-M0-1:** Fixed canonical grid (`bins`, `bin_minutes`).
* **FR-M0-2:** `Series<T>` = contiguous numeric vector aligned to grid.
* **FR-M0-3:** Graph with nodes/edges, topological sort, deterministic `Evaluate()`.
* **FR-M0-4:** Node kinds:

  * `ConstSeriesNode(id, values[])`
  * `BinaryOpNode(id, left, right, op)` where op ∈ {Add, Mul}. In M0 YAML, scalar RHS is supported (e.g., "name \* 0.8"); a full expression parser is planned for M1.
* **FR-M0-5:** CLI `run` command: load YAML model → emit CSV.

### Nonfunctional Requirements

* Deterministic eval, clear cycle detection.
* Unit tests: core math, topo sort.
* No allocations per bin inside Evaluate (basic perf hygiene).
* Text editor with YAML schema validation.
* Run button → calls API or simulation (respect flag) and refreshes chart.
* Inline validation & evaluation errors (no stale results on failure).
* Optional toggle between static model selector and editor mode.
* Retain structural graph & micro-DAG panels for edited model.

### Inputs

YAML model (camelCase keys):

```yaml
grid: { bins: 4, binMinutes: 60 }
nodes:
  - id: demand
    kind: const
    values: [10, 20, 30, 40]
  - id: served
    kind: expr
    expr: "demand * 0.8"
outputs:
  - series: served
    as: served.csv
```

### Outputs

* `out/<run>/served.csv` with schema: `t,value`
* Optional stdout run summary when `--verbose` is used.
* CSV from the CLI is the canonical reference artifact in M0; later API JSON/CSV must match it byte-for-byte (content-wise) for the same model.

### New Code/Files

```
src/FlowTime.Core/
  TimeGrid.cs
  Series.cs
  Node.cs (INode, NodeId)
  Graph.cs (TopoSort, Evaluate)
  Nodes/
    ConstSeriesNode.cs
    BinaryOpNode.cs
src/FlowTime.Cli/
  Program.cs
examples/hello/model.yaml
tests/FlowTime.Tests/
  Graph tests (topological order, evaluation)
```

### CLI

```bash
dotnet run --project src/FlowTime.Cli -- run examples/hello/model.yaml --out out/hello
```

### Acceptance Criteria

* CSV matches expected values.
* Cycles detected and rejected.
* Unit tests pass (topo order and evaluation).

---

## SVC-M0 — Minimal API (FlowTime.API)

### Goal

Introduce a thin, stateless HTTP surface so everything can be driven via API early. Enables UI to talk to the API and automation to call runs without the CLI. Hosting is implementation-agnostic (Functions, ASP.NET Core, or other), exposed via FlowTime.API.

### Functional Requirements

* POST `/run` — input: model YAML (body or multipart); output: JSON with series values and basic metadata; optional zipped CSV artifact.
* GET `/graph` — returns compiled node graph (nodes, edges) for explainability.
* GET `/healthz` — simple health check.
* Optional: POST `/negotiate` stub for future real-time (returns a placeholder payload).

### Inputs

* Same YAML as M0 (camelCase), including the minimal `expr` support already accepted in M0.

### Outputs

* JSON response: grid, outputs as arrays, and file paths if writing CSV to a temp container.
* Optional zipped CSVs for download.

### New Code/Files

```
apis/FlowTime.API/
  RunHandler.cs         # POST /run
  GraphHandler.cs       # GET /graph
  HealthHandler.cs      # GET /healthz
  appsettings.json      # dev-only config (or equivalent)
```

### Acceptance Criteria

* Posting a valid model returns deterministic outputs and 200 OK.
* `/graph` mirrors the CLI’s internal plan (ids, inputs, edges).
* Runs locally with a lightweight host (Functions, ASP.NET Core, etc.); minimal logging and clear error messages.

#### Parity and no-drift

* Single source of truth: both CLI and API call FlowTime.Core for evaluation; no duplicated logic.
* Contract tests compare CLI CSV vs API JSON/CSV for identical models; results must match.
* Versioned output schema; breaking changes gated behind a version flag.
* CLI integration: add optional `--via-api <url>` mode to route runs through the API for parity checks; default remains local execution to avoid coupling and keep offline support.

---

## **SVC-SIM-PROXY (Optional) — FlowTime Service → FlowTime-Sim proxy**

### Goal

Expose `/sim/*` on **FlowTime Service**, forwarding to **FlowTime-Sim**, storing artifacts beside engine runs under the same `runs/<id>/` root.

### Why

Single backend origin & auth, private Sim topology, unified RBAC/audit, one run catalog.

### Endpoints (proxy)

* `POST /sim/run` → forwards to Sim; persists artifacts under `runs/<simRunId>/…`
* `GET /sim/runs/{id}/index` → proxies `runs/<id>/series/index.json`
* `GET /sim/runs/{id}/series/{seriesId}` → proxies CSV/Parquet
* `GET /sim/scenarios` → proxies list
* `GET /sim/stream` → proxy SSE/NDJSON (optional)

### Acceptance

CLI/API parity; artifacts normalized; unified logs/auth.

---

## UI-M0 — Minimal Observer UI

### Goal

Provide a first, minimal visualization: plot CSV outputs in a SPA (planned for a later milestone; not part of M0).

### Functional Requirements

* SPA (Blazor WASM).
* Load outputs from API/CLI runs.
* Display time-series in line chart.

### Inputs

* Prefer API (SVC-M0) when available; fallback to a local CSV file produced by the CLI. Outputs must be identical for the same model.

### Outputs

* Line chart with labeled series.

### New Code/Files

```
ui/FlowTime.UI/
  Program.cs
  Services/ApiOrFileService.cs
  Pages/Chart.razor
```

### Acceptance Criteria

* Can run `dotnet run` for API + UI.

* Chart shows demand/served.

---

## SYN-M0 — Synthetic Adapter (File)

### Acceptance criteria

* **Reads FlowTime/Sim file artifacts** from disk and exposes **typed, grid-aligned series**:

  * `runs/<runId>/run.json`
  * `runs/<runId>/series/index.json`
  * `runs/<runId>/series/*.csv`
* Deterministic: repeated reads produce byte-identical re-exports.
* Handles missing optional series gracefully (e.g., no backlog yet).
* CI fixture: one golden run read & re-served via API.

### Code

```
src/FlowTime.Adapters.Synthetic/Files/*
```

`ISeriesReader` (illustrative):

```csharp
public interface ISeriesReader {
  Task<RunManifest> ReadManifestAsync(string runPath);
  Task<SeriesIndex> ReadIndexAsync(string runPath);
  Task<Series> ReadSeriesAsync(string runPath, string seriesId);
}
```

---

## M1 — Contracts Parity (Artifacts Alignment)

### Goal

Lock the core run artifact contract in parity with the simulator (FlowTime-Sim) so future UI & adapter work can rely on stable shapes. Introduce dual-write structured artifacts (run + manifest + index) and schema validation without introducing expression features early. This is an interposed milestone; numbering of later milestones remains unchanged.

### Functional Requirements

* **FR-M1-1:** Persist original spec YAML: save normalized (LF line endings) input model as `spec.yaml` alongside artifacts for overlay derivations.
* **FR-M1-2:** Dual-write run artifact directory: `runs/<runId>/` containing:

  * `spec.yaml` (original model, normalized line endings)
  * `run.json` (summary + series listing + source + warnings + events placeholder)
  * `manifest.json` (determinism + integrity metadata with rng object)
  * `series/index.json` (series index with componentId, class, formats.goldTable placeholder)
  * Per-series CSV: `series/<seriesId>.csv` (schema: `t,value`)
* **FR-M1-3:** run.json fields per authoritative contract:

  * `schemaVersion` (1), `runId`, `engineVersion`, `source` ("engine")
  * `grid` with `bins`, `binMinutes`, `timezone` ("UTC"), `align` ("left")
  * `scenarioHash` (SHA256 of normalized YAML), optional `modelHash`
  * `createdUtc`, `warnings` (array), `series` (array with id, path, unit)
  * `events` object: `schemaVersion` (0), `fieldsReserved` (full contract list)
* **FR-M1-4:** manifest.json fields per contract:

  * `schemaVersion` (1), `scenarioHash`, `rng` object with `kind`/`seed`
  * `seriesHashes` (map of seriesId → SHA256 of CSV file bytes)
  * `eventCount` (0), `createdUtc`
* **FR-M1-5:** series/index.json per contract:

  * `schemaVersion` (1), `grid` (bins, binMinutes, timezone)
  * `series` array with `id`, `kind`, `path`, `unit`, `componentId`, `class`, `points`, `hash`
  * `formats.goldTable` placeholder (path, dimensions, measures) even if file not produced
* **FR-M1-6:** SeriesId naming convention: `measure@componentId[@class]` format; temporary mapping strategy until components exist in modeling (e.g., map node IDs to implicit component namespace).
* **FR-M1-7:** Enhanced deterministic normalization: trim trailing whitespace per line, collapse consecutive blank lines, ignore YAML key ordering recursively.
* **FR-M1-8:** JSON Schema definitions in `docs/schemas/`: `run.schema.json`, `manifest.schema.json`, `series-index.schema.json`; CI validates artifacts.
* **FR-M1-9:** CLI flags: `--no-manifest` (skip manifest only), `--deterministic-run-id`, future `--no-gold`, `--no-events`.
* **FR-M1-10:** Backward compatibility: existing CSV output maintained; artifacts are authoritative for downstream tools.

### Inputs

* Existing M0 YAML models (no new schema fields required).
* Optional flag toggles: `--no-manifest` (tests verify absence), `--deterministic-run-id <seed>` (optional explicit override for reproducible directory name; if omitted derives from modelHash+seed).

### Outputs

Directory layout example:

```
runs/engine_20250101T120000Z_ab12cd34/
  spec.yaml
  run.json
  manifest.json
  series/
    served@COMP_1.csv
    demand@COMP_1.csv
  gold/
    node_time_bin.parquet    # placeholder (path in index, file optional)
  events.ndjson              # placeholder (optional)
```

run.json (illustrative contract fields):

```json
{
  "schemaVersion": 1,
  "runId": "engine_20250101T120000Z_ab12cd34",
  "engineVersion": "0.1.0",
  "source": "engine",
  "grid": { "bins": 4, "binMinutes": 60, "timezone": "UTC", "align": "left" },
  "scenarioHash": "sha256:...",
  "createdUtc": "2025-01-01T12:00:00Z",
  "warnings": [],
  "series": [
    { "id": "demand@COMP_1", "path": "series/demand@COMP_1.csv", "unit": "entities/bin" },
    { "id": "served@COMP_1", "path": "series/served@COMP_1.csv", "unit": "entities/bin" }
  ],
  "events": { 
    "schemaVersion": 0, 
    "fieldsReserved": ["entityType","eventType","componentId","connectionId","class","simTime","wallTime","correlationId","attrs"] 
  }
}
```

manifest.json (illustrative contract fields):

```json
{
  "schemaVersion": 1,
  "scenarioHash": "sha256:...",
  "rng": { "kind": "pcg32", "seed": 12345 },
  "seriesHashes": { 
    "demand@COMP_1": "sha256:...", 
    "served@COMP_1": "sha256:..." 
  },
  "eventCount": 0,
  "createdUtc": "2025-01-01T12:00:00Z"
}
```

series/index.json (illustrative contract fields):

```json
{
  "schemaVersion": 1,
  "grid": { "bins": 4, "binMinutes": 60, "timezone": "UTC" },
  "series": [
    { 
      "id": "demand@COMP_1", 
      "kind": "flow", 
      "path": "series/demand@COMP_1.csv", 
      "unit": "entities/bin",
      "componentId": "COMP_1",
      "class": "DEFAULT",
      "points": 4, 
      "hash": "sha256:..." 
    },
    { 
      "id": "served@COMP_1", 
      "kind": "flow", 
      "path": "series/served@COMP_1.csv", 
      "unit": "entities/bin",
      "componentId": "COMP_1", 
      "class": "DEFAULT",
      "points": 4, 
      "hash": "sha256:..." 
    }
  ],
  "formats": {
    "goldTable": {
      "path": "gold/node_time_bin.parquet",
      "dimensions": ["time_bin", "component_id", "class"],
      "measures": ["arrivals", "served", "errors"]
    }
  }
}
```

### New Code/Files

```
src/FlowTime.Cli/Artifacts/
  RunArtifactWriter.cs
  ManifestWriter.cs
  IndexBuilder.cs
src/FlowTime.Core/Hashing/ModelHasher.cs
tests/FlowTime.Tests/ArtifactContractsTests.cs
tests/FlowTime.Tests/ArtifactDeterminismTests.cs
docs/schemas/run.schema.json
docs/schemas/manifest.schema.json
docs/schemas/series-index.schema.json
docs/contracts.md (updated with new artifact shapes and field glossary)
```

### Acceptance Criteria

* Running the existing hello model produces the dual-write directory with all three JSON files and per-series CSVs under `series/`.
* JSON Schemas validate in CI (failing test if contract drift occurs without schema update).
* simulator vs engine artifact parity test passes (structural & hash equivalence where expected).
* Hash stability: reordering YAML keys does not change `modelHash`; changing a numeric literal does.
* `--no-manifest` suppresses `manifest.json` only; other artifacts still emitted.
* All prior tests remain green; no expression parser added ahead of original M1.

### Notes

* This milestone intentionally avoids adding new functional modeling features; it focuses purely on artifact/contract stability to unblock UI + cross-tooling.
* Event emission & enriched event fields will activate in later milestones (backlog, routing) without breaking changes due to reserved placeholders.
* schemaVersion remains `1`; future breaking schema evolution will bump to `2` (e.g., when backlog/latency structural changes occur).

---

## M1.5 — Expressions & Built-ins

### Goal

Make FlowTime truly “spreadsheet-y” with formula parser and references.

### Functional Requirements

* **FR-M1-1:** Expression parser (`+ - * /`, MIN, MAX, CLAMP).
* **FR-M1-2:** Node reference resolution (`expr: "demand * 0.8 + SHIFT(demand,-1)"`).
* **FR-M1-3:** Built-in function: `SHIFT(series, k)`.

### Inputs

YAML model:

```yaml
grid: { bins: 3, bin_minutes: 60 }
nodes:
  - id: base
    kind: const
    values: [1,2,3]
  - id: shifted
    kind: expr
    expr: "SHIFT(base,1)"
```

### Outputs

CSV: shifted series with lag.

### New Code/Files

```
src/FlowTime.Expressions/
  ExpressionParser.cs
  Builtins.cs
src/FlowTime.Engine/Nodes/
  ExprNode.cs
tests/FlowTime.Tests/ExpressionTests.cs
```

### Acceptance Criteria

* Expressions with references work.
* SHIFT validated.

---

## UI-M1 — Editor Basics

### Goal

Enable model editing directly in the UI.

### Functional Requirements

* Text editor with YAML schema validation.

* Run button → calls API → refresh chart.

* Show errors inline.

### Inputs

* Edited YAML model.

### Outputs

* Updated chart.

* Errors shown.

### New Code/Files

```
ui/FlowTime.UI/Pages/Editor.razor
ui/FlowTime.UI/Services/RunService.cs
```

### Acceptance Criteria

* Models editable & runnable in browser.

---

## M2 — PMF Support

### Goal

Introduce PMF nodes for probabilistic modeling.

### Functional Requirements

* **FR-M2-1:** Parse `{ value: prob }`, normalize.

* **FR-M2-2:** Emit expected value series.

### Inputs

```yaml
nodes:
  - id: attempts
    kind: pmf
    pmf: { "1": 0.6, "2": 0.3, "3": 0.1 }
```

### Outputs

* Expected attempts per bin.

### New Code/Files

```
src/FlowTime.Pmf/
  Pmf.cs
  PmfNode.cs
tests/FlowTime.Tests/PmfTests.cs
```

### Acceptance Criteria

* PMF normalized.

* CSV matches expectation.

---

## UI-M2 — PMF Visualization

### Goal

Add histogram visualization for PMFs.

### Functional Requirements

* Bar chart of PMF values.

* Overlay expected value series.

### Acceptance Criteria

* Histograms render correctly.

---

## **M3 — Backlog v1 + Latency + Series Index & Artifact Endpoints (pulled forward)**

### Goal

Introduce **state level** (`backlog`) and derived **latency**; publish `series/index.json`; expose **artifact endpoints** from the service.

### Why

Users ask “what’s pending and how long?” early; the cost is modest (single recurrence); index simplifies UI/adapters; endpoints reduce drift.

### Functional Requirements (Core)

* `backlog[t] = max(0, backlog[t-1] + inflow[t] - served[t])`
* `latency[t] = (served[t] == 0) ? 0 : backlog[t] / served[t] * binMinutes`
* Emit `runs/<runId>/series/index.json` listing actual outputs and **units**:

  * flows: **entities/bin**
  * levels: **entities**
  * derived latency: **minutes**
* Per-series CSVs under `runs/<runId>/series/*.csv` (InvariantCulture formatting)

### Functional Requirements (Service/API) — **SVC-M1**

* `GET /runs/{runId}/index` → returns `runs/<runId>/series/index.json`
* `GET /runs/{runId}/series/{seriesId}` → streams CSV
* (optional) `POST /compare` returns minimal deltas for common series

### Inputs

Existing models; no schema change required.

### Outputs

`series/backlog.csv`, `series/latency.csv`, updated `series/index.json`, `run.json` warnings when applicable.

### New Code/Files

`BacklogNode`, `LatencyDerived`, `SeriesIndexBuilder`; API handlers for artifact endpoints.

### Acceptance Criteria

* **Conservation:** `cum(inflow) − cum(served) ≈ backlog[last]`
* **No div-by-zero crash:** latency set to 0 when `served[t]==0`; `run.json` warns for affected bins
* **Parity:** CLI/API produce identical artifacts; existing tests remain green

---

## UI-M3 — Run Metadata Viewer

### Goal

UI shows run metadata.

### Functional Requirements

* Display run.json contents.

* Warn if PMF normalized.

---

## M4 — Scenarios & Compare

### Goal

What-if analysis via overlays.

### Functional Requirements

* Overlay YAML schema: adjust demand/capacity.

* Apply overlay within time window.

* `compare` tool: baseline vs scenario.

### Inputs

* model.yaml + overlay.yaml.

### Outputs

* scenario series, delta.csv, kpi.csv.

### New Code/Files

```
src/FlowTime.Engine/Overlay.cs
src/FlowTime.Cli/CompareCommand.cs
```

### Acceptance Criteria

* Scenario runs show correct deltas.

---

## UI-M4 — Scenario Runner

### Goal

UI can run scenarios and compare results.

### Functional Requirements

* Editor for overlay YAML.

* Side-by-side charts.

---

## **SYN-M1 — Synthetic Adapter (Stream)**

### Goal

Consume **NDJSON events** + periodic **watermarks** (e.g., from FlowTime-Sim streaming mode) and update in-memory series incrementally for live demos.

### Acceptance criteria

* Order-independent within a bin; watermark yields consistent slices.
* Resume from last watermark without duplication.
* Parity check: accumulated file after stream == file produced for the same seed/run.

### Code

```
src/FlowTime.Adapters.Synthetic/Stream/*
```

---

## M5 — Routing, Fan-out, Capacity Caps

### Goal

Model flows across multiple paths and capacity limits.

### Functional Requirements

* RouterNode (split by ratio).

* FanOutNode (replicate).

* CapacityNode (clamp inflow vs capacity).

* Overflow series.

### Acceptance Criteria

* Splits sum to 1.

* Overflow computed.

---

## UI-M5 — Routing Visualizer

### Goal

Visual graph view.

### Functional Requirements

* Display nodes as boxes, edges as lines.

* Hover: show series previews.

---

## M6 — Lag/Shift (Batch Windows)

### Goal

Model periodic batch processing.

### Functional Requirements

* BatchGateNode:

  * open/close windows.

  * release retained arrivals during open.

### Acceptance Criteria

* Batch spikes visible in series.

---

## UI-M6 — Batch Indicators

### Goal

UI overlays shaded regions for batch windows.

---

## M7 — Backlog v1 + Latency

### Goal

Introduce real queues and latency metrics.

### Functional Requirements

* BacklogNode:

  * Q\[t] = Q\[t-1] + inflow\[t] - served\[t]
* Latency = Q\[t] / served\[t] \* bin\_minutes.

### Acceptance Criteria

* Conservation holds.

* Latency trends correct.

---

## UI-M7 — Queue Depth Visualization

### Goal

Show backlog as area chart under series.

---

## M8 — Multi-Class + Priority/Fairness

### Goal

Support multiple classes with fairness/priority.

### Functional Requirements

* Series2D\[class, t].

* Weighted fair or strict priority serving.

### Acceptance Criteria

* VIP SLA maintained under priority.

---

## UI-M8 — Class Segmentation

### Goal

Color-series by class.

---

## M9 — Data Import & Fitting

### Goal

Run models against real telemetry.

### Functional Requirements

* CSV adapter: Gold schema.

* Resample.

* Capacity smoothing.

### Acceptance Criteria

* Modeled queues approximate telemetry.

---

## UI-M9 — Telemetry Overlay

### Goal

Overlay real telemetry vs model.

---

## M10 — Scenario Sweeps

### Goal

Sensitivity analysis across parameter ranges.

### Functional Requirements

* CLI sweep with param expansion.

* Emit sweep.csv, sweep.html.

### Acceptance Criteria

* Multiple runs executed.

* CSV aggregated.

---

## UI-M10 — Sweep Explorer

### Goal

Interactive parameter sweeps viewer.

---

## M11 — Headless Runner (CLI/API)

### Goal

Enable automation.

### Functional Requirements

* CLI: run, compare, sweep, learn.

* API: GET /graph, POST /run, GET /state\_window.

### Acceptance Criteria

* API usable by UI.

---

## UI-M11 — API Integration

### Goal

UI consumes real API instead of local stub.

---

## **UI-SIM-M1 — Scenario Browser & Sim Runner (targets FlowTime-Sim SIM-SVC-M2)**

### Goal

From FlowTime UI, list FlowTime-Sim presets, tweak parameters, start a **Sim run**, and visualize outputs like engine runs.

### Functional Requirements

* **Mode toggle:** Analyze (engine) vs **Simulate** (sim)
* Scenario list (`/sim/scenarios`), minimal parameter form, **Run**
* After run: poll `/sim/runs/{id}/index`, render charts
* Catalog merges sources with **source tag** (`engine`/`sim`)

### Acceptance Criteria

Sim artifacts render via SYN-M0; compare works between Sim ↔ engine.

---

## **UI-SIM-M2 — Live Stream Viewer (targets FlowTime-Sim SIM-SVC-M5)**

### Goal

Connect to `/sim/stream` (SSE/NDJSON + watermarks), render incrementally, and **snapshot** to artifacts.

### Functional

Live/paused, watermark progress, “Snapshot to run” → writes `runs/<id>/…`.

### Acceptance

Incremental view stable; final snapshot equals file-generated pack for same seed.

---

## M12 — Templating v2

### Goal

Reusable model templates.

### Functional Requirements

* Template YAML expansion.

* Parameters per type.

---

## UI-M12 — Template Explorer

### Goal

List and instantiate templates in UI.

---

## M13 — Backlog v2 (Multi-Queue + Spill)

### Goal

Advanced queue semantics.

### Functional Requirements

* DLQ, priority spill, finite buffer.

---

## UI-M13 — Spill Indicators

### Goal

Visual DLQ vs main queue.

---

## M14 — Calibration & Drift

### Goal

Close loop with telemetry.

### Functional Requirements

* Learn routing, retries, delay.

* Drift report.

---

## UI-M14 — Drift Dashboard

### Goal

UI shows model vs real error metrics.

---

## M15 — Uncertainty Bands

### Goal

Model risk ranges.

### Functional Requirements

* Monte Carlo runs.

* Emit P50/P90.

---

## UI-M15 — Uncertainty Viewer

### Goal

Shade percentile bands on charts.

---

# Future-Proofing Placeholders

* **GA/Optimization:** optional future milestone for genetic algorithms.

* **Distributed Evaluation:** partition across workers.

* **Streaming Ingestion:** real-time incremental simulation.

* **Plugin System:** allow 3rd party nodes.

---

## M16 — WASM engine (browser-run) — Future

Goal: Run the engine in the browser for interactive what-if modeling and offline demos; keep outputs identical to server runs.

Scope (brief):

* WASM binding exposing the same run API as the server engine; no HTTP.
* UI toggle for Run Mode: Server (API) vs Browser (WASM).
* Target: \~150 nodes × 7–14 days × 5m bins with AOT; parity via shared golden vectors.

Deliverables:

* src/FlowTime.Core.Wasm (binding) and UI wiring.
* Tests for parity and basic perf budgets.
* docs/wasm.md with build flags (SIMD, optional AOT).

---

# Repository Layout

```
flowtime/
├─ src/
│  ├─ FlowTime.Engine/
│  ├─ FlowTime.Service/
│  └─ FlowTime.Tests/
├─ ui/FlowTime.UI/
├─ examples/
│  ├─ hello/
│  ├─ expr/
│  ├─ pmf/
│  ├─ scenario/
│  ├─ routing/
│  ├─ batch/
│  ├─ backlog/
│  ├─ multiclass/
│  ├─ gold/
│  ├─ sweep/
│  ├─ templating/
│  ├─ dlq/
│  └─ uncertainty/
├─ docs/
│  ├─ roadmap.md
│  ├─ api.md
│  ├─ quickstart.md
│  ├─ concepts.md
│  └─ scenarios.md
└─ .github/workflows/
   ├─ build.yml
   └─ codeql.yml
```

---
