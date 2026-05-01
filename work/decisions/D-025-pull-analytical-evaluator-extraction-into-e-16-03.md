---
id: D-025
title: Pull analytical evaluator extraction into E-16-03
status: accepted
---

**Status:** active
**Context:** Deleting `AnalyticalCapabilities` in M-014 left its computation methods (`ComputeBin`, `ComputeWindow`, metadata gates, stationarity checks) without a truthful owner. Waiting until M-015 would have required either keeping the bridge alive or renaming it in place, which would violate the no-coexistence rule.
**Decision:** Extract the descriptor-backed `RuntimeAnalyticalEvaluator` in M-014 as the minimal owner for the surviving analytical computation surface. M-015 still owns broader evaluator consolidation (flow-latency migration, emitted-series truth, warning fact cleanup), but not the initial extraction itself.
**Consequences:** `AnalyticalCapabilities` can be deleted cleanly in M-014. The runtime descriptor now carries typed analytical identity and the evaluator consumes descriptor facts directly. M-015 builds on an existing evaluator instead of performing the first bridge cut.
