# FlowTime-Sim Roadmap (v1.1, domain-neutral, SIM-Mx, API-enabled)

> **Purpose:** Single source of truth with sequenced milestones (SIM-M0…SIM-M10+) spanning Core/Generators, **Service/API**, and artifacts. Each milestone states **Goal, Why, Inputs, Outputs, Code, CLI/API, Acceptance**.
> **Scope:** FlowTime-Sim generates **synthetic events** and **Gold, bin-aligned series** compatible with FlowTime’s contracts (**run.json** + **series/index.json** + per-series CSV/Parquet). It remains **domain-neutral**.

---

## Status Summary (✅ = Done)

* **SIM-M0 — Skeleton & Contracts** — **✅ Done**
* **SIM-M1 — Foundations & Determinism Hardening** — **✅ Done**

> v1.1 adds **dual-write artifacts compatible with FlowTime**, a tiny **Catalog.v1** for structure/diagramming, and a **minimal Sim Service/API** so FlowTime UI can call Sim directly (or via FlowTime’s optional proxy).

---

## Vocabulary (domain-neutral)

* **entity** — the thing that moves (job/request/item).
* **component** — processing point (node).
* **connection** — directed link between components.
* **class** — segment/category of entities.
* **measure (per bin)** — `arrivals`, `served`, `errors` (flows).
* **state level (per bin)** — `queue_depth` (pending level at bin boundary).
* **grid** — `{ binMinutes, bins }`, UTC, **left-aligned**.

> Avoid “route/step/stock”; use **component/connection**, **measure**, **state level**.

---

## Contracts (artifact-first; harmonized with FlowTime)

### 1) Run manifest (dual-write) — `runs/<simRunId>/run.json` **and** `runs/<simRunId>/manifest.json`

```json
{
  "schemaVersion": 1,
  "runId": "sim-2025-09-01T10-20-30Z-5F2C",
  "engineVersion": "sim-0.1.0",
  "grid": { "bins": 288, "binMinutes": 5, "timezone": "UTC", "align": "left" },
  "scenarioHash": "sha256:…",
  "rng": { "kind": "pcg32", "seed": 42 },
  "createdUtc": "2025-09-01T10:20:31Z",
  "source": "sim",
  "warnings": [],
  "notes": []
}
```

### 2) Series index — `runs/<simRunId>/series/index.json`

```json
{
  "schemaVersion": 1,
  "grid": { "bins": 288, "binMinutes": 5, "timezone": "UTC" },
  "series": [
    { "id": "arrivals", "kind": "flow",    "path": "series/arrivals.csv", "unit": "entities/bin" },
    { "id": "served",   "kind": "flow",    "path": "series/served.csv",   "unit": "entities/bin" },
    { "id": "errors",   "kind": "flow",    "path": "series/errors.csv",   "unit": "entities/bin" }
  ]
}
```

### 3) Series CSVs — `runs/<simRunId>/series/<seriesId>.csv`

```
t,value   # t = 0..(bins-1); InvariantCulture formatting
```

### 4) Events — `events.ndjson` (reserved keys present from v1)

```json
{
  "entityId":"E-10023",
  "entityType":"entity",
  "eventType":"enter|exit|error",
  "componentId":"COMP-A",
  "connectionId":"COMP-A->COMP-B",
  "class":"DEFAULT",
  "simTime":"2025-09-01T08:00:00Z",
  "wallTime":"2025-09-01T08:00:00Z",
  "correlationId":"E-10023",
  "attrs":{"attempt":1}
}
```

### 5) Gold table (dense, bin-aligned)

```
time_bin (UTC start), component_id, class, arrivals, served, errors
```

**Dense rows policy (v1):** write **dense** rows (every component × class × bin) for simplicity and parity with FlowTime readers. Sparsity/compression may be introduced later with a versioned flag.

> **schemaVersion: 2** (later) adds `queue_depth` (unit: **entities**) and optional `latency_est_minutes` (unit: **minutes**) to Gold and series index.

---

# Milestones (SIM-Mx)

### SIM-M0 — Skeleton & Contracts — **✅ Done**

**Goal** Minimum CLI `run` from YAML; Poisson/constant arrivals; NDJSON + Gold; seeded determinism.
**Acceptance** Hash-stable outputs; time alignment; basic schema validation.

---

### SIM-M1 — Foundations & Determinism Hardening — **✅ Done**

**Goal** Lock v1 behavior for reproducibility.
**Features** `schemaVersion:1`; PCG RNG default; manifest; validation; parity harness; service-time placeholder; negative tests.

---

### SIM-M2 — Contracts v1.1 (Event Enrichment + Series Index + Schema Guards + Dual-write)

