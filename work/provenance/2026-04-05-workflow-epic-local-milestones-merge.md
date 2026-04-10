# Session: Workflow Epic-Local Milestones Merge

**Date:** 2026-04-05  
**Branch:** `chore/workflow-epic-local-milestones`  
**ADO:** none  
**PR:** none  
**Source:** session metadata unavailable in mounted workspace storage

## Summary

This session completed the archival migration of legacy milestone specs and tracking logs from the central `work/milestones/` folders into epic-local homes under `work/epics/`. It also repaired moved-document references, added retrospective epic descriptors for previously ungrouped legacy work, and verified the Git ancestry needed to land the workflow-only refactor onto `main` without pulling unrelated E-16 milestone commits.

## Work Done

- Finished relocating the remaining legacy milestone specs and paired logs into epic-local folders under `work/epics/completed/`.
- Added retrospective epic descriptors for `core-foundations`, `pmf-modeling`, `artifacts-schema-provenance`, `service-api-foundation`, and `synthetic-ingest`.
- Created `work/epics/browser-execution/` to preserve the legacy `M-16.00` browser/WASM design thread as future work rather than leaving it in the central milestone archive.
- Updated roadmap, milestone README, release notes, and development docs to point at the new epic-local paths.
- Repaired moved-file links inside archived milestone documents and supporting docs.
- Verified that `chore/workflow-epic-local-milestones` is based on the active E-16 milestone branch rather than `main`, which requires replaying only the workflow commits onto `main`.

## Decisions & Rationale

- **Preserve history by epic-local ownership:** Historical milestone specs and logs were moved beside their owning epic threads so archived workflow artifacts follow the same structure as active work.
- **Create retrospective completed epics where needed:** Legacy milestone families without a prior explicit epic were grouped only where the ownership arc was defensible, instead of leaving them indefinitely in central staging folders.
- **Treat browser execution as future work, not completed archive residue:** `M-16.00` was moved into a dedicated future epic folder because it represents a preserved design direction, not shipped work.
- **Cherry-pick workflow commits onto `main` instead of merging the worktree branch directly:** The migration branch sits on top of E-16 milestone commits, so a direct merge would incorrectly carry milestone work into `main`.

## Problems & Solutions

- **Problem:** The central `work/milestones/completed/` and `work/milestones/tracking/` directories still held pre-epic legacy artifacts that no longer matched the repo's epic-local workflow.
  **Solution:** Grouped the remaining legacy artifacts into retrospective epic folders, moved each spec together with its paired log, and updated all known references.
- **Problem:** The separate migration worktree became confusing once the user wanted the final state on `main`.
  **Solution:** Inspected both worktrees and the branch graph, then chose a replay-on-`main` path that keeps the workflow refactor isolated from unrelated product work.
- **Problem:** The expected session metadata JSONL was not available through the mounted workspace storage path.
  **Solution:** Recorded the required provenance narrative sections without the optional metadata table.

## Key Files

**Created:**
- `provenance/2026-04-05-workflow-epic-local-milestones-merge.md`
- `work/epics/browser-execution/epic.md`
- `work/epics/completed/core-foundations/spec.md`
- `work/epics/completed/pmf-modeling/spec.md`
- `work/epics/completed/artifacts-schema-provenance/spec.md`
- `work/epics/completed/service-api-foundation/spec.md`
- `work/epics/completed/synthetic-ingest/spec.md`

**Modified:**
- `work/epics/epic-roadmap.md`
- `work/milestones/README.md`
- `docs/architecture/run-provenance.md`
- `docs/archive/ROADMAP-2025-06-legacy.md`
- `docs/development/milestone-documentation-guide.md`
- `docs/development/milestone-rules-quick-ref.md`
- `docs/releases/M-01.06.md`
- `docs/releases/M-02.00-v0.4.0.md`
- `docs/releases/UI-M-02.00.md`

## Follow-up

- Commit the remaining staged workflow migration batch in the migration worktree.
- Replay only the workflow migration commits onto `main`.
- Verify that `main` receives only archive/workflow changes and no E-16 milestone commits.
- Remove the extra migration worktree once the replay onto `main` is complete.