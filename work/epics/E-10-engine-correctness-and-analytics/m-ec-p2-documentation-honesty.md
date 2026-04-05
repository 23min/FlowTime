# Milestone: Phase 2 — Documentation Honesty

**ID:** m-ec-p2
**Epic:** Engine Correctness & Analytical Primitives
**Status:** approved

## Goal

Align documentation with code reality: document all shipped expression functions, correct overclaimed constraint capabilities, archive obsolete design docs, and standardize schema versions.

## Context

The March 2026 engine deep review found documentation drift: 6 undocumented expression functions, overstated constraint claims ("shipped" when the allocator has zero callers), and inconsistent schema meta-versions. Phase 0 and Phase 1 fixed the code; Phase 2 fixes the docs.

## Acceptance Criteria

1. **AC-1: Expression language design doc updated.** `docs/architecture/expression-language-design.md` documents all 11 implemented functions: SHIFT, CONV, MIN, MAX, CLAMP, MOD, FLOOR, CEIL, ROUND, STEP, PULSE. Each function has signature, description, and example. The 6 newly documented functions (MOD, FLOOR, CEIL, ROUND, STEP, PULSE) match the implementations in `ExprNode.cs`.

2. **AC-2: Expression extensions roadmap updated.** `docs/architecture/expression-extensions-roadmap.md` moves MOD, FLOOR, CEIL, ROUND, STEP, PULSE from "candidate/aspirational" to "shipped." Status language reflects reality.

3. **AC-3: Constraint claims downgraded.** Three docs corrected:
   - `docs/reference/engine-capabilities.md` — constraint description notes that `ConstraintAllocator` exists but is not called during evaluation; constraints are declared but not enforced at runtime.
   - `docs/flowtime-charter.md` — "Dependency constraints" under shipped foundations qualified with "foundations laid, enforcement pending."
   - `docs/flowtime-engine-charter.md` — same qualification.

4. **AC-4: Catalog architecture archived.** `docs/architecture/catalog-architecture.md` moved to `docs/archive/catalog-architecture.md`. No new doc created — the catalog concept is dormant.

5. **AC-5: JSON Schema meta-versions standardized.** All schema files in `docs/schemas/` use `https://json-schema.org/draft-07/schema#`. The `template.schema.json` (currently draft/2020-12) is changed to draft-07. Any `http://` URIs are updated to `https://`.

## Technical Notes

- No code changes — all documentation and schema metadata only.
- Charters are actively referenced from many docs (modeling.md, whitepaper, concepts, milestone guides). They must be corrected in place, not archived.
- The `model.schema.yaml` already exists (927 lines) — no action needed (originally scoped but already complete).
- "Time-travel" scope clarification dropped — `engine-capabilities.md` already describes it correctly as artifact querying. The branding is fine.

## Out of Scope

- Rewriting charters from scratch — just correct the overclaimed sections.
- Creating new architecture docs.
- Code changes to ConstraintAllocator (that's Phase 3).
- Model schema creation (already exists).

## Dependencies

- Phase 0 complete ✅
- Phase 1 complete ✅
