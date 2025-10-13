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
- **WS1 – Schema Foundations:** ✅ Implemented new template shapes (`TemplateWindow`, `TemplateTopology`, semantics) and refactored `NodeBasedTemplateService` to emit canonical YAML with window/topology/provenance preserved.
- **WS2 – Shared Validation:** 🛠 Integrated `FlowTime.Expressions` semantic validator; topology validator (node/edge/semantics + mode awareness) in place with extensive unit coverage.
- **WS3 – Service & CLI Enhancements:** 🔄 Partial — service/CLI still default to embedding provenance, but response formatting updates deferred to follow-up milestone.
- **WS4 – Template & Fixture Upgrade:** 🛠 Core curated templates (`templates/`) and integration examples migrated to the time-travel template format (window/topology/provenance) while preserving `schemaVersion: 1` in emitted models.
- **WS5 – Testing & Tooling:** 🛠 Expanded node-based and integration test suites; documentation refresh underway.

### Test Status
- ✅ `dotnet test tests/FlowTime.Sim.Tests/FlowTime.Sim.Tests.csproj`
- ⚠️ `dotnet test FlowTime.sln` (single performance flake `M2PerformanceTests.Test_PMF_Complexity_Scaling`; re-run in isolation ✅)

---

## Progress Log

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
- Finish WS3 updates (service/CLI response payloads, storage metadata).
- Continue WS4 template migrations (examples/fixtures parity) and add migration guide.
- Harden integration/contract tests between Sim and Engine.

---

## Open Items / Risks
- Service/CLI payload adjustments (mode flags, hash metadata) queued for WS3.
- Need documented migration guide + automation for legacy template upgrades.
- Performance tests occasionally flake; monitor after warm-up improvements.
