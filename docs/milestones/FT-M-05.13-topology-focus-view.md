# FT-M-05.13 — Topology Focus View (Provenance Drilldown)

**Status:** 📋 Planned  
**Dependencies:** ✅ FT‑M‑05.12  
**Target:** Add a focused “node drilldown” view that isolates a selected node and its predecessors/outputs with a compact relayout.

---

## Overview

Large topologies make provenance hard to follow. This milestone introduces a **focus view** that filters the graph to the selected node plus its inbound/outbound chain and performs a compact relayout so the remaining nodes aren’t spaced as if the whole graph were present.

This is intended as a complement to metric provenance (FT‑M‑05.12).

---

## Scope

### In Scope ✅
1. **Focus toggle**: “Focus on selected node” with clear on/off state.
2. **Graph filtering**: keep selected node, its predecessors, and its outgoing dependencies (depth configurable).
3. **Compact relayout**: reflow the filtered subgraph to remove large gaps.
4. **Preserve semantics**: all metrics, tooltips, and chips should work in focus view.
5. **Exit behavior**: return to full graph with previous pan/zoom.

### Out of Scope ❌
- ❌ Auto‑clustering or anomaly overlays.
- ❌ Cross‑run comparisons.
- ❌ Layout algorithm replacement (keep existing layout engine if possible).

---

## Requirements

### FR1: Focus Toggle
**Acceptance Criteria**
- [ ] Toggle exists in topology UI (feature bar or inspector).
- [ ] Disabled unless a node is selected.
- [ ] Keyboard shortcut optional (nice‑to‑have).

### FR2: Filtered Subgraph
**Acceptance Criteria**
- [ ] Selected node included.
- [ ] All upstream predecessors included (depth configurable).
- [ ] All downstream successors included (depth configurable).
- [ ] Orphaned nodes removed from focus view.

### FR3: Compact Relayout
**Acceptance Criteria**
- [ ] Filtered nodes are re‑laid out to minimize whitespace.
- [ ] Edge routing remains readable.
- [ ] Re‑layout does not mutate the original graph state.

### FR4: State Preservation
**Acceptance Criteria**
- [ ] Pan/zoom preserved when returning to full view.
- [ ] Focus view maintains its own pan/zoom state.

---

## Implementation Plan

### Phase 1: UI Toggle + Filtering
1. Add focus toggle to topology UI.
2. Implement node/edge filtering by traversal.

### Phase 2: Relayout
1. Reuse existing layout engine with filtered graph.
2. Cache layout per focus target for responsiveness.

### Phase 3: Validation
1. Verify chips/tooltips/inspectors in focus view.
2. Manual tests on large templates (transportation, supply chain).

---

## Test Plan

### UI Tests
- `Topology_FocusView_TogglesOnSelectedNode`
- `Topology_FocusView_PreservesFullGraphState`
- `Topology_FocusView_RelayoutsFilteredGraph`

---

## Success Criteria

- [ ] Focus view enables provenance tracing without manual panning.
- [ ] Returning to full graph restores original view.
- [ ] No regression in metrics or tooltip behavior.

