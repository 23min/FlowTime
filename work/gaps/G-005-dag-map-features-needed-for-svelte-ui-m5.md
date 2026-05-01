---
id: G-005
title: dag-map Features Needed for Svelte UI M5+
status: open
---

- ~~**Heatmap mode**: per-node/edge metric coloring~~ **Done** (dag-map `metrics`/`edgeMetrics`/`colorScales`)
- **Click/tap events**: callback with node ID (M5 blocker for inspector). `data-id` attributes exist; need event delegation in Svelte wrapper or library-level callback.
- **Hover tooltips**: on stations (M5 blocker for inspector)
- **Selected node highlighting**: visual state (M5 blocker for inspector)
- **Node shape differentiation**: custom shapes per node kind (service=rect, queue=diamond, dlq=triangle). Possible via `renderNode` callback.

See dag-map ROADMAP.md “Planned” sections.
