# SB-M-05.04 Implementation Tracking

> **Note:** This tracking document is created when work begins on the feature branch.  
> Do not create this until you're ready to start implementation.  
> See [Milestone Rules](milestone-rules-quick-ref.md) for workflow.

**Milestone:** SB-M-05.04 — Deterministic Run Orchestration  
**Started:** 2025-11-28  
**Status:** 📋 Planned  
**Branch:** `milestone/sb-m-05.04`  
**Assignee:** Codex

---

## Quick Links

- **Milestone Document:** [`docs/milestones/[MILESTONE-ID].md`](../milestones/[MILESTONE-ID].md)
- **Related Analysis:** [Link to any analysis documents]
- **Milestone Guide:** [`docs/development/milestone-documentation-guide.md`](milestone-documentation-guide.md)

---

## Current Status

### Overall Progress
- [ ] Phase 1: Hashing & Provenance (0/2 tasks)
- [ ] Phase 2: Orchestration & Engine Boundary (0/2 tasks)
- [ ] Phase 3: UI/CLI, Docs & Release (0/2 tasks)

### Test Status
- **Unit Tests:** 0 passing / 0 total
- **Integration Tests:** 0 passing / 0 total
- **E2E Tests:** 0 passing / X planned

---

## Progress Log

### 2025-11-28 — Kickoff

**Preparation:**
- [x] Read milestone document
- [x] Read SIM/engine boundary epic doc
- [x] Create feature branch `milestone/sb-m-05.04`
- [ ] Verify orchestration/engine services running (pending when coding begins)

**Next Steps:**
- [ ] Phase 1 Task 1.1 (hash RED tests)
- [ ] Capture progress per task

---

### [YYYY-MM-DD] - [Session Title]

**Changes:**
- [What was done in this session]
- [What was done in this session]

**Tests:**
- ✅ [Tests passing]
- ❌ [Tests failing] (if any)

**Commits:**
- `[hash]` - [commit message]
- `[hash]` - [commit message]

