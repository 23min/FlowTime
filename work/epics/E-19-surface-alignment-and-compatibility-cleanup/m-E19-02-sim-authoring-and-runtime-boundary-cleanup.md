# Milestone: Sim Authoring & Runtime Boundary Cleanup

**ID:** m-E19-02-sim-authoring-and-runtime-boundary-cleanup
**Epic:** [Surface Alignment & Compatibility Cleanup (E-19)](./spec.md)
**Status:** in-progress
**Branch:** `milestone/m-E19-02-sim-authoring-and-runtime-boundary-cleanup` (off `epic/E-19`)

## Goal

Execute the runtime deletions locked by [m-E19-01](./m-E19-01-supported-surface-inventory.md) A1–A6: remove stored drafts, the Sim ZIP archive layer, Engine bundle-import and dead direct-eval routes, runtime catalogs, and the Sim-only `POST /api/v1/drafts/validate` wrapper. Narrow `POST /api/v1/drafts/run` to inline-source only. When this milestone closes, Sim authoring surfaces expose only the explicitly supported paths and Engine exposes only the canonical query/operator surface over `data/runs/<runId>/`.

## Context

m-E19-01 published the supported-surface matrix in [docs/architecture/supported-surfaces.md](../../../docs/architecture/supported-surfaces.md) and locked retention/deletion decisions A1–A6 in the milestone spec. No code changed in m-E19-01 — every deletion was assigned an owning downstream milestone and a grep guard. This milestone is the first deletion pass and executes every row whose `Owning milestone` column is `m-E19-02`.

Scope boundaries inherited from m-E19-01:

- `FlowTime.Core`, `FlowTime.Generator`, `FlowTime.API`, and `FlowTime.Sim.*` are **not renamed** and their high-level responsibilities do not change in E-19. Generator stays frozen; its Path B extraction belongs to E-18 m-E18-01a.
- The canonical run directory under `data/runs/<runId>/` is unchanged.
- Analytical surfaces purified by E-16 are out of scope.
- Blazor stale-wrapper cleanup (beyond the catalog selector, which is coupled to A5) belongs to m-E19-04.
- Schema, template, example, and current-doc cleanup belong to m-E19-03.
- `FlowTime.TimeMachine` is not introduced here — that is E-18 m-E18-01a.

The default execution path for first-party UIs during and after this milestone remains `POST /api/v1/orchestration/runs` on `FlowTime.Sim.Service` per A1. Sunsetting that endpoint is an E-18 decision, not this milestone's.

## Acceptance Criteria

### AC1 — Stored drafts retired (A2)

Forward-only deletion of the stored-draft product surface.

**Delete:**

- Sim routes in [src/FlowTime.Sim.Service/Program.cs](../../../src/FlowTime.Sim.Service/Program.cs): `GET /api/v1/drafts` (line 399), `GET /api/v1/drafts/{draftId}` (line 418), `POST /api/v1/drafts` (line 444), `PUT /api/v1/drafts/{draftId}` (line 489), `DELETE /api/v1/drafts/{draftId}` (line 527).
- `StorageKind.Draft` enum value in [src/FlowTime.Contracts/Storage/StorageContracts.cs](../../../src/FlowTime.Contracts/Storage/StorageContracts.cs) and every call site that writes or reads drafts through `IStorageBackend`.
- `data/storage/drafts/` directory references in Sim service configuration and any backend code that materialises that path.
- `DraftEndpointsTests.cs` CRUD test cases in `tests/FlowTime.Sim.Tests/` (inline-source tests that survive A7 narrowing must stay).
- `DraftCreateRequest`, `DraftUpdateRequest`, and other stored-draft request/response contracts that only serve the deleted CRUD routes.

**Preserve:**

- `POST /api/v1/drafts/generate` (A2 narrowing: stays as an authoring generate-materialised-model surface, but must not resolve any `draftId`).
- `POST /api/v1/drafts/map-profile` (A2: supported profile authoring helper).

**Grep guards (must return zero matches in `src/` and `tests/` after deletion):**

- `"drafts/{draftId"`, `"drafts\"\s*,\s*async"` on CRUD handlers
- `StorageKind.Draft`
- `data/storage/drafts`

