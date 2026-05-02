# CLAUDE.md

This project uses **aiwf v3** (Go binary at `~/go/bin/aiwf`) plus the **ai-workflow-rituals** plugin marketplace (Claude Code plugins `aiwf-extensions` + `wf-rituals`). The planning kernel lives in 6 entity kinds: epic, milestone, ADR, gap, decision, contract — each with a closed status set, stable ids that survive rename/reallocate, and `git log` as the audit trail.

Despite the filename, this file is shared workspace context for both Claude and GitHub Copilot in this repo. Keep it assistant-neutral.

## Session Start

At the start of every session, pick up context:

- `aiwf status` — one-screen snapshot of in-flight work, open decisions, gaps, recent activity
- `## Current Work` section below — narrative summary
- `work/migration/aiwf-v3-plan.md` — historical record of the v1→v3 migration; check `id-map.csv` for old-id ↔ new-id lookups

After `/compact` or a fresh session, this file is re-available via the system prompt. Say **"refresh context"** to re-read everything.

## Hard Rules

- **NEVER commit or push without explicit human approval** — "continue" / "ok" do not count
- **TDD by default** for logic, API, and data code — red → green → refactor
- **Branch coverage (hard rule)** — every reachable conditional branch needs a test before declaring done; perform a line-by-line audit before the commit-approval prompt
- **Branch discipline** — do NOT commit milestone work directly to `main`
- **Update CLAUDE.md Current Work** after starting or wrapping a milestone
- Conventional Commits format: `feat:`, `fix:`, `chore:`, `docs:`, `test:`, `refactor:`

## Agent Routing

Role agents ship via the `aiwf-extensions` plugin (loaded into Claude Code from the plugin cache).

| Intent | Agent | Drives |
|--------|-------|--------|
| build, implement, code, start, fix, patch | **builder** | `aiwfx-start-milestone` → `wf-tdd-cycle` → `aiwfx-wrap-milestone`; `wf-patch` for one-offs |
| plan, design, scope, epic, architecture | **planner** | `aiwfx-plan-epic`, `aiwfx-plan-milestones`, `aiwfx-record-decision` |
| review, check, validate, wrap, finish | **reviewer** | `wf-review-code`; status promotions via `aiwf promote` |
| release, deploy, tag, publish | **deployer** | `aiwfx-release` |

**Wrap-milestone supplement:** the upstream `aiwfx-wrap-milestone` skill does not chain dead-code-audit. After wrap, builder/reviewer should also invoke the repo-private `dead-code-audit` skill.

## Repo Layout

