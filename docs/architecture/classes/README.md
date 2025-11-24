# Classes as Flows Architecture

## 1. Summary

This document defines **classes** as the primary way FlowTime models **flows** (entity types) through a system, and describes how to implement end‑to‑end support:

- Templates and models explicitly declare **classes** (entity types).
- The engine and synthetic telemetry emit **per‑class node metrics**.
- `/state` and `/state_window` expose **class‑aware** state.
- The UI exposes **per‑flow views** by filtering on class.
- Telemetry ingestion (TelemetryLoader) treats **class** as a first‑class dimension in the gold contract.

This epic is intentionally **separate** from EdgeTimeBin:

- Classes first: per‑flow **node‑centric** modeling and visualization.
- EdgeTimeBin later: per‑flow **edge‑centric** routing and path analytics.

Classes are designed to work with the **Loop**:

> Model → Sim → Engine → synthetic telemetry → state  
> and telemetry → Engine → state must produce consistent results, with classes present in both paths.

---

## 2. Motivation

FlowTime’s whitepaper and time‑travel architecture describe flows as *entities moving through a topology over time*. Today:

- Nodes are modeled with time‑binned metrics (arrivals, served, errors, queueDepth, utilization, etc.).
- Retry governance, DLQ, and terminal edges are implemented.
- Edge overlays are derived from node metrics.
- Telemetry capture produces **gold, aggregated** synthetic telemetry.

What is missing is a **stable, end‑to‑end dimension for “what kind of entity is flowing?”**:

- Different **document types** or **business processes** (e.g., `Order`, `Refund`, `Invoice`).
- Different flow semantics and SLAs.
- The ability to answer:
  - “What does the system look like for a specific flow type?”
  - “Where are bottlenecks for that flow only?”
  - “Which nodes participate in this flow’s path?”

Classes provide this dimension:

- **Class = entity type = flow type.**
- Same topology; multiple classes traverse it.
- Classes are orthogonal to **labels** (customer, region, env), which are contextual metadata.

This epic focuses on **gold data** (aggregated time‑binned metrics), not per‑transaction traces, but is compatible with future silver telemetry.

---

## 3. Definitions

### 3.1 Class

A **class** is an identifier for a type of entity or business process that flows through the system.

Examples:

- `Order`, `Refund`, `Invoice`, `Claim`, `SubscriptionRenewal`.

Properties:

- Declared in templates and models as `classes`.
- Used in traffic definitions (per‑class arrivals).
- Used in metrics as a dimension:
  - `arrivalsByClass[classId]`
  - `servedByClass[classId]`
  - `errorsByClass[classId]`
  - Optionally, latency and queue stats per class.

Classes **may** affect behavior:

- Different routing per class.
- Different retry/DLQ policies per class.
- Different SLAs.

### 3.2 Labels

**Labels** are orthogonal metadata attached to runs, flows, or telemetry, not part of topology.

Examples:

- `customerId`, `tenantId`
- `region`, `environment`
- `channel` (`web`, `mobile`, `batch`)
- `priority` or `tier`

Properties:

- Key–value pairs.
- Used for filtering, grouping, segmentation.
- Core modeling logic typically does **not** depend on labels (unless explicitly configured).
- High‑cardinality labels (e.g., `customerId`) are mostly for **silver** telemetry.

Relationship:

- Class answers: **“what is this flow?”**
- Labels answer: **“in what context?”** (who, where, which env).

### 3.3 Gold vs Silver Telemetry

- **Gold**: Aggregated, system‑level or per‑class metrics.
  - Grain: `(nodeId, timeBin, classId)` → counts, queueDepth, latency aggregates, etc.
  - Driven by the **Loop** (synthetic telemetry as reference).
- **Silver**: More detailed telemetry, potentially per‑customer or per‑document.
  - Grain: `(nodeId, timeBin, classId, labelSet...)`, or even per‑event.
  - Stored separately, but adheres to the same semantics.

This epic primarily targets the **gold** contract but is designed to extend cleanly to silver.

---

## 4. Scope

### 4.1 In Scope