### AC2 — `/api/v1/drafts/run` narrowed to inline-source only (A1, A2)

`POST /api/v1/drafts/run` at [src/FlowTime.Sim.Service/Program.cs:675](../../../src/FlowTime.Sim.Service/Program.cs) remains a live route but only accepts `DraftSource.type == "inline"`. Any `draftId` resolution branch is removed.

- No request shape accepts `draftId` on this endpoint after the milestone.
- Inline-source tests in `DraftEndpointsTests.cs` survive and are the only tests left covering this route.
- Documentation for this endpoint (in `docs/reference/contracts.md` and elsewhere) is updated by m-E19-03 — this milestone only removes the code branch.

**Grep guard:** No `draftId` reference remains in the `/api/v1/drafts/run` handler or its request shape.

### AC3 — Sim-only `/api/v1/drafts/validate` deleted (A6)

`POST /api/v1/drafts/validate` at [src/FlowTime.Sim.Service/Program.cs:540](../../../src/FlowTime.Sim.Service/Program.cs) is removed along with its endpoint-specific tests. The library pieces that back it remain untouched (they become the tier 1/2/3 ingredients the future Time Machine composes per [D-2026-04-07-017](../../decisions.md)):

**Preserved unchanged:**

- `FlowTime.Core.Models.ModelSchemaValidator`
- `FlowTime.Core.Models.ModelValidator`
- `FlowTime.Core.Compiler.ModelCompiler`
- `FlowTime.Core.Models.ModelParser`
- `FlowTime.Sim.Core.Analysis.TemplateInvariantAnalyzer`
- `FlowTime.Sim.Core.Analysis.InvariantAnalyzer`

**Grep guard:** No `/api/v1/drafts/validate` route literal or `drafts/validate` handler remains in `src/` or `tests/`.

### AC4 — Sim-side ZIP archive layer deleted (A3)

Remove the post-hoc run-bundle archive path that writes ZIPs to `data/storage/runs/<runId>` and the `BundleRef` / `StorageRef` return values that surface them.

**Delete:**

- `StorageKind.Run` bundle ZIP writes inside `RunOrchestrationService.CreateSimulationRunAsync` (wherever that service currently calls the archive writer).
- `BundleRef` and `StorageRef` members on `RunCreateResponse` in [src/FlowTime.Contracts/TimeTravel/RunContracts.cs](../../../src/FlowTime.Contracts/TimeTravel/RunContracts.cs).
- `StorageKind.Run` enum value in [src/FlowTime.Contracts/Storage/StorageContracts.cs](../../../src/FlowTime.Contracts/Storage/StorageContracts.cs) and the `data/storage/runs/` backend write path, together with any helper that only services that writer.
- Any Sim-side helper whose only caller was the archive writer.

**Explicitly preserved:** the canonical run directory layout at `data/runs/<runId>/` (`model/`, `series/`, `run.json`). This is not a bundle and is untouched by this milestone.

**Grep guards:** No `StorageKind.Run`, `BundleRef`, `StorageRef`, or `data/storage/runs` reference remains in `src/` or `tests/` on the current surface.

### AC5 — Engine `POST /v1/runs` deleted outright (A4)

`POST /v1/runs` in [src/FlowTime.API/Endpoints/RunOrchestrationEndpoints.cs:19](../../../src/FlowTime.API/Endpoints/RunOrchestrationEndpoints.cs) is removed entirely. No 410-style rejection stub remains. The read endpoints `GET /v1/runs` (line 20) and `GET /v1/runs/{runId}` (line 21) stay — they are the canonical run discovery/detail contract.

**Delete:**

- `group.MapPost("/runs", HandleCreateRunAsync)` at line 19 and the `HandleCreateRunAsync` handler itself.
- `bundlePath`, `bundleArchiveBase64`, and `BundleRef` resolution branches (wherever they live once bundled into the removed handler).
- `RunImportRequest` fields `BundlePath`, `BundleArchiveBase64`, and the `BundleRef` type on import contracts in [src/FlowTime.Contracts/TimeTravel/RunContracts.cs](../../../src/FlowTime.Contracts/TimeTravel/RunContracts.cs).
- `ExtractArchiveAsync` and any support helpers that only served bundle import.
- Bundle-import test cases in `RunOrchestrationTests.cs` (forward-only deletion — do not keep them as "preserved for future import redesign").

