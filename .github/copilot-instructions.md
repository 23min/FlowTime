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
- **Development vs Production phases**: 0.x.x for development, 1.x.x+ for production-ready releases
- **Version format**: `<Major>.<Minor>.<Patch>[-<PreRelease>]`

### Development Phase (0.x.x)
- **0.x.0**: Milestone completions during development phase (breaking changes acceptable)
- **0.x.y**: Bug fixes, improvements, and features within development milestones
- **API instability expected**: Contracts, schemas, and interfaces may change between releases
- **Breaking changes allowed**: Focus on capability delivery over backward compatibility

### Production Phase (1.x.x+)  
- **1.0.0**: First stable release with API/contract stability commitments
- **1.x.0**: New capabilities with backward compatibility promises
- **x.0.0**: Breaking changes only for fundamental architecture evolution (rare)

### Pre-merge review guidance:
- **Development phase**: Does this complete a milestone? → 0.x.0 bump | Bug fix/improvement? → 0.x.y bump
- **Production phase**: New capability with compatibility? → x.y.0 | Breaking change? → y.0.0 | Bug fix? → x.y.z
- **PreRelease**: `-preview`, `-rc` during development cycles
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
