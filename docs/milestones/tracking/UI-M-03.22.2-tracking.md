# UI‑M‑03.22.2 Tracking — Topology Expr Tooltip + Inspector Sparkline

**Branch:** `feature/ui-m-0322-topology-canvas`  
**Owner:** UI  
**Status:** 🔄 In Progress

---

## TODOs

- [x] Contracts: add `semantics.expression` for expr nodes; pass through API → UI mapper.
- [x] Window data: include expr nodes in `BuildNodeSparklines`.
- [x] Canvas tooltip: draw mini sparkline under subtitle for expr nodes.
- [x] Canvas ports: skip `drawPort` for edges touching expr/const/pmf nodes.
- [x] In‑node metric: sample bin(t) for computed nodes and render in node body.
- [x] Inspector: add expression code block (mono, dark‑grey on very light grey).
- [x] Inspector: add SVG sparkline with min/max Y labels; X axis tick marks only; ≥20px side padding.
- [x] Tests: update render tests; add inspector sparkline + expression text checks.
- [x] Docs: milestone notes updated per guidelines.

## Validation

- [ ] Hover expr node → tooltip shows mini sparkline; no legend; positions unchanged.
- [ ] Inspector for expr node shows expression text (mono, styled) and enlarged SVG sparkline.
- [ ] Port markers absent on computed nodes; present on operational nodes.
- [ ] Computed nodes show bin(t) inside node; updates on scrub; no perf regressions (series must be present in window payload).

## Notes
- Degrade gracefully if expr series missing (muted message + warn log). No hard error.
- No new network calls for expression content or sparklines.

## Progress Log
- 2025-04 — Mapped expression semantics through contracts, added tooltip sparkline plus computed-node bin labels, refreshed inspector UI/SVG implementation, and updated milestone docs.
- 2025-11 — Added inspector sparkline + PMF coverage (BUnit) and refreshed docs/tracker notes.
