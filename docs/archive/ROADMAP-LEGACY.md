# FlowTime Engine Roadmap

> **⚠️ IMPORTANT CHARTER UPDATE**  
> This roadmap is **superseded by the FlowTime-Engine Charter** (September 2025).  
> **👉 See [Charter Roadmap](milestones/CHARTER-ROADMAP.md)** for current milestone sequence and artifacts-centric paradigm.  
> **👉 See [FlowTime-Engine Charter](flowtime-engine-charter.md)** for the new architectural vision.  
>  
> This document remains for **historical reference and M-0-M-02.06 completed milestones**.  
> **New development follows charter milestones: M-02.07 (Registry) → M-02.08 (Charter UI) → M-02.09 (Compare) → SIM-M-03.00.**

> **Version:** 1.0 (Legacy - Charter Superseded)
> **Audience:** Engineers and architects implementing FlowTime.
> **Purpose:** Defines a sequenced, detailed roadmap with milestones M-0–M-15, interleaving Core/Engine, UI, Service/API, data adapters, scenarios, and calibration. Each milestone includes requirements, inputs/outputs, new code/files, and acceptance criteria.

---

## Principles

### Spreadsheet Metaphor

FlowTime behaves like a **spreadsheet for flows**:

- Deterministic evaluation, grid-aligned.
- Cells = time-bins, formulas = expressions, PMFs, built-ins.
- Graph nodes reference each other like spreadsheet cells.

## UI-M-0 — Minimal Observer UI (Completed / Expanded)

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

### Catalog.v1 (required, domain-neutral)

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

## Architecture

For detailed architectural guidance including artifact-first integration patterns, schema contracts, and FlowTime-Sim integration specifications, see:

- [Integration Architecture (archived)](flowtime-sim-integration-spec-legacy.md)

Key principles:
- **Artifact-first:** All integration through canonical run artifacts, not custom JSON blobs
- **Schema versioning:** v1 contract with backward compatibility requirements  
- **Deterministic hashing:** Content-based artifact identification and caching

---

# Milestones

---

## M-0 — Foundation: Canonical Grid, Series, Graph Skeleton

### Goal

Establish the minimum viable FlowTime engine: arrays, DAG evaluation, deterministic output.
This is the “Hello World” of FlowTime.

### Functional Requirements

- **FR-M-0-1:** Fixed canonical grid (`bins`, `bin_minutes`).
- **FR-M-0-2:** `Series<T>` = contiguous numeric vector aligned to grid.
- **FR-M-0-3:** Graph with nodes/edges, topological sort, deterministic `Evaluate()`.
- **FR-M-0-4:** Node kinds:

  - `ConstSeriesNode(id, values[])`
  - `BinaryOpNode(id, left, right, op)` where op ∈ {Add, Mul}. In M-0 YAML, scalar RHS is supported (e.g., "name \* 0.8"); a full expression parser is planned for M-01.
- **FR-M-0-5:** CLI `run` command: load YAML model → emit CSV.

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
- CSV from the CLI is the canonical reference artifact in M-0; later API JSON/CSV must match it byte-for-byte (content-wise) for the same model.

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

## SVC-M-00.00 — Minimal API (FlowTime.API) ✅ COMPLETED

### Goal

Introduce a thin, stateless HTTP surface so everything can be driven via API early. Enables UI to talk to the API and automation to call runs without the CLI. Hosting is implementation-agnostic (Functions, ASP.NET Core, or other), exposed via FlowTime.API.

**Status**: All functional requirements implemented and verified.

### Functional Requirements

- POST `/run` — input: model YAML (body or multipart); output: JSON with series values and basic metadata; optional zipped CSV artifact.
- GET `/graph` — returns compiled node graph (nodes, edges) for explainability.
- GET `/healthz` — simple health check.
- Optional: POST `/negotiate` stub for future real-time (returns a placeholder payload).

### Inputs

- Same YAML as M-0 (camelCase), including the minimal `expr` support already accepted in M-00.

### Outputs

- JSON response: grid, outputs as arrays, and file paths if writing CSV to a temp container.
- Optional zipped CSVs for download.

### New Code/Files

```
src/FlowTime.API/
  RunHandler.cs         # POST /run
  GraphHandler.cs       # GET /graph
  HealthHandler.cs      # GET /healthz
  appsettings.json      # dev-only config (or equivalent)
```

### Acceptance Criteria

- Posting a valid model returns deterministic outputs and 200 OK.
- `/graph` mirrors the CLI’s internal plan (ids, inputs, edges).
- Runs locally with a lightweight host (Functions, ASP.NET Core, etc.); minimal logging and clear error messages.

