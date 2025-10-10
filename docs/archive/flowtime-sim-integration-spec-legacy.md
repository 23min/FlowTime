# FlowTime-Sim ↔ FlowTime Integration — Working Spec (v1.1)

> **Archived:** This document reflects early M0/M1 integration assumptions and is no longer maintained. See `docs/architecture/time-travel/` for current architecture and planning material.

> **📋 Charter Alignment**: This integration specification aligns well with the [FlowTime-Engine Charter](../flowtime-engine-charter.md) artifacts-centric paradigm. The "artifact-first" principle described here directly supports the charter workflow: [Models] → [Runs] → [Artifacts] → [Learn].

**Scope:** Early milestones (UI-M0/1, SVC-M0/1, M0–M3).  
**Principle:** *Artifact-first*. Services are stateless post-run. UI/adapters load artifacts, not ad-hoc JSON.  
**Charter Connection:** Implements Engine+Sim ecosystem with shared artifact contracts supporting registry-based analysis.

# FlowTime-Sim ↔ FlowTime Integration — Working Spec (v1.1)

**Scope:** Early milestones (UI-M0/1, SVC-M0/1, M0–M3).
**Principle:** *Artifact-first*. Services are stateless post-run. UI/adapters load artifacts, not ad-hoc JSON.

---

## 1) Roles (crisp)

* **FlowTime-Sim (Simulator)**

  * Generates **synthetic runs**: per-series CSV time series + JSON manifests; optional streaming later.
  * Accepts scenario/config; **writes** `runs/<runId>/…` (see §3).
  * Returns only a small envelope (`{simRunId}`) and links to the artifacts. **No** custom result JSON that duplicates artifacts.

* **FlowTime (Engine)**

  * Evaluates YAML models (the “spreadsheet”) and can produce **the same artifact shape**.
  * Also **ingests** simulator artifacts via adapters (file/stream) to power the UI and comparisons.

**Key invariant:** Engine and Sim emit **the same artifact contract** so the UI/adapters don’t care who produced a run.

---

## 2) API surfaces (minimal, stable)

### FlowTime-Sim

* `POST /sim/run` → `{ simRunId }` (201 Created)
  **Side effect:** writes `runs/<simRunId>/…` (files below).
* `GET /sim/runs/{id}/index` → `series/index.json` (application/json)
* `GET /sim/runs/{id}/series/{seriesId}` → CSV stream (`t,value`)
* (optional) `GET /sim/scenarios` → scenario presets + knobs
* (optional) `POST /sim/overlay` → derive new run from prior `spec.yaml`
* (optional) `GET /sim/stream?id=…` → SSE/NDJSON with watermarks

### FlowTime (Engine)

* `POST /run` → `{ runId }` (writes artifacts)
* `GET /runs/{id}/index` / `GET /runs/{id}/series/{seriesId}`
* `GET /graph`
* (optional) `POST /compare`

### Optional proxy (single origin)

* FlowTime service may expose `/sim/*` as a transparent proxy; artifacts still land under the same `runs/` root.

---

## 3) Canonical run artifacts (MANDATORY for engine **and** sim)

```
runs/<runId>/
  spec.yaml
  run.json
  manifest.json
  series/
    index.json
    <seriesId>.csv           # schema: t,value; t=0..bins-1
  gold/
    node_time_bin.parquet    # optional early; keep placeholder path in index.json
  events.ndjson              # optional; safe as empty file early
```

### Required fields/conventions

* `schemaVersion: 1` in **run.json**, **manifest.json**, **series/index.json**.
* `runId` format (opaque, recommended): `<source>_<YYYY-MM-DDTHH-mm-ssZ>_<8-char slug>` where `source ∈ {engine, sim}`.
* `run.json` **must** include:

  * `source: "sim"` or `"engine"`, `engineVersion`, `grid{ bins, binMinutes, timezone:"UTC", align:"left" }`,
  * `scenarioHash`, `createdUtc`, `warnings[]`,
  * `series[]` (id, path, unit).
  * `events.fieldsReserved` = `["entityType","eventType","componentId","connectionId","class","simTime","wallTime","correlationId","attrs"]`.
* `manifest.json` **must** include:

  * `rng{ kind:"pcg32", seed }`, `seriesHashes{ seriesId: sha256 }`, `eventCount`, `createdUtc`, `scenarioHash` (and optional `modelHash`).
