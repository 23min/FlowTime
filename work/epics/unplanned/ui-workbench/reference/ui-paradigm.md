# UI Paradigm: From Topology Overlay to Flow Workbench

**Status:** Design proposal
**Date:** 2026-04-03
**Applies to:** Svelte UI (`ui/`). Blazor UI remains in maintenance mode.

---

## Problem

FlowTime is three things at once:

| Identity | Implies | Natural UI |
|----------|---------|-----------|
| Flow algebra engine | Inspect formulas, trace provenance | Spreadsheet, formula bar |
| Queueing theory made executable | Set parameters, read computed answers | Simulation tool, calculator |
| System topology analyzer | See structure, spot patterns | Graph with overlays, dashboard |

The current UI pursues the third identity: a DAG with overlaid metrics,
sparklines, inspector panels, class filters, and a timeline scrubber. This
converges toward an observability dashboard (Datadog, Grafana) — a category
FlowTime does not belong to and cannot compete in.

The result is a **dimensionality problem**. At any moment the topology view
tries to show topology, a selected metric, time position, per-class breakdown,
constraints, warnings, and trends. That is 5-6 dimensions on a 2D surface.
No amount of visual polish fixes this.

---

## Decision

Restructure the Svelte UI around three complementary paradigms:

### 1. Workbench (foundation)

The DAG becomes a **navigation surface**, not a display surface. It shows
topology and one color dimension (the selected metric). Nothing else on the
nodes — no sparklines, no inline numbers, no badges.

Analytical depth moves to a **workbench panel** below or beside the DAG.
Clicking a node or edge pins it to the workbench. Multiple items can be
pinned for side-by-side comparison.

```
+------------------------------------------+
|  Topology (clean, minimal, clickable)    |
|  Nodes show ONE thing: color = metric    |
|  Click a node -> pins to workbench       |
+------------------------------------------+
|  Workbench (pinned nodes/edges)          |
|  +----------+ +----------+ +-----------+|
|  | api_svc  | | db_pool  | | edge:     ||
|  | rho: 87% | | rho: 43% | | api->db   ||
|  | Q: 14    | | Q: 2     | | vol: 80   ||
|  | CT: 340ms| | CT: 45ms | |           ||
|  | [chart]  | | [chart]  | | [chart]   ||
|  +----------+ +----------+ +-----------+|
+------------------------------------------+
|  Timeline scrubber                       |
+------------------------------------------+
```

**Why:** Separates WHERE (topology) from WHAT and WHY (workbench). Respects
the "spreadsheet for flow dynamics" identity — select cells, inspect them.
People know this pattern from Chrome DevTools (element tree + properties),
Excel (cells + formula bar), Grafana Explore (query panels).

### 2. Layered Views (not one view that does everything)

Instead of overlays on a single DAG, offer distinct views optimized for
different analytical tasks:

| View | Purpose | Representation |
|------|---------|---------------|
| **Topology** | Structure + one metric | DAG with colored nodes |
| **Heatmap** | Patterns across nodes and time | Grid: nodes x bins, colored by metric |
| **Decomposition** | Deep-dive into one node | Stacked bar (queue + service time), Kingman prediction, flow efficiency |
| **Comparison** | What-if reasoning | Two scenarios side-by-side, diff highlighted |
| **Flow Balance** | Model validation | Conservation checks, invariant warnings, annotation list |

These are not tabs that duplicate the DAG. They are **different representations
of the same model**, each optimized for a different task. A heatmap of
utilization-over-time reveals temporal patterns the DAG cannot show.

### 3. Question-Driven Interface (future — depends on Phase 3 + DSL)

Structure analytical queries around **questions FlowTime can answer**:

- "Where is the bottleneck?" -> highlights node with max rho, shows queue growth
- "Why is cycle time high at X?" -> cycle time decomposition + Kingman
- "What if I double capacity at Y?" -> runs scenario, shows diff

This can start as a **structured query panel** (dropdowns + computed answers)
before evolving toward DSL or LLM integration. Not a chatbot — a computation
interface with provenance.

Precedent: Wolfram Alpha (structured computation), ActionableAgile SLE
forecaster (specific questions, probabilistic answers).

---

## Rendering Strategy

**SVG first.** The dag-map library and all new views start with SVG rendering.
SVG provides:

- CSS variable theming (dark/light mode for free)
- DOM events for interaction (click, hover, drag)
- Accessibility (screen readers, keyboard navigation)
- Debuggability (inspect element in browser DevTools)

**Canvas/WebGL when needed.** Switch to canvas only if we encounter concrete
performance problems (hundreds of nodes, animation frame drops, very dense
heatmaps). The trigger should be a measured problem, not a speculative one.

