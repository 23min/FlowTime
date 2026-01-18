# Milestones Directory Layout

The `docs/milestones/` folder now mirrors the active roadmap status:

- **Active / Upcoming work** remains at the top level, alongside the shared `tracking/` documents.
- **Completed milestones** have been archived under `docs/milestones/completed/`. Every reference in the repo now points to the files in that subfolder (e.g., `docs/milestones/completed/UI-M-03.12.md`).

## Adding a new milestone

1. Author the spec at `docs/milestones/<MILESTONE-ID>.md` (only for work that is planned or in progress).
   - The spec filename may include a descriptive suffix (for example, `FT-M-05.08-servicewithbuffer-inspector.md`).
   - References elsewhere should use the milestone ID without the suffix (for example, `FT-M-05.08`).
2. When the milestone ships, move the spec to `docs/milestones/completed/` and update references (including the tracking log and release notes).
3. Keep the tracking document under `docs/milestones/tracking/` no matter what stage the milestone is in.

## Quick Links

- **Active Milestone:** See top-level files under `docs/milestones/`.
- **Archive:** See `docs/milestones/completed/` for historic milestones across backend, UI, time-travel, and service epics.
