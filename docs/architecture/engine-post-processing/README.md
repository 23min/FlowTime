# FlowTime Engine as Post-Processing & Semantics Layer

## 1. Purpose & Status

This document proposes elevating the FlowTime Engine from a PoC-time-travel backend to a reusable **post-processing and semantics layer** in telemetry pipelines.

Target pipeline (conceptual):

```text
App Insights / Azure Monitor / Logs
        ↓
      ADX / Lake (raw)
        ↓  (KQL / ETL)
  TelemetryLoader → Canonical FlowTime Bundles (gold telemetry)
        ↓
    FlowTime Engine (post-processing + semantics)
        ↓
   UIs / BI / Notebooks (Power BI, custom Blazor, ...)
```

The engine’s responsibilities include:

- Converting canonical telemetry bundles into **stateful flow views** (`/state`, `/state_window`, `/graph`, `/metrics`).
- Applying **domain semantics** (queues, retries, DLQs, classes, future EdgeTimeBins).
- Running **validation** and surfacing health signals.
- Providing a stable set of **output contracts** that any UI or BI tool can consume.

This proposal builds on the existing time-travel architecture, the Classes and EdgeTimeBin epics, and the Loop concept (synthetic telemetry as contract).

---

## 2. Current State (Today’s Architecture)

### 2.1 Engine Role Today

Today, the FlowTime Engine already acts as a semantics layer for:

- **Simulation / gold telemetry**:
  - Sim templates and models are evaluated by the Engine.
  - Canonical run artifacts are written (model, series, manifest, run.json).
- **Time-travel endpoints**:
  - `/v1/runs/{runId}/state` and `/state_window` expose bin-level node metrics and derived metrics (utilization, latency, flow latency, retry governance, etc.).
  - `/graph` and `/metrics` provide topology and SLA aggregates.
- **Validation & governance**:
  - Retry budgets, exhausted failures, terminal edges.
  - Conservation and health checks (queue, throughput, errors).
- **UI**:
  - The current Blazor UI is tightly coupled to these endpoints and contracts.

However, this is mostly treated as **“the backend for this UI”**, not as a reusable pipeline component.

### 2.2 The Loop

The Loop concept already pushes FlowTime toward a pipeline role:

1. **Model + Sim → Engine → Canonical Bundles** (gold synthetic telemetry).
2. **Canonical Bundles → TelemetryLoader → Engine (telemetry mode)**.
3. `/state` and `/state_window` output should be **equivalent** (within tolerances) between the two paths.

This means FlowTime already defines:

- A **canonical input shape** (model + bundles).
- A set of **derived outputs** and **semantic checks**.

The proposal is to formalize and strengthen this role so it can sit cleanly between ADX and multiple consumers.

---

## 3. Target Role in the Pipeline

### 3.1 Responsibilities of Each Stage

**Telemetry Ingestion (App Insights / Azure Monitor / ADX / ETL):**

- Owns connectivity, security, and raw data capture.
- Groups and transforms raw signals into FlowTime’s **input contract** (gold bundles):
  - `(nodeId, timeBin, classId)` node aggregates.
  - Later: `(edgeId, timeBin, classId)` edge aggregates.
- Performs *no* FlowTime-specific semantics beyond basic aggregation.

**FlowTime Engine (Post-Processing & Semantics):**

- Accepts canonical bundles as runs.
- Applies flow semantics (queues, classes, retries, DLQ, later EdgeTimeBins).
- Computes derived metrics and validations:
  - Utilization, queue latency, flow latency, service time, throughput ratios.
  - Retry budget governance and terminal disposition.
  - Node/edge conservation, mode-based validation.
- Exposes stable **output contracts** via APIs and/or materialized tables.

**Consumers (Power BI, custom UIs, notebooks):**

- Consume FlowTime’s interpreted state and metrics.
- Implement domain-specific visualizations, dashboards, and reports.
- May also use raw ADX data for ad-hoc queries, but FlowTime is the **authoritative source** for flow semantics.

### 3.2 Why a Dedicated Semantics Layer?

Without FlowTime in the pipeline, each UI or analysis tool would need to:

- Reimplement queue dynamics, latency calculations, retry semantics, DLQ behavior.
- Re-encode validation and health logic.
- Maintain its own understanding of time grid and path semantics.

This would create:

- Diverging implementations of the same rules.
- Difficult regressions when fundamentals change (e.g., new retry policy semantics).
- Higher onboarding cost for new tools.

With FlowTime as a semantics layer:

- Semantics are implemented **once**.
- All consumers rely on **one set of contracts and checks**.
- Changes in flow semantics happen behind stable contracts.