---

## Component Architecture

The new paradigm requires components beyond what dag-map provides. dag-map
is the topology renderer; everything else is built as independent Svelte
components in the UI project.

### dag-map (topology rendering)

**Readiness: high.** dag-map already handles layout, metric coloring (heatmap
mode), theming (CSS variables), and produces clean SVG with data attributes.

Two gaps tracked as issues on the dag-map repo:

- **Node interaction callbacks** ([23min/dag-map#4](https://github.com/23min/dag-map/issues/4)):
  `bindEvents()` helper for click/hover with event delegation and edge hit
  areas. Required for workbench pinning.
- **Selected node state** ([23min/dag-map#5](https://github.com/23min/dag-map/issues/5)):
  `selected: Set<string>` option that renders a selection ring, composable
  with heatmap, dim, and gate modes. Required for workbench pinning visual.

### Heatmap grid (new component)

A **nodes x bins** matrix where each cell is colored by a metric value.
This is NOT dag-map's heatmap mode (which colors nodes on the DAG). It is a
separate SVG component — a colored grid with no topology, no edges.

Responsibilities:
- Render rows (nodes) and columns (bins) as SVG `<rect>` cells
- Color each cell using a shared color scale (reuse dag-map's `colorScales`)
- Row sorting: by node ID, max value, mean, variance
- Click a cell to jump timeline + pin node to workbench
- Hover tooltip with exact value
- Row/column labels

Performance consideration: a 20-node x 100-bin grid = 2000 `<rect>` elements,
well within SVG limits. For 50+ nodes x 200+ bins, consider cell-size
reduction or row virtualization before moving to canvas.

### Chart primitives (new components)

The decomposition, flow efficiency, and utilization views need small,
purpose-built SVG charts. These should be built as a shared set of Svelte
components, not as a charting library:

- **Stacked bar:** queue time + service time per bin (decomposition view)
- **Line chart:** flow efficiency, utilization over time
- **Area chart:** queue depth over time
- **Sparkline:** compact inline trend for workbench cards

Design principles:
- Bespoke SVG, no charting library dependency (consistent with E-14 non-goals)
- Shared axes and scales across charts in the same view
- Tailwind + CSS variables for theming consistency
- Responsive (scale to container width)

These are authored in the Svelte UI project (`ui/src/lib/components/charts/`)
and are not part of dag-map.

### Tooltip component (new component)

A positioned overlay `<div>` that appears on hover over DAG nodes, heatmap
cells, and chart data points. Shows contextual metric values. Managed by
Svelte (not SVG `<title>` elements) for styling control and positioning.

---

## Relationship to Existing Work

| Epic | Relationship |
|------|-------------|
| **E-11 (Svelte UI)** | M1-M4 complete. M5 (Inspector) evolves into the Workbench paradigm. M6 (Run Orchestration) unchanged. M7 (Dashboard) absorbed into Analytical Views. M8 (Polish) applies to new paradigm. |
| **E-14 (Visualizations)** | Absorbed into the Analytical Views epic. The "chart gallery" concept is replaced by purpose-built views (heatmap, decomposition, comparison). Role-focused chart bundles may still exist as presets within views. |
| **ui-layout (Layout Motors)** | Unchanged — layout engine abstraction is orthogonal to the paradigm shift. |
| **dag-map library** | Continues as the topology rendering engine. Gaps tracked: [#4](https://github.com/23min/dag-map/issues/4) (interaction callbacks), [#5](https://github.com/23min/dag-map/issues/5) (selected state). |

---

## Blazor UI

The Blazor UI (`src/FlowTime.UI/`) enters **maintenance mode**:

- Continues to work with evolving APIs (endpoint changes, new fields)
- No new features or visual improvements
- Remains usable as the reference implementation while Svelte catches up
- No effort spent on CSS debt or MudBlazor upgrade

---

## Risks

| Risk | Mitigation |
|------|-----------|
| Innovation means people don't understand | Workbench uses familiar patterns (select + inspect, side-by-side). Only the composition is novel, not the components. |
| Stripped DAG feels empty | Color coding carries information. The workbench immediately rewards clicking. First-run experience should auto-pin the highest-utilization node. |
| Too many views fragment the experience | Views share the same model, same timeline, same selections. Switching views preserves context. Start with 2 views (topology + heatmap), add more only when Phase 3 primitives are ready. |
| SVG performance at scale | Monitor node count thresholds. The heatmap is the most likely bottleneck (nodes x bins cells). Virtualization or canvas fallback if needed. |
