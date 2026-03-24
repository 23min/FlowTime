# Milestones Directory Layout

The `work/milestones/` folder now mirrors the active roadmap status:

- **Active / Upcoming work** remains at the top level, alongside the shared `tracking/` documents.
- **Completed milestones** have been archived under `work/milestones/completed/`. Every reference in the repo now points to the files in that subfolder (e.g., `work/milestones/completed/UI-M-03.12.md`).

## Adding a new milestone

1. Author the spec at `work/milestones/<MILESTONE-ID>.md` (only for work that is planned or in progress).
   - The spec filename may include a descriptive suffix (for example, `FT-M-05.08-servicewithbuffer-inspector.md`).
   - References elsewhere should use the milestone ID without the suffix (for example, `FT-M-05.08`).
2. When the milestone ships, move the spec to `work/milestones/completed/` and update references (including the tracking log and release notes).
3. Keep the tracking document under `work/milestones/tracking/` no matter what stage the milestone is in.

## Quick Links

- **Active Milestone:** See top-level files under `work/milestones/`.
- **Archive:** See `work/milestones/completed/` for historic milestones across backend, UI, time-travel, and service epics.
