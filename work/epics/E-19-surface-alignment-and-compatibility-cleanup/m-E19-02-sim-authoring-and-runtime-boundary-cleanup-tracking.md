# Tracking: m-E19-02 Sim Authoring & Runtime Boundary Cleanup

**Status:** in-progress
**Epic:** [E-19 Surface Alignment & Compatibility Cleanup](./spec.md)
**Milestone spec:** [m-E19-02-sim-authoring-and-runtime-boundary-cleanup.md](./m-E19-02-sim-authoring-and-runtime-boundary-cleanup.md)
**Branch:** `milestone/m-E19-02-sim-authoring-and-runtime-boundary-cleanup` (off `epic/E-19`)

## Acceptance Criteria

- [ ] AC1. Stored drafts CRUD retired (A2): delete `/api/v1/drafts` GET/PUT/POST/DELETE/list routes, `StorageKind.Draft`, `data/storage/drafts/`, and `DraftEndpointsTests.cs` CRUD tests.
- [ ] AC2. `POST /api/v1/drafts/run` narrowed to inline-source only (A1/A2): remove `draftId` resolution branch; inline tests survive.
- [ ] AC3. `POST /api/v1/drafts/validate` deleted (A6): remove endpoint handler and its tests; preserve `ModelSchemaValidator`, `ModelValidator`, `ModelCompiler`, `ModelParser`, `TemplateInvariantAnalyzer`, `InvariantAnalyzer` unchanged.
- [ ] AC4. Sim ZIP archive layer deleted (A3): remove `StorageKind.Run` writes in `RunOrchestrationService`, `BundleRef`/`StorageRef` on `RunCreateResponse`, `data/storage/runs/` backend write path, and the `StorageKind.Run` enum value.
- [ ] AC5. Engine `POST /v1/runs` deleted outright (A4): remove handler, bundle-import branches (`BundlePath`, `BundleArchiveBase64`, `BundleRef`), `ExtractArchiveAsync` helpers, and bundle-import tests. No 410 stub. `GET /v1/runs` and `GET /v1/runs/{runId}` preserved.
- [ ] AC6. Engine debug/direct-eval routes deleted: `GET /v1/debug/scan-directory/{dirName}`, `POST /v1/run`, `POST /v1/graph`.
- [x] AC7. Catalogs retired entirely (A5): routes (`/api/v1/catalogs*`), `CatalogService`/`ICatalogService`, `CatalogPicker.razor`, `CatalogId = "default"` placeholder callers, `catalogId` DTO fields, `data/catalogs/` directory, catalog-only tests.
- [ ] AC8. Public contracts cleanup consolidated in `FlowTime.Contracts`: `RunImportRequest`/`RunCreateResponse` bundle fields gone; `StorageKind.Draft` and `StorageKind.Run` enum values removed.
- [ ] AC9. Build green, full test suite green, grep guards asserted (zero matches for each deleted symbol in `src/` and `tests/`).
- [ ] AC10. Status surfaces reconciled at wrap: epic spec, ROADMAP.md, epic-roadmap.md, CLAUDE.md, and this tracking doc all show m-E19-02 complete with final test count and grep guard results recorded.

## Implementation Sequence

Per milestone spec Technical Notes — each step must leave build green and tests passing before the next begins.

- [x] Step 1: Catalogs (AC7) — lowest coupling, highest confidence
- [ ] Step 2: `/api/v1/drafts/validate` (AC3) — trivial unused route
- [ ] Step 3: Stored drafts CRUD (AC1)
- [ ] Step 4: Narrow `/api/v1/drafts/run` (AC2)
- [ ] Step 5: Sim ZIP archive layer (AC4)
- [ ] Step 6: Engine `POST /v1/runs` + bundle-import (AC5)
- [ ] Step 7: Engine debug/direct-eval routes (AC6)
- [ ] Step 8: Public contracts finalisation (AC8)
- [ ] Step 9: Grep guards + build/test finalisation (AC9)
- [ ] Step 10: Wrap (AC10)

## Grep Guards

Each must return zero matches in `src/` and `tests/` at wrap time.

- [ ] `drafts/{draftId` (draft CRUD handlers gone)
- [ ] `StorageKind.Draft`
- [ ] `data/storage/drafts`
- [ ] `drafts/validate` handler literal
- [ ] `StorageKind.Run`
- [ ] `BundleRef`
- [ ] `StorageRef`
- [ ] `data/storage/runs`
- [ ] `MapPost("/runs", HandleCreateRunAsync)`
- [ ] `BundlePath`
- [ ] `BundleArchiveBase64`
- [ ] `/v1/debug/scan-directory`
- [ ] `MapPost("/run"` and `MapPost("/graph"`
- [x] `/api/v1/catalogs`
- [x] `CatalogService`
- [x] `ICatalogService`
- [x] `CatalogPicker`
- [x] `CatalogId = "default"`

