# UI‑M‑03.22.2 — Topology Canvas Polish (Expr Tooltip + Inspector Sparkline)

**Status:** 🔄 In Progress  
**Dependencies:** ✅ UI‑M‑03.22 (Topology Canvas), ✅ UI‑M‑03.22.1 (LOD + Feature Bar)  
**Target:** Enhance expression/computed node usability with inline sparklines and expression text while reducing visual clutter on computed nodes.

---

## Overview

This micro‑milestone polishes the Topology experience by adding a mini sparkline to expression node tooltips, surfacing the expression text and an enlarged sparkline in the node inspector, simplifying computed node visuals (no port “lollipops”), and showing the current bin(t) metric directly inside computed nodes.

### Strategic Context
- Motivation: Operators need quick context for expression/computed nodes without leaving the topology.
- Impact: Faster diagnosis (sparkline trend at a glance), clearer dependency visuals, consistent inspector experience.
- Dependencies: Existing per‑run window (`state_window`) and graph contracts; inspector shell already present.

---

## Scope

### In Scope ✅
1. Tooltip sparkline for expression nodes (canvas‑drawn, under subtitle).
2. Inspector: expression text (monospace) + enlarged sparkline (SVG, no chart library).
3. Remove circular port markers ("lollipops") on edges connected to computed nodes (expr/const/pmf).
4. Show bin(t) metric inside computed nodes; value updates on scrub and hover/focus.

### Out of Scope ❌
- ❌ Full node‑panel analytics (multiple charts, tables) — tracked under UI‑M‑03.23.
- ❌ Server‑side aggregation changes.
- ❌ New charting dependencies or component libraries.

### Future Work
- Optional per‑node detail popover for dependency breakdown (if needed after validation).

---

## Requirements

### Functional Requirements

#### FR1: Expr Tooltip Sparkline
**Description:** When hovering an expression node, the canvas tooltip renders a compact sparkline using the per‑run window series for that node.
**Acceptance Criteria:**
- [ ] Sparkline renders below the tooltip subtitle (60–100 px width), basis‑tinted stroke, no legend.
- [ ] Visible for expression nodes with series present; degrades to muted “No series” message if absent.
- [ ] Tooltip layout and 8 px left alignment remain unchanged.

#### FR2: Inspector — Expression Text (Mono)
**Description:** The node inspector shows the expression string in a monospace block.
**Acceptance Criteria:**
- [ ] Monospace rendering; dark grey text on very light grey background (Template Studio code style reused).
- [ ] Truncated with copy affordance or scroll when long; accessible contrast maintained.

#### FR3: Inspector — SVG Sparkline
**Description:** Inspector renders an enlarged sparkline as inline SVG; no chart component.
**Acceptance Criteria:**
- [ ] Width fits inspector with ≥20 px left/right padding.
- [ ] Y‑axis shows min/max labels in larger font; X‑axis shows tick marks only (no values).
- [ ] No legend; the title above the chart identifies the series.

#### FR4: Remove Port Markers for Computed Nodes
**Description:** Edge endpoint circles are not drawn for edges connected to expr/const/pmf nodes.
**Acceptance Criteria:**
- [ ] Port markers remain for operational nodes; removed for computed nodes.
- [ ] No change to edge routing/arrowheads.

#### FR5: In‑Node bin(t) Metric for Computed Nodes
**Description:** Computed nodes display the selected bin value inside the node body.
**Acceptance Criteria:**
- [ ] Value updates as the global scrubber moves.
- [ ] If series is missing, value is omitted and the node remains visually consistent (no error state in topology).

### Non‑Functional Requirements

#### NFR1: Performance
**Target:** No measurable regressions; tooltip draw + inspector SVG add < 2 ms per interaction on mid‑range hardware.
**Validation:** Manual profiling during hover/inspector open with standard test graphs.

#### NFR2: Accessibility
**Target:** Inspector content is readable, keyboard‑reachable, and provides sufficient contrast.
**Validation:** Manual pass.

---

## Data Contracts

### Graph — Node Semantics (Extension)
- Add optional `expression` field to expression nodes’ semantics.
  - UI: propagate via API contracts → mapper → canvas payload.
  - Non‑breaking; absent for non‑expression kinds.

### State Window — Series for Expr Nodes
- Ensure expression nodes receive series slices in the per‑run window for the current visible range.
- UI may derive the sparkline from existing `windowData` slices used by service nodes.

---

## Implementation Plan

1) Contracts & Mapper
- Extend `GraphNode.semantics` with `expression`; map through API → UI DTOs.
- Include expression nodes in `BuildNodeSparklines` so they have `NodeSparklineData`.

2) Canvas (JS)
- Tooltip sparkline: sample values and draw a tiny polyline under subtitle; color by current basis.
- Port markers: gate `drawPort(...)` to skip edges connected to expr/const/pmf nodes.
- In‑node value: reuse focus label sampling for computed nodes at selected bin.

3) Inspector (Blazor)
- Add an expression code block with Template Studio styles (dark grey on light grey).
- Add lightweight SVG sparkline component with min/max labels and X ticks; no legend.

4) Tests & Docs
- Update render tests for tooltip sparkline and port marker gating.
- Add inspector render checks for expression text and SVG sparkline.
- Document behavior and limitations.

### Progress (Apr 2025)
- Contracts/API/UI mapping now carry `semantics.expression` for expression nodes end-to-end.
- Window sparkline builder includes expression nodes and tracks missing-series warnings for graceful degradation.
- Tooltip renders a basis-colored mini sparkline for expression nodes (with bin highlight and fallback when data is absent).
- Canvas skips lollipop ports on const/expr/pmf edges and displays computed `bin(t)` values inside nodes (when the series is present in the window payload).
- Inspector surfaces expression text in a monospace block and shows an SVG sparkline panel with min/max labels.
- Added unit coverage for the inspector sparkline baseline handling and PMF distribution fallback (FlowTime.UI.Tests).

### Next Focus
- Expand automated checks to cover tooltip sparkline wiring and computed-node focus labels against synthetic data.
- Polish empty-series messaging across tooltip/inspector once real data is exercised.
- Update release notes once verification passes.

---

## Acceptance Notes
- Graceful degradation: if an expr series is missing, tooltip/inspector show a muted “No series for selected range.” and the app logs a warning; topology remains steady.
- No additional network calls required to view expression content or sparklines.

---

## References
- work/epics/completed/time-travel/ui-m3-roadmap.md
- work/milestones/UI‑M‑03.22.md, UI‑M‑03.22.1.md
- docs/development/milestone-documentation-guide.md
- docs/development/milestone-rules-quick-ref.md
- docs/development/TEMPLATE-tracking.md
