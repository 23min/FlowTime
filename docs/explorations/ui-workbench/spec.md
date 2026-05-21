# Epic: UI Workbench & Topology Refinement

**Status:** draft
**Depends on:** E-11 M3 (topology via dag-map), M4 (timeline & playback), and post-E-16 fact surfaces for richer analytical detail
**Supersedes:** E-11 M5 (Inspector & Feature Bar) — M5 evolves into this epic
**Architecture:** [reference/ui-paradigm.md](reference/ui-paradigm.md)

---

## Intent

Restructure the Svelte topology page from "DAG with overlaid information" to
"clean DAG as navigation surface + workbench panel for inspection and
comparison." This is the foundational paradigm shift described in the UI
Paradigm architecture doc.

## Goals

### G1: Strip the topology to essentials

The DAG shows two things: **structure** (nodes and edges) and **one metric**
(node color). Everything else moves to the workbench.

- Nodes: colored circle/shape, label. No sparklines, no inline numbers, no
  badges, no utilization rings.
- Edges: lines showing flow direction. Width or opacity may encode volume,
  but no labels or numbers on edges.
- Metric selector: a small chip bar (Utilization, Queue Depth, Arrivals,
  Served, Errors, Flow Latency) determines the color basis. One active at
  a time.
- Color scale: visible legend mapping metric values to colors. Consistent
  across light/dark themes.

### G2: Build the workbench panel

A panel (below the topology, or right-side split — TBD during prototyping)
that shows detailed metrics for **pinned** nodes and edges.

**Pinning interaction:**
- Click a node in the DAG to pin it to the workbench.
- Click an edge to pin it.
- Pinned items appear as cards in the workbench.
- Cards can be dismissed (unpin). Order is user-controlled (drag to reorder
  or most-recent-right).
- Auto-pin: on first load, auto-pin the node with the highest utilization
  so the workbench is never empty.

**Workbench card contents (per node):**
- Node ID and kind
- Key metrics at current bin: utilization, queue depth, arrivals, served,
  errors, capacity
- Cycle time decomposition (when available — consumes the stable post-E-16 fact surface for cycle-time outputs):
  queue time, service time, flow efficiency
- Kingman prediction (when available — enriched by resumed p3c diagnostics)
- Sparkline: selected metric over the full time window (small inline chart)
- Warnings/annotations from InvariantAnalyzer (conservation violations,
  capacity warnings)
- Per-class breakdown (expandable section if classes exist)

**Workbench card contents (per edge):**
- Source and target node IDs
- Flow volume at current bin
- Attempt/failure/retry volumes (if effort edge)
- Sparkline of flow volume over time

### G3: Side-by-side comparison

With multiple cards pinned, the workbench naturally supports comparison:
- Two nodes with different utilization are visually adjacent
- Sparklines at the same time scale make trends comparable
- A "compare" toggle could overlay two sparklines on the same axes

### G4: Timeline integration

The timeline scrubber (already implemented in E-11 M4) continues to work as
before. When the user scrubs or plays:
- Topology node colors update per bin
- Workbench card metrics update per bin
- Sparklines show a position indicator for the current bin

### G5: Feature bar simplification

The Blazor feature bar (left panel with 15+ toggles) is replaced by:
- Metric selector (chip bar, one active metric for topology coloring)
- Class filter (if classes exist — dropdown or chip toggle)
- View switcher (topology / heatmap / decomposition — see Analytical Views
  epic)

No overlay toggles, no sparkline-on-node toggles, no edge label toggles.
The workbench handles all detail.

## Component Dependencies

### dag-map library

The workbench paradigm requires two features not yet in dag-map:

- **Node interaction callbacks** ([23min/dag-map#4](https://github.com/23min/dag-map/issues/4)):
  A `bindEvents()` helper for click/hover with event delegation. Without
  this, the Svelte wrapper must manually wire DOM listeners after each
  re-render. Workable but fragile — this is the most important dag-map gap.
- **Selected node state** ([23min/dag-map#5](https://github.com/23min/dag-map/issues/5)):
  A `selected: Set<string>` render option that draws a selection ring on
  pinned nodes. Without this, selection must be done via CSS overrides
  (conflicts with heatmap coloring) or a full `renderNode` override
  (heavy).

Both can be worked around with external wiring until dag-map ships them.
The workbench epic should not block on dag-map changes.

### New components (built in Svelte UI)

- **Workbench card** — node/edge inspection card with metrics, sparkline,
  warnings. Reusable across pinned items.
- **Sparkline** — compact inline SVG chart for metric trends. Shared with
  heatmap tooltips and decomposition view.
- **Tooltip** — positioned overlay for hover context on DAG nodes.

## Non-Goals

- Chart gallery or role-focused dashboards (see Analytical Views epic)
- LLM or DSL integration (see Question-Driven epic)
- Canvas rendering (SVG first; canvas only if measured performance problems)
- Scenario comparison / what-if (see Analytical Views epic, Comparison view)
- New dag-map layout engines (see ui-layout epic)

## Open Questions

1. **Panel position:** Bottom panel (Chrome DevTools style) or right panel
   (VS Code style)? Bottom gives more horizontal space for cards; right
   preserves vertical space for the DAG. Prototype both.

2. **Card density:** How much to show by default vs. behind expandable
   sections? Start minimal (ID, key metrics, sparkline), expand for cycle
   time decomposition, per-class, warnings.

3. **Keyboard navigation:** Should arrow keys move between pinned cards?
   Should there be a shortcut to pin/unpin the hovered node?

4. **Mobile/narrow viewport:** Workbench could become a bottom sheet or
   overlay. Not a priority but worth considering in the layout.

5. **Max pinned items:** Unlimited, or cap at N (e.g., 8) to prevent
   scroll fatigue? Start unlimited, add a cap if UX testing shows problems.

## Milestones

To be defined during planning. Likely sequence:

1. Strip topology (remove overlays, sparklines, badges from DAG nodes)
2. Workbench panel scaffold (pinning, card layout, dismiss)
3. Node card content (metrics, sparkline, warnings)
4. Edge card content
5. Timeline integration (cards update on scrub)
6. Feature bar replacement (metric selector, class filter, view switcher)
