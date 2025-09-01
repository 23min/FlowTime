> See also `docs/capability-matrix.md` for a current snapshot of implemented vs planned capabilities.
# FlowTime OSS Roadmap (Full Master Reference)

> **Version:** 1.0  
> **Audience:** Engineers and architects implementing FlowTime.  
> **Purpose:** Defines a sequenced, detailed roadmap with milestones M0–M15, interleaving Core/Engine, UI, Service/API, data adapters, scenarios, and calibration. Each milestone includes requirements, inputs/outputs, new code/files, and acceptance criteria.  

---

## Principles

### Spreadsheet Metaphor
FlowTime behaves like a **spreadsheet for flows**:
- Deterministic evaluation, grid-aligned.
- Cells = time-bins, formulas = expressions, PMFs, built-ins.
- Graph nodes reference each other like spreadsheet cells.

## UI-M0 — Minimal Observer UI (Completed / Expanded)
### Visualization Early
- UI is not deferred — it’s introduced from the beginning.  
- SPA (Blazor WASM). ✅
- Load outputs from API runs (CLI fallback deferred). ✅ (API + Simulation stub toggle)
- Display time-series in line chart. ✅
- Structural graph view (table of nodes + degrees). ➕ (pulled early)
- Micro-DAG visualization (compact SVG). ➕
- Persistent preferences (theme, simulation mode, selected model). ➕
- Simulation mode feature flag with deterministic stub. ➕
- Early visualization validates the model, helps debugging, and makes FlowTime accessible.  
- Even a basic charting UI pays dividends for adoption.
### Acceptance Criteria (updated)
- `dotnet run` for API + UI shows demand/served. ✅
- Structural graph invariants test passes. ✅
- Micro-DAG renders sources/sinks distinctly. ✅
- Simulation vs API toggle switches data source. ✅
- Theme + model selection persist across reloads. ✅
### API-First
- All features must be callable via API first.  
- CLI, UI, and automation layers build on the same API surface.  
- Ensures end-to-end validation from the beginning.

### Expressions as Core
- Expressions (`expr:` fields) make FlowTime “spreadsheet-y.”  
- They serve three roles:
  - **Modeling:** encode dependencies and math.
  - **Lineage:** transparent references across nodes.
  - **Teaching/Demo:** intuitive to explain to non-experts.

### Probabilistic Mass Functions (PMFs)
- PMFs can approximate arrival/attempt patterns.  
- They are **optional**:
  - Replaceable by real telemetry.  
  - Or, telemetry can be reduced to PMFs for input.  
- Scoping:
  - Early milestones: simple expected value series.
  - Later: convolution/distribution propagation.

---

# Milestones

---

## M0 — Foundation: Canonical Grid, Series, Graph Skeleton

### Goal
Establish the minimum viable FlowTime engine: arrays, DAG evaluation, deterministic output.  
This is the “Hello World” of FlowTime.

### Functional Requirements
- **FR-M0-1:** Fixed canonical grid (`bins`, `bin_minutes`).
- **FR-M0-2:** `Series<T>` = contiguous numeric vector aligned to grid.
- **FR-M0-3:** Graph with nodes/edges, topological sort, deterministic `Evaluate()`.
- **FR-M0-4:** Node kinds:
  - `ConstSeriesNode(id, values[])`
  - `BinaryOpNode(id, left, right, op)` where op ∈ {Add, Mul}. In M0 YAML, scalar RHS is supported (e.g., "name * 0.8"); a full expression parser is planned for M1.
- **FR-M0-5:** CLI `run` command: load YAML model → emit CSV.

### Nonfunctional Requirements
- Deterministic eval, clear cycle detection.
- Unit tests: core math, topo sort.
- No allocations per bin inside Evaluate (basic perf hygiene).
- Text editor with YAML schema validation.
- Run button → calls API or simulation (respect flag) and refreshes chart.
- Inline validation & evaluation errors (no stale results on failure).
- Optional toggle between static model selector and editor mode.
- Retain structural graph & micro-DAG panels for edited model.

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

- `out/<run>/served.csv` with schema: `t,value`
- Optional stdout run summary when `--verbose` is used.
- CSV from the CLI is the canonical reference artifact in M0; later API JSON/CSV must match it byte-for-byte (content-wise) for the same model.

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

