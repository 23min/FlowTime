#!/usr/bin/env bash
# m-E19-03 grep guards — assert that the deprecated binMinutes authoring shape,
# schema-migration example fixtures, stale UI spec, catalog-stale phrasing, and
# stale test parameter keys all stay retired on the active first-party surfaces.
#
# Scope: src/, tests/, docs/, examples/, templates/, scripts/ — the active
# surfaces m-E19-03 owns. Explicitly excluded (see per-guard filters):
#   - docs/archive/        — historical archive, legitimate home for retired content
#   - docs/releases/       — release notes, historical
#   - docs/architecture/reviews/ — point-in-time review snapshots
#   - work/                — framework/tracking dirs outside the guard scope
#
# Unlike m-E19-02 (which only grepped src/ and tests/), m-E19-03 must include
# docs/ because several guards target specific docs (whitepaper, retry-modeling,
# UI.md, contracts.md, engine-capabilities.md). Each guard therefore scopes its
# search to the specific file or directory it owns.
#
# Run from the repo root:
#     bash scripts/m-E19-03-grep-guards.sh
#
# Exits 0 if every guard passes, 1 otherwise.

set -euo pipefail

cd "$(dirname "$0")/.."

# Fail fast if ripgrep is missing. Without this check, `rg ... || true` would
# silently return empty output for every guard and falsely report PASS — which
# would make the whole script a no-op on any machine where ripgrep isn't on PATH.
# Backported from scripts/m-E19-04-grep-guards.sh.
if ! command -v rg >/dev/null 2>&1; then
    printf 'ERROR: ripgrep (rg) is not on PATH.\n' >&2
    printf '       Install with: apt-get install ripgrep (Debian/Ubuntu) or brew install ripgrep (macOS).\n' >&2
    printf '       m-E19-03 grep guards cannot run without it.\n' >&2
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
# Guard 1 (AC1) — No binMinutes in the Blazor mock template service.
# The two JsonSchemaProperty entries and three demo YAML generators were
# rewritten to binSize/binUnit in Bundle A (commit dd61ca6).
# ----------------------------------------------------------------------------
check "Guard 1 — no binMinutes in TemplateServiceImplementations.cs" \
    "$(rg --no-heading --line-number --with-filename 'binMinutes' \
        src/FlowTime.UI/Services/TemplateServiceImplementations.cs 2>/dev/null || true)"

# ----------------------------------------------------------------------------
# Guard 2 (AC2) — No binMinutes in UI wwwroot fixtures.
# src/FlowTime.UI/wwwroot/sample/run-example.json was rewritten in Bundle A.
# ----------------------------------------------------------------------------
check "Guard 2 — no binMinutes in src/FlowTime.UI/wwwroot/" \
    "$(rg --no-heading --line-number --with-filename 'binMinutes' \
        src/FlowTime.UI/wwwroot 2>/dev/null || true)"

# ----------------------------------------------------------------------------
# Guard 3 (AC3) — No binMinutes in the Engine CLI project.
# Program.cs verbose label was rewritten to use binSize/binUnit in Bundle A.
# TimeGrid.BinMinutes (the live internal computed property) is in Core, not
# Cli, and is explicitly a preserved surface.
# ----------------------------------------------------------------------------
check "Guard 3 — no binMinutes in src/FlowTime.Cli/" \
    "$(rg --no-heading --line-number --with-filename 'binMinutes' \
        src/FlowTime.Cli 2>/dev/null || true)"

# ----------------------------------------------------------------------------
# Guard 4 (AC4) — No binMinutes in whitepaper.md except the Little's Law
# formula, which is derived-concept math notation and is explicitly allowlisted
# by an inline HTML comment marker.
# ----------------------------------------------------------------------------
check "Guard 4 — no binMinutes in whitepaper.md except allowlisted Little's Law notation" \
    "$(rg --no-heading --line-number --with-filename 'binMinutes' \
        docs/architecture/whitepaper.md 2>/dev/null \
        | grep -v 'm-E19-03:allow-binminutes-notation' || true)"

# ----------------------------------------------------------------------------
# Guard 5 (AC4) — No binMinutes in retry-modeling.md.
# Three YAML examples were rewritten to binSize/binUnit in Bundle B.
# ----------------------------------------------------------------------------
check "Guard 5 — no binMinutes in retry-modeling.md" \
    "$(rg --no-heading --line-number --with-filename 'binMinutes' \
        docs/architecture/retry-modeling.md 2>/dev/null || true)"

