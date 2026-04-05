# Milestones Directory Layout

`work/milestones/` is now a legacy holding area, not the source of truth for active workflow artifacts.

Current workflow:

- New and active milestone specs live inside the owning epic folder under `work/epics/<epic>/`.
- Milestone progress logs live beside the spec as `*-log.md` in that same epic folder.

What remains here:

- `work/milestones/completed/` contains only unresolved legacy milestone specs that do not yet have a defensible epic-local home.
- `work/milestones/tracking/` contains the matching unresolved legacy logs.
- Typical remaining families are the pre-epic milestones such as `M-00/M-01/M-02`, `UI-M-00/UI-M-01/UI-M-02`, `SVC-M`, `SYN-M`, and `M-16.00`.

## Adding a new milestone

1. Create the milestone spec inside the owning epic folder, not in `work/milestones/`.
2. Create the milestone log beside it using the `*-log.md` naming pattern.
3. Leave this directory unchanged unless you are relocating or documenting unresolved legacy milestones.

## Quick Links

- **Current workflow:** See `work/epics/` for active and completed epic-local milestone specs and logs.
- **Legacy archive:** See `work/milestones/completed/` and `work/milestones/tracking/` for the residual unmapped pre-epic archive.