#### Parity and no-drift

- Single source of truth: both CLI and API call FlowTime.Core for evaluation; no duplicated logic.
- Contract tests compare CLI CSV vs API JSON/CSV for identical models; results must match.
- Versioned output schema; breaking changes gated behind a version flag.
- CLI integration: add optional `--via-api <url>` mode to route runs through the API for parity checks; default remains local execution to avoid coupling and keep offline support.

---

## **SVC-SIM-PROXY (Optional) — FlowTime Service → FlowTime-Sim proxy**

### Goal

Expose `/sim/*` on **FlowTime Service**, forwarding to **FlowTime-Sim**, storing artifacts beside engine runs under the same `runs/<id>/` root.

### Why

Single backend origin & auth, private Sim topology, unified RBAC/audit, one run catalog.

### Endpoints (proxy)

- `POST /sim/run` → forwards to Sim; persists artifacts under `runs/<simRunId>/…`
- `GET /sim/runs/{id}/index` → proxies `runs/<id>/series/index.json`
- `GET /sim/runs/{id}/series/{seriesId}` → proxies CSV/Parquet
- `GET /sim/scenarios` → proxies list
- `GET /sim/stream` → proxy streaming of pre-binned time series data (optional)

### Acceptance

CLI/API parity; artifacts normalized; unified logs/auth.

---

## UI-M-0 — Minimal Observer UI ✅ COMPLETED

### Goal

Provide a first, minimal visualization: plot CSV outputs in a SPA (planned for a later milestone; not part of M-0).

**Status**: All functional requirements implemented and verified.

### Functional Requirements

- SPA (Blazor WASM).
- Load outputs from API/CLI runs.
- Display time-series in line chart.

### Inputs

- Prefer API (SVC-M-00.00) when available; fallback to a local CSV file produced by the CLI. Outputs must be identical for the same model.

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

## SYN-M-00.00 — Synthetic Adapter (File) ✅ COMPLETED

### Acceptance criteria

**Status**: All functional requirements implemented and verified.

- **Reads FlowTime/Sim file artifacts** from disk and exposes **typed, grid-aligned series**:

  - `runs/<runId>/run.json`
  - `runs/<runId>/series/index.json`
  - `runs/<runId>/series/*.csv`
- Deterministic: repeated reads produce byte-identical re-exports.
- Handles missing optional series gracefully (e.g., no backlog yet).
- CI fixture: one golden run read & re-served via API.

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

## M-1 — Contracts Parity (Artifacts Alignment) ✅ COMPLETED

### Goal

Lock the core run artifact contract in parity with the simulator (FlowTime-Sim) so future UI & adapter work can rely on stable shapes. Introduce dual-write structured artifacts (run + manifest + index) and schema validation without introducing expression features early. This is an interposed milestone; numbering of later milestones remains unchanged.

> Contract Source of Truth: Field-level semantics live in [contracts.md](reference/contracts.md). This section enumerates required presence & ordering only; do not replicate or drift field definitions here.

**Status**: All functional requirements implemented and verified.

### Functional Requirements

- **FR-M-1-1:** Persist spec.yaml verbatim (normalized line endings, original content) alongside artifacts for reproducibility and overlay derivations.
- **FR-M-1-2:** Dual-write run artifact directory: `out/<runId>/` containing:

  - `spec.yaml` (original submitted/derived simulation spec)
  - `run.json` (high-level summary + series listing)
  - `manifest.json` (determinism + integrity metadata)
  - `series/index.json` (series index for quick discovery)
  - Per-series CSV: `series/<seriesId>.csv` (schema: `t,value`)
  - Placeholder directories for `gold/` (future analytics formats)
- **FR-M-1-3:** run.json per authoritative contracts.md schema:

  - Add `source` field = "engine"
  - Add `engineVersion` (current version string)
  - Expand `grid` with `timezone: "UTC"` and `align: "left"`
  - Include `warnings` array for normalization notes
  - Optional `modelHash` (engine may include)
- **FR-M-1-4:** manifest.json per authoritative schema:

  - Remove required `modelHash` (optional for engine)
  - Use `rng` object: `{kind: "pcg32", seed: 12345}` instead of separate fields
  - Include `scenarioHash`, `seriesHashes`, `eventCount`, `createdUtc`
