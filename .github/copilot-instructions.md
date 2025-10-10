# Copilot Instructions for the FlowTime Mono-Repo

Purpose: give AI assistants the minimum context to work safely and productively across the unified FlowTime Engine + FlowTime-Sim codebase.

---

## 1. Tooling & Workflow Guardrails
- Always use Serena MCP tools for navigation and edits (`serena__find_symbol`, `serena__read_file`, `serena__insert_after_symbol`, etc.); avoid whole-file reads unless necessary.
- Do not stage, commit, push, or make network calls unless the user explicitly asks.
- Prefer precise, symbol-level edits; stick to established patterns and avoid broad refactors without context.
- Build and test before handing work back (`dotnet build FlowTime.sln`, `dotnet test FlowTime.sln`).
- No time/effort estimates in docs or planning artifacts.
- Process safety: verify processes before killing (`lsof -ti:PORT`, `ps aux | grep`); use `pkill -f "ProcessName"` or `lsof -ti:PORT | xargs -r kill -TERM`. Never `kill <PORT>`.
- Default ports: 8080 (Engine API), 8090 (Sim API), 5219/7047 (UI), 8091 (Sim diagnostics), 5091 (Engine dev profile).
- Treat sibling checkouts as read-only references unless the user instructs otherwise.

---

## 2. Project Layout
- `src/FlowTime.Core`, `src/FlowTime.API`, `src/FlowTime.CLI`, `src/FlowTime.Contracts`, `src/FlowTime.Adapters.Synthetic`: Engine surface.
- `src/FlowTime.Sim.Core`, `src/FlowTime.Sim.Service`, `src/FlowTime.Sim.Cli`: Simulation surface.
- `ui/FlowTime.UI`: Blazor WebAssembly UI.
- Tests mirror project names under `tests/` (e.g., `tests/FlowTime.Core.Tests`, `tests/FlowTime.Sim.Tests`, `tests/FlowTime.Api.Tests`).
- Documentation in `docs/` (Engine/shared) and `docs-sim/` (Sim-specific, temporary until Phase 5).
- Devcontainer, scripts, GitHub workflows, and VS Code tasks are consolidated at repo root.

---

## 3. Coding Conventions
- .NET 9 / C# 13 with implicit usings and nullable enabled.
- Private fields **must use camelCase without a leading underscore** (`private readonly string dataDirectory;`).
- Follow existing patterns before introducing new abstractions; check for shared contracts in `FlowTime.Contracts`.
- Keep CLI ↔ API behaviour aligned; update relevant `.http` examples and docs when changing endpoints.
- Use invariant culture for parsing/formatting; keep tests deterministic.

---

## 4. Branching, Versioning, Releases
- Branches follow milestone-driven flow: `milestone/mX` for integration, `feature/<surface>-mX/<desc>` per feature.
- Conventional commits (`feat(api):`, `fix(sim):`, `docs:`, etc.).
- Version format `<major>.<minor>.<patch>[-pre]`; milestone completions typically bump the minor version (e.g., `0.6.0 → 0.7.0`).
- Release notes live in `docs/releases/` with milestone-based naming (e.g., `SIM-M2.7-v0.6.0.md`).

---

## 5. Build, Run, Test Shortcuts
- `dotnet build FlowTime.sln` / `dotnet test FlowTime.sln`.
- VS Code tasks (shared): `build`, `build-sim`, `test`, `test-sim`, `start-api`, `stop-api`, `start-sim-api`, `stop-sim-api`, `start-ui`, `stop-ui`.
- When running APIs manually:
  - Engine: `dotnet run --project src/FlowTime.API` (exposes :8080).
  - Sim: `dotnet run --project src/FlowTime.Sim.Service` with `ASPNETCORE_URLS=http://0.0.0.0:8090`.
  - UI: `dotnet run --project ui/FlowTime.UI`.

---

## 6. Testing Guidance
- Unit tests stay fast and deterministic; avoid network or file-system side effects.
- API tests use `WebApplicationFactory<Program>`; favour real dependencies over mocks when possible.
- Sim tests live in `tests/FlowTime.Sim.Tests` and cover CLI, template parsing, provenance, and service behaviours.
- Add integration tests under `tests/FlowTime.Integration.Tests` for cross-surface scenarios (planned expansion).

---

## 7. Documentation & Schema Notes
- Engine + shared docs remain in `docs/`; Sim docs temporarily reside in `docs-sim/` until documentation migration.
- Shared schema files live in `docs/schemas/`; Sim-specific schema mirrors exist under `docs-sim/schemas/`.
- Never reintroduce deprecated schema fields (e.g., `binMinutes`); current schema uses `{ bins, binSize, binUnit }`.

Keep these conventions in mind when generating or modifying code, docs, or configuration so FlowTime Engine and FlowTime-Sim remain aligned in the mono-repo.***
