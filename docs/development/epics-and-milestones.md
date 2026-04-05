# Epics and Milestones

This document describes how we structure larger chunks of work into **epics** and **milestones** and how that relates to the roadmap and implementation workflow.

## Epics

- An **epic** captures a coherent architectural or product theme (for example, Engine Correctness, Svelte UI, or Formula-First Core Purification).
- Each epic has a dedicated folder under `work/epics/` (active) or `work/epics/completed/` (historical).
- Active numbered epics follow `work/epics/E-{NN}-<slug>/` and keep their working artifacts together in that folder.
- An epic folder typically contains:
  - `spec.md` for the epic-level scope and sequencing.
  - milestone specs and milestone logs for the work inside that epic.
  - supporting reference or review docs when needed.

## Milestones

- Epics are divided into one or more **milestones**.
- Milestone specs live beside the owning epic spec under `work/epics/<epic>/`.
- Milestone logs live beside the milestone spec as `<milestone-id>-log.md` in that same epic folder.
- `work/milestones/` remains in the repo only as a compatibility stub for older references; do not create active milestone artifacts there.
- Each milestone:
  - Has a clear ID (for example, `m-ec-p3b` or `m-E16-03-runtime-analytical-descriptor`).
  - States its status, dependencies, functional requirements, phases, and test plan.
  - References its owning epic explicitly.

## Relationship to the Roadmap

- `ROADMAP.md` is the higher-level product roadmap.
- `work/epics/epic-roadmap.md` is the active sequencing and gating roadmap for implementation work.
- Epic specs under `work/epics/` carry the detailed scope, milestone list, and local references for that epic.

## Workflow Summary

1. **Roadmap**: Add or refine high-level items in `ROADMAP.md`.
2. **Epic**: For a roadmap item that needs deeper design, create an epic folder under `work/epics/` and describe its scope and rationale.
3. **Milestones**: Break the epic into concrete, scoped milestone specs in that same epic folder, then track progress with epic-local milestone logs.

## Integration Branches (Recommended)

When an epic needs a shared integration base, use a dedicated epic integration branch:

- Create `epic/E-{NN}-<epic-slug>` for the active epic.
- Create milestone branches as `milestone/<milestone-id>` from the appropriate base branch.
- Use `feature/<surface>-<milestone-id>/<short-desc>` when a milestone needs parallel feature branches.
- Merge the epic integration branch back to `main` only when the epic is complete.

For cross-epic "catch-up" trains, use an explicit integration slug (for example, `epic/ft-05-integration`) and treat it as the shared base until the work can be split into per-epic branches.

## Documentation Sync on Completion

- When an **epic** or **milestone** completes, documentation under `docs/` must be brought back in sync with reality.
- This typically requires a short sweep over relevant docs to:
  - Identify pages that reference the old behavior, naming, or status.
  - Update or amend those docs to match the completed implementation.
  - Add new documentation where a significant new capability or concept was introduced.
- The documentation sweep happens **before** the release ceremony (see `docs/development/release-ceremony.md`) so that the release reflects the current state of the system and its plans.

This keeps architecture, implementation plans, and the roadmap aligned while still allowing milestones to be small and independently shippable.
