# FlowTime.Sim Time-Travel Implementation Plan

**Status:** Draft**  
**Last Updated:** 2025-10-11  
**Scope:** FlowTime.Sim Core, Service, CLI, Templates, Tests

This plan decomposes the work required to align FlowTime.Sim with the time-travel roadmap. It complements the end-to-end milestone sequencing in `time-travel-planning-roadmap.md` by detailing the simulation-specific backlog.

---

## 1. Workstream Summary

| Workstream | Goals | Dependencies |
|------------|-------|--------------|
| **WS1: Schema Foundations** | Extend template model (window/topology/semantics), preserve metadata, upgrade generation pipeline. | None (internal refactor). |
| **WS2: Shared Validation** | Adopt shared expression parser, enforce topology/semantic rules, add mode awareness. | Engine follow-up: shared expression library. |
| **WS3: Service & CLI Enhancements** | Embed provenance, expose schema metadata, update storage layout, add mode toggles. | WS1, WS2. |
| **WS4: Template & Fixture Upgrade** | Convert curated templates to schema v1.1, add M-3 fixtures, align examples with KISS rules. | WS1. |
| **WS5: Testing & Tooling** | Expand unit/integration tests, add regression suites, update documentation and examples. | WS1–WS4. |

---

## 2. Detailed Task Breakdown

### WS1 — Schema Foundations

1. Introduce new template classes (`TemplateWindow`, `TemplateTopology`, etc.) and update YAML parsing.
2. Add a required `version` field to `TemplateMetadata`; ensure provenance and CLI/service surfaces expose it.
3. Modify `NodeBasedTemplateService` to retain metadata, parameters, and outputs, and to emit canonical schema fields.
4. Add `Source` and `Initial` properties to `TemplateNode`; ensure parameter substitution handles nested paths.
5. Update serialization tests (`tests/FlowTime.Sim.Tests/NodeBased/*`) to cover new classes and metadata versioning.

**Exit Criteria:** Generating a model from an updated template yields window/topology sections and passes basic Engine parsing (no semantic validation yet).

### WS2 — Shared Validation

1. Consume the shared `FlowTime.Expressions` package once Engine publishes it (see Post-M-03.00 follow-up #1).
2. Replace manual expression checks with shared parser/AST; surface errors with actionable messages.
3. Implement topology/semantics validator following `sim-schema-and-validation.md`.
4. Introduce `TemplateMode` and propagate to provenance.

**Exit Criteria:** Invalid templates fail generation with deterministic errors; unit tests cover expression and topology edge cases.

### WS3 — Service & CLI Enhancements

1. Default `/api/v1/templates/{id}/generate` to embed provenance; remove legacy stripping of metadata.
2. Add API fields for window/topology coverage (e.g., `hasTopology`, `mode`).
3. Update CLI commands to write new schema by default and surface validation warnings/errors.
4. Refresh storage layout (hash directories) to include schema version and mode metadata.

**Exit Criteria:** CLI and service responses showcase the new schema, provenance, and validation behavior; manual smoke tests confirm compatibility with Engine M-3 fixtures.

### WS4 — Template & Fixture Upgrade

1. Convert curated templates in `templates/` to schema v1.1 (window, topology, semantics, provenance).
2. Add M-3 ready fixtures mirroring Engine scenarios used by the UI.
3. Provide migration notes or CLI script to upgrade existing schema v1.0 templates.

**Exit Criteria:** All shipping templates pass validation in WS2 and run end-to-end with Engine M-03.x; changelog documents the upgrade.

### WS5 — Testing & Tooling

1. Expand unit tests (Template serialization, validator rules, provenance embedder).
2. Add integration tests that generate models and run them through Engine test harness or fixtures.
3. Update `.http` examples and CLI walkthroughs to demonstrate new schema capabilities.
4. Refresh documentation (`sim` architecture package) as tasks complete.

**Exit Criteria:** CI suites prevent regression on schema/validation; documentation reflects final state.

---

## 3. Timeline & Milestone Alignment

| Phase | Roadmap Alignment | Notes |
|-------|-------------------|-------|
| WS1 | Aligns with Engine M-03.00 deliverables (schema foundation). | Should begin immediately; no external dependencies. |
| WS2 | Requires Engine follow-up shared library. | Track dependency in roadmap Post-M-03.00 section. |
| WS3 | Parallel with Engine M-03.01 API work. | Ensure FIFO validation so Engine fixtures can rely on FlowTime.Sim models. |
| WS4 | Supports Engine M-03.02 TelemetryLoader. | Templates must be ready for telemetry binding ahead of loader integration. |
| WS5 | Runs continuously; finalize before Engine M-03.03 validation milestone. | |

---

## 4. Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Shared library delivery slips | Validation overhaul blocked | Stub an internal wrapper matching expected API; swap when library arrives. |
| Template migration churn | Breaking changes frustrate users | Provide automated upgrade script and detailed release notes. |
| Schema drift between Sim & Engine | Runtime regressions | Add contract tests that serialize a Sim template and parse it with Engine in CI. |
| Increased validation cost | Generation time delays | Benchmark validator, cache ASTs where possible, keep rules deterministic. |

---

## 5. Deliverable Tracking

| ID | Description | Owner | Status |
|----|-------------|-------|--------|
| SIM-M-03.WS1-01 | Implement window/topology classes | Sim Core | Not Started |
| SIM-M-03.WS1-02 | Preserve metadata during generation | Sim Service | Not Started |
| SIM-M-03.WS1-03 | Add TemplateMetadata.version and propagate to provenance | Sim Core | Not Started |
| SIM-M-03.WS2-01 | Integrate shared expression validator | Sim Core | Blocked (Engine follow-up) |
| SIM-M-03.WS3-01 | Embed provenance by default | Sim Service | Not Started |
| SIM-M-03.WS4-01 | Upgrade curated templates | Sim Core | Not Started |
| SIM-M-03.WS5-01 | Add integration tests using Engine fixtures | Sim Tests | Not Started |

Update this table as work progresses.

---

## 6. Hand-offs & Communication

- Coordinate with Engine owners on shared library release cadence.
- Notify UI team when topology semantics are stable for layout work.
- Document breaking changes in release notes (`docs/releases/`) and ensure CLI `--help` references the new schema.

---

## 7. References

- `sim-architecture-overview.md`
- `sim-schema-and-validation.md`
- `time-travel-planning-roadmap.md` (Post-M-03.00 follow-up)
- FlowTime.Sim readiness audit (`sim-time-travel-readiness-audit.md`)
