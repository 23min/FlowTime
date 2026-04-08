# Tracking: m-E19-04 Blazor Support Alignment

**Status:** in-progress
**Started:** 2026-04-08
**Epic:** [E-19 Surface Alignment & Compatibility Cleanup](./spec.md)
**Milestone spec:** [m-E19-04-blazor-support-alignment.md](./m-E19-04-blazor-support-alignment.md)
**Branch:** `milestone/m-E19-04-blazor-support-alignment` (off `epic/E-19`)
**Baseline test count (epic/E-19 head):** 1250 passed, 9 skipped, 0 failed
**Grep guards:** 11 tests planned in `scripts/m-E19-04-grep-guards.sh`

## Acceptance Criteria

- [x] **AC1.** Delete stale `RunAsync` wrapper from `IFlowTimeSimApiClient`, `FlowTimeSimApiClient`, and `FlowTimeSimApiClientWithFallback`. `SimRunResponse` DTO deleted (was only referenced by the stale wrapper). Preserved `ApiRunClient.RunAsync`, `RunClientRouter.RunAsync`, `SimulationRunClient.RunAsync` (IRunClient members, deferred Engine path). (Bundle A)
- [x] **AC2.** Delete stale `GetIndexAsync` and `GetSeriesAsync` wrappers from `IFlowTimeSimApiClient`, `FlowTimeSimApiClient`, and `FlowTimeSimApiClientWithFallback`. Preserved `IFlowTimeApiClient.GetRunIndexAsync`/`GetRunSeriesAsync` and the `SeriesIndex` type. (Bundle A)
- [x] **AC3.** Rewired `FlowTimeSimService.RunApiModeSimulationAsync` onto `simClient.CreateRunAsync(new RunCreateRequestDto(templateId, "simulation", parameters, ...))` — the Sim orchestration endpoint now owns template expansion for API mode. Rewired `FlowTimeSimService.GetRunStatusAsync` onto `apiClient.GetRunIndexAsync(...)` (Engine query path). Removed dead `ResultsUrl = "/{apiVersion}/sim/runs/{runId}/index"` assignment and, by extension, the `ResultsUrl` field from `SimulationRunResult` entirely (see Note 1 below). Deleted orphaned helpers `GenerateSimulationYamlAsync`, `TranslateToSimulationSchema`, `ConvertRequestToApiParameters` — all unreachable after the rewire. Dropped the `YamlDotNet.Serialization` + `YamlDotNet.Serialization.NamingConventions` usings from `TemplateServiceImplementations.cs` (only used by the deleted `TranslateToSimulationSchema`). Added `ConvertParametersToJsonElements` helper to convert `Dictionary<string, object>` → `Dictionary<string, JsonElement>?` for the `RunCreateRequestDto.Parameters` shape. (Bundle A)
- [x] **AC4.** Simplified `SimResultsService.GetSimulationResultsAsync` to use Engine API for every non-demo query (dropped the `isEngineRun` branch). Removed `IFlowTimeSimApiClient` constructor parameter; updated `Program.cs` DI registration. Demo mode preserved (both the `demo://` URL-scheme branch and the `UseDemoMode` feature-flag branch). (Bundle A)
- [x] **AC5.** Collapsed the dead demo-vs-API download-URL branch in `SimulationResults.razor.DownloadSeries` onto a single canonical Engine URL (`$"{apiConfig.BaseUrl}/{apiConfig.ApiVersion}/runs/{runId}/series/{seriesId}"`). Demo-mode download button now short-circuits with an informational notification — demo runs are synthetic in-memory data with no server file to download. Also replaced the three `ResultsUrl` sentinel checks at lines 57, 235, 407 with `RunId`-based checks, consistent with the `ResultsUrl` field removal (see Note 1). (Bundle A)
- [x] **AC6.** Row 63 alignment audit — confirmed `{BaseAddress, HealthAsync, GetDetailedHealthAsync, GetTemplatesAsync, GetTemplateAsync, GenerateModelAsync, CreateRunAsync}` are the only members on `IFlowTimeSimApiClient` after Bundle A. Each targets a live Sim route per the supported-surfaces sweep: `v1/healthz` (row 51), `v1/healthz?detailed=true` (row 51), `api/v1/templates` (row 52), `api/v1/templates/{id}` (row 52), `api/v1/templates/{id}/generate` (row 53), `api/v1/orchestration/runs` (row 54). **No drift.** No code change. (Bundle B)
- [x] **AC7.** Rows 66/67 alignment audit — confirmed `ui/src/lib/api/sim.ts` targets 6 supported Sim routes (`/api/v1/healthz`, `/api/v1/healthz?detailed=true`, `/api/v1/templates`, `/api/v1/templates/{id}`, `/api/v1/templates/categories`, `/api/v1/orchestration/runs`) with no stale `catalogs` / `drafts` / `bundle*` literals; `ui/src/lib/api/flowtime.ts` targets 12 supported Engine routes (`/healthz`, `/v1/healthz`, `/v1/runs`, `/v1/runs/{runId}`, `/v1/artifacts*`, `/v1/runs/{runId}/graph`, `/v1/runs/{runId}/state`, `/v1/runs/{runId}/index`, `/v1/runs/{runId}/state_window`) with no stale `POST /v1/runs` / `bundle*` / `/v1/debug/` literals. **No drift.** No code change. (Bundle B)
- [x] **AC8.** `scripts/m-E19-04-grep-guards.sh` created with 11 named guards (AC1–AC7 coverage); script exits 0 when all guards pass. 11/11 passing against ripgrep. Script mirrors `scripts/m-E19-03-grep-guards.sh` structure and adds a fail-fast `command -v rg` check at the top so silent no-ops on machines without ripgrep are turned into loud errors — see Note 5 below. (Bundle C)
- [ ] **AC9.** Full build + full test suite green, grep guards green, no new compiler warnings. (Wrap)
- [ ] **AC10.** Tracking doc finalized; status reconciled across spec, epic spec milestone table, ROADMAP.md, epic-roadmap.md, CLAUDE.md Current Work. (Wrap)

