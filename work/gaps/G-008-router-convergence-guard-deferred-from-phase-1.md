---
id: G-008
title: Router Convergence Guard (Deferred from Phase 1)
status: open
---

### What was planned
Add a max-iteration limit and convergence detection to `RouterAwareGraphEvaluator`, which currently performs a single re-evaluation pass.

### Why deferred
The single-pass design is correct for static router weights. A convergence guard is only needed if dynamic/expression-based router weights are introduced (e.g., "route to least-utilized path"). No current or planned models require dynamic routing. FlowTime operates on aggregated time series per bin — routing fractions are either observed (telemetry) or assumed (static weights).

### When to revisit
When dynamic routing is designed as a feature. At that point, add iteration with convergence detection and a max-iteration safety limit.

### Reference
- `src/FlowTime.Core/Routing/RouterAwareGraphEvaluator.cs`
- Phase 1 spec: `work/epics/E-10-engine-correctness-and-analytics/m-ec-p1-engineering-foundation.md`

---
