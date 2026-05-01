---
id: M-027
title: Blazor Support Alignment
status: done
parent: E-19
acs:
  - id: AC-1
    title: Stale `RunAsync` wrapper deleted (row 64)
    status: met
  - id: AC-2
    title: Stale `GetIndexAsync` and `GetSeriesAsync` wrappers deleted (row 65)
    status: met
  - id: AC-3
    title: '`FlowTimeSimService` API-mode data generation rewired to orchestration'
    status: met
  - id: AC-4
    title: '`SimResultsService` run queries go through the Engine API only'
    status: met
  - id: AC-5
    title: Dead Sim run-query URL construction removed from `SimulationResults.razor`
    status: met
  - id: AC-6
    title: Supported Blazor Sim client surface confirmed aligned (row 63)
    status: met
  - id: AC-7
    title: Svelte client surfaces confirmed aligned (rows 66, 67)
    status: met
  - id: AC-8
    title: Grep-guard script codified
    status: met
  - id: AC-9
    title: Build, tests, and grep guards green
    status: met
  - id: AC-10
    title: Tracking doc and status surfaces reconciled
    status: met
---

## Goal

Remove stale `FlowTime.UI` Sim-client compatibility wrappers and the broken caller assumptions built on them, rewire the surviving Blazor authoring and run-query flows onto the supported Sim orchestration endpoint and Engine query API, and confirm the Svelte client layer stays aligned to current contracts. When this milestone closes, Blazor remains a supported first-party UI whose Sim client surface is exactly the row 63 supported set (`HealthAsync`, `GetDetailedHealthAsync`, `GetTemplatesAsync`, `GetTemplateAsync`, `GenerateModelAsync`, `CreateRunAsync`) and every Blazor caller either reaches a live endpoint or has been deleted alongside its wrapper.

## Context

[M-024](./M-024.md) published the supported-surfaces matrix in [docs/architecture/supported-surfaces.md](../../../docs/architecture/supported-surfaces.md) and assigned the Blazor HTTP call-site rows (63–65) and the Svelte alignment rows (66–67) to this milestone. [M-025](./M-025.md) deleted the Sim runtime seams those wrappers would have depended on (stored drafts CRUD, Sim ZIP archive layer, Engine bundle-import, runtime catalogs, `/api/v1/drafts/validate`, `GET /v1/debug/scan-directory`) and narrowed `/api/v1/drafts/run` to inline-only. [M-026](./M-026.md) retired deprecated schema, template, and example residue from active surfaces.

This milestone is the Blazor client-layer cleanup pass over that cleaned-up baseline. Every row whose `Owning milestone` column in the matrix is `m-E19-04` is executed here.

Scope boundaries inherited from the epic and M-024:

- Blazor is not retired. Blazor remains a supported first-party UI for debugging, operator workflows, and as plan-B to Svelte per the Blazor/Svelte support policy in [docs/architecture/supported-surfaces.md](../../../docs/architecture/supported-surfaces.md).
- Feature parity between Blazor and Svelte is not a goal. Svelte is intentionally behind Blazor.
- Demo mode stays. `FlowTimeSimService.RunDemoModeSimulationAsync` and the demo-data generators in `TemplateServiceImplementations.cs` are preserved as-is.
- `FlowTime.Core`, `FlowTime.Generator`, `FlowTime.API`, and `FlowTime.Sim.*` are not renamed and their high-level responsibilities do not change.
- Analytical surfaces purified by E-16 are out of scope.
- Engine and Sim runtime route deletions are not re-opened. M-025 owns them.
- Schema/template/example/docs retirement is not re-opened. M-026 owns it.
- `POST /v1/run` and `POST /v1/graph` remain deferred per [D-042](../../decisions.md#d-2026-04-08-029-defer-post-v1run-and-post-v1graph-deletion-out-of-m-e19-02-ac6-scope-narrowing). `TemplateRunner.razor`'s engine-eval flow at line 744 consuming `IRunClient.RunAsync` (routed via `ApiRunClient` → `IFlowTimeApiClient.RunAsync` → Engine `POST /v1/run`) stays on that deferred surface — this milestone does not touch it.
- `FlowTimeSimApiClientWithFallback` and `PortDiscoveryService` are legitimate dev-environment port discovery, not a compatibility shim. Their pass-through methods for deleted interface members are removed in this milestone; their port-discovery bootstrap stays.

The key distinction this milestone enforces:

- **Stale wrapper** — an `IFlowTimeSimApiClient` method that calls a Sim route that no longer exists (`RunAsync` → removed `/api/v1/run`; `GetIndexAsync`/`GetSeriesAsync` → `/api/v1/runs/{id}/index`, `/api/v1/runs/{id}/series/{id}` which were never added on Sim). These are the row 64 and row 65 targets. Delete.
- **Supported Sim client call** — methods backed by live Sim routes (`HealthAsync`, `GetDetailedHealthAsync`, `GetTemplatesAsync`, `GetTemplateAsync`, `GenerateModelAsync`, `CreateRunAsync`). These are the row 63 targets. Keep, audit for drift.
- **Engine API client** (`IFlowTimeApiClient`) — the correct surface for run queries (index, series, state, metrics). Rewired callers use this for every run query previously routed at the stale Sim wrappers.

## Acceptance criteria

### AC-1 — Stale `RunAsync` wrapper deleted (row 64)

`RunAsync(string yaml, ...)` on `IFlowTimeSimApiClient` targets `POST /api/v1/run` on the Sim service, which was removed on 2025-10-01 and does not return even when Sim is reachable. The file itself marks the method broken with a TODO comment.

**Delete:**

- `Task<Result<SimRunResponse>> RunAsync(string yaml, CancellationToken ct = default)` from the `IFlowTimeSimApiClient` interface at [src/FlowTime.UI/Services/FlowTimeSimApiClient.cs:9](../../../src/FlowTime.UI/Services/FlowTimeSimApiClient.cs).
- The implementation body and its TODO comment at [src/FlowTime.UI/Services/FlowTimeSimApiClient.cs:100-130](../../../src/FlowTime.UI/Services/FlowTimeSimApiClient.cs).
- The pass-through method `FlowTimeSimApiClientWithFallback.RunAsync` at [src/FlowTime.UI/Services/FlowTimeSimApiClientWithFallback.cs:72-76](../../../src/FlowTime.UI/Services/FlowTimeSimApiClientWithFallback.cs).
- `SimRunResponse` DTO and related deserialization types if they are unused after this AC and AC2 complete.

**Preserve:**

- `ApiRunClient.RunAsync` at [src/FlowTime.UI/Services/ApiRunClient.cs:14](../../../src/FlowTime.UI/Services/ApiRunClient.cs) — routes through `IFlowTimeApiClient.RunAsync` → Engine `POST /v1/run`, which is deferred per D-042.
- `RunClientRouter.RunAsync` at [src/FlowTime.UI/Services/RunClientRouter.cs:24](../../../src/FlowTime.UI/Services/RunClientRouter.cs) and `SimulationRunClient.RunAsync` — these are `IRunClient` members, not `IFlowTimeSimApiClient` members, and feed the deferred Engine direct-eval path.
- `FlowTimeSimApiClient.CreateRunAsync` (row 63) — the supported Sim orchestration wrapper.

**Grep guard:** No declaration or use of `IFlowTimeSimApiClient.RunAsync` remains in `src/FlowTime.UI/` or `tests/FlowTime.UI.Tests/`. The literal `api/v1/run` (i.e. the Sim `/api/v1/run` path, as distinct from Sim `/api/v1/runs/...` query routes which also do not exist and are covered by AC2) must not appear anywhere in `src/FlowTime.UI/Services/FlowTimeSimApiClient.cs` or `src/FlowTime.UI/Services/FlowTimeSimApiClientWithFallback.cs`.

### AC-2 — Stale `GetIndexAsync` and `GetSeriesAsync` wrappers deleted (row 65)

`GetIndexAsync` and `GetSeriesAsync` on `IFlowTimeSimApiClient` target `GET /api/v1/runs/{runId}/index` and `GET /api/v1/runs/{runId}/series/{seriesId}` on the Sim service. Neither route exists on Sim today and both files are marked broken with TODO comments pointing at Engine API as the correct target.

**Delete:**

- `Task<Result<SeriesIndex>> GetIndexAsync(string runId, ...)` from the `IFlowTimeSimApiClient` interface at [src/FlowTime.UI/Services/FlowTimeSimApiClient.cs:10](../../../src/FlowTime.UI/Services/FlowTimeSimApiClient.cs).
- `Task<Result<Stream>> GetSeriesAsync(string runId, string seriesId, ...)` from the `IFlowTimeSimApiClient` interface at [src/FlowTime.UI/Services/FlowTimeSimApiClient.cs:11](../../../src/FlowTime.UI/Services/FlowTimeSimApiClient.cs).
- Both implementation bodies and their TODO comments at [src/FlowTime.UI/Services/FlowTimeSimApiClient.cs:132-183](../../../src/FlowTime.UI/Services/FlowTimeSimApiClient.cs).
- The pass-through methods `FlowTimeSimApiClientWithFallback.GetIndexAsync` and `FlowTimeSimApiClientWithFallback.GetSeriesAsync` at [src/FlowTime.UI/Services/FlowTimeSimApiClientWithFallback.cs:78-88](../../../src/FlowTime.UI/Services/FlowTimeSimApiClientWithFallback.cs).

**Preserve:**

- `IFlowTimeApiClient.GetRunIndexAsync` and `GetRunSeriesAsync` — the canonical Engine run-query client methods. Every Blazor run-query caller routes through these after AC3 and AC5.
- `SeriesIndex` type itself — still consumed by the Engine client return type.

**Grep guard:** No declaration or use of `IFlowTimeSimApiClient.GetIndexAsync` or `IFlowTimeSimApiClient.GetSeriesAsync` remains in `src/FlowTime.UI/` or `tests/FlowTime.UI.Tests/`. No literal `api/v1/runs/{` followed by `/index` or `/series/` constructed against a Sim base address remains in `src/FlowTime.UI/Services/FlowTimeSimApiClient.cs` or `src/FlowTime.UI/Services/FlowTimeSimApiClientWithFallback.cs`.

### AC-3 — `FlowTimeSimService` API-mode data generation rewired to orchestration

`FlowTimeSimService.RunApiModeSimulationAsync` at [src/FlowTime.UI/Services/TemplateServiceImplementations.cs:951-1008](../../../src/FlowTime.UI/Services/TemplateServiceImplementations.cs) currently:

1. Generates a local YAML spec.
2. Calls `simClient.RunAsync(yamlSpec)` → the broken wrapper AC1 deletes.
3. Builds a `ResultsUrl` pointing at `/{apiVersion}/sim/runs/{runId}/index` — a path that does not exist on either Sim or Engine.

The supported replacement path is `CreateRunAsync` (row 63) on the Sim orchestration endpoint `POST /api/v1/orchestration/runs`, which accepts a `RunCreateRequestDto(templateId, mode, parameters, rng, telemetry)` and returns a canonical `RunCreateResponseDto` with a runId that the Engine API read surface recognises.

**Rewrite:**

- Replace the `GenerateSimulationYamlAsync` → `simClient.RunAsync(yamlSpec)` path inside `RunApiModeSimulationAsync` with a direct `simClient.CreateRunAsync(new RunCreateRequestDto(request.TemplateId, "simulation", request.Parameters, rng: null, telemetry: null))` call. The Sim orchestration endpoint owns template expansion — the local YAML-generation helper is no longer needed for this flow.
- Remove `ResultsUrl = $"/{simConfig.ApiVersion}/sim/runs/{runId}/index"` at [src/FlowTime.UI/Services/TemplateServiceImplementations.cs:987](../../../src/FlowTime.UI/Services/TemplateServiceImplementations.cs). Blazor consumers of `SimulationRunResult` already look up results by `RunId` through `SimResultsService`; the ad-hoc URL was dead guidance.
- Update `GetRunStatusAsync` at [src/FlowTime.UI/Services/TemplateServiceImplementations.cs:1012-1060](../../../src/FlowTime.UI/Services/TemplateServiceImplementations.cs) to call `apiClient.GetRunIndexAsync(runId, ct)` (Engine) instead of `simClient.GetIndexAsync(runId)` (deleted wrapper). The status-inference logic (completed / not_found / running) stays the same.
- If `GenerateSimulationYamlAsync` and any supporting schema-translation helpers in `TemplateServiceImplementations.cs` become unreachable after the rewrite, delete them. If they still have at least one live caller outside the rewritten method, leave them alone.

**Preserve:**

- `RunDemoModeSimulationAsync` and every demo-mode code path at [src/FlowTime.UI/Services/TemplateServiceImplementations.cs:898-949](../../../src/FlowTime.UI/Services/TemplateServiceImplementations.cs). Demo mode is not retired by this milestone.
- `FlowTimeSimService.RunSimulationAsync`'s outer demo-vs-api branch — only the API-mode branch body changes.

**Grep guard:** No `simClient.RunAsync(` or `simClient.GetIndexAsync(` call site remains in `src/FlowTime.UI/Services/TemplateServiceImplementations.cs`. No `/sim/runs/` URL literal remains anywhere in `src/FlowTime.UI/`.

### AC-4 — `SimResultsService` run queries go through the Engine API only

`SimResultsService.GetSimulationResultsAsync` at [src/FlowTime.UI/Services/SimResultsService.cs:38-124](../../../src/FlowTime.UI/Services/SimResultsService.cs) currently branches on `isEngineRun` (a `runId.StartsWith("run_")` check) and calls either `apiClient.GetRunIndexAsync`/`GetRunSeriesAsync` (for engine runs) or the stale `simClient.GetIndexAsync`/`GetSeriesAsync` (for non-engine runs). After AC3 rewires data generation onto `CreateRunAsync`, every API-mode run produces a canonical Engine-format runId, so the branch is dead.

**Rewrite:**

- Delete the `isEngineRun` branch at [src/FlowTime.UI/Services/SimResultsService.cs:57-96](../../../src/FlowTime.UI/Services/SimResultsService.cs). Route every non-demo run through `apiClient.GetRunIndexAsync(runId, ct)` for the series index and `apiClient.GetRunSeriesAsync(runId, series.Id, ct)` for each series stream.
- Remove the `simClient` field, constructor parameter, and any `IFlowTimeSimApiClient` dependency on `SimResultsService` once the Sim-branch is gone. Update the `Program.cs` DI registration accordingly.
- Preserve the `demo://` prefix handling and the `UseDemoMode` feature-flag check. Demo mode is not retired.

**Preserve:**

- `GetDemoModeResultsAsync` and its synthetic-data generators.
- `SimResultData` result type including the `BinMinutes` computed display property (preserved per M-026 spec).

**Grep guard:** No `simClient.GetIndexAsync(` or `simClient.GetSeriesAsync(` call site remains in `src/FlowTime.UI/Services/SimResultsService.cs`. `IFlowTimeSimApiClient` must no longer appear in the `SimResultsService` constructor signature.

### AC-5 — Dead Sim run-query URL construction removed from `SimulationResults.razor`

[src/FlowTime.UI/Components/Templates/SimulationResults.razor:295-312](../../../src/FlowTime.UI/Components/Templates/SimulationResults.razor) constructs a download URL conditional on demo vs API mode:

- Demo branch (line 302-303): `downloadUrl = $"{baseUrl}/{simConfig.ApiVersion}/sim/runs/{runId}/series/{seriesId}"` — a path that does not exist on Sim.
- API branch (line 307-311): `downloadUrl = $"{baseUrl}/{apiConfig.ApiVersion}/runs/{runId}/series/{seriesId}"` — a path that does not match the canonical Engine run-series route (`/v1/runs/{runId}/series/{seriesId}`).

After AC3 rewires API-mode data generation to produce canonical Engine run IDs, the single correct download URL for every non-demo run is the Engine API's `GET /v1/runs/{runId}/series/{seriesId}`.

**Rewrite:**

- Collapse the demo-vs-API branch at [src/FlowTime.UI/Components/Templates/SimulationResults.razor:295-312](../../../src/FlowTime.UI/Components/Templates/SimulationResults.razor). Non-demo runs use `$"{apiBaseUrl}/{apiConfig.ApiVersion}/runs/{runId}/series/{seriesId}"` resolved from `FlowTimeApiOptions`. Confirm at implementation time whether the existing `apiConfig.ApiVersion` value produces the canonical `/v1/runs/...` shape; if not, correct the format string to match the live Engine route.
- Demo-mode download behaviour: either reuse the Engine download URL (if demo runs are materialised to canonical run directories) or remove the download button in demo mode. Decide at implementation time after checking whether demo runs actually produce downloadable series files; if not, the mode-mismatch warning path already covers the user story and the download button can be hidden in demo mode.

**Preserve:**

- The mode-mismatch warning logic at [src/FlowTime.UI/Components/Templates/SimulationResults.razor:280-293](../../../src/FlowTime.UI/Components/Templates/SimulationResults.razor). That UX guidance still applies.

**Grep guard:** No `/sim/runs/` literal remains in `src/FlowTime.UI/Components/`. No `{apiConfig.ApiVersion}/runs/{runId}/series/` literal that does not match the canonical Engine route shape remains in `src/FlowTime.UI/Components/Templates/SimulationResults.razor`.

### AC-6 — Supported Blazor Sim client surface confirmed aligned (row 63)

Row 63 of the supported-surfaces matrix lists `HealthAsync`, `GetDetailedHealthAsync`, `GetTemplatesAsync`, `GetTemplateAsync`, `GenerateModelAsync`, `CreateRunAsync` as the supported Blazor Sim client surface. After AC1 and AC2 complete, those are the only methods remaining on `IFlowTimeSimApiClient`.

**Audit:**

- Confirm each surviving method targets a live Sim route per the Sim route sweep in [docs/architecture/supported-surfaces.md](../../../docs/architecture/supported-surfaces.md).
- Confirm no surviving method reconstructs metrics, state, or run shapes locally where a canonical endpoint already exists.
- Confirm the response DTOs the surviving methods deserialize (e.g. `FlowTimeSimDetailedHealthResponse`, `ApiTemplateInfo`, `TemplateGenerationResponse`, `RunCreateResponseDto`) match the current Sim wire shapes. Any drift is a milestone regression.

**Expected outcome:** no code change if no drift is found. If drift is discovered, fix it in the same commit bundle as the alignment audit and document the change in the tracking doc. Adding new capability is out of scope — only bringing the surface back into alignment with a current contract is.

**Grep guard:** `IFlowTimeSimApiClient` at [src/FlowTime.UI/Services/FlowTimeSimApiClient.cs](../../../src/FlowTime.UI/Services/FlowTimeSimApiClient.cs) exposes exactly the row 63 supported set after this milestone. No method names outside `{BaseAddress, HealthAsync, GetDetailedHealthAsync, GetTemplatesAsync, GetTemplateAsync, GenerateModelAsync, CreateRunAsync}` remain on the interface.

### AC-7 — Svelte client surfaces confirmed aligned (rows 66, 67)

Rows 66 and 67 of the matrix list the Svelte `Sim` client at [ui/src/lib/api/sim.ts](../../../ui/src/lib/api/sim.ts) and Engine client at [ui/src/lib/api/flowtime.ts](../../../ui/src/lib/api/flowtime.ts) as supported first-party surfaces. M-027 is the owning milestone for their alignment audit.

**Audit:**

- Confirm `ui/src/lib/api/sim.ts` call sites target the current Sim routes listed in the HTTP Call Site Sweep (supported-surfaces.md): `/api/v1/healthz`, `/api/v1/templates`, `/api/v1/templates/{id}`, `/api/v1/templates/categories`, `/api/v1/orchestration/runs`. No stale draft CRUD, catalog, or bundle-import probes remain.
- Confirm `ui/src/lib/api/flowtime.ts` call sites target the current Engine routes: `/healthz`, `/v1/healthz`, `/v1/runs`, `/v1/runs/{runId}`, `/v1/artifacts*`, `/v1/runs/{runId}/graph`, `/v1/runs/{runId}/state`, `/v1/runs/{runId}/index`, `/v1/runs/{runId}/state_window`. No stale bundle-import, `POST /v1/runs`, or `/v1/debug/` probes remain.
- Confirm neither file reconstructs metrics, state, or run shapes locally where a canonical Engine endpoint already exists.

**Expected outcome:** no code change if no drift is found. If drift is discovered, fix it in the same commit bundle as the alignment audit.

**Grep guard:** `ui/src/lib/api/sim.ts` must not contain literals matching `catalogs`, `drafts`, `bundle`, `bundlePath`, `bundleArchiveBase64`, or `bundleRef`. `ui/src/lib/api/flowtime.ts` must not contain literals matching `POST /v1/runs`, `bundlePath`, `bundleArchiveBase64`, `bundleRef`, or `/v1/debug/`.

### AC-8 — Grep-guard script codified

Create `scripts/m-E19-04-grep-guards.sh` mirroring the structure of `scripts/m-E19-03-grep-guards.sh`. Every guard listed in AC1–AC7 becomes a named test in the script. The script must exit 0 when all guards pass.

**Guards, as implemented in the script (each a named test):**

1. No `RunAsync(` declaration on `IFlowTimeSimApiClient` in `src/FlowTime.UI/Services/FlowTimeSimApiClient.cs` or implementation in `FlowTimeSimApiClient`/`FlowTimeSimApiClientWithFallback`.
2. No `api/v1/run"` literal (Sim `/api/v1/run` path) in `src/FlowTime.UI/Services/FlowTimeSimApiClient.cs` or `FlowTimeSimApiClientWithFallback.cs`.
3. No `GetIndexAsync(` or `GetSeriesAsync(` declaration on `IFlowTimeSimApiClient` or implementation in `FlowTimeSimApiClient`/`FlowTimeSimApiClientWithFallback`.
4. No `api/v1/runs/{` literal constructed against a Sim base address in `FlowTimeSimApiClient.cs` or `FlowTimeSimApiClientWithFallback.cs`.
5. No `simClient.RunAsync(` or `simClient.GetIndexAsync(` or `simClient.GetSeriesAsync(` call site in `src/FlowTime.UI/Services/TemplateServiceImplementations.cs`.
6. No `simClient.GetIndexAsync(` or `simClient.GetSeriesAsync(` call site in `src/FlowTime.UI/Services/SimResultsService.cs`.
7. No `IFlowTimeSimApiClient` dependency on the `SimResultsService` constructor signature.
8. No `/sim/runs/` URL literal anywhere in `src/FlowTime.UI/`.
9. `IFlowTimeSimApiClient` interface surface is exactly `{BaseAddress, HealthAsync, GetDetailedHealthAsync, GetTemplatesAsync, GetTemplateAsync, GenerateModelAsync, CreateRunAsync}` — no extra methods.
10. No `catalogs`, `drafts`, `bundlePath`, `bundleArchiveBase64`, or `bundleRef` literal in `ui/src/lib/api/sim.ts`.
11. No `POST /v1/runs`, `bundlePath`, `bundleArchiveBase64`, `bundleRef`, or `/v1/debug/` literal in `ui/src/lib/api/flowtime.ts`.

Scoped searches are limited to `src/FlowTime.UI/`, `ui/src/lib/api/`, and `tests/FlowTime.UI.Tests/` by default. The script runs locally and in the wrap pass. CI wiring stays deferred, matching the pattern in `scripts/m-E19-02-grep-guards.sh` and `scripts/m-E19-03-grep-guards.sh`.

### AC-9 — Build, tests, and grep guards green

- `dotnet build FlowTime.sln` is green with no new warnings introduced by this milestone.
- `dotnet test FlowTime.sln` is green across all test projects. Test deletions for deleted code are acceptable; failing tests or reduced coverage for surviving code are not. In particular, `tests/FlowTime.UI.Tests/TimeTravelDataServiceTests.cs`, `DashboardTests.cs`, and `ArtifactListRenderTests.cs` define mock implementations of an `IFlowTimeApiClient`-like interface whose method names happen to include `RunAsync` and `GetSeriesAsync` — those are Engine-client members, not Sim-client members, and must remain untouched. Only the `IFlowTimeSimApiClient` declarations and implementations are in scope.
- The Svelte `ui/` project's existing `npm`/`pnpm` build (if wired) is green after the alignment audit.
- `scripts/m-E19-04-grep-guards.sh` exits 0 from the repo root.

### AC-10 — Tracking doc and status surfaces reconciled

- Create `work/epics/E-19-surface-alignment-and-compatibility-cleanup/m-E19-04-blazor-support-alignment-tracking.md` at milestone start and update it after each AC lands. Tracking doc records: per-AC file changes, grep-guard results, test counts, alignment-audit findings (drift or no drift), and deviations from the spec (if any).
- Flip milestone status in a single reconciliation pass at wrap time:
  - This spec: `draft` → `in-progress` at start → `completed` at wrap.
  - [work/epics/E-19-surface-alignment-and-compatibility-cleanup/spec.md](./spec.md) milestone table: `m-E19-04` status `next` → `in-progress` → `completed`; header `Status:` line updated; epic `Success Criteria` checkboxes for "first-party clients no longer maintain duplicate endpoint, metrics, or health fallback logic" and "grep and regression audits prove targeted legacy/fallback helpers are removed or isolated" flipped to checked if M-027 closes them.
  - [ROADMAP.md](../../../ROADMAP.md) E-19 section: sync M-027 completion. If M-027 is the final E-19 milestone before epic closure, advance the E-19 section to completed and name the next epic/milestone.
  - [work/epics/epic-roadmap.md](../epic-roadmap.md) E-19 row: same sync.
  - [CLAUDE.md](../../../CLAUDE.md) Current Work section: sync E-19 topology and next-step pointer.
- All status-surface updates happen in a single wrap commit after the grep guards pass.

## Technical Notes

### Commit plan (bundled)

ACs are grouped into four focused commits plus the wrap. Each bundle is a single atomic concept so bisect points to one conceptual slice of the milestone.

1. **Bundle A — stale Sim client deletion + caller rewire (AC1 + AC2 + AC3 + AC4 + AC5).** Delete the three stale interface methods and their implementations in `FlowTimeSimApiClient` and `FlowTimeSimApiClientWithFallback`. Rewire `FlowTimeSimService.RunApiModeSimulationAsync` onto `CreateRunAsync` and `GetRunStatusAsync` onto `apiClient.GetRunIndexAsync`. Simplify `SimResultsService` to use Engine API for every non-demo query and drop its `IFlowTimeSimApiClient` dependency. Collapse the dead download-URL branch in `SimulationResults.razor`. This is one conceptual cleanup — the stale Sim client and its ripple effects — and goes in one commit.
2. **Bundle B — alignment audit findings (AC6 + AC7).** Typically no code change. If the audits surface drift, fix it here. If no drift, this bundle is a no-op commit or folded into the tracking-doc wrap (if folded, the tracking doc records "no drift found").
3. **Bundle C — grep guard script (AC8).** Its own commit. The script must pass against the tree from Bundle A (and Bundle B if it produced changes), proving the cleanup is complete before the wrap.
4. **Wrap (AC9 + AC10).** Tracking doc finalization and status-surface reconciliation in a single commit after the grep guards and build/test pass.

If any bundle surfaces a complication at implementation time (e.g. Bundle A discovers a deeper caller chain that cannot be rewired cleanly, or AC5's `SimulationResults.razor` demo-mode download decision needs human input), stop and present options before widening or splitting the bundle, the way M-025 handled the AC6 scope narrowing and M-026 handled the Little's Law allowlist marker.

### Recommended implementation sequence within Bundle A

Each step should leave the build green and the test suite passing before the next step begins. Forward-only; no compatibility shims.

1. **Rewire `FlowTimeSimService` first.** Change `RunApiModeSimulationAsync` to call `simClient.CreateRunAsync(...)` instead of `simClient.RunAsync(yamlSpec)`. Change `GetRunStatusAsync` to call `apiClient.GetRunIndexAsync(runId, ct)` instead of `simClient.GetIndexAsync(runId)`. Build and run `dotnet test` before removing anything — this isolates the rewire from the deletion.
2. **Simplify `SimResultsService`.** Remove the `isEngineRun` branch, drop the `simClient` field and constructor parameter, update the `Program.cs` DI registration. Build and test again.
3. **Collapse `SimulationResults.razor` download URL.** Decide the demo-mode download handling (reuse the Engine URL if demo runs materialise, otherwise hide the demo-mode download button). Build and test again.
4. **Delete the interface methods and implementations.** Now that every caller is rewired, delete `RunAsync`/`GetIndexAsync`/`GetSeriesAsync` from `IFlowTimeSimApiClient`, `FlowTimeSimApiClient`, and `FlowTimeSimApiClientWithFallback`. Build and test. Any lingering reference surfaces as a compile error and must be rewired in this same commit, not deferred.
5. **Clean up orphaned helpers.** If `GenerateSimulationYamlAsync` or related schema-translation helpers in `TemplateServiceImplementations.cs` are unreachable, delete them. If `SimRunResponse` DTO is unused, delete it. Build and test once more.

### Supporting data

- `IFlowTimeSimApiClient` interface declaration: [src/FlowTime.UI/Services/FlowTimeSimApiClient.cs:6-18](../../../src/FlowTime.UI/Services/FlowTimeSimApiClient.cs).
- Concrete implementation: [src/FlowTime.UI/Services/FlowTimeSimApiClient.cs:20-439](../../../src/FlowTime.UI/Services/FlowTimeSimApiClient.cs).
- Port-discovery wrapper (retains port-discovery bootstrap, loses pass-throughs): [src/FlowTime.UI/Services/FlowTimeSimApiClientWithFallback.cs](../../../src/FlowTime.UI/Services/FlowTimeSimApiClientWithFallback.cs).
- `FlowTimeSimService` API-mode orchestration: [src/FlowTime.UI/Services/TemplateServiceImplementations.cs:867-1060](../../../src/FlowTime.UI/Services/TemplateServiceImplementations.cs).
- `SimResultsService` result loading: [src/FlowTime.UI/Services/SimResultsService.cs:38-124](../../../src/FlowTime.UI/Services/SimResultsService.cs).
- Download URL construction: [src/FlowTime.UI/Components/Templates/SimulationResults.razor:295-315](../../../src/FlowTime.UI/Components/Templates/SimulationResults.razor).
- Blazor `TemplateRunner.razor` data-generation caller: [src/FlowTime.UI/Pages/TemplateRunner.razor:831](../../../src/FlowTime.UI/Pages/TemplateRunner.razor).
- Blazor `TemplateRunner.razor` engine-eval caller (preserved, deferred per D-042): [src/FlowTime.UI/Pages/TemplateRunner.razor:744](../../../src/FlowTime.UI/Pages/TemplateRunner.razor).
- Engine API client read methods used by the rewired callers: `IFlowTimeApiClient.GetRunIndexAsync` and `GetRunSeriesAsync` in [src/FlowTime.UI/Services/FlowTimeApiClient.cs](../../../src/FlowTime.UI/Services/FlowTimeApiClient.cs).
- Svelte Sim client: [ui/src/lib/api/sim.ts](../../../ui/src/lib/api/sim.ts).
- Svelte Engine client: [ui/src/lib/api/flowtime.ts](../../../ui/src/lib/api/flowtime.ts).

### Test strategy

Forward-only deletion and rewire, not migration:

- Tests that exist only to exercise the deleted stale wrappers are deleted alongside the wrappers.
- Tests that mock `IFlowTimeSimApiClient` for an unrelated scenario (e.g. template-metadata tests) must be updated to drop the deleted methods from the mock implementation. If the mock no longer compiles because the interface shrank, that is a desired forcing function — update the mock to the shrunken surface.
- Tests that mock `IFlowTimeApiClient` (Engine client) are out of scope. Method-name collisions with `RunAsync`/`GetSeriesAsync` on the Engine mocks are not stale-wrapper residue — Engine-side deletion is deferred per D-042.
- No new unit tests are required by this milestone unless a rewire surfaces a regression that existing coverage did not catch. In that case, the regression test is added alongside the fix.
- Grep guards (AC8) are the load-bearing regression check for this milestone. Every deleted symbol and every rewired caller path is asserted absent or present via a guard.

## Preserved Surfaces

Explicit list of surfaces that must remain untouched by this milestone. Any accidental change to these surfaces is a milestone regression.

- `IFlowTimeApiClient` and its Engine-facing implementations (`FlowTimeApiClient`, `ApiRunClient`) — they are the canonical Engine query surface.
- `IRunClient`, `RunClientRouter`, `SimulationRunClient`, `ApiRunClient` — these feed the deferred Engine direct-eval path per D-042. Their `RunAsync` members are not stale wrappers.
- `FlowTimeSimApiClient.CreateRunAsync`, `HealthAsync`, `GetDetailedHealthAsync`, `GetTemplatesAsync`, `GetTemplateAsync`, `GenerateModelAsync` — row 63 supported surface.
- `FlowTimeSimApiClientWithFallback` class, its `PortDiscoveryService` integration, and its bootstrap in `Program.cs` — legitimate dev-environment port discovery. Only the pass-through methods for deleted interface members are removed.
- `PortDiscoveryService` and `FlowTimeSimApiOptions` — unchanged.
- `FlowTimeSimService.RunSimulationAsync` outer demo-vs-api branch, `RunDemoModeSimulationAsync`, and every demo-data generator in `TemplateServiceImplementations.cs`.
- `SimResultsService.GetDemoModeResultsAsync` and every demo-data generator.
- `SimResultData` result type including its `BinMinutes` computed display property (explicitly preserved by M-026 as a display helper, not a schema field).
- `TemplateRunner.razor` — flow analysis path at line 744 routing through `IRunClient.RunAsync` stays untouched. Only the data-generation path at line 831 is affected, and only transitively via `FlowTimeSimService` rewire.
- `Simulate.razor` — listed in M-026 preserved surfaces; any incidental reads of `SimResultsService` continue to work after the rewire.
- `TemplateServiceImplementations.TemplateService` (the template metadata class distinct from `FlowTimeSimService`) — template authoring is out of scope here; M-026 already retired its `binMinutes` demo residue.
- `ui/src/lib/api/sim.ts` and `ui/src/lib/api/flowtime.ts` — no code change expected from the alignment audit unless drift is found.
- `IFlowTimeSimApiClient` methods that are NOT in the stale-wrapper set: only AC1 and AC2 targets are removed. `CreateRunAsync` in particular must be preserved and is the replacement for the deleted `RunAsync`.

## Out of Scope

- Retiring Blazor as a first-party UI. Blazor remains supported per the Blazor/Svelte support policy.
- Retiring Blazor demo mode. Demo mode is explicitly preserved.
- Forcing feature parity between Blazor and Svelte. Feature parity is not a goal.
- Adding new capability to either UI. Only alignment with current contracts is in scope.
- Deleting `POST /v1/run` or `POST /v1/graph` from Engine or their test consumers. Deferred per D-042.
- Touching the `IRunClient` / `ApiRunClient` / `RunClientRouter` / `SimulationRunClient` abstractions. Those feed the deferred Engine direct-eval path.
- Touching `FlowTime.Core`, `FlowTime.Generator`, `FlowTime.API`, `FlowTime.Sim.*`, or any non-UI project.
- Re-opening schema, template, example, or docs retirement. M-026 owns those and is complete.
- Re-opening Sim runtime route deletion. M-025 owns those and is complete.
- Introducing or referencing `FlowTime.TimeMachine`. That component is new in E-18 m-E18-01a and does not exist yet.
- Reintroducing any deleted Sim route via a Blazor-side compatibility shim.
- Refactoring `FlowTimeSimService`, `SimResultsService`, or `SimulationResults.razor` beyond what the deletions and rewires require. The commit bundle stays scoped to the stale-wrapper cleanup ripple.
- Performance, observability, or error-handling improvements unrelated to deletion and rewire.
- CI wiring for `scripts/m-E19-04-grep-guards.sh`. The script exists and runs locally; CI integration is deferred matching the pattern in M-025 and M-026.
- Updating release notes, completed-epic specs, or other historical material under `docs/releases/`, `docs/archive/`, or `work/epics/completed/`.

## Guards / DO NOT

- **DO NOT** delete `CreateRunAsync`, `HealthAsync`, `GetDetailedHealthAsync`, `GetTemplatesAsync`, `GetTemplateAsync`, or `GenerateModelAsync` from `IFlowTimeSimApiClient`. They are the row 63 supported surface and the rewired API-mode data-generation path depends on `CreateRunAsync`.
- **DO NOT** delete or modify `ApiRunClient.RunAsync`, `RunClientRouter.RunAsync`, or `SimulationRunClient.RunAsync`. Those are `IRunClient` members that feed the deferred Engine direct-eval path and are out of scope.
- **DO NOT** delete `FlowTimeSimApiClientWithFallback` or its `PortDiscoveryService` integration. Only the pass-through methods for deleted interface members are removed.
- **DO NOT** retire Blazor demo mode or any demo-data generator. Demo mode is explicitly preserved.
- **DO NOT** introduce new HTTP clients, new interface abstractions, or new DI lifetimes during the rewire. The rewire reuses `IFlowTimeApiClient` for run queries and `IFlowTimeSimApiClient.CreateRunAsync` for run creation.
- **DO NOT** reintroduce any deleted Sim route literal (`/api/v1/run`, `/api/v1/runs/{id}/index`, `/api/v1/runs/{id}/series/{id}`) anywhere in `src/FlowTime.UI/`.
- **DO NOT** add advisory comments pointing at E-18 Time Machine or at the deleted wrappers. Forward-only — once the wrappers are gone, no migration commentary is needed.
- **DO NOT** widen the scope into template authoring, schema cleanup, runtime endpoint changes, or Contracts-level refactors. Those are other milestones (or already complete).
- **DO NOT** touch the Svelte UI unless the AC7 alignment audit surfaces drift. The default expected outcome of AC7 is "no code change."
- **DO NOT** introduce compatibility shims, feature flags, or configuration toggles to keep deleted wrappers reachable.
- **DO NOT** commit before explicit human approval per the repo's Hard Rules.

## Dependencies

- [M-024 Supported Surface Inventory, Boundary ADR & Exit Criteria](./M-024.md) — supplies matrix rows 63–67 and the Blazor/Svelte support policy this milestone executes.
- [M-025 Sim Authoring & Runtime Boundary Cleanup](./M-025.md) — already removed the Sim runtime routes these wrappers would have depended on, so the current state is "broken wrappers" not "unused-but-working wrappers."
- [M-026 Schema, Template & Example Retirement](./M-026.md) — already retired the deprecated `binMinutes` authoring residue from `TemplateServiceImplementations.cs` demo generators and the UI sample fixture, so the only `TemplateServiceImplementations.cs` residue left is the stale Sim client caller chain this milestone rewires.
- [docs/architecture/supported-surfaces.md](../../../docs/architecture/supported-surfaces.md) — authoritative row-by-row ownership.

## References

- [E-19 epic spec](./spec.md)
- [M-024 spec](./M-024.md) — see matrix rows 63–67 and the Blazor/Svelte Support Policy section
- [M-025 spec](./M-025.md)
- [M-026 spec](./M-026.md)
- [work/decisions.md](../../decisions.md) — D-042 (deferred `/v1/run` `/v1/graph`), Blazor/Svelte support policy decision
- [scripts/M-026.sh](../../../scripts/M-026.sh) — template for the M-027 grep-guard script
