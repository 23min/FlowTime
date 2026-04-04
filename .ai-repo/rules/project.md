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
- `work/` — AI framework housekeeping: epics, milestones, tracking docs, gaps, decisions.

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

- Branches: `milestone/mX` for integration, `feature/<surface>-mX/<desc>` per feature.
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

## Truth Discipline

### Precedence (highest to lowest)
1. **Code + passing tests** define live truth.
2. **`work/decisions.md`** defines approved direction.
3. **Milestone and epic specs** (`work/milestones/`, `work/epics/`) define implementation target, within their scope.
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
- Do not treat aspirational docs as implementation authority.

## Documentation

- Engine + shared docs in `docs/`; use Mermaid for diagrams (not ASCII art).
- Keep docs/schemas/templates aligned when touching contracts or schemas.
- Repository language: English. No time or effort estimates in docs or plans.
- Treat sibling checkouts as read-only references unless the user instructs otherwise.
