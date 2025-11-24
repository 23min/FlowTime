# Subsystems, Zooming & Filtered Views Architecture

## 1. Summary

This document proposes a **multi-resolution topology and filtering model** for FlowTime that introduces **subsystems** as a first-class concept and defines how the Engine, UI, and telemetry pipelines support:

- Zoomed-out views showing **only subsystems as opaque boxes** with subsystem-level metrics.
- Zoomed-in views showing **only one subsystem** with its internal nodes, edges, and metrics.
- Full-detail views showing the **entire system topology**.
- All of the above views filtered by **classes** and **labels**.

The goal is to make "zooming" and "filtering" *contracted behaviors* of the Engine APIs, not just UI tricks, so that:

- UIs, BI tools, and telemetry validation pipelines can all share the same semantics.
- The design remains compatible with **streaming/near-real-time** scenarios.

This document builds on existing work:

- Time-travel state and graph architecture.
- Classes as flows (`docs/architecture/classes/README.md`).
- EdgeTimeBin foundations (`docs/architecture/edge-time-bin/README.md`).
- Engine-as-post-processing proposal (`docs/architecture/engine-post-processing/README.md`).

---

## 2. Conceptual Model

### 2.1 Subsystems

A **subsystem** is a named grouping of nodes that represents a logical part of the system:

- Examples: `orders-core`, `payments-core`, `catalog-service`, `email-outbound`.
- Each node may belong to **zero or one** subsystem.
  - Nodes without `subsystemId` remain top-level nodes in zoomed-out views.

Properties:

- `id`: stable identifier, e.g. `orders-core`.
- `displayName`: human-friendly name.
- Optional metadata: description, owner team, tags, etc.

### 2.2 Terminals and Boundaries

Subsystems are connected via **boundary edges**:

- **Internal edges**: both endpoints are in the same subsystem.
- **Boundary edges**:
  - **Inbound** to subsystem X: from a node not in X to a node in X.
  - **Outbound** from subsystem X: from a node in X to a node not in X.

At the subsystem level, we do not expose individual boundary nodes. Instead, we treat the union of boundary edges as the **interfaces** of the subsystem.

### 2.3 View Modes

We define three **view modes** for topology and metrics:

1. **Full system (`full`)**
   - Nodes: all nodes.
   - Edges: all edges.
   - Metrics: per-node (and per-edge once EdgeTimeBin is available).

2. **All subsystems (`subsystems`)**
   - Nodes: one node per subsystem, plus standalone nodes (with no `subsystemId`).
   - Edges: aggregated flows between subsystems (and between standalone nodes and subsystems).
   - Metrics: aggregated per-subsystem metrics.

3. **Single subsystem (`subsystem`)**
   - Topology: only nodes and edges belonging to a specific subsystem.
   - Metrics: node/edge metrics restricted to that subsystem.

These view modes are projections over the same underlying run; they do **not** require separate storage.

### 2.4 Filters (Classes & Labels)

All view modes can be combined with filters:

- `classIds`: one or more **classes** (flows) to include.
- `labels`: key–value filters on run or telemetry labels (e.g., `environment=prod`, `region=eu`).

Semantics:

- Class filters restrict metrics and flows to the selected classes, leaving topology unchanged.
- Label filters restrict which **runs/telemetry segments** are considered when aggregating metrics (depending on the label level: run-level vs telemetry-level).

---

## 3. Modeling Subsystems in Templates/Models

### 3.1 Schema Extensions

We extend the KISS model schema with a `subsystems` section and optional `subsystemId` on nodes.

```yaml
schemaVersion: 1
model:
  id: "order-system-with-subsystems-v1"

  subsystems:
    - id: "orders-core"
      displayName: "Orders Core"
    - id: "payments-core"
      displayName: "Payments Core"

  topology:
    nodes:
      - id: "orders-api"
        type: "service"
        subsystemId: "orders-core"
      - id: "orders-db"
        type: "storage"
        subsystemId: "orders-core"
      - id: "billing-api"
        type: "service"
        subsystemId: "payments-core"
      - id: "email-outbound"
        type: "service"
        # standalone, no subsystem

    edges:
      - id: "orders-to-billing"
        from: "orders-api"
        to: "billing-api"
      - id: "orders-to-email"
        from: "orders-api"
        to: "email-outbound"
```

Rules:

