#!/usr/bin/env bash
# m-E19-04 grep guards — assert that the stale FlowTime.UI Sim client wrappers
# (RunAsync, GetIndexAsync, GetSeriesAsync against removed/nonexistent Sim
# routes) stay deleted on the active Blazor surface, that their ripple effects
# in FlowTimeSimService, SimResultsService, and SimulationResults.razor remain
# cleaned up, and that both Svelte API clients stay aligned to current contracts.
#
# Scope: src/FlowTime.UI/, ui/src/lib/api/, tests/FlowTime.UI.Tests/.
# Explicitly excluded:
#   - src/FlowTime.*        non-UI projects — not this milestone's scope
#   - tests outside FlowTime.UI.Tests — Engine-client mocks whose method names
#       happen to collide with deleted Sim-client members (e.g. TimeTravel tests
#       mocking IFlowTimeApiClient.RunAsync) are not stale wrappers and are out
#       of scope per the spec's Preserved Surfaces list.
#   - docs/, work/          documentation is not touched by m-E19-04
#
# Run from the repo root:
#     bash scripts/m-E19-04-grep-guards.sh
#
# Exits 0 if every guard passes, 1 otherwise.

set -euo pipefail

cd "$(dirname "$0")/.."

# Fail fast if ripgrep is missing. Without this check, `rg ... || true` would
# silently return empty output for every guard and falsely report PASS — which
# would make the whole script a no-op on any machine where ripgrep isn't on PATH.
if ! command -v rg >/dev/null 2>&1; then
    printf 'ERROR: ripgrep (rg) is not on PATH.\n' >&2
    printf '       Install with: apt-get install ripgrep (Debian/Ubuntu) or brew install ripgrep (macOS).\n' >&2
    printf '       m-E19-04 grep guards cannot run without it.\n' >&2
    exit 2
fi

failed=0
total=0

# Each check takes a label and the output of an rg (possibly piped through
# grep -v for allowlists). Non-empty output means the guard regressed.
check() {
    local label="$1"
    local matches="$2"
    total=$((total + 1))
    if [[ -n "$matches" ]]; then
        printf 'FAIL  %s\n' "$label"
        printf '%s\n' "$matches" | sed 's/^/        /'
        failed=$((failed + 1))
    else
        printf 'PASS  %s\n' "$label"
    fi
}

# ----------------------------------------------------------------------------
# Guard 1 (AC1) — No RunAsync member on IFlowTimeSimApiClient interface or its
# implementations FlowTimeSimApiClient / FlowTimeSimApiClientWithFallback.
# The wrapper targeted removed Sim /api/v1/run and was deleted in Bundle A.
# ----------------------------------------------------------------------------
check "Guard 1 — no RunAsync member on IFlowTimeSimApiClient or its impls" \
    "$(rg --no-heading --line-number --with-filename '\bRunAsync\b' \
        src/FlowTime.UI/Services/FlowTimeSimApiClient.cs \
        src/FlowTime.UI/Services/FlowTimeSimApiClientWithFallback.cs 2>/dev/null || true)"

# ----------------------------------------------------------------------------
# Guard 2 (AC1) — No Sim /api/v1/run path literal in the Sim client files.
# The canonical supported Sim run-creation path is /api/v1/orchestration/runs
# via CreateRunAsync, not /api/v1/run.
# ----------------------------------------------------------------------------
check "Guard 2 — no Sim /api/v1/run path literal in Sim client files" \
    "$(rg --no-heading --line-number --with-filename -F 'api/v1/run"' \
        src/FlowTime.UI/Services/FlowTimeSimApiClient.cs \
        src/FlowTime.UI/Services/FlowTimeSimApiClientWithFallback.cs 2>/dev/null || true)"

# ----------------------------------------------------------------------------
# Guard 3 (AC2) — No GetIndexAsync or GetSeriesAsync member on
# IFlowTimeSimApiClient interface or its implementations. The wrappers targeted
# Sim routes that never existed (/api/v1/runs/{id}/index, /series/) and were
# deleted in Bundle A. Engine-side equivalents IFlowTimeApiClient.GetRunIndexAsync
# and GetRunSeriesAsync live in FlowTimeApiClient.cs and are preserved.
# ----------------------------------------------------------------------------
check "Guard 3 — no GetIndexAsync/GetSeriesAsync member on Sim client files" \
    "$(rg --no-heading --line-number --with-filename 'GetIndexAsync|GetSeriesAsync' \
        src/FlowTime.UI/Services/FlowTimeSimApiClient.cs \
        src/FlowTime.UI/Services/FlowTimeSimApiClientWithFallback.cs 2>/dev/null || true)"

# ----------------------------------------------------------------------------
# Guard 4 (AC2) — No Sim /api/v1/runs/{runId}/index or /series/ path literal
# in the Sim client files. Run queries belong on the Engine API.
# ----------------------------------------------------------------------------
check "Guard 4 — no Sim run-query path literals in Sim client files" \
    "$(rg --no-heading --line-number --with-filename 'api/v1/runs/\{|apiBasePath.*runs/' \
        src/FlowTime.UI/Services/FlowTimeSimApiClient.cs \
        src/FlowTime.UI/Services/FlowTimeSimApiClientWithFallback.cs 2>/dev/null || true)"

