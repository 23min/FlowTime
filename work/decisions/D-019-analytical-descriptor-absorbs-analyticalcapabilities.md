---
id: D-019
title: Analytical descriptor absorbs AnalyticalCapabilities
status: accepted
---

**Status:** active
**Context:** M-057 introduced `AnalyticalCapabilities` as a Core bridge resolved from `kind + logicalType` strings. E-16 introduces a compiled analytical descriptor produced by the compiler from typed semantic references. The question is whether they coexist or the descriptor replaces capabilities.
**Decision:** The descriptor absorbs `AnalyticalCapabilities`. Capability flags become compiled descriptor fields. Computation methods (`ComputeBin`, `ComputeWindow`, etc.) move to the Core analytical evaluator (M-015). `AnalyticalCapabilities.Resolve(kind, logicalType)` is deleted — string-based resolution is exactly what E-16 eliminates. `EffectiveKind` is removed as a bridge concept.
**Consequences:** M-014 deletes `AnalyticalCapabilities`. M-015 builds the evaluator from its computation methods. No coexistence period.