- `subsystems` is optional; absence means all nodes are standalone.
- `subsystemId` is validated against `subsystems[].id`.
- A node belongs to at most one subsystem.

### 3.2 Deriving Boundaries

From the model we derive, per subsystem X:

- **Member nodes**: nodes with `subsystemId = X`.
- **Internal edges**: edges where both `from` and `to` are member nodes.
- **Boundary edges**:
  - `inbound`: `from` not in X, `to` in X.
  - `outbound`: `from` in X, `to` not in X.

These boundaries are the basis for subsystem-level edges and metrics.

---

## 4. Engine Graph Contract

### 4.1 Full Graph (`viewMode=full`)

We extend the existing `/graph` response to include subsystem metadata.

Key elements:

```jsonc
{
  "nodes": [
    {
      "id": "orders-api",
      "type": "service",
      "subsystemId": "orders-core",
      "...": "other node metadata"
    },
    {
      "id": "email-outbound",
      "type": "service",
      "subsystemId": null
    }
  ],
  "edges": [
    { "id": "orders-to-billing", "from": "orders-api", "to": "billing-api" },
    { "id": "orders-to-email", "from": "orders-api", "to": "email-outbound" }
  ],
  "subsystems": {
    "orders-core": { "displayName": "Orders Core" },
    "payments-core": { "displayName": "Payments Core" }
  }
}
```

This allows any consumer to:

- Group nodes by `subsystemId`.
- Compute subsystem-level graphs client-side if desired.

### 4.2 Subsystem Graph (`viewMode=subsystems`)

We introduce a **projection mode** for `/graph`:

```http
GET /v1/runs/{runId}/graph?viewMode=subsystems
    [&classId=Order&classId=Refund]
    [&label.environment=prod]
```

Response outline:

```jsonc
{
  "subsystems": {
    "orders-core": { "displayName": "Orders Core" },
    "payments-core": { "displayName": "Payments Core" }
  },
  "nodes": [
    { "subsystemId": "orders-core" },
    { "subsystemId": "payments-core" },
    { "nodeId": "email-outbound" }  // standalone node
  ],
  "edges": [
    {
      "fromSubsystemId": "orders-core",
      "toSubsystemId": "payments-core",
      "edgeIds": ["orders-to-billing", "orders-to-refunds"]
    },
    {
      "fromSubsystemId": "orders-core",
      "toNodeId": "email-outbound",
      "edgeIds": ["orders-to-email"]
    }
  ]
}
```

Notes:

- Internal edges are not listed separately; they are implied within each subsystem.
- Boundary edges are grouped by subsystem pairs.
- Class/label filters do **not** change which subsystems appear, but may be used to compute edge/metric overlays.

### 4.3 Single Subsystem Graph (`viewMode=subsystem`)

```http
GET /v1/runs/{runId}/graph?viewMode=subsystem&subsystemId=orders-core
    [&classId=Order]
    [&label.environment=prod]
```

Response outline:

```jsonc
{
  "subsystem": { "id": "orders-core", "displayName": "Orders Core" },
  "nodes": [
    { "id": "orders-api", "type": "service", "subsystemId": "orders-core" },
    { "id": "orders-db", "type": "storage", "subsystemId": "orders-core" }
  ],
  "edges": [
    { "id": "orders-to-orders-db", "from": "orders-api", "to": "orders-db" }
  ]
}
```

Only internal nodes and edges for that subsystem are returned. Class/label filters again affect metrics, not topology.

---

## 5. State & Metrics Projections

### 5.1 Node-Level State (Full View)

Existing `/state` and `/state_window` endpoints already provide per-node metrics, extended by classes.

Example per-bin state (simplified):

```jsonc
{
  "timeBin": "2025-11-23T10:00:00Z",
  "nodes": {
    "orders-api": {
      "arrivals": 200,
      "served": 195,
      "errors": 5,
      "byClass": {
        "Order": { "arrivals": 180, "served": 176, "errors": 4 },
        "Refund": { "arrivals": 20, "served": 19, "errors": 1 }
      }
    },
    "orders-db": { "arrivals": 195, "served": 195, "errors": 0 }
  }
}
```

Filters:

- `classId=Order`: reduce metrics to the selected classes (e.g., sum over selected `byClass` entries and recompute derived KPIs).
- `label.*`: restrict which underlying telemetry slices contribute to the aggregates.

### 5.2 Subsystem-Level Metrics

