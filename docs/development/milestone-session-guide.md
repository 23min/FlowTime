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

Current milestone: /workspaces/flowtime-vnext/work/milestones/[CURRENT_MILESTONE].md
Current milestone status: /workspaces/flowtime-vnext/work/milestones/tracking/[CURRENT_MILESTONE]-tracking.md

When starting a milestone, use the branching guidelines.
When wrapping a milestone, do not merge to main; stay on the branch.
The next milestone branch branches off the current branch.

Guardrails:
1) Follow /workspaces/flowtime-vnext/.github/copilot-instructions.md
2) Always TDD (RED -> GREEN -> REFACTOR). List tests first in tracking.
3) Copy TEMPLATE-tracking.md to the milestone tracking file before coding.
4) Run dotnet build + dotnet test --nologo before handoff. If the full suite times out, run per-project tests instead and record results (this satisfies the test requirement).
5) Use rg/fd for searches; avoid destructive git.
6) Keep docs/schemas/templates aligned when touching contracts.
7) Keep telemetry artifacts aligned with schema/registry expectations.
8) Tests deterministic; no external network calls.

Initial TODO:
1) Create a new branch per docs/development/branching-strategy.md
2) Create work/milestones/tracking/[milestone]-tracking.md from template
3) Populate phases and tasks (tests-first order) and mark status as Planned

Kick off implementation when ready, starting with the tracking doc creation.
```

## Notes

- Keep the milestone ID dynamic in session prompts; do not hardcode a specific ID.
- If the milestone is already in progress, use the tracking doc to resume.
- When the epic completes, archive the milestone specs together.
- Prefer running tests per project to avoid long-running full-suite timeouts. Use `--blame-hang` with a short timeout to catch hangs quickly; this can replace a single full-suite `dotnet test --nologo` run.
- Recommended per-project test order (each with `--blame-hang --blame-hang-timeout 60s`):
  - `tests/FlowTime.Expressions.Tests/FlowTime.Expressions.Tests.csproj`
  - `tests/FlowTime.Core.Tests/FlowTime.Core.Tests.csproj`
  - `tests/FlowTime.Tests/FlowTime.Tests.csproj`
  - `tests/FlowTime.Adapters.Synthetic.Tests/FlowTime.Adapters.Synthetic.Tests.csproj`
  - `tests/FlowTime.Sim.Tests/FlowTime.Sim.Tests.csproj`
  - `tests/FlowTime.UI.Tests/FlowTime.UI.Tests.csproj`
  - `tests/FlowTime.Api.Tests/FlowTime.Api.Tests.csproj`
  - `tests/FlowTime.Integration.Tests/FlowTime.Integration.Tests.csproj`
  - `tests/FlowTime.Cli.Tests/FlowTime.Cli.Tests.csproj`

### Wrap Checklist (Docs to Update)

When wrapping a **milestone** or **epic**, verify these docs stay in sync:

- Milestone status + tracking doc (`work/milestones/` and `work/milestones/tracking/`).
- Release note for the milestone (`docs/releases/`).
- `work/epics/epic-roadmap.md` (status + ordering).
- `ROADMAP.md` (high-level view).
- `docs/reference/engine-capabilities.md` (if shipped behavior changed).
- Charters: `docs/flowtime-charter.md` and `docs/flowtime-engine-charter.md`.
- Any affected concept/architecture/guide docs listed in `docs/modeling.md`.

If an epic wraps, also move milestone specs to `work/milestones/completed/` as a batch.
