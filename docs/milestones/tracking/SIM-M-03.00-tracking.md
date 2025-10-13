# SIM-M-03.00 Implementation Tracking

**Milestone:** SIM-M-03.00 — Schema Foundations & Shared Validation  
**Started:** 2025-10-13  
**Status:** 🚧 In Progress  
**Branch:** `time-travel-m3`

---

## Quick Links

- **Milestone Document:** [`docs/milestones/SIM-M-03.00.md`](../SIM-M-03.00.md)
- **Implementation Plan:** [`docs/architecture/time-travel/sim/sim-implementation-plan.md`](../../architecture/time-travel/sim/sim-implementation-plan.md)
- **Schema Requirements:** [`docs/architecture/time-travel/sim/sim-schema-and-validation.md`](../../architecture/time-travel/sim/sim-schema-and-validation.md)
- **Readiness Audit:** [`docs/architecture/time-travel/sim-time-travel-readiness-audit.md`](../../architecture/time-travel/sim-time-travel-readiness-audit.md)

---

## Current Status

### Workstream Snapshot
- **WS1 – Schema Foundations:** ✅ Implemented new template shapes (`TemplateWindow`, `TemplateTopology`, semantics) and refactored `TemplateService` to emit canonical YAML with window/topology/provenance preserved.
- **WS2 – Shared Validation:** ✅ Integrated `FlowTime.Expressions` semantic validator; topology validator (node/edge/semantics + mode awareness) in place with extensive unit coverage.
- **WS3 – Service & CLI Enhancements:** ✅ Service/CLI now surface schema/mode metadata, respect mode overrides, and ship updated `.http` walkthroughs (legacy `embed_provenance` kept as shim).
- **WS4 – Template & Fixture Upgrade:** ✅ Curated templates accept optional telemetry `file://` bindings (simulation defaults retained); telemetry-mode unit/integration tests and fixture guidance captured.
- **WS5 – Testing & Tooling:** 🛠 Continuing work on telemetry warning policy and synthetic Gold automation; regression coverage expanded (Sim↔Engine telemetry test).

### Test Status
- ✅ `dotnet test tests/FlowTime.Sim.Tests/FlowTime.Sim.Tests.csproj`
- ⚠️ `dotnet test FlowTime.sln` (single performance flake `M2PerformanceTests.Test_PMF_Complexity_Scaling`; re-run in isolation ✅)

---

## Progress Log

### 2025-10-20 — WS3/WS4 Enhancements

**Changes**
- Renamed `NodeBasedTemplateService` → `TemplateService`, ensuring schema/mode metadata propagates through CLI, service, and provenance payloads.
- Updated service HTTP walkthrough and API implementation to enumerate nested `schema-{n}/mode-{name}/{hash}` directories and return latest models with schema/mode context.
- Added telemetry-mode unit test (`ModelGenerationTests.GenerateModelAsync_WithTelemetryMode_PopulatesSourcesAndTelemetryMetadata`) and Sim↔Engine integration test validating telemetry artifacts.
- Annotated curated templates with optional telemetry source parameters plus documentation/fixture notes; refreshed API/Sim `.http` samples and synthetic CSV fixtures for demos.

**Docs**
- `templates/README.md`, `templates/fixtures.md`, `FlowTime.API.http`, `FlowTime.Sim.Service.http` updated to reflect telemetry bindings and storage layout.
- Tracking document refreshed to capture WS3/WS4 completion status.

**Tests**
- ✅ `dotnet test tests/FlowTime.Sim.Tests/FlowTime.Sim.Tests.csproj --filter ModelGenerationTests`
- ✅ `dotnet test tests/FlowTime.Integration.Tests/FlowTime.Integration.Tests.csproj --filter Telemetry_Mode_Model_Parses_WithFileSources`
- ⚠️ Full solution run still gated on performance suite flake (tracked under WS5).

**Next Steps**
- Define telemetry-mode warning policy + synthetic Gold automation scope (WS5).
- Monitor performance tests; consider isolating slow scenarios until perf work lands.

---

### 2025-10-13 — Schema Foundations & Validation Pass

**Changes**
- Added comprehensive template model extensions (`TemplateWindow`, `TemplateTopology`, semantics/initial/UI hints, provenance) and `TemplateValidator` enforcing KISS schema rules with mode awareness.
- Refactored `NodeBasedTemplateService` to substitute parameters safely (arrays, strings), emit canonical YAML via new `SimModelBuilder`, and persist provenance metadata.
- Integrated `FlowTime.Expressions` validator across Sim Core, ensuring shared semantic checks (self-shift, unknown identifiers).
- Migrated curated templates (`templates/*.yaml`) and integration examples (`examples/m0.const.sim.yaml`) to the time-travel template format (window/topology/provenance) with generated models still declaring `schemaVersion: 1`.
- Updated CLI/service behaviour to respect embedded provenance by default while maintaining backward-compatible separate provenance payloads.
- Expanded Sim unit/integration tests, including CLI/service provenance expectations, parameter substitution coverage, and topology validation scenarios.

**Docs**
- Updated `sim-implementation-plan.md` & `sim-schema-and-validation.md` to reflect implemented workstreams, validation rules, and migration notes.
- Authored this tracking document for SIM-M-03.00.

**Tests**
- ✅ `dotnet test tests/FlowTime.Sim.Tests/FlowTime.Sim.Tests.csproj`
- ⚠️ `dotnet test FlowTime.sln` (performance flake) → ✅ `dotnet test tests/FlowTime.Tests/FlowTime.Tests.csproj --filter "FullyQualifiedName=FlowTime.Tests.Performance.M2PerformanceTests.Test_PMF_Complexity_Scaling"`

**Next Steps**
- Complete WS3 response formatting and storage metadata (✅ 2025-10-20).
- Continue WS4 template migrations (✅ 2025-10-20) and add migration guide (queued under WS5).
- Harden integration/contract tests between Sim and Engine (in progress under WS5).

---

## Open Items / Risks
- Service/CLI payload adjustments (mode flags, hash metadata) queued for WS3.
- Need documented migration guide + automation for legacy template upgrades.
- Performance tests occasionally flake; monitor after warm-up improvements.
