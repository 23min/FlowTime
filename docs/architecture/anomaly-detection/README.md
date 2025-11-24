# Anomaly & Pathology Detection in FlowTime

## 1. Purpose and Scope

This document describes a future-facing architecture for **automatic anomaly and pathology detection** in FlowTime, and how it feeds into human-facing stories and dashboards.

The goal is that when an SRE, support engineer, or executive receives a notification, they are taken to a **FlowTime-generated view** that:

- Shows **what is currently abnormal** (or was abnormal in a past window).
- Explains **where in the flow graph** the issue manifests.
- Summarizes **how it evolved over time** (onset, peak, recovery).
- Tags the event with recognizable **pathological patterns** (e.g., retry storm, slow drain).
- Provides an entry point for both **deep technical analysis** and **high-level impact understanding**.

This is explicitly a **future architecture**. It builds on FlowTime's existing discrete-time engine, DAG structure, and time-travel APIs, but does not assume any particular implementation yet.

---

## 2. Foundations: Why FlowTime Is a Natural Detection Layer

FlowTime already has most of the ingredients needed for high-quality anomaly/pathology detection:

- **DAG of nodes and edges**: services, queues, adapters, DLQs, grouped into subsystems and flows.
- **Time-binned state**: per-node/per-class vectors such as arrivals, served, errors, backlog, latency, capacity.
- **Deterministic, replayable runs**: a run is a reproducible artifact; time windows can be revisited.
- **Telemetry alignment**: gold contracts for real telemetry (5m/hourly bins) and synthetic data that follow the same shape.

This means FlowTime can:

- Detect anomalies in the context of **where** they occur in the graph.
- Classify them based on **how flows behave over time**, not just point anomalies.
- Re-run or replay windows to analyze or refine detection logic.

FlowTime is therefore a good place to centralize **flow-aware detection**, rather than scattering anomaly logic across individual services or APM tools.

---

## 3. Key Concepts

To talk about detection and its outputs, we introduce a few conceptual entities.

### 3.1 Anomaly

An **anomaly** is a localized, metric-level deviation from expected behavior at a given node (and optionally class) over time. Examples:

- Capacity drops to zero or near zero.
- Latency spikes beyond a defined threshold.
- Backlog grows far above baseline.
- Error rate jumps significantly.

Anomalies are defined over **time-binned series** and usually span multiple bins (not just a single point).

### 3.2 Pathology

A **pathology** is a recognisable **pattern of anomalies over time and across nodes** that corresponds to a systemic flow problem. Examples:

- **Retry Storm**: upstream services amplify failure at a dependency by retrying aggressively.
- **Slow Drain**: backlog drains very slowly even after capacity returns, due to mis-sized queues or limited downstream capacity.
- **Masked Downstream Failure**: user-facing services look healthy while back-office or downstream queues silently grow.
- **Over-Buffered Queue**: large buffers prevent visible failure but hide chronic capacity deficits.

Pathologies are often **graph- and time-aware**: they depend on how anomalies propagate along edges and how flows move.

### 3.3 Incident

An **incident** (in this document) is a higher-level aggregate that groups:

- One or more anomalies.
- Zero or more detected pathologies.
- A time window \([t_{start}, t_{end}]\).
- A set of involved nodes, edges, subsystems, and flows/classes.

Not all incidents are “paged” incidents in the operational sense; some may be **low-severity anomalies** that only appear in trend views.

### 3.4 Story & Dashboard

For each incident, FlowTime should be able to produce a **story and dashboard view**:

- **Story**: a narrative-style summary (human-readable text) of what happened, where, and how it evolved.
- **Dashboard**: a compact set of charts, tables, and graph views focused on the incident’s scope.

These two are driven by the same underlying incident model and metrics; they are just different presentations for different personas.

---

## 4. Detection Architecture Overview

At a high level, detection runs as a pipeline over FlowTime data:

1. **Data Ingestion / Run Completion**
   - New telemetry is ingested into FlowTime, or a simulation run completes.
   - Node/class time series for the relevant window are available.

2. **Anomaly Detection**
   - Per node (and optionally per class), FlowTime evaluates metrics against baselines and thresholds.
   - Anomalies are created as structured entities when conditions are met.

3. **Pathology Classification**
   - Groups of anomalies (across nodes and over time) are evaluated against known patterns.
   - Pathology tags are assigned where appropriate.

4. **Incident Construction**
   - Anomalies and pathologies are clustered into incidents, each with:
     - A time window.
     - Involved nodes/edges/flows.
     - Severity.
     - Pattern tags.

5. **Notification & Surfacing**
   - Incidents may trigger notifications.
   - Clicking a notification leads to the story + dashboard view for that incident.

