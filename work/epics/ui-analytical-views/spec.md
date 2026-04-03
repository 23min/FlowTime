# Epic: Analytical Views

**Status:** draft
**Depends on:** UI Workbench epic (for view switcher and shared state),
E-10 Phase 3 (for analytical primitives)
**Absorbs:** E-14 (Visualizations / Chart Gallery) — the chart gallery concept
is replaced by purpose-built analytical views
**Architecture:** [docs/architecture/ui-paradigm.md](../../../docs/architecture/ui-paradigm.md)

---

## Intent

Add distinct views alongside the topology, each optimized for a different
analytical task. These are not tabs that duplicate the DAG — they are different
representations of the same model, sharing timeline position, node selection,
and class filters.

## Goals

### V1: Heatmap View

A grid visualization: **nodes on one axis, time bins on the other**, each cell
colored by the selected metric.

**What it reveals that the topology cannot:**
- Temporal patterns — a node that is fine in bins 1-4 but saturated in bins
  5-8 is invisible on the topology (which shows one bin at a time)
- Cross-node comparison — which nodes are consistently hot vs. cold
- Transient events — a spike in queue depth at bin 3 that clears by bin 5

**Interaction:**
- Same metric selector as topology (Utilization, Queue Depth, etc.)
- Click a cell to jump the timeline to that bin and pin that node in the
  workbench
- Hover shows tooltip with exact value
- Rows sortable by: node ID, max value, mean value, variance

**Rendering:** SVG grid. Each cell is a `<rect>` with fill from the color
scale. For large models (50+ nodes x 100+ bins), consider virtualization or
cell-size reduction before switching to canvas.

**Data source:** `/state_window` provides per-node, per-bin metrics. No new
API needed.

### V2: Decomposition View

A per-node deep-dive showing **cycle time decomposition** and queueing
theory diagnostics.

**Depends on:** m-ec-p3a (cycle time), m-ec-p3c (Kingman) — gracefully
degrades when these are unavailable (shows only what the engine provides).

**Contents:**
- **Stacked bar over time:** queue time (bottom) + service time (top) = cycle
  time, per bin. Visually shows where time is spent and how it changes.
- **Flow efficiency trend:** line chart of serviceTime/cycleTime per bin.
- **Kingman prediction overlay:** dashed line showing predicted queue wait
  time from Kingman's approximation, compared to actual queue time. Delta
  highlights where the model diverges from theory (suggesting missing factors).
- **Utilization trend:** line chart of rho per bin, with warning thresholds
  (70%, 90%) as horizontal reference lines.
- **Queue depth trend:** area chart showing inventory accumulation.
- **Annotations:** conservation warnings, starvation/blocking flags,
  non-stationarity warnings from AC-5.

**Node selection:** The decomposition view shows the node currently selected
in the topology or workbench. Switching nodes updates the view.

**Rendering:** SVG charts. Bespoke — no charting library. Consistent
styling with the rest of the Svelte UI (Tailwind + CSS variables for theming).

### V3: Comparison View

Side-by-side visualization of two runs (or two scenarios from the same model
with different parameters).

**Depends on:** Scenario Overlay infrastructure (overlays epic). Can start
with two separate runs compared manually.

**Contents:**
- Two topology DAGs side by side, same layout, same color scale
- Diff overlay: nodes that changed significantly are highlighted (outline
  or glow)
- Metric diff table: for each node, show metric in run A, metric in run B,
  delta, % change
- Timeline synced across both panels

**Rendering:** Two SVG DAGs + a diff table. Reuses existing dag-map renderer.

### V4: Flow Balance View

A validation-focused view showing conservation checks and model health.

**Contents:**
- Table of all invariant check results from InvariantAnalyzer
- Per-node: arrivals, served, errors, queue delta, conservation balance
- Warnings highlighted (non-zero balance, capacity violations, etc.)
- Per-edge: flow volume consistency checks
- Color coding: green (balanced), yellow (within tolerance), red (violation)

**Rendering:** Styled table. Simple.

### V5: Role-Based Chart Bundles (from E-14)

Curated chart combinations for specific audiences:

| Role | Charts | Purpose |
|------|--------|---------|
| Executive | Throughput trend, flow efficiency, bottleneck location | "Is the system healthy?" |
| SRE | Utilization heatmap, queue depth trends, retry tax | "Where is the risk?" |
| Support | Cycle time decomposition, SLA compliance, class breakdown | "Why are customers waiting?" |

These are **presets** that select specific views and metrics, not separate
view implementations. An "Exec" preset might open the heatmap with
utilization selected + the decomposition for the bottleneck node.

## Component Dependencies

### New components (built in Svelte UI)

These views require SVG components that are NOT part of dag-map. They live
in the Svelte UI project at `ui/src/lib/components/charts/` (or similar).

- **Heatmap grid** — SVG `<rect>` matrix (nodes x bins), colored by metric.
  Reuses dag-map's `colorScales` module for consistent color mapping. Not
  the same as dag-map's heatmap mode (which colors nodes on the DAG). For
  a 20x100 grid (2000 cells), SVG is fine. Monitor performance if models
  grow beyond 50 nodes x 200 bins.

- **Stacked bar chart** — queue time + service time per bin for cycle time
  decomposition. Bespoke SVG, no charting library.

- **Line chart** — flow efficiency, utilization trends, Kingman prediction
  overlay. Shared axes with stacked bar in decomposition view.

- **Area chart** — queue depth over time.

- **Sparkline** — shared with workbench cards. Compact inline trend.

Design principles for all chart components:
- Bespoke SVG (no charting library dependency)
- Tailwind + CSS variables for theming (light/dark)
- Responsive to container width
- Shared scale/axis utilities across components to prevent drift
- Click and hover emit events that integrate with workbench pinning and
  timeline scrubber

### dag-map (comparison view only)

The comparison view (V3) renders two dag-map instances side by side with
different metrics. dag-map's deterministic layout ensures identical node
positions. Diff highlighting (changed nodes) uses the `selected` feature
from [23min/dag-map#5](https://github.com/23min/dag-map/issues/5) or a
CSS class toggle.

## Non-Goals

- General-purpose charting library or chart builder
- Real-time / live data (FlowTime is batch/deterministic)
- Custom chart authoring by end users
- Canvas rendering (SVG first)

## Open Questions

1. **View switching UX:** Tabs? A sidebar icon strip (VS Code style)?
   Keyboard shortcuts? Start with tabs, iterate.

2. **Heatmap axis orientation:** Nodes as rows + bins as columns (reads
   left-to-right over time), or bins as rows + nodes as columns? Standard
   convention is time on x-axis, so nodes-as-rows is likely better.

3. **Decomposition: one node or compare two?** Start with one. If the
   workbench epic delivers side-by-side cards, comparison is already
   available there.

4. **Chart styling:** Establish a small charting primitive library (axes,
   scales, bars, lines, areas) shared across views? Or build each view
   independently? A shared primitive set prevents drift but adds upfront
   cost. Decide during first implementation.

5. **E-14 transition:** Formally close E-14 and point to this epic, or
   keep E-14 as the "role-based presets" milestone within this epic?

## Milestones

To be defined during planning. Likely sequence:

1. View switcher infrastructure (shared state, URL routing, transitions)
2. Heatmap view (V1) — highest standalone value, least dependency on Phase 3
3. Decomposition view (V2) — after m-ec-p3a ships
4. Flow balance view (V4) — uses existing InvariantAnalyzer output
5. Comparison view (V3) — after scenario overlay infrastructure
6. Role presets (V5) — after V1-V4 are stable
