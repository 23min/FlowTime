## Git Branching Strategy with Milestones

This repository uses a milestone-first workflow that scales from solo work to small teams, while keeping main stable.

### Rationale

- Roadmap is organized around milestones (`m0`, `m1`, ...). A milestone can touch multiple surfaces: API, UI, CLI, Core.
- Milestone branches provide a short-lived integration space to coordinate parallel work without breaking `main`.

### Branch types

- Mainline: `main` — always green and releasable
- Milestone: `milestone/m0` — integration branch for a milestone spanning surfaces
- Feature: `feature/<surface>-m0/<short-desc>` — actual work branches targeting the milestone branch
- Release (optional): `release/m0` — brief hardening before tagging
- Tags: `v0.1.0-m0` — snapshot of milestone release

### When to use each

- Single-surface, quick change: create `feature/<surface>-m0/<short-desc>` off `main`, PR directly into `main`.
- Multi-surface (e.g., API and UI at once): create `milestone/m0`, base all related features from it, PR into the milestone branch, then merge milestone → main when complete.

### Commands (examples)

- Create milestone branch:
  - git checkout main
  - git pull
  - git checkout -b milestone/m0
  - git push -u origin milestone/m0

- Start a feature targeting the milestone:
  - git checkout -b feature/api-m0/api-launch milestone/m0

- Keep milestone branch fresh:
  - git fetch origin
  - git merge origin/main   # into milestone/m0

- Complete milestone:
  - Merge all features into milestone via PRs (squash preferred)
  - Ensure tests/docs are updated
  - Tag: v0.1.0-m0 (semantic version + milestone suffix)
  - Merge milestone/m0 → main and delete the branch

### Commit messages (Conventional Commits)

Use Conventional Commits with an optional scope for the surface:

- feat(api): add /run endpoint
- fix(ui): correct parse of YAML errors
- chore(repo): enable API launch in VS Code
- docs: update branching strategy for milestone flow
- test(api): parity tests CLI vs API

Scopes you can use: `api`, `ui`, `cli`, `core`, `repo`, `docs`, `test`, `build`, `chore`.

### PR guidance

- For multi-surface work, target PRs to `milestone/m0` (not `main`).
- Prefer squash merges to keep history clean.
- Link PRs to the milestone in GitHub for traceability.

### Notes

- Tags preserve history; delete feature and milestone branches after merge.
- Keep `docs/milestones/m0/*` updated alongside code.
