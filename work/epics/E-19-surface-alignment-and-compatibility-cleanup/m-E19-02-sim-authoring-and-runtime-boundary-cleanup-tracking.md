# Tracking: m-E19-02 Sim Authoring & Runtime Boundary Cleanup

**Status:** in-progress
**Epic:** [E-19 Surface Alignment & Compatibility Cleanup](./spec.md)
**Milestone spec:** [m-E19-02-sim-authoring-and-runtime-boundary-cleanup.md](./m-E19-02-sim-authoring-and-runtime-boundary-cleanup.md)
**Branch:** `milestone/m-E19-02-sim-authoring-and-runtime-boundary-cleanup` (off `epic/E-19`)

## Acceptance Criteria

- [x] AC1. Stored drafts CRUD retired (A2): delete `/api/v1/drafts` GET/PUT/POST/DELETE/list routes, `StorageKind.Draft`, `data/storage/drafts/`, and `DraftEndpointsTests.cs` CRUD tests.
- [x] AC2. `POST /api/v1/drafts/run` narrowed to inline-source only (A1/A2): remove `draftId` resolution branch; inline tests survive.
- [x] AC3. `POST /api/v1/drafts/validate` deleted (A6): remove endpoint handler and its tests; preserve `ModelSchemaValidator`, `ModelValidator`, `ModelCompiler`, `ModelParser`, `TemplateInvariantAnalyzer`, `InvariantAnalyzer` unchanged.
- [x] AC4. Sim ZIP archive layer deleted (A3): remove `StorageKind.Run` writes in `RunOrchestrationService`, `BundleRef`/`StorageRef` on `RunCreateResponse`, `data/storage/runs/` backend write path, and the `StorageKind.Run` enum value.
- [x] AC5. Engine `POST /v1/runs` deleted outright (A4): remove handler, bundle-import branches (`BundlePath`, `BundleArchiveBase64`, `BundleRef`), `ExtractArchiveAsync` helpers, and bundle-import tests. No 410 stub. `GET /v1/runs` and `GET /v1/runs/{runId}` preserved.
- [x] AC6. Engine debug route deleted (narrowed scope — see implementation log): `GET /v1/debug/scan-directory/{dirName}` deleted. `POST /v1/run` and `POST /v1/graph` deletion deferred per D-2026-04-08-029 — audit missed 50+ test call sites that use these routes for Engine-side provenance/parity coverage. Deferral tracked in `work/gaps.md`.
- [x] AC7. Catalogs retired entirely (A5): routes (`/api/v1/catalogs*`), `CatalogService`/`ICatalogService`, `CatalogPicker.razor`, `CatalogId = "default"` placeholder callers, `catalogId` DTO fields, `data/catalogs/` directory, catalog-only tests.
- [ ] AC8. Public contracts cleanup consolidated in `FlowTime.Contracts`: `RunImportRequest`/`RunCreateResponse` bundle fields gone; `StorageKind.Draft` and `StorageKind.Run` enum values removed.
- [ ] AC9. Build green, full test suite green, grep guards asserted (zero matches for each deleted symbol in `src/` and `tests/`).
- [ ] AC10. Status surfaces reconciled at wrap: epic spec, ROADMAP.md, epic-roadmap.md, CLAUDE.md, and this tracking doc all show m-E19-02 complete with final test count and grep guard results recorded.

## Implementation Sequence

Per milestone spec Technical Notes — each step must leave build green and tests passing before the next begins.

- [x] Step 1: Catalogs (AC7) — lowest coupling, highest confidence
- [x] Step 2: `/api/v1/drafts/validate` (AC3) — trivial unused route
- [x] Step 3: Stored drafts CRUD (AC1)
- [x] Step 4: Narrow `/api/v1/drafts/run` (AC2)
- [x] Step 5: Sim ZIP archive layer (AC4)
- [x] Step 6: Engine `POST /v1/runs` + bundle-import (AC5)
- [x] Step 7: Engine debug route (AC6, narrowed)
- [ ] Step 8: Public contracts finalisation (AC8)
- [ ] Step 9: Grep guards + build/test finalisation (AC9)
- [ ] Step 10: Wrap (AC10)