- **FR-M-1-5:** series/index.json per authoritative schema:

  - Include full series metadata: `componentId`, `class`, `kind`, `unit`, `points`, `hash`
  - Add `formats.goldTable` placeholder (path, dimensions, measures) even if not producing Parquet yet
  - Use canonical units: **entities/bin** for flows, **entities** for levels, **minutes** for latency
- **FR-M-1-6:** Adopt seriesId naming convention `measure@componentId[@class]`; for M-1 temporary mapping: use node IDs as componentId namespace until component modeling exists.
- **FR-M-1-7:** Enhanced deterministic hashing with full normalization (trim trailing whitespace, collapse consecutive blank lines, ignore YAML key ordering recursively).
- **FR-M-1-8:** JSON Schema validation: schemas under `docs/schemas/` (run.schema.json, manifest.schema.json, series-index.schema.json); CI tests validate artifacts against schemas.
- **FR-M-1-9:** CLI flags: `--no-manifest` (skips manifest only), `--deterministic-run-id <id>`, optional placeholders for `--no-gold`, `--no-events`.
- **FR-M-1-10:** Backward compatibility: existing single CSV output behavior preserved; dual-write artifacts are authoritative for downstream tooling.

### Inputs

- Existing M-0 YAML models (no new schema fields required).
- Optional flag toggles: `--no-manifest` (tests verify absence), `--deterministic-run-id <seed>` (optional explicit override for reproducible directory name; if omitted derives from modelHash+seed).

### Outputs

Directory layout example:

```
out/run_20250101T120000Z_ab12cd34/
  spec.yaml
  run.json
  manifest.json
  series/
    index.json
    demand@COMP_1.csv
    served@COMP_1.csv
  gold/
    (placeholder directory for future analytics formats)
```

run.json (illustrative minimal fields):

```json
{
  "schemaVersion": 1,
  "runId": "run_20250101T120000Z_ab12cd34",
  "engineVersion": "0.1.0",
  "source": "engine",
  "grid": { "bins": 4, "binMinutes": 60, "timezone": "UTC", "align": "left" },
  "scenarioHash": "sha256:...",
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
    { "id": "served", "path": "series/served.csv" }
  ],
  "events": { "schemaVersion": 0, "fieldsReserved": ["entityType","routeId","stepId","componentId","correlationId"] }
}
```

manifest.json (illustrative):

```json
{
  "schemaVersion": 1,
  "scenarioHash": "sha256:...",
  "rng": { "kind": "pcg32", "seed": 123456789 },
  "seriesHashes": { 
    "demand@COMP_1": "sha256:...", 
    "served@COMP_1": "sha256:..." 
  },
  "eventCount": 0,
  "createdUtc": "2025-01-01T12:00:00Z"
}
```

series/index.json (illustrative):

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
docs/contracts/run.schema.json
docs/contracts/manifest.schema.json
docs/contracts/series-index.schema.json
docs/contracts.md (updated with new artifact shapes and field glossary)
```

### Acceptance Criteria

- Running the existing hello model produces the dual-write directory with all three JSON files and per-series CSVs under `series/`.
- JSON Schemas validate in CI (failing test if contract drift occurs without schema update).
- simulator vs engine artifact parity test passes (structural & hash equivalence where expected).
- Hash stability: reordering YAML keys does not change `modelHash`; changing a numeric literal does.
- `--no-manifest` suppresses `manifest.json` only; other artifacts still emitted.
- All prior tests remain green; no expression parser added ahead of original M-01.

### Notes

- This milestone intentionally avoids adding new functional modeling features; it focuses purely on artifact/contract stability to unblock UI + cross-tooling.
- Event emission & enriched event fields will activate in later milestones (backlog, routing) without breaking changes due to reserved placeholders.
- schemaVersion remains `1`; future breaking schema evolution will bump to `2` (e.g., when backlog/latency structural changes occur).

---

## M-01.05 — Focused Expression Language ✅ COMPLETED

### Goal

Add basic expression language with SHIFT operator only. Deliberately focused to avoid over-engineering.

**Status**: All functional requirements implemented, tested, and performance validated.

### Functional Requirements

- **FR-M-1-1:** Expression parser (`+ - * /`, MIN, MAX, CLAMP).
- **FR-M-1-2:** Node reference resolution (`expr: "demand * 0.8 + SHIFT(demand,-1)"`).
- **FR-M-1-3:** Built-in function: `SHIFT(series, k)` for time-series offsetting.
- **FR-M-1-4:** Basic stateful node interface (minimal, for SHIFT only).

**Scope Limitation:** Advanced retry modeling capabilities deferred to M-09.05. YAGNI principle applied.

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
src/FlowTime.Core/Expressions/
  ExpressionParser.cs
  Builtins.cs
src/FlowTime.Core/Nodes/
  ExprNode.cs
  ShiftNode.cs
tests/FlowTime.Tests/ExpressionTests.cs
```

