# FlowTime Sim Roadmap (v1.2, domain-neutral, SIM-Mx, API-enabled)

> **Purpose:** Single source of truth with sequenced milestones (SIM-M0…SIM-M10+) spanning Core/Generators, **Service/API**, and artifacts. Each milestone states **Goal, Why, Inputs, Outputs, Code, CLI/API, Acceptance**.
> **Scope:** FlowTime-Sim generates **synthetic events** and **Gold, bin-aligned series** compatible with FlowTime's contracts (**run.json** + **series/index.json** + per-series CSV/Parquet). It remains **domain-neutral**.

---

## Status Summary (✅ = Done)

- **SIM-M0 — Skeleton & Contracts** — **✅ Done**
- **SIM-M1 — Foundations & Determinism Hardening** — **✅ Done**
- **SIM-M2 — Artifact Parity & Series Index** — **✅ Done**
- **SIM-SVC-M2 — Minimal Sim Service/API** — **✅ Done**
- **SIM-CAT-M2 — Catalog.v1 (structural source of truth)** — **✅ Done**

> v1.2 tightens **artifact parity** with FlowTime, adds a tiny **Catalog.v1** for structure/diagramming, and a **minimal Sim Service/API** so FlowTime UI can call Sim directly (or via FlowTime's optional proxy). It also clarifies **per-component series enumeration**, **latency semantics**, and **streaming details**.m Roadmap (v1.2, domain-neutral, SIM-Mx, API-enabled)

> **Purpose:** Single source of truth with sequenced milestones (SIM-M0…SIM-M10+) spanning Core/Generators, **Service/API**, and artifacts. Each milestone states **Goal, Why, Inputs, Outputs, Code, CLI/API, Acceptance**.
> **Scope:** FlowTime-Sim generates **synthetic events** and **Gold, bin-aligned series** compatible with FlowTime’s contracts (**run.json** + **series/index.json** + per-series CSV/Parquet). It remains **domain-neutral**.

---

## Status Summary (✅ = Done)

- **SIM-M0 — Skeleton & Contracts** — **✅ Done**
- **SIM-M1 — Foundations & Determinism Hardening** — **✅ Done**

> v1.2 tightens **artifact parity** with FlowTime, adds a tiny **Catalog.v1** for structure/diagramming, and a **minimal Sim Service/API** so FlowTime UI can call Sim directly (or via FlowTime’s optional proxy). It also clarifies **per-component series enumeration**, **latency semantics**, and **streaming details**.

---

## Vocabulary (domain-neutral)

- **entity** — the thing that moves (job/request/item)
- **component** — processing point (node)
- **connection** — directed link between components
- **class** — segment/category of entities
- **measure (per bin)** — `arrivals`, `served`, `errors` (flows)
- **state level (per bin)** — `queue_depth` (pending level at bin boundary)
- **grid** — `{ binMinutes, bins }`, UTC, **left-aligned**

> Avoid “route/step/stock”; use **component/connection**, **measure**, **state level**.

---

## Vocabulary (domain-neutral)

- **Goal** Minimum CLI `run` from YAML; Poisson/constant arrivals; NDJSON + Gold; seeded determinism.
- **Acceptance** Hash-stable outputs; time alignment; basic schema validation.

---

### SIM-M1 — Foundations & Determinism Hardening — **✅ Done**

- **Goal** Lock v1 behavior for reproducibility.
- **Features** `schemaVersion:1`; PCG RNG default; manifest; validation; parity harness; service-time placeholder; negative tests.

---

### SIM-M2 — Artifact Parity & Series Index — **✅ Done**

- **Scope Update (2025-09-02)** Event enrichment, Parquet Gold table, and Service/API endpoints deferred (see SIM-SVC-M2). SIM-M2 now focuses solely on producing a stable artifact pack (dual JSON + per-series CSVs + index) with deterministic hashing & integrity tests.
- **Goal** Provide a minimal, frozen artifact layout consumable by adapters/UI (no service dependency).
- **Why** Unblock downstream tooling on a guaranteed-stable v1 artifact shape before layering APIs/streaming.
- **Inputs** Simulation spec (unchanged semantics from SIM-M1; `metadata.json` deprecated).
- **Outputs** `runs/<runId>/run.json`, `runs/<runId>/manifest.json` (currently identical content), `runs/<runId>/series/index.json`, `runs/<runId>/series/*.csv`; optional `runs/<runId>/events.ndjson` (may be omitted this milestone). No Parquet yet.
- **Integrity** `manifest.json` lists per-series SHA-256 hashes (`sha256:<64hex>`). Tests enforce determinism and detect tampering.
- **runId Format** Standardized: `sim_YYYY-MM-DDTHH-mm-ssZ_<8slug>` (underscore separators) supersedes earlier hyphenated draft.
- **Deprecations** Single-file `gold.csv` & `metadata.json` removed; replaced by per-series CSVs and dual JSON documents.
- **Acceptance**
  - Determinism: identical spec+seed ⇒ identical per-series CSV bytes & hashes.
  - Dual JSON present (`schemaVersion:1`), contents identical (divergence reserved for future roles).
  - `series/index.json` enumerates all series with units + hash.
  - Tamper test: altering a series CSV invalidates stored hash.
  - runId matches documented regex; timestamps UTC.
  - Absence of `events.ndjson` does not fail acceptance (optional).

---

### **SIM-CAT-M2 — Catalog.v1 (structural source of truth)** — **✅ Done**

- **Goal** Provide a **domain-neutral catalog** that both the simulator and UI can consume to render the system diagram and to stamp component IDs into Gold.

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

- **Normalization:** `components[].id` MUST be stable, trimmed, and match Gold `component_id` exactly (case-sensitive). If normalization is applied, it must be deterministic and documented.
- **API additions (extend SIM-SVC-M2 below)**

  - `GET /sim/catalogs` → list catalogs (id, title, hash)
  - `GET /sim/catalogs/{id}` → returns Catalog.v1
  - `POST /sim/catalogs/validate` → schema + referential integrity
  - `POST /sim/run` accepts `{ catalogId, scenario, params?, seed? }` **or** inline `{ catalog, … }`
- **Acceptance** Each `component.id` maps to Gold `component_id`; elk/react-flow can render structure; deterministic layout for same catalog + hints.

---

### **SIM-SVC-M2 — Minimal Sim Service/API (artifact-centric)** — **✅ Done**

- **Goal** Expose Sim as a **stateless HTTP service** so FlowTime UI can request scenarios/runs directly.
- **API (minimum viable)**

  - `POST /sim/run` → `{ simRunId }` (writes `runs/<simRunId>/…`)
  - `GET /sim/runs/{id}/index` → `series/index.json`
  - `GET /sim/runs/{id}/series/{seriesId}` → CSV (Parquet passthrough if negotiated; accepts URL-encoded `seriesId`)
  - `GET /sim/scenarios` → list presets + knobs (domain-neutral)
  - `POST /sim/overlay` → `{ baseRunId, overlaySpec }` → new `simRunId`
  - **Catalog endpoints** from **SIM-CAT-M2**
- **Service rules** Stateless after completion; seed + scenario hash in manifest; CORS/AAD ready; culture-invariant formatting.
- **Acceptance** CLI vs API parity; stable scenario listing; efficient artifact streaming.

---

### SIM-M3 — Curve-First Generators & Fuse (GEN)

- **Goal** Deterministic generators for arrivals & capacity and a **fuse** that computes served.
- **Why** Produce useful Gold packs quickly for UI/CI.
- **Inputs** `hourly.json`/`weekly.json` (arrivals profiles), `shifts.yaml` (capacity).
- **Outputs** Gold + index + manifest/run.json (per-component series enumerated).
- **Code** `Generators/GenArrivals`, `Generators/GenCapacity`, `Generators/Fuse` (`served = min(arrivals, capacity)`), shared writers.
- **CLI**

  ```
  flow-sim gen arrivals --hourly hourly.json --bin-minutes 5 --seed 42 --out runs/A
  flow-sim gen capacity --shifts shifts.yaml --bin-minutes 5 --out runs/A
  flow-sim fuse --in runs/A --out runs/A
  ```
- **Acceptance** Seeded determinism; served ≤ min(arrivals, capacity); writer parity; index lists all per-component series.

---

### SIM-M4 — Overlays Framework (windowed multipliers + selectors)

- **Goal** Apply time-windowed modifiers over generated arrays.
- **Selector grammar (shared with FlowTime)**

  - Match by `component_id` (glob/regex) and/or `class`.
  - Optional label groups via catalog `layoutHints.groups` (if used, document as an extension until FlowTime adopts the same).
  - Apply to measures (`arrivals`, `capacity`, …) within `{ start, end }`.
- **CLI** `flow-sim overlay --in runs/A --overlay overlays/peak.yaml --out runs/A`
- **Acceptance** Correct partial windows; non-matching is a no-op; idempotent overlays guarded.

---

### SIM-M5 — Streaming Mode (stdout NDJSON + watermarks)

- **Goal** Live playback without infra.
- **Spec additions**

  - Watermark includes **`binIndex`** **and** `simTime`.
  - Stream preface includes **`runId`**.
  - Optional **resume token** (`?resume=<binIndex>`).
  - Optional **heartbeat** records every N seconds; terminal `{"type":"end"}` record.
- **CLI** `flow-sim stream --in runs/A --speed 10x --watermark-bins 6`
- **Acceptance** Ordered by `simTime`; watermarks on schedule; resume works; end frame emitted.

---

### **SIM-SVC-M5 — Streaming Endpoint (SSE/NDJSON)**

- **Goal** Serve the same stream over HTTP for FlowTime UI.
- **API** `GET /sim/stream?runId=…&speed=10x&watermarkBins=6[&resume=…]` → SSE or chunked NDJSON.
- **Acceptance** Order-independent within a bin; watermark-based slices stable; final snapshot equals file outputs for same seed.

---

### SIM-M6 — Exceptions, Retries & Fan-out

- **Goal** Model failures (`errors`) and retry behavior via kernel PMFs; support fan-out.
- **Inputs** Retry kernels (bins), fan-out distributions.
- **Outputs** Events with correlation chains; Gold reflecting retries/fan-out.
- **CLI** `flow-sim run scenario.yaml --retries retry.yaml --fanout fanout.yaml --out runs/R`
- **Acceptance** Retry volumes match kernel; correlation traceability; bin-edge correctness.

---

### SIM-M7 — Multi-Class & Fairness Controls

- **Goal** Multiple classes with adjustable service share when capacity binds.
- **Inputs** Class mixes; fairness policy (`weighted` | `strictPriority`).
- **Outputs** Class-segmented Gold rows.
- **Acceptance** Priority maintains SLA; weighted shares respect capacity.

---

### SIM-M8 — Backlog Level v1 (+ latency estimate) — **schemaVersion: 2**

- **Goal** Add `queue_depth` and a simple latency estimate.
- **Core**

  - `Q[t] = max(0, Q[t-1] + arrivals[t] - capacity[t])`
  - `latency_est_minutes = (Q[t] / max(eps, capacity[t])) * binMinutes`
- **Semantics**

  - `latency_est_minutes` is a **service-rate estimate** (capacity-based).

  - FlowTime’s **`latency`** (when present) is **flow-through**: `backlog / served * binMinutes`. Keep names distinct to avoid confusion.
  - Optionally, Sim may later emit a `latency` series using the FlowTime formula once `served` is well-defined per component.
- **Units** `queue_depth` → **entities**; `latency_est_minutes` → **minutes**.
- **Acceptance** Non-negative Q; version bump to 2; consumers detect via index.

---

### SIM-M9 — Calibration Mode (fit from telemetry)

- **Goal** Fit arrivals PMFs and service/transfer distributions; emit parameter packs.
- **Inputs** Telemetry in Gold shape.
- **Outputs** `params.yaml` and regenerated pack.
- **Acceptance** Error vs telemetry below tolerance.

---

### SIM-M10 — Scenario Library (domain-neutral presets)

- **Goal** Publish reusable presets (baseline weekday, peak day, capacity dip, drift).
- **Acceptance** CI regenerates identical hashes by name.

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

- **Direct:** UI calls **FlowTime-Sim Service** (`/sim/run`, `/sim/runs/{id}/index`, `/sim/stream`, `/sim/catalogs`) and FlowTime Service for analysis/compare.
- **Via proxy (optional, in FlowTime):** FlowTime Service exposes `/sim/*` and stores artifacts under the same `runs/*` root → single origin/auth/catalog in UI.
- **Adapters:** FlowTime’s **SYN-M0 (file)** and **SYN-M1 (stream)** consume these artifacts/streams. `series/index.json` + per-series CSVs are the UI’s **canonical** source.

---

## Always-on Acceptance Gates

- **Determinism:** same spec + seed ⇒ identical artifacts (hash-stable).
- **Schema guard:** reject unknown top-level scenario keys unless `x-…`.
- **Time alignment:** all timestamps divisible by `binMinutes`.
- **Contracts parity:** CLI and Service write **identical artifacts** (dual-write `run.json` & `manifest.json`).
- **Catalog join test:** bijection (or defined subset) between `Catalog.components[].id` and Gold `component_id`; IDs stable and normalized as documented.
- **Unit parity:** FlowTime’s SYN-M0 reader consumes a Sim pack with no overrides.
- **Performance (warn-only):** print bins/sec, rows/sec, wall time.
- **Streaming:** watermarks include `binIndex`; optional heartbeat; terminal `{"type":"end"}` frame.
- **CSV/Parquet disclosure:** manifest notes emitted formats; `--csv` flag switches writers.