We introduce a subsystem projection for `/state` and `/state_window` via a `viewMode` parameter.

```http
GET /v1/runs/{runId}/state_window
    ?startBin=...
    &endBin=...
    &viewMode=subsystems
    [&classId=Order]
    [&label.environment=prod]
```

Example response fragment:

```jsonc
{
  "timeBin": "2025-11-23T10:00:00Z",
  "bySubsystem": {
    "orders-core": {
      "arrivals": 200,
      "served": 195,
      "errors": 5
    },
    "payments-core": {
      "arrivals": 80,
      "served": 78,
      "errors": 2
    }
  }
}
```

Aggregation rules:

- Subsystem totals are **sums over member nodes** for the selected classes and labels:

  $$
  arrivals_{subsystem} = \sum_{n \in nodes(subsystem)} arrivals_n
  $$

- Optionally, per-subsystem `byClass` can be added in future if needed:

  ```jsonc
  "orders-core": {
    "arrivals": 200,
    "byClass": {
      "Order": 180,
      "Refund": 20
    }
  }
  ```

### 5.3 Single Subsystem State (`viewMode=subsystem`)

```http
GET /v1/runs/{runId}/state_window
    ?startBin=...
    &endBin=...
    &viewMode=subsystem
    &subsystemId=orders-core
    [&classId=Order]
    [&label.environment=prod]
```

Response:

- Same shape as full `/state_window`, but restricted to nodes in `orders-core` and corresponding edges.
- Allows detailed node-level inspection after zooming in from the subsystem overview.

### 5.4 Metrics API

The `/metrics` API can mirror these projections:

```http
GET /v1/runs/{runId}/metrics
    ?viewMode=subsystems
    [&classId=Order]
    [&label.environment=prod]
```

This would return:

- Subsystem-level aggregates suitable for dashboards (averages, percentiles, SLA hit rates) instead of per-bin state.

---

## 6. UI Affordances

With the projections above, the UI can support:

### 6.1 Zoomed-Out Subsystem View

- Fetch: `graph?viewMode=subsystems` and `metrics?viewMode=subsystems` (with optional class/label filters).
- Render one box per subsystem and per standalone node.
- Draw edges between subsystems based on aggregated boundary edges.
- Color/thickness of edges based on volume or error rate.
- Subsystem-level metrics displayed in tooltips or side panels.

### 6.2 Zoom-In to a Single Subsystem

- User clicks subsystem `orders-core`.
- UI fetches:
  - `graph?viewMode=subsystem&subsystemId=orders-core`.
  - `state_window?viewMode=subsystem&subsystemId=orders-core&...`.
- Canvas re-renders with only internal nodes and edges of `orders-core`.
- All filters (classes, labels, time window) remain applied.

### 6.3 Full-Detail System View

- Fetch: `graph?viewMode=full` and `/state_window?viewMode=full&...`.
- Render all nodes and edges.
- Optionally, visually group nodes into subsystem boxes but keep all detail visible.

### 6.4 Class & Label Filters

- UI always passes selected `classId[]` and `label.*` as query parameters.
- All three view modes are treated uniformly:
  - Zoomed out + class filters: shows subsystem flows for that class only.
  - Zoomed in + labels: shows subsystem internals for a specific customer group or region.

---

## 7. Streaming & Near Real-Time

The design is compatible with streaming:

- View modes and filters are **orthogonal to data arrival**; they define *how to view* data, not *how data is stored*.
- In streaming mode, `/state_window` and `/metrics` simply:
  - Operate over whatever bins are currently available.
  - Mark results as partial via fields like `isFinal: false` or `coverage: <percentage>`.

Implementation notes:

- Batch mode (today):
  - Derived entirely from completed run artifacts (CSV + manifests).
  - Projections computed on demand from these artifacts.
- Streaming mode (future):
  - Metrics per node, per class, and per subsystem can be maintained incrementally.
  - Same API contracts; only the backing store and computation strategy differ.

---

## 8. Telemetry & ADX Integration

Subsystem and filter-aware views are important for telemetry:

### 8.1 Gold Telemetry Contract

TelemetryLoader continues to emit canonical gold telemetry:

- Node series: grain `(nodeId, timeBin, classId)` plus labels.
- Edge series (after EdgeTimeBin): `(edgeId, timeBin, classId)` plus labels.

The Engine’s projections define **which aggregates matter**:

