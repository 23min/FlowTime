---
id: G-015
title: Silent no-op + self-match false positives in m-E19-02 and m-E19-03 grep-guard scripts
status: addressed
addressed_by:
  - E-19
---

### Why this was a gap

Discovered during the `epic/E-19 → main` merge sanity check (2026-04-08). Running the three E-19 grep-guard scripts with ripgrep explicitly on `$PATH`:

- `scripts/m-E19-04-grep-guards.sh` — 11/11 passing
- `scripts/m-E19-03-grep-guards.sh` — 9/11 passing (2 false-positive self-matches)
- `scripts/m-E19-02-grep-guards.sh` — 20/21 passing (1 false-positive self-match)

All three scripts were authored with the intent of being runnable locally and in the wrap pass. The problem has two layers:

1. **Silent no-op when `rg` is missing from `$PATH`.** Every guard in the M-025 and M-026 scripts wraps its `rg` invocation with `2>/dev/null || true`. On a machine without ripgrep on `$PATH` (for example the devcontainer this codebase is currently developed in — ripgrep exists under `/vscode/vscode-server/bin/.../node_modules/@vscode/ripgrep/bin/rg` but is not linked onto `$PATH`), every `rg` call errors, stderr is swallowed, `|| true` absorbs the non-zero exit, and the command substitution returns the empty string. The `check "<label>" "$matches"` helper treats empty `matches` as PASS. Result: every guard silently reports PASS without actually inspecting the tree. When the M-025 and M-026 milestones recorded "21/21 passing" and "11/11 passing" in their tracking docs, those counts were true against an environment with ripgrep available but not against the devcontainer where the milestones were actually wrapped. The issue was not caught at wrap time because the false positives also pass-through silently.

2. **Self-match false positives when ripgrep IS available.** With ripgrep on `$PATH`, the scripts surface their own hits because their default search scope (`src tests docs examples templates scripts`) includes `scripts/` — and each guard's script code contains the forbidden literal in comments explaining the guard. On top of that, M-026's Guard 8 also self-matches `docs/architecture/supported-surfaces.md:83`, which contains the literal `/api/templates/` inside the quoted description "No current docs reference `/api/templates/` or other pre-v1 template routes" — a row that DESCRIBES the invariant, not a violation of it.

Specific false-positive findings:

| Script | Guard | False-positive source |
|--------|-------|-----------------------|
| `scripts/m-E19-02-grep-guards.sh` | AC2 draftId source type literal | Self-match in the script's own comment |
| `scripts/m-E19-03-grep-guards.sh` | Guard 7 `docs/ui/template-integration-spec` | Self-match in the script's own comment at lines 105/107/113 |
| `scripts/m-E19-03-grep-guards.sh` | Guard 8 `/api/templates/` pre-v1 route literal | Self-match in the script's own comment at lines 118/120/123/124 + description row in `docs/architecture/supported-surfaces.md:83` |

Neither layer represents a real regression on the `main` tree: every underlying M-025 and M-026 cleanup invariant (stored drafts gone, ZIP archive gone, `POST /v1/runs` bundle-import gone, catalogs gone, `binMinutes` authoring shape gone, schema-migration examples archived, `docs/ui/template-integration-spec.md` archived, catalog-stale phrasing cleaned) is in fact intact on current `main`, verified by hand.

### Status

**Resolved 2026-04-08** via `chore/grep-guard-cleanup` patch branch. All three scripts now run correctly:

- `scripts/m-E19-02-grep-guards.sh` — 20/20 passing
- `scripts/m-E19-03-grep-guards.sh` — 11/11 passing
- `scripts/m-E19-04-grep-guards.sh` — 11/11 passing (unchanged; already correct)

All three scripts now fail fast with exit code 2 and an install hint when ripgrep is missing from `$PATH`.

### Resolution

1. **Fail-fast `command -v rg` check** backported from `scripts/m-E19-04-grep-guards.sh` to the M-025 and M-026 scripts. Silent no-ops on machines without ripgrep are now loud failures with an install hint.
2. **`scripts/m-E19-03-grep-guards.sh` Guards 7 and 8** gained `--glob '!scripts/m-E19-03-grep-guards.sh'` exclusions so the script no longer self-matches its own explanatory comments. Guard 6 was left alone — it uses a regex that doesn't match any literal in the script body, so it can't self-match.
3. **`scripts/m-E19-02-grep-guards.sh` AC2 draftId source type literal guard dropped entirely.** The pattern `"draftId"|"draftid"` was too broad to express AC2's real invariant ("no `draftId` on `/drafts/run` specifically"). The preserved `/api/v1/drafts/map-profile` endpoint legitimately returns `["draftId"] = draftId` at `src/FlowTime.Sim.Service/Program.cs:932`, and a simple global grep cannot distinguish the two handlers. Allowlisting `Program.cs` would hide real regressions in `/drafts/run`, so dropping the guard is safer. AC2's invariant is still enforced at build/test time — the `/drafts/run` handler body no longer resolves `DraftSource.type == "draftId"` and the tests verify inline-only behavior. A `NOTE:` comment replaces the dropped guard line documenting the rationale. M-025 is now 20 guards instead of 21.
4. **`docs/architecture/supported-surfaces.md:83`** reworded from `"No current docs reference \`/api/templates/\` or other pre-v1 template routes"` to `"No current docs reference the pre-v1 template routes"` — drops the forbidden literal from the description row while preserving meaning.

### Retained learning

- **When writing future grep-guard scripts, use the M-027 template** (fail-fast `command -v rg` + `--glob` exclusions that keep the script body out of its own search path) as the reference.
- **Global grep patterns cannot enforce handler-scoped invariants.** If an AC's real invariant is "no X in handler Y", a global grep across the containing file will false-positive on any other handler that legitimately uses X. Either scope the search with a more precise tool (AST-based, or a scoped extraction like the awk block extraction used in M-027 Guard 9) or accept that the invariant is not expressible as a simple grep and rely on build/test coverage instead.
- **If the guards are eventually wired into CI**, the CI image must have ripgrep installed (`apt-get install ripgrep` on Debian/Ubuntu) — otherwise CI fail-fasts (exit code 2) which is the correct behavior. Silent no-op is no longer possible.
- **Trust completed-milestone "N/N passing" counts** from M-025 (now 20/20) and M-026 (now 11/11) as of this fix, but not retroactively — the earlier tracking-doc counts pre-date the fail-fast check.

---
