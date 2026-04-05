---
description: Patch/chore skill for one-off fixes, tweaks, or maintenance tasks not tracked as epics or milestones. Can optionally be linked to a GitHub issue or another external tracker.
name: patch
when_to_use: |
  - When a quick fix, UI tweak, or maintenance task is needed outside the epic/milestone workflow
  - When responding to a bug, issue, or request tracked in GitHub Issues or another issue tracker
responsibilities:
  - Read and understand the linked issue/task when one exists
  - Create a descriptive branch (e.g., fix/..., patch/..., chore/...)
  - Implement the change with focused commits
  - Open a PR (prefer "Rebase and merge" to connect branch tip to main)
  - Reference the issue/task in the PR and commit message when applicable
  - Clean up the branch after merge
output:
  - Linked PR and commit(s) referencing the issue/task when applicable
  - Patch, fix, or maintenance change merged to main
invoked_by:
  - planner agent (when a patch/chore/issue is requested)
  - patcher/maintainer agent (if defined)
---

# Skill: Patch/Chore

This skill enables the workflow to:
- Handle one-off fixes, UI tweaks, or maintenance tasks
- Link work to an issue or tracker item when one exists
- Keep the codebase healthy and responsive to small requests

## Workflow
1. Read and understand the linked issue/task when one exists
2. Create a descriptive branch (fix/..., patch/..., chore/...)
3. Implement the change, run tests, stage files
4. 🛑 **STOP — show staged changes and proposed commit message. Wait for human to say "commit".**
5. Commit and push (only after explicit human approval)
6. Open a PR (prefer "Rebase and merge")
7. Reference the issue/task in PR and commit message when applicable
8. Merge and clean up the branch (only after human approval)
9. **Record learnings** — if this fix revealed a pattern, pitfall, or convention worth remembering:
   - Append to `work/decisions.md` if a decision was made (use the standard format)
   - Append to `work/agent-history/<agent>.md` with a brief note on what was learned

## Example Triggers
- "Fix login button alignment (see GitHub Issue #123)"
- "Patch: update copyright year"
- "Chore: remove unused config"

## Best Practices
- Keep the scope focused and the branch short-lived
- Reference the issue/task for traceability when one exists
- Use clear, conventional branch names
