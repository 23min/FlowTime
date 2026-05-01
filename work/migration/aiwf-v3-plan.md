# aiwf v3 migration plan

Living plan for re-platforming this repo from the v1 AI framework (`.ai/` submodule + `bash sync.sh` + generated `.claude/`/`.github/` adapters) to v3 (`aiwf` Go binary + `ai-workflow-rituals` Claude Code plugin marketplace).

Drafted **2026-05-01** on branch `migration/aiwf-v3`. Source-of-truth for migration sequencing while v1 is being torn down. Append findings as we work; finalize and archive to `work/migration/completed/` once main has cut over.

---

## Why this is a re-platform, not an upgrade

v1 ships rituals, validators, agents, skills as a single bundle synced via bash. v3 splits the layer:

- **`aiwf` core** — single Go binary (`go install github.com/23min/ai-workflow-v2/tools/cmd/aiwf@poc/aiwf-v3`). Owns 6 entity kinds (epic, milestone, ADR, gap, decision, contract), validators, pre-push hook, structured commit trailers, `aiwf history` from `git log`, `aiwf import` for bulk creation. Ships 6 embedded `aiwf-*` skills materialized to `.claude/skills/aiwf-*/` (gitignored cache).
- **`ai-workflow-rituals` plugin marketplace** — companion repo, two Claude Code plugins:
  - `aiwf-extensions` — milestone-lifecycle skills (`aiwfx-*`) + 4 role agents (planner/builder/reviewer/deployer) + templates. Coupled to aiwf vocabulary.
  - `wf-rituals` — generic engineering rituals (`wf-patch`, `wf-tdd-cycle`, `wf-review-code`, `wf-doc-lint`). Aiwf-agnostic by discipline.
- **Repo-private skills** — live directly in `.claude/skills/<name>/SKILL.md`, committed normally. No prefix. v1's `.ai-repo/skills/` workaround disappears.

Conceptual identity (epics in `work/epics/E-NN/`, ADRs in `docs/`, commits track lifecycle) is preserved. Mechanism is completely different.

---

## Decision log (settled during planning)

| # | Decision | Settled |
|---|---|---|
| 1 | Re-platform via v3, not stay on v1 | yes |
| 2 | Repo-private skills land at `.claude/skills/<name>/` (committed); no `.ai-repo/skills/` carry-forward | yes |
| 3 | `dead-code-audit`, `devcontainer`, `ui-debug` ported as repo-private skills at migration time | yes |
| 4 | `verify-contracts` ported when first aiwf `contract` entity lands (not at migration); `design-contract` deferred until schema-evolution work begins | yes |
| 5 | `quality-score`, `doc-garden` deferred until real friction (likely never) | yes |
| 6 | Zero contract entities migrated — repo has none today | yes |
| 7 | Migration runs on long-lived `migration/aiwf-v3` branch off `main`; pre-push hook deferred until cutover so `main` keeps pushing | yes |
| 8 | Plan lives at `work/migration/aiwf-v3-plan.md` (not an epic); archive on completion | yes |

---

## Open questions (must settle before Phase 2 projector code lands)