- CSV matches expected values.
- Cycles detected and rejected.
- Unit tests pass (topo order and evaluation).

---

## SVC-M0 — Minimal API (FlowTime.API)

### Goal

Introduce a thin, stateless HTTP surface so everything can be driven via API early. Enables UI to talk to the API and automation to call runs without the CLI. Hosting is implementation-agnostic (Functions, ASP.NET Core, or other), exposed via FlowTime.API.

### Functional Requirements

- POST `/run` — input: model YAML (body or multipart); output: JSON with series values and basic metadata; optional zipped CSV artifact.
- GET `/graph` — returns compiled node graph (nodes, edges) for explainability.
- GET `/healthz` — simple health check.
- Optional: POST `/negotiate` stub for future real-time (returns a placeholder payload).

### Inputs

- Same YAML as M0 (camelCase), including the minimal `expr` support already accepted in M0.

### Outputs

- JSON response: grid, outputs as arrays, and file paths if writing CSV to a temp container.
- Optional zipped CSVs for download.

### New Code/Files

```
apis/FlowTime.API/
  RunHandler.cs         # POST /run
  GraphHandler.cs       # GET /graph
  HealthHandler.cs      # GET /healthz
  appsettings.json      # dev-only config (or equivalent)
```

### Acceptance Criteria

- Posting a valid model returns deterministic outputs and 200 OK.
- `/graph` mirrors the CLI’s internal plan (ids, inputs, edges).
- Runs locally with a lightweight host (Functions, ASP.NET Core, etc.); minimal logging and clear error messages.

#### Parity and no‑drift

- Single source of truth: both CLI and API call FlowTime.Core for evaluation; no duplicated logic.
- Contract tests compare CLI CSV vs API JSON/CSV for identical models; results must match.
- Versioned output schema; breaking changes gated behind a version flag.
- CLI integration: add optional `--via-api <url>` mode to route runs through the API for parity checks; default remains local execution to avoid coupling and keep offline support.

---

## UI-M0 — Minimal Observer UI

### Goal

Provide a first, minimal visualization: plot CSV outputs in a SPA (planned for a later milestone; not part of M0).

### Functional Requirements

- SPA (Blazor WASM).
- Load outputs from API/CLI runs.
- Display time-series in line chart.
    

### Inputs

- Prefer API (SVC-M0) when available; fallback to a local CSV file produced by the CLI. Outputs must be identical for the same model.
    

### Outputs

- Line chart with labeled series.
    

### New Code/Files

```
ui/FlowTime.UI/
  Program.cs
  Services/ApiOrFileService.cs
  Pages/Chart.razor
```

### Acceptance Criteria

- Can run `dotnet run` for API + UI.
    
- Chart shows demand/served.

---

## SYN-M0 — Synthetic Adapter

### Acceptance criteria:

- Reads NDJSON/Parquet in the normalized “Gold event” schema.
- Runs end‑to‑end with sample datasets via CLI.
- CI uses samples to validate stitching + metrics.


---

## M1 — Contracts Parity (Artifacts Alignment)

### Goal

Lock the core run artifact contract in parity with the simulator (FlowTime-Sim) so future UI & adapter work can rely on stable shapes. Introduce dual-write structured artifacts (run + manifest + index) and schema validation without introducing expression features early. This is an interposed milestone; numbering of later milestones remains unchanged.

### Functional Requirements

- **FR-M1-1:** Dual-write run artifact directory: `out/<runId>/` containing:
  - `run.json` (high-level summary + series listing)
  - `manifest.json` (determinism + integrity metadata)
  - `index.json` (series index for quick discovery)
  - Per-series CSV: `<seriesId>.csv` (schema: `t,value`)
- **FR-M1-2:** Deterministic `runId` (timestamp + hash slug or pure hash) and `scenarioHash` (normalized LF YAML of model; excludes whitespace/comments) included in both `run.json` and `manifest.json`.
- **FR-M1-3:** Manifest fields (aligned with simulator current draft):
  - `schemaVersion` (still `1` — no breaking changes)
  - `modelHash` (SHA256 of normalized YAML)
  - `scenarioHash` (same as above or alias; keep both if clarifying)
  - `seed`, `rng`: deterministic seed and algorithm name
  - `seriesHashes`: map of seriesId → SHA256(content) for CSV bytes (LF line endings)
  - `eventCount` (placeholder 0 until event emission added in later milestone) 
  - `generatedAtUtc`
