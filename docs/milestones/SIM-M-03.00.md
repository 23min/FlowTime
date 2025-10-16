# SIM-M-03.00 — Schema Foundations & Shared Validation

**Status:** ✅ Complete (WS1–WS4 shipped; WS5 follow-ups ongoing)  
**Dependencies:** M-03.00 (Engine Schema & Fixtures), M-03.00.01 (Shared Expression Validation Library)  
**Target:** Upgrade FlowTime.Sim templates, generation pipeline, and validation to emit the KISS time-travel schema (window, topology, semantics, provenance) while consuming the shared FlowTime.Expressions library for deterministic validation.

---

## Overview

SIM-M-03.00 delivered the foundational refactors needed for time-travel milestones: templates now capture window/topology semantics, provenance is preserved, telemetry-friendly nodes are supported, and validation aligns with Engine behaviour via the shared FlowTime.Expressions library. These changes unlock downstream workstreams (service/CLI updates, fixture upgrades) that build on the compliant schema emitted by FlowTime.Sim.

### Strategic Context
- **Motivation:** Time-travel APIs (Engine M-03.01+) require consistent schema/validation across Engine and Sim. Without this upgrade, Sim-generated models cannot participate in `/state` workflows.
- **Impact:** Templates become authoritative artifacts with complete topology semantics, enabling deterministic runs and accurate UI colouring. Validation parity prevents drift between surfaces.
- **Dependencies:** Engine schema work (M-03.00) and shared expression library (M-03.00.01) provide the contract and validation primitives consumed here.

---

## Scope

### In Scope ✅
1. Extend FlowTime.Sim template model to include `window`, `topology`, node semantics/kinds, telemetry `source`, and `initial` values.
2. Preserve template metadata and provenance (`TemplateMetadata.version`, mode) when generating Engine YAML.
3. Integrate the shared FlowTime.Expressions library into Sim parsing/validation, replacing bespoke checks.
4. Implement topology/semantic validators (node kinds, semantics presence, edge validation) with simulation/telemetry mode awareness.
5. Update existing unit tests to cover new schema elements and validation outcomes.

### Out of Scope ❌
- Service/CLI endpoint updates (tracked under SIM-M-03.01+ workstreams).
- Template conversions to the new schema (WS4), handled once the foundation is complete.
- Telemetry loader tooling or fixture packaging.

### Future Work
- SIM-M-03.01: Service & CLI enhancements that surface the new schema and provenance.
- SIM-M-03.02: Template migrations, curated fixtures, and integration tests using Engine APIs.

---

## Requirements

### Functional Requirements

#### FR1: Schema Support
- Template classes capture `window`, `topology`, `TopologyNode`, `TopologyEdge`, `NodeSemantics`, `InitialCondition`, and `UIHint`.
- `TemplateNode` supports `source`, `values`, `initial`, and `kind`.
- Generation pipeline emits Engine-compatible YAML without stripping metadata/provenance.

#### FR2: Shared Validation Integration
- FlowTime.Sim references `FlowTime.Expressions` and uses `ExpressionParser`/`ExpressionSemanticValidator`.
- Expressions referencing unknown nodes/functions fail with deterministic errors.
- Self-referential SHIFT without `initial` fails validation before generation.

#### FR3: Topology & Mode Validation
- Validators ensure node IDs/kinds, semantics mappings, and edges match contract.
- `TemplateMode` (simulation/telemetry) drives validation severity (simulation = fail fast, telemetry may warn for missing telemetry sources).
- Validation outputs actionable error messages (aligned with Engine phrasing where applicable).

### Non-Functional Requirements

#### NFR1: Backward Compatibility Strategy
- Provide documentation highlighting schema changes and migration guidance for existing templates.
- Legacy templates should fail validation with clear errors (no silent fallback to M-02 behaviour).

#### NFR2: Test Coverage & Tooling
- Unit tests cover template serialization, validation rules, and expression integration.
- Smoke test confirms Engine can parse a Sim-generated model (optional integration test stub for follow-up milestone).

---

## Technical Design