## Grep Guards

Each must return zero matches in `src/` and `tests/` at wrap time.

- [x] `drafts/{draftId` (draft CRUD handlers gone)
- [x] `StorageKind.Draft`
- [x] `data/storage/drafts`
- [x] `drafts/validate` handler literal
- [x] `StorageKind.Run`
- [x] `BundleRef`
- [x] `data/storage/runs`
- [x] `MapPost("/runs", HandleCreateRunAsync)` on Engine surface (Sim orchestration keeps the literal on its supported endpoint)
- [x] `BundlePath`
- [x] `BundleArchiveBase64`
- [x] `StorageRef` on bundle response contracts (type still exists for Model/Series storage infrastructure)
- [x] `/v1/debug/scan-directory`
- [ ] ~~`MapPost("/run"` and `MapPost("/graph"`~~ — deferred out of m-E19-02 per D-2026-04-08-029; tracked in `work/gaps.md`
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

## AC3 implementation log

**Status:** complete. Build green, 1258 tests pass (0 failed, 9 skipped).

**Files edited:**
- `src/FlowTime.Sim.Service/Program.cs` — deleted the `POST /api/v1/drafts/validate` endpoint handler (previously ~77 lines wrapping compile + evaluate + analyse through `TemplateInvariantAnalyzer`).
- `tests/FlowTime.Sim.Tests/Service/DraftEndpointsTests.cs` — deleted `ValidateDraftInline_ReturnsValidPayload` and `ValidateDraftId_ReturnsValidPayload` (the only tests exercising the route).

**Preserved unchanged (A6 library contract):**
- `FlowTime.Core.Models.ModelSchemaValidator`
- `FlowTime.Core.Models.ModelValidator`
- `FlowTime.Core.Compiler.ModelCompiler`
- `FlowTime.Core.Models.ModelParser`
- `FlowTime.Sim.Core.Analysis.TemplateInvariantAnalyzer`
- `FlowTime.Core.Analysis.InvariantAnalyzer`

These become the tier 1/2/3 ingredients for the future Time Machine validation operation per D-2026-04-07-017 / E-18 m-E18-01b.

**Grep guard verified zero-match after deletion:** `drafts/validate`.

## AC1 + AC2 implementation log (bundled commit)

**Status:** complete. Build green (0 warnings, 0 errors), 1258 tests pass (0 failed, 9 skipped).

**Rationale for bundling:** A1 narrowing (`/api/v1/drafts/run` inline-only) and A2 (stored drafts retirement) are atomically coupled — deleting `StorageKind.Draft` makes the `draftId` branch of `ResolveDraftSourceAsync` unreachable, so the resolver narrowing is forced by the CRUD deletion. Separating them would leave either half-deleted code or an orphan enum value between commits.

**Files edited:**