# ----------------------------------------------------------------------------
# Guard 5 (AC3) — No simClient.RunAsync / GetIndexAsync / GetSeriesAsync call
# site in TemplateServiceImplementations.cs. After Bundle A,
# FlowTimeSimService.RunApiModeSimulationAsync calls simClient.CreateRunAsync
# and GetRunStatusAsync calls apiClient.GetRunIndexAsync.
# ----------------------------------------------------------------------------
check "Guard 5 — no stale simClient.RunAsync/GetIndexAsync/GetSeriesAsync calls in TemplateServiceImplementations.cs" \
    "$(rg --no-heading --line-number --with-filename \
        'simClient\.(RunAsync|GetIndexAsync|GetSeriesAsync)\(' \
        src/FlowTime.UI/Services/TemplateServiceImplementations.cs 2>/dev/null || true)"

# ----------------------------------------------------------------------------
# Guard 6 (AC4) — No simClient.GetIndexAsync / GetSeriesAsync call site in
# SimResultsService.cs. After Bundle A, SimResultsService routes every non-demo
# query through apiClient.GetRunIndexAsync / GetRunSeriesAsync.
# ----------------------------------------------------------------------------
check "Guard 6 — no stale simClient.GetIndexAsync/GetSeriesAsync calls in SimResultsService.cs" \
    "$(rg --no-heading --line-number --with-filename \
        'simClient\.(GetIndexAsync|GetSeriesAsync)\(' \
        src/FlowTime.UI/Services/SimResultsService.cs 2>/dev/null || true)"

# ----------------------------------------------------------------------------
# Guard 7 (AC4) — No IFlowTimeSimApiClient dependency on the SimResultsService
# constructor signature. After Bundle A the ctor only takes IFlowTimeApiClient,
# FeatureFlagService, and ILogger.
# ----------------------------------------------------------------------------
check "Guard 7 — no IFlowTimeSimApiClient in SimResultsService constructor" \
    "$(rg --no-heading --line-number --with-filename \
        'public SimResultsService\(.*IFlowTimeSimApiClient' \
        src/FlowTime.UI/Services/SimResultsService.cs 2>/dev/null || true)"

# ----------------------------------------------------------------------------
# Guard 8 (AC3 + AC5) — No /sim/runs/ URL literal anywhere in the Blazor UI.
# Both the dead ResultsUrl assignment in TemplateServiceImplementations.cs and
# the dead demo-mode download branch in SimulationResults.razor were removed
# in Bundle A.
# ----------------------------------------------------------------------------
check "Guard 8 — no /sim/runs/ URL literal in src/FlowTime.UI/" \
    "$(rg --no-heading --line-number --with-filename -F '/sim/runs/' \
        src/FlowTime.UI 2>/dev/null || true)"

# ----------------------------------------------------------------------------
# Guard 9 (AC6) — IFlowTimeSimApiClient interface surface is exactly the
# row 63 supported set. Any extra member name on the interface is a regression.
# Allowed members: BaseAddress, HealthAsync, GetDetailedHealthAsync,
# GetTemplatesAsync, GetTemplateAsync, GenerateModelAsync, CreateRunAsync.
# This guard greps the interface body for any *Async member that is not in the
# allowed set.
# ----------------------------------------------------------------------------
check "Guard 9 — IFlowTimeSimApiClient interface surface is exactly the row 63 supported set" \
    "$(awk '/^public interface IFlowTimeSimApiClient/,/^}/' \
        src/FlowTime.UI/Services/FlowTimeSimApiClient.cs 2>/dev/null \
        | rg --no-filename '\s(\w+Async)\(' --only-matching --replace '$1' \
        | rg -v '^(HealthAsync|GetDetailedHealthAsync|GetTemplatesAsync|GetTemplateAsync|GenerateModelAsync|CreateRunAsync)$' \
        || true)"

# ----------------------------------------------------------------------------
# Guard 10 (AC7) — No stale literals in the Svelte Sim client. `catalogs`,
# `drafts`, `bundlePath`, `bundleArchiveBase64`, and `bundleRef` all belong to
# surfaces retired in m-E19-02 and must not reappear on the active Svelte Sim
# client. (The Svelte client file is intentionally small — ~44 lines.)
# ----------------------------------------------------------------------------
check "Guard 10 — no stale catalogs/drafts/bundle literals in ui/src/lib/api/sim.ts" \
    "$(rg --no-heading --line-number --with-filename \
        'catalogs|drafts|bundlePath|bundleArchiveBase64|bundleRef' \
        ui/src/lib/api/sim.ts 2>/dev/null || true)"

# ----------------------------------------------------------------------------
# Guard 11 (AC7) — No stale literals in the Svelte Engine client. `POST /v1/runs`
# (bundle-import route, deleted in m-E19-02), `bundlePath`, `bundleArchiveBase64`,
# `bundleRef`, and `/v1/debug/` (zombie debug route, deleted in m-E19-02) must
# not reappear on the active Svelte Engine client.
# ----------------------------------------------------------------------------
check "Guard 11 — no stale bundle/POST v1/runs/debug literals in ui/src/lib/api/flowtime.ts" \
    "$(rg --no-heading --line-number --with-filename \
        'POST /v1/runs|bundlePath|bundleArchiveBase64|bundleRef|/v1/debug/' \
        ui/src/lib/api/flowtime.ts 2>/dev/null || true)"

# ----------------------------------------------------------------------------
# Summary
# ----------------------------------------------------------------------------
printf '\n'
printf 'm-E19-04 grep guards: %d/%d passed\n' "$((total - failed))" "$total"

if [[ $failed -ne 0 ]]; then
    printf 'RESULT: FAIL — %d guard(s) regressed.\n' "$failed" >&2
    exit 1
fi

printf 'RESULT: PASS — m-E19-04 cleanup invariants hold.\n'