- **FR-M1-4:** `index.json` lists each exported series with: `id`, `path`, `hash`, `points`, optional `units` (null for now), and `kind` (e.g., `const`, `expr`, `pmf_expected` once available; current set minimal: `const`, `expr`).
- **FR-M1-5:** Reserved event schema enrichment placeholders in `run.json` under `events` object (non-breaking, empty arrays/objects): `schemaVersion`, `fieldsReserved` (list containing: `entityType`, `routeId`, `stepId`, `componentId`, `correlationId`). Actual event emission deferred.
- **FR-M1-6:** JSON Schema definitions committed under `docs/contracts/` (or `schemas/`): `run.schema.json`, `manifest.schema.json`, `index.schema.json`; CI test validates produced artifacts against schemas.
- **FR-M1-7:** Parity tests: engine artifacts vs simulator artifacts for equivalent const model produce matching manifest/run structural fields (ignoring engine‑only fields or ordering); hash stability tests (modify model → hash changes; reorder YAML keys → hash unchanged).
- **FR-M1-8:** CLI: still primary interface; expressions still limited to M0 subset (no new parser yet). Add `--emit-manifest` (default on) and `--no-manifest` flag to disable for debugging.
- **FR-M1-9:** Backward compatibility: existing single CSV output path still works; when old `--out <dir>` specified, dual-write occurs inside `<dir>/<runId>/` and top-level CSV behavior remains (symlink or duplicate copy optional; defer if complexity > benefit).

### Inputs

- Existing M0 YAML models (no new schema fields required).
- Optional flag toggles: `--no-manifest` (tests verify absence), `--deterministic-run-id <seed>` (optional explicit override for reproducible directory name; if omitted derives from modelHash+seed).

### Outputs

Directory layout example:

```
out/run_20250101T120000Z_ab12cd34/
  run.json
  manifest.json
  index.json
  demand.csv
  served.csv
```

run.json (illustrative minimal fields):
```json
{
  "schemaVersion": 1,
  "runId": "run_20250101T120000Z_ab12cd34",
  "scenarioHash": "sha256:...",
  "grid": { "bins": 4, "binMinutes": 60 },
  "series": [ { "id": "demand", "path": "demand.csv" }, { "id": "served", "path": "served.csv" } ],
  "events": { "schemaVersion": 0, "fieldsReserved": ["entityType","routeId","stepId","componentId","correlationId"] }
}
```

manifest.json (illustrative):
```json
{
  "schemaVersion": 1,
  "modelHash": "sha256:...",
  "scenarioHash": "sha256:...",
  "seed": 123456789,
  "rng": "pcg32",
  "seriesHashes": { "demand": "sha256:...", "served": "sha256:..." },
  "eventCount": 0,
  "generatedAtUtc": "2025-01-01T12:00:00Z"
}
```

index.json (illustrative):
```json
{
  "series": [
    { "id": "demand", "path": "demand.csv", "hash": "sha256:...", "points": 4, "kind": "const", "units": null },
    { "id": "served", "path": "served.csv", "hash": "sha256:...", "points": 4, "kind": "expr", "units": null }
  ]
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
docs/contracts/run.schema.json
docs/contracts/manifest.schema.json
docs/contracts/index.schema.json
docs/contracts.md (updated with new artifact shapes and field glossary)
```

### Acceptance Criteria

- Running the existing hello model produces the dual-write directory with all three JSON files and per-series CSVs.
- JSON Schemas validate in CI (failing test if contract drift occurs without schema update).
- simulator vs engine artifact parity test passes (structural & hash equivalence where expected).
- Hash stability: reordering YAML keys does not change `modelHash`; changing a numeric literal does.
- `--no-manifest` suppresses `manifest.json` only; other artifacts still emitted.
- All prior tests remain green; no expression parser added ahead of original M1.

### Notes

- This milestone intentionally avoids adding new functional modeling features; it focuses purely on artifact/contract stability to unblock UI + cross-tooling.
- Event emission & enriched event fields will activate in later milestones (backlog, routing) without breaking changes due to reserved placeholders.
- schemaVersion remains `1`; future breaking schema evolution will bump to `2` (e.g., when backlog/latency structural changes occur).

