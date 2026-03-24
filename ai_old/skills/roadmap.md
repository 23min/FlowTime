# Skill: roadmap

Purpose: maintain the lifecycle of epics between the high-level roadmap and epic architecture docs.

Use when:
- Proposing new epics.
- Promoting an epic from idea to planned/active.
- Closing an epic.

Policy:
- docs/ROADMAP.md is the authoritative list of proposed and active epics.
- docs/architecture/epic-roadmap.md is the authoritative list of epics with docs.

Status flow:
- Proposed: only in docs/ROADMAP.md.
- Planned: epic folder exists with README.md.
- Active: milestones underway.
- Complete: epic wrapped and milestones archived.

Process:
1) Proposed epic
   - Add to docs/ROADMAP.md with intent and rough ordering.
   - No epic folder required.
2) Promote to Planned/Active
   - Run epic-refine.
   - Create docs/architecture/<epic-slug>/README.md.
   - Add to docs/architecture/epic-roadmap.md.
   - Update docs/ROADMAP.md status.
3) Complete
   - Run epic-wrap.
   - Update docs/ROADMAP.md and docs/architecture/epic-roadmap.md.

Outputs:
- Roadmap and epic roadmap in sync.
- Clear epic status at each stage.
