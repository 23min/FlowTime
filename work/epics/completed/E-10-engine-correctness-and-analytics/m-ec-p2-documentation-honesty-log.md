# Tracking: Phase 2 — Documentation Honesty

**Milestone:** m-ec-p2
**Epic:** Engine Correctness & Analytical Primitives
**Branch:** `milestone/m-ec-p2`
**Started:** 2026-04-01

## Acceptance Criteria

- [x] AC-1: Expression language design doc updated (11 functions)
- [x] AC-2: Expression extensions roadmap updated (6 shipped)
- [x] AC-3: Constraint claims downgraded (3 docs)
- [x] AC-4: Catalog architecture archived
- [x] AC-5: JSON Schema meta-versions standardized

## Progress Log

### 2026-04-01
- Milestone spec approved, branch created, tracking doc initialized
- AC-1: Added Function Reference section to expression-language-design.md with all 11 functions (signatures, descriptions, NaN behavior), updated decision log and future evolution
- AC-2: Updated expression-extensions-roadmap.md — section 2.5 moved from "proposed" to "shipped" for MOD/FLOOR/CEIL/ROUND/STEP/PULSE, remaining candidates (IF, HOLD, SMOOTH, etc.) kept as aspirational
- AC-3: Downgraded constraint claims in engine-capabilities.md (2 locations), flowtime-charter.md, and flowtime-engine-charter.md — all now say "foundations laid, enforcement pending"
- AC-4: Moved catalog-architecture.md to docs/archive/
- AC-5: Standardized all 7 schema files to https://json-schema.org/draft-07/schema# (template.schema.json was 2020-12, 5 files were http://)
- Build green
