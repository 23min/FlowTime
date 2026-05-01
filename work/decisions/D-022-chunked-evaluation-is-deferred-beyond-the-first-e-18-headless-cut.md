---
id: D-022
title: Chunked evaluation is deferred beyond the first E-18 headless cut
status: accepted
---

**Status:** active
**Context:** The headless and optimization story benefits from a pure callable engine quickly, but chunked evaluation for feedback simulation depends on real stateful execution semantics. The current `IStatefulNode` seam is only a stub and is not sufficient to justify bundling chunked/stateful execution into the initial headless foundation.
**Decision:** Split E-18 into layers. The first cut covers the shared runtime parameter foundation, evaluation SDK, and headless CLI / sidecar. Advanced analysis modes (sweep, sensitivity, optimization, fitting) build on that. Chunked evaluation waits for a dedicated streaming/stateful execution seam.
**Consequences:** The sidecar/SDK foundation can ship without solving streaming/stateful execution. Reviews should reject attempts to block the headless foundation on chunked evaluation design.