- `src/FlowTime.Sim.Service/Program.cs`:
  - deleted 5 draft CRUD handlers: `GET /api/v1/drafts`, `GET /api/v1/drafts/{draftId}`, `POST /api/v1/drafts`, `PUT /api/v1/drafts/{draftId}`, `DELETE /api/v1/drafts/{draftId}`
  - narrowed `ResolveDraftSourceAsync` to the inline branch only, rejecting any other `source.type` with a clear error; method is now synchronous under the hood and returns `Task.FromResult`
  - deleted the `persist` branch from `POST /api/v1/drafts/map-profile` (it only served stored drafts); response no longer carries `persisted`
  - deleted unused DTOs: `DraftCreateRequest`, `DraftUpdateRequest`, `DraftWriteResponse`, `DraftResponse`, `DraftSummary`, `DraftListResponse`
  - dropped `DraftProfileMapRequest.Persist`
  - changed `DraftSource.Type` default from `"draftId"` to `"inline"` (it's now the only supported value)
- `src/FlowTime.Contracts/Storage/StorageContracts.cs` — removed `Draft` from the `StorageKind` enum
- `src/FlowTime.Contracts/Storage/StorageBackends.cs` — removed `StorageKind.Draft => "drafts"` from `StoragePathHelper.GetKindFolder`

**Test files:**

- `tests/FlowTime.Sim.Tests/Service/DraftEndpointsTests.cs` — unchanged by this commit; the CRUD tests were already removed by AC3 (there were none to begin with — only `ValidateDraftInline/Id` had been there, both deleted in AC3). The surviving `GenerateDraftInline_ReturnsModel`, `GenerateDraftInline_RegistersModel`, and `RunDraftInline_ReturnsRunId` all use `source.type = "inline"` and still pass.
- `tests/FlowTime.Sim.Tests/Service/ProfileEndpointsTests.cs` — rewrote `MapProfileToDraft_UpdatesYaml` to use inline source (was creating a stored draft first). Removed the tail assertion that read back via `GET /api/v1/drafts/{draftId}`, since that endpoint no longer exists.
- `tests/FlowTime.Tests/Storage/StorageRefTests.cs` — migrated `TryParse_ParsesValidStorageUri` to use `storage://model/model_001` and `StorageKind.Model` (was `storage://draft/draft_001`). The test exercises the `StorageRef.TryParse` infrastructure, not drafts specifically, so using `Model` as a generic substrate preserves the infrastructure coverage without retaining draft references.
- `tests/FlowTime.Tests/Storage/FileSystemStorageBackendTests.cs` — migrated `WriteReadListDelete_WorksEndToEnd` to use `StorageKind.Model` / `model_1` / `model-content` for the same reason. This is test adaptation, not retention of deleted concepts.

**Grep guards verified zero-match after deletion:**

- `drafts/{draftId` (no CRUD handlers)
- `StorageKind.Draft`
- `data/storage/drafts`
- `DraftCreateRequest`, `DraftUpdateRequest`, `DraftWriteResponse`, `DraftListResponse`, `DraftSummary`
- `"draftid"` / `"draftId"` source-type literal in Sim runtime
- `Persist` / `persisted` in `Program.cs`

**Preserved surviving surfaces:**

- `POST /api/v1/drafts/generate` — inline-only authoring generate
- `POST /api/v1/drafts/run` — inline-only transitional execution bridge (A1)
- `POST /api/v1/drafts/map-profile` — inline-only authoring helper (persist dropped)
- `DraftSource`, `DraftTemplateRequest`, `DraftRunRequest`, `DraftProfileMapRequest`, `DraftSourceResolution` — still used by surviving routes
- `FileSystemStorageBackend`, `BlobStorageBackend`, `StorageBackendOptions`, `StorageRef`, `IStorageBackend`, `StorageListRequest`, etc. — the storage abstraction is still needed for `StorageKind.Model`, `StorageKind.Run`, `StorageKind.Series`. Only the `Draft` enum value and its backend path mapping were removed.

## AC4 + AC5 implementation log (bundled commit)

**Status:** complete. Build green (0 warnings, 0 errors), 1250 tests pass (0 failed, 9 skipped; down 8 from pre-AC4 baseline of 1258, matching the two deleted test files).

**Rationale for bundling:** A3 (Sim ZIP archive layer) and A4 (Engine bundle-import + `POST /v1/runs`) share the `BundleRef` and `BundlePath`/`BundleArchiveBase64` types on `FlowTime.Contracts`. A3 deletes the writer; A4 deletes the reader and the import route. Both halves must land atomically because `RunImportRequest` and `RunCreateResponse.BundleRef` are cross-surface types — deleting the writer without the reader leaves dead request fields on the Engine surface, and vice versa.

**Files edited:**

- `src/FlowTime.API/Endpoints/RunOrchestrationEndpoints.cs` — rewrote the file to contain only `HandleListRunsAsync` and `HandleGetRunAsync`. Deleted `MapPost("/runs", ...)` registration, `HandleCreateRunAsync`, `ExtractArchiveAsync` (both overloads), `TryReadRunIdAsync`, `FindBundleRoot`, `CopyDirectory`, `TryDeleteDirectory`. Removed unused imports (`System.IO.Compression`, `System.Text.Json`, `FlowTime.API.Services`, `FlowTime.Contracts.Storage`, `FlowTime.Generator.Artifacts`).
- `src/FlowTime.Contracts/TimeTravel/RunContracts.cs` — deleted `RunImportRequest` class entirely. Removed `BundleRef` property from `RunCreateResponse`. Removed the `FlowTime.Contracts.Storage` using since `StorageRef` is no longer referenced from this file.
- `src/FlowTime.Contracts/Storage/StorageContracts.cs` — removed `Run` from the `StorageKind` enum.
- `src/FlowTime.Contracts/Storage/StorageBackends.cs` — removed `StorageKind.Run => "runs"` from `StoragePathHelper.GetKindFolder`.
- `src/FlowTime.Sim.Service/Extensions/RunOrchestrationEndpointExtensions.cs` — removed the archive build + `storage.WriteAsync(StorageKind.Run, ...)` block from `HandleCreateRunAsync`, dropped `BundleRef = bundleWrite.Reference` from the response, dropped the `IStorageBackend storage` parameter (no longer used), and removed the `FlowTime.Contracts.Storage` using.
- `src/FlowTime.Sim.Service/Program.cs` — same removal in the `POST /api/v1/drafts/run` handler (the archive build + storage write + `BundleRef` on response). Deleted the private `BuildRunArchive` helper (no longer called from anywhere). The supported Sim orchestration endpoint (`/api/v1/orchestration/runs`) still uses `MapPost("/runs", HandleCreateRunAsync)` as its internal registration literal, because it is a different handler on a different route prefix.

**Files deleted:**

- `tests/FlowTime.Api.Tests/RunOrchestrationTests.cs` (6 bundle-import tests, all targeted the deleted `POST /v1/runs` route)
- `tests/FlowTime.Api.Tests/RunOrchestrationGoldenTests.cs` (2 bundle-import golden tests)
- `tests/FlowTime.Api.Tests/Golden/create-run-response.golden.json`
- `tests/FlowTime.Api.Tests/Golden/get-run-response.golden.json`
- `tests/FlowTime.Api.Tests/Golden/simulation-create-run-response.golden.json`
- `tests/FlowTime.Api.Tests/Golden/simulation-get-run-response.golden.json`
- `tests/FlowTime.Api.Tests/Golden/list-runs-response.golden.json`
- Corresponding `.actual` files

**Files fixed to adapt to the deletion:**

- `tests/FlowTime.Api.Tests/TelemetryCaptureEndpointsTests.cs` — `CreateRunAndImportAsync` helper was creating a run in a temp directory then importing it via `POST /v1/runs`. Rewrote it to call `RunOrchestrationService.CreateRunAsync` directly against `Program.ServiceHelpers.RunsRoot(configuration)` (the Engine's canonical runs directory). The round-trip-through-import step was only an artifact of the old test topology, not a meaningful coverage concern. Added a `Microsoft.Extensions.Configuration` using.
- `tests/FlowTime.Sim.Tests/Service/DraftEndpointsTests.cs` — removed the `bundleRef` assertion from `RunDraftInline_ReturnsRunId` (the field no longer exists on `RunCreateResponse`).
- `tests/FlowTime.Tests/Storage/StorageRefTests.cs` — migrated `TryParse_ParsesOptionalQueryFields` from `storage://run/run_123` / `StorageKind.Run` to `storage://series/series_123` / `StorageKind.Series` (test was using `Run` as a substrate, not exercising run-specific behaviour).

**Grep guards verified zero-match after deletion:**

- `StorageKind.Run`
- `BundleRef`
- `data/storage/runs`
- `BuildRunArchive`
- `BundlePath` / `bundlePath`
- `BundleArchiveBase64` / `bundleArchiveBase64`
- `RunImportRequest`
- `ExtractArchiveAsync`

**Preserved surviving surfaces:**

- `GET /v1/runs` and `GET /v1/runs/{runId}` — the canonical run query surface
- `POST /api/v1/orchestration/runs` (Sim) — A1-supported orchestration endpoint (no longer writes ZIP archives; just returns canonical metadata)
- `POST /api/v1/drafts/run` (Sim) — A1-supported inline-source transitional execution bridge (no longer writes ZIP archives)
- Canonical run directory under `data/runs/<runId>/` — unchanged
- `StorageRef`, `IStorageBackend`, `FileSystemStorageBackend`, `BlobStorageBackend` — storage infrastructure still needed for `StorageKind.Model` and `StorageKind.Series`

## AC6 implementation log (scope narrowed)

**Status:** complete. Build green (0 warnings, 0 errors), 1250 tests pass (0 failed, 9 skipped — same count as pre-AC6; no tests removed).

**Scope change:** m-E19-01 A5/matrix scheduled three routes for deletion in AC6: `GET /v1/debug/scan-directory/{dirName}`, `POST /v1/run`, `POST /v1/graph`. During implementation, grep showed that `POST /v1/run` has 50 call sites and `POST /v1/graph` has 2 call sites across 9 Engine test files (`ProvenanceHashTests`, `ProvenanceHeaderTests`, `ProvenanceEmbeddedTests`, `ProvenancePrecedenceTests`, `ProvenanceStorageTests`, `ProvenanceQueryTests`, `Legacy/ApiIntegrationTests`, `ParityTests`, `CliApiParityTests`). These are the primary run-creation mechanism for Engine-side runtime provenance, CLI ↔ API parity, and legacy integration coverage. Deleting the routes in this milestone would either regress ~50 provenance/parity tests or pull substantial test-migration work that is out of scope for a runtime-cleanup milestone.

AC6 was narrowed to deleting `GET /v1/debug/scan-directory/{dirName}` only. The `/v1/run` / `/v1/graph` deletion is deferred per D-2026-04-08-029 and tracked in `work/gaps.md` under "Deferred deletion: Engine `POST /v1/run` and `POST /v1/graph`". A follow-up unit of work (candidate name `m-E19-02a-engine-runtime-route-retirement`) must land the test migration before the routes can be removed.

**Files edited:**

- `src/FlowTime.API/Program.cs` — deleted the `GET /v1/debug/scan-directory/{dirName}` handler (previously lines 434–466, ~32 lines). No other references.
- `docs/architecture/supported-surfaces.md` — updated the `POST /v1/run`, `POST /v1/graph` row from `delete m-E19-02` to `transitional / deferred (see work/gaps.md)` with an explanation of the scope narrowing.
- `work/epics/E-19-surface-alignment-and-compatibility-cleanup/m-E19-02-sim-authoring-and-runtime-boundary-cleanup.md` — rewrote AC6 text to reflect the narrowing, cite D-2026-04-08-029, and link to `work/gaps.md`. Updated the Technical Notes step 7 summary.
- `work/gaps.md` — added new section "Deferred deletion: Engine `POST /v1/run` and `POST /v1/graph`" with the 50+ call site inventory, resolution path options, and interim usage rules.
- `work/decisions.md` — added D-2026-04-08-029 recording the scope change, context, rationale, resolution path, and process learning (future inventory milestones should include a test-file sweep when an audit row implies route deletion).

**Grep guard verified zero-match after deletion:** `/v1/debug/scan-directory`.

**Grep guard intentionally NOT enforced (deferred):** `MapPost("/run"` and `MapPost("/graph"` — both still exist in `src/FlowTime.API/Program.cs` as documented transitional surfaces pending the follow-up test migration milestone.

**Preserved:** `POST /v1/run` and `POST /v1/graph` Engine routes and all their test callers — no coverage regression on Engine-side provenance/parity.
