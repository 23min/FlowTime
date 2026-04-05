# Milestones Directory Layout

`work/milestones/` is now a legacy holding area, not the source of truth for active workflow artifacts.

Current workflow:

- New and active milestone specs live inside the owning epic folder under `work/epics/<epic>/`.
- Milestone progress logs live beside the spec as `*-log.md` in that same epic folder.

What remains here:

- `work/milestones/completed/` and `work/milestones/tracking/` are legacy staging directories and are currently empty.

Why these still live here:

- They provide a temporary holding area if a future archival cleanup encounters genuinely unmapped legacy artifacts.
- They are no longer the source of truth for shipped or planned milestone specs in this repository.

Move rule for leftovers:

- Prefer creating or identifying an explicit epic-local home first.
- Move the paired log at the same time and rename it to `*-log.md` beside the milestone spec.
- Use this directory only as a temporary staging area when ownership is genuinely unresolved and still needs a human decision.

## Adding a new milestone

1. Create the milestone spec inside the owning epic folder, not in `work/milestones/`.
2. Create the milestone log beside it using the `*-log.md` naming pattern.
3. Leave this directory unchanged unless you are relocating or documenting unresolved legacy milestones.

## Quick Links

- **Current workflow:** See `work/epics/` for active and completed epic-local milestone specs and logs.
- **Legacy staging area:** `work/milestones/completed/` and `work/milestones/tracking/` should normally remain empty.
