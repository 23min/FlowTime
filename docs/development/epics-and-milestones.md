# Epics and Milestones

This document describes how we structure larger chunks of work into **epics** and **milestones** and how that relates to architecture docs and the overall roadmap.

## Epics

- An **epic** captures a coherent architectural or product theme (e.g., class-aware routing, services with buffers).
- Each epic has a dedicated folder under `docs/architecture/`, for example:
  - `docs/architecture/classes/`
  - `docs/architecture/service-with-buffer/`
- An epic folder typically contains:
  - A short `README.md` describing the epic at a high level.
  - One or more detailed architecture / design documents.
  - Links to the relevant milestones under `docs/milestones/`.

## Milestones

- Epics are divided into one or more **milestones**.
- Milestones are specified as individual markdown files under `docs/milestones/`, following the existing guardrails in:
  - `docs/development/milestones-guardrails.md`
  - `docs/development/milestones-overview.md` (if present)
- Each milestone:
  - Has a clear ID (e.g., `CL-M-04.03.02`, `SB-M-01`).
  - States its status, dependencies, functional requirements, phases, and test plan.
  - May reference one or more epics via an "Epic" or "Related" field.

## Relationship to the Roadmap

- The high-level product and architecture **roadmap** lives in `docs/ROADMAP.md`.
- `docs/ROADMAP.md` should:
  - Call out major epics (by name and folder) and sequence them over time.
  - Link to the **Epic Roadmap** document under `docs/architecture/` for more detail.
- The **Epic Roadmap** in `docs/architecture` (see `docs/architecture/epic-roadmap.md`) lists:
  - The epic folders under `docs/architecture/`.
  - The intended ordering (what we do first/next/later).
  - A short rationale for each epic.

## Workflow Summary

1. **Roadmap**: Add or refine high-level items in `docs/ROADMAP.md`.
2. **Epic**: For a roadmap item that needs deeper design, create an epic folder under `docs/architecture/` and describe its scope and rationale.
3. **Milestones**: Break the epic into concrete, scoped milestones under `docs/milestones/`, following the milestone guardrails.

## Documentation Sync on Completion

- When an **epic** or **milestone** completes, documentation under `docs/` must be brought back in sync with reality.
- This typically requires a short sweep over relevant docs to:
  - Identify pages that reference the old behavior, naming, or status.
  - Update or amend those docs to match the completed implementation.
  - Add new documentation where a significant new capability or concept was introduced.
- The documentation sweep happens **before** the release ceremony (see `docs/development/release-ceremony.md`) so that the release reflects the current state of the system and its plans.

This keeps architecture, implementation plans, and the roadmap aligned while still allowing milestones to be small and independently shippable.