- Subsystem-level KPIs for specific classes & labels.
- Flows between subsystems for certain classes.

### 8.2 Driving ADX Queries

For real systems:

- ADX tables store raw/silver telemetry (events, per-node metrics, etc.).
- The gold contract is a layer that produces aggregates matching synthetic bundles.

The subsystem + filter projections become:

- **Reference views** the telemetry pipeline must be able to reproduce.
- **Targets** for permanent ADX queries (e.g., per-subsystem SLA dashboards).
- **Scenarios** for validation:
  - Compare `/metrics?viewMode=subsystems&classId=Order` from synthetic runs vs. equivalent ADX-based aggregates from real telemetry.

### 8.3 Focused Telemetry Generation

By combining:

- View mode (`full` / `subsystems` / `subsystem`).
- Filters (`classId`, `label.*`).

You can define *which subsets* of the system and flows merit high-fidelity telemetry:

- E.g., "all subsystems for `class=Refund` in `region=eu`".
- Telemetry teams can prioritize instrumentation and ADX queries for those slices.

---

## 9. IN/OUT Semantics at Shared Nodes

Some architectures route both **inbound** and **outbound** flows through the same logical node (e.g., an edge gateway, broker, or adapter). For example:

- External clients → `edge-gw` → inner subsystem (inbound leg).
- Inner subsystem → `edge-gw` → external clients (outbound leg).

Physically, `edge-gw` may have multiple replicas (pods) behind a shared queue/service, but FlowTime models a single **logical node** with aggregated metrics.

### 9.1 Topology & Direction Modeling

The DAG remains well-defined:

- Distinct edges represent different directions and roles:
  - `external-in → edge-gw` (inbound entry).
  - `edge-gw → inner-node` (forward into subsystem).
  - `inner-node → edge-gw` (return from subsystem).
  - `edge-gw → external-out` (outbound exit).

Even though inbound and outbound flows visit the same node at different times, their paths are distinguishable by edge IDs and time bins. Once EdgeTimeBin is in place, per-edge, per-class volumes make IN vs OUT flows explicit.

Visualization patterns:

- **Port-based view** (recommended): one node `edge-gw` with edges docking to specific “ports” (inbound, outbound, internal), styled differently in the UI.
- **Role-split nodes** (optional): model two logical nodes (`edge-gw-in`, `edge-gw-out`) that share a label but have distinct metrics/edges.

Replica detail (pods) should generally be handled as silver telemetry via labels (e.g., `label.podName`) and not as separate topology nodes.

### 9.2 Queueing & Metrics at Shared Nodes

Consider a FIFO in-queue in front of `edge-gw` that batches **all documents**, regardless of whether they are on the inbound or outbound leg. If a sudden burst of inbound documents arrives, outbound documents must wait behind them.

FlowTime’s gold model already supports this pattern:

- The node’s **queue** is modeled at the node level (arrival, service, queueDepth, latency) without distinguishing direction.
- If inbound and outbound flows share the same in-queue and capacity, the queueing behavior is the same as any multi-class queue.
- With classes enabled:
  - Both inbound and outbound documents of a given class contribute to the same node-level `arrivals` and `queueDepth` in the bins where they occur.
  - Per-class metrics (`byClass`) can show how much of the queue is due to inbound vs outbound legs **if** those are distinguishable by class or event type.

Implications for metrics:

- **Volume metrics** (arrivals, served, errors):
  - Node totals account for all flows through the node, regardless of direction.
  - Class-filtered views (`classId=...`) can isolate flows of interest (e.g., refund traffic) even when mixed with others.

- **QueueDepth and latency**:
  - QueueDepth is driven by total arrival rate vs service rate at the node.
  - If many inbound documents arrive, QueueDepth rises, and outbound documents entering later will wait longer.
  - FlowTime can model and report this directly:
    - `queueDepth` per bin.
    - Queue latency / waiting time per class, if the simulation or telemetry tracks class-based waiting times.

- **Flow latency** for entities that go IN and OUT via the same node:
  - End-to-end latency for a class (e.g., from `external-in` arrival to `external-out` completion) is computed via the topology and node/service times.
  - Shared nodes with contention simply contribute more to that latency when heavily loaded.

In other words, even if this is not an ideal architecture, FlowTime can still capture and visualize its consequences:

