# FlowTime Time‑Travel UI — Wireframes v3 (Minimal)
Date: October 16, 2025
Scope: Minimal set to ship quickly with gold data. This spec narrows the UI to three visualizations and a few shared components. No forecasting, no transactional drilldowns, no customer/class segmentation.

---

## Goals
- Keep it simple and fast to implement while covering core workflows.
- Center on time scrubbing and clear status: what’s healthy, what isn’t, and where.
- Operate on gold run bundles; REST endpoints may come later.

---

## Global Layout (Top Bar + Scrubber)

```
+--------------------------------------------------------------------------------------------------+
| FlowTime ▾  Run: run_…  |  Range: Last 24h ▾  |  Start: 2025‑10‑07 13:00Z  End: 2025‑10‑07 14:00Z |
| Filter: [ text ]  Status: [ All ▾ ]  Node Type: [ All ▾ ]                                  [Help] |
| Time: ━━━━━━━●━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ 14:00     [▶]   |
+--------------------------------------------------------------------------------------------------+
```

Controls
- Run selector: choose a run bundle.
- Time range selector: presets (1h, 6h, 24h, 7d) or absolute Start/End. Used to scope queries to `startTimeUtc`/`endTimeUtc` (or bin range).
- Filter: text (node id contains), optional status/type filters.
- Scrubber: global time cursor; playback (▶) and step (←/→) control.

Notes
- All views listen to the global scrubber and range.
- Keep the scrubber always visible (top bar) for tight feedback.

---

## Visualization 1 — Flow Topology with Time Scrubber (Heat Map Coloring)

Purpose: Show the end‑to‑end topology (entire graph). Color nodes by health at the current time. Clicking a node opens the Node Detail Panel.

Rendering: Canvas (for performance with many nodes/edges). Sparklines on nodes can be SVG overlays if needed (optional).

```
+--------------------------------------------------------------------------------------------------+
| Topology — Entire Graph                                                                       │X│ |
|--------------------------------------------------------------------------------------------------|
|  (Canvas)                                                                                       |
|                                                                                                 |
|    [ OrderSvc ]──▶──[ OrderQueue ]──▶──[ BillingSvc ]──▶──[ PaymentAPI ]                        |
|     (green)            (red)               (yellow)              (green)                        |
|                                                                                                 |
|  Pan (drag), Zoom (wheel), Reset [⟲]                                                            |
|--------------------------------------------------------------------------------------------------|
|  Node Detail Panel  (opens on click)                                                            |
+--------------------------------------------------------------------------------------------------+
```

Coloring (heat map like, simple thresholds)
- If node has SLA latency threshold `slaMin`: Green if `lat <= slaMin`, Yellow if `slaMin < lat <= 1.5*slaMin`, Red if `lat > 1.5*slaMin`.
- Else by utilization: Green `< 0.7`, Yellow `0.7–0.9`, Red `>= 0.9`.
- Gray for “no data” in current bin.

Edges
- Directed arrows; optional width proportional to `srv` (served throughput) for visual emphasis.

Interactions
- Click node → opens Node Detail Panel (right side). Panel remains sticky across scrub changes, updating the charts.
- Shift + drag canvas → lasso select (optional later). MVP: pan/zoom only.
- Keyboard: `←/→` previous/next bin; `Space` play/pause.

Data binding
- Topology: `graph.json` (nodes, edges, optional node `type`: `service|queue`).
- At current bin: lookup in `state_window.json.bins[i].nodes[nodeId]` for `lat`, `util`, `q`, `arr`, `srv`, `err`.

---

## Visualization 2 — SLA Dashboard (Exec)

Purpose: Tiles per flow with a tiny mini bar sparkline; no forecast, just current range facts.

Rendering: Tiles as regular components; mini‑bars as SVG (tiny, crisp). No advanced charting.

```
+--------------------------------------------------------------------------------------------------+
| SLA Dashboard                                                                                   |
|--------------------------------------------------------------------------------------------------|
| [ Orders     95.8%  ✓  23/24 ]   ▁▂▃▅▇▅▃▂   (mini bars)                                         |
| [ Billing    91.2%  !  22/24 ]   ▁▃▅▇▆▅▃▁                                                       |
| [ Inventory  78.5%  ✕  19/24 ]   ▁▃▅▇██▇▅                                                       |
|   Sort: SLA% ▾   Filter: text …                                                                  |
|                                                                                                  |
+--------------------------------------------------------------------------------------------------+
```

Tile contents
- Flow name, SLA % (bins meeting SLA / total bins), status icon ✓/!/✕ based on SLA % thresholds (Green ≥95, Yellow [90–95), Red <90).
- Mini bar sparkline (7–10 bars) of SLA‑per‑bin or primary KPI; keep it strictly visual (no axes/ticks).

Interactions
- Click a tile → focuses the Topology view with that flow filtered/highlighted if flow tagging exists; otherwise, jumps to the topology and scrolls node list to likely nodes (optional). MVP: click navigates to Topology without filter.

