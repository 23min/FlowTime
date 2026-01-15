# Release CL-M-04.01.01 — Template Schema Alignment

**Release Date:** 2025-11-24  
**Type:** Follow-up (Schema/Validation)  
**Version:** No version bump (inherits 0.6.x lineage)

## Overview

CL-M-04.01.01 aligns the template schema/docs with the class-aware template model and adds schema-backed validation to the Sim pipeline. Templates with `classes` and `traffic.arrivals.classId` now validate against the authoritative JSON schema before semantic checks, reducing drift between documentation and runtime behavior.

## Key Changes

- **Template Schema:** `docs/schemas/template.schema.json` updated to include `classes`, `traffic.arrivals.classId`, required `window`, backlog nodes, and class-aware examples; `template-schema.md` refreshed accordingly.
- **Schema Validation:** New `TemplateSchemaValidator` runs prior to semantic validation in `TemplateParser`, with legacy fallback when `schemaVersion` is missing.
- **Tests:** Generator schema tests extended for class-aware arrivals; Sim canonical/validation suites unchanged and still green.

## Test Coverage

- `dotnet test --nologo` (passing; perf tests remain skipped by design).
- Schema validation tests in `FlowTime.Generator.Tests` updated for arrivals/classId.
- Sim scoped class tests (`CanonicalModelWriterTests`, `TemplateClassValidationTests`) passing.

## Breaking Changes

None. Legacy templates without `schemaVersion` bypass schema validation; class-aware rules are additive.

## Known Issues

- PMF perf tests remain skipped pending epic 4 perf tuning.

## What’s Next

- Drive CL-M-04.02+ (byClass metrics/UI) and the remaining template schema/CLI polish as needed.