* `series/index.json` **must** include:

  * `grid`, `series[]` entries with `{ id, kind("flow|state|derived"), path, unit, componentId, class, points, hash }`,
  * `formats.goldTable` placeholder.

### Series ID + units discipline

* `seriesId`: `measure@componentId[@class]`; allowed chars `[A-Za-z0-9._\-@]`.
* **Units:**

  * flows = `entities/bin` (e.g., `arrivals`, `served`, `errors`, `capacity-as-rate`)
  * levels = `entities` (engine: `backlog`; sim may add `queue_depth` in a later version)
  * derived latency = `minutes` (engine’s `latency = backlog/served * binMinutes`)

### Determinism/hashing (both producers)

* Normalize `spec.yaml` (LF, trim EOL, collapse blank lines, ignore YAML key order) for hashing.
* Culture-invariant CSV; exact byte-for-byte reproducibility given same spec+seed.
* Hash bytes of each CSV into `manifest.json.seriesHashes`.

---

## 4) “Mock mode” (what Copilot must do **now**)

Even when returning mock data, the simulator **must write the artifact pack.** Replace the ad-hoc `SimulationRunResult.Metadata` with a tiny envelope plus real files:

**Bad (current mock):** returns JSON metrics, doesn’t write artifacts.
**Good (required mock):** writes the full `runs/<runId>/…`, then returns:

```json
{
  "simRunId": "sim_2025-09-04T10-22-01Z_ab12cd34",
  "indexUrl": "/sim/runs/sim_2025-09-04T10-22-01Z_ab12cd34/index"
}
```

**Mock writer (rules):**

* Create `spec.yaml` from the submitted request (or a recorded template).
* Emit at least 2–3 series under `series/` (e.g., `arrivals@COMP_A.csv`, `served@COMP_A.csv`, optional `capacity@COMP_A.csv`), each with `bins` rows.
* Populate `series/index.json`, `run.json`, `manifest.json` consistently (include hashes).
* Create `gold/` directory and `events.ndjson` (may be empty) to keep shape stable.

> Net: **the UI must read from `series/index.json` and stream the CSVs**, never from custom metadata blobs.

---

## 5) UI behavior (UI-M0/M1) — “Analyze” vs “Simulate”

* **Mode toggle:** *Analyze (Engine)* vs *Simulate (Sim)*.
* In **Simulate** mode:

  1. UI posts the Sim config to `POST /sim/run`.
  2. UI polls/loads `/sim/runs/{id}/index`.
  3. UI renders series listed in **index.json**.
     No assumptions about filenames beyond what `index.json` lists.
* In **Analyze** mode:

  1. UI posts model YAML to `POST /run`.
  2. Same artifact reading path as above.

**No direct parameter forwarding** from UI to the engine when sim mode is selected. The engine is not a sim.

---

## 6) “Do/Don’t” guidance for Copilot

**Do**

* Do always write the artifact pack (even for mocks).
* Do include `schemaVersion: 1` everywhere and the `source` field.
* Do keep `series/index.json` authoritative for what exists and where.

**Don’t**

* Don’t invent an API that returns computed stats instead of files.
* Don’t put PMFs/“analytics” as outputs by default. PMFs are **inputs** or internal; outputs are **series CSVs** (+ optional events, gold).
* Don’t bypass `series/index.json` in UI—no hard-coded CSV names.

---

## 7) Minimal contracts for request bodies

### `POST /sim/run` (body shape — domain-neutral)

```json
{
  "grid": { "bins": 288, "binMinutes": 5 },
  "rng": { "seed": 12345 },
  "components": ["COMP_A"],           // for early runs, one component is fine
  "measures": ["arrivals","served"],  // minimal set; sim may add "errors","capacity"
  "arrivals": { "kind": "rate", "ratePerBin": 1.2 },
  "served":   { "kind": "fractionOf", "of": "arrivals", "fraction": 0.85 },
  "catalogId": null,                  // optional
  "templateId": "hello",              // optional
  "notes": "UI-M0 smoke"
}
```

**Response**

```json
{ "simRunId": "sim_2025-09-04T10-22-01Z_ab12cd34" }
```

*(Server writes artifacts synchronously or queues and returns 202; either way, index is available when the UI requests it.)*

---

## 8) Acceptance criteria (for PRs and CI)

