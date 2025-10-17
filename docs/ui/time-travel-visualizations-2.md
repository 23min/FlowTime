# FlowTime Time‑Travel UI — Wireframes v2
Date: October 16, 2025
Scope: Full UI flow for time‑travel features using “gold” run bundles. Keep docs/ui/time-travel-visualizations.md for reference; this file proposes the end‑to‑end UI and concrete data bindings for the new views.

---

## Objectives

- Cover 80% of exec + ops lighthouse scenarios without transactional/customer data.
- Center the experience on time travel (scrub, play, compare) and flow‑level analysis (paths, propagation, bottlenecks, SLA).
- Operate directly on gold run bundles (JSON artifacts) until REST endpoints are finalized.

---

## Global Shell

```
+--------------------------------------------------------------------------------------------------+
| FlowTime ▾  Run: run_20251007 …  |  Time: ━━●━━━━ 14:00  |  Range: Last 24h ▾  |  Compare ▢      |
| [Dashboard] [Flows] [Analysis] [Compare] [Reports]                                 [Filters ▾]  |
+--------------------------------------------------------------------------------------------------+
```

Global controls
- Run selector: choose a run bundle to explore (gold data).
- Time scrubber: drag/play across bins; keyboard: ←/→ bin, Space play/pause.
- Range: quick presets (1h, 6h, 24h, 7d) bound to visible bins.
- Compare toggle: enable baseline overlay (select another run).
- Filters: text search flows, status filters (breach, warn, healthy), node type filters.

Time model
- All charts sync to the current bin/time range; scrubbing updates views within 200 ms target.
- Playback: 0.5×, 1×, 2× speeds.

---

## View A — SLA Dashboard (Exec Lighthouse)

Purpose: At‑a‑glance SLA status, trends, and projection.

```
+----------------------------------------------------------------------------------------------+
| SLA Dashboard                                                            [Time] ━━●━ 14:00  |
| Range: Last 24h ▾   Forecast: On ▣   Sort: Risk ▾                                         |
|                                                                                             |
|  [ Orders   95.8%  ✓  23/24 ]   [ Billing  91.2%  !  22/24 ]   [ Inventory 78.5% ✕ 19/24 ] |
|   ▁▂▃▅▇▅▃▂  Δ+2.1%      ▁▂▃▅▇▆▅▃  Δ−1.3%         ▁▃▅▇██▇▅  Δ−4.7%                          |
|                                                                                             |
|  SLA Timeline (selected flow) — line + forecast band + threshold                            |
|  ┌───────────────────────────────────────────────────────────────────────────────┐         |
|  │    forecast band (green α)                                                    │         |
|  │  ──────────────────────────────╳─────(threshold)────────────────────────────  │         |
|  └───────────────────────────────────────────────────────────────────────────────┘         |
|                                                                                             |
|  Top Risks (chips):  Inventory (proj breach) • Payments (low margin) • Billing (flat)      |
+----------------------------------------------------------------------------------------------+
```

Key interactions
- Click a tile → opens View B Flow Topology focused on that flow.
- Hover tile → tooltip (worst/avg latency, errors, bins met/missed).
- Forecast band toggle overlays projected SLA based on `metrics.forecast`.

---

## View B — Flow Topology with Time Scrubber (Ops Lighthouse)

Purpose: Visualize topology state at the current bin, watch changes over time.

```
+--------------------------------------------------------------------------------------------------+
| Orders Flow                                         [Time] ━━●━━━━━━ 14:00  [▶] [◀] [▶] [Speed] |
| SLA 95.8%  |  Breaches: 1/24  |  Path: Critical ▾  |  Show: Queues ▣ Errors ▣ Utilization ▣    |
|--------------------------------------------------------------------------------------------------|
|                                                                                                  |
|   ┌─────────────┐        ┌─────────────┐           ┌───────────────┐                            |
|   │ OrderSvc    │───▶───▶│ OrderQueue  │───▶──────▶│ BillingSvc    │                            |
|   │ Lat 0.5m    │        │ Q 25  Lat10 │           │ Lat 2.1m      │                            |
|   │ Arr150 Srv145│       │ Util 93%    │           │ Util 72%      │                            |
|   │ ▂▃▅▇▅▃▂     │        │ ▁▃▅▇██▇▅   │           │ ▂▃▅▃▂         │                            |
|   └─────────────┘        └─────────────┘           └───────────────┘                            |
|                                                                                                  |
|  [ Node Details ▾ ]  Arrivals • Service • Latency • Queue • Errors • Retries (mini trends)      |
|  [ Propagation ▾ ]  First degraded: OrderQueue@13:55   Delay map: A→B 2m, B→C 6m               |
+--------------------------------------------------------------------------------------------------+
```

