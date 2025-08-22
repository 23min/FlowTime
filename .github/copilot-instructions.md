# Copilot instructions for FlowTime

Purpose: give AI agents the minimum context to be productive and safe in this repo.

## Guardrails
- Don’t push (no `git push`) or make network calls unless explicitly requested.
- Prefer editor-based edits; avoid cross-project refactors without context.
- Always build and run tests before finishing; keep solution compiling.

## Branching and commits
- Milestones: integrate on `milestone/mX` (m0 → m1 → …). Features target the milestone branch.
  - Feature branches: `feature/<surface>-mX/<short-desc>` (e.g., `feature/api-m0/run-endpoint`).
- Conventional Commits: `feat(api): ...`, `fix(core): ...`, `chore(repo): ...`, `docs: ...`, `test(api): ...`.
- See `docs/branching-strategy.md` for the full workflow.

## Dev workflows
- Tasks: build (`dotnet build`), test (`dotnet test`), run CLI example (see `.vscode/tasks.json`).
- Debug: 
  - CLI: “.NET Launch FlowTime.Cli (hello)”
  - API: “.NET Launch FlowTime.API” (http://localhost:5091). Use `apis/FlowTime.API/FlowTime.API.http`.

## Testing conventions
- Docs: `docs/testing.md` (core vs API slice tests).
- Core tests: no web deps; deterministic; fast.
- API tests: `WebApplicationFactory<Program>` only; avoid mocking Core; include at least one negative case.

## Determinism and culture
- Use invariant culture for parsing/formatting.
- Numeric assertions: exact for M0 unless a tolerance is explicitly required.

## CLI ↔ API parity
- Maintain parity between CLI CSV and API JSON for the same model.
- Add/keep contract tests (future: dedicated parity suite). Update tests/docs on intentional changes.

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
