---
id: G-022
title: Heatmap view — deferred enhancements (m-E21-06 Q&A, 2026-04-23)
status: open
---

### Why this is a gap

The M-043 Heatmap View design Q&A (14 questions) surfaced several enhancements that are real analytical value but are not required for the first shipping heatmap. Default behavior for the milestone favors paradigm coherence (shared normalization with topology, topological default sort, etc.); these items are alternative modes and secondary analytical tools.

### Deferred items

- **Fixed per-metric color ranges** (Q3-D). Utilization anchored to `[0, 1]`, bounded metrics pinned to their natural domain, etc. Stable across runs (e.g. utilization of 0.5 always looks the same). Requires metric-registry enrichment (per-metric `domain` metadata) and doesn't work for unbounded metrics without convention. Default normalization is shared full-window with 99th-percentile clipping.
- **Per-row (per-node) color normalization toggle** (Q3-C). Each row normalizes over its own min/max, surfacing "temporal pattern within this node" at the cost of cross-node comparability. Useful secondary mode for "what's the shape of this node's pattern?" — defer until asked.
- **Current-bin value sort mode** (Q7 extra). Sorts rows by value at the scrubber's current bin; re-sorts when the scrubber moves. Cute but volatile; unclear real analytical use.
- **Trend / slope sort mode** (Q7 extra). Rank rows by slope of their time series to answer "which nodes are getting worse over time?" Genuinely analytical but adds statistical complexity; defer.
- **View-registry graduation** (Q13). M-043 ships a typed `<ViewSwitcher>` with views listed inline on the topology page and shared state in a store. When a third layered view lands (decomposition / comparison / flow-balance — currently out-of-scope of E-21) with real asymmetry from topology/heatmap, graduate to a manifest-based registry + `ViewContext` pattern. Premature to build now.

### Immediate implications

- These are UX-enrichment items, not correctness gaps. Shipping M-043 without them is honest: the default behavior is the right default for a first heatmap.
- If future layered-view milestones surface asymmetry that the inline `<ViewSwitcher>` handles awkwardly, graduate to a registry pattern then — not speculatively.

### Reference

- E-21 M-043 Q&A conversation, 2026-04-23 (in-session, not archived).
- Source spec: `work/epics/unplanned/ui-analytical-views/spec.md` V1 Heatmap View.
- E-21 epic spec: `work/epics/completed/E-21-svelte-workbench-and-analysis/spec.md` M-043 row.

---