**Goal** Stabilize consumer surfaces and harmonize with FlowTime artifacts.
**Why** UI/FlowTime need measure discovery & units; dual-write lets FlowTime consume Sim runs without special cases.

**Inputs** Existing scenario YAML.
**Outputs** `events.ndjson` (enriched), `runs/<id>/run.json` **and** `manifest.json` (identical), `series/index.json` (units in **entities/bin**).
**Code** Event writer enrichments; `SeriesIndexBuilder`; JSON Schemas: `manifest.v1`, `events.v1`, `series-index.v1`.
**CLI** `flow-sim run scenario.yaml --out runs/<id>`
**Acceptance** Determinism (hash-stable); schema guard; time alignment; **unit parity** with FlowTime.

---

### **SIM-CAT-M2 — Catalog.v1 (structural source of truth)**

**Goal** Provide a **domain-neutral catalog** that both the simulator and UI can consume to render the system diagram and to stamp component IDs into Gold.

**Contract (JSON/YAML)**

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

**API additions (extend SIM-SVC-M2 below)**

* `GET /sim/catalogs` → list catalogs (id, title, hash)
* `GET /sim/catalogs/{id}` → returns Catalog.v1
* `POST /sim/catalogs/validate` → schema + referential integrity
* `POST /sim/run` accepts `{ catalogId, scenario, params?, seed? }` **or** inline `{ catalog, … }`

**Acceptance** Each `component.id` maps to Gold `component_id`; elk/react-flow can render structure; deterministic layout for same catalog + hints.

---

### **SIM-SVC-M2 — Minimal Sim Service/API (artifact-centric)**

**Goal** Expose Sim as a **stateless HTTP service** so FlowTime UI can request scenarios/runs directly.

**API (minimum viable)**

* `POST /sim/run` → `{ simRunId }` (writes `runs/<simRunId>/…`)
* `GET /sim/runs/{id}/index` → `series/index.json`
* `GET /sim/runs/{id}/series/{seriesId}` → CSV (Parquet passthrough if negotiated)
* `GET /sim/scenarios` → list presets + knobs (domain-neutral)
* `POST /sim/overlay` → `{ baseRunId, overlaySpec }` → new `simRunId`
* **Catalog endpoints** from **SIM-CAT-M2** (see above)

**Service rules** Stateless after completion; seed + scenario hash in manifest; CORS/AAD ready; culture-invariant formatting.
**Acceptance** CLI vs API parity; stable scenario listing; efficient artifact streaming.

---

### SIM-M3 — Curve-First Generators & Fuse (GEN)

**Goal** Deterministic generators for arrivals & capacity and a **fuse** that computes served.
**Why** Produce useful Gold packs quickly for UI/CI.

**Inputs** `hourly.json`/`weekly.json` (arrivals profiles), `shifts.yaml` (capacity).
**Outputs** Gold + index + manifest/run.json.
**Code** `Generators/GenArrivals`, `Generators/GenCapacity`, `Generators/Fuse` (`served = min(arrivals, capacity)`), shared writers.
**CLI**

```
flow-sim gen arrivals --hourly hourly.json --bin-minutes 5 --seed 42 --out runs/A
flow-sim gen capacity --shifts shifts.yaml --bin-minutes 5 --out runs/A
flow-sim fuse --in runs/A --out runs/A
```

**Acceptance** Seeded determinism; served ≤ min(arrivals, capacity); writer parity.

---

### SIM-M4 — Overlays Framework (windowed multipliers + selectors)

**Goal** Apply time-windowed modifiers over generated arrays.

**Selector grammar (shared with FlowTime)**

* Match by `component_id` (glob/regex) and/or `class`.
* Optional label groups via catalog `layoutHints.groups`.
* Apply to measures (`arrivals`, `capacity`, …) within `{ start, end }`.

**CLI** `flow-sim overlay --in runs/A --overlay overlays/peak.yaml --out runs/A`
**Acceptance** Correct partial windows; non-matching is a no-op; idempotent overlays guarded.

---

### SIM-M5 — Streaming Mode (stdout NDJSON + watermarks)

**Goal** Live playback without infra.

**Spec additions**

* Watermark includes **`binIndex`** **and** `simTime`.
* Stream preface includes **`runId`**.
* Optional **resume token** (`?resume=<binIndex>`).

**CLI** `flow-sim stream --in runs/A --speed 10x --watermark-bins 6`
**Acceptance** Ordered by `simTime`; watermarks on schedule; resume works.

---

### **SIM-SVC-M5 — Streaming Endpoint (SSE/NDJSON)**