### Acceptance Criteria

- Expressions with references work.
- SHIFT operator validated with lag behavior.
- No over-engineering for future retry modeling needs.

---

## UI-M-1 — Template-Based Simulation Runner (Completed)

### Goal

Enable template-based simulation workflow with parameter configuration and catalog selection.

### Functional Requirements (Achieved)

- **FR-UI-M-1-1:** Template gallery with search and categorization ✅
- **FR-UI-M-1-2:** Dynamic parameter forms generated from JSON schemas ✅
- **FR-UI-M-1-3:** System catalog selection with detailed information ✅
- **FR-UI-M-1-4:** Complete simulation workflow execution ✅
- **FR-UI-M-1-5:** Realistic simulation results display ✅

### Inputs

- Template selection from gallery
- User-configured parameters via dynamic forms
- Selected system catalog

### Outputs

- Simulation execution with realistic mock results
- Statistical analysis and performance metrics
- Model metadata and execution information

### New Code/Files (Delivered)

```
ui/FlowTime.UI/Pages/TemplateRunner.razor
ui/FlowTime.UI/Components/Templates/TemplateGallery.razor
ui/FlowTime.UI/Components/Templates/DynamicParameterForm.razor
ui/FlowTime.UI/Components/Templates/CatalogPicker.razor
ui/FlowTime.UI/Components/Templates/SimulationResults.razor
ui/FlowTime.UI/Services/TemplateServices.cs
ui/FlowTime.UI/Services/TemplateServiceImplementations.cs
ui/FlowTime.UI/wwwroot/css/app.css (enhanced)
```

### Acceptance Criteria (Met)

- ✅ Template gallery displays available simulation templates
- ✅ Parameter forms auto-generate from JSON schemas with validation
- ✅ Catalog selection provides system information and capabilities
- ✅ Simulation execution returns realistic FlowTime-style results
- ✅ UI is responsive and handles errors gracefully
- ✅ All components integrate into cohesive three-column workflow

### Implementation Notes

- Used mock services for UI-M-1 to enable independent UI development
- Template Runner accessible at `/template-runner` route
- Professional MudBlazor components with consistent theming
- Fixed critical infinite loop bug and improved dropdown UX

---

## M-2 — PMF Support

### Goal

Introduce PMF nodes for probabilistic modeling.

### Functional Requirements

- **FR-M-2-1:** Parse `{ value: prob }`, normalize.

- **FR-M-2-2:** Emit expected value series.

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

## UI-M-2 — Real API Integration ✅

### Goal

Replace mock services with real FlowTime-Sim API integration, implementing artifact-first architecture for live simulation execution. Transform the Template Runner from prototype to production-ready with full HTTP service integration while enhancing UX and component reliability.

### Functional Requirements

- **FR-UI-M-2-1:** Replace mock services with real FlowTime-Sim API calls ✅
- **FR-UI-M-2-2:** Implement artifact-first result loading via series/index.json ✅
- **FR-UI-M-2-3:** Add FlowTimeSimApiClient for /sim/run execution and CSV streaming ✅
- **FR-UI-M-2-4:** Enhance mode switching with reliable component state management ✅
- **FR-UI-M-2-5:** Add comprehensive error handling and graceful API fallbacks ✅
- **FR-UI-M-2-6:** Expand test coverage for critical artifact endpoints ✅

### Inputs

- UI-M-1 Template Runner with mock service layer
- FlowTime-Sim service endpoints (/sim/run, /sim/runs/{id}/index, /sim/scenarios)
- Integration specification for artifact-first communication patterns

### Outputs

- Live simulation execution via real FlowTime-Sim APIs
- Artifact-first result loading from canonical run artifacts
- HTTP-based template and catalog services with real data
- Enhanced UX with mode switching and disabled state feedback
- Expanded test coverage (33→35 tests) with API endpoint validation
- Production-ready service architecture with graceful error handling

### New Code/Files

```
ui/FlowTime.UI/Services/Http/
  FlowTimeSimApiClient.cs       # Real API integration
  SimResultsService.cs          # Artifact-first result loading
ui/FlowTime.UI/Services/
  (Updated) Template/Catalog services with HTTP calls
ui/FlowTime.UI/Components/
  (Enhanced) All components with real API integration
src/FlowTime.API/Services/
  RunArtifactWriter.cs          # Shared artifact generation
tests/FlowTime.Api.Tests/
  ArtifactEndpointTests.cs      # Critical endpoint coverage
scripts/
  README.md + integration tests # Organized development workflow
```

