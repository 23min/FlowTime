# UI‑M‑03.22.2 Tracking — Topology Expr Tooltip + Inspector Sparkline

**Branch:** `feature/ui-m-0322-topology-canvas`  
**Owner:** UI  
**Status:** 📋 Planned

---

## TODOs

- [ ] Contracts: add `semantics.expression` for expr nodes; pass through API → UI mapper.
- [ ] Window data: include expr nodes in `BuildNodeSparklines`.
- [ ] Canvas tooltip: draw mini sparkline under subtitle for expr nodes.
- [ ] Canvas ports: skip `drawPort` for edges touching expr/const/pmf nodes.
- [ ] In‑node metric: sample bin(t) for computed nodes and render in node body.
- [ ] Inspector: add expression code block (mono, dark‑grey on very light grey).
- [ ] Inspector: add SVG sparkline with min/max Y labels; X axis tick marks only; ≥20px side padding.
- [ ] Tests: update render tests; add inspector sparkline + expression text checks.
- [ ] Docs: milestone notes updated per guidelines.

## Validation

- [ ] Hover expr node → tooltip shows mini sparkline; no legend; positions unchanged.
- [ ] Inspector for expr node shows expression text (mono, styled) and enlarged SVG sparkline.
- [ ] Port markers absent on computed nodes; present on operational nodes.
- [ ] Computed nodes show bin(t) inside node; updates on scrub; no perf regressions.

## Notes
- Degrade gracefully if expr series missing (muted message + warn log). No hard error.
- No new network calls for expression content or sparklines.
