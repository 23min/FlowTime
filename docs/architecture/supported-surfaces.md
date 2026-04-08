# Supported Surfaces

**Status:** active baseline for E-19 follow-on cleanup
**Related epic:** [E-19](../../work/epics/E-19-surface-alignment-and-compatibility-cleanup/spec.md)
**Related milestone:** [m-E19-01-supported-surface-inventory](../../work/epics/E-19-surface-alignment-and-compatibility-cleanup/m-E19-01-supported-surface-inventory.md)
**Related ADR:** [template-draft-model-run-bundle-boundary.md](./template-draft-model-run-bundle-boundary.md)

This document is the authoritative supported-surface matrix for current FlowTime first-party surfaces. It records what is supported now, what is transitional, what is scheduled for deletion or archival, and which downstream milestone owns each cleanup.

Rows are grouped when sibling elements share one decision, one owner, and one grep guard. The raw exhaustive sweeps that back those grouped rows appear after the matrix.

## Shared Framing

- No project renames happen in E-19. `FlowTime.Core`, `FlowTime.Generator`, `FlowTime.API`, and `FlowTime.Sim.*` keep their names while E-19 narrows current surfaces.
- `FlowTime.Core` remains the pure evaluation library and authoritative validation/analyser home. No HTTP, orchestration, storage, or client-specific logic gets pushed into Core.
- `FlowTime.Generator` stays frozen during E-19 and is extracted into `FlowTime.TimeMachine` and deleted in E-18 Path B.
- `FlowTime.API` remains the query and operator surface over canonical run artifacts. It does not become the template-driven execution host, and obsolete API write endpoints are deleted outright rather than preserved as rejection stubs.
- `FlowTime.Sim.Service` owns template authoring and template-facing metadata. It hosts execution only as a transitional first-party UI bridge until the Time Machine ships.
- `FlowTime.TimeMachine` is owned by E-18. It is the future client-agnostic execution component for compile, tiered validation, evaluate, reevaluate, parameter override, and artifact write.
- Validation is a first-class client-agnostic operation. Sim UI, Blazor UI, Svelte UI, MCP servers, external AI agents, tests, and CI are equal callers of Time Machine validation and execution operations.
- The canonical run directory under `data/runs/<runId>/` and the portable canonical bundle are distinct artifacts with different purposes. Runs are the in-place debug/query truth. Bundles are the portable interchange format.
- When the Time Machine ships, Sim orchestration endpoints are deleted by default. Any temporary facade requires a documented technical migration reason and explicit removal criteria.

## Blazor / Svelte Support Policy

- Blazor remains a supported first-party UI for debugging, operator workflows, and as a plan-B to Svelte.
- Feature parity between Blazor and Svelte is not a goal. Each UI only carries the features it actually implements.
- Both UIs consume current Engine and Sim contracts. Shared contract changes must keep both UIs compiling and functional.
- Blazor proceeds with its own planned deprecations. Svelte does not need to inherit features Blazor is removing.
- Neither UI should carry stale compatibility wrappers, duplicate endpoint probes, or local metrics/state reconstruction where canonical endpoints already exist.
- Neither UI should keep deprecated schema shapes or demo-generation residue on the active shared contract surface.
- Blazor-specific debugging and operator workflows remain supported as long as they call current contracts.

## Decision Matrix

