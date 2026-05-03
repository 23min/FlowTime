---
id: G-009
title: Parallelism `object?` Typing (Deferred from Phase 1)
status: open
---

### What was planned
Replace `NodeSemantics.Parallelism` (`object?`) with a proper discriminated union type. The loose typing exists because YAML deserialization can produce a string (file URI or node reference), a numeric scalar, or a double array.

### Why deferred
The change touches 21 files across Core, Contracts, Sim, API, and UI — a cross-cutting refactor with high risk for a foundation milestone. CUE (https://cuelang.org/) was noted as a potential future approach for model schema validation with native union type support.

### When to revisit
Addressed by E-16 M-012 (Compiled Semantic References). See D-020. Parallelism becomes a typed reference resolved at compile time. Close this gap after M-012 completes.

### Reference
- `src/FlowTime.Core/Models/NodeSemantics.cs` (line 21)
- `src/FlowTime.Core/DataSources/SemanticLoader.cs` (ResolveParallelism method)
- Phase 1 spec: `work/epics/E-10-engine-correctness-and-analytics/m-ec-p1-engineering-foundation.md`

---
