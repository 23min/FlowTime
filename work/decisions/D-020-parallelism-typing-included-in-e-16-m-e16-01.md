---
id: D-020
title: Parallelism typing included in E-16 m-E16-01
status: accepted
---

**Status:** active
**Context:** `NodeSemantics.Parallelism` is `object?`, parsed at runtime in both Core (`InvariantAnalyzer`) and API (`GetEffectiveCapacity`, `ParseParallelismScalar`). gaps.md deferred this as a 21-file cross-cut. Parallelism represents Kubernetes pod instances (service replicas) and scales effective capacity — this is flow algebra, not presentation.
**Decision:** Include Parallelism typing in M-012 as part of typed semantic references. Replace `object?` with a typed reference (numeric constant or series ref) resolved at compile time. Effective capacity computation (`capacity x parallelism`) moves to Core evaluator in M-015.
**Consequences:** The 21-file cross-cut is acceptable because E-16 is already touching the semantic reference boundary. gaps.md entry for Parallelism can be closed after M-012.
