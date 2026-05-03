---
id: G-003
title: Dependency Constraint Enforcement (Deferred M-10.03)
status: open
---

### What was planned
M-065 scoped MCP-side pattern enforcement: a dependency pattern selector routing user intent to Option A or Option B, rejecting unsupported patterns (feedback loops, retries), and promoting engine warnings to hard errors during MCP model generation.

### Why deferred
1. The engine review (2026-03) found that `ConstraintAllocator` has **zero callers** in the evaluation pipeline — constraints are declared in models but silently ignored at runtime. MCP-side enforcement alone doesn't fix this.
2. The sequenced plan recommends wiring `ConstraintAllocator` into `Graph.Evaluate()` (Phase 3.5) before MCP enforcement adds value.
3. Near-term priority is correctness bugs and analytical primitives (Phases 0–3 of the sequenced plan).

### When to revisit
After Phase 3.5 (runtime constraint enforcement) is complete. At that point, M-065 should be re-scoped to include both runtime enforcement and MCP guardrails.

### Reference
- Spec: `work/epics/E-12-dependency-constraints/M-10.03-dependency-mcp-pattern-enforcement.md`
- Review: `docs/architecture/reviews/review-sequenced-plan-2026-03.md` (Phase 3.5, Phase 4.1)

---
