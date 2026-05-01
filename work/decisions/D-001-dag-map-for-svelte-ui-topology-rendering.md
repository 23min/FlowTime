---
id: D-001
title: dag-map for Svelte UI topology rendering
status: accepted
---

**Status:** active
**Context:** M3 originally planned to wrap topologyCanvas.js (10K LOC from Blazor UI). Initial integration worked but was rough — canvas sizing issues, requires overlay payload before draw, and the approach duplicates the Blazor rendering code.
**Decision:** Use dag-map library instead. dag-map is our own library with a general-purpose flow visualization roadmap. Extend dag-map with features needed by FlowTime (heatmap mode, click events, hover) rather than wrapping the Blazor-specific canvas JS.
**Consequences:** M4 (timeline) now depends on dag-map heatmap mode being implemented first. dag-map features must remain general-purpose, not FlowTime-specific. topologyCanvas.js stays in Blazor UI only.
