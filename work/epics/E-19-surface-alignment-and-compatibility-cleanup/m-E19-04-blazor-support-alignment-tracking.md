# Tracking: m-E19-04 Blazor Support Alignment

**Status:** in-progress
**Started:** 2026-04-08
**Epic:** [E-19 Surface Alignment & Compatibility Cleanup](./spec.md)
**Milestone spec:** [m-E19-04-blazor-support-alignment.md](./m-E19-04-blazor-support-alignment.md)
**Branch:** `milestone/m-E19-04-blazor-support-alignment` (off `epic/E-19`)
**Baseline test count (epic/E-19 head):** 1250 passed, 9 skipped, 0 failed
**Grep guards:** 11 tests planned in `scripts/m-E19-04-grep-guards.sh`

## Acceptance Criteria

- [ ] **AC1.** Delete stale `RunAsync` wrapper from `IFlowTimeSimApiClient`, `FlowTimeSimApiClient`, and `FlowTimeSimApiClientWithFallback`. Remove `SimRunResponse` DTO if unused. Preserve `ApiRunClient.RunAsync`, `RunClientRouter.RunAsync`, `SimulationRunClient.RunAsync` (IRunClient members, deferred Engine path). (Bundle A)
- [ ] **AC2.** Delete stale `GetIndexAsync` and `GetSeriesAsync` wrappers from `IFlowTimeSimApiClient`, `FlowTimeSimApiClient`, and `FlowTimeSimApiClientWithFallback`. Preserve `IFlowTimeApiClient.GetRunIndexAsync`/`GetRunSeriesAsync` and the `SeriesIndex` type. (Bundle A)
- [ ] **AC3.** Rewire `FlowTimeSimService.RunApiModeSimulationAsync` onto `simClient.CreateRunAsync(...)` (Sim orchestration path). Rewire `FlowTimeSimService.GetRunStatusAsync` onto `apiClient.GetRunIndexAsync(...)` (Engine query path). Remove dead `ResultsUrl = "/{apiVersion}/sim/runs/{runId}/index"`. Delete orphaned `GenerateSimulationYamlAsync` / schema-translation helpers if unreachable. (Bundle A)
- [ ] **AC4.** Simplify `SimResultsService.GetSimulationResultsAsync` to use Engine API for every non-demo query (drop the `isEngineRun` branch). Remove `IFlowTimeSimApiClient` constructor dependency on `SimResultsService`. Update Program.cs DI registration. Preserve demo mode. (Bundle A)
- [ ] **AC5.** Collapse dead download-URL construction in `SimulationResults.razor` (lines 295-312) onto a single Engine API URL that matches the canonical `/v1/runs/{runId}/series/{seriesId}` shape. Decide demo-mode download handling (reuse Engine URL or hide download button). (Bundle A)
- [ ] **AC6.** Row 63 alignment audit — confirm `HealthAsync`, `GetDetailedHealthAsync`, `GetTemplatesAsync`, `GetTemplateAsync`, `GenerateModelAsync`, `CreateRunAsync` are the only methods on `IFlowTimeSimApiClient` after Bundle A and each targets a live Sim route. No drift expected. (Bundle B)
- [ ] **AC7.** Rows 66/67 alignment audit — confirm `ui/src/lib/api/sim.ts` and `ui/src/lib/api/flowtime.ts` target current contracts with no stale drafts/catalogs/bundle literals. No drift expected. (Bundle B)
- [ ] **AC8.** `scripts/m-E19-04-grep-guards.sh` created with 11 named guards (AC1–AC7 coverage); script exits 0 when all guards pass. (Bundle C)
- [ ] **AC9.** Full build + full test suite green, grep guards green, no new compiler warnings. (Wrap)
- [ ] **AC10.** Tracking doc finalized; status reconciled across spec, epic spec milestone table, ROADMAP.md, epic-roadmap.md, CLAUDE.md Current Work. (Wrap)

## Commit Plan (Bundles)

Per milestone spec Technical Notes — four focused commits plus the wrap.

- [ ] **Status-sync commit** (this doc + spec draft→in-progress + epic spec table + ROADMAP.md + epic-roadmap.md + CLAUDE.md) — runs before Bundle A.
- [ ] **Bundle A** (AC1 + AC2 + AC3 + AC4 + AC5): stale Sim client deletion + caller rewire. Single conceptual cleanup; one commit.
- [ ] **Bundle B** (AC6 + AC7): alignment audit. Typically no code change; folded into tracking doc update if no drift, otherwise its own commit.
- [ ] **Bundle C** (AC8): grep-guard script. Its own commit. Script must pass against the tree from Bundles A (+B if any).
- [ ] **Wrap** (AC9 + AC10): tracking doc finalization and status-surface reconciliation in a single commit after grep guards + build/test pass.

## Grep Guards

Each must return zero matches (or the expected count) after the milestone completes. Full script: `scripts/m-E19-04-grep-guards.sh`.