---

## 4. Input & Output Contracts

### 4.1 Canonical Input Contract (Gold Bundles)

The canonical input to FlowTime Engine (via TelemetryLoader or capture) consists of:

1. **Model**
   - `model.yaml` in the KISS schema (window, topology, nodes, edges, semantics, classes).
   - Declares nodes, edges, classes (flows), and any fixed semantics.

2. **Series (Gold Telemetry)**
   - Node series:
     - Grain: `(nodeId, timeBin, classId)`.
     - Metrics: `arrivals`, `served`, `errors`, `queueDepth`, `processingTimeMsSum`, `servedCount`, optional extras.
   - Edge series (once EdgeTimeBin is implemented):
     - Grain: `(edgeId, timeBin, classId)`.
     - Metrics: `flowTotal`, `errors`, `retries`, optional extras.

3. **Manifests & Run Metadata**
   - `manifest.json`: window, grid, file index, warnings, provenance.
   - `run.json`: mode (simulation / telemetry), labels, classes, health summary.

**TelemetryLoader** (for real systems) is responsible for:

- Mapping raw system telemetry into this shape.
- Grouping by `nodeId`, `timeBin`, `classId` (+ optional labels).
- Emitting warnings where information is missing or approximate.

FlowTime Engine **does not** implement ingestion-specific concerns (auth, Kusto specifics, etc.); it assumes canonical bundles are present.

### 4.2 Output Contracts (State, Graph, Metrics)

Key Engine API surfaces:

1. **State APIs**
   - `/v1/runs/{runId}/state?binIndex={i}`
   - `/v1/runs/{runId}/state_window?startBin={s}&endBin={e}`
   - Provide:
     - Window + grid metadata.
     - Per-node metrics (including per-class metrics once classes epic lands).
     - Derived metrics (utilization, queue latency, throughput ratio, flow latency, etc.).
     - Warnings and validation indicators.
     - Later: `edges` section with EdgeTimeBins.

2. **Graph API**
   - `/v1/runs/{runId}/graph`
   - Provides topology, node kinds, semantics, and UI hints.

3. **Metrics API**
   - `/v1/runs/{runId}/metrics?startBin={s}&endBin={e}`
   - Provides higher-level aggregates (e.g., SLA, mini-bars) suitable for dashboards.

4. **Run Orchestration APIs**
   - `/v1/runs` create/list/detail with run metadata, labels, and capabilities.

For pipeline use, these outputs can be:

- Queried live by UIs and BI tools.
- Or materialized into tables (e.g., `state_nodes`, `state_edges`, `graph`, `metrics`) for Power BI to query directly.

Contract discipline:

- Schemas are versioned and stored under `docs/schemas/`.
- Golden tests ensure no accidental schema drift.

---

## 5. Benefits of Treating Engine as a Pipeline Stage

### 5.1 Centralized Semantics & Validation

- Single implementation of:
  - Queue dynamics and conservation.
  - Retry budgets, terminal edges, DLQs.
  - Latency metrics (queue latency, service time, flow latency).
  - Future EdgeTimeBin-based routing and cut conservation.
- Validation logic (warnings vs errors) lives in one place.
- Consumers trust Engine’s outputs instead of re-deriving similar but slightly different metrics.

### 5.2 Accelerated UI / BI Development

- New UIs or BI reports can focus on **interaction and visualization**:
  - Graph layout, per-flow dashboards, scenario comparisons.
- They don’t need to understand:
  - How to compute utilization from capacity.
  - How to apply retry governance semantics.
  - How to interpret DLQ volumes.
- They map directly onto:
  - Node and edge metrics.
  - Validation warnings.
  - Health classifications.

### 5.3 Stronger Loop Guarantees

By using the same Engine for both synthetic and real telemetry:

- Any regression or drift in ingestion (e.g., a KQL query change) shows up as a difference in `/state` vs the synthetic reference.
- The Loop becomes an automated **contract test** for telemetry pipelines.

### 5.4 Extensibility with New Semantics

When new concepts are introduced (e.g., classes, EdgeTimeBin, new latency measures):

- Templates + Engine + schemas are extended.
- TelemetryLoader is updated to emit the extra fields.
- All consumers benefit immediately; none need to re-interpret raw data.

---

## 6. Risks and Trade-Offs

### 6.1 API & Schema Stability Obligation

Once external tools depend on FlowTime’s APIs/schemas:

- Changing response shapes or semantics has external impact.

Risk:

- Rapid iteration becomes harder if you treat endpoints as “internal only”.

Mitigation:

- Adopt explicit versioning (e.g., `/v1`, `/v2` routes) when breaking changes are necessary.
- Maintain schema files and golden tests; evolve via additive changes where possible.

### 6.2 Over-Coupling to FlowTime

Risk:

- Teams might feel everything must go through FlowTime, even for questions that don’t need flow semantics.

Mitigation:

- Clearly scope FlowTime’s problem domain:
  - Flow-centric questions (throughput, latency, retries, DLQs, queue dynamics, routes).
- Encourage direct ADX/AI usage for other analytics (e.g., low-level error logs, per-request traces).

### 6.3 Performance & Scale

As a pipeline stage, FlowTime might see:

- Many concurrent queries across different runs and windows.
- Very large runs (many nodes, edges, classes, bins).

Risks:

- High latency or resource usage if all computations are done on-demand from raw series.

Mitigations:

- Introduce **materialization modes**:
  - Batch jobs that precompute and store state/metrics tables for heavy runs.
- Caching strategies for hot runs and common windows.
- Clear performance budgets for `/state` and `/state_window` per run size.

### 6.4 Telemetry Quality Dependence

FlowTime’s outputs are only as good as its inputs:

- Missing or low-quality telemetry (e.g., incomplete class labels, no edge data) limit the fidelity of flow views.

Mitigation:

- Use FlowTime’s validation and data-quality warnings as **feedback to improve instrumentation**.
- Design contracts to support "exact", "approximate", and "unavailable" modes for each metric.

---

## 7. Streaming / Near Real-Time Considerations

Today, FlowTime primarily operates on **batch** artifacts (completed runs). For pipeline use, streaming or near-real-time is a future but compatible evolution.

### 7.1 Stream-Friendly Design Anchors

The existing architecture already has properties that help streaming:

- Fixed **time grid** (bins, binSize, binUnit, start time).
- Windowed state queries (`/state`, `/state_window`).
- Distinct notions of **gold** and **silver** telemetry.

### 7.2 Future Streaming Mode (Conceptual)

In a streaming scenario:

- TelemetryLoader or an ingestion service incrementally writes canonical slices (e.g., per-minute or per-5-minute batches) into the FlowTime artifact space or a bounded store.
- FlowTime Engine:
  - Either recomputes derived state incrementally per new batch, or
  - Is invoked per window over partial data.
- `/state` and `/state_window` can be called while the window is still open, marked with:
  - `isFinal: false` and appropriate caveats.

This evolution would require:

- A streaming-friendly TelemetryLoader.
- Potential incremental computation paths in Engine.

It does **not** require discarding the current design; it extends it.

---

## 8. Relation to Existing Epics

### 8.1 Classes as Flows (CL-M-04.xx)

Classes make **flows** first-class in the model and telemetry:

- Per-class node metrics.
- Class-aware contracts for gold telemetry.
- Per-flow views in UI.

For the post-processing role:

- Classes define **what** is flowing.
- FlowTime becomes responsible for interpreting telemetry **per class** and exposing per-flow KPIs.

### 8.2 EdgeTimeBin Foundations (ETB-M-05.xx)

EdgeTimeBin makes **edges** quantitative:

- Per-edge, per-bin (and per-class) flows.
- Routing matrices and path analytics.
- Strong node–edge conservation.

For the post-processing role:

- EdgeTimeBin provides richer **where** flows go.
- FlowTime can answer:
  - "Which routes carry the most volume?"
  - "Which paths end in DLQ?"
  - "Where is flow leaking or being delayed?"

Both epics refine the semantics that the Engine applies to raw telemetry, increasing the value of FlowTime as a pipeline stage.

---

## 9. Recommendation

Given the existing architecture, milestones, and status:

- FlowTime Engine is already **more than a PoC backend**.
- With modest additional work on **contracts, documentation, and materialization**, it can be a viable, reusable **post-processing and semantics layer** between ADX and multiple UIs.

Recommended next steps:

1. **Document Input/Output Contracts Explicitly**
   - Lock in gold telemetry input schemas (node + class now, edges later).
   - Harden and version `/state`, `/state_window`, `/graph`, `/metrics` schemas.

2. **Complete Classes and EdgeTimeBin Epics**
   - Ensure flows (classes) and edges are fully represented in Engine semantics and contracts.

3. **Add a Simple Export Path**
   - Provide a way to dump state/metrics into tables or files for Power BI / external tools.

4. **Clarify Scope in ROADMAP.md**
   - Add a section describing FlowTime as a semantics layer in the telemetry pipeline, referencing this document.

Taken together, these steps move FlowTime from "working tool" to a well-defined, reusable component in the broader observability & capacity-planning ecosystem.