### Acceptance Criteria

- Template Runner executes real simulations via FlowTime-Sim APIs ✅
- Results loaded from canonical artifacts (series/index.json + CSV streaming) ✅
- Real templates loaded from /sim/scenarios endpoint ✅
- All mock services replaced with HTTP implementations ✅
- Artifact-first architecture: no custom JSON blobs, canonical contracts only ✅
- Mode switching works reliably with proper component remounting ✅
- Error handling: 404 for missing artifacts, graceful API fallbacks ✅
- Test coverage expanded with critical API endpoint validation ✅
- End-to-end integration testing with running FlowTime-Sim service ✅

---

## **M-3 — Backlog v1 + Latency + Artifact Endpoints**

### Goal

Introduce **state level** (`backlog`) and derived **latency**; publish `series/index.json`; expose **artifact endpoints** from the service.

### Why

Users ask “what’s pending and how long?” early; the cost is modest (single recurrence); index simplifies UI/adapters; endpoints reduce drift.

### Functional Requirements (Core)

- `backlog[t] = max(0, backlog[t-1] + inflow[t] - served[t])`
- `latency[t] = (served[t] == 0) ? 0 : backlog[t] / served[t] * binMinutes`
- Emit `runs/<runId>/series/index.json` listing actual outputs and **units**:

  - flows: **entities/bin**
  - levels: **entities**
  - derived latency: **minutes**
- Per-series CSVs under `runs/<runId>/series/*.csv` (InvariantCulture formatting)

### Functional Requirements (Service/API) — **SVC-M-01.00** ✅

- `GET /runs/{runId}/index` → returns `runs/<runId>/series/index.json` ✅
- `GET /runs/{runId}/series/{seriesId}` → streams CSV ✅
- (optional) `POST /compare` returns minimal deltas for common series

**Status**: COMPLETED - Artifact endpoints implemented with SYN-M-00.00 adapters, full test coverage.

### Inputs

Existing models; no schema change required.

### Outputs

`series/backlog.csv`, `series/latency.csv`, updated `series/index.json`, `run.json` warnings when applicable.

### New Code/Files

`BacklogNode`, `LatencyDerived`, `SeriesIndexBuilder`; API handlers for artifact endpoints.

### Acceptance Criteria

- **Conservation:** `cum(inflow) − cum(served) ≈ backlog[last]`
- **No div-by-zero crash:** latency set to 0 when `served[t]==0`; `run.json` warns for affected bins
- **Parity:** CLI/API produce identical artifacts; existing tests remain green

---

## UI-M-3 — Run Metadata Viewer

### Goal

UI shows run metadata.

### Functional Requirements

- Display run.json contents.

- Warn if PMF normalized.

---

## M-4 — Scenarios & Compare

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

## UI-M-4 — Scenario Runner

### Goal

UI can run scenarios and compare results.

### Functional Requirements

- Editor for overlay YAML.

- Side-by-side charts.

---

## **SYN-M-01.00 — Synthetic Adapter (Stream)**

### Goal

Stream **pre-binned time series data** from external processors (e.g., from FlowTime-Sim or other gold metric producers) and update in-memory series incrementally for live demos.

### Acceptance criteria

- Order-independent within a bin; watermark yields consistent slices.
- Resume from last watermark without duplication.
- Parity check: accumulated file after stream == file produced for the same seed/run.
- **Note**: Raw event processing happens OUTSIDE FlowTime - adapters consume only gold metrics (time-binned data).

### Code

```
src/FlowTime.Adapters.Synthetic/Stream/*
```

---

## M-5 — Routing, Fan-out, Capacity Caps

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

## UI-M-5 — Routing Visualizer

### Goal

Visual graph view.

### Functional Requirements

- Display nodes as boxes, edges as lines.

- Hover: show series previews.

---

## M-6 — Lag/Shift (Batch Windows)

### Goal

Model periodic batch processing.

### Functional Requirements

- BatchGateNode:

  - open/close windows.

  - release retained arrivals during open.

### Acceptance Criteria

- Batch spikes visible in series.

---

## UI-M-6 — Batch Indicators

### Goal

UI overlays shaded regions for batch windows.

---

## M-06.05 — Cross-System Integration

### Goal

Validate FlowTime artifacts can be consumed by FlowTime-Sim and vice versa.

### Functional Requirements

