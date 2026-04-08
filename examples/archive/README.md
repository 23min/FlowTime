# Archived Examples

This directory holds historical example YAML files that are no longer on the active `examples/` surface but are preserved for reference.

## Contents

### Schema-migration compatibility fixtures (archived in m-E19-03)

Three fixtures originally kept under `examples/` to exercise the engine's behavior across the schema transition from the deprecated `binMinutes` grid shape to the current `binSize` + `binUnit` schema. They are not user-facing examples of current modeling guidance.

- `test-old-schema.yaml` — uses the deprecated `binMinutes: 60` grid shape. The current `ModelValidator` rejects this shape at parse time; the file is preserved as a fixture for the rejection gate and as a historical reference for the migration.
- `test-new-schema.yaml` — uses the current `binSize: 1, binUnit: hours` grid shape. Functionally equivalent to the canonical `hello/model.yaml` hello-world example.
- `test-no-schema.yaml` — converted from an implicit/unschematized YAML draft to the current strict node-based schema.

**Decision record:** [m-E19-01 supported-surfaces matrix](../../docs/architecture/supported-surfaces.md) (Examples row, decision `archive`), executed by [m-E19-03 Schema, Template & Example Retirement](../../work/epics/E-19-surface-alignment-and-compatibility-cleanup/m-E19-03-schema-template-example-retirement.md).

**Current examples:** see the parent `examples/` directory. For authoritative current schema documentation, see [docs/schemas/model.schema.md](../../docs/schemas/model.schema.md).