- High queueDepth and latency at the shared node during bursts.
- Class-filtered views showing which flows suffer.
- Subsystem-level views highlighting `outer-shell` bottlenecks when many flows compete at the edge.

### 9.3 Subsystem & IN/OUT Visualization

For subsystems that do most of the work internally but share IN/OUT services:

- At subsystem level:
  - External → `outer-shell` → `inner-core` → `outer-shell` → External.
  - `outer-shell` contains the shared edge services/brokers.
- At node level:
  - Shared nodes (e.g., `edge-gw`) sit within `outer-shell` and show aggregate metrics.
  - Edge overlays (from EdgeTimeBin) and class filters differentiate IN vs OUT flows and which subsystems they connect.

This is a common modern architecture (API gateways, brokers, adapters). FlowTime’s design handles it as:

- A DAG with time-binned metrics and direction-specific edges.
- Shared nodes modeled as multi-class queues.
- Optional subsystem projections to understand where bottlenecks reside.

---

## 10. API Design Choices (GET vs POST)

### 10.1 GET for Primary Surfaces

The pattern above works well with **GET** requests:

- Parameters:
  - `viewMode=full|subsystems|subsystem`.
  - `subsystemId=...` (only when `viewMode=subsystem`).
  - `classId=...` (repeatable).
  - `label.environment=prod`, `label.region=eu`, etc.
  - `startBin`, `endBin` for windowed state.

Benefits:

- Cacheable and bookmarkable URLs.
- Simple and consistent across graph, state, and metrics.

### 10.2 POST for Advanced Queries (Future)

If future requirements demand more complex filters or aggregations (AND/OR logic, custom groupings), we can introduce:

- `POST /v1/runs/{runId}/query` with a JSON body describing:
  - view mode,
  - filters,
  - requested projections (graph, state, metrics).

This should be considered future work; the initial design should keep GET as the primary mode.

---

## 11. Benefits

- **Unified semantics** for zooming and filtering across UI, BI, and telemetry validation.
- **No duplication** of subsystem logic across consumers; Engine defines subsystem boundaries and projections.
- **Extensibility**: view modes and filters are orthogonal and can be extended without breaking basic contracts.
- **Streaming-ready**: contracts are stable whether data is batch or incremental.
- **Telemetry focus**: subsystem + filter views guide where to invest in instrumentation and ADX query development.

---

## 12. Risks & Mitigations

- **Over-complex APIs**:
  - Risk: too many combinations of view modes and filters may confuse users.
  - Mitigation: start with a limited set of modes (`full`, `subsystems`, `subsystem`) and simple filters; document examples.

- **Aggregation correctness**:
  - Risk: inconsistent aggregation (node vs subsystem vs edge) could break invariants.
  - Mitigation: define clear aggregation rules and invariants; use golden tests comparing projections.

- **Performance**:
  - Risk: computing projections on large graphs per request might be expensive.
  - Mitigation: cache common projections (e.g., `viewMode=subsystems`) and reuse aggregated metrics; consider pre-materializing subsystem metrics for hot runs.

- **Streaming partial data**:
  - Risk: users misinterpret partial subsystem metrics as final.
  - Mitigation: include completeness flags (`isFinal`, coverage percentages) and surface them clearly in UI.

---

## 13. Milestone Outline (High Level)

Tentative milestones for subsystem/zooming support (IDs to align with future epic planning):

1. **Model & Graph Support**
   - Add `subsystems` and `subsystemId` to templates/models.
   - Extend `/graph` to include subsystem metadata in `viewMode=full`.

2. **Subsystem Graph Projection**
   - Implement `viewMode=subsystems` and `viewMode=subsystem` for `/graph`.
   - UI renders zoomed-out subsystem view and zoom-in behavior using these.

3. **Subsystem Metrics Projection**
   - Extend `/state_window` and `/metrics` with `viewMode=subsystems` and `viewMode=subsystem`.
   - UI shows subsystem-level KPIs on zoomed-out canvas and in details.

4. **Filtering Integration**
   - Wire class and label filters (`classId`, `label.*`) consistently across all view modes.
   - Validate behavior with synthetic telemetry and Loop tests.

5. **Telemetry & Streaming Readiness**
   - Document how these projections map to ADX queries and telemetry contracts.
   - Add basic streaming-readiness signaling (`isFinal`, coverage) to responses.

These milestones can be refined into a dedicated epic once other high-priority work (e.g., classes, EdgeTimeBin) stabilizes.
