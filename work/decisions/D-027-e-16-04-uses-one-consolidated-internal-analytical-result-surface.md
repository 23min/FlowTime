---
id: D-027
title: E-16-04 uses one consolidated internal analytical result surface
status: accepted
---

**Status:** active
**Context:** M-015 must move emitted-series truth, effective capacity, utilization, and graph-level flow latency into Core. An open design question was whether Core should return many narrow result types or one consolidated analytical result.
**Decision:** Use one consolidated internal Core analytical result surface, with explicit nested sections rather than a flat bag of fields. The result owns snapshot/window/by-class analytical outputs, emitted-series truth, effective-capacity facts, and graph-level flow-latency outputs so adapters project one source of truth instead of recomposing partial answers.
**Consequences:** This does not require one public contract type, and it does not prevent later milestone-specific refinements. The important constraint is that adapters and query surfaces consume one coherent Core result model instead of rebuilding analytical policy from multiple partial calculators.
