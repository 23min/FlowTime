# FlowTime.Sim Time-Travel Implementation Plan

**Status:** ✅ Core delivered (WS1–WS4 complete; WS5 refinements ongoing)**  
**Last Updated:** 2025-10-16  
**Scope:** FlowTime.Sim Core, Service, CLI, Templates, Tests

This plan decomposes the work required to align FlowTime.Sim with the time-travel roadmap. It complements the end-to-end milestone sequencing in `time-travel-planning-roadmap.md` by detailing the simulation-specific backlog. Live ADX ingestion remains deferred; the telemetry capture/bundle flow is the supported path until that follow-up is prioritised.

---

## 1. Workstream Summary

| Workstream | Goals | Dependencies |
|------------|-------|--------------|
| **WS1: Schema Foundations** | ✅ Complete — Template model extended (window/topology/semantics/provenance) and generation pipeline emits canonical KISS schema. | None (internal refactor). |
| **WS2: Shared Validation** | ✅ Complete — Shared FlowTime.Expressions validator integrated; topology & mode-aware validation enforced. | Engine follow-up: shared expression library. |
| **WS3: Service & CLI Enhancements** | ✅ Complete — Service/CLI surface schema metadata, provenance defaults, mode toggles; `.http` refresh tracked under WS5. | WS1, WS2. |
| **WS4: Template & Fixture Upgrade** | ✅ Complete — Templates accept optional telemetry bindings; fixtures + docs refreshed. | WS1. |
| **WS5: Testing & Tooling** | 🛠 In Progress — Telemetry warning policy & synthetic Gold automation pending; regression coverage expanding. | WS1–WS4. |

---

## 2. Detailed Task Breakdown

### WS1 — Schema Foundations

1. ✅ Introduce new template classes (`TemplateWindow`, `TemplateTopology`, etc.) and update YAML parsing. *(2025-10-13)*
2. ✅ Add required `TemplateMetadata.version` and propagate to provenance/service layers. *(2025-10-13)*
3. ✅ Refactor `TemplateService` to retain metadata/parameters/outputs and emit canonical schema. *(2025-10-13)*
4. ✅ Add `Source`/`Initial` handling to `TemplateNode`; parameter substitution now supports arrays/strings safely. *(2025-10-13)*
5. ✅ Refresh node-based tests to cover new classes and metadata versioning. *(2025-10-13)*

**Exit Criteria:** **Met** — Generated models include window/topology semantics and pass Engine parsing smoke test.

### WS2 — Shared Validation

1. ✅ Consume shared `FlowTime.Expressions` package (ExpressionSemanticValidator) and wire smoke tests. *(Complete)*
2. ✅ Replace bespoke expression checks with shared parser/AST; surfaced deterministic errors. *(Complete)*
3. ✅ Implement topology/semantics validator per schema spec (node kinds, edges, queue initials). *(Complete)*
4. ✅ Introduce `TemplateMode` (simulation/telemetry) and propagate to provenance + mode-aware validation. *(Complete)*

**Exit Criteria:** **Met** — Invalid templates fail fast with shared validation errors covered by unit/integration tests.

### WS3 — Service & CLI Enhancements

1. ✅ Default `/api/v1/templates/{id}/generate` to embed provenance and retain the legacy `embed_provenance` query flag as a no-op compatibility shim.
2. ✅ Include metadata summary fields (`schemaVersion`, `mode`, `hasWindow`, `hasTopology`, `hasTelemetrySources`, `modelHash`) in every service response alongside the persisted JSON sidecars.
3. ✅ Document CLI mode overrides (`--mode simulation|telemetry`), keep verbose output surfacing window/topology/telemetry coverage, and clearly mark `--embed-provenance` as legacy.
4. ✅ Refresh storage layout to `data/models/{templateId}/schema-{schemaVersion}/mode-{mode}/{hashPrefix}/` with embedded provenance plus `metadata.json` and `provenance.json`.
5. 🔄 Follow-up: refresh `.http` samples and user-facing walkthroughs to match the new response contract.

**Exit Criteria:** CLI and service responses showcase the new schema, provenance, and validation behavior; manual smoke tests confirm compatibility with Engine M-3 fixtures.

### WS4 — Template & Fixture Upgrade

1. ✅ Curated templates in `templates/` migrated to the time-travel format with optional telemetry `file://` bindings (simulation defaults retained).
2. ✅ Telemetry fixtures/documentation captured (`templates/README.md`, `templates/fixtures.md`, synthetic CSV examples) and walkthroughs refreshed.

**Exit Criteria:** Templates validate with/without telemetry sources; telemetry-mode tests/fixtures documented for Engine M-03.02.

### WS5 — Testing & Tooling

1. ✅ Expanded unit tests for template serialization, validator rules, provenance embedder.
2. ✅ Integration tests include telemetry-mode Sim→Engine coverage.
3. ✅ CLI/service `.http` samples refreshed post-WS3 updates.
4. 🛠 Define telemetry-mode warning policy and scope synthetic Gold automation.

**Exit Criteria:** Regression coverage aligned with time-travel schema; telemetry warning policy + synthetic Gold plan documented.

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
| SIM-M-03.WS1-01 | Implement window/topology classes | Sim Core | ✅ Complete |
| SIM-M-03.WS1-02 | Preserve metadata during generation | Sim Service | ✅ Complete |
| SIM-M-03.WS1-03 | Add TemplateMetadata.version and propagate to provenance | Sim Core | ✅ Complete |
| SIM-M-03.WS2-01 | Integrate shared expression validator | Sim Core | ✅ Complete |
| SIM-M-03.WS3-01 | Embed provenance defaults & response formatting | Sim Service | ✅ Complete (docs refresh tracked under WS3 follow-up) |
| SIM-M-03.WS4-01 | Upgrade curated templates | Sim Core | ✅ Complete |
| SIM-M-03.WS5-01 | Add integration tests using Engine fixtures | Sim Tests | 🛠 Partial (Sim ↔ Engine smoke test live; telemetry warning policy pending) |
| SIM-M-03.WS2-02 | Add FlowTime.Expressions smoke test in Sim Tests | Sim Tests | ✅ Complete |

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
