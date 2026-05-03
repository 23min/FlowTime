---
id: G-004
title: dag-map Layout Quality (Svelte UI)
status: open
---

### Wiggly trunk in dag-map bezier layout
The main trunk path in dag-map's `layoutMetro` wobbles vertically when branches push the Y-positions around. Visible on the FlowTime "Transportation Network" model (12 nodes). The trunk should be a straight horizontal line with branches diverging above/below.

### No class differentiation from FlowTime API
The FlowTime `/v1/runs/{id}/graph` endpoint returns node `kind` (service, queue, dlq) but not flow classes. dag-map's layout engine uses `cls` to assign route colors and lane spread. Without meaningful classes, all nodes land on one route.

### Possible fixes
- dag-map: improve trunk stability in layoutMetro (prioritize trunk straightness)
- FlowTime API: expose class-to-node mapping so dag-map can assign routes
- dag-map: add heatmap mode so coloring comes from metrics, not static classes

---
