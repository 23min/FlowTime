#!/usr/bin/env bash
# m-E19-02 grep guards — assert that every deleted symbol from AC1–AC7 stays deleted.
#
# Scope: src/ and tests/ only. Excludes docs/, work/, and archive locations
# (historical/documentation surfaces can and should still reference the deleted
# concepts — only runtime code and tests must stay clean).
#
# Run from the repo root:
#     bash scripts/m-E19-02-grep-guards.sh
#
# Exits 0 if every guard passes, 1 otherwise.
#
# Narrowed scope notes:
# - AC6 `/v1/run` and `/v1/graph` deletion is deferred per D-2026-04-08-029.
#   This script does NOT guard against those routes. See `work/gaps.md`
#   "Deferred deletion: Engine POST /v1/run and POST /v1/graph".

set -euo pipefail

cd "$(dirname "$0")/.."

# Fail fast if ripgrep is missing. Without this check, `rg ... || true` would
# silently return empty output for every guard and falsely report PASS — which
# would make the whole script a no-op on any machine where ripgrep isn't on PATH.
# Backported from scripts/m-E19-04-grep-guards.sh.
if ! command -v rg >/dev/null 2>&1; then
    printf 'ERROR: ripgrep (rg) is not on PATH.\n' >&2
    printf '       Install with: apt-get install ripgrep (Debian/Ubuntu) or brew install ripgrep (macOS).\n' >&2
    printf '       m-E19-02 grep guards cannot run without it.\n' >&2
    exit 2
fi

# Each guard: "<label>|<pattern>"
# The pattern is passed to ripgrep as a regular expression.
guards=(
    # AC1 — stored drafts CRUD (A2)
    "AC1 drafts CRUD handlers|drafts/\{draftId"
    "AC1 StorageKind.Draft|StorageKind\.Draft"
    "AC1 data/storage/drafts directory|data/storage/drafts"
    "AC1 stored-draft request/response DTOs|DraftCreateRequest|DraftUpdateRequest|DraftWriteResponse|DraftListResponse|DraftSummary"

    # AC2 — /drafts/run narrowed to inline only.
    # NOTE: Originally guarded with pattern "\"draftId\"|\"draftid\"" but that
    # was too broad to express the real invariant. AC2's actual requirement is
    # "no draftId on /drafts/run specifically", and the preserved
    # /api/v1/drafts/map-profile endpoint legitimately uses ["draftId"] = draftId
    # in its response. A global grep cannot distinguish the two handlers, so
    # the guard was dropped rather than allowlisted (which would hide real
    # regressions in /drafts/run). AC2 invariant is still enforced at
    # build/test time — the /drafts/run handler body no longer resolves
    # DraftSource.type == "draftId" and the tests verify inline-only behavior.

    # AC3 — /api/v1/drafts/validate deleted (A6)
    "AC3 drafts/validate handler literal|drafts/validate"

    # AC4 — Sim ZIP archive layer (A3)
    "AC4 StorageKind.Run|StorageKind\.Run"
    "AC4 BundleRef|BundleRef"
    "AC4 data/storage/runs directory|data/storage/runs"
    "AC4 BuildRunArchive helper|BuildRunArchive"

    # AC5 — Engine POST /v1/runs + bundle-import (A4)
    "AC5 POST /v1/runs HandleCreateRunAsync on Engine|MapPost\(\"/runs\", HandleCreateRunAsync\)"
    "AC5 BundlePath field|BundlePath|bundlePath"
    "AC5 BundleArchiveBase64 field|BundleArchiveBase64|bundleArchiveBase64"
    "AC5 RunImportRequest class|RunImportRequest"
    "AC5 ExtractArchiveAsync helper|ExtractArchiveAsync"

    # AC6 (narrowed) — Engine debug scan-directory route
    "AC6 /v1/debug/scan-directory route|/v1/debug/scan-directory"

    # AC7 — Catalogs retired entirely (A5)
    "AC7 /api/v1/catalogs routes|/api/v1/catalogs"
    "AC7 CatalogService class|CatalogService"
    "AC7 ICatalogService interface|ICatalogService"
    "AC7 CatalogPicker razor component|CatalogPicker"
    "AC7 CatalogId = \"default\" placeholder|CatalogId = \"default\""
)

# AC5 exception: the Sim orchestration endpoint uses the literal
# MapPost("/runs", HandleCreateRunAsync) on its supported /api/v1/orchestration/runs
# surface. The grep guard above targets the Engine, not Sim, so we explicitly
# allow matches in the Sim extensions file.
allowed_paths=(
    "src/FlowTime.Sim.Service/Extensions/RunOrchestrationEndpointExtensions.cs"
)

# AC7 exception: ClassCatalogEntry / MetricProvenanceCatalog / NodeCatalog / modelCatalog
# are different concepts preserved on purpose. The catalog greps above are
# specific enough (CatalogService, ICatalogService, CatalogPicker,
# `/api/v1/catalogs`, `CatalogId = "default"`) that they do not match these
# preserved names, so no additional allowlist is needed.

failed=0
total=0

for guard in "${guards[@]}"; do
    label="${guard%%|*}"
    pattern="${guard#*|}"
    total=$((total + 1))

    # Collect matches in src/ and tests/, strip any allowed paths.
    matches=$(rg --no-heading --line-number --with-filename "$pattern" src tests 2>/dev/null || true)

    if [[ -n "$matches" ]]; then
        filtered="$matches"
        for allowed in "${allowed_paths[@]}"; do
            filtered=$(printf '%s\n' "$filtered" | grep -v "^$allowed:" || true)
        done

        if [[ -n "$filtered" ]]; then
            printf 'FAIL  %s\n' "$label"
            printf '%s\n' "$filtered" | sed 's/^/        /'
            failed=$((failed + 1))
            continue
        fi
    fi

    printf 'PASS  %s\n' "$label"
done

printf '\n'
printf 'm-E19-02 grep guards: %d/%d passed\n' "$((total - failed))" "$total"

if [[ $failed -ne 0 ]]; then
    printf 'RESULT: FAIL — %d guard(s) regressed.\n' "$failed" >&2
    exit 1
fi

printf 'RESULT: PASS — every deleted symbol stays deleted.\n'
