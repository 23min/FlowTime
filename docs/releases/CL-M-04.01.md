# Release CL-M-04.01 — Class Schema & Template Enablement

**Release Date:** 2025-11-24  
**Type:** Milestone (Schema/DTO/CLI)  
**Version:** 0.6.x (classes schema baseline)

## Overview

CL-M-04.01 introduces class-aware modeling across schemas, canonical outputs, and CLI validation. Template authors can declare `classes`, bind arrivals to `classId`, and see those classes emitted in canonical artifacts (`model.yaml`, `manifest.json`, `run.json`). DTOs and Sim plumbing now preserve class metadata with deterministic ordering; docs and examples illustrate single- and multi-class models.

## Key Changes

- **Schema:** `docs/schemas/model.schema.yaml` accepts `classes[]` and `traffic.arrivals[].classId`; conditional requirement when classes are present.
- **Validation:** New `ModelSchemaValidator` + TemplateValidator updates enforce declared classes, unknown-class errors, and wildcard defaults.
- **DTOs & Artifacts:** Model/traffic/classes added to contracts and run/manifest outputs; manifests and run metadata include class inventory.
- **CLI:** `flow-sim generate` surfaces declared classes; validation errors on undeclared/missing class references.
- **Docs & Examples:** Class guidance added to authoring/concepts/PMF docs; new sample `examples/class-enabled.yaml`; telemetry guide notes class metadata forward reference.
- **Follow-up Spec:** `docs/milestones/CL-M-04.01.01.md` captures template schema alignment and schema-backed Sim validation as next work.

## Test Coverage

- Full suite: `dotnet test --nologo` (all passing; perf tests skipped by design).
- Scoped Sim tests: class-aware canonical writer/manifest (`CanonicalModelWriterTests`), class validation (`TemplateClassValidationTests`).
- Schema tests: `TemplateSchemaTests` for class declarations, unknown references, wildcard defaults.
- Known skips: PMF performance tests (`M2PerformanceTests`) deferred until epic 4 perf tuning.

## Breaking Changes

None. Classes are additive; templates without `classes` still run with implicit wildcard `*`.

## Known Issues

- PMF performance tests are skipped pending perf tuning (devcontainer variability).

## What’s Next

- Execute CL-M-04.01.01 to align template schema docs/JSON with class-aware templates and wire schema validation into Sim CLI.
- CL-M-04.02+: surface classes in metrics (`byClass`), UI class selectors, and telemetry contracts.