6. **Optional AI Enrichment**
   - An MCP-based AI layer can read the incident and underlying state to produce richer narratives and suggestions.

Each step can evolve independently, but all are grounded in FlowTime’s discrete-time, graph-based model.

---

## 5. Anomaly Detection

### 5.1 Scope

Anomaly detection operates at the level of **(run, node, class, metric, time-window)**. It should be:

- **Explainable**: avoid black-box models in early versions; start with thresholds and baselines.
- **Configurable**: allow different sensitivities per metric/subsystem.
- **Reproducible**: given the same run data and configuration, detection results must be deterministic.

### 5.2 Baselines and Thresholds

FlowTime can support multiple baseline strategies:

- **Static thresholds**:
  - e.g., capacity < 0.1 × nominal capacity.
  - e.g., latency > 2 seconds.
- **Rolling baselines**:
  - e.g., compare backlog to a moving average over the previous N bins.
- **Historical baselines**:
  - e.g., compare to the same time-of-day on previous days/weeks.

Baselines must be attached to metric definitions and/or configuration:

- Per metric (e.g., backlog, latency, error rate).
- Per node/class (e.g., stricter thresholds for critical flows).

### 5.3 Basic Anomaly Types

Some typical anomaly types to start with:

- **Capacity Outage**:
  - Condition: capacity series at a node drops below a small threshold for consecutive bins.
- **Throughput Collapse**:
  - Condition: served series drops sharply relative to arrivals or baseline.
- **Backlog Explosion**:
  - Condition: backlog grows beyond a baseline/threshold at a given slope.
- **Latency Spike**:
  - Condition: latency increases above threshold or a multiple of baseline.
- **Error Burst**:
  - Condition: error rate crosses a threshold.

Each anomaly stores:

- NodeId, optional classId.
- Metric name.
- `startTs`, `endTs`.
- Baseline and threshold values used.
- Summary metrics (e.g., peak value, average deviation).

### 5.4 Implementation Characteristics

- Detection can run in **batch mode** (after a run) or **incremental mode** (as new bins arrive).
- Results should be stored in a durable artifact format (e.g., JSON per run).
- Configuration should live alongside other model/telemetry contracts.

---

## 6. Pathology Detection

### 6.1 From Anomalies to Pathologies

Pathologies describe **patterns of anomalies**, often across multiple nodes and over a time interval. Examples of rules:

- **Retry Storm**:
  - Preconditions:
    - Downstream node B shows Error Burst or Capacity Outage.
    - Upstream node(s) A show Backlog Explosion and/or Arrivals spike, especially on retry paths.
  - Relationships:
    - A → B edge(s) exist in the graph.
    - Timing: A’s anomalies follow B’s anomaly within a short lag.

- **Slow Drain**:
  - Preconditions:
    - Backlog Explosion at node X followed by slow reduction despite restored capacity.
  - Relationships:
    - Capacity returns to normal or above baseline.
    - Backlog takes unusually long to return to baseline.

- **Masked Downstream Failure**:
  - Preconditions:
    - User-facing node F appears healthy (no major latency spike or backlog explosion).
    - Downstream nodes D1, D2 show Backlog Explosion and/or Error Bursts.
  - Relationships:
    - F → D* edges exist.

These patterns can initially be coded as **rule-based classifiers** operating on:

- Sets of anomalies.
- Graph structure (nodes, edges, subsystems).
- Relative timing and magnitudes.

### 6.2 Pathology Entities

A `Pathology` entity might include:

- `id`
- `type` (e.g., `RetryStorm`, `SlowDrain`)
- `runId`
- `timeWindow: { startTs, endTs }`
- `primaryNodes: string[]`
- `relatedNodes: string[]`
- `supportingAnomalies: string[]` (links to underlying anomaly IDs)
- `evidenceSummary` (metrics snapshots used in detection)

Pathologies are then attached to incidents (see next section).

---

## 7. Incident Construction

### 7.1 Clustering

Incidents are constructed by clustering anomalies and pathologies that are:

- Close in time.
- Connected in the graph.
- Conceptually part of the same event.

Example strategy:

1. Group anomalies by time overlap and graph connectivity.
2. For each group, collect pathologies whose time windows overlap the group’s.
3. Define the incident window as the union (or a slightly expanded range) of all contributing anomalies.

### 7.2 Incident Model

An `Incident` might look like:

- `id`
- `runId`
- `timeWindow: { startTs, endTs }`
- `severity: 'info' | 'warning' | 'major' | 'critical'`
- `primaryNodes: string[]`
- `relatedNodes: string[]`
- `primaryPathologies: string[]`
- `allAnomalies: string[]`
- `summaryMetrics` (e.g., max backlog, max latency, total affected demand)
- `labels: string[]` (e.g., `['checkout-flow', 'region-eu', 'premium-users']`)