- **FR-M-06.05-1:** Artifact schema compatibility tests
- **FR-M-06.05-2:** Series index interoperability validation  
- **FR-M-06.05-3:** Joint scenario execution (FlowTime model → FlowTime-Sim validation)
- **FR-M-06.05-4:** Grid alignment validation between systems
- **FR-M-06.05-5:** RNG determinism compatibility (PCG32 seeding)

### Inputs

YAML models compatible with both FlowTime and FlowTime-Sim:

```yaml
grid: { bins: 24, binMinutes: 60 }
nodes:
  - id: service
    kind: expr
    expr: "MIN(capacity, arrivals)"
```

### Outputs

- Cross-system compatibility report
- Schema validation results
- Joint execution test results

### New Code/Files

```
tests/FlowTime.Integration/
  CrossSystemCompatibilityTests.cs
  SchemaValidationTests.cs
scripts/
  validate-flowtime-sim-compat.sh
```

### Acceptance Criteria

- FlowTime artifacts successfully consumed by FlowTime-Sim
- FlowTime-Sim artifacts successfully consumed by FlowTime
- Grid definitions compatible between systems
- RNG seeding produces consistent results
- Schema versions aligned across both systems

---

## M-7 — Backlog v2 (Buffers & Spill)

### Goal

Advanced queue semantics with finite buffers and overflow handling.

### Functional Requirements

- **FR-M-7-1:** Finite buffer queues with capacity limits
- **FR-M-7-2:** Overflow/spill handling when buffers exceed capacity
- **FR-M-7-3:** Buffer utilization metrics and reporting
- **FR-M-7-4:** Dead letter queue (DLQ) for dropped items
- **FR-M-7-5:** Enhanced conservation validation with spill accounting

### Inputs

YAML models with buffer specifications:

```yaml
nodes:
  - id: service_queue
    kind: backlog
    capacity: 1000           # Maximum queue depth
    spillPolicy: drop        # drop | redirect | dlq
    dlq: service_dlq        # Target for spilled items
```

### Outputs

- Enhanced backlog series with capacity constraints
- Spill/drop metrics per queue
- DLQ depth and utilization reports
- Buffer utilization trends

### New Code/Files

```
src/FlowTime.Core/Nodes/
  FiniteBacklogNode.cs      # Buffer-constrained queue
  SpillPolicyNode.cs        # Overflow handling logic
  DlqNode.cs                # Dead letter queue implementation
src/FlowTime.Core/Metrics/
  BufferUtilizationCalc.cs  # Buffer metrics and reporting
```

### Acceptance Criteria

- Buffer capacity limits enforced correctly
- Spill policies handle overflow as specified  
- Conservation holds: arrivals = served + queued + spilled
- DLQ accumulates dropped items accurately
- Performance scales with buffer size, not total volume

---

## UI-M-7 — Buffer & Spill Visualization

### Goal

Show buffer utilization and spill indicators in queue visualizations.

### Functional Requirements

- Buffer capacity bars showing current vs maximum depth
- Spill rate indicators and DLQ accumulation charts
- Color-coded overflow warnings when approaching capacity
- Historical buffer utilization trends

### Acceptance Criteria

- Buffer capacity visually distinguishable from infinite queues
- Spill events highlighted in timeline visualization
- DLQ growth patterns clearly visible

---

## M-8 — Multi-Class + Priority/Fairness

### Goal

Support multiple flow classes with capacity sharing policies.

### Functional Requirements

- **FR-M-8-1:** Class-aware series (`arrivals@serviceA@VIP`, `arrivals@serviceA@STANDARD`).
- **FR-M-8-2:** Weighted-fair capacity sharing when capacity binds.
- **FR-M-8-3:** Strict priority with overflow to lower classes.
- **FR-M-8-4:** Foundation for per-class retry behavior (implementation in M-09.05).

### Inputs

YAML with class definitions:

```yaml
classes: ["VIP", "STANDARD"]
nodes:
  - id: service
    kind: expr
    expr: |
      # Class-aware expressions
      served_vip := MIN(capacity * 0.7, arrivals_vip)
      served_std := MIN(capacity * 0.3, arrivals_std)
```

### Acceptance Criteria

- Multi-class conservation holds per class and in aggregate
- Priority/fairness policies respected under capacity constraints
- Foundation established for per-class retry behavior (full implementation in M-09.05)

---

## M-08.05 — Multi-Class + Priority/Fairness (UI)

### Goal

Support multiple classes with fairness/priority.

### Functional Requirements

- Series2D\[class, t].

- Weighted fair or strict priority serving.