Core behaviors
- Node coloring: SLA or utilization thresholds (Green/Yellow/Orange/Red); Gray for no data.
- Edge pulses: animate downstream to visualize wave propagation around spikes.
- Node click: opens side panel with metric cards and a larger mini‑chart strip.
- Hold Shift and click two nodes: highlight the critical path between them (if available).
- Play controls: scrub or animate; graph updates smoothly at 60 FPS target.

Panels
- Node Details: metric values at bin, 10‑bin sparkline, current vs baseline deltas (if Compare enabled).
- Propagation: first degraded node, hop delays, amplification ratios; link to View D Wave.

---

## View C — Path Analysis (Exec + Ops)

Purpose: Evaluate critical and alternative paths end‑to‑end without customer drilldown.

```
+----------------------------------------------------------------------------------------+
| Path Analysis — Orders Flow                         [Time] 14:00 | Range: 24h ▾       |
|----------------------------------------------------------------------------------------|
|  Rank  Path                     Total Lat   Prob   Bottleneck    Δ vs Baseline         |
|   1    Critical (A→B→D)         16.2 m     0.42   OrderQueue    −1.1 m                |
|   2    Alt A (A→C→D)            18.9 m     0.31   PaymentAPI    +0.6 m                |
|   3    Alt B (A→E→D)            19.4 m     0.19   BillingSvc    +0.3 m                |
|----------------------------------------------------------------------------------------|
|  Contribution bars (Critical Path):                                                     |
|  A  25%  |███████▌                                                            |
|  B  45%  |███████████████▌                                                    |
|  D  30%  |█████████                                                           |
|                                                                                 Compare ▣       |
+----------------------------------------------------------------------------------------+
```

Interactions
- Select a row: highlights the path on the Flow Topology (curved edges, badges) and keeps the list pinned.
- Toggle Compare: overlay baseline contribution bars and deltas.

---

## View D — Wave Propagation (Ops Lighthouse)

Purpose: Track how a spike propagates over time and where it amplifies.

```
+---------------------------------------------------------------------------------------------+
| Wave Propagation — Orders Flow                         Window: 13:40–14:10 ▾               |
|---------------------------------------------------------------------------------------------|
| Heatmap (nodes × time bins) with delay/amplification legend                                 |
| ┌──────────────────────────────────────────────────────────────────────────────┐            |
| │ node1 ▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒                                                    │            |
| │ node2   ▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒                                                │            |
| │ node3       ██████████████████████                                           │            |
| │ node4                ░░░░░░░░░░░░░░░░░░                                      │            |
| └──────────────────────────────────────────────────────────────────────────────┘            |
| Cascade Timeline (ordered by first degradation)                                              |
| [ OrderSvc ]── 2m ──[ OrderQueue ]── 6m ──[ BillingSvc ]── 3m ──[ Payment ]                  |
+---------------------------------------------------------------------------------------------+
```

Interactions
- Click a heatmap cell → jump the global scrubber to that bin and focus the node in View B.
- Toggle metric layer: latency/queue depth/utilization; tooltips show first‑spike bin and delay.

---

## View E — Compare Runs (Exec)

Purpose: Validate improvements or regressions between two runs.

```
+------------------------------------------------------------------------------------------------+
| Compare: run_20251007 vs baseline run_20250930     Sync Scrub ▣    Diff Highlights ▣         |
|------------------------------------------------------------------------------------------------|
| SLA Timeline (overlay)   Current: Indigo   Baseline: Gray   Δ chips: +1.8% / −0.3m p95        |
| Flow Topology (Δ badges) Node borders show up/down deltas; tooltips show value pairs          |
+------------------------------------------------------------------------------------------------+
```

---

## View F — Compliance Report (Reports)

Purpose: Exportable summary for audits without drilling into transactions.

Contents
- Bins met/missed, worst latency, error totals, top 3 risk flows.
- Projection note if any breaches likely within the next window.
- Export: PNG/PDF (server assisted later; client print‑style now).

---

## Components & Rendering

Rendering choices
- Canvas: Flow topology, propagation heatmap, cascade timeline (performance at scale, animations).
- SVG: Inline sparklines/mini histograms in cards and list rows (crisp text/layout control).
- Chart library: SLA timelines, stacked areas, comparison overlays (tooltips, legends out‑of‑box). MudBlazor + ApexCharts/ECharts wrapper recommended for richer types (heatmap, bands).

Mini‑charts
- Node card sparkline: 10 bars (queue) or line (latency), color‑coded by status.
- SLA tile delta spark: thin 12‑point line with positive/negative coloring.

States
- Empty: “No data for selected window” with quick range shortcuts.
- Loading: lightweight shimmer lines/boxes; keep scrubber usable.
- Error: toast with retry; render last good snapshot if available.

Accessibility
- Keyboard: Tab focusable tiles and nodes; ←/→ scrub, Space play/pause, Enter open details.
- Colorblind‑safe accents: add shapes/icons to reinforce color status.
- Descriptive labels on charts; aria‑labels on controls.

---

