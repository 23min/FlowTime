# Copilot instructions for FlowTime

Purpose: give AI agents the minimum context to be productive and safe in this repo.

## Testing conventions
- Docs: `docs/testing.md` explains scope and how to run.

## Coding patterns and style
- .NET 9, C# nullable + implicit usings enabled.
- Avoid private field names starting with `_` (analyzers may flag them in tests).
- For API changes, update both `.http` examples and API tests.

## Branching and commits
- Milestone workflow (see `docs/branching-strategy.md`):
  - Use `milestone/m0` when multiple surfaces (API/UI/CLI) move together.
  - Feature branches: `feature/<surface>-m0/<short-desc>` targeting the milestone branch.
- Conventional Commits: `feat(api): ...`, `fix(core): ...`, `chore(repo): ...`, `docs: ...`, `test(api): ...`.

## Guardrails (important)
- Don’t push (no `git push`) or make network calls unless explicitly requested.
- Prefer editor-based file edits; avoid invasive refactors across projects without context.
- Before finishing: build + test locally; keep solution/projects compiling.
