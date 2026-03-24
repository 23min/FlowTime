# Skill: epic-wrap

Purpose: close out an epic, sync docs, and decide PR vs direct merge.

Use when:
- All milestones in the epic are complete.

Process:
1) Verify each milestone status is ✅ Complete.
2) Move milestone specs to docs/milestones/completed/ as a batch.
3) Update:
   - docs/architecture/epic-roadmap.md
   - docs/ROADMAP.md
   - docs/flowtime-charter.md and docs/flowtime-engine-charter.md (if impacted)
4) Ask whether the epic should merge via PR or directly to main.
5) Ensure release ceremony steps are ready or completed.

Outputs:
- Epic docs and roadmap updated.
- Milestones archived together.