This entity becomes the **anchor** for notifications and story/dashboard views.

---

## 8. Personas and Flows: From Notification to Story & Dashboard

### 8.1 Personas

Different personas need different levels of detail:

- **SREs / On-call engineers**:
  - Need fast localization, root-cause hints, and timelines.
- **Support / Customer-facing teams**:
  - Need clear answers about impact, duration, and current status.
- **Product / Executives**:
  - Need aggregate impact, trend over time, and confidence in mitigation.

### 8.2 Notification Entry Points

A notification (e.g., in chat, incident tool, or FlowTime UI) should:

- Reference the `Incident id` and time window.
- Use a short, readable summary:
  - e.g., "[Critical] Retry storm involving `Billing.Settle` and `Orders.API` (10:12–10:41, peak backlog 620)."
- Provide a link to the **Incident Story & Dashboard** view.

### 8.3 Story View (Narrative)

The story view is a **narrative-first** representation, rendered in the FlowTime UI (with or without AI assistance). It should typically include:

- **Title**:
  - e.g., "Retry storm around Billing.Settle on 2025-11-24 10:12–10:41".
- **Headline summary**:
  - One or two sentences capturing: what went wrong, where, and the impact.
- **Timeline paragraphs**:
  - Before: state leading up to the incident (e.g., normal traffic).
  - During: onset, peak, and propagation (key nodes, metrics, and pathologies).
  - After: recovery shape and time to steady-state.
- **Impact summary**:
  - Estimated affected requests or flows.
  - Key business-related metrics (if modeled).

The story can be:

- **Template-based** (deterministic text based on the Incident model), and/or
- **AI-enriched** (MCP server + AI assistant generating a richer narrative from metrics and graph slices).

### 8.4 Dashboard View (Data)

The dashboard is a **visual, metric-heavy** representation aligned with the story. It should typically include:

- **Graph slice**:
  - A subgraph view focused on `primaryNodes` and `relatedNodes`.
  - Highlighted edges where flow is disrupted.

- **Key time-series charts**:
  - Backlog, served, capacity, and latency for primary nodes over the incident window.
  - Optional comparison to baseline behavior.

- **Incident table**:
  - Rows: nodes; columns: key metrics (peak backlog, peak latency, duration above threshold).

- **Pathology tags & evidence**:
  - List of detected pathologies with brief explanations.
  - Links to underlying anomalies/metrics.

All of these are derived from the same Incident, Anomaly, and Pathology data, ensuring consistency.

---

## 9. Role of AI (Optional but Powerful)

AI is not required for detection itself, but it can significantly improve **interpretation and communication**.

Using the MCP architecture described in `docs/architecture/ai/`, an AI assistant can:

- Read:
  - Incident, anomaly, and pathology entities.
  - Graph slices via `get_graph`.
  - Time windows via `get_state_window`.
- Generate:
  - Rich narrative summaries (postmortem drafts, status updates).
  - Tables of key metrics and nodes.
  - Suggested root-cause hypotheses and remediation ideas.

Key design points:

- **FlowTime remains the source of truth** for metrics and detection.
- AI is an **editor/explainer**, not the detector of record.
- All AI-created text is attached to the Incident as an artifact, but the underlying detection remains deterministic and reproducible.

---

## 10. Implementation Phasing

To move from concept to reality, a phased approach is sensible:

### Phase 1: Anomaly Detection Only

- Implement anomaly detectors with configurable baselines and thresholds.
- Store anomalies as first-class artifacts.
- Provide basic UI views and APIs to query anomalies per run/window.

### Phase 2: Pathologies and Incidents

- Implement rule-based pathology detection using anomalies and graph structure.
- Introduce the Incident model and clustering logic.
- Add an Incident list and basic detail page in the UI.

### Phase 3: Story & Dashboard

- Design the Incident Story & Dashboard view.
- Implement template-based summaries and incident-focused charts.
- Integrate notifications that deep-link to this view.

### Phase 4: AI Enrichment (Optional)

- Expose incidents and metrics via an MCP server.
- Let AI generate richer narratives and analyses.
- Iterate on templates and prompts based on user feedback.

---

## 11. Summary

Anomaly and pathology detection is a natural extension of FlowTime's role as a **flow-aware, time-binned digital twin**. By grounding detection in:

- Clear anomaly definitions over deterministic metrics.
- Graph- and time-aware pathological patterns.
- Structured incidents that tie anomalies and pathologies together.

FlowTime can provide:

- Actionable, explainable alerts for SREs and support teams.
- High-level impact narratives and dashboards for executives.
- A solid foundation for AI-assisted incident analysis and communication.

This document outlines the conceptual architecture for that future capability and can guide both design and implementation of the corresponding epics.