- Template/model schema support for **classes** as flow types.
- Engine + synthetic adapters emitting **per‑class node metrics**.
- `/state` and `/state_window` exposing per‑class metrics.
- Telemetry capture and synthetic bundles including per‑class node data.
- UI support for:
  - Listing classes for a run.
  - Filtering views by class.
  - Displaying per‑class node KPIs.
- TelemetryLoader contract updates for node + class at gold level.

### 4.2 Out of Scope

- EdgeTimeBin fact table and per‑class **edge** metrics (separate epic).
- Full path analytics (dominant routes, route histograms).
- Per‑customer (label‑level) gold telemetry; this belongs to silver and a later epic.
- Per‑class persistence changes beyond existing artifacts layout (same canonical run structure).

---

## 5. Architecture Overview

### 5.1 Templates and Models

Templates define **classes** and may use them in traffic and routing:

```yaml
schemaVersion: 1
model:
  id: "order-system-classes-v1"
  classes:
    - id: "Order"
      displayName: "Order Flow"
    - id: "Refund"
      displayName: "Refund Flow"

  topology:
    nodes:
      - id: "ingest"
        type: "queue"
      - id: "process"
        type: "service"
      - id: "outQueue"
        type: "queue"
    edges:
      - id: "ingest-to-process"
        from: "ingest"
        to: "process"
      - id: "process-to-out"
        from: "process"
        to: "outQueue"

  traffic:
    arrivals:
      - nodeId: "ingest"
        classId: "Order"
        pattern:
          kind: "constant"
          ratePerBin: 20
      - nodeId: "ingest"
        classId: "Refund"
        pattern:
          kind: "constant"
          ratePerBin: 5
```

Sim generates a canonical `model.yaml` that includes `classes` and class‑aware traffic sections.

### 5.2 Run Metadata and Labels

Runs can carry **labels** for customer, region, environment, etc.:

```yaml
runs:
  - id: "run-all-customers"
    description: "All customers aggregated"
    mode: "simulation"
    labels:
      environment: "prod"
      customerGroup: "all"

  - id: "run-customer-a"
    description: "Customer A analysis"
    mode: "simulation"
    labels:
      environment: "prod"
      customerId: "CUST-A"
```

Labels:

- Are stored in run metadata (`run.json`, manifest, registry index).
- Are available to UI and TelemetryLoader for filtering and grouping.
- Do not affect topology by default.

---

## 6. Engine & State Schema

### 6.1 Node Metrics per Class

The canonical time‑travel state schema is extended to include **per‑class metrics on nodes**.

Per‑bin, per‑node entry:

```jsonc
{
  "timeBin": "2025-11-23T10:00:00Z",
  "nodes": {
    "ingest": {
      "arrivals": 125,
      "served": 120,
      "errors": 5,
      "queueDepth": 42,
      "byClass": {
        "Order": {
          "arrivals": 100,
          "served": 96,
          "errors": 4
        },
        "Refund": {
          "arrivals": 25,
          "served": 24,
          "errors": 1
        }
      }
    },
    "process": {
      "arrivals": 120,
      "served": 118,
      "errors": 2,
      "byClass": {
        "Order": { "arrivals": 95, "served": 94, "errors": 1 },
        "Refund": { "arrivals": 25, "served": 24, "errors": 1 }
      }
    }
  }
}
```

Constraints:

- `arrivals` = Σ `byClass[*].arrivals` (when fully instrumented).
- `served`, `errors` similarly align with sums over classes.
- Some nodes may see only a subset of the declared classes.

This keeps the node‑level contract backward compatible:

- Existing fields remain; `byClass` is **additive**.
- Consumers that don’t care about classes can ignore `byClass`.

### 6.2 Engine Aggregation

For simulation runs:

- Engine tracks class per item internally (via Sim traffic definitions and routing rules).
- When writing `series/*.csv` and `index.json`, metrics are aggregated **per node and per class**.
- The time‑travel pipeline composes those into `/state` and `/state_window` responses.

For telemetry runs:

- Telemetry bundles include a **class dimension** (see §7).
- Engine ingests per‑node, per‑class aggregates and surfaces them in `/state`.

If class data is missing or partial in telemetry:

- Engine still fills total node metrics from whatever is available.
- Per‑class metrics are:
  - Omitted if not derivable.
  - Or filled with partial data, with warnings.

---

## 7. Telemetry and the Loop

### 7.1 The Loop with Classes

The Loop is extended to be **class‑aware**:

1. **Model + classes → Sim → Engine**  
   Engine runs a simulation for a model with explicit `classes`.
2. **Engine → Capture → Synthetic telemetry bundles**  
   Per‑node, per‑bin, **per‑class** metrics are written to CSVs + manifest.
3. **Synthetic telemetry → TelemetryLoader → Engine**  
   The same gold bundles are fed back via telemetry mode.
4. **State comparison**  
   `/state` for (model‑driven run) ≈ `/state` for (synthetic telemetry run), including `byClass`.

This ensures:

- Synthetic telemetry defines the **gold contract** for per‑class node metrics.
- Real telemetry pipelines must conform to that shape to close the Loop.

### 7.2 Telemetry Bundle Shape (Gold)

For this epic, the gold telemetry contract is:

- Grain: `(nodeId, timeBin, classId)` → metrics:

  ```csv
  nodeId,timeBin,classId,arrivals,served,errors,queueDepth,processingTimeMsSum,servedCount
  ingest,2025-11-23T10:00:00Z,Order,100,96,4,42,19200,96
  ingest,2025-11-23T10:00:00Z,Refund,25,24,1,10,4800,24
  ```

- Manifest describes:
  - Declared `classes`.
  - Presence of per‑class fields.
  - Warning codes if class dimension is partial.

TelemetryLoader responsibilities (gold):

- Group real system events/metrics by `(nodeId, timeBin, classId)`.
- Produce bundles matching the synthetic structure.
- Tag data quality issues; do **not** implement FlowTime semantics.

---

## 8. UI & Visualization

Minimal requirements for this epic:

- **Class selector**:
  - UI lists classes declared for the run.
  - User selects:
    - All classes (aggregate).
    - Single class (primary case).
    - Optionally, a small subset of classes.

- **Node colouring & metrics**:
  - When a class is selected, node KPIs are derived from that class’s metrics only:
    - Throughput, error rate, queueDepth, utilization, flow latency.
  - Nodes with zero volume for the selected class are dimmed or hidden.

- **Node inspector per‑class chips**:
  - Show per‑class metrics for a node:
    - `Order`: 100 arrivals, 4 errors.
    - `Refund`: 25 arrivals, 1 error.

Edge overlays remain **node‑derived** in this epic; they do not yet show per‑class edge flows.

---

## 9. Risks, Gaps, and Interactions

- Interaction with the whitepaper and time‑travel docs is positive: classes make flows explicit in the data model while staying aligned with gold‑data focus.
- Need to coordinate with expression extensions roadmap if expressions become class‑aware; initial classes epic should stay mostly data‑oriented.
- Validation and conservation for per‑class metrics should start as **soft warnings**, not hard errors, to avoid blocking M‑03.03.

---

## 10. Milestone Outline (High Level)

Engine/architecture milestones for this epic should use the 4.x range:

- **CL‑M‑04.01**: Schema + DTO + templates — templates declare `classes`; Sim/DTOs handle `classes` with default `["*"]`.
- **CL‑M‑04.02**: Engine + state aggregation — `/state` exposes `byClass` on nodes; synthetic telemetry bundles include `(nodeId, timeBin, classId)`.
- **CL‑M‑04.03**: UI per‑class views — class selector, node per‑class chips and filtered KPIs.
- **CL‑M‑04.04**: Telemetry contract + Loop tests — class‑aware synthetic telemetry vs telemetry mode equivalence tests; TelemetryLoader contract updated for node + class.

This epic is intentionally small compared to EdgeTimeBin; it threads the **class dimension** through existing surfaces without introducing new fact tables.

---

## 11. Future Work

- Extend class support to **edge metrics** (EdgeTimeBin epic).
- Enable **per‑class path analytics** when EdgeTimeBin is in place.
- Silver telemetry extensions for per‑customer class metrics while reusing the same class/label contract.
