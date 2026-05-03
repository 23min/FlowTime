---
id: D-015
title: cycleTimeMs coexists with flowLatencyMs
status: accepted
---

**Status:** active
**Context:** Phase 3a introduced `cycleTimeMs` (per-node: queue + service time) alongside the existing `flowLatencyMs` (cumulative: graph-level propagation from entry to node). Needed to decide whether the new metric replaces or coexists with the old.
**Decision:** Coexist. `cycleTimeMs` answers "how long does work spend at this node?" while `flowLatencyMs` answers "how long does it take for work to get here from entry?" `flowLatencyMs` now uses `CycleTimeComputer` for its per-node base value, but the graph propagation stays in `StateQueryService`.
**Consequences:** Both fields appear in `NodeDerivedMetrics`. `cycleTimeMs` decomposes into `queueTimeMs` + `serviceTimeMs` with `flowEfficiency` as a ratio. `flowLatencyMs` remains the cumulative metric for end-to-end analysis.