- [ ] Guard 1: No `RunAsync(` declaration on `IFlowTimeSimApiClient` in `src/FlowTime.UI/Services/FlowTimeSimApiClient.cs` or implementation in `FlowTimeSimApiClient`/`FlowTimeSimApiClientWithFallback`.
- [ ] Guard 2: No `api/v1/run"` literal in `src/FlowTime.UI/Services/FlowTimeSimApiClient.cs` or `FlowTimeSimApiClientWithFallback.cs`.
- [ ] Guard 3: No `GetIndexAsync(` or `GetSeriesAsync(` declaration on `IFlowTimeSimApiClient` or implementation in `FlowTimeSimApiClient`/`FlowTimeSimApiClientWithFallback`.
- [ ] Guard 4: No `api/v1/runs/{` literal constructed against a Sim base address in `FlowTimeSimApiClient.cs` or `FlowTimeSimApiClientWithFallback.cs`.
- [ ] Guard 5: No `simClient.RunAsync(`, `simClient.GetIndexAsync(`, or `simClient.GetSeriesAsync(` in `src/FlowTime.UI/Services/TemplateServiceImplementations.cs`.
- [ ] Guard 6: No `simClient.GetIndexAsync(` or `simClient.GetSeriesAsync(` in `src/FlowTime.UI/Services/SimResultsService.cs`.
- [ ] Guard 7: No `IFlowTimeSimApiClient` dependency on the `SimResultsService` constructor signature.
- [ ] Guard 8: No `/sim/runs/` URL literal anywhere in `src/FlowTime.UI/`.
- [ ] Guard 9: `IFlowTimeSimApiClient` interface surface is exactly `{BaseAddress, HealthAsync, GetDetailedHealthAsync, GetTemplatesAsync, GetTemplateAsync, GenerateModelAsync, CreateRunAsync}`.
- [ ] Guard 10: No `catalogs`, `drafts`, `bundlePath`, `bundleArchiveBase64`, or `bundleRef` literal in `ui/src/lib/api/sim.ts`.
- [ ] Guard 11: No `POST /v1/runs`, `bundlePath`, `bundleArchiveBase64`, `bundleRef`, or `/v1/debug/` literal in `ui/src/lib/api/flowtime.ts`.

## Preserved Surfaces (Must Not Regress)

See milestone spec § Preserved Surfaces for the full list. Key surfaces that must stay untouched:

- `IFlowTimeApiClient` and all Engine-client members — especially `GetRunIndexAsync`, `GetRunSeriesAsync`, `GetRunAsync`, `GetRunMetricsAsync`, `GetRunStateAsync`, `GetRunGraphAsync`, `GetRunStateWindowAsync`, `GetRunSummariesAsync`.
- `ApiRunClient.RunAsync`, `RunClientRouter.RunAsync`, `SimulationRunClient.RunAsync` (IRunClient members feeding the deferred Engine direct-eval path per D-2026-04-08-029).
- `FlowTimeSimApiClient.CreateRunAsync`, `HealthAsync`, `GetDetailedHealthAsync`, `GetTemplatesAsync`, `GetTemplateAsync`, `GenerateModelAsync` (row 63 supported surface).
- `FlowTimeSimApiClientWithFallback` class + `PortDiscoveryService` bootstrap (legit dev-environment port discovery — only pass-throughs for deleted interface members are removed).
- `FlowTimeSimService.RunDemoModeSimulationAsync` and every demo-data generator in `TemplateServiceImplementations.cs`.
- `SimResultsService.GetDemoModeResultsAsync` + `SimResultData` including its `BinMinutes` computed display property.
- `TemplateRunner.razor` flow-analysis path at line 744 (`IRunClient.RunAsync`) — untouched.
- Engine tests mocking `IFlowTimeApiClient`-like interfaces with method names `RunAsync`/`GetSeriesAsync` (`TimeTravelDataServiceTests.cs`, `DashboardTests.cs`, `ArtifactListRenderTests.cs`) — those are Engine members, not in scope.

## Implementation Log

### Preflight — 2026-04-08

Baseline `dotnet build FlowTime.sln` + `dotnet test FlowTime.sln` on `epic/E-19` HEAD (`c2fe669`): **build green** (1 pre-existing xUnit2031 warning in `ClassMetricsAggregatorTests.cs`, not introduced by this milestone); **1250 passed, 9 skipped, 0 failed** across all test projects.

Branch `milestone/m-E19-04-blazor-support-alignment` created off `epic/E-19`.

### Status-sync commit — pending

Files staged:
- `work/epics/E-19-surface-alignment-and-compatibility-cleanup/m-E19-04-blazor-support-alignment.md` (new spec, status `in-progress`)
- `work/epics/E-19-surface-alignment-and-compatibility-cleanup/m-E19-04-blazor-support-alignment-tracking.md` (this file)
- `work/epics/E-19-surface-alignment-and-compatibility-cleanup/spec.md` (status line + milestone table)
- `ROADMAP.md` (E-19 section status)
- `work/epics/epic-roadmap.md` (E-19 status + key milestones)
- `CLAUDE.md` (Current Work section)

Not staged: pre-existing uncommitted deletion in `work/agent-history/builder.md` is unrelated and left untouched. Will ask user about it separately (possibly at wrap time when appending m-E19-04 learnings).

## Test Summary

- **Baseline:** 1250 passed, 9 skipped, 0 failed
- **Current:** (status-sync commit only — no code changes yet)
- **Build:** green

## Notes

_Decisions made, issues encountered, deviations from spec — appended per bundle._

## Completion

- **Completed:** pending
- **Final test count:** pending
- **Deferred items:** (none yet)