### Acceptance Criteria

- VIP SLA maintained under priority.

---

## UI-M-8 — Class Segmentation

### Goal

Color-series by class.

---

## M-9 — Data Import & Fitting

### Goal

Run models against real telemetry.

### Functional Requirements

- CSV adapter: Gold schema.

- Resample.

- Capacity smoothing.

### Acceptance Criteria

- Modeled queues approximate telemetry.

---

## UI-M-9 — Telemetry Overlay

### Goal

Overlay real telemetry vs model.

---

## M-09.05 — Retry & Feedback Modeling

### Goal

Enable temporal feedback loops through causal delay operators.

**Note:** Deferred from original M-04.05 position to align with FlowTime-Sim SIM-M-6 timeline and avoid over-engineering early milestones.

### Functional Requirements

- **FR-M-09.05-1:** CONV operator (`retries := CONV(errors, [0.0, 0.6, 0.3, 0.1])`).
- **FR-M-09.05-2:** DELAY operator for arbitrary time shifting.
- **FR-M-09.05-3:** RETRY built-in function with attempt limits and DLQ.
- **FR-M-09.05-4:** EMA operator for smoothed feedback signals.
- **FR-M-09.05-5:** Conservation validation (arrivals + retries - served - ΔQ - dlq ≈ 0).
- **FR-M-09.05-6:** Integration with FlowTime-Sim retry pattern generation.

### Inputs

YAML model with retry patterns:

```yaml
nodes:
  - id: service
    kind: expr
    expr: |
      arrivals_total := arrivals + retries
      attempts := MIN(capacity, arrivals_total)  
      errors := attempts * fail_rate
      retries := CONV(errors, [0.0, 0.6, 0.3, 0.1])
      served := attempts - errors
```

### Outputs

- Series with retry echoes across time bins
- Conservation reports showing flow accounting
- Compatible artifacts for FlowTime-Sim validation

### New Code/Files

```
src/FlowTime.Core/Operators/
  ConvNode.cs               # CONV operator implementation
  DelayNode.cs              # DELAY operator implementation
  RetryNode.cs              # RETRY built-in function
  EmaNode.cs                # Exponential moving average
src/FlowTime.Core/Validation/
  ConservationValidator.cs  # Flow conservation validation
tests/FlowTime.Tests/
  RetryModelingTests.cs     # Comprehensive retry tests
  ConservationTests.cs      # Conservation law validation
```

### Acceptance Criteria

- Single-pass evaluation maintained (no algebraic loops)
- Retry volumes match convolution kernel exactly
- Conservation laws verified for complex retry scenarios
- Integration validated with FlowTime-Sim SIM-M-6+ retry patterns
- Performance maintained despite temporal complexity

### Dependencies

- **Prerequisite:** M-06.05 (Cross-System Integration) for FlowTime-Sim compatibility
- **Prerequisite:** M-8 (Multi-Class) for per-class retry behavior
- **Aligned with:** FlowTime-Sim SIM-M-6 retry pattern generation

---

## M-10 — Scenario Sweeps

### Goal

Sensitivity analysis across parameter ranges.

### Functional Requirements

- CLI sweep with param expansion.

- Emit sweep.csv, sweep.html.

### Acceptance Criteria

- Multiple runs executed.

- CSV aggregated.

---

## UI-M-10 — Sweep Explorer

### Goal

Interactive parameter sweeps viewer.

---

## M-11 — Headless Runner (CLI/API)

### Goal

Enable automation.

### Functional Requirements

- CLI: run, compare, sweep, learn.

- API: GET /graph, POST /run, GET /state\_window.

### Acceptance Criteria

- API usable by UI.

---

## UI-M-11 — API Integration

### Goal

UI consumes real API instead of local stub.

---

## **UI-SIM-M-1 — Scenario Browser & Sim Runner (targets FlowTime-Sim SIM-SVC-M-2)**

### Goal

From FlowTime UI, list FlowTime-Sim presets, tweak parameters, start a **Sim run**, and visualize outputs like engine runs.

### Functional Requirements

- **Mode toggle:** Analyze (engine) vs **Simulate** (sim)
- Scenario list (`/sim/scenarios`), minimal parameter form, **Run**
- After run: poll `/sim/runs/{id}/index`, render charts
- Catalog merges sources with **source tag** (`engine`/`sim`)

### Acceptance Criteria

Sim artifacts render via SYN-M-00.00; compare works between Sim ↔ engine.

---

## **UI-SIM-M-2 — Live Stream Viewer (targets FlowTime-Sim SIM-SVC-M-5)**

