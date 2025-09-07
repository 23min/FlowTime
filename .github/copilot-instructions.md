# Copilot instructions for FlowTime

Purpose: give AI agents the minimum context to be productive and safe in this repo.

## Guardrails
- Don’t push (no `git push`) or make network calls unless explicitly requested.
- Don’t commit or stage changes without explicit user approval. Propose edits first; commit only after the user says to.
- Prefer editor-based edits; avoid cross-project refactors without context.
- Always build and run tests before finishing; keep solution compiling.

## Branching and commits
- Milestones: integrate on `milestone/mX` (m0 → m1 → …). Features target the milestone branch.
  - Feature branches: `feature/<surface>-mX/<short-desc>` (e.g., `feature/api-m0/run-endpoint`).
- Conventional Commits: `feat(api): ...`, `fix(core): ...`, `chore(repo): ...`, `docs: ...`, `test(api): ...`.
- See `docs/branching-strategy.md` for the full workflow.

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

## Dev workflows
- Tasks: build (`dotnet build`), test (`dotnet test`), run CLI example (see `.vscode/tasks.json`).

## Testing conventions
- Docs: `docs/testing.md` (core vs API slice tests).
- Core tests: no web deps; deterministic; fast.
- API tests: `WebApplicationFactory<Program>` only; avoid mocking Core; include at least one negative case.

## Determinism and culture
- Use invariant culture for parsing/formatting.
- Numeric assertions: exact for M0 unless a tolerance is explicitly required.

## Coding patterns and style
- .NET 9, C# nullable + implicit usings enabled.
- Avoid private field names starting with `_` (analyzers may flag them in tests).
- For API changes, update `.http` examples and API tests together.

## Roadmap-driven areas (reference docs, don’t hard-code here)
- deeper Core conventions are governed by the roadmap/milestones. Consult:
  - `docs/ROADMAP.md`
  - `docs/milestones/`
  - project docs within each milestone
