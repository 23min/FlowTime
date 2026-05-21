---
id: D-0019
title: Analytical descriptor absorbs AnalyticalCapabilities
status: accepted
---

**Status:** active
**Context:** M-0057 introduced `AnalyticalCapabilities` as a Core bridge resolved from `kind + logicalType` strings. E-0016 introduces a compiled analytical descriptor produced by the compiler from typed semantic references. The question is whether they coexist or the descriptor replaces capabilities.
**Decision:** The descriptor absorbs `AnalyticalCapabilities`. Capability flags become compiled descriptor fields. Computation methods (`ComputeBin`, `ComputeWindow`, etc.) move to the Core analytical evaluator (M-0015). `AnalyticalCapabilities.Resolve(kind, logicalType)` is deleted — string-based resolution is exactly what E-0016 eliminates. `EffectiveKind` is removed as a bridge concept.
**Consequences:** M-0014 deletes `AnalyticalCapabilities`. M-0015 builds the evaluator from its computation methods. No coexistence period.