* **AC-1 Artifacts present:** `spec.yaml`, `run.json`, `manifest.json`, `series/index.json`, ≥2 CSVs in `series/`.
* **AC-2 Schema valid:** All three JSONs validate (`schemaVersion: 1`).
* **AC-3 Determinism:** Given same body+seed, re-run produces identical CSV bytes and hashes.
* **AC-4 Units/kinds:** flows → `entities/bin`; levels → `entities`; derived `latency` → `minutes`.
* **AC-5 UI path:** UI renders by reading `series/index.json` (no guessing filenames).
* **AC-6 Engine↔Sim parity:** For a simple scenario, engine outputs (`served@COMP_A`) match sim outputs when configured equivalently (or deltas are explainable).
* **AC-7 Warnings:** `run.json.warnings[]` includes standard messages (e.g., divide-by-zero latency coerced to 0; PMF renormalized).
* **AC-8 Events placeholder:** `events.ndjson` exists (may be empty) to stabilize adapters.
* **AC-9 Compare (if enabled):** `/compare` documents CSV shape (`t,delta`) and one KPI file (e.g., `kpi.json` with sum served, max backlog, P95 latency).

---

## 9) What to change in your current code (concrete)

* Replace the `SimulationRunResult.Metadata` bundle with **artifact writing**; return only `{simRunId}` (and optionally `indexUrl`).
* Ensure the mock writes:

  * `runs/<simRunId>/spec.yaml` (derived from request),
  * `run.json` with `source:"sim"`, `engineVersion:"sim-0.1.0"`, `grid` fields (incl. `timezone:"UTC"`, `align:"left"`),
  * `manifest.json` with `rng.seed` and `seriesHashes`,
  * `series/index.json` referencing the CSVs you actually wrote,
  * 2–3 CSVs with `t,value`.
* In the UI, rip out any code that reads ad-hoc metadata fields like `series.count`, `stats.totalDemand`, etc. Instead:

  * GET `/sim/runs/{id}/index`
  * For each listed `series[].id`, GET `/sim/runs/{id}/series/{seriesId}` and plot.

---

## 10) Clarification on PMFs and “analytics”

* **PMFs** are **inputs** or internal intermediates for modeling. They are **not required** outputs of a run pack. If you expose them, do so as *additional series* (e.g., `attempts_expected@COMP_A` in `entities/bin`) or document a separate, optional `pmf/` directory—but don’t make UIs depend on it.
* **Analytics** (KPIs) should be either:

  * derived by the UI/adapter from the canonical series; or
  * written as **optional** artifacts (`kpi.json`, `delta.csv`) with documented schemas. They are not substitutes for the canonical CSVs.

---

### TL;DR for Copilot

* **Write the files, not a JSON blob.**
* **UI reads `series/index.json`, then CSVs.**
* **Keep `schemaVersion=1`, `source`, `grid`, and hashes correct.**

---

## 11) Future Considerations (Beyond Early Milestones)

The spec above covers early milestones (UI-M0/1, SVC-M0/1, M0–M3), but several areas warrant future specification:

### Error Handling & Artifact Integrity
* **Incomplete artifacts:** What happens when artifacts are partially written or corrupted?
* **Validation strategy:** Should consumers validate `manifest.json` hashes before processing?
* **Recovery patterns:** How to handle missing series files referenced in `index.json`?
* **Timeout scenarios:** Behavior when artifacts are being written during read attempts?

### Streaming & Progressive Writing
* **Long simulations:** How does progressive artifact writing work for multi-hour simulations?
* **Incremental updates:** Can `series/index.json` be updated as new series become available?
* **Live monitoring:** Pattern for reading partial results before completion?
* **Watermark strategy:** How to signal "up to time T is complete" for streaming consumers?

### Caching & Performance
* **Artifact caching:** Guidelines for caching artifacts vs re-reading from source?
* **Series-level caching:** When to cache individual CSV streams vs full artifact packs?
* **Invalidation strategy:** How to detect when cached artifacts are stale?
* **Multi-consumer scenarios:** Coordination when multiple services read same artifacts?

### Schema Evolution & Migration
* **Schema versioning:** How to handle evolution beyond `schemaVersion: 1`?
* **Backward compatibility:** Support strategy for mixed-version artifact environments?
* **Migration tooling:** Automated conversion between schema versions?
* **Deprecation timeline:** How long to maintain compatibility with older schemas?

These considerations should inform future milestone specifications as the system scales beyond prototype integration.

---

If you want, I can turn this into:

* `docs/specs/sim-integration.md` (this spec)
* a tiny `SimRunWriter` skeleton (file writer + hash calc) you can drop into the sim service so the mock is artifact-correct on day one.