**Preserve:** `GET /v1/runs` and `GET /v1/runs/{runId}` — they are the canonical run query surface consumed by the Svelte UI and operator workflows.

**Grep guards:** `MapPost("/runs", HandleCreateRunAsync)`, `BundlePath`, `BundleArchiveBase64`, and `BundleRef` return zero matches in `src/` and `tests/` on the current API surface.

### AC6 — Engine debug / direct-eval routes deleted

Three Engine routes from [src/FlowTime.API/Program.cs](../../../src/FlowTime.API/Program.cs) are listed as `delete` in the supported-surfaces matrix with owning milestone `m-E19-02`:

- `GET /v1/debug/scan-directory/{dirName}` — internal debug route with no first-party product caller.
- `POST /v1/run` — ad hoc direct-YAML evaluation surface not used by current first-party UIs.
- `POST /v1/graph` — ad hoc graph surface not used by current first-party UIs.

**Delete** each route, its handler, and any tests that exist only to exercise them.

**Grep guards:** No `"/v1/debug/scan-directory"`, `MapPost("/run"`, or `MapPost("/graph"` literal remains in runtime code.

### AC7 — Catalogs retired entirely (A5)

Catalog surfaces are zombie residue with no supported first-party caller. Delete them atomically across runtime and the Blazor catalog selector (the one UI site coupled to this server deletion).

**Delete:**

- Sim routes: `GET /api/v1/catalogs`, `GET /api/v1/catalogs/{id}`, `PUT /api/v1/catalogs/{id}`, `POST /api/v1/catalogs/validate` in [src/FlowTime.Sim.Service/Program.cs](../../../src/FlowTime.Sim.Service/Program.cs).
- `CatalogService`, `ICatalogService`, and any mock catalog service implementation in Sim.
- `CatalogPicker.razor` in the Blazor UI and any Svelte catalog selector if one exists.
- `CatalogId = "default"` placeholder callers (including the hardcoded value in `TemplateRunner.razor`).
- The `catalogId` field on any request/response DTO where it appears.
- `data/catalogs/` directory references.
- Catalog-only tests.

**Grep guards:** `/api/v1/catalogs`, `CatalogService`, `ICatalogService`, `CatalogPicker`, and the literal `CatalogId = "default"` return zero matches in `src/` and `tests/` on the current surface.

### AC8 — Public contracts cleanup consolidated

All public contract changes forced by AC1–AC7 above land in [src/FlowTime.Contracts/](../../../src/FlowTime.Contracts/) in a single consistent pass:

- `RunImportRequest.BundlePath`, `RunImportRequest.BundleArchiveBase64`, `RunCreateResponse.BundleRef`, and the `BundleRef` / `StorageRef` types removed.
- `StorageKind.Draft` and `StorageKind.Run` enum values removed from [src/FlowTime.Contracts/Storage/StorageContracts.cs](../../../src/FlowTime.Contracts/Storage/StorageContracts.cs). Any storage-kind switch statements lose their draft/run cases.
- Stored-draft request/response contracts (`DraftCreateRequest`, `DraftUpdateRequest`) removed unless a surviving inline-only route still needs them.

`StorageBackendOptions`, `IStorageBackend`, `StorageWriteRequest`, `StorageWriteResult`, `StorageReadResult`, `StorageListRequest`, and `StorageItemSummary` remain on the public surface — they still serve surviving storage needs (for example, series storage referenced in the supported-surfaces matrix). This milestone removes only the draft/run kinds, not the underlying storage abstraction.

### AC9 — Build, tests, and grep guards green

- `dotnet build FlowTime.sln` is green with no new warnings introduced by this milestone.
- `dotnet test FlowTime.sln` is green across all test projects (deleted tests for deleted code are acceptable; failing tests or reduced coverage for surviving code is not).
- Every grep guard from AC1–AC7 is asserted by a simple repo-root script or CI check that `rg` returns zero matches in `src/` and `tests/` for the deleted symbols. The check can be a single shell script runnable locally; it does not need to become a full CI pipeline step in this milestone but it must exist and be documented.

