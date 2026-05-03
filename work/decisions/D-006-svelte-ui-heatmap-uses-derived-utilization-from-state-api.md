---
id: D-006
title: Svelte UI heatmap uses derived.utilization from state API
status: accepted
---

**Status:** active
**Context:** The FlowTime state API returns metrics at multiple levels: `metrics.*` (raw), `derived.*` (computed), `byClass.*` (per-class). Needed to pick the right field for heatmap coloring.
**Decision:** Use `derived.utilization` as primary heatmap metric, `derived.throughputRatio` as fallback. Other focus metrics (SLA, error rate, queue depth) to be added via a metric selector chip.
**Consequences:** Heatmap works end-to-end for utilization. Need to add metric selector for other derived fields.