Decision references: [Shared framing](../../work/epics/E-19-surface-alignment-and-compatibility-cleanup/m-E19-01-supported-surface-inventory.md#shared-framing), [A1](../../work/epics/E-19-surface-alignment-and-compatibility-cleanup/m-E19-01-supported-surface-inventory.md#a1--sim-orchestration-endpoints), [A2](../../work/epics/E-19-surface-alignment-and-compatibility-cleanup/m-E19-01-supported-surface-inventory.md#a2--stored-drafts), [A3](../../work/epics/E-19-surface-alignment-and-compatibility-cleanup/m-E19-01-supported-surface-inventory.md#a3--datastorageruns--bundleref), [A4](../../work/epics/E-19-surface-alignment-and-compatibility-cleanup/m-E19-01-supported-surface-inventory.md#a4--engine-post-v1runs-bundle-import), [A5](../../work/epics/E-19-surface-alignment-and-compatibility-cleanup/m-E19-01-supported-surface-inventory.md#a5--catalogs), [A6](../../work/epics/E-19-surface-alignment-and-compatibility-cleanup/m-E19-01-supported-surface-inventory.md#a6--validation-as-a-first-class-client-agnostic-operation).

| Surface | Element | Current status | Decision | Target state | Owning milestone | Grep guard |
|---------|---------|----------------|----------|--------------|------------------|------------|
| Engine API route | `/healthz`, `/v1/healthz` in [src/FlowTime.API/Program.cs](../../src/FlowTime.API/Program.cs) | Live health/service-identity routes used by current UIs and operator checks | supported | Keep as the current Engine health contract | m-E19-01 | n/a |
| Engine API route | `POST /v1/runs` in [src/FlowTime.API/Endpoints/RunOrchestrationEndpoints.cs](../../src/FlowTime.API/Endpoints/RunOrchestrationEndpoints.cs) | Live legacy write route still carries bundle-import branches that only tests exercise and currently rejects template-driven calls with a 410 | delete ([A4]) | Delete `POST /v1/runs` entirely. No rejection stub or advisory tombstone remains on the current API surface | m-E19-02 | No `MapPost("/runs", HandleCreateRunAsync)`, `BundlePath`, `BundleArchiveBase64`, or `BundleRef` remain on the current API surface |
| Engine API route | `GET /v1/runs`, `GET /v1/runs/{runId}` in [src/FlowTime.API/Endpoints/RunOrchestrationEndpoints.cs](../../src/FlowTime.API/Endpoints/RunOrchestrationEndpoints.cs) | Live run discovery/detail surface used by current Svelte UI and operator workflows | supported | Keep as the current run list/detail contract | m-E19-01 | n/a |
| Engine API route | `GET /v1/runs/{runId}/graph`, `/metrics`, `/state`, `/state_window`, `/index`, `/series/{seriesId}` in [src/FlowTime.API/Program.cs](../../src/FlowTime.API/Program.cs) | Canonical read/query surface over `data/runs/<runId>/` | supported | Keep as the primary Engine read/query contract | m-E19-01 | n/a |
| Engine API route | `POST /v1/runs/{runId}/export`, `GET /v1/runs/{runId}/export/{format}` in [src/FlowTime.API/Program.cs](../../src/FlowTime.API/Program.cs) | Live operator/export surface over canonical run artifacts | supported | Keep as the export surface over canonical runs | m-E19-01 | n/a |
| Engine API route | `POST /v1/telemetry/captures` in [src/FlowTime.API/Endpoints/TelemetryCaptureEndpoints.cs](../../src/FlowTime.API/Endpoints/TelemetryCaptureEndpoints.cs) | Current canonical telemetry-capture API; later rewired to Time Machine internals per D-2026-04-07-020 | supported | Keep public contract stable while E-18 moves the implementation | E-18 m-E18-01a | n/a |
| Engine API route | `POST /v1/templates/refresh` in [src/FlowTime.API/Program.cs](../../src/FlowTime.API/Program.cs) | Live operational template-cache refresh route documented for current services | supported | Keep as the current refresh control surface | m-E19-01 | n/a |
| Engine API route | Artifact registry routes in [src/FlowTime.API/Program.cs](../../src/FlowTime.API/Program.cs): `POST /v1/artifacts/index`, `GET /v1/artifacts`, `GET /v1/artifacts/{id}`, `GET /v1/artifacts/{id}/relationships`, `GET /v1/artifacts/{id}/download`, `GET /v1/artifacts/{id}/files/{fileName}`, `POST /v1/artifacts/bulk-delete`, `POST /v1/artifacts/archive` | Live Svelte/operator/debug surface for artifact browsing and maintenance | supported | Keep as the operator/debug artifact surface | m-E19-01 | n/a |
| Engine API route | `POST /v1/diagnostics/hover` in [src/FlowTime.API/Program.cs](../../src/FlowTime.API/Program.cs) | Live UI-performance diagnostics surface | supported | Keep as the current diagnostics capture endpoint | m-E19-01 | n/a |
| Engine API route | `GET /v1/debug/scan-directory/{dirName}` in [src/FlowTime.API/Program.cs](../../src/FlowTime.API/Program.cs) | Internal debug-only route with no first-party product caller | delete | Remove from the live API surface; keep equivalent investigation logic only as local developer tooling if still needed | m-E19-02 | No `/v1/debug/scan-directory/` route remains in runtime code or current docs |
| Engine API route | `POST /v1/run`, `POST /v1/graph` in [src/FlowTime.API/Program.cs](../../src/FlowTime.API/Program.cs) | Ad hoc direct-YAML evaluation and graph endpoints used by 50+ Provenance/Parity/Legacy test call sites as the primary run-creation mechanism (first-party UIs do not call them, but test infrastructure depends on them) | transitional | Keep as a test-infrastructure surface until the Provenance/Parity/Legacy tests are migrated to an alternative run-creation path; deletion deferred from m-E19-02 after implementation-time discovery that the m-E19-01 audit underweighted test-infrastructure coupling | deferred (see `work/gaps.md`) | n/a |
| Sim route | `/healthz`, `/v1/healthz` in [src/FlowTime.Sim.Service/Program.cs](../../src/FlowTime.Sim.Service/Program.cs) | Live health/service-identity routes used by Blazor and Svelte | supported | Keep as the current Sim health contract | m-E19-01 | n/a |
| Sim route | Template discovery and metadata routes in [src/FlowTime.Sim.Service/Program.cs](../../src/FlowTime.Sim.Service/Program.cs): `GET /api/v1/templates`, `GET /api/v1/templates/{id}`, `GET /api/v1/templates/{id}/source`, `GET /api/v1/templates/categories`, `POST /api/v1/templates/refresh` | Current authoring metadata surface for both first-party UIs and docs | supported | Keep as Sim-owned template authoring metadata | m-E19-01 | n/a |
| Sim route | `POST /api/v1/templates/{id}/generate` in [src/FlowTime.Sim.Service/Program.cs](../../src/FlowTime.Sim.Service/Program.cs) | Current template authoring/materialisation surface used by Blazor and authoring docs | supported | Keep as the explicit generate-materialised-model authoring surface | m-E19-01 | n/a |
| Sim route | `POST /api/v1/orchestration/runs` in [src/FlowTime.Sim.Service/Extensions/RunOrchestrationEndpointExtensions.cs](../../src/FlowTime.Sim.Service/Extensions/RunOrchestrationEndpointExtensions.cs) | Active first-party template-driven run path for Blazor and Svelte | transitional ([A1]) | Keep as a first-party UI-only bridge until the Time Machine ships, then delete by default | E-18 m-E18-01a | n/a |
| Sim route | Draft CRUD routes in [src/FlowTime.Sim.Service/Program.cs](../../src/FlowTime.Sim.Service/Program.cs): `GET /api/v1/drafts`, `GET /api/v1/drafts/{draftId}`, `POST /api/v1/drafts`, `PUT /api/v1/drafts/{draftId}`, `DELETE /api/v1/drafts/{draftId}` | No current first-party UI calls them; only draft-endpoint tests exercise them | delete ([A2]) | Remove stored drafts as a product surface | m-E19-02 | No draft CRUD handlers, `StorageKind.Draft`, or `data/storage/drafts/` runtime paths remain; only inline `/api/v1/drafts/run` may survive until E-18 |
| Sim route | `POST /api/v1/drafts/validate` in [src/FlowTime.Sim.Service/Program.cs](../../src/FlowTime.Sim.Service/Program.cs) | Live but unused HTTP wrapper around heavier compile+evaluate+analyse logic | delete ([A6]) | Remove the Sim-only endpoint and preserve the underlying library pieces for Time Machine tiered validation | m-E19-02 | No `/api/v1/drafts/validate` route remains; `ModelSchemaValidator`, `ModelValidator`, `ModelCompiler`, `ModelParser`, `TemplateInvariantAnalyzer`, and `InvariantAnalyzer` remain |
| Sim route | `POST /api/v1/drafts/generate` in [src/FlowTime.Sim.Service/Program.cs](../../src/FlowTime.Sim.Service/Program.cs) | Current authoring route for generating a model from draft-style source | supported | Keep as a generate-materialised-model authoring surface while Sim owns template authoring | m-E19-01 | n/a |
| Sim route | `POST /api/v1/drafts/run` in [src/FlowTime.Sim.Service/Program.cs](../../src/FlowTime.Sim.Service/Program.cs) | Live narrower inline-YAML “run this now” surface | transitional ([A1], [A2]) | Keep only the inline-source path, remove any `draftId` dependency, and delete by default when the Time Machine ships | m-E19-02, E-18 m-E18-01a | n/a |
| Sim route | `POST /api/v1/series/ingest`, `POST /api/v1/series/summarize` in [src/FlowTime.Sim.Service/Program.cs](../../src/FlowTime.Sim.Service/Program.cs) | Live profile-fitting/data-intake authoring surface | supported | Keep as current Sim-side data intake until a future explicit replacement is designed | m-E19-01 | n/a |
| Sim route | `POST /api/v1/profiles/fit`, `POST /api/v1/profiles/preview`, `POST /api/v1/drafts/map-profile` in [src/FlowTime.Sim.Service/Program.cs](../../src/FlowTime.Sim.Service/Program.cs) | Live authoring/profile workflow surface | supported | Keep as current Sim authoring support | m-E19-01 | n/a |
| Sim route | `GET /api/v1/models`, `GET /api/v1/models/{templateId}` in [src/FlowTime.Sim.Service/Program.cs](../../src/FlowTime.Sim.Service/Program.cs) | Current stored-model preview/history surface used by authoring flows and debugging | supported | Keep as current Sim model-preview surface unless a later milestone explicitly replaces it | m-E19-01 | n/a |
| Sim route | Catalog routes in [src/FlowTime.Sim.Service/Program.cs](../../src/FlowTime.Sim.Service/Program.cs): `GET /api/v1/catalogs`, `GET /api/v1/catalogs/{id}`, `PUT /api/v1/catalogs/{id}`, `POST /api/v1/catalogs/validate` | Zombie residue with mock/default behavior and no supported first-party caller | delete ([A5]) | Remove catalogs from live runtime and UI surfaces | m-E19-02 | No live `/api/v1/catalogs` routes, `CatalogService`, `ICatalogService`, `CatalogPicker`, or `catalogId = "default"` placeholders remain |
| Blazor HTTP call site | Supported Sim client calls in [src/FlowTime.UI/Services/FlowTimeSimApiClient.cs](../../src/FlowTime.UI/Services/FlowTimeSimApiClient.cs): `HealthAsync`, `GetDetailedHealthAsync`, `GetTemplatesAsync`, `GetTemplateAsync`, `GenerateModelAsync`, `CreateRunAsync` | Current first-party Blazor surface aligned to live Sim contracts | supported | Keep as Blazor’s supported Sim client surface | m-E19-04 | n/a |
| Blazor HTTP call site | `RunAsync` in [src/FlowTime.UI/Services/FlowTimeSimApiClient.cs](../../src/FlowTime.UI/Services/FlowTimeSimApiClient.cs) | Stale wrapper targeting removed `/api/v1/run`; file already marks it broken | delete | Remove the dead wrapper and any caller assumptions built on the removed route | m-E19-04 | No `RunAsync` wrapper or Sim `/api/v1/run` client literal remains in Blazor client code |
| Blazor HTTP call site | `GetIndexAsync`, `GetSeriesAsync` in [src/FlowTime.UI/Services/FlowTimeSimApiClient.cs](../../src/FlowTime.UI/Services/FlowTimeSimApiClient.cs) | Stale wrappers targeting missing Sim run-query routes; file already marks them broken | delete | Remove stale Sim-query wrappers and keep run queries on Engine API only | m-E19-04 | No Sim `/api/v1/runs/{runId}/index` or `/series/` client literals remain in Blazor client code |
| Svelte HTTP call site | Sim client in [ui/src/lib/api/sim.ts](../../ui/src/lib/api/sim.ts) | Current first-party Svelte surface for health, template discovery, categories, and orchestration | supported | Keep as the current Svelte-to-Sim client surface | m-E19-04 | n/a |
| Svelte HTTP call site | Engine client in [ui/src/lib/api/flowtime.ts](../../ui/src/lib/api/flowtime.ts) | Current first-party Svelte surface for run queries and artifacts | supported | Keep as the current Svelte-to-Engine client surface | m-E19-04 | n/a |
| Public contracts | Run/orchestration family in [src/FlowTime.Contracts/TimeTravel/RunContracts.cs](../../src/FlowTime.Contracts/TimeTravel/RunContracts.cs) | Public request/response surface still exposes bundle-import and bundle-ref residue alongside live run/telemetry shapes | transitional ([A3], [A4]) | Remove `RunImportRequest` bundle-import fields and `RunCreateResponse.BundleRef`, keep current run/summary/telemetry contracts | m-E19-02 | No public `BundlePath`, `BundleArchiveBase64`, or `BundleRef` fields remain on run-creation/import contracts |
| Public contracts | Storage family in [src/FlowTime.Contracts/Storage/StorageContracts.cs](../../src/FlowTime.Contracts/Storage/StorageContracts.cs) and [src/FlowTime.Contracts/Storage/StorageBackend.cs](../../src/FlowTime.Contracts/Storage/StorageBackend.cs) | Public storage abstractions still encode draft/run archive concepts that E-19 is retiring | transitional ([A2], [A3], [A5]) | Remove draft/run archive product concepts from public storage contracts; keep only live model/series storage shapes if they remain needed | m-E19-02 | No public `StorageKind.Draft` or `StorageKind.Run` contract usage remains on current surfaces |
| Public contracts | Graph, state, and metrics families in [src/FlowTime.Contracts/TimeTravel/GraphContracts.cs](../../src/FlowTime.Contracts/TimeTravel/GraphContracts.cs), [src/FlowTime.Contracts/TimeTravel/StateContracts.cs](../../src/FlowTime.Contracts/TimeTravel/StateContracts.cs), and [src/FlowTime.Contracts/TimeTravel/MetricsContracts.cs](../../src/FlowTime.Contracts/TimeTravel/MetricsContracts.cs) | Current canonical Engine read/query contracts | supported | Keep as the current analytical read/query surface purified by E-16 | m-E19-01 | n/a |
| Public contracts | Authored model DTO family in [src/FlowTime.Contracts/Dtos/ModelDtos.cs](../../src/FlowTime.Contracts/Dtos/ModelDtos.cs) | Current authored model/template expansion contract family | supported | Keep as current authored model DTO surface | m-E19-01 | n/a |
| Public contracts | Artifact registry family in [src/FlowTime.Contracts/Services/ArtifactModels.cs](../../src/FlowTime.Contracts/Services/ArtifactModels.cs) and [src/FlowTime.Contracts/Services/IArtifactRegistry.cs](../../src/FlowTime.Contracts/Services/IArtifactRegistry.cs) | Current operator/debug artifact surface used by Svelte and API | supported | Keep as the current artifact registry contract family | m-E19-01 | n/a |
| Public contracts | Descriptor/support types in [src/FlowTime.Contracts/TimeTravel/DispatchScheduleDescriptor.cs](../../src/FlowTime.Contracts/TimeTravel/DispatchScheduleDescriptor.cs) and [src/FlowTime.Contracts/TimeTravel/QueueLatencyStatusDescriptor.cs](../../src/FlowTime.Contracts/TimeTravel/QueueLatencyStatusDescriptor.cs) | Current supporting descriptor types on live Engine responses | supported | Keep as current support descriptors | m-E19-01 | n/a |
| Schema | Runtime/query schemas in [docs/schemas/manifest.schema.json](../schemas/manifest.schema.json), [docs/schemas/run.schema.json](../schemas/run.schema.json), [docs/schemas/series-index.schema.json](../schemas/series-index.schema.json), [docs/schemas/telemetry-manifest.schema.json](../schemas/telemetry-manifest.schema.json), and [docs/schemas/time-travel-state.schema.json](../schemas/time-travel-state.schema.json) | Current canonical schema surface for runtime artifacts and query responses | supported | Keep as the active runtime schema set | m-E19-03 | n/a |
| Schema | Authoring schemas in [docs/schemas/template.schema.json](../schemas/template.schema.json) and [docs/schemas/model.schema.yaml](../schemas/model.schema.yaml) | Current authoring/model schema surface | supported | Keep as the active authoring schema set | m-E19-03 | n/a |
| Templates | Current repo-backed template set under [templates/](../../templates/) | Current authored template source surface for first-party authoring/orchestration | supported | Keep all current YAML templates on the active template surface | m-E19-03 | n/a |
| Examples | Active example set under [examples/](../../examples/) excluding schema-compatibility fixtures | Current user-facing example surface for modeling, PMF, and HTTP-demo data | supported | Keep active examples that reflect current contracts | m-E19-03 | n/a |
| Examples | [examples/archive/test-old-schema.yaml](../../examples/archive/test-old-schema.yaml), [examples/archive/test-no-schema.yaml](../../examples/archive/test-no-schema.yaml), [examples/archive/test-new-schema.yaml](../../examples/archive/test-new-schema.yaml) | Compatibility examples whose purpose is schema-transition coverage rather than current user guidance | archived (m-E19-03) | Moved off the active current-example surface into `examples/archive/` | m-E19-03 | No schema-migration compatibility examples remain under the active `examples/` surface |
| Current docs | Engine/query docs in [docs/reference/contracts.md](../reference/contracts.md), [docs/reference/engine-capabilities.md](../reference/engine-capabilities.md), [docs/reference/data-formats.md](../reference/data-formats.md), [docs/operations/telemetry-capture-guide.md](../operations/telemetry-capture-guide.md), [docs/guides/UI.md](../guides/UI.md), [docs/ui/api-integration.md](../ui/api-integration.md), [docs/ui/time-travel-visualizations.md](../ui/time-travel-visualizations.md), and [docs/architecture/run-provenance.md](./run-provenance.md) | Current docs that describe live Engine/API query or operator contracts | supported | Keep as the current contract documentation set | m-E19-03 | n/a |
| Current docs | Template/schema docs in [docs/templates/template-authoring.md](../templates/template-authoring.md), [docs/templates/template-testing.md](../templates/template-testing.md), [docs/templates/profiles.md](../templates/profiles.md), [docs/schemas/template-schema.md](../schemas/template-schema.md), and [docs/schemas/model.schema.md](../schemas/model.schema.md) | Current docs that describe live template and schema authoring surfaces | supported | Keep as current authoring/schema documentation | m-E19-03 | n/a |
| Current docs | Boundary docs in [docs/architecture/template-draft-model-run-bundle-boundary.md](./template-draft-model-run-bundle-boundary.md) and [docs/architecture/supported-surfaces.md](./supported-surfaces.md) | Current architectural truth for terminology, ownership, and support policy | supported | Keep as the architectural baseline cited by later E-19 and E-18 work | m-E19-01 | n/a |
| Current docs | Broad overview docs in [docs/flowtime.md](../flowtime.md) and [docs/flowtime-v2.md](../flowtime-v2.md) | Current overview docs still used as product-level reference material | supported | Keep, but terminology must stay aligned to current E-18 Time Machine naming and E-19 surface policy | m-E19-03 | n/a |
| Current docs | [docs/archive/ui/template-integration-spec.md](../archive/ui/template-integration-spec.md) | Stale pre-v1 UI spec, archived by m-E19-03 | archived (m-E19-03) | Moved off the active `docs/ui/` surface into `docs/archive/ui/` | m-E19-03 | No current docs reference the pre-v1 template routes |

## Explicit Open Questions

None currently. Unclear items discovered during the sweep were decided directly in the matrix above so downstream milestones inherit fixed ownership rather than another review pass.

## Raw Sweep Appendices

Historical, release-note, research, and archive documents are intentionally excluded from the current-surface matrix unless they are still describing a live contract on the current surface.

### Engine API Route Sweep

- [src/FlowTime.API/Program.cs](../../src/FlowTime.API/Program.cs): `GET /healthz`
- [src/FlowTime.API/Program.cs](../../src/FlowTime.API/Program.cs): `GET /v1/healthz`
- [src/FlowTime.API/Program.cs](../../src/FlowTime.API/Program.cs): `POST /v1/templates/refresh`
- [src/FlowTime.API/Program.cs](../../src/FlowTime.API/Program.cs): `POST /v1/artifacts/index`
- [src/FlowTime.API/Program.cs](../../src/FlowTime.API/Program.cs): `GET /v1/artifacts`
- [src/FlowTime.API/Program.cs](../../src/FlowTime.API/Program.cs): `POST /v1/diagnostics/hover`
- [src/FlowTime.API/Program.cs](../../src/FlowTime.API/Program.cs): `GET /v1/artifacts/{id}/relationships`
- [src/FlowTime.API/Program.cs](../../src/FlowTime.API/Program.cs): `GET /v1/artifacts/{id}`
- [src/FlowTime.API/Program.cs](../../src/FlowTime.API/Program.cs): `GET /v1/artifacts/{id}/download`
- [src/FlowTime.API/Program.cs](../../src/FlowTime.API/Program.cs): `GET /v1/artifacts/{id}/files/{fileName}`
- [src/FlowTime.API/Program.cs](../../src/FlowTime.API/Program.cs): `GET /v1/debug/scan-directory/{dirName}`
- [src/FlowTime.API/Program.cs](../../src/FlowTime.API/Program.cs): `POST /v1/artifacts/bulk-delete`
- [src/FlowTime.API/Program.cs](../../src/FlowTime.API/Program.cs): `POST /v1/artifacts/archive`
- [src/FlowTime.API/Program.cs](../../src/FlowTime.API/Program.cs): `POST /v1/run`
- [src/FlowTime.API/Program.cs](../../src/FlowTime.API/Program.cs): `POST /v1/graph`
- [src/FlowTime.API/Program.cs](../../src/FlowTime.API/Program.cs): `GET /v1/runs/{runId}/graph`
- [src/FlowTime.API/Program.cs](../../src/FlowTime.API/Program.cs): `GET /v1/runs/{runId}/metrics`
- [src/FlowTime.API/Program.cs](../../src/FlowTime.API/Program.cs): `GET /v1/runs/{runId}/state`
- [src/FlowTime.API/Program.cs](../../src/FlowTime.API/Program.cs): `GET /v1/runs/{runId}/state_window`
- [src/FlowTime.API/Program.cs](../../src/FlowTime.API/Program.cs): `GET /v1/runs/{runId}/index`
- [src/FlowTime.API/Program.cs](../../src/FlowTime.API/Program.cs): `GET /v1/runs/{runId}/series/{seriesId}`
- [src/FlowTime.API/Program.cs](../../src/FlowTime.API/Program.cs): `POST /v1/runs/{runId}/export`
- [src/FlowTime.API/Program.cs](../../src/FlowTime.API/Program.cs): `GET /v1/runs/{runId}/export/{format}`
- [src/FlowTime.API/Endpoints/RunOrchestrationEndpoints.cs](../../src/FlowTime.API/Endpoints/RunOrchestrationEndpoints.cs): `POST /v1/runs`, `GET /v1/runs`, `GET /v1/runs/{runId}`
- [src/FlowTime.API/Endpoints/TelemetryCaptureEndpoints.cs](../../src/FlowTime.API/Endpoints/TelemetryCaptureEndpoints.cs): `POST /v1/telemetry/captures`

### Sim Route Sweep

- [src/FlowTime.Sim.Service/Program.cs](../../src/FlowTime.Sim.Service/Program.cs): `GET /healthz`, `GET /v1/healthz`
- [src/FlowTime.Sim.Service/Extensions/RunOrchestrationEndpointExtensions.cs](../../src/FlowTime.Sim.Service/Extensions/RunOrchestrationEndpointExtensions.cs): `POST /api/v1/orchestration/runs`
- [src/FlowTime.Sim.Service/Program.cs](../../src/FlowTime.Sim.Service/Program.cs): `GET /api/v1/templates`
- [src/FlowTime.Sim.Service/Program.cs](../../src/FlowTime.Sim.Service/Program.cs): `GET /api/v1/templates/{id}`
- [src/FlowTime.Sim.Service/Program.cs](../../src/FlowTime.Sim.Service/Program.cs): `GET /api/v1/templates/{id}/source`
- [src/FlowTime.Sim.Service/Program.cs](../../src/FlowTime.Sim.Service/Program.cs): `GET /api/v1/templates/categories`
- [src/FlowTime.Sim.Service/Program.cs](../../src/FlowTime.Sim.Service/Program.cs): `POST /api/v1/templates/refresh`
- [src/FlowTime.Sim.Service/Program.cs](../../src/FlowTime.Sim.Service/Program.cs): `POST /api/v1/templates/{id}/generate`
- [src/FlowTime.Sim.Service/Program.cs](../../src/FlowTime.Sim.Service/Program.cs): `GET /api/v1/drafts`
- [src/FlowTime.Sim.Service/Program.cs](../../src/FlowTime.Sim.Service/Program.cs): `GET /api/v1/drafts/{draftId}`
- [src/FlowTime.Sim.Service/Program.cs](../../src/FlowTime.Sim.Service/Program.cs): `POST /api/v1/drafts`
- [src/FlowTime.Sim.Service/Program.cs](../../src/FlowTime.Sim.Service/Program.cs): `PUT /api/v1/drafts/{draftId}`
- [src/FlowTime.Sim.Service/Program.cs](../../src/FlowTime.Sim.Service/Program.cs): `DELETE /api/v1/drafts/{draftId}`
- [src/FlowTime.Sim.Service/Program.cs](../../src/FlowTime.Sim.Service/Program.cs): `POST /api/v1/drafts/validate`
- [src/FlowTime.Sim.Service/Program.cs](../../src/FlowTime.Sim.Service/Program.cs): `POST /api/v1/drafts/generate`
- [src/FlowTime.Sim.Service/Program.cs](../../src/FlowTime.Sim.Service/Program.cs): `POST /api/v1/drafts/run`
- [src/FlowTime.Sim.Service/Program.cs](../../src/FlowTime.Sim.Service/Program.cs): `POST /api/v1/series/ingest`
- [src/FlowTime.Sim.Service/Program.cs](../../src/FlowTime.Sim.Service/Program.cs): `POST /api/v1/series/summarize`
- [src/FlowTime.Sim.Service/Program.cs](../../src/FlowTime.Sim.Service/Program.cs): `POST /api/v1/profiles/fit`
- [src/FlowTime.Sim.Service/Program.cs](../../src/FlowTime.Sim.Service/Program.cs): `POST /api/v1/profiles/preview`
- [src/FlowTime.Sim.Service/Program.cs](../../src/FlowTime.Sim.Service/Program.cs): `POST /api/v1/drafts/map-profile`
- [src/FlowTime.Sim.Service/Program.cs](../../src/FlowTime.Sim.Service/Program.cs): `GET /api/v1/models`
- [src/FlowTime.Sim.Service/Program.cs](../../src/FlowTime.Sim.Service/Program.cs): `GET /api/v1/models/{templateId}`
- [src/FlowTime.Sim.Service/Program.cs](../../src/FlowTime.Sim.Service/Program.cs): `GET /api/v1/catalogs`
- [src/FlowTime.Sim.Service/Program.cs](../../src/FlowTime.Sim.Service/Program.cs): `GET /api/v1/catalogs/{id}`
- [src/FlowTime.Sim.Service/Program.cs](../../src/FlowTime.Sim.Service/Program.cs): `PUT /api/v1/catalogs/{id}`
- [src/FlowTime.Sim.Service/Program.cs](../../src/FlowTime.Sim.Service/Program.cs): `POST /api/v1/catalogs/validate`

### HTTP Call Site Sweep

- [src/FlowTime.UI/Services/FlowTimeSimApiClient.cs](../../src/FlowTime.UI/Services/FlowTimeSimApiClient.cs): `GET v1/healthz`, `GET v1/healthz?detailed=true`
- [src/FlowTime.UI/Services/FlowTimeSimApiClient.cs](../../src/FlowTime.UI/Services/FlowTimeSimApiClient.cs): `GET api/v1/templates`, `GET api/v1/templates/{templateId}`, `POST api/v1/templates/{templateId}/generate`, `POST api/v1/orchestration/runs`
- [src/FlowTime.UI/Services/FlowTimeSimApiClient.cs](../../src/FlowTime.UI/Services/FlowTimeSimApiClient.cs): stale `POST api/v1/run`, `GET api/v1/runs/{runId}/index`, `GET api/v1/runs/{runId}/series/{seriesId}` wrappers marked with TODO comments
- [ui/src/lib/api/sim.ts](../../ui/src/lib/api/sim.ts): `GET /api/v1/healthz`, `GET /api/v1/healthz?detailed=true`, `GET /api/v1/templates`, `GET /api/v1/templates/{id}`, `GET /api/v1/templates/categories`, `POST /api/v1/orchestration/runs`
- [ui/src/lib/api/flowtime.ts](../../ui/src/lib/api/flowtime.ts): `GET /healthz`, `GET /v1/healthz`, `GET /v1/runs`, `GET /v1/runs/{runId}`, `GET /v1/artifacts`, `GET /v1/artifacts/{id}`, `GET /v1/artifacts/{id}/relationships`, `GET /v1/artifacts/{id}/files/{fileName}`, `GET /v1/runs/{runId}/graph`, `GET /v1/runs/{runId}/state`, `GET /v1/runs/{runId}/index`, `GET /v1/runs/{runId}/state_window`

### Public Contract Sweep

- [src/FlowTime.Contracts/Dtos/ModelDtos.cs](../../src/FlowTime.Contracts/Dtos/ModelDtos.cs): `RngDto`, `ModelDto`, `GridDto`, `NodeDto`, `PmfDto`, `ClassDto`, `TrafficDto`, `ArrivalDto`, `ArrivalPatternDto`, `OutputDto`, `TopologyDto`, `TopologyNodeDto`, `TopologySemanticsDto`, `TopologyInitialConditionDto`, `TopologyEdgeDto`, `TopologyConstraintDto`, `ConstraintSemanticsDto`, `UiHintsDto`, `RouterInputsDto`, `RouterRouteDto`, `DispatchScheduleDto`
- [src/FlowTime.Contracts/TimeTravel/RunContracts.cs](../../src/FlowTime.Contracts/TimeTravel/RunContracts.cs): `RunCreateRequest`, `RunImportRequest`, `RunRngOptions`, `RunTelemetryOptions`, `RunCreationOptions`, `RunCreateResponse`, `RunSummaryResponse`, `RunSummary`, `RunCreatePlan`, `RunCreatePlanFile`, `RunCreatePlanWarning`, `RunTelemetrySummary`, `TelemetryCaptureRequest`, `TelemetryCaptureSource`, `TelemetryCaptureOutput`, `TelemetryCaptureResponse`, `TelemetryCaptureSummary`
- [src/FlowTime.Contracts/TimeTravel/GraphContracts.cs](../../src/FlowTime.Contracts/TimeTravel/GraphContracts.cs): `GraphResponse`, `GraphNode`, `GraphNodeSemantics`, `GraphNodeUi`, `GraphEdge`, `GraphNodeDistribution`
- [src/FlowTime.Contracts/TimeTravel/StateContracts.cs](../../src/FlowTime.Contracts/TimeTravel/StateContracts.cs): `StateSnapshotResponse`, `StateWindowResponse`, `StateMetadata`, `ClassCatalogEntry`, `SchemaMetadata`, `StorageDescriptor`, `BinDetail`, `WindowSlice`, `NodeSnapshot`, `NodeSeries`, `SlaMetricDescriptor`, `SlaSeriesDescriptor`, `NodeAnalyticalFacts`, `NodeClassTruthFacts`, `EdgeSeries`, `ConstraintMetrics`, `ConstraintSeries`, `NodeMetrics`, `ClassMetrics`, `NodeDerivedMetrics`, `NodeTelemetryInfo`, `NodeTelemetryWarning`, `SeriesSemanticsMetadata`, `StateWarning`
- [src/FlowTime.Contracts/TimeTravel/MetricsContracts.cs](../../src/FlowTime.Contracts/TimeTravel/MetricsContracts.cs): `MetricsResponse`, `MetricsWindow`, `MetricsGrid`, `ServiceMetrics`
- [src/FlowTime.Contracts/TimeTravel/QueueLatencyStatusDescriptor.cs](../../src/FlowTime.Contracts/TimeTravel/QueueLatencyStatusDescriptor.cs): `QueueLatencyStatusDescriptor`
- [src/FlowTime.Contracts/TimeTravel/DispatchScheduleDescriptor.cs](../../src/FlowTime.Contracts/TimeTravel/DispatchScheduleDescriptor.cs): `DispatchScheduleDescriptor`
- [src/FlowTime.Contracts/Services/ArtifactModels.cs](../../src/FlowTime.Contracts/Services/ArtifactModels.cs): `Artifact`, `RegistryIndex`, `ArtifactListResponse`, `ArtifactRelationships`, `ArtifactReference`
- [src/FlowTime.Contracts/Services/IArtifactRegistry.cs](../../src/FlowTime.Contracts/Services/IArtifactRegistry.cs): `IArtifactRegistry`, `ArtifactQueryOptions`
- [src/FlowTime.Contracts/Storage/StorageBackend.cs](../../src/FlowTime.Contracts/Storage/StorageBackend.cs): `StorageWriteRequest`, `StorageWriteResult`, `StorageReadResult`, `StorageListRequest`, `StorageItemSummary`, `IStorageBackend`
- [src/FlowTime.Contracts/Storage/StorageContracts.cs](../../src/FlowTime.Contracts/Storage/StorageContracts.cs): `StorageKind`, `StorageBackendKind`, `StorageIndexKind`, `StorageBackendOptions`, `StorageRef`

### Schema Sweep

- [docs/schemas/manifest.schema.json](../schemas/manifest.schema.json)
- [docs/schemas/model.schema.yaml](../schemas/model.schema.yaml)
- [docs/schemas/run.schema.json](../schemas/run.schema.json)
- [docs/schemas/series-index.schema.json](../schemas/series-index.schema.json)
- [docs/schemas/telemetry-manifest.schema.json](../schemas/telemetry-manifest.schema.json)
- [docs/schemas/template.schema.json](../schemas/template.schema.json)
- [docs/schemas/time-travel-state.schema.json](../schemas/time-travel-state.schema.json)

### Template Sweep

- [templates/dependency-constraints-attached.yaml](../../templates/dependency-constraints-attached.yaml)
- [templates/dependency-constraints-minimal.yaml](../../templates/dependency-constraints-minimal.yaml)
- [templates/it-document-processing-continuous.yaml](../../templates/it-document-processing-continuous.yaml)
- [templates/it-system-microservices.yaml](../../templates/it-system-microservices.yaml)
- [templates/manufacturing-line.yaml](../../templates/manufacturing-line.yaml)
- [templates/network-reliability.yaml](../../templates/network-reliability.yaml)
- [templates/supply-chain-incident-retry.yaml](../../templates/supply-chain-incident-retry.yaml)
- [templates/supply-chain-multi-tier-classes.yaml](../../templates/supply-chain-multi-tier-classes.yaml)
- [templates/supply-chain-multi-tier.yaml](../../templates/supply-chain-multi-tier.yaml)
- [templates/transportation-basic-classes.yaml](../../templates/transportation-basic-classes.yaml)
- [templates/transportation-basic.yaml](../../templates/transportation-basic.yaml)
- [templates/warehouse-picker-waves.yaml](../../templates/warehouse-picker-waves.yaml)

### Example Sweep

- [examples/class-enabled.yaml](../../examples/class-enabled.yaml)
- [examples/m0.const.sim.yaml](../../examples/m0.const.sim.yaml)
- [examples/m0.const.yaml](../../examples/m0.const.yaml)
- [examples/m0.poisson.sim.yaml](../../examples/m0.poisson.sim.yaml)
- [examples/m15.complex-pmf.yaml](../../examples/m15.complex-pmf.yaml)
- [examples/m2.pmf.yaml](../../examples/m2.pmf.yaml)
- [examples/archive/test-new-schema.yaml](../../examples/archive/test-new-schema.yaml)
- [examples/archive/test-no-schema.yaml](../../examples/archive/test-no-schema.yaml)
- [examples/archive/test-old-schema.yaml](../../examples/archive/test-old-schema.yaml)
- [examples/hello/model.yaml](../../examples/hello/model.yaml)
- [examples/http-demo/OrderService_arrivals.csv](../../examples/http-demo/OrderService_arrivals.csv)
- [examples/http-demo/OrderService_capacity.csv](../../examples/http-demo/OrderService_capacity.csv)
- [examples/http-demo/OrderService_errors.csv](../../examples/http-demo/OrderService_errors.csv)
- [examples/http-demo/OrderService_served.csv](../../examples/http-demo/OrderService_served.csv)
- [examples/http-demo/SupportQueue_arrivals.csv](../../examples/http-demo/SupportQueue_arrivals.csv)
- [examples/http-demo/SupportQueue_errors.csv](../../examples/http-demo/SupportQueue_errors.csv)
- [examples/http-demo/SupportQueue_queue.csv](../../examples/http-demo/SupportQueue_queue.csv)
- [examples/http-demo/SupportQueue_served.csv](../../examples/http-demo/SupportQueue_served.csv)

### Current Contract Docs Sweep

- [docs/flowtime.md](../flowtime.md)
- [docs/flowtime-v2.md](../flowtime-v2.md)
- [docs/reference/contracts.md](../reference/contracts.md)
- [docs/reference/engine-capabilities.md](../reference/engine-capabilities.md)
- [docs/reference/data-formats.md](../reference/data-formats.md)
- [docs/guides/UI.md](../guides/UI.md)
- [docs/ui/api-integration.md](../ui/api-integration.md)
- [docs/ui/time-travel-visualizations.md](../ui/time-travel-visualizations.md)
- [docs/archive/ui/template-integration-spec.md](../archive/ui/template-integration-spec.md)
- [docs/operations/telemetry-capture-guide.md](../operations/telemetry-capture-guide.md)
- [docs/templates/template-authoring.md](../templates/template-authoring.md)
- [docs/templates/template-testing.md](../templates/template-testing.md)
- [docs/templates/profiles.md](../templates/profiles.md)
- [docs/schemas/README.md](../schemas/README.md)
- [docs/schemas/model.schema.md](../schemas/model.schema.md)
- [docs/schemas/template-schema.md](../schemas/template-schema.md)
- [docs/architecture/run-provenance.md](./run-provenance.md)
- [docs/architecture/template-draft-model-run-bundle-boundary.md](./template-draft-model-run-bundle-boundary.md)
