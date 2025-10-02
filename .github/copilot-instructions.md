# Copilot instructions for FlowTime

Purpose: give AI agents the minimum context to be productive and safe in this repo.

## Guardrails
- Don't push (no `git push`) or make network calls unless explicitly requested.
- Don't commit or stage changes without explicit user approval. Propose edits first; commit only after the user says to.
- Prefer editor-based edits; avoid cross-project refactors without context.
- Always build and run tests before finishing; keep solution compiling.
- **No time estimates in documentation**: Don't write hours, days, or weeks in docs. No effort estimates in milestones, roadmaps, or planning documents.
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
- **Milestone-driven versioning**: Major.Minor reflects capability level, not arbitrary breaking changes
- **Version format**: `<Major>.<Minor>.<Patch>[-<PreRelease>]`
  - **Patch**: Bug fixes, CLI improvements, documentation updates within milestone scope
  - **Minor**: Milestone completion, new capabilities, API additions
  - **Major**: Reserved for fundamental architecture changes or major breaking changes
  - **PreRelease**: `-preview`, `-rc` during development cycles
- **Pre-merge review**: Before merging to main (release), evaluate:
  - Does this complete a milestone? → Minor bump
  - Is this a bug fix or improvement within current milestone? → Patch bump
  - Does this break existing APIs or fundamentally change architecture? → Major bump
  - Is this work-in-progress toward next milestone? → PreRelease suffix
- **Version consistency**: Update `<VersionPrefix>` in all `.csproj` files together
- **No hardcoded automation**: Version decisions made during merge review, not automated based on branch names

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

## Coding patterns and style
- .NET 9, C# nullable + implicit usings enabled.
- Avoid private field names starting with `_` (analyzers may flag them in tests).
- For API changes, update `.http` examples and API tests together.

## Roadmap-driven areas (reference docs, don’t hard-code here)
- Expression grammar beyond M0, DTO versioning, route semantics (e.g., GET vs POST for `/graph`), and deeper Core conventions are governed by the roadmap/milestones. Consult:
  - `docs/ROADMAP.md`
  - `docs/milestones/`
  - project docs within each milestone