| Path | Purpose |
|------|---------|
| `aiwf.yaml` | aiwf consumer config (~10 lines) |
| `work/epics/E-NN-<slug>/` | active epic dirs, with `epic.md` + `M-NNN-<slug>.md` milestones |
| `work/epics/completed/E-NN-<slug>/` | completed epic dirs (status: done) |
| `work/decisions/D-NNN-<slug>.md` | per-decision entities |
| `work/gaps/G-NNN-<slug>.md` | per-gap entities |
| `work/contracts/C-NNN-<slug>/` | contract entities (none yet) |
| `docs/adr/ADR-NNNN-<slug>.md` | ADRs |
| `work/archived-epics/<slug>/` | pre-aiwf historical epics (no E-NN id; out of aiwf's walked roots) |
| `work/migration/` | v1→v3 migration history (plan, manifest, id-map, scripts) |
| `.claude/skills/aiwf-*/` | gitignored, materialized by `aiwf init` / `aiwf update` |

`aiwf` verbs: `init` (one-time setup), `add <kind>`, `promote`, `cancel`, `rename`, `reallocate`, `move`, `check`, `history`, `status`, `render roadmap`, `doctor`, `import`, `schema`, `template`, `contract verify` etc. Run `aiwf help` for the full list.

## Deferred follow-ups

Out-of-band items that aren't repo-internal FlowTime work but shouldn't be forgotten. Each entry should name a clear trigger and the action to take when the trigger fires. Remove the entry once acted on.

- **Gap discovered_by / discovered_in archeology backfill.** During the v1→v3 migration we discovered aiwf's gap schema has `discovered_in` (entity context, optional) but no `discovered_by` field for actor. All 33 gaps were created via `aiwf import`'s single bulk commit, which collapses per-entity provenance into one undifferentiated `aiwf-actor:` trailer — losing the original gap-author signal. Archeology in `work/migration/scripts/gap_archeology.py` recovers both signals from `git blame` on the deleted v1 `work/gaps.md`. **Trigger:** aiwf ships an improved provenance model (e.g. `discovered_by` on gap, or a `--actor-from-frontmatter` flag for `aiwf import`, or per-entity provenance trailers within bulk imports). **Action:** re-run `gap_archeology.py`; backfill `discovered_in` (and `discovered_by` if available) on the 28 gaps where archeology found a signal (6 high-confidence, 22 med-low; see report). The 5 silent gaps (G-001/2/4/5/6/7/21) stay unset.

## Project-Specific Rules

# FlowTime Project Rules

Project-specific conventions for the FlowTime mono-repo (Engine + Sim + UI).

---

## Tooling

- Prefer precise edits; stick to established patterns and avoid broad refactors without context.
- Use `rg`/`fd` for searches.

## Project Layout

- `src/FlowTime.Core`, `src/FlowTime.API`, `src/FlowTime.Cli`, `src/FlowTime.Contracts`, `src/FlowTime.Adapters.Synthetic` — Engine surface.
- `src/FlowTime.Sim.Core`, `src/FlowTime.Sim.Service`, `src/FlowTime.Sim.Cli` — Simulation surface.
- `src/FlowTime.UI`, `src/FlowTime.UI.Tests` — Blazor WebAssembly UI.
- `tests/` mirrors project names (e.g., `tests/FlowTime.Core.Tests`, `tests/FlowTime.Sim.Tests`, `tests/FlowTime.Api.Tests`).
- `docs/` — Engine/shared documentation. `docs-sim/` is archived — ignore unless explicitly requested.
- `work/` — aiwf planning state: epics, milestones, decisions, gaps, contracts.

## Workflow Artifact Layout

- The canonical artifact layout is defined by `aiwf.yaml` + aiwf's per-kind path conventions (see `aiwf schema` for required frontmatter fields per kind).
- Older `*-log.md` and `*-tracking.md` files in `work/archived-epics/` are pre-aiwf historical residue; they're out of aiwf's walked roots and not validated.
- Tracking-doc convention (per the `aiwfx-track` skill): tracking docs are advisory free-form markdown alongside the milestone spec; not aiwf entities, not validated.

## Milestone Status Sync

- Mutating verbs (`aiwf promote`, `aiwf cancel`, `aiwf reallocate`, etc.) handle status updates atomically with a git commit each; manual cross-surface reconciliation isn't needed.
- After milestone start/wrap, refresh `CLAUDE.md` Current Work and run `aiwf render roadmap` to update `ROADMAP.md` if used as a top-level surface.
- Do not edit milestone frontmatter status by hand — go through `aiwf promote` so the FSM check + commit trailer happen.

## Coding Conventions

- .NET 9 / C# 13 with implicit usings and nullable enabled.
- Private fields **must use camelCase without a leading underscore** (`private readonly string dataDirectory;`).
- Follow existing patterns before introducing new abstractions; check for shared contracts in `FlowTime.Contracts`.
- Keep CLI ↔ API behaviour aligned; update relevant `.http` examples and docs when changing endpoints.
- Use invariant culture for parsing/formatting; keep tests deterministic.
- JSON payloads and schemas use camelCase — do not introduce snake_case fields.
- Never reintroduce deprecated schema fields (e.g., `binMinutes`); current schema uses `{ bins, binSize, binUnit }`.
- When inserting inline code containing `|` inside Markdown tables, escape the pipe as `\|`.

## Branching & Versioning

- Epic integration branches use `epic/E-{NN}-<slug>`. Every numbered epic gets an integration branch; milestone branches branch from it and merge back into it.
- Milestone branches use `milestone/<milestone-id>`.
- Feature branches use `feature/<surface>-<milestone-id>/<desc>` when a milestone needs parallel work.
- Single-surface quick changes can branch from `main` and PR directly back to `main` when no milestone integration branch is needed.
- Conventional commits: `feat(api):`, `fix(sim):`, `docs:`, etc.
- Commit messages: conventional prefix, no icons/emoji; subject + short bullet body capturing the milestone and key work/tests touched.
- Version format `<major>.<minor>.<patch>[-pre]`; milestone completions typically bump minor (e.g., `0.6.0 → 0.7.0`).
- Release notes in `docs/releases/` with milestone-based naming (e.g., `SIM-M2.7-v0.6.0.md`).

## Build & Run

- `dotnet build FlowTime.sln` / `dotnet test FlowTime.sln`
- VS Code tasks: `build`, `test`, `start-api`, `stop-api`, `start-sim-api`, `stop-sim-api`, `start-ui`, `stop-ui`
- Engine API: `dotnet run --project src/FlowTime.API` → port 8081
- Sim API: `dotnet run --project src/FlowTime.Sim.Service` with `ASPNETCORE_URLS=http://0.0.0.0:8090`
- UI: `dotnet run --project src/FlowTime.UI`
- Default ports: 8081 (Engine API), 8090 (Sim API), 5219/7047 (UI), 8091 (Sim diagnostics), 5091 (Engine dev profile)
- Build and test before handing work back.

## Devcontainer Port Safety

- **Never blindly kill all processes on port 8081** — the devcontainer port-forwarder listens there; killing it destroys the session.
- To free port 8081, filter by process name: only kill `dotnet` processes.
- Use the `kill-port-8081` VS Code task — it filters safely.
- Verify processes before killing: `lsof -ti:PORT`, `ps aux | grep`. Use `pkill -f "ProcessName"` or `lsof -ti:PORT | xargs -r kill -TERM`. Never `kill <PORT>`.
- Send SIGTERM first, wait, then SIGKILL only if still alive. Never start with `kill -9`.

## Testing

- Unit tests: fast and deterministic; no network or file-system side effects.
- API tests: use `WebApplicationFactory<Program>`; prefer real dependencies over mocks.
- Sim tests: `tests/FlowTime.Sim.Tests` — covers CLI, template parsing, provenance, service behaviours.
- Integration tests: `tests/FlowTime.Integration.Tests` for cross-surface scenarios.

### UI testing (hard rule)

- **UI work must be eval'd end-to-end in a real browser.** Every milestone that ships new or changed UI (Blazor or Svelte) must include Playwright tests that drive the feature in a real browser and verify the rendered outcome. Type checks and unit tests on pure helpers are necessary but not sufficient — they do not catch broken event handlers, state leaks, reactive glitches, or CSS-driven breakage. The user experience is the contract; a passing test must prove the user experience works.
- **Playwright infrastructure lives at `tests/ui/`** with config at `tests/ui/playwright.config.ts`, specs under `tests/ui/specs/`, and helpers under `tests/ui/helpers/`. Add new specs alongside existing ones.
- **Graceful skip when infrastructure is down.** If the API or dev server isn't running, the spec should skip with a clear message rather than fail. Follow the existing pattern used by the Rust engine integration tests (health probe → skip on unavailable).
- **Svelte UI runs on port 5173** (vite dev). **Blazor UI runs on port 5219**. Override `baseURL` per-spec if needed.
- **Vitest covers pure logic.** Svelte/TS pure functions (helpers, store derivations, protocol encoding) should have vitest unit tests in `ui/src/**/*.test.ts`. These run fast and guard the foundation; Playwright guards the integration.
- **Cover the critical paths.** For each user-facing surface: (1) page loads and renders expected initial state, (2) at least one user interaction drives a visible change, (3) reset/undo/error-recovery paths behave correctly, (4) key latency or correctness metrics that the UI exposes actually display correct values.

## Truth Discipline

### Precedence (highest to lowest)
1. **Code + passing tests** define live truth.
2. **`work/decisions.md`** defines approved direction.
3. **Epic specs and epic-local milestone specs** under `work/epics/` define implementation target, within their scope.
4. **Architecture docs** (`docs/`) summarize and connect the above — they never outrank code or decisions.
5. **Historical and exploration docs** are context only — never implementation authority.

If code, decisions.md, and an architecture doc disagree, do not choose arbitrarily. Report the mismatch and ask.

### Truth classes
- **`docs/`** — current ground truth. If it's in `docs/`, it describes what IS (shipped, provable by code/tests).
- **`work/epics/`** — decided-next and exploration. Proposals, specs, architectural direction for future work.
- **`docs/archive/`**, **`docs/releases/`** — historical. What WAS. Do not use for current state.
- **`docs/notes/`** — exploration only. Brainstorming, research, ideas. Never treat as implementation authority.

### Guards
- Do not describe a target contract in present tense unless it is live.
- Do not let one file simultaneously act as current reference and historical archive.
- Do not restate a canonical contract in many places from memory — point to the owning doc.
- Do not let adapter/UI projection become the only place where semantics exist.
- Do not keep "temporary" compatibility shims without explicit deletion criteria.
- When a milestone explicitly owns a bridge or cleanup seam, do not preserve the bridge helper past that milestone as a tolerated coexistence state. Treat the surviving helper as incomplete work.
- Do not reconstruct semantic or analytical identity in adapters or clients from `kind`, `logicalType`, file stems, or similar heuristics when compiled/runtime facts can own that truth.
- When a runtime boundary changes, prefer forward-only regeneration of runs, fixtures, and approved outputs over compatibility readers that recover missing facts.
- Do not keep both a bridge abstraction and its compiled replacement once the replacement milestone is active unless the spec explicitly allows a coexistence window.
- "API stability" does not mean "keep old functions around." When a function has no production callers after a refactor, delete it and its tests in the same change — do not retain it as a dead alternative entry point under the banner of keeping the existing surface stable.
- Do not treat aspirational docs as implementation authority.

## Documentation

- Engine + shared docs in `docs/`; use Mermaid for diagrams (not ASCII art).
- Keep docs/schemas/templates aligned when touching contracts or schemas.
- Repository language: English. No time or effort estimates in docs or plans.
- Treat sibling checkouts as read-only references unless the user instructs otherwise.


## Current Work
<!-- Updated by start-milestone and wrap-milestone skills. Do not edit in sync.sh. -->

**Active focus:** none — E-21 Svelte Workbench & Analysis Surfaces closed and merged to main 2026-05-01 (wrap artefact: `work/epics/completed/E-21-svelte-workbench-and-analysis/wrap.md`). Awaiting next-epic decision.

**Open question:** what next? Two engine-side gaps filed during E-21 dogfooding (see `work/gaps.md`) lean toward sequencing a testing-rigor / engine-investigation milestone before E-22 Model Fit. Alternatively: E-15 Telemetry Ingestion is the long-pole for the client-telemetry vision; E-22 depends on E-15 + Telemetry Loop & Parity per D-045 Option A.

> **Note:** the catalog below is a historical trail kept manually; convention prefers a narrow narrative-only Current Work section. The catalog exceeds the 15-line guideline — slated for trim during a future cleanup pass; not in scope for this milestone.

- **E-17** Interactive What-If Mode — **completed and merged to main (2026-04-12).** Archived to `work/epics/completed/E-17-interactive-what-if-mode/`.
  - 6 milestones. WebSocket bridge → parameter panel → topology heatmap → warnings → edge heatmap → time scrubber. 200 vitest + 26 Playwright E2E.
- **E-18** Time Machine (`work/epics/E-18-headless-pipeline-and-optimization/spec.md`) — **in-progress** — foundation + analysis layer delivered; Fit + Chunked + SDK carried forward as **E-22**.
  - Headless engine: parameterized evaluation → streaming protocol → pipeline component.
  - **M-001** (complete): Parameterized evaluation — ParamTable, evaluate_with_params, compile-once eval-many.
  - **M-002** (complete): Engine session + streaming protocol — persistent process, MessagePack over stdin/stdout.
  - **M-004** (complete): `FlowTime.TimeMachine` project created; `FlowTime.Generator` deleted (Path B, no coexistence window).
  - **M-003** (complete): Tiered validation — `TimeMachineValidator` (schema/compile/analyse), `POST /v1/validate`, Rust `validate_schema` session command.
  - **M-005** (complete): `ITelemetrySource` interface + `CanonicalBundleSource` + `FileCsvSource`. 23 tests.
  - **M-006** (complete): Parameter sweep — `SweepSpec`/`SweepRunner`/`ConstNodePatcher`, `IModelEvaluator`/`RustModelEvaluator`, `POST /v1/sweep`. 35 tests.
  - **M-007** (complete): Sensitivity analysis — `ConstNodeReader`, `SensitivitySpec`/`SensitivityRunner` (central difference), `POST /v1/sensitivity`. 39 tests.
  - **M-008** (complete): Goal seeking — `GoalSeekSpec`/`GoalSeeker` (bisection), `POST /v1/goal-seek`. 33 tests.
  - **M-009** (complete): Optimization — `OptimizeSpec`/`Optimizer` (Nelder-Mead, N params), `POST /v1/optimize`. 29 unit + 10 API tests.
  - **M-010** (complete, merged to epic 2026-04-15): `SessionModelEvaluator` — persistent `flowtime-engine session` subprocess, MessagePack over stdin/stdout, compile-once/eval-many. `RustEngine:UseSession` config switch (default true); `RustModelEvaluator` retained as fallback. DI lifetime moved Singleton → Scoped. 44 new tests (32 unit + 8 integration + 4 API DI) — every reachable branch covered.
  - **M-011** (complete, merged to epic 2026-04-15): .NET Time Machine CLI — `flowtime validate/sweep/sensitivity/goal-seek/optimize` as pipeable JSON-over-stdio commands byte-compatible with `/v1/` endpoints. `--no-session` selects `RustModelEvaluator`; `--engine`/`FLOWTIME_RUST_BINARY`/default path resolution; exit code contract (0/1/2/3); `CliJsonIO` + `CliCommonArgs` + `CliEngineSetup` + `AnalysisCliRunner` shared helpers. 72 CLI unit + 10 integration tests — every reachable branch covered with one documented platform-edge gap. Full suite 1,702 passed / 9 skipped.
  - **Gap analysis:** `work/epics/E-18-headless-pipeline-and-optimization/e18-gap-analysis.md`
  - **Active delivery sequence (decided 2026-04-15, Option A):**
    1. ~~**M-010 SessionModelEvaluator**~~ — complete and merged to epic branch.
    2. ~~**M-011 .NET Time Machine CLI**~~ — complete and merged to epic branch.
    3. ~~**UI parity fork**~~ — now **E-21 Svelte Workbench & Analysis Surfaces** (in-progress). Svelte becomes platform for new telemetry/fit/discovery surfaces; Blazor → maintenance mode.
    4. **E-15 Telemetry Ingestion** — Gold Builder → Graph Builder → first dataset path.
    5. **Telemetry Loop & Parity** — parity harness (prerequisite for trustworthy fit).
    6. **E-22 Model Fit + Chunked Evaluation + Pipeline SDK** — carries the remaining E-18 scope forward as a dedicated epic. Fit, chunked evaluation, and the `FlowTime.Pipeline` embeddable SDK.
  - **Aspirational (see ROADMAP.md Cloud Deployment section):** Azure-native deployment — batch Functions, event-driven, long-running Container Apps service — with cloud `ITelemetrySource` adapters (ADX, Blob, Event Hubs), Blob artifact sink, OTEL/App Insights. Not scheduled; marker so near-term work stays compatible.
  - **Deferred (tracked in `work/gaps.md`):** optimization constraints, Monte Carlo, `FlowTime.Telemetry.*` adapters.
  - **Architecture:** `docs/architecture/headless-engine-architecture.md` — four-layer design; `docs/architecture/time-machine-analysis-modes.md` — sweep/sensitivity/goal-seek/optimize.
- **E-20** Matrix Engine — **completed and merged to main (2026-04-10).** Archived to `work/epics/completed/E-20-matrix-engine/`.
  - 10 milestones. 172 Rust tests + 1,332 .NET tests. E-17/E-18 unblocked.
- **E-10** Engine Correctness — **completed and merged to main (2026-04-09).** Archived to `work/epics/completed/E-10-engine-correctness-and-analytics/`.
- **E-16** Formula-First Core Purification — **completed.** Archived to `work/epics/completed/E-16-formula-first-core-purification/`.
- **E-19** Surface Alignment & Compatibility Cleanup — **completed and merged to main (2026-04-08).** Archived to `work/epics/completed/E-19-surface-alignment-and-compatibility-cleanup/`.
  - Deferred (tracked in `work/gaps.md`): `POST /v1/run` and `POST /v1/graph` Engine route deletion per D-042 (test-infrastructure migration needed first).
- **E-24** Schema Alignment — **completed and merged to main (2026-04-25).** Archived to `work/epics/completed/E-24-schema-alignment/`.
  - 5 milestones. Unified the post-substitution model: one C# type (`ModelDto`+`ProvenanceDto` in `FlowTime.Contracts`), one schema (`docs/schemas/model.schema.yaml`), one validator. `SimModelArtifact` + 6 satellites deleted; Sim emits the unified type directly; Engine parses it directly. Mirrored `ParseScalar` `ScalarStyle.Plain` guard + sibling `QuotedAmbiguousStringEmitter` round-trip pair. Canary `Survey_Templates_For_Warnings` promoted to hard `val-err == 0` build-time gate. 5 ADRs ratified. Closure logged in `D-2026-04-25-038`; E-23 unblocked.
- **E-23** Model Validation Consolidation — **completed and merged to main (2026-04-26).** Archived to `work/epics/completed/E-23-model-validation-consolidation/`.
  - 3 milestones. M-046: 94 rules audited; 16 schema-add edits + 5-arm `oneOf` schema restructure + silent-error fallback; 12 named adjunct methods on `ModelSchemaValidator`; 32-test negative-case regression catalogue. M-047: 3 production call sites + 28 test calls migrated to `ModelSchemaValidator.Validate`; `TimeMachineValidator` redundant-delegation block removed; real `ProvenanceService.StripProvenance` round-trip bug fixed via YamlStream surgical removal preserving scalar styles; +16 tests including a watertight `/v1/run`-level integration regression. M-048: `ModelValidator.cs` deleted; `ValidationResult` relocated to its own file. **`ModelSchemaValidator.Validate` is now the single model-YAML validator in the codebase.** Final suite 1862 / 0 / 9. Stashed input material from the pre-pivot `milestone/m-E23-01-schema-alignment` branch + `stash@{0}` is now obsolete — schema-alignment work was absorbed by E-24 M-051 / M-053; safe to discard.
  - **Unblocks:** M-044 Validation Surface (Svelte) — consumes the consolidated `ModelSchemaValidator` once E-21 resumes.
- **E-21** Svelte Workbench & Analysis Surfaces (`work/epics/completed/E-21-svelte-workbench-and-analysis/spec.md`) — **resumed (2026-04-26)** — paused 2026-04-23 to run E-24 then E-23; both closed. M-043 Heatmap View merged into `epic/E-21-svelte-workbench-and-analysis` 2026-04-26 (backfill of the missing wrap-time merge); main caught up onto the epic branch in the same pass. Reentry point is M-044 Validation Surface — consumes the consolidated `ModelSchemaValidator` that E-23 delivers.
  - **M-038** (complete, merged to epic): Workbench Foundation — density system, dag-map events (library), workbench panel with click-to-pin node cards. 217 vitest + 293 dag-map tests.
  - **M-039** (complete, merged to epic): Metric Selector & Edge Cards — metric chip bar, edge click-to-pin, edge cards, class filter, custom TimelineScrubber, dark-mode/viz-palette fixes. 323 vitest + 293 dag-map = 616 tests.
  - **M-040** (complete, merged to epic 2026-04-17; ultrareview follow-ups 2026-04-20): Sweep & Sensitivity Surfaces — `/analysis` route with tabbed surfaces, sweep config + results, sensitivity bar chart. 433 vitest + 293 dag-map = 726 tests; 8 Playwright specs. D-046 ratifies the `GET /v1/runs/{runId}/model` backend carve-out.
  - **M-041** (complete, merged to epic 2026-04-22 in commit `8c4898f`; **scope split 2026-04-21** — Optimize moved to M-042): Goal Seek Surface — goal-seek panel on `/analysis`, shared `AnalysisResultCard` + `ConvergenceChart` components (pure-SVG with geometry siblings), `interval-bar-geometry` for search-interval visualization, new `flowtime.goalSeek(...)` client method. Backend `trace` on both `/v1/goal-seek` and `/v1/optimize` per **D-047** (backend landed in commit `29ac3e9`; optimize trace ready for M-042). 482 vitest (+49 new) + 293 dag-map = 775 tests; 8 Playwright passing / 1 pre-existing env flake. Full branch-coverage audit (backend + UI) in tracking doc.
  - **M-042** (complete, merged to epic 2026-04-22 in commit `a94fc66`): Optimize Surface — live `/v1/optimize` wired to the `/analysis` Optimize tab (N-param Nelder-Mead under bounds). Consumes shared `AnalysisResultCard` + `ConvergenceChart` + `interval-bar-geometry` from M-041; adds `flowtime.optimize(...)` client method; sibling `optimize-helpers.ts` module for form validation. Per-param result table with separate range-bar column (via `intervalMarkerGeometry`) showing where each optimized value landed inside its bound. Backend trace landed in M-041 commit `29ac3e9` — no backend work this milestone. 520 vitest (+19 new) · 11/11 optimize Playwright specs green against live Rust engine · 1 pre-existing sweep env flake (unchanged from main). Full branch-coverage audit in tracking doc.
  - **M-043** (complete, completed 2026-04-24 in commit `5dddb5d` on branch `milestone/m-E21-06-heatmap-view`): Heatmap View — nodes-x-bins grid as sibling of topology under `/time-travel/topology` with typed `<ViewSwitcher>` (inline views, no registry per ADR-m-E21-06-01), shared view-state store (`view-state.svelte.ts`), shared full-window 99p-clipped color-scale normalization (topology straight-swapped from per-bin per ADR-m-E21-06-02 — no escape hatch), shared-toolbar `[ Operational | Full ]` node-mode toggle reaching Blazor parity (AC15, `mode` param on `getStateWindow`). 15/15 ACs landed; zero backend work (`state_window` sufficed). Multiple mid-flight spec amendments (all dated and captured): pinned-first row-float removed (pin glyph sole indicator); column highlight reduced from full-column outline to top-bar marker; persistent-selection SVG overlay keyed to `viewState.selectedCell` (survives window blur) + workbench-card title cross-link; auto-fit `CELL_W` with fractional pixels + 4 px right-margin + root-layout `min-w-0` plumbing so SVG never exceeds container (iteration log v1→v5 in tracking doc); `fitWidth` toggle persisted in shared store. Dead-code cleanup landed the new framework guard (2026-04-23): `buildMetricMapForDef` + `buildMetricMapForDefFiltered` + `pinnedIds` on `HeatmapGridInput` deleted once they had no production callers — guard mirrored into `.ai-repo/rules/project.md` + `CLAUDE.md`. New chrome tokens `--ft-pin` (red pin glyph) + `--ft-highlight` (turquoise highlight/selection/card-title). **770 ui-vitest passing across 32 suites** (net +269 from the 501 baseline) · 16 Playwright specs on `svelte-heatmap.spec.ts` (13 AC14 + #12b/#12c/#12d/#12e), 11 pass / 2 graceful-skip on fixtures without class metadata · `.NET` green on single-test re-run (one pre-existing timing flake in a file untouched by this milestone). Four gaps filed (topology keyboard/ARIA posture, data-viz palette color-blind validation, heatmap sliding-window scrubber, bidirectional card↔view reverse cross-link). Full branch-coverage audit in tracking doc.
  - 8 milestones (was 7 before split): workbench foundation → metric selector + edge cards → sweep/sensitivity → goal-seek → optimize → heatmap view → validation surface → polish.
  - Absorbs E-11 M5/M7/M8 under workbench paradigm. Svelte is the platform for new surfaces; Blazor is maintenance-only.
- **E-11** Svelte UI — paused after M6; absorbed into E-21
  - M1-M4 + M6 done. M5 (Inspector) → E-21 workbench. M7 (Dashboard) → deferred. M8 (Polish) → E-21 M-045.
- **E-12–E-15:** planned, not started. E-15 is on critical path for client-telemetry vision.
- **E-22** Time Machine: Model Fit & Chunked Evaluation (`work/epics/E-22-model-fit-chunked-evaluation/spec.md`) — **planning**
  - Carries the remaining E-18 scope forward: model fit against real telemetry (`POST /v1/fit`), chunked evaluation (Rust `chunk_step` protocol + `POST /v1/chunked-eval`), and the `FlowTime.Pipeline` embeddable SDK wrapper.
  - **Planned milestones (3):** m-E22-01 Model Fit, m-E22-02 Chunked Evaluation, m-E22-03 FlowTime.Pipeline SDK.
  - **Depends on:** E-15 Telemetry Ingestion, Telemetry Loop & Parity. Sequenced after both per D-045 Option A.
  - **Out of scope (tracked in `work/gaps.md`):** optimization constraints, Monte Carlo, `FlowTime.Telemetry.*` direct-source adapters.