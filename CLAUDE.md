# CLAUDE.md

This project uses **aiwf v3** (Go binary at `~/go/bin/aiwf`) plus the **ai-workflow-rituals** plugin marketplace (Claude Code plugins `aiwf-extensions` + `wf-rituals`). The planning kernel lives in 6 entity kinds: epic, milestone, ADR, gap, decision, contract — each with a closed status set, stable ids that survive rename/reallocate, and `git log` as the audit trail.

Despite the filename, this file is shared workspace context for both Claude and GitHub Copilot in this repo. Keep it assistant-neutral.

## Session Start

For project state, run `aiwf status` (one-screen snapshot of in-flight work, open decisions, gaps, recent activity). `STATUS.md` is the same data auto-rendered. Recently-shipped epic context lives in each epic's `wrap.md`.

After `/compact` or a fresh session, this file is re-available via the system prompt. Say **"refresh context"** to re-read everything.

## Hard Rules

- **NEVER commit or push without explicit human approval** — "continue" / "ok" do not count
- **TDD by default** for logic, API, and data code — red → green → refactor
- **Branch coverage** — every reachable conditional branch needs a test before declaring done; perform a line-by-line audit before the commit-approval prompt
- **Branch discipline** — do NOT commit milestone work directly to `main`
- Conventional Commits format: `feat(api):`, `fix(sim):`, `chore:`, `docs:`, `test:`, `refactor:` — no icons/emoji; subject + short bullet body capturing the milestone and key work/tests touched

## Agent Routing

Role agents ship via the `aiwf-extensions` plugin (loaded into Claude Code from the plugin cache).

| Intent | Agent | Drives |
|--------|-------|--------|
| build, implement, code, start, fix, patch | **builder** | `aiwfx-start-milestone` → `wf-tdd-cycle` → `aiwfx-wrap-milestone`; `wf-patch` for one-offs |
| plan, design, scope, epic, architecture | **planner** | `aiwfx-plan-epic`, `aiwfx-plan-milestones`, `aiwfx-record-decision` |
| review, check, validate, wrap, finish | **reviewer** | `wf-review-code`; status promotions via `aiwf promote` |
| release, deploy, tag, publish | **deployer** | `aiwfx-release` |

After a wrap, builder/reviewer should also invoke the repo-private `dead-code-audit` skill (the upstream `aiwfx-wrap-milestone` skill does not chain it).

## Project Layout

**Source tree**

- `src/FlowTime.Core`, `src/FlowTime.API`, `src/FlowTime.Cli`, `src/FlowTime.Contracts`, `src/FlowTime.Adapters.Synthetic` — Engine surface.
- `src/FlowTime.Sim.Core`, `src/FlowTime.Sim.Service`, `src/FlowTime.Sim.Cli` — Simulation surface.
- `src/FlowTime.UI`, `src/FlowTime.UI.Tests` — Blazor WebAssembly UI.
- `ui/` — SvelteKit + shadcn-svelte frontend.
- `tests/` mirrors project names (e.g., `tests/FlowTime.Core.Tests`, `tests/FlowTime.Sim.Tests`, `tests/FlowTime.Api.Tests`); UI Playwright at `tests/ui/`.
- `docs/` — Engine/shared documentation. `docs-sim/` is archived — ignore unless explicitly requested.

**Planning tree** (canonical layout defined by `aiwf.yaml`; per-kind frontmatter via `aiwf schema`)

| Path | Purpose |
|------|---------|
| `aiwf.yaml` | aiwf consumer config |
| `work/epics/E-NN-<slug>/` | epic dirs (active and done), with `epic.md` + `M-NNN-<slug>.md` milestones |
| `work/decisions/D-NNN-<slug>.md` | per-decision entities |
| `work/gaps/G-NNN-<slug>.md` | per-gap entities |
| `work/contracts/C-NNN-<slug>/` | contract entities |
| `docs/adr/ADR-NNNN-<slug>.md` | ADRs |
| `work/archived-epics/<slug>/` | pre-aiwf historical epics (no E-NN id; out of aiwf's walked roots) |
| `.claude/skills/aiwf-*/` | gitignored, materialized by `aiwf init` / `aiwf update` |

`aiwf` verbs: `init`, `add <kind>`, `promote`, `cancel`, `rename`, `reallocate`, `move`, `check`, `history`, `status`, `render roadmap`, `doctor`, `import`, `schema`, `template`, `contract verify`. Run `aiwf help` for the full list. Don't edit entity frontmatter status by hand — use `aiwf promote` so the FSM check + commit trailer happen.

Tracking docs (per the `aiwfx-track` skill) are advisory free-form markdown alongside a milestone spec; not aiwf entities, not validated. Older `*-log.md` / `*-tracking.md` files in `work/archived-epics/` are pre-aiwf residue.

## Coding Conventions

- .NET 9 / C# 13 with implicit usings and nullable enabled.
- Private fields **must use camelCase without a leading underscore** (`private readonly string dataDirectory;`).
- Follow existing patterns before introducing new abstractions; check for shared contracts in `FlowTime.Contracts`.
- Keep CLI ↔ API behaviour aligned; update relevant `.http` examples and docs when changing endpoints.
- Use invariant culture for parsing/formatting; keep tests deterministic.
- JSON payloads and schemas use camelCase — do not introduce snake_case fields.
- Never reintroduce deprecated schema fields (e.g., `binMinutes`); current schema uses `{ bins, binSize, binUnit }`.
- When inserting inline code containing `|` inside Markdown tables, escape the pipe as `\|`.
- Prefer precise edits; stick to established patterns and avoid broad refactors without context. Use `rg`/`fd` for searches.

## Branching & Versioning

- Epic integration branches use `epic/E-{NN}-<slug>`. Every numbered epic gets an integration branch; milestone branches branch from it and merge back into it.
- Milestone branches use `milestone/<milestone-id>`.
- Feature branches use `feature/<surface>-<milestone-id>/<desc>` when a milestone needs parallel work.
- Single-surface quick changes can branch from `main` and PR directly back to `main` when no milestone integration branch is needed.
- Version format `<major>.<minor>.<patch>[-pre]`; milestone completions typically bump minor (e.g., `0.6.0 → 0.7.0`).
- Release notes in `docs/releases/` with milestone-based naming (e.g., `SIM-M2.7-v0.6.0.md`).

## Build & Run

- `dotnet build FlowTime.sln` / `dotnet test FlowTime.sln`
- VS Code tasks: `build`, `test`, `start-api`, `stop-api`, `start-sim-api`, `stop-sim-api`, `start-ui`, `stop-ui`
- Engine API: `dotnet run --project src/FlowTime.API` → port 8081
- Sim API: `dotnet run --project src/FlowTime.Sim.Service` with `ASPNETCORE_URLS=http://0.0.0.0:8090`
- Blazor UI: `dotnet run --project src/FlowTime.UI` → port 5219
- Svelte UI: `cd ui && npm run dev` → port 5173
- Default ports: 8081 (Engine API), 8090 (Sim API), 5173 (Svelte), 5219/7047 (Blazor), 8091 (Sim diagnostics), 5091 (Engine dev profile)
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
2. **Decision entities** (`work/decisions/D-NNN-*.md`) and **ADRs** (`docs/adr/ADR-*.md`) define approved direction.
3. **Epic specs and epic-local milestone specs** under `work/epics/` define implementation target, within their scope.
4. **Architecture docs** (`docs/`) summarize and connect the above — they never outrank code or decisions.
5. **Historical and exploration docs** are context only — never implementation authority.

If code, decisions, and an architecture doc disagree, do not choose arbitrarily. Report the mismatch and ask.

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