## Commit Plan (Bundles)

Per milestone spec Technical Notes — four focused commits plus the wrap.

- [ ] **Status-sync commit** (this doc + spec draft→in-progress + epic spec table + ROADMAP.md + epic-roadmap.md + CLAUDE.md) — runs before Bundle A.
- [x] **Bundle A** (AC1 + AC2 + AC3 + AC4 + AC5): stale Sim client deletion + caller rewire. Single conceptual cleanup; one commit pending. Result: `dotnet build` green with no new warnings; full solution test suite 1246 passed / 9 skipped / 0 failed (−4 from baseline due to deleted tests exercising deleted code — see Note 2 below).
- [x] **Bundle B** (AC6 + AC7): alignment audit. No drift found on any of the three audited rows (63, 66, 67). No code change; findings recorded in AC6/AC7 checkboxes above and folded into the Bundle C (grep-guard script) or wrap commit per spec.
- [x] **Bundle C** (AC8): grep-guard script. Its own commit pending. Script passes 11/11 against ripgrep on the tree produced by Bundle A (Bundle B was no-op).
- [ ] **Wrap** (AC9 + AC10): tracking doc finalization and status-surface reconciliation in a single commit after grep guards + build/test pass.

## Grep Guards

Each must return zero matches (or the expected count) after the milestone completes. Full script: `scripts/m-E19-04-grep-guards.sh`.

- [x] Guard 1: No `\bRunAsync\b` member on `IFlowTimeSimApiClient`/`FlowTimeSimApiClient`/`FlowTimeSimApiClientWithFallback` (word-boundary anchored so `CreateRunAsync` is not a false positive).
- [x] Guard 2: No `api/v1/run"` literal in the Sim client files.
- [x] Guard 3: No `GetIndexAsync` or `GetSeriesAsync` member on the Sim client files.
- [x] Guard 4: No Sim run-query path literal (`api/v1/runs/{` or `apiBasePath.*runs/`) in the Sim client files.
- [x] Guard 5: No `simClient.RunAsync(`, `simClient.GetIndexAsync(`, or `simClient.GetSeriesAsync(` call site in `TemplateServiceImplementations.cs`.
- [x] Guard 6: No `simClient.GetIndexAsync(` or `simClient.GetSeriesAsync(` call site in `SimResultsService.cs`.
- [x] Guard 7: No `IFlowTimeSimApiClient` dependency on the `SimResultsService` constructor signature.
- [x] Guard 8: No `/sim/runs/` URL literal anywhere in `src/FlowTime.UI/`.
- [x] Guard 9: `IFlowTimeSimApiClient` interface surface is exactly `{BaseAddress, HealthAsync, GetDetailedHealthAsync, GetTemplatesAsync, GetTemplateAsync, GenerateModelAsync, CreateRunAsync}` — enforced by awk-extracted interface block + rg allowlist filter. Sanity-checked against a simulated `RunAsync` regression; guard correctly flags the regression.
- [x] Guard 10: No `catalogs`, `drafts`, `bundlePath`, `bundleArchiveBase64`, or `bundleRef` literal in `ui/src/lib/api/sim.ts`.
- [x] Guard 11: No `POST /v1/runs`, `bundlePath`, `bundleArchiveBase64`, `bundleRef`, or `/v1/debug/` literal in `ui/src/lib/api/flowtime.ts`.

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

### Bundle A — implementation decisions

**Note 1: `ResultsUrl` field removed from `SimulationRunResult` entirely (scope widening).**

Spec AC3 asked only to remove the dead api-mode `ResultsUrl = "/{apiVersion}/sim/runs/{runId}/index"` assignment at line 987. At implementation time, `SimulationResults.razor` turned out to use `ResultsUrl` as a non-null sentinel in three guard checks (lines 57, 235, 407) that gate whether to auto-load / display simulation results. Leaving the field in place but setting it to `null` for api-mode would block all api-mode results from displaying — breaking the rewire. Two forward-only options:

1. Set api-mode `ResultsUrl` to a canonical Engine URL to keep the sentinel alive.
2. Drop `ResultsUrl` entirely from `SimulationRunResult` and replace the sentinel with `Status == "completed" && RunId != null`.

Chose option 2: `ResultsUrl` was always a redundant sentinel (the `Status == "completed"` check already conveyed the same signal), and the `demo://` URL stored on line 938 was never actually consumed — `SimResultsService` handles demo runs via the `UseDemoMode` feature flag and the `demo://` URL-scheme branch keys on `runId` format, not on `ResultsUrl`. Cleaner forward-only state: no dead sentinel, guard conditions consistent across three call sites, and `SimulationResults.razor.DownloadSeries` can use `Metadata["source"] == "demo"` (which it doesn't — the demo-mode check now routes via `FeatureFlags.UseDemoMode` instead, which is simpler).

**Note 2: 4 UI tests deleted alongside the helpers they exercised.**

Bundle A deleted `ConvertRequestToApiParameters` (which itself was marked "Temporary stub method to support existing tests during API integration transition. TODO: Remove this method and update tests once API integration is complete."), `GenerateSimulationYamlAsync`, and `TranslateToSimulationSchema`. Four tests reached into these helpers via reflection:

- `tests/FlowTime.UI.Tests/TemplateServiceParameterConversionTests.cs` — 3 tests, entire file deleted via `git rm`.
- `tests/FlowTime.UI.Tests/TemplateServiceMetadataTests.cs::TranslateToSimulationSchema_UsesFirstConstNodeForArrivals` + the file-local helpers `ReadSimulationSchema` and `InvokeTranslateToSimulationSchema` deleted; the rest of the file (3 theory tests exercising the still-live `GenerateSimulationYaml`) preserved unchanged.

These tests had no surviving production coverage to protect — they existed only to exercise the deleted helpers. Forward-only test deletion per the m-E19-02 pattern. Net test count: 1250 → 1246 passed.

**Note 3: `SimRunResponse` DTO deleted.**

Defined in `FlowTimeSimApiClient.cs:256-259` with a single `SimRunId` string field. Its only caller was the deleted `IFlowTimeSimApiClient.RunAsync` implementation. Not referenced in any surviving `src/` or `tests/` code. Deleted.

**Note 4: `FlowTimeSimApiClientWithFallback` retained — port discovery is legit.**

The class name was initially misleading ("Fallback" suggested a compatibility shim). On inspection, it is actually a port-discovery bootstrap (via `PortDiscoveryService.GetAvailableFlowTimeSimUrlAsync`) that picks the first available Sim service URL from a configurable list. That is legitimate dev-environment ergonomics, not a stale wrapper. Preserved unchanged; only the pass-through methods for the three deleted `IFlowTimeSimApiClient` members were removed.

### Bundle C — implementation decisions

**Note 5: Fail-fast `rg` availability check added to the m-E19-04 guard script (improvement over m-E19-03).**

While writing `scripts/m-E19-04-grep-guards.sh`, discovered that the m-E19-03 script silently no-ops on machines without `rg` on PATH: every guard wraps its `rg` call with `2>/dev/null || true`, so when `rg` is missing the command errors quietly, produces empty stdout, and the `check` helper interprets the empty string as "no matches found" → `PASS`. On this devcontainer `rg` is not installed (bundled ripgrep binaries exist under `/vscode/vscode-server/.../node_modules/@vscode/ripgrep/bin/rg` but are not on `$PATH`), so a naive run of either the m-E19-03 or m-E19-04 script reports all-PASS without actually checking anything.

The m-E19-04 script adds a `command -v rg` check at the top that exits with code 2 and a clear install hint (`apt-get install ripgrep` / `brew install ripgrep`) when ripgrep is missing. This turns silent no-ops into loud failures and preserves the invariant that "11/11 passing" actually means 11 guards verified their targets. The m-E19-03 script is not retroactively fixed — out of scope for m-E19-04.

Verification of the m-E19-04 script was performed by prepending the VS Code Server ripgrep directory to `$PATH` for the run, then sanity-checking Guard 9 (the surface-check) against a simulated `RunAsync` regression in a temp file. Guard 9 correctly flagged the regression. All 11 guards reported PASS on the actual milestone tree.

**Note 6: Guard 1 uses `\bRunAsync\b` word boundaries.**

First draft of Guard 1 used the literal `RunAsync` which also matched `CreateRunAsync` as a substring, producing 4 false positives on the row 63 supported `CreateRunAsync` method. Fixed by anchoring with `\b` word boundaries. Guards 3, 5, 6 use `GetIndexAsync` and `GetSeriesAsync` tokens that don't collide with any supported member, so they don't need word boundaries.

**Note 7: Guard 9 regex simplified from `Task<[^>]+>\s+(\w+Async)` to `\s(\w+Async)\(`.**

The original regex failed to match members with nested generic return types like `Task<Result<List<ApiTemplateInfo>>>` because `[^>]+` stops at the first `>`. Simplified to "whitespace, Async-ending word, open paren" which matches every interface method signature regardless of return type complexity.

## Completion

- **Completed:** pending
- **Final test count:** pending
- **Deferred items:** (none yet)
