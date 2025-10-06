# Copilot instructions for FlowTime

Purpose: give AI agents the minimum context to be productive and safe in this repo.

## Serena
# Copilot Instructions for FlowTime Project

## Code Style Rules (Apply to ALL code generation, including Serena)

**Private Field Naming Convention (STRICT):**
- ✅ Use **camelCase** WITHOUT underscore prefix: `dataDirectory`, `indexLock`, `registry`
- ❌ NEVER use underscore prefix: `_dataDirectory`, `_indexLock`, `_registry`
- This prevents analyzer warnings in test projects
- Example: `private readonly string dataDirectory;` NOT `private readonly string _dataDirectory;`

## Code Navigation and Editing Rules

**ALWAYS use Serena MCP tools for code operations:**

### Before any implementation:
1. Use `serena__find_symbol` to locate relevant existing code
2. Use `serena__get_symbols_overview` to understand file structure
3. Use `serena__find_referencing_symbols` to see how code is used

### When writing new code:
1. Use `serena__find_symbol` to find similar patterns in the codebase
2. Look for existing base classes, interfaces, or patterns to follow
3. Use `serena__insert_after_symbol` or `serena__replace_symbol_body` for precise placement

### When modifying code:
- NEVER read entire files unless absolutely necessary
- Use `serena__read_file` for targeted file reading
- Use `serena__replace_symbol_body` to modify specific methods/classes
- Use `serena__insert_after_symbol` to add new code near existing symbols

### When writing tests:
1. Use `serena__find_symbol` to locate the class being tested
2. Find existing test patterns with `serena__find_symbol` (e.g., "Test")
3. Understand test structure before generating new tests

### Examples:
- ❌ "Read OrderService.cs and modify the CalculateTotal method"
- ✅ "Use serena__find_symbol to locate CalculateTotal, then serena__replace_symbol_body to update it"

- ❌ "Show me all the files in src/"
- ✅ "Use serena__find_symbol to find all classes in the Services namespace"

## Project Structure
- API is in `src/FlowTime.Api/`
- CLI is in `src/FlowTime.CLI/`
- Core logic (Engine) is in `src/FlowTime.Core/`

- Tests follow pattern: `ClassName` → `ClassNameTests`
- Most test locations follow this pattern `tests/` + name of the project + `Tests` (e.g., `tests/FlowTime.Core.Tests/`)
- Some tests are located simply in `tests/FlowTime.Tests` (for cross-cutting tests)

Prefer semantic, symbol-level operations over file-level operations whenever possible.


## Guardrails
- Don't push (no `git push`) or make network calls unless explicitly requested.
- Don't commit or stage changes without explicit user approval. Propose edits first; commit only after the user says to.
- Prefer editor-based edits; avoid cross-project refactors without context.
- Always build and run tests before finishing; keep solution compiling.
- **No time estimates in documentation**: Don't write hours, days, or weeks in docs. No effort estimates in milestones, roadmaps, or planning documents. See `docs/development/milestone-documentation-guide.md` for detailed rules.
- **Repository access**: When working from flowtime-vnext container, treat flowtime-sim-vnext as read-only reference. Only commit to the flowtime-sim-vnext repository unless explicitly requested to modify flowtime-sim-vnext.
- **Process safety**: When managing solution services, use safe process management:
  - Use `lsof -ti:PORT | xargs kill` or process name patterns like `pkill -f "ProcessName"`
  - NEVER kill processes by bare PID numbers (e.g., `kill 8080`, `kill 5219`) as these could accidentally target system processes
  - Solution ports: 8080 (FlowTime API), 5219/7047 (FlowTime UI), 8090 (FlowTime-Sim API), 5091
  - When looking up PIDs by port, always verify the process before killing and use proper commands

## Branching and commits
- Milestones: integrate on `milestone/mX` (m0 → m1 → …). Features target the milestone branch.
  - Feature branches: `feature/<surface>-mX/<short-desc>` (e.g., `feature/api-m0/run-endpoint`).
- Conventional Commits: `feat(api): ...`, `fix(core): ...`, `chore(repo): ...`, `docs: ...`, `test(api): ...`.
- See `docs/development/branching-strategy.md` for the full workflow.

## Versioning strategy
- **Milestone-driven versioning**: Version reflects capability progression, not arbitrary changes
- **Version format**: `<Major>.<Minor>.<Patch>[-<PreRelease>]`
- **0.x Phase (Pre-Production)**: API may change, 0.x can go beyond 0.9 (e.g., 0.10.0, 0.11.0, 0.50.0)
  - **Patch (0.6.x)**: Small milestones, features, bug fixes within current major milestone series
  - **Minor (0.x.0)**: Major milestone completions, new subsystems, significant capabilities
  - **Major (1.0.0)**: Reserved for production-ready release with API stability commitment
  - **PreRelease**: `-preview`, `-rc` during development cycles

- **Pre-merge review**: Before merging to main, evaluate:
  - Is this a major milestone (M3.0, M4.0, etc.)? → Minor bump (0.6.0 → 0.7.0)
  - Is this a small milestone or feature addition? → Patch bump (0.6.0 → 0.6.1)
  - Bug fix only? → Patch bump (0.6.0 → 0.6.1)
  - Production-ready with API stability? → Major bump (0.x.y → 1.0.0)