1. **History scope.** ✅ **Settled — hybrid: project everything with an E-NN id, best-effort on completed.**
   - **Active:** 6 epics in `work/epics/E-*` (E-11, E-13, E-14, E-15, E-18, E-22) — full projection, all milestones, frontmatter reconstructed properly.
   - **Completed (id'd):** 9 epics in `work/epics/completed/E-*` (E-10, E-12, E-16, E-17, E-19, E-20, E-21, E-23, E-24) — best-effort projection. Missing/ambiguous fields get sensible defaults; failures logged in `skip-log.md`, don't block import.
   - **Completed (no id):** 16 dirs without E-NN prefix (`ai/`, `core-foundations/`, `time-travel/`, etc.) — **excluded** and **relocated** to `work/archived-epics/` during Phase 3 pre-processing. Pre-date the E-NN convention; aiwf doesn't model them. Kept on disk for grep/git-blame; out of aiwf's walked roots so `aiwf check` ignores them.
   - **Unplanned:** `work/epics/unplanned/` — only project subdirs that have an E-NN prefix (none currently — all 13 are slug-only, so all excluded).
   - **Best-effort discipline.** Projector flags rather than fails on: missing status, missing parent on milestone, milestone IDs that don't fit `m-EXX-NN-...` shape, dangling depends-on refs. Each becomes a `skip-log.md` entry; we triage in Phase 4 dry-run loop and decide per-finding whether to fix the source or accept the projection.

2. **ID stability.** ✅ **Settled — best-effort per-epic preprocessing + chronological renumber for D/G.**

   **Strategy.** Pin epic ids (already match v3); produce explicit, human-meaningful ids for milestones, decisions, and gaps via per-kind deterministic rules; rewrite body cross-references via id-map.csv (committed permanently to `work/migration/id-map.csv`).

   **Per-epic preprocessing config.** Each id'd epic gets a small per-epic config block in the projector. Most are 3-line generic ("epic spec at `spec.md`, milestones match `m-E{NN}-NN-<slug>.md`"); 3 outliers need custom rules.

   | Epic | Epic spec | Milestone shape | Notes |
   |---|---|---|---|
   | E-13, E-14, E-15, E-22 | `spec.md` | n/a (no milestones yet) or `m-E{NN}-NN-...` | generic |
   | E-16, E-17, E-19, E-20, E-21, E-23, E-24, E-18 | `spec.md` | `m-E{NN}-NN-<slug>.md` | generic; tracking/log files siblings |
   | **E-10** | check; uses `m-ec-pN-<slug>.md` | custom milestone-id rule (`m-ec-pN-...`) | special |
   | **E-11** | no `spec.md`; loose layout | milestones use `m-svui-NN-...` | special; pick representative file or synthesize body |
   | **E-12** | `spec.md` (verify) | `M-10.NN-<slug>.md` (capitalized + dotted) | special |

   **Milestone ids.** ~~Use `id: auto`~~ **Use explicit `M-NNN` computed by the projector** in deterministic order (epic-id ascending, then milestone-old-id ascending within epic). Revision rationale (Pass C, 2026-05-01): aiwf manifest reference fields (e.g. `depends_on`) can resolve against existing-tree ids OR manifest-declared ids — but `auto` entries don't declare an id at manifest-time, so intra-manifest cross-references like m-E18-02's `depends_on: m-E18-01` cannot resolve. Switching to projector-computed explicit M-NNN sidesteps this and makes id-map.csv a direct projector emit (no parsing of aiwf allocation output needed). Mapping captured in `id-map.csv`. Body cross-references rewritten via the map.

   **Decision ids.** 54 entries in `work/decisions.md`, all explicit `D-YYYY-MM-DD-NNN:` headings. Sort tuple `(date, NNN)` → assign `D-001..D-054` chronologically. Body references in specs/tracking-docs/`CLAUDE.md`/memory get rewritten via id-map.

   **Gap ids.** ~~157 entries~~ **33 H2 entries** in `work/gaps.md` (the earlier 157 figure was an over-count via `grep -c '^##'` which catches H3+ too — H2-only count is 33 + 1 "Open Questions" footer). Only 6 have explicit `(resolved YYYY-MM-DD)` suffix in title (resolution date, not creation date). Use **`git blame`** on each H2 line to recover the entry's creation date (the commit that introduced it). Sort by (blame-date, blame-commit-time) → assign `G-001..G-033`. Verified by sampling: blame produces clean per-entry dates ranging 2026-03-24..2026-04-28; doc order is unreliable (recent entries inserted at top).

   **Body rewriting.** Projector substitutes old → new ids in body text. Identifier classes to handle: `m-E\d+-\d+-[a-z-]+`, `M-\d+\.\d+-[a-z-]+` (E-12 dotted), `m-ec-p\d-[a-z-]+` (E-10), `m-svui-\d+-[a-z-]+` (E-11), `D-\d{4}-\d{2}-\d{2}-\d+` (decisions), `ADR-m-E\d+-\d+-\d+` (a few epic-local ADRs noted in catalog). Plus prose like "the session-evaluator milestone" — left as-is; aiwf doesn't parse prose.

   **Source untouched until Phase 5.** Projector reads `work/` read-only; the rewrites land only in the manifest body strings. Source is rewritten en-masse only at cutover (Phase 5) when v1 layout teardown happens anyway.

3. **ADR path.** ✅ **Settled — adopt v3 default `docs/adr/`.**
   - First ADR lands at `docs/adr/ADR-0001-<slug>.md`. No `aiwf.yaml` override.
   - Empty `docs/decisions/` directory deletes in Phase 5 v1 teardown (one `git rm -r`).
   - Going forward, `aiwfx-record-decision` skill picks ADR vs. D-NNN in-flow per the kind's conventions; path is then mechanical.

4. **Pre-push hook timing.** ✅ **Settled — gap closed upstream; install whenever convenient.**
   - Upstream landed a self-guarding hook on `poc/aiwf-v3` (commit on or before 53393ed): the embedded pre-push script's first content line is `[ -f "$(git rev-parse --show-toplevel)/aiwf.yaml" ] || exit 0`, plus a bonus `Options.SkipHook` flag for husky/lefthook composition. Pushes from any branch lacking `aiwf.yaml` silently no-op.
   - **Implication for our sequence.** We can run `aiwf init` as early as Phase 1 (right after `go install`) on the migration branch without breaking `main` pushes. The hook simply doesn't fire on `main` until `aiwf.yaml` arrives via merge.
   - **Practical choice.** Run `aiwf init` near the end of Phase 4 anyway — after the import lands and the tree validates — so the first time the hook fires it's against a known-clean projection. Earlier-install gives no extra signal since `aiwf check` is also available as a CLI we can run manually during dry-run iteration.

5. **`.claude/settings.json` plugin pin scope.** ✅ **Settled — Project scope.**
   - `/plugin install aiwf-extensions@ai-workflow-rituals` and `/plugin install wf-rituals@ai-workflow-rituals` both pin into the committed `.claude/settings.json`. Migration commit includes the diff; collaborators get the same plugins after a one-time `/plugin marketplace add` on first clone.
   - Aligns with existing usage of `.claude/settings.json` (statusline + team-wide hooks).
   - **Side-note flagged for Phase 5.** Existing SessionStart hooks in `.claude/settings.json` reference v1 paths (`.ai/tools/scratch-audit.sh`, `.ai-repo/bin/wf-graph`) — these break at v1 teardown and need to be removed or replaced. Added to the Phase 5 checklist below.

6. **Devcontainer Go install.** ✅ **Settled — devcontainer feature + `postCreateCommand`, branch-tip pin.**
   - `.devcontainer/devcontainer.json` gets:
     ```jsonc
     "features": {
       "ghcr.io/devcontainers/features/go:1": { "version": "1.22" }
     },
     "postCreateCommand": "go install github.com/23min/ai-workflow-v2/tools/cmd/aiwf@poc/aiwf-v3"
     ```
   - Branch-tip pin (`@poc/aiwf-v3`) is deliberate during the migration window: PoC is actively iterating, and we want upstream fixes (like the Q4 brownfield hook fix) automatically on rebuild.
   - When v3 stabilizes (PoC → tagged release), bump to a fixed version in one line.
   - **One-time pain.** Existing devs need to rebuild the container to pick up Go + aiwf. Acceptable; happens at start of Phase 1.

---

## Skills inventory and disposition

### Upstream (no porting needed)

| Our v1 skill | v3 destination | Notes |
|---|---|---|
| `wf-plan-epic` | `aiwfx-plan-epic` | shipped in `aiwf-extensions` plugin |
| `wf-plan-milestones` | `aiwfx-plan-milestones` | shipped |
| `wf-start-milestone` | `aiwfx-start-milestone` | shipped |
| `wf-wrap-milestone` | `aiwfx-wrap-milestone` | shipped |
| `wf-wrap-epic` | `aiwfx-wrap-epic` | shipped |
| `wf-release` | `aiwfx-release` | shipped |
| `wf-architect` | `aiwfx-record-decision` | shipped, replaces v1 architect |
| `wf-draft-spec` | folded into `aiwfx-plan-milestones` | confirm during Phase 1 audit |
| `wf-patch` | `wf-patch` (wf-rituals) | shipped |
| `wf-tdd-cycle` | `wf-tdd-cycle` (wf-rituals) | shipped |
| `wf-review-code` | `wf-review-code` (wf-rituals) | shipped |
| `wf-doc-lint` | `wf-doc-lint` (wf-rituals) | shipped |

### Repo-private at migration time

Port from `.ai/skills/` (or `.ai-repo/skills/`) into `.claude/skills/<name>/SKILL.md`, committed.

| Skill | Source | Lands at |
|---|---|---|
| `dead-code-audit` | `.ai/skills/dead-code-audit.md` | `.claude/skills/dead-code-audit/SKILL.md` + `recipes/dead-code-{dotnet,rust,typescript}.md` |
| `devcontainer` | `.ai-repo/skills/devcontainer.md` | `.claude/skills/devcontainer/SKILL.md` |
| `ui-debug` | `.ai-repo/skills/ui-debug.md` | `.claude/skills/ui-debug/SKILL.md` |

### Repo-private later

| Skill | When |
|---|---|
| `verify-contracts` | when first aiwf contract entity is registered (post-migration) |
| `design-contract` | when schema-evolution work begins (post-migration) |
| `quality-score`, `doc-garden` | only if real friction shows up |

### Dropped entirely

| v1 skill | Replaced by |
|---|---|
| `wf-workflow-graph` | aiwf core (validators + history) |
| `wf-workflow-audit` | `aiwf check` + `aiwf doctor` |
| `wf-update-framework` | `aiwf update` + `/plugin update` |
| `wf-devcontainer` | repo-private `devcontainer` already covers this |

### Agents

All four (`planner`, `builder`, `reviewer`, `deployer`) ship in `aiwf-extensions` plugin. Local `.claude/agents/*.md` files delete at cutover.

---

## Branching strategy

```
main ────────────────────────────────────────►
       │
       └── migration/aiwf-v3 ────►
                  │
                  └── feature branches if useful
                       (projector, gaps-split, decisions-split)
```

Side-effect scopes:

| Side effect | Scope | Handling |
|---|---|---|
| `go install` | global to machine (`~/go/bin/aiwf`) | install once via devcontainer feature |
| `/plugin install` rituals plugin | global to Claude Code user (cache) + branch-scoped `.claude/settings.json` | settings.json normal git; cache is harmless |
| `aiwf init` writes `aiwf.yaml`, `.gitignore` | working tree — branch-scoped | run on migration branch only |
| `aiwf init` writes `.git/hooks/pre-push` | **NOT branch-scoped** — `.git/hooks/` applies to every branch in this clone | **defer until late Phase 4** so `main` keeps pushing |
| Plugin marketplace add | per-machine Claude Code config | one-time, harmless |

The pre-push hook is the only real footgun. We use the binary's read-only verbs (`aiwf check`, `aiwf import --dry-run`) without ever running `aiwf init`, until we're ready to lock in.

---

## Phased checklist

Status legend: `[ ]` not started · `[~]` in progress · `[x]` done · `[-]` skipped

### Phase 0 — planning and inventory

- [x] Read v3 docs (`README`, `poc-design-decisions`, `poc-import-format`, `poc-migrating-from-prior-systems`, `rituals-plugin-plan`, `design-lessons`)
- [x] Inventory v1 skills × v3 destinations (above)
- [x] Confirm zero existing aiwf contract entities to migrate
- [x] Decide repo-private skill list and timing (`dead-code-audit` + `devcontainer` + `ui-debug` at migration; contracts later)
- [x] Branching strategy settled
- [x] Plan doc drafted (this file)
- [x] Settle 6 open questions above (Q1–Q6 ✅ 2026-05-01)

### Phase 1 — sandbox install

- [x] Add Go to devcontainer (feature `ghcr.io/devcontainers/features/go:1`); rebuild container — Go 1.22.10 in
- [x] Verify `go version` and `~/go/bin` on `$PATH` — Go OK; PATH needed a fix (hardcode `/home/vscode/...` instead of `${containerEnv:HOME}` which didn't resolve)
- [x] Install aiwf — landed via `init.sh` (resolves branch tip via `git ls-remote` then `go install @<sha>`, since `go install @poc/aiwf-v3` rejects slash-named branches)
- [x] Verify `aiwf --version` (`dev`) and `aiwf doctor --self-check` — **22 steps green**
- [x] `aiwf doctor` against current repo — confirms brownfield state cleanly: no `aiwf.yaml`, 8 skills not yet materialized, hook + pre-commit missing, plugin not detected. All expected.
- [x] `/plugin marketplace add 23min/ai-workflow-rituals` (User scope; cache populated at `~/.claude/plugins/cache/ai-workflow-rituals/`)
- [x] `/plugin install aiwf-extensions@ai-workflow-rituals` — landed via manual `enabledPlugins` edit in `.claude/settings.json` (Path C; `/plugin install` failed due to plugin manager state-confusion across projects, see migration log)
- [x] `/plugin install wf-rituals@ai-workflow-rituals` — same mechanism, same edit
- [x] Confirmed via `aiwf doctor` — reports "rituals plugin detected (aiwf-extensions in .claude/settings)"
- [x] Audit each shipped `aiwfx-*` and `wf-*` skill body for coverage gaps — done from cache, see findings below
- [ ] **Do NOT run `aiwf init` yet** — defer to end of Phase 4 against a clean tree

**Skill-audit findings (read from plugin cache, before install):**

Shipped surface verified present and feature-complete:
- `aiwf-extensions`: 8 skills (`aiwfx-{plan-epic, plan-milestones, start-milestone, wrap-milestone, wrap-epic, release, record-decision, track}`), 4 agents (`builder, planner, reviewer, deployer`), 5 templates (`epic-spec, milestone-spec, tracking-doc, adr, decision`).
- `wf-rituals`: 4 skills (`wf-{patch, tdd-cycle, review-code, doc-lint}`).
- Plugin commit pinned: `e556ec9215c5` (cache subdir).

Coverage gaps and mitigations:

| Gap | Detail | Mitigation |
|---|---|---|
| **`aiwfx-wrap-milestone` does not chain `dead-code-audit`** | Shipped wrap-milestone has step 3 doc-lint but no extension hook for additional audits. Our v1 wrap-milestone invoked `wf-dead-code-audit` as a non-blocking step. | Document in `CLAUDE.md` that wrap also invokes the repo-private `dead-code-audit` skill (one-line addition to builder/reviewer agent guidance). Acceptable. |
| **`wf-doc-lint` is a minimal port** | Drops v1's `metrics.json` / `docs/log.md` / `docs/index.md` primitives, mode flag (scoped vs full), uncovered-contract-surface check, badges. Keeps the 4 mechanical checks (code-ref drift, removed-feature docs, orphan docs, doc TODOs). | Acceptable — we don't actively use the dropped primitives in any wired flow; `contractSurfaces` is unconfigured. |
| **Tracking-doc path convention** | v3 default `work/tracking/M-NNN-<slug>.md` (centralized). Our v1 convention is epic-local `work/epics/.../<m-id>-tracking.md`. The shipped `aiwfx-track` skill explicitly allows project-override. | Phase 5 decision (record at projector design): keep epic-local layout, override the framework default. Lower churn; matches our existing artifact-layout. |
| **No shipped equivalents for** `workflow-audit`, `workflow-graph`, `update-framework`, `verify-contracts`, `design-contract`, `quality-score`, `doc-garden`, `dead-code-audit` | All accounted for in plan: first three replaced by aiwf core (`check`/`history`/`update` + `/plugin update`); last five are repo-private (port now: `dead-code-audit`; later: `verify-contracts`; deferred: rest). | No new gap; matches Q1-Q6 settled state. |

**No blocking gaps.** Plugins ship feature-complete relative to our actual flow. Repo-private port list is unchanged.

**Phase 1 findings recorded:**
- aiwf core ships 8 embedded skills now (was 6 in earlier docs): aiwf-{add, check, contract, history, promote, reallocate, rename, status}.
- New since earlier docs: a pre-commit hook that auto-regenerates `STATUS.md` (installed by `aiwf update` not `aiwf init`; toggleable via `status_md.auto_update` config).
- Filesystem case-insensitive at `/workspaces/flowtime-vnext` (devcontainer bind mount over macOS host APFS). v3's `case-paths` validator may surface findings during Phase 4 dry-run; not a blocker, but track.

### Phase 2 — projector

**Design (Phase 2 Q&A settled 2026-05-01):**
- Home: `work/migration/scripts/`
- Manifests output: `work/migration/manifests/` (separate from scripts; data ≠ code)
- Language: Python 3 + `uv` script mode (PEP 723 inline deps; `uv run script.py`); no `pyproject.toml`
- Incremental scope: E-22 first, then extend in successive passes; decisions + gaps last

**Authoritative aiwf status sets (from `aiwf schema`):**

| Kind | Statuses | Required fields | Notes |
|---|---|---|---|
| epic | `proposed, active, done, cancelled` | id, title, status | no parent |
| milestone | `draft, in_progress, done, cancelled` | id, title, status, parent | parent → epic |
| adr | `proposed, accepted, superseded, rejected` | id, title, status | optional supersedes/superseded_by |
| gap | `open, addressed, wontfix` | id, title, status | optional discovered_in/addressed_by |
| decision | `proposed, accepted, superseded, rejected` | id, title, status | optional relates_to |
| contract | `proposed, accepted, deprecated, retired, rejected` | id, title, status | optional linked_adrs |

**Status-derivation rule (precedence):**

1. **Dir location wins for terminal states.** Any epic under `work/epics/completed/` → `done`, regardless of what the source spec's `**Status:**` says. (Pass D applies this to E-10, E-12, E-16, E-17, E-19, E-20, E-21, E-23, E-24.)
2. **Otherwise use the source `**Status:**` line, mapped via the table below** (also cross-check `work/epics/epic-roadmap.md` when source spec is silent or ambiguous).
3. **If neither source signals nor roadmap clarify** → default `proposed`, log finding to `skip-log.md`.

   *Rationale:* most active-dir epics in this repo are "we plan to do this; nothing's started" → `proposed`. Epics with explicit "paused" signal map to aiwf `active` (paused work has real commitments / branches / prior milestones; closer to active than proposed). Default for missing-status is `proposed` since the absence of a paused signal in source means we shouldn't infer one.

**v1 → v3 status-mapping table (filled as passes encounter source statuses):**

| v1 source status | v1 kind | v3 status | Settled |
|---|---|---|---|
| `planning` | epic | `proposed` | ✅ Pass A (E-22 spec) |
| `in-progress` | epic / milestone | (epic) `active` / (milestone) `in_progress` | ✅ Pass B (E-18 spec + roadmap + ROADMAP + CLAUDE.md all agree) |
| `superseded` / `absorbed` | epic | `cancelled` (work moved permanently to a different epic; original plan no longer governs) | ✅ Pass B (E-14: absorbed into ui-analytical-views which is out of migration scope per Q1) |
| `complete` / `completed` | epic / milestone | `done` | TBD when encountered |
| `pending` | milestone | `draft` | TBD |
| `paused` | epic / milestone | `active` (epic) — paused work has real commitments / branches / prior milestones; closer to active than proposed | ✅ Pass B (user override) |
| `active` | decision | `accepted` | TBD |
| `superseded` | decision | `superseded` | TBD |
| `withdrawn` | decision | `rejected` | TBD |
| `open` | gap | `open` | TBD |
| `resolved` (with date suffix) | gap | `addressed` | TBD |

**Successive-pass plan:**

| Pass | Scope | Goal |
|---|---|---|
| **A (spike)** | E-22 only | debug shared projector logic against minimal surface |
| **B** | E-13 + E-14 + E-15 (no-milestone active epics) | confirm shared logic generalizes |
| **C** | E-18 (multi-milestone generic `m-EXX-NN-...`) | exercise milestone-emit + body-rewrite at scale |
| **D** | completed-id'd generic-shape epics (E-16, E-17, E-19, E-20, E-21, E-23, E-24) | best-effort projection |
| **E** | outliers (E-10 `m-ec-pN`; E-11 no-spec.md + `m-svui-NN`; E-12 `M-10.NN`) | per-epic custom rules |
| **F** | decisions (54, chronological) + gaps (157, git-blame sort) | mechanical projection on stable code |
| **G** | body-rewrite cross-pass | substitute old ids → new ids per id-map.csv across manifest body strings |

**Implementation checklist:**
- [x] Decide projector home — `work/migration/scripts/`
- [x] Decide language — Python 3 + `uv` script mode
- [x] Decide incremental scope — E-22 first; successive passes A–G
- [x] Pass A: spike on E-22 — `work/migration/scripts/project_e22.py` (uv script-mode, ruamel.yaml). Generates `work/migration/manifests/e22-spike.yaml`. **Dry-run green:** `aiwf import --dry-run` zero findings, exit 0. 12,902-byte `epic.md` would land at `work/epics/E-22-time-machine-model-fit-chunked-evaluation/epic.md`
- [x] Pass B: extend to E-13/E-14/E-15 (+ E-22 carried from A) — `project_epics.py` replaces `project_e22.py`. **Dry-run green:** 4 epics, 0 findings, exit 0. skip-log.md emitted (1 entry: E-13 default-status). Status mapping table proven on missing/superseded/parenthesized-prose/clean inputs.
- [x] Pass C: extend to E-18 + 11 milestones. Q2 revised: explicit `M-NNN` (computed by projector) instead of `auto` because intra-manifest `depends_on` can't reference auto entries. **Dry-run green:** 5 epics + 11 milestones, 0 errors, exit 0. id-map.csv emitted (11 entries). M-002 depends_on M-001 resolves correctly within manifest. m-E18-01 epic-target dep (E-20) dropped + skip-logged.
- [x] Pass D: extend to completed-id'd generic-shape epics (E-16, E-17, E-19, E-20, E-21, E-23, E-24). Dir-location override (`completed/` → `done`) extended to milestones too — milestones inside completed/ epic dirs forced to `done` regardless of source status. Two new shape variants handled: YAML frontmatter (E-23, E-24 epics + their milestones) and full-slug `**ID:**` lines (m-E16-NN). H1 separator broadened to accept `:` (`# m-E23-01: Title`). **Dry-run green:** 12 epics + 53 milestones = 65 writes, 0 errors, exit 0. id-map.csv now 53 entries. 12 findings (10 are noise: `**Epic:**` field carries title instead of id; 2 are real: E-13 default-status, m-E18-01 epic-dep).
- [x] Pass E: outlier per-epic rules (E-10 `m-ec-pN[a-z]?\d?`, E-11 `m-svui-NN`, E-12 `M-10\.NN`). Generalized milestone-id regex via `_MILESTONE_ID_PATTERNS` list (alternation). Filename filter excludes `-tracking`/`-log`/`-review`. H1 parsing handles `Milestone:` prefix and full-slug variants like `m-svui-01-scaffold`. **Final coverage achieved:** 15 epics + 65 milestones = 80 writes, 0 errors, exit 0. id-map.csv 65 entries.
- [x] Pass F: decisions + gaps. `parse_decisions` splits `work/decisions.md` by `## D-YYYY-MM-DD-NNN:` headings, sorts by (date, seq) chronologically, allocates D-001..D-053. `parse_gaps` splits `work/gaps.md` by H2 headings (skipping "Open Questions" footer), uses `git blame --date=short` to recover per-line creation dates, sorts by (date, line) and allocates G-001..G-033. Decision status mapping `active → accepted`. Gap status from `(resolved YYYY-MM-DD)` title-suffix → `addressed`, else `open`. **Final manifest: 166 entities** (15 epics + 65 milestones + 53 decisions + 33 gaps), 166 writes, 0 errors, exit 0. id-map.csv 118 entries (65 milestones + 53 decisions; gaps are new-only).
- [x] Pass G: body-rewrite cross-pass with id-map. Q&A settled three points: (1) targeted rewrite — id-map only; (2) inline during projection — single source of truth; (3) skip fenced code + inline code, rewrite both bare-id and full-slug forms (collapse to bare new id). `rewrite_body_text` saves fenced/inline regions as placeholders, applies substitutions in length-DESC order with `(?<![\w-]).(?![\w-])` lookarounds, restores placeholders. Verified: 0 occurrences of `m-E18-13` post-rewrite; 7 of `M-010`; E-22 body cleanly references `M-002`, `D-045`, `M-043` etc. **`aiwf import --dry-run` 166 plans, 0 errors, exit 0 — Phase 2 complete.**
- [ ] Emit `skip-log.md` accumulated across passes
- [ ] Final: produce single combined manifest for Phase 4 dry-run loop

### Phase 3 — pre-process source

- [x] Stayed on migration/aiwf-v3 (no separate preprocess branch needed; commits are scoped)
- [x] Fill missing required fields in active epic specs: E-13 added `**Status:** proposed`; E-11 added `**Status:** paused` (maps to active)
- [x] Resolve source ambiguities: the only real skip-log finding remaining is m-E18-01's `**Depends on:** E-20` (epic-target) — accepted as projected since aiwf milestone.depends_on requires milestone targets and body prose retains the dep
- [x] Apply Q1 decision: 17 non-id'd `work/epics/completed/` dirs relocated to `work/archived-epics/` via `git mv` (history preserved); `completed/` now contains only the 9 E-NN dirs

### Phase 4 — dry-run loop

- [ ] `aiwf import --dry-run manifest.yaml` from a clean state
- [ ] Iterate: each finding either fixes the projector (Phase 2) or the source (Phase 3); halve findings each pass
- [ ] When dry-run is clean, run `aiwf import manifest.yaml` for real → atomic commit on migration branch
- [ ] Run `aiwf init` (installs pre-push hook + materializes 6 `aiwf-*` skills) — separate commit
- [ ] Test push from migration branch; pre-push hook should pass since import succeeded
- [ ] Spot-check: `aiwf history E-22`, `aiwf check`, `aiwf render roadmap`

### Phase 5 — teardown and cutover

- [ ] Port repo-private skills (`dead-code-audit`, `devcontainer`, `ui-debug`) to `.claude/skills/<name>/SKILL.md`; commit
- [ ] Move `.ai-repo/recipes/dead-code-*.md` under `.claude/skills/dead-code-audit/recipes/`
- [ ] Fold `.ai-repo/rules/project.md` content into `CLAUDE.md`
- [ ] Delete `.ai/` submodule (`.gitmodules` edit + `git rm`)
- [ ] Delete `.ai-repo/` (entire directory)
- [ ] Delete generated `.github/skills/` adapter files (if any)
- [ ] Delete generated `.claude/skills/wf-*/` (v1 generated copies; replaced by plugin install)
- [ ] Delete `.claude/agents/*.md` (replaced by `aiwf-extensions` plugin agents)
- [ ] Update `CLAUDE.md`: replace v1 framework references with v3; rewrite "Resolved Artifact Layout" section against `aiwf.yaml`; rewrite "Agent Routing" against plugin agents; remove "Framework Sources" v1 table
- [ ] Update `.claude/settings.json`: remove or replace the v1-path SessionStart hooks (`.ai/tools/scratch-audit.sh`, `.ai-repo/bin/wf-graph`) — either delete the hook entries entirely or substitute v3 equivalents (e.g. `aiwf doctor --quiet` on session start) if useful
- [ ] Update `CLAUDE.md` Current Work to reference new milestone ids
- [ ] Update memory files: `feedback_audit_mute_archived.md` rewrites to target `aiwf check` rather than `wf-workflow-audit`
- [ ] Run `aiwf doctor`, `aiwf check`, `aiwf doctor --self-check` — all green
- [ ] Open PR `migration/aiwf-v3` → `main`, request review, merge

### Phase 6 — post-merge

- [ ] Archive this plan to `work/migration/completed/aiwf-v3-plan.md`
- [ ] First real `aiwf` work: pick first contract entity (likely `model.schema.yaml` → `C-001`); port `verify-contracts` skill at that point

---

## Migration log

Append-only record of dry-run iterations, decisions taken mid-flight, and findings. Format: `YYYY-MM-DD — phase — note`.

- 2026-05-01 — phase 0 — branch created, plan drafted, open questions captured.
- 2026-05-01 — phase 0 — Q1 settled: hybrid history scope. Project all E-NN-prefixed epics (6 active + 9 completed-id'd = 15 epics). Best-effort on completed; non-id'd dirs (16) relocate to `work/archived-epics/`.
- 2026-05-01 — phase 0 — Q2 settled: per-epic preprocessing + chronological renumber. Epic ids verbatim. Milestones `auto`-allocated under deterministic order. Decisions sorted by (date, seq) → D-001..D-054. Gaps sorted by git-blame date → G-001..G-157. id-map.csv captures all mappings. Outlier epics (E-10, E-11, E-12) get custom per-epic projector rules.
- 2026-05-01 — phase 0 — Q3 settled: adopt v3 default `docs/adr/`. No config override. Empty `docs/decisions/` deletes in Phase 5 teardown.
- 2026-05-01 — phase 0 — Q4 settled: brownfield-migration gap closed upstream (self-guarding pre-push hook + `Options.SkipHook` flag landed on poc/aiwf-v3 commit ≤53393ed). `aiwf init` is safe to run on the migration branch any time without breaking `main` pushes. Practical choice: install at end of Phase 4 against a known-clean tree.
- 2026-05-01 — phase 0 — Q5 settled: Project scope for plugin pins — committed to `.claude/settings.json`. SessionStart hooks pointing at v1 paths flagged for Phase 5 cleanup (remove or replace).
- 2026-05-01 — phase 0 — Q6 settled: devcontainer feature for Go + `postCreateCommand` for aiwf. Branch-tip pin during migration window; bump to tagged release once PoC stabilizes.
- 2026-05-01 — phase 0 — **all 6 open questions settled.** Plan ready for Phase 1.
- 2026-05-01 — phase 1 — devcontainer rebuilt; Go 1.22.10 + aiwf installed (init.sh resolves branch tip via `git ls-remote` to a SHA, since `go install @poc/aiwf-v3` rejects slash-named branches). PATH hardcoded to `/home/vscode/...` (containerEnv:HOME doesn't resolve in this devcontainer setup). `aiwf doctor --self-check` 22/22 green. `aiwf doctor` against current repo confirms brownfield state cleanly. Filesystem flagged case-insensitive — track for Phase 4 dry-run findings.
- 2026-05-01 — phase 1 — marketplace `23min/ai-workflow-rituals` registered (User scope; cache populated under `~/.claude/plugins/cache/`). Plugin install (Project scope) awaiting user action.
- 2026-05-01 — phase 1 — skill-audit done from plugin cache: 8 aiwfx-* skills + 4 wf-* skills + 4 agents + 5 templates verified present. Three real gaps identified, all with acceptable mitigations: (1) `aiwfx-wrap-milestone` doesn't chain `dead-code-audit` — document in CLAUDE.md; (2) `wf-doc-lint` is minimal port — acceptable since we don't use dropped primitives; (3) tracking-doc path convention shifted to centralized `work/tracking/` — keep our epic-local layout via project override. No blocking gaps.
- 2026-05-01 — phase 1 — `/plugin install` failed with "Source path does not exist" despite source being present; root cause was plugin-manager bookkeeping confusion (plugins were already installed for `/Users/peterbru/Projects/proliminal.net` and Claude Code wouldn't add a second per-project install record). Recovered via manual edit of `.claude/settings.json` — added `enabledPlugins: { "aiwf-extensions@ai-workflow-rituals": true, "wf-rituals@ai-workflow-rituals": true }`. `aiwf doctor` now confirms "rituals plugin detected." **Phase 1 closed.**
- 2026-05-01 — phase 1 — committed (8865346 + 2d08da8): devcontainer infra + plan doc.
- 2026-05-01 — phase 2 — Q1 settled: projector home = `work/migration/scripts/`. Co-locates with plan + id-map.csv + skip-log.md; deletes as one dir at Phase 5.
- 2026-05-01 — phase 2 — Q2 settled: Python 3 with `uv`. Use uv script mode (PEP 723 inline `# /// script` metadata declaring deps like `ruamel.yaml`); `uv run script.py`. No `pyproject.toml`/project skeleton — each script is self-contained and disposable. uv already in devcontainer (init.sh installs it).
- 2026-05-01 — phase 2 — Q3 settled: incremental scope = E-22 first (Pass A spike), extend in passes B–G; decisions + gaps last on stable projector code.
- 2026-05-01 — phase 2 — micro-decisions for Pass A: status mapping `planning → proposed` (epic kind); manifests output dir `work/migration/manifests/` (separate from scripts; data ≠ code). Authoritative status sets pulled from `aiwf schema` and recorded in plan. **Phase 2 design closed; ready to implement Pass A.**
- 2026-05-01 — phase 2 — Pass A landed. `project_e22.py` (uv PEP-723 script mode, ruamel.yaml LiteralScalarString for body). Single-entity manifest validates with `aiwf import --dry-run` zero findings; would write `work/epics/E-22-time-machine-model-fit-chunked-evaluation/epic.md` (12,902 bytes).
- 2026-05-01 — phase 2 — **Pass A finding (slug derivation):** aiwf derives destination dir slug from `title`, not source dir name. Source `E-22-model-fit-chunked-evaluation` ≠ aiwf-generated `E-22-time-machine-model-fit-chunked-evaluation`. Phase 3/5 decision pending: preserve v1 short slugs (via `aiwf rename` or trimmed manifest titles) vs. accept title-derived slugs (id is stable ref; path is incidental). Lean: accept; settle before mass import in Pass B.
- 2026-05-01 — phase 2 — slug-preservation settled: **accept aiwf title-derived slugs.** Identity = id; path = incidental (per upstream design-lessons.md principle 1 "identity is not location"). Projector emits titles verbatim; aiwf decides slugs. Phase 5 cleanup deletes old v1 dirs whole; memory files get one-pass review to fix path references that matter. No `aiwf rename` post-import; no manifest title trimming.
- 2026-05-01 — phase 2 — Pass B finding 1 settled: missing `**Status:**` in active-dir epic spec → default `proposed` + skip-log finding. Status-derivation rule expanded: roadmap is a secondary cross-check; missing status defaults to `proposed`.
- 2026-05-01 — phase 2 — mapping override (user): `paused → active` (epic kind). Paused work has real commitments / branches / prior milestones; closer to aiwf `active` than `proposed`. Affects E-11 (paused after M6 per roadmap) → `active`. Default for missing-status is still `proposed` (absence of explicit paused signal means we don't infer one).
- 2026-05-01 — phase 2 — Pass B finding 2 settled: `superseded` / `absorbed` → `cancelled` (epic). Affects E-14 (absorbed into ui-analytical-views, which is out of migration scope per Q1). Body retains supersession prose verbatim.
- 2026-05-01 — phase 2 — Pass B mapping resolved (user) for E-18: `in-progress → active`. All four surfaces agree (spec, epic-roadmap, ROADMAP, CLAUDE.md) plus the live `epic/E-18-time-machine` branch hasn't been closed. E-18 → `active`.
- 2026-05-01 — phase 2 — Pass B finding 3 settled: parenthesized qualifier on `**Status:**` line is preserved by prepending a `> **Status note:** <qualifier>` blockquote to the top of the body. Information not lost; body retains the "capture is shipped; ingestion pipeline is not" nuance from E-15 inline.
- 2026-05-01 — phase 2 — Pass B landed. `project_epics.py` (replaces `project_e22.py`); `epics-active.yaml` (4 epics: E-13/E-14/E-15/E-22); `skip-log.md` (1 finding: E-13 default-status). Status-mapping table exercised on all four input shapes (missing / superseded / parenthesized-prose / clean). `aiwf import --dry-run` zero error findings, exit 0. Plans 4 writes at title-derived slugs as expected.
- 2026-05-01 — phase 2 — Pass C landed. Projector extended to milestones. Q2 **revised** (recorded in plan): explicit `M-NNN` ids computed by projector instead of `auto`, because aiwf manifest reference fields like `depends_on` can't resolve against `auto` entries (no id declared at manifest time). E-18 + 11 milestones (M-001..M-011) projected; M-002 depends_on M-001 resolves cleanly. id-map.csv emitted. m-E18-01's `**Depends on:** E-20` (epic-target) correctly dropped from frontmatter (body retains prose) and skip-logged. Two milestone H1 shapes handled (Variant A: prose `**ID:**` line; Variant B: id embedded in H1). Two milestone status mappings: `complete` → `done`. `aiwf import --dry-run` zero errors, exit 0. 16 writes planned.
- 2026-05-01 — phase 2 — Pass D landed. Projector extended to completed-id'd generic-shape epics (E-16, E-17, E-19, E-20, E-21, E-23, E-24). Dir-location override extended to milestones (completed/ parent → milestone forced `done`). Two new shape variants handled: (a) YAML frontmatter `--- ... ---` blocks (E-23, E-24 epics + milestones); (b) full-slug `**ID:**` lines (m-E16-NN normalized via MILESTONE_OLD_ID_RE). H1 separator broadened to `[:—-]`. Final: 12 epics + 53 milestones = 65 writes, 0 errors, exit 0. id-map.csv 53 entries. 12 findings (10 noise from `**Epic:**` field-carries-title; 2 real).
- 2026-05-01 — phase 2 — Pass E landed. **Full coverage of Phase 2 scope achieved.** Projector extended to outliers via generalized id pattern alternation: `M-10\.NN` (E-12) | `m-ec-pN[a-z]?\d?` (E-10) | `m-svui-NN` (E-11) | `m-E\d+-\d+` (generic). Filename filter adds `-review` exclusion (E-10 has review docs). H1 parsing handles `# Milestone: <id-with-slug> — <Title>` shapes via `\S*` after id pattern. Final: **15 epics + 65 milestones = 80 writes, 0 errors, exit 0.** id-map.csv 65 entries. 25 findings (~22 noise from `**Epic:**` field carrying title; ~3 real).
- 2026-05-01 — phase 2 — Pass F landed. Decisions + gaps. `parse_decisions` splits monolithic `work/decisions.md` by `## D-YYYY-MM-DD-NNN:` headings → 53 entities sorted chronologically as D-001..D-053. `parse_gaps` splits `work/gaps.md` by H2 (skipping "Open Questions" footer) using `git blame --date=short` to recover per-line creation dates → 33 entities sorted chronologically as G-001..G-033. Status: decision `active → accepted`; gap title-suffix `(resolved YYYY-MM-DD) → addressed`. **Final: 166 entities (15 + 65 + 53 + 33), 166 writes, 0 errors, exit 0.** id-map.csv now 118 entries (65 milestones + 53 decisions).
- 2026-05-01 — phase 2 — Pass G landed. Body-text rewrite via id-map (settled Q&A: targeted, inline, skip-code-blocks). `rewrite_body_text` segments fenced and inline code as placeholders, substitutes old ids → new ids on remaining prose with `(?<![\w-]){escaped}(?:-[a-z0-9-]+)?(?![\w-])` regex (matches both bare id and full-slug forms; collapses both to bare new id). Substitutions applied in length-DESC order. Verified: every old m-EXX-NN and D-YYYY-MM-DD-NNN occurrence in body prose now points to its new M-NNN/D-NNN. `aiwf import --dry-run`: 166 plans, 0 errors, exit 0. **Phase 2 (projector) complete — manifest is import-ready.**
- 2026-05-01 — phase 3 — pre-process source. (1) 17 non-id'd `work/epics/completed/` dirs relocated to `work/archived-epics/` via `git mv` (preserves history); `completed/` now contains only 9 E-NN dirs. (2) E-13 source spec gets `**Status:** proposed`; E-11 source spec gets `**Status:** paused`. (3) m-E18-01 epic-target dep accepted-as-projected (aiwf invariant; body retains prose). Re-projection: 166 entities unchanged, skip-log shrunk to 1 real + 22 noise findings, `aiwf import --dry-run` exit 0. **Phase 3 complete.**
