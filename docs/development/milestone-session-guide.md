# Milestone Session Guide

Use this guide when starting or continuing a milestone session with Codex. It complements
`docs/development/milestone-documentation-guide.md` and `.github/copilot-instructions.md`.

## Session Prompt Template (Parameterized)

Start by asking:

- **Which milestone do you wish to start or continue?**

Once the milestone ID is provided, substitute it for `[CURRENT_MILESTONE]` below:

```
You are Codex working in /workspaces/flowtime-vnext (branch main unless already branched).

In this prompt, [CURRENT_MILESTONE] refers to <MILESTONE-ID>.

Current milestone: /workspaces/flowtime-vnext/docs/milestones/[CURRENT_MILESTONE].md
Current milestone status: /workspaces/flowtime-vnext/docs/milestones/tracking/[CURRENT_MILESTONE]-tracking.md

When starting a milestone, use the branching guidelines.
When wrapping a milestone, do not merge to main; stay on the branch.
The next milestone branch branches off the current branch.

Guardrails:
1) Follow /workspaces/flowtime-vnext/.github/copilot-instructions.md
2) Always TDD (RED -> GREEN -> REFACTOR). List tests first in tracking.
3) Copy TEMPLATE-tracking.md to the milestone tracking file before coding.
4) Run dotnet build + dotnet test --nologo before handoff.
5) Use rg/fd for searches; avoid destructive git.
6) Keep docs/schemas/templates aligned when touching contracts.
7) Keep telemetry artifacts aligned with schema/registry expectations.
8) Tests deterministic; no external network calls.

Initial TODO:
1) Create a new branch per docs/development/branching-strategy.md
2) Create docs/milestones/tracking/[milestone]-tracking.md from template
3) Populate phases and tasks (tests-first order) and mark status as Planned

Kick off implementation when ready, starting with the tracking doc creation.
```

## Notes

- Keep the milestone ID dynamic in session prompts; do not hardcode a specific ID.
- If the milestone is already in progress, use the tracking doc to resume.
- When the epic completes, archive the milestone specs together.