- **Examples**: M2.10 → 0.6.1, M3.0 → 0.7.0, M4.0 → 0.8.0, M6.0 → 0.10.0, v1.0 → 1.0.0
- **Version consistency**: Update `<VersionPrefix>` in all `.csproj` files together
- **No hardcoded automation**: Version decisions made during merge review, not automated based on branch names

## Post-merge ceremony (after merging to main)
**Complete ceremony required** - See `docs/development/release-ceremony.md` for full details

Quick checklist:
1. **Decide version**: Minor (major milestone M3.0+), Patch (small milestone/features), Major (1.0 stable)
2. **Update all .csproj files**: Change `<VersionPrefix>` in all 5 projects
3. **Commit version bump**: `git commit -m "chore(release): bump version to X.Y.Z"`
4. **Create release doc**: `docs/releases/M<X>.<Y>-vX.Y.Z.md` (use template from ceremony doc)
5. **Commit release doc**: `git commit -m "docs(release): add M<X>.<Y> release notes"`
6. **Create git tag**: `git tag -a vX.Y.Z -m "Release vX.Y.Z - [description]"`
7. **Push tag**: `git push origin vX.Y.Z`
8. **Push main**: `git push origin main`
9. **Clean up branches**: Delete feature/milestone branches
10. **Verify**: Check tag exists, tests pass, version consistent

**Files to update for version bump:**
- `src/FlowTime.API/FlowTime.API.csproj`
- `src/FlowTime.Core/FlowTime.Core.csproj`
- `src/FlowTime.Cli/FlowTime.Cli.csproj`
- `src/FlowTime.Contracts/FlowTime.Contracts.csproj`
- `src/FlowTime.Adapters.Synthetic/FlowTime.Adapters.Synthetic.csproj`

## Documentation conventions
- **Release documents**: Use milestone-based naming pattern `M<X>.<Y>-v<major>.<minor>.<patch>.md` (e.g., `M2.0-v0.4.0.md`)
  - Captures both milestone completion and semantic version
  - More meaningful than pure semantic version naming for milestone-driven development
  - Located in `docs/releases/`
  
## Dev workflows
- Tasks: build (`dotnet build`), test (`dotnet test`), run CLI example (see `.vscode/tasks.json`).
- Debug: 
  - CLI: ".NET Launch FlowTime.Cli (hello)"
  - API: ".NET Launch FlowTime.API" (http://localhost:5091). Use `src/FlowTime.API/FlowTime.API.http`.

## Testing conventions
- Docs: `docs/development/testing.md` (core vs API slice tests).
- Core tests: no web deps; deterministic; fast.
- API tests: `WebApplicationFactory<Program>` only; avoid mocking Core; include at least one negative case.

## Determinism and culture
- Use invariant culture for parsing/formatting.
- Numeric assertions: exact for M0 unless a tolerance is explicitly required.

## CLI ↔ API parity
- Maintain parity between CLI CSV and API JSON for the same model.
- Add/keep contract tests (future: dedicated parity suite). Update tests/docs on intentional changes.

## API architecture and patterns
- **Minimal APIs**: Use Minimal APIs (`.MapPost()`, `.MapGet()`, etc.) defined in `Program.cs`. Do NOT create controller classes.
- **Route pattern**: Group routes using `app.MapGroup("/v1")` for versioning.
- **Dependency injection**: Inject services directly into route handlers (e.g., `(HttpRequest req, IArtifactRegistry registry, ILogger<Program> logger) => { ... }`).
- **Testing**: Use `WebApplicationFactory<Program>` for integration tests. Make HTTP requests via `HttpClient`, never instantiate handlers directly.

## API contracts and errors (M0)
- Content type: `text/plain` with YAML body for `/run` and `/graph`.
- Error model: return `400 Bad Request` with `{ error: "..." }` for invalid input; avoid `500` by validating early.
- `/graph` semantics: POST in M0 (graph is compiled from request body; no server-side model). Add a GET variant when models become server resources (e.g., `GET /models/{id}/graph`).

## Schema evolution and compatibility
- **NO backward compatibility for OLD schema**: Engine, Sim, and UI must NOT support `binMinutes` in any external schema (API responses, artifacts, model files).
- **Current schema (M2.9+)**: `{ bins, binSize, binUnit }` - OLD schema `{ bins, binMinutes }` is rejected.
- **Internal usage only**: UI/Engine may compute `binMinutes` internally for display or calculations, but it must NEVER be serialized to/from JSON or YAML.
- **Breaking changes**: Schema changes are breaking and require coordinated updates across Engine, Sim, and UI.

## Coding patterns and style
- .NET 9, C# nullable + implicit usings enabled.
- **Private field naming convention**: Use **camelCase** WITHOUT underscore prefix (e.g., `dataDirectory`, `indexLock`, NOT `_dataDirectory`)
  - This is a strict project convention to avoid analyzer warnings in test projects
  - Exception: Legacy code like `Pcg32.cs` has underscore prefixes - do NOT replicate this pattern in new code
  - Examples: `private readonly string dataDirectory;`, `private static readonly SemaphoreSlim indexLock;`
- For API changes, update `.http` examples and API tests together.

## Roadmap-driven areas (reference docs, don’t hard-code here)
- Expression grammar beyond M0, DTO versioning, route semantics (e.g., GET vs POST for `/graph`), and deeper Core conventions are governed by the roadmap/milestones. Consult:
  - `docs/ROADMAP.md`
  - `docs/milestones/`
  - project docs within each milestone
