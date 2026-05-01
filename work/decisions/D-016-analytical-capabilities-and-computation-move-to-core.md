---
id: D-016
title: Analytical capabilities and computation move to Core
status: accepted
---

**Status:** active
**Context:** Phase 3a review found the same "does this node have queue/service semantics?" decision duplicated 6 ways in `StateQueryService` (snapshot, window, metadata, stationarity warnings, per-class conversion, flow-latency composition) with diverging predicates. The adapter was doing engine work — capability decisions and metric computation are domain knowledge, not projection concerns.
**Decision:** M-057 moves analytical capability resolution and derived metric computation into `FlowTime.Core`. Core provides an `AnalyticalCapabilities` concept resolved once per node, plus a computation surface (capabilities + raw data → derived metrics with finite-value safety). `StateQueryService` becomes a stateless projector for analytical metrics — it consumes Core output and maps to contract DTOs. `flowLatencyMs` graph propagation stays in the adapter (orchestration concern). Non-analytical derived metrics (utilization, throughputRatio, retryTax) stay in the adapter for now.
**Consequences:** Capability parity (explicit vs logicalType-resolved `serviceWithBuffer`) is guaranteed by construction. Metadata honesty, stationarity warning eligibility, and finite-value safety are driven by capabilities, not ad-hoc adapter predicates. p3b/p3c/p3d add to Core's computation surface; the adapter stays thin.
