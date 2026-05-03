# Skill: milestone-wrap

**Trigger phrases:** "complete milestone", "close milestone", "wrap up", "milestone done", "finish milestone"

## Purpose

Complete a milestone without merging to main. This ensures all documentation and tracking artifacts are finalized before the milestone is considered done.

## Use When
- All acceptance criteria are met
- All tests pass
- Implementation is complete
- Ready to hand off for review or next milestone

## Process

### 1. Verify Completion

**Check acceptance criteria:**
- [ ] All functional requirements implemented
- [ ] All tests written and passing
- [ ] No known regressions
- [ ] Edge cases handled
- [ ] Error handling complete

**Run full test suite:**
```bash
# Build first
dotnet build

# Then test
dotnet test --nologo
```

### 2. Update Milestone Status

In the milestone spec (`docs/milestones/<milestone-id>.md`):
- Change status to ✅ Complete
- Add completion date
- Note any deferred scope

### 3. Finalize Tracking Document

In tracking doc (`docs/milestones/tracking/<milestone-id>-tracking.md`):
- Mark all phases complete
- Record final test results
- Summarize what was delivered
- Document any decisions or tradeoffs
- List any follow-up work or gaps

### 4. Update Related Documentation

**Documentation sweep checklist:**
- [ ] Roadmap updated with milestone status
- [ ] Epic status reflects milestone completion
- [ ] Release notes drafted for changes
- [ ] Architecture docs updated if design changed
- [ ] Reference docs updated if capabilities changed
- [ ] Charters updated if product scope changed
- [ ] API docs updated if contracts changed
- [ ] Schema docs updated if formats changed

**Common docs to check:**
- `docs/ROADMAP.md` - high-level status
- `docs/architecture/epic-roadmap.md` - epic progress
- Epic README in `docs/architecture/<epic-slug>/`
- `docs/releases/` - add release note
- Reference docs in `docs/reference/`
- Concept docs in `docs/concepts/`

### 5. Stay on Branch

**Do NOT merge to main yet.**

The milestone branch stays open for:
- Review and approval
- Integration testing
- Next milestone branching
- Epic integration (if using epic branches)

Next milestone can branch from this one if sequential.

### 6. Prepare Handoff

Create a summary for handoff:
```markdown
## Milestone [ID] Complete

**What shipped:**
- [Key deliverable 1]
- [Key deliverable 2]

**Tests:**
- All passing ✅
- Coverage: [summary]

**Documentation:**
- [Updated doc 1]
- [Updated doc 2]

**Next steps:**
- Review and merge
- Or: start next milestone [ID]
```

## Outputs

- ✅ Milestone status updated
- ✅ Tracking doc finalized
- ✅ Related docs updated
- ✅ Tests passing
- ✅ Branch ready for review
- ✅ Handoff summary prepared

## Notes

**Milestone completion ≠ epic completion**
- Milestones stay in `docs/milestones/` until epic wraps
- Archive to `docs/milestones/completed/` only when epic closes

**If last milestone in epic:**
- Proceed to epic-wrap next
- That's when you merge to main
