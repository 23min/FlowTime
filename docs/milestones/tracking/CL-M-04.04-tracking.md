# CL-M-04.04 Implementation Tracking

**Milestone:** CL-M-04.04 — Telemetry Contract & Loop Validation for Classes  
**Started:** 2025-11-25  
**Status:** 🔄 In Progress  
**Branch:** `milestone/m4.4`

---

## Quick Links

- **Milestone Document:** `docs/milestones/CL-M-04.04.md`
- **Latest Release:** `docs/releases/CL-M-04.03.md`
- **Milestone Guide:** `docs/development/milestone-documentation-guide.md`

---

## Current Status

### Overall Progress
- [ ] Phase 1: Contract & Docs (0/3 tasks)
- [x] Phase 2: Engine + Telemetry Plumbing (1/3 tasks)
- [ ] Phase 3: Capture Endpoint & Loop Tests (0/3 tasks)

### Test Status
- ✅ Targeted: `dotnet test --filter WriteArtifacts_ClassSeriesScaleThroughScalarMultipliers`
- ⏳ Full `dotnet test --nologo` (scheduled for session end)
- ⏳ Telemetry/Loop integration suites (pending Phase 3)

---

## Progress Log

### 2025-11-25 - Kickoff & Engine Backfill

**Changes:**
- Added RED regression (`WriteArtifacts_ClassSeriesScaleThroughScalarMultipliers`) covering scalar multiply/subtract class propagation.
- Updated `ClassContributionBuilder` multiply/min/max logic so per-class series survive capacity clamps and zeroed queues, keeping `classCoverage` truly `full`.
- Regenerated class-aware demo runs for supply chain (`run_20251125T130751Z_21597334`) and transportation (`run_20251125T130822Z_91deaced`).
- Documented progress + run IDs in `docs/milestones/CL-M-04.04.md`.

**Tests:**
- ✅ `dotnet test --filter WriteArtifacts_ClassSeriesScaleThroughScalarMultipliers`
- ⏳ Full build + test sweep (run after remaining changes).

**Next Steps:**
- [ ] Expand schema/docs for telemetry manifest updates (Phase 1 RED tests).
- [ ] Begin TelemetryLoader ingestion work (Phase 2 Task 2).
- [ ] Draft CLI/API validation plan for Phase 3.

**Blockers:**
- None — engine/class artifacts now emit data needed for telemetry loop work.

---

## Phase 1: Contract & Docs

**Goal:** Formalize manifest/schema/doc updates for class-aware telemetry.

**TDD Checklist:**
- [x] RED: `TelemetryManifestSchemaTests.ManifestSchema_Requires_ClassColumns_When_SupportsTrue`
- [x] RED: `TelemetryManifestSchemaTests.ManifestSchema_LegacyMode_AllowsTotalsOnly`
- [x] GREEN: Update manifest schema + `docs/operations/telemetry-capture-guide.md`
- [x] GREEN: Versioning + examples (`docs/schemas/telemetry-manifest.schema.json`, schema index)
- [ ] REFACTOR: Ensure docs/reference pages link to the new schema

_Status:_ Schema + docs landed; final doc sweep (reference cross-links) still pending.

### 2025-12-01 - Telemetry Manifest Schema v2

**Changes:**
- Added RED tests (`TelemetryManifestSchemaTests`) asserting that manifests must declare `supportsClassMetrics`, require per-file `classId` entries when the flag is true, and still allow totals-only bundles when false.
- Bumped `docs/schemas/telemetry-manifest.schema.json` to schemaVersion **2** with the new `supportsClassMetrics` boolean, conditional `classes`/`classCoverage` requirements, and file-level `classId` enforcement.
- Updated `docs/operations/telemetry-capture-guide.md` and `docs/schemas/README.md` to describe the new manifest contract and provide sample JSON.

**Tests:**
- ✅ `dotnet test --filter FullyQualifiedName~TelemetryManifestSchemaTests --nologo`

---

## Phase 2: Engine + Telemetry Plumbing

**Goal:** Ensure engine artifacts + telemetry ingestion stay aligned on per-class data.

### Task 2.1: Engine artifact parity
- ✅ Regression test + `ClassContributionBuilder` fix ensure per-class CSVs exist even when values clamp to zero.
- ✅ Demo runs regenerated for downstream validation.

### Task 2.2: TelemetryLoader ingestion
- ⏳ Planned — will cover CSV parsing + coverage warnings.

### Task 2.3: CLI summary/logging
- ⏳ Planned — update CLI output once ingestion work lands.

---

## Phase 3: Capture Endpoint & Loop Tests

**Goal:** Enforce telemetry contract server-side and validate loop parity.

_Status:_ Pending — will start after loader work is stable._

---

## Testing & Validation Checklist

- [x] Targeted regression for class propagation
- [ ] `dotnet build`
- [ ] `dotnet test --nologo`
- [ ] Telemetry ingestion unit tests
- [ ] API/Loop integration tests

---

## Notes

- Engine side fix unblocks telemetry ingestion by guaranteeing every node publishes per-class CSVs (no more `DEFAULT`-only queues).
- Keep regenerated runs handy for UI + telemetry loop comparisons; both labeled in `data/` for easy discovery.