**Goal** Serve the same stream over HTTP for FlowTime UI.
**API** `GET /sim/stream?runId=…&speed=10x&watermarkBins=6[&resume=…]` → SSE or chunked NDJSON.
**Acceptance** Order-independent within a bin; watermark-based slices stable; final snapshot equals file outputs for same seed.

---

### SIM-M6 — Exceptions, Retries & Fan-out

**Goal** Model failures (`errors`) and retry behavior via kernel PMFs; support fan-out.

**Inputs** Retry kernels (bins), fan-out distributions.
**Outputs** Events with correlation chains; Gold reflecting retries/fan-out.
**CLI** `flow-sim run scenario.yaml --retries retry.yaml --fanout fanout.yaml --out runs/R`
**Acceptance** Retry volumes match kernel; correlation traceability; bin-edge correctness.

---

### SIM-M7 — Multi-Class & Fairness Controls

**Goal** Multiple classes with adjustable service share when capacity binds.
**Inputs** Class mixes; fairness policy (`weighted` | `strictPriority`).
**Outputs** Class-segmented Gold rows.
**Acceptance** Priority maintains SLA; weighted shares respect capacity.

---

### SIM-M8 — Backlog Level v1 (+ latency estimate) — **schemaVersion: 2**

**Goal** Add `queue_depth` and simple latency estimate.

**Core**
`Q[t] = max(0, Q[t-1] + arrivals[t] - capacity[t])`
`latency_est_minutes = (Q[t] / max(eps, capacity[t])) * binMinutes`

**Units** `queue_depth` → **entities**; `latency_est_minutes` → **minutes**.
**Acceptance** Non-negative Q; version bump to 2; consumers detect via index.

---

### SIM-M9 — Calibration Mode (fit from telemetry)

**Goal** Fit arrivals PMFs and service/transfer distributions; emit parameter packs.
**Inputs** Telemetry in Gold shape.
**Outputs** `params.yaml` and regenerated pack.
**Acceptance** Error vs telemetry below tolerance.

---

### SIM-M10 — Scenario Library (domain-neutral presets)

**Goal** Publish reusable presets (baseline weekday, peak day, capacity dip, drift).
**Acceptance** CI regenerates identical hashes by name.

---

## Repository Layout

```
flow-sim/
├─ src/
│  ├─ Cli/                        # CLI entrypoints
│  ├─ Core/                       # planner, sequencer, distributions
│  ├─ Generators/                 # arrivals, capacity, fuse, backlog
│  ├─ Writers/                    # events + Gold writers (shared)
│  └─ Service/                    # Minimal Sim HTTP service (SIM-SVC-M2, M5)
├─ catalogs/                      # Catalog.v1 files (optional, domain-neutral)
│  ├─ tiny-demo.yaml
│  └─ baseline.yaml
├─ specs/                         # scenarios & overlays
│  ├─ baseline.weekday.yaml
│  ├─ peak_day.overlay.yaml
│  └─ outage.overlay.yaml
├─ params/                        # saved parameter packs
├─ samples/                       # generated packs for CI/demos
│  └─ weekday-5m/
├─ docs/
│  ├─ roadmap.md
│  ├─ contracts.md
│  └─ schemas/
│     ├─ manifest.v1.json
│     ├─ events.v1.json
│     └─ series-index.v1.json
└─ tests/
   ├─ determinism/
   ├─ schema/
   ├─ generators/
   ├─ catalog/
   └─ service/
```

---

## FlowTime UI integration (at a glance)

* **Direct:** UI calls **FlowTime-Sim Service** (`/sim/run`, `/sim/runs/{id}/index`, `/sim/stream`, `/sim/catalogs`) and FlowTime Service for analysis/compare.
* **Via proxy (optional, in FlowTime):** FlowTime Service exposes `/sim/*` and stores artifacts under the same `runs/*` root → single origin/auth/catalog in UI.
* **Adapters:** FlowTime’s **SYN-M0 (file)** and **SYN-M1 (stream)** already consume these artifacts/streams.

---

## Always-on Acceptance Gates

* **Determinism:** same spec + seed ⇒ identical artifacts (hash-stable).
* **Schema guard:** reject unknown top-level scenario keys unless `x-…`.
* **Time alignment:** all timestamps divisible by `binMinutes`.
* **Contracts parity:** CLI and Service write **identical artifacts** (dual-write `run.json` & `manifest.json`).
* **Catalog join test:** bijection (or defined subset) between `Catalog.components[].id` and Gold `component_id`.
* **Unit parity:** FlowTime’s SYN-M0 reader consumes a Sim pack with no overrides.
* **Performance (warn-only):** print bins/sec, rows/sec, wall time.
* **Optional CSV export:** document `--csv` writer flag for environments without Parquet (paths mirrored under `gold/*.csv`).