- **Template Model:** Introduce new classes mirroring Engine contracts (`TemplateWindow`, `TemplateTopology`, etc.) under `FlowTime.Sim.Core/Templates`.
- **Generation Pipeline:** Refactor `TemplateService` to populate new sections, preserve metadata, and embed provenance (mode/version hashes).
- **Validation:** Implement validators that use shared expression results plus topology semantics checks; expose mode-aware messages.
- **Telemetry Sources:** Handle `file://` URIs for telemetry-bound const nodes while retaining inline `values` for synthetic use.
- **Testing:** Extend `tests/FlowTime.Sim.Tests/NodeBased/*` and validator suites; add FlowTime.Expressions smoke tests (already wired) plus expression-specific assertions.

---

## Implementation Plan

### Phase 1: Template & Schema Model
1. Introduce new template classes and update YAML parsing/serialization.
2. Add `TemplateNode.kind`, `source`, and `initial`; ensure parameter substitution supports nested paths.
3. Update provenance to include `mode`, `templateVersion`, and embed by default.

### Phase 2: Validation & Shared Expressions
1. Reference `FlowTime.Expressions` from Sim projects; replace ad-hoc expression checks.
2. Implement topology/semantics validator (node kinds, required semantics, edges, initial conditions).
3. Add mode-aware validation pipeline (simulation vs telemetry) with unit tests.
4. Add a minimal integration smoke test (generate model → Engine parse) to confirm schema alignment.

### Phase 3: Testing & Documentation
1. Extend unit tests for template serialization, validation errors, and expression integration.
2. Update Sim architecture docs (`sim-schema-and-validation.md`, `sim-implementation-plan.md`) with new behaviour and migration notes.
3. Provide a migration guide snippet or checklist for authors converting legacy templates.

---

## Test Plan

### TDD Approach
1. Write failing unit tests for new template classes (window/topology serialization) and expression validation.
2. Implement schema/validation changes to satisfy tests.
3. Add integration smoke test (optional) to ensure Engine parsing succeeds.
4. Re-run full Sim and Engine test suites (`dotnet test FlowTime.sln`) to confirm no regressions.

### Test Cases
- Template serialization includes window/topology when authoring YAML.
- Validation errors for missing semantics, invalid edges, self-SHIFT without initial.
- Expression validator catches unknown identifiers/functions using shared library.
- Mode toggle scenario: telemetry mode issues warnings for missing telemetry `source`.
- Smoke test: generated model parses via Engine `ModelParser.ParseMetadata`.

---

## Follow-Up Work

- WS5 (ongoing): Define telemetry-mode warning policy, scope synthetic Gold automation, and broaden integration/contract regression coverage.
- Testing: Until the performance suite stabilises, prefer `dotnet test FlowTime.Tests --filter Category!=Benchmark` instead of the full solution run; re-run `dotnet test FlowTime.sln` when performance flake mitigation lands.

---

## Success Criteria
- [x] Template model supports window/topology semantics and preserves metadata/provenance when generating YAML.
- [x] FlowTime.Sim integrates `FlowTime.Expressions` with unit tests covering validation outcomes.
- [x] Topology/semantic validators enforce KISS schema rules with mode-aware messaging.
- [x] Sim and Engine smoke tests confirm generated models parse successfully.
- [x] Documentation (`sim-schema-and-validation.md`, `sim-implementation-plan.md`) updated to guide authors and contributors.

---

## File Impact Summary

### Files to Modify
- `src/FlowTime.Sim.Core/Templates/**`
- `src/FlowTime.Sim.Core/Services/TemplateService.cs`
- `src/FlowTime.Sim.Core/Services/ProvenanceService.cs` / embedder
- `src/FlowTime.Sim.Core/Validation/**`
- `src/FlowTime.Sim.Core/FlowTime.Sim.Core.csproj` (references)
- `src/FlowTime.Sim.Cli/Program.cs`, `src/FlowTime.Sim.Service/**` (if required for mode defaults)
- `tests/FlowTime.Sim.Tests/**`
- Documentation under `docs/architecture/time-travel/sim/`

### Files to Create
- New template/validator classes (e.g., `TemplateTopology.cs`, `TopologyValidator.cs`)
- Migration guide snippet under `docs/architecture/time-travel/sim/`
- Optional integration smoke test fixture.

---

## References
- `docs/architecture/time-travel/time-travel-planning-roadmap.md` (Sim WS1/WS2 context)
- `docs/architecture/time-travel/sim/sim-implementation-plan.md`
- `sim-schema-and-validation.md`
- `sim-time-travel-readiness-audit.md`