### Goal

Connect to `/sim/stream` (streaming pre-binned time series data + watermarks), render incrementally, and **snapshot** to artifacts.

### Functional

Live/paused, watermark progress, “Snapshot to run” → writes `runs/<id>/…`.

### Acceptance

Incremental view stable; final snapshot equals file-generated pack for same seed.

---

## M-12 — Templating v2

### Goal

Reusable model templates.

### Functional Requirements

- Template YAML expansion.

- Parameters per type.

---

## UI-M-12 — Template Explorer

### Goal

List and instantiate templates in UI.

---

## M-13 — Backlog v3 (Priority Spill & Class Depth)

### Goal

Multi-class queue management with priority-based spill policies and per-class depth tracking.

### Functional Requirements

- **FR-M-13-1:** Per-class queue depth tracking and reporting
- **FR-M-13-2:** Priority-based spill policies (VIP items spill last)
- **FR-M-13-3:** Class-aware buffer allocation and sharing
- **FR-M-13-4:** Advanced DLQ routing based on class and priority
- **FR-M-13-5:** Multi-class conservation validation with priority accounting

### Inputs

YAML models with class-aware buffer policies:

```yaml
classes: ["VIP", "STANDARD"]
nodes:
  - id: priority_queue
    kind: backlog
    capacity: 1000
    classPolicy:
      VIP: { priority: 1, minReserved: 200 }      # Higher priority, reserved space
      STANDARD: { priority: 2, minReserved: 0 }   # Lower priority, shared space
    spillPolicy: priority                          # Spill lowest priority first
```

### Outputs

- Per-class queue depth series
- Priority-based spill reports showing which classes dropped
- Class utilization efficiency metrics
- Priority violation reports (VIP items dropped while STANDARD queued)

### New Code/Files

```
src/FlowTime.Core/Nodes/
  PriorityBacklogNode.cs    # Multi-class priority queue
  ClassAwareSpillPolicy.cs  # Priority-based overflow handling
src/FlowTime.Core/Metrics/
  ClassDepthCalculator.cs   # Per-class queue tracking
  PriorityViolationDetector.cs # SLA violation detection
```

### Acceptance Criteria

- Priority spill policies protect higher-class items
- Per-class depth tracking accurate across all classes
- Conservation holds per-class and in aggregate
- VIP items never dropped while STANDARD items remain queued
- Class reservation policies enforced under capacity pressure

### Dependencies

- **Prerequisite:** M-8 (Multi-Class + Priority/Fairness) for class infrastructure
- **Prerequisite:** M-7 (Backlog v2) for buffer and spill foundation

---

## UI-M-13 — Multi-Class Queue Visualization

### Goal

Visualize per-class queue depths and priority spill behavior.

### Functional Requirements

- Stacked area charts showing queue depth by class
- Priority violation alerts when VIP items are dropped
- Class reservation utilization indicators
- Priority spill event timeline with class breakdown

### Acceptance Criteria

- Per-class queue depths clearly distinguished by color/pattern
- Priority violations prominently highlighted in UI
- Class reservation boundaries visible in queue visualizations
- Spill events show which classes were affected

---

## M-14 — Calibration & Drift

### Goal

Close loop with telemetry.

### Functional Requirements

- Learn routing patterns from telemetry.
- Calibrate retry kernels against observed behavior (building on M-09.05 retry modeling).
- Detect model drift in retry rates, latency distributions.
- Drift report with statistical confidence intervals.

---

## UI-M-14 — Drift Dashboard

### Goal

UI shows model vs real error metrics.

---

## M-15 — Uncertainty Bands

### Goal

Model risk ranges.

### Functional Requirements

- Monte Carlo runs.

- Emit P50/P90.

---

## UI-M-15 — Uncertainty Viewer

### Goal

Shade percentile bands on charts.

---

# Future-Proofing Placeholders

- **GA/Optimization:** optional future milestone for genetic algorithms.

- **Distributed Evaluation:** partition across workers.

- **Streaming Ingestion:** real-time incremental simulation.

- **Plugin System:** allow 3rd party nodes.

---

## M-16 — WASM engine (browser-run) — Future

Goal: Run the engine in the browser for interactive what-if modeling and offline demos; keep outputs identical to server runs.

Scope (brief):

- WASM binding exposing the same run API as the server engine; no HTTP.
- UI toggle for Run Mode: Server (API) vs Browser (WASM).
- Target: \~150 nodes × 7–14 days × 5m bins with AOT; parity via shared golden vectors.

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

---