### AC10 — Status surfaces reconciled at wrap time

At milestone wrap:

- [work/epics/E-19-surface-alignment-and-compatibility-cleanup/spec.md](./spec.md) milestone table marks m-E19-02 complete and m-E19-03 next.
- [ROADMAP.md](../../../ROADMAP.md) and [work/epics/epic-roadmap.md](../../epic-roadmap.md) reflect the same status.
- [CLAUDE.md](../../../CLAUDE.md) Current Work section names m-E19-02 complete and m-E19-03 next.
- The tracking doc [m-E19-02-sim-authoring-and-runtime-boundary-cleanup-tracking.md](./m-E19-02-sim-authoring-and-runtime-boundary-cleanup-tracking.md) records every AC checked, the final test count, and the grep guard results.
- `work/decisions.md` does **not** need new entries — this milestone executes decisions A1–A6 already recorded under D-2026-04-07-023 through D-2026-04-07-028. If an implementation judgment call surfaces that m-E19-01 did not anticipate, it is logged in `work/gaps.md` or as a new D-entry at wrap time.

## Technical Notes

### Recommended sequence

Each step should leave the build green and the test suite passing before the next step begins. This is a forward-only cleanup — no compatibility shims, no temporary wrappers.

1. **Catalogs (AC7).** Fully self-contained: routes, services, Blazor picker, placeholder callers, `data/catalogs/`, catalog-only tests. Lowest coupling, highest confidence.
2. **`/api/v1/drafts/validate` (AC3).** Trivial — unused route, clean deletion, library pieces explicitly preserved.
3. **Stored drafts CRUD (AC1).** Delete routes, `StorageKind.Draft`, `data/storage/drafts/`, CRUD tests.
4. **Narrow `/api/v1/drafts/run` (AC2).** Strip `draftId` branches; keep inline-source path.
5. **Sim ZIP archive layer (AC4).** Remove `StorageKind.Run` writes in `RunOrchestrationService`, drop `BundleRef`/`StorageRef` return values, delete `data/storage/runs/` backend writer, remove `StorageKind.Run` from the enum.
6. **Engine `POST /v1/runs` + bundle-import (AC5).** Delete handler, remove bundle-import fields from `RunImportRequest`, delete bundle-import tests, delete the route registration.
7. **Engine debug / direct-eval routes (AC6).** Delete the three debug/eval routes and any tests exercising only them.
8. **Public contracts finalisation (AC8).** Sanity pass to confirm every deleted runtime symbol is also gone from `FlowTime.Contracts`.
9. **Grep guards + build/test finalisation (AC9).** Run every grep guard, fix any straggler references, rerun the full test suite.
10. **Wrap (AC10).** Tracking doc, status surfaces, `CLAUDE.md` current work.

### Supporting data

- Sim service endpoints are defined inline in [src/FlowTime.Sim.Service/Program.cs](../../../src/FlowTime.Sim.Service/Program.cs) (75 catalog/drafts references counted during planning).
- Engine run orchestration is isolated to [src/FlowTime.API/Endpoints/RunOrchestrationEndpoints.cs](../../../src/FlowTime.API/Endpoints/RunOrchestrationEndpoints.cs).
- Engine debug/eval routes are in [src/FlowTime.API/Program.cs](../../../src/FlowTime.API/Program.cs).
- Storage abstractions are in [src/FlowTime.Contracts/Storage/](../../../src/FlowTime.Contracts/Storage/).
- Run-contract DTOs that need trimming are in [src/FlowTime.Contracts/TimeTravel/RunContracts.cs](../../../src/FlowTime.Contracts/TimeTravel/RunContracts.cs).

### Test strategy

Forward-only deletion, not migration:

- Tests that exist only to exercise deleted routes are deleted alongside the routes.
- Tests covering surviving inline paths (`/api/v1/drafts/run` with `inline` source, `/api/v1/drafts/generate`, `/api/v1/drafts/map-profile`, `/api/v1/orchestration/runs`, Engine `GET /v1/runs*`) must stay green.
- No new unit tests are required by this milestone unless a deletion surfaces a regression that existing coverage did not catch. In that case, the regression test is added alongside the fix.
- Grep guards (AC9) are the load-bearing regression check for this milestone. Every deleted symbol is asserted absent.

