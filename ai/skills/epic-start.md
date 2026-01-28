# Skill: epic-start

Purpose: initialize or confirm epic context before milestone work begins.

Use when:
- Starting work on a new epic.
- Returning to an epic after time away and context is unclear.

Inputs:
- Epic name and slug
- Target branch strategy (epic integration or mainline)

Process:
1) Locate epic folder: docs/architecture/<epic-slug>/.
2) Review core docs:
   - docs/architecture/<epic-slug>/README.md
   - docs/architecture/epic-roadmap.md
   - docs/ROADMAP.md
3) Confirm or create epic integration branch per docs/development/branching-strategy.md.
4) Summarize epic scope and intended milestone list.
5) Hand off to milestone-start for a specific milestone.

Outputs:
- Epic context summary.
- Confirmed branch plan.
- Named milestone to start or continue.