---

## M1.5 — Expressions & Built-ins

### Goal

Make FlowTime truly “spreadsheet-y” with formula parser and references.

### Functional Requirements

- **FR-M1-1:** Expression parser (`+ - * /`, MIN, MAX, CLAMP).
- **FR-M1-2:** Node reference resolution (`expr: "demand * 0.8 + SHIFT(demand,-1)"`).
- **FR-M1-3:** Built-in function: `SHIFT(series, k)`.
    

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

- Expressions with references work.
- SHIFT validated.
    

---

## UI-M1 — Editor Basics

### Goal

Enable model editing directly in the UI.

### Functional Requirements

- Text editor with YAML schema validation.
    
- Run button → calls API → refresh chart.
    
- Show errors inline.
    

### Inputs

- Edited YAML model.
    

### Outputs

- Updated chart.
    
- Errors shown.
    

### New Code/Files

```
ui/FlowTime.UI/Pages/Editor.razor
ui/FlowTime.UI/Services/RunService.cs
```

### Acceptance Criteria

- Models editable & runnable in browser.
    

---

## M2 — PMF Support

### Goal

Introduce PMF nodes for probabilistic modeling.

### Functional Requirements

- **FR-M2-1:** Parse `{ value: prob }`, normalize.
    
- **FR-M2-2:** Emit expected value series.
    

### Inputs

```yaml
nodes:
  - id: attempts
    kind: pmf
    pmf: { "1": 0.6, "2": 0.3, "3": 0.1 }
```

### Outputs

- Expected attempts per bin.
    

### New Code/Files

```
src/FlowTime.Pmf/
  Pmf.cs
  PmfNode.cs
tests/FlowTime.Tests/PmfTests.cs
```

### Acceptance Criteria

- PMF normalized.
    
- CSV matches expectation.
    

---

## UI-M2 — PMF Visualization

### Goal

Add histogram visualization for PMFs.

### Functional Requirements

- Bar chart of PMF values.
    
- Overlay expected value series.
    

### Acceptance Criteria

- Histograms render correctly.
    

---

## M3 — Foundation Hygiene

### Goal

Trustworthy and consistent runs.

### Functional Requirements

- Rich error messages: cycles, unknown node, mismatch.
    
- Output metadata: `run.json` (grid, hash, timestamp, warnings).
    
- CSV writer with culture-invariant formatting.
    

### Acceptance Criteria

- Invalid models fail clearly.
    
- Metadata complete.
    

---

## UI-M3 — Run Metadata Viewer

### Goal

UI shows run metadata.

### Functional Requirements

- Display run.json contents.
    
- Warn if PMF normalized.
    

---

## M4 — Scenarios & Compare

### Goal

What-if analysis via overlays.

### Functional Requirements

- Overlay YAML schema: adjust demand/capacity.
    
- Apply overlay within time window.
    
- `compare` tool: baseline vs scenario.
    

### Inputs

- model.yaml + overlay.yaml.
    

### Outputs

- scenario series, delta.csv, kpi.csv.
    

### New Code/Files

```
src/FlowTime.Engine/Overlay.cs
src/FlowTime.Cli/CompareCommand.cs
```

### Acceptance Criteria

- Scenario runs show correct deltas.
    

---

## UI-M4 — Scenario Runner

### Goal

UI can run scenarios and compare results.

### Functional Requirements

- Editor for overlay YAML.
    
- Side-by-side charts.
    

---

## M5 — Routing, Fan-out, Capacity Caps

### Goal

Model flows across multiple paths and capacity limits.

### Functional Requirements

- RouterNode (split by ratio).
    
- FanOutNode (replicate).
    
- CapacityNode (clamp inflow vs capacity).
    
- Overflow series.
    

### Acceptance Criteria

- Splits sum to 1.
    
- Overflow computed.
    

---

## UI-M5 — Routing Visualizer

### Goal

Visual graph view.

### Functional Requirements

- Display nodes as boxes, edges as lines.
    
- Hover: show series previews.
    

---

## M6 — Lag/Shift (Batch Windows)

### Goal