## Data Contracts (Gold Bundle Artifacts)

Until REST endpoints are ready, the UI reads JSON artifacts from the run bundle. Proposed minimal files and fields:

1) runs/{runId}/graph.json
```json
{
  "flow": "Orders",
  "nodes": [ { "id": "OrderSvc", "type": "service" }, { "id": "OrderQueue", "type": "queue" }, { "id": "BillingSvc", "type": "service" } ],
  "edges": [ { "from": "OrderSvc", "to": "OrderQueue" }, { "from": "OrderQueue", "to": "BillingSvc" } ],
  "sla": { "latencyThresholdMin": 2.0 }
}
```

2) runs/{runId}/state_window.json
```json
{
  "binMinutes": 5,
  "bins": [ { "t": "2025-10-07T13:55:00Z", "nodes": { "OrderSvc": {"arr":150, "srv":145, "lat":0.5, "util":0.72, "err":2 }, "OrderQueue": {"q":25, "lat":10.7, "util":0.93, "err":0}, "BillingSvc": {"lat":2.1, "util":0.72} } } ]
}
```

3) runs/{runId}/metrics.json (SLA aggregates + forecast)
```json
{
  "flows": [
    { "name": "Orders", "slaPct": 95.8, "binsMet": 23, "binsTotal": 24, "deltaPct": +2.1, "forecast": { "projectedBreach": false } },
    { "name": "Billing", "slaPct": 91.2, "binsMet": 22, "binsTotal": 24, "deltaPct": -1.3, "forecast": { "projectedBreach": false } }
  ]
}
```

4) runs/{runId}/path-analysis.json
```json
{
  "flow": "Orders",
  "paths": [
    { "id": "critical", "nodes": ["A","B","D"], "totalLatencyMin": 16.2, "prob": 0.42, "bottleneck": "OrderQueue", "rank": 1 },
    { "id": "alt-a", "nodes": ["A","C","D"], "totalLatencyMin": 18.9, "prob": 0.31, "bottleneck": "PaymentAPI", "rank": 2 }
  ]
}
```

5) runs/{runId}/wave-propagation.json
```json
{
  "firstSpikeBinByNode": { "OrderQueue": 167, "BillingSvc": 169 },
  "delayMsByNode": { "OrderQueue": 120000, "BillingSvc": 480000 },
  "amplificationByNode": { "OrderQueue": 1.6, "BillingSvc": 1.2 }
}
```

Compare support (optional now)
- runs/{runId}/baseline.json → { "baselineRunId": "run_20250930..." }
- The UI loads the same artifacts from the baseline run and renders overlays/deltas.

Mapping to future REST
- metrics.json → GET /v1/runs/{id}/metrics
- graph.json + state_window.json → GET /v1/runs/{id}/graph, /state, /state_window
- path-analysis.json → GET /v1/runs/{id}/path_analysis
- wave-propagation.json → GET /v1/runs/{id}/wave

---

## Interaction Specs

Time scrubber
- Drag: live updates at ≤200 ms; snapping to bin boundaries.
- Click on any chart (timeline/heatmap) sets the global cursor.

Playback
- Controls: ▶/❚❚, ◀ Prev bin, Next ▶, Speed ▾ (0.5×/1×/2×).
- During playback, heavy components (topology/heatmap) repaint on requestAnimationFrame.

Selection & linking
- Selecting a flow/node in one view synchronizes highlights in others (e.g., selecting a path highlights edges on the topology).
- Deep linkable URLs: include runId, flow, bin index, selected path.

Keyboard & a11y
- Focus ring on tiles/nodes. Shortcuts: ←/→ scrub, Space play/pause, Enter details, “c” compare.

---

## Performance & Limits

Targets
- Initial load ≤ 2 s; scrub update ≤ 200 ms; playback 60 FPS.

Assumptions
- Max 20 nodes per flow; ≤ 10 flows on dashboard; ≤ 200 bins visible per query.

Rendering
- Topology: canvas + offscreen buffer for node textures; hit‑testing via bounding boxes.
- Heatmap: canvas tile rendering; optional WebGL fallback later if needed.

---

## MVP and Phasing

MVP (Priority 1)
- SLA Dashboard tiles + timeline strip (no forecast band needed at first).
- Flow Topology with scrubber, node colors, Node Details panel.
- Path Analysis list + highlight on topology.

Priority 2
- Wave Propagation heatmap + cascade timeline.
- Compare runs overlays (timeline + node deltas).

Priority 3
- Compliance report export (print‑style PDF), keyboard shortcuts, mobile layout.

---

## Open Questions

- Do we show utilization vs SLA as combined border color or separate badges per node?
- Which forecast method are we comfortable previewing (simple carry, EWMA)?
- Path probability: how do we compute under gold constraints; any heuristics OK for UI demo?
- Alert markers: will we ingest any alert times to annotate timelines in MVP?

---

End of Wireframes v2

