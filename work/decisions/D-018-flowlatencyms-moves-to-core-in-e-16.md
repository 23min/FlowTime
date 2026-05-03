---
id: D-018
title: flowLatencyMs moves to Core in E-16
status: accepted
---

**Status:** active (supersedes D-016 for flowLatencyMs specifically)
**Context:** D-016 kept flowLatencyMs graph propagation in the adapter as an "orchestration concern" during M-057 bridge work. E-16 milestone review found this is graph-level queueing theory: expected sojourn time through a network, computed as topological accumulation of per-node cycle times weighted by edge flow volumes. The Core IS a graph engine — graph-level analytical computation belongs there.
**Decision:** flowLatencyMs computation moves to Core in M-015. The algorithm (`base = cycleTimeMs[node] + weightedAvg(flowLatencyMs[predecessors], edgeFlowVolume)`) becomes a pure Core evaluator function. The adapter passes topology + series inputs, Core returns results.
**Consequences:** M-015 scope includes flowLatencyMs migration. D-016 remains active for its other points (non-analytical derived metrics like utilization/throughputRatio/retryTax staying in the adapter was a bridge — E-16 now moves utilization to Core too since effective capacity is flow algebra).