Model periodic batch processing.

### Functional Requirements

- BatchGateNode:
    
    - open/close windows.
        
    - release retained arrivals during open.
        

### Acceptance Criteria

- Batch spikes visible in series.
    

---

## UI-M6 — Batch Indicators

### Goal

UI overlays shaded regions for batch windows.

---

## M7 — Backlog v1 + Latency

### Goal

Introduce real queues and latency metrics.

### Functional Requirements

- BacklogNode:
    
    - Q[t] = Q[t-1] + inflow[t] - served[t]
        
- Latency = Q[t] / served[t] * bin_minutes.
    

### Acceptance Criteria

- Conservation holds.
    
- Latency trends correct.
    

---

## UI-M7 — Queue Depth Visualization

### Goal

Show backlog as area chart under series.

---

## M8 — Multi-Class + Priority/Fairness

### Goal

Support multiple classes with fairness/priority.

### Functional Requirements

- Series2D[class, t].
    
- Weighted fair or strict priority serving.
    

### Acceptance Criteria

- VIP SLA maintained under priority.
    

---

## UI-M8 — Class Segmentation

### Goal

Color-series by class.

---

## M9 — Data Import & Fitting

### Goal

Run models against real telemetry.

### Functional Requirements

- CSV adapter: Gold schema.
    
- Resample.
    
- Capacity smoothing.
    

### Acceptance Criteria

- Modeled queues approximate telemetry.
    

---

## UI-M9 — Telemetry Overlay

### Goal

Overlay real telemetry vs model.

---

## M10 — Scenario Sweeps

### Goal

Sensitivity analysis across parameter ranges.

### Functional Requirements

- CLI sweep with param expansion.
    
- Emit sweep.csv, sweep.html.
    

### Acceptance Criteria

- Multiple runs executed.
    
- CSV aggregated.
    

---

## UI-M10 — Sweep Explorer

### Goal

Interactive parameter sweeps viewer.

---

## M11 — Headless Runner (CLI/API)

### Goal

Enable automation.

### Functional Requirements

- CLI: run, compare, sweep, learn.
    
- API: GET /graph, POST /run, GET /state_window.
    

### Acceptance Criteria

- API usable by UI.
    

---

## UI-M11 — API Integration

### Goal

UI consumes real API instead of local stub.

---

## M12 — Templating v2

### Goal

Reusable model templates.

### Functional Requirements

- Template YAML expansion.
    
- Parameters per type.
    

---

## UI-M12 — Template Explorer

### Goal

List and instantiate templates in UI.

---

## M13 — Backlog v2 (Multi-Queue + Spill)

### Goal

Advanced queue semantics.

### Functional Requirements

- DLQ, priority spill, finite buffer.
    

---

## UI-M13 — Spill Indicators

### Goal

Visual DLQ vs main queue.

---

## M14 — Calibration & Drift

### Goal

Close loop with telemetry.

### Functional Requirements

- Learn routing, retries, delay.
    
- Drift report.
    

---

## UI-M14 — Drift Dashboard

### Goal

UI shows model vs real error metrics.

---

## M15 — Uncertainty Bands

### Goal

Model risk ranges.

### Functional Requirements

- Monte Carlo runs.
    
- Emit P50/P90.
    

---

## UI-M15 — Uncertainty Viewer

### Goal

Shade percentile bands on charts.

---

# Future-Proofing Placeholders

- **GA/Optimization:** optional future milestone for genetic algorithms.
    
- **Distributed Evaluation:** partition across workers.
    
- **Streaming Ingestion:** real-time incremental simulation.
    
- **Plugin System:** allow 3rd party nodes.
    

---

## M16 — WASM engine (browser-run) — Future

Goal: Run the engine in the browser for interactive what‑if modeling and offline demos; keep outputs identical to server runs.

Scope (brief):
- WASM binding exposing the same run API as the server engine; no HTTP.
- UI toggle for Run Mode: Server (API) vs Browser (WASM).
- Target: ~150 nodes × 7–14 days × 5m bins with AOT; parity via shared golden vectors.

Deliverables:
- src/FlowTime.Core.Wasm (binding) and UI wiring.
- Tests for parity and basic perf budgets.
- docs/wasm.md with build flags (SIMD, optional AOT).

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