**Next Steps:**
- [ ] [What's next]
- [ ] [What's next]

**Blockers:**
- [Any blockers encountered]

---

## Phase 1: Hashing & Provenance

**Goal:** Compute deterministic bundle hashes and persist run metadata.

### Task 1.1: Hash builder + provenance schema
**Files:** `src/FlowTime.Sim.Core/Orchestration/*`, `docs/schemas/manifest.schema.json`, `tests/FlowTime.Tests/Orchestration/*`

- [ ] RED: unit test verifying identical inputs produce same hash (`RunHashCalculatorTests`)
- [ ] Implement hash builder + provenance metadata
- [ ] GREEN: targeted unit tests

### Task 1.2: CLI/orchestration hash plumbing
**Files:** `src/FlowTime.Sim.Cli/Program.cs`, `src/FlowTime.Sim.Service/*`, `tests/FlowTime.Sim.Tests/Orchestration/*`

- [ ] RED: orchestration integration test expecting reuse detection
- [ ] Wire hash calculation into CLI/service responses
- [ ] GREEN: targeted tests

---

### Phase 1 Validation

**Smoke Tests:**
- [ ] Build solution (no compilation errors)
- [ ] Run unit tests (all passing)
- [ ] [Other validation checks]

**Success Criteria:**
- [ ] [Criterion from milestone doc]
- [ ] [Criterion from milestone doc]

---

## Phase 2: Orchestration & Engine Boundary

**Goal:** Extend orchestration workflows (reuse/overwrite) and simplify engine API to accept only bundles.

### Task 2.1: Orchestration reuse/overwrite logic
**Files:** `src/FlowTime.Sim.Core/Orchestration/RunOrchestrationService.cs`, `tests/FlowTime.Sim.Tests/Orchestration/*`

- [ ] RED: integration test verifying reuse prompt + forced overwrite
- [ ] Implement hash lookup + filesystem checks (reuse vs regenerate)
- [ ] GREEN: targeted tests

### Task 2.2: Engine API simplification
**Files:** `src/FlowTime.API/Controllers/RunsController.cs`, `src/FlowTime.API/Services/RunSubmissionService.cs`, `tests/FlowTime.Api.Tests/Runs/*`

- [ ] RED: API tests ensuring template IDs are rejected, bundles required
- [ ] Remove template orchestration logic from engine API; update clients
- [ ] GREEN: API tests

---

## Phase 3: UI/CLI, Docs & Release

**Goal:** Expose reuse/overwrite choices in UI/CLI, update docs, and wrap the milestone.

### Task 3.1: UI/CLI prompt flow
**Files:** `src/FlowTime.UI/Pages/RunOrchestration.razor`, `src/FlowTime.UI/Services/RunService.cs`, `src/FlowTime.Sim.Cli/Program.cs`, `tests/FlowTime.UI.Tests/*`

- [ ] Implement UI prompt/radio for reuse vs regenerate
- [ ] Update CLI flags (`--reuse`, `--force-overwrite`)
- [ ] Tests: UI render tests + CLI integration

### Task 3.2: Docs & release
**Files:** `docs/templates/template-authoring.md`, `docs/operations/*`, `docs/releases/SB-M-05.04.md`, `docs/milestones/SB-M-05.04.md`

- [ ] Update docs/milestone/spec
- [ ] `dotnet build` & `dotnet test --nologo`
- [ ] Manual verification + release note + tracker wrap

---

## Testing & Validation

### Test Case 1: [Test Name]
**Status:** ⏳ Not Started

**Steps:**
1. [ ] [Step]
2. [ ] [Step]
3. [ ] [Step]

**Expected:**
- [Expected outcome]

**Actual:**
- [To be filled during testing]

**Result:** [✅ Pass | ❌ Fail]

---

### Test Case 2: [Test Name]
[Repeat structure]

---

## Issues Encountered

### Issue 1: [Short Description]
**Encountered:** [YYYY-MM-DD]  
**Severity:** [Low | Medium | High | Critical]

**Description:**
[Detailed description of the issue]

**Impact:**
[What was blocked or affected]

**Resolution:**
[How it was fixed]

**Commits:**
- `[hash]` - [fix description]

**Status:** [Open | Resolved | Deferred]

---

## Final Checklist

### Code Complete
- [ ] All phase tasks complete
- [ ] All tests passing
- [ ] No compilation errors
- [ ] No console warnings
- [ ] Code reviewed (if applicable)

### Documentation
- [ ] Milestone document updated (status → ✅ Complete)
- [ ] ROADMAP.md updated
- [ ] Release notes entry created
- [ ] Related docs updated

### Quality Gates
- [ ] All unit tests passing
- [ ] All integration tests passing
- [ ] Manual E2E tests passing
- [ ] Performance acceptable
- [ ] No regressions

### Pre-Merge
- [ ] Branch rebased on latest main
- [ ] Conflicts resolved
- [ ] Squash commits (if needed)
- [ ] Conventional commit message ready
- [ ] PR created (if team workflow)

---

## Metrics

**Development Time:** [Track actual time spent, if desired]

**Commits:** [Count]

**Tests Added:**
- Unit: [count]
- Integration: [count]
- E2E: [count]

**Lines Changed:**
- Added: [count]
- Removed: [count]
- Modified files: [count]

**Code Coverage:** [If applicable]

---

## Notes

### Key Decisions
- [Document any architectural or implementation decisions made during development]
- [Rationale for choosing one approach over another]

### Lessons Learned
- **What went well:** [Successes]
- **What could be improved:** [Areas for improvement]
- **Future considerations:** [Things to remember for next time]

### Dependencies Discovered
- [Any unexpected dependencies found during implementation]

---

## Template Instructions

**How to use this tracking document:**

1. **WHEN TO CREATE:**
   - Create this tracking document ONLY when you create the work branch to start implementation
   - Do NOT create during milestone planning phase
   - First commit on work branch should include this tracking doc

2. **Setup Phase (First Commit):**
   - Copy this template to `docs/milestones/tracking/[MILESTONE-ID]-tracking.md`
   - Fill in header (milestone ID, title, date, branch, assignee)
   - Update Quick Links section
   - Customize phase names and task lists from milestone doc
   - Create TDD plan based on test plan from milestone document
   - Commit: `docs: create tracking document for [MILESTONE-ID]`

3. **During Development:**
   - Check off tasks as completed
   - Add commit hashes after each commit
   - Record test results (✅ Pass or ❌ Fail)
   - Document issues encountered
   - Update progress log after each session

3. **Phase Completion:**
   - Run validation checklist
   - Mark phase complete
   - Update overall progress percentages

4. **Milestone Completion:**
   - Complete final checklist
   - Update all documentation links
   - Archive this tracking document

**Update Frequency:**

✅ **Do update:**
- After each commit that advances the milestone
- When completing a task
- When tests pass/fail
- When encountering blockers
- End of each development session
- When making key technical decisions

❌ **Don't update:**
- For unrelated commits
- For trivial typo fixes (unless in milestone scope)
- For routine maintenance outside milestone scope

**Tracking Tips:**
- Be honest about status (better to flag issues early)
- Keep notes brief but informative
- Link to commits for detailed context
- Update "Next Steps" to maintain momentum
- Document blockers immediately

---

**Document Version:** 1.0  
**Created From Template:** [YYYY-MM-DD]  
**Last Updated:** [YYYY-MM-DD]  
**Updated By:** [Name]