## Notes

- Forward-only deletion; no compatibility shims, no 410 stubs, no feature flags.
- `FlowTime.Core` and `FlowTime.Generator` stay frozen per E-19 shared framing.
- Blazor stale wrappers beyond `CatalogPicker.razor` are m-E19-04.
- Schema/template/example/docs cleanup is m-E19-03.
- Tiered validation replacement for `/api/v1/drafts/validate` is E-18 m-E18-01b (not this milestone).

## AC7 implementation log

**Status:** complete. Build green, 1260 tests pass (0 failed, 9 skipped), all AC7 grep guards return zero matches for runtime catalog symbols.

**Files deleted (wholesale):**
- `src/FlowTime.Sim.Core/Catalog.cs`
- `src/FlowTime.Sim.Core/CatalogIO.cs`
- `src/FlowTime.UI/Components/Templates/CatalogPicker.razor`
- `tests/FlowTime.Sim.Tests/CatalogTests.cs`
- `tests/FlowTime.Sim.Tests/CatalogIOTests.cs`
- `catalogs/demo-system.yaml`, `catalogs/tiny-demo.yaml` (and the `catalogs/` directory)
- `data/catalogs/` directory (was empty)

**Files edited:**
- `src/FlowTime.Sim.Service/Program.cs` — removed 4 catalog routes, `EnsureRuntimeCatalogs` startup call + method, `CatalogsRoot` helper, `IsSafeCatalogId` helper, catalog entries in the healthz capabilities list, and updated the `DataRoot` docstring. The `DraftSourceResolution` helper's two `IsSafeCatalogId` call sites were switched to the existing `IsSafeId` helper (the draft paths never relied on the dot-allowed variant, so `IsSafeId` is the correct generic match).
- `src/FlowTime.Sim.Service/Services/CapabilitiesDetectionService.cs` — removed `catalog-management` from the core features array.
- `src/FlowTime.Sim.Service/Services/ServiceInfoProvider.cs` — removed `catalogsDirectory` from service-info health details.
- `src/FlowTime.Sim.Service/FlowTime.Sim.Service.http` — removed the "Catalog Operations" section.
- `src/FlowTime.Sim.Cli/CliConfig.cs` — removed the `DataConfig.Catalogs` YAML property.
- `src/FlowTime.UI/Program.cs` — removed the `ICatalogService` DI registration.
- `src/FlowTime.UI/Pages/TemplateRunner.razor` — removed `@inject ICatalogService`, catalog copy from the page description, `selectedCatalog` / `selectedCatalogModeVersion` fields, all catalog clears in `OnFlagsChanged` and `ToggleMode`, `AutoSelectDefaultCatalogAsync` method, `OnCatalogSelected` handler, two `CatalogId = "default"` assignments on `SimulationRunRequest`, and made `OnTemplateSelected` non-async since it no longer awaits anything.
- `src/FlowTime.UI/Pages/Health.razor` — removed the "Catalogs Directory" health display line.
- `src/FlowTime.UI/Services/TemplateServices.cs` — removed `ICatalogService` interface, `CatalogInfo` DTO, and `SimulationRunRequest.CatalogId` field.
- `src/FlowTime.UI/Services/TemplateServiceImplementations.cs` — removed the entire `CatalogService` class (including `GetMockCatalogs` mock data) and the `catalogId` plumbing in `ConvertRequestToApiParameters`.
- `src/FlowTime.UI/Services/FlowTimeApiModels.cs` — removed `HealthDetails.CatalogsDirectory` property.
- `src/FlowTime.UI/wwwroot/css/app.css` — removed `.catalog-select-dropdown` rules.
- `src/FlowTime.UI/wwwroot/css/template-studio.css` — removed `.catalog-card*` rules.
- `tests/FlowTime.Sim.Tests/ServiceIntegrationTests.cs` — removed 5 catalog integration tests (other tests in the file preserved).
- `tests/FlowTime.UI.Tests/TemplateServiceParameterConversionTests.cs` — removed `ConvertRequestToApiParameters_ShouldPreserveCatalogId` theory (catalog-only test).

**Grep guards verified zero-match after deletion:**
- `/api/v1/catalogs`
- `CatalogService`
- `ICatalogService`
- `CatalogPicker`
- `CatalogId = "default"`
- `data/catalogs/`

Remaining repo references to "catalog" (preserved on purpose): `ClassCatalogEntry` / `BuildClassCatalog` in `StateQueryService.cs` (state-schema class list, purified by E-16) and the `MetricProvenanceCatalog` UI helper family. Both are unrelated to the runtime catalog concept that A5 retired.
