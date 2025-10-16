# Milestone Prompt Template

Use this template when asking Codex to create or update a milestone. It captures the required structure, authoritative sources, and desired output location so responses stay consistent.

---



## Milestone creation instructions


I need a milestone spec for `<Milestone ID> — <Title>`.
Use docs/development/milestone-documentation-guide.md and docs/development/milestone-rules-quick-ref.md for structure and rules.
Scope, acceptance criteria, and test coverage should come from:
- docs/architecture/time-travel/time-travel-planning-roadmap.md
- docs/architecture/time-travel/time-travel-planning-decisions.md
If additional architectural detail is needed, reference docs/architecture documents, starting with:
- docs/architecture/time-travel/time-travel-architecture-ch3-components.md
- docs/architecture/time-travel/time-travel-architecture-ch5-implementation-roadmap.md
Create the milestone document at docs/milestones/`<Milestone ID>`.md following the standard outline.

### Notes
- Replace `<Milestone ID>` and `<Title>` with the specific milestone (e.g., `M-03.01 — Time-Travel APIs`).
- Add any milestone-specific context (e.g., dependencies, known constraints) after the template block if needed.
- When executing a milestone (updating status or tracking progress), point Codex to the existing milestone doc and corresponding tracking file instead of creating a new spec.
- If unsure about something or you find conflicting information, immediately ask me so that we can do a Q&A and clarify or update the documentation.

## Implementaation Instructions

- Use instructions from .github/copilot-instructions.md.
- Use docs/ to look up documentation, specification, schemas.
- Never commit until I ask for it.
- Code is planned with TDD strategy, RED-GREEN-REFACTOR. 
- For code style, some is documented in .editorconfig.
- Remember to run tests as you progress (e.g., dotnet test tests/FlowTime.Sim.Tests/FlowTime.Sim.Tests.csproj, followed by dotnet test FlowTime.sln when appropriate). 
- End each session with git status and summarize changes/tests per standard workflow.
- Code should be KISS first. 
- Use bash -lc with a workdir on every shell call; rely on rg for searches.
- Prefer targeted diffs/patches; avoid unnecessary whole-file reads.
- Run dotnet test FlowTime.sln (or narrower suites) after meaningful code changes; you may skip rebuilds with --no-build.