### Do NOT touch

- `FlowTime.Core` — no changes. Library pieces preserved for A6 are explicitly unchanged.
- `FlowTime.Generator` — frozen in E-19; any Generator work belongs to E-18 m-E18-01a.
- Canonical run directory layout at `data/runs/<runId>/` — unchanged.
- `/api/v1/orchestration/runs` — supported per A1. No changes.
- `/api/v1/templates/*` authoring surface — supported. No changes.
- `/api/v1/drafts/generate` and `/api/v1/drafts/map-profile` — supported authoring surfaces. No changes beyond removing `draftId` resolution.
- `/api/v1/series/*`, `/api/v1/profiles/*`, `/api/v1/models/*` — supported Sim authoring/data-intake surfaces. No changes.
- Blazor stale-wrapper cleanup beyond `CatalogPicker.razor` — that is m-E19-04's job.
- Schema files under `docs/schemas/` — m-E19-03 owns any deprecated-schema removal.
- Template files under `templates/` — m-E19-03 owns any deprecated-template removal.
- Example files under `examples/` — m-E19-03 owns schema-compatibility example retirement.

## Out of Scope

- Introducing or referencing `FlowTime.TimeMachine`. That component is new in E-18 m-E18-01a and does not exist yet.
- Any Path B extraction of `FlowTime.Generator`. Generator is frozen.
- Schema, template, example, or docs cleanup (m-E19-03 owns those).
- Blazor stale compatibility wrappers outside the catalog picker (m-E19-04).
- Replacing the deleted validation endpoint with a tiered validation API on Sim — that is explicitly an E-18 m-E18-01b deliverable per A6.
- Reintroducing any deleted surface as a "temporary compatibility shim."
- Refactoring `RunOrchestrationService` or `IStorageBackend` beyond removing the deleted code paths.
- Performance, observability, or error-handling improvements unrelated to deletion.
- Introducing new tests for surviving endpoints beyond what already exists.

## Guards / DO NOT

- **DO NOT** preserve a 410-style rejection stub or advisory tombstone for any deleted route. Forward-only deletion per shared framing in [m-E19-01 § Shared Framing](./m-E19-01-supported-surface-inventory.md#shared-framing).
- **DO NOT** design or stub anything under `FlowTime.TimeMachine` or any `Headless` namespace. The Time Machine is E-18 m-E18-01a.
- **DO NOT** extend the `POST /api/v1/orchestration/runs` surface. It stays as-is; sunsetting is an E-18 decision.
- **DO NOT** add new compatibility wrappers, feature flags, or configuration toggles to keep deleted behaviour reachable in any environment.
- **DO NOT** widen the scope into schema/template/example cleanup. Those are m-E19-03.
- **DO NOT** touch the canonical run directory layout at `data/runs/<runId>/`. The bundle archive layer is separate.
- **DO NOT** re-home `TemplateInvariantAnalyzer` into `FlowTime.Core` in this milestone. That is an E-18 m-E18-01b concern.
- **DO NOT** leave partially deleted symbols behind. Every grep guard must pass at wrap time.

## Dependencies

- [m-E19-01 Supported Surface Inventory, Boundary ADR & Exit Criteria](./m-E19-01-supported-surface-inventory.md) — locks A1–A6 decisions and the boundary ADR this milestone executes against.
- [docs/architecture/supported-surfaces.md](../../../docs/architecture/supported-surfaces.md) — authoritative row-by-row ownership for deletions.
- [docs/architecture/template-draft-model-run-bundle-boundary.md](../../../docs/architecture/template-draft-model-run-bundle-boundary.md) — current/transitional/target diagrams that deletions must not contradict.

## References

- [E-19 epic spec](./spec.md)
- [m-E19-01 spec](./m-E19-01-supported-surface-inventory.md)
- [work/decisions.md](../../decisions.md) — D-2026-04-07-017 (A6), D-2026-04-07-022 through D-2026-04-07-028 (shared framing and A1–A5)
- [E-18 epic spec](../E-18-headless-pipeline-and-optimization/spec.md) — downstream dependency for validation replacement