Data binding
- `metrics.json.flows[]` with `{ name, slaPct, binsMet, binsTotal, mini: [0..1] }`.
- If flows are not present in the dataset, show a single “Overall” tile derived from global SLA aggregates.

---

## Visualization 3 — Node Detail Panel (Line Charts)

Purpose: On node click, show a right‑side panel with basic lines over the active time range.

Rendering: Use a chart component or simple SVG lines (keep it minimal). SVG is acceptable given 1–3 series per chart.

```
+---------------------------------- Node: OrderQueue (queue) ----------------------------------+
| Summary  |  Lines  |  Info                                                                            |
|-----------------------------------------------------------------------------------------------|
| Summary: Lat 10.7 m  •  Q 25  •  Util 0.93  •  Err 0                                            |
| Lines:                                                                                          |
|  - Queue Depth (bars or line)                                                                   |
|  - Latency (line)                                                                               |
|  - Arrival vs Service (two lines)                                                               |
| Controls:  Metric ▾  |  Window: sync with global  |  Export PNG                                   |
+------------------------------------------------------------------------------------------------+
```

Node types and defaults
- Queue node: Queue Depth, Latency, Arrival vs Service.
- Service node: Latency, Utilization, Errors.

Mini trends vs. lines
- Keep mini bars only in SLA tiles and (optionally) inline in node cards. The Node Detail Panel uses simple lines for readability.

Data binding
- From `state_window.json` sliced to the selected range. For each bin, plot series by `nodeId`.

---

## Shared Components

Minimal Minigraph (SVG)
- Visual only; 7–10 bars arranged left→right. Input values normalized 0..1.
- Used in SLA tiles and optionally on node boxes in the topology (MVP optional for topology).

Node Detail Panel
- Opens when a node is clicked; sticky across scrubs. Shows current bin values and line charts for the selected range.

Time Range Selector
- Quick presets + absolute start/end. Drives data queries via `startTimeUtc` and `endTimeUtc` (or visible bin indices).

Top Bar
- Hosts run selector, filters, range, and scrubber. Always visible.

Scrubber
- Single global cursor with keyboard and play controls. Updates all views.

---

## Data Contracts (Gold Bundle, Minimal)

1) runs/{runId}/graph.json
```json
{
  "nodes": [ { "id": "OrderSvc", "type": "service" }, { "id": "OrderQueue", "type": "queue" }, { "id": "BillingSvc", "type": "service" } ],
  "edges": [ { "from": "OrderSvc", "to": "OrderQueue" }, { "from": "OrderQueue", "to": "BillingSvc" } ],
  "sla": { "latencyThresholdMin": 2.0 }
}
```

2) runs/{runId}/state_window.json
```json
{
  "binMinutes": 5,
  "bins": [
    { "t": "2025-10-07T13:55:00Z",
      "nodes": {
        "OrderSvc":   { "arr": 150, "srv": 145, "lat": 0.5,  "util": 0.72, "err": 2 },
        "OrderQueue": { "q": 25,  "lat": 10.7, "util": 0.93, "err": 0 },
        "BillingSvc": { "lat": 2.1, "util": 0.72, "err": 0 }
      }
    }
  ]
}
```

3) runs/{runId}/metrics.json
```json
{
  "flows": [
    { "name": "Orders", "slaPct": 95.8, "binsMet": 23, "binsTotal": 24, "mini": [0.1,0.2,0.35,0.6,0.8,0.6,0.3,0.2] }
  ]
}
```

Range parameters
- When requesting/slicing `state_window.json`, use `startTimeUtc`/`endTimeUtc` (or derive bin indices) to bound the visible series.

---

## Rendering Choices
- Canvas: Topology (pan/zoom, many edges), edge pulses optional later.
- SVG: Mini bar sparkline; Node Detail simple line charts.
- Chart library: Optional; SVG lines are acceptable for MVP.

---

## Interaction Details
- Scrub updates: ≤200 ms target for canvas recolor + panel value updates.
- Click node: open panel; panel follows scrub (series update). ESC closes.
- SLA tile click: navigate to Topology; if flow tagging exists, filter highlight; otherwise, center/zoom to related nodes (future).
- Keyboard: Tab focus; ←/→ scrub one bin; Space play/pause.

---

## MVP Checklist
- Top Bar with range + scrubber + basic filters.
- Topology canvas: load `graph.json`, paint nodes/edges, color by current bin; click → Node Detail.
- SLA Dashboard: tiles with mini bars from `metrics.json` (no forecast).
- Node Detail Panel: 1–3 simple lines from `state_window.json` over the selected range; current bin values.
- Data loading: gold files only; no backend changes required.

Out of scope (for now)
- Forecasting, anomaly overlays, alert markers, path analysis boards, propagation heatmaps, compare runs.

---

End of Wireframes v3 (Minimal)