# ----------------------------------------------------------------------------
# Guard 6 (AC5) — No references to the schema-migration example YAMLs outside
# their archive locations. The three files moved to examples/archive/ in
# Bundle C. A path literal matching `examples/test-(old|no|new)-schema.yaml`
# will not match the archive paths (`examples/archive/...`) because of the
# intermediate `archive/` segment.
# ----------------------------------------------------------------------------
check "Guard 6 — no stale test-*-schema.yaml references outside archive" \
    "$(rg --no-heading --line-number --with-filename \
        'examples/(test-old-schema|test-no-schema|test-new-schema)\.yaml' \
        src tests docs examples templates scripts 2>/dev/null \
        | grep -v '^docs/archive/' \
        | grep -v '^examples/archive/' || true)"

# ----------------------------------------------------------------------------
# Guard 7 (AC6) — No references to the old docs/ui/template-integration-spec.md
# path outside the archive. The file moved to docs/archive/ui/ in Bundle C.
# Matching the literal `docs/ui/template-integration-spec` does not match the
# archive path `docs/archive/ui/template-integration-spec` because of the
# intermediate `archive/` segment.
# ----------------------------------------------------------------------------
check "Guard 7 — no template-integration-spec.md references outside archive" \
    "$(rg --no-heading --line-number --with-filename \
        --glob '!scripts/m-E19-03-grep-guards.sh' \
        'docs/ui/template-integration-spec' \
        src tests docs examples templates scripts 2>/dev/null \
        | grep -v '^docs/archive/' || true)"

# ----------------------------------------------------------------------------
# Guard 8 (AC6) — No pre-v1 `/api/templates/...` route literals outside
# archived documentation. Current supported template routes use
# `/api/v1/templates/...`; the pre-v1 prefix `/api/templates/` does not
# overlap with it as a substring (different path component structure).
# ----------------------------------------------------------------------------
check "Guard 8 — no pre-v1 /api/templates/ route literals outside archive" \
    "$(rg --no-heading --line-number --with-filename -F \
        --glob '!scripts/m-E19-03-grep-guards.sh' \
        '/api/templates/' \
        src tests docs examples templates scripts 2>/dev/null \
        | grep -v '^docs/archive/' \
        | grep -v '^docs/releases/' || true)"

# ----------------------------------------------------------------------------
# Guard 9 (AC7) — No `template/catalog` stale phrasing in active docs.
# UI.md and contracts.md were rewritten in Bundle B.
# ----------------------------------------------------------------------------
check "Guard 9 — no template/catalog literal in UI.md or contracts.md" \
    "$(rg --no-heading --line-number --with-filename -F 'template/catalog' \
        docs/guides/UI.md docs/reference/contracts.md 2>/dev/null || true)"

# ----------------------------------------------------------------------------
# Guard 10 (AC7) — No `catalog/export/import/registry` stale phrasing in
# engine-capabilities.md. The stale "no catalog/export/import/registry
# endpoints" fragment was removed in Bundle B (the Engine actually has both
# export and artifact registry routes, so the fragment was factually wrong
# beyond just the catalog issue).
# ----------------------------------------------------------------------------
check "Guard 10 — no catalog/export/import/registry literal in engine-capabilities.md" \
    "$(rg --no-heading --line-number --with-filename -F 'catalog/export/import/registry' \
        docs/reference/engine-capabilities.md 2>/dev/null || true)"

# ----------------------------------------------------------------------------
# Guard 11 (AC8) — No `["binMinutes"]` dictionary-key literal in the UI
# parameter-conversion integration tests. The three dict literals were renamed
# to `["binSize"]` in Bundle A.
# ----------------------------------------------------------------------------
check 'Guard 11 — no ["binMinutes"] dict key in ParameterConversionIntegrationTests.cs' \
    "$(rg --no-heading --line-number --with-filename -F '["binMinutes"]' \
        tests/FlowTime.UI.Tests/ParameterConversionIntegrationTests.cs 2>/dev/null || true)"

# ----------------------------------------------------------------------------
# Summary
# ----------------------------------------------------------------------------
printf '\n'
printf 'm-E19-03 grep guards: %d/%d passed\n' "$((total - failed))" "$total"

if [[ $failed -ne 0 ]]; then
    printf 'RESULT: FAIL — %d guard(s) regressed.\n' "$failed" >&2
    exit 1
fi

printf 'RESULT: PASS — m-E19-03 cleanup invariants hold.\n'
