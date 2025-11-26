# CL-M-04.03.02 Implementation Tracking

**Milestone:** CL-M-04.03.02 — Scheduled Dispatch & Flow Control Primitives  
**Started:** 2025-11-27  
**Status:** 🔄 In Progress  
**Branch:** `feature/router-m4.3.2`  
**Assignee:** Codex (GPT-5.1)

---

## Quick Links

- **Milestone Document:** `docs/milestones/CL-M-04.03.02.md`
- **Expression Roadmap:** `docs/architecture/expression-extensions-roadmap.md`
- **Milestone Guide:** `docs/development/milestone-documentation-guide.md`

---

## Current Status

### Overall Progress
- [x] Phase 1: Expression Primitives (3/3 tasks)
- [ ] Phase 2: Scheduled Dispatch Engine + Analyzer (0/3 tasks)
- [ ] Phase 3: Templates, UI, Cache Refresh (0/4 tasks)

### Test Status
- **Unit Tests:** `dotnet test --filter ExpressionIntegrationTests --nologo` ✅
- **Integration Tests:** Pending
- **E2E / Golden Runs:** Planned (transportation & warehouse examples)

---

## Progress Log

### 2025-11-27 - Kickoff

**Preparation:**
- [x] Read milestone document
- [x] Reviewed expression roadmap + prior router milestone context
- [x] Created feature branch `feature/router-m4.3.2`
- [x] Created tracking document

**Next Steps:**
- [ ] Phase 2 RED: scheduled dispatch backlog tests (`ScheduledDispatchTests`)
- [ ] Update schema + analyzer per FR2

---

### 2025-11-27 - Phase 1 Expression Primitives

**Changes:**
- Added RED tests for MOD/FLOOR/CEIL/ROUND/STEP/PULSE in `ExpressionIntegrationTests`, then implemented the corresponding evaluators in `ExprNode` and `ClassContributionBuilder` (shared helpers inside `ClassSeries`).
- Updated docs to advertise the new helpers (`docs/reference/engine-capabilities.md`, `docs/templates/template-authoring.md`).

**Tests:**
- ✅ `dotnet test --filter ExpressionIntegrationTests --nologo` *(UI build emits existing nullable warnings — unchanged)*

**Commits:**
- _pending (feature branch work in progress)_

**Next Steps:**
- [ ] Start Phase 2 RED work (scheduled backlog tests + schema updates)
- [ ] Capture analyzer requirements for dispatch schedules

**Blockers:**
- None

---

### 2025-11-27 - Phase 2 RED: Scheduled Dispatch Tests

**Changes:**
- Added `ScheduledDispatchTests` under `tests/FlowTime.Core.Tests/Aggregation/` to codify the expected cadence behavior (release only on scheduled bins, respect capacity overrides).
- Introduced a placeholder `DispatchScheduleProcessor.ApplySchedule` helper (currently passthrough) to host the forthcoming engine logic.

**Tests:**
- ❌ `dotnet test --filter ScheduledDispatchTests --nologo` *(fails as expected until schedule gating is implemented; full solution build emits existing UI nullable warnings)*

**Next Steps:**
- [ ] Implement `DispatchScheduleProcessor` gating logic and integrate with backlog evaluation.
- [ ] Extend `ClassSeries.Backlog` / `BacklogNode` + schema parsing to consume the new schedule model.

**Blockers:**
- None yet.

---

# Phase 1: Expression Primitives

**Goal:** Extend the expression engine/parser/test suite so templates can use MOD/FLOOR/CEIL/ROUND/STEP/PULSE when modeling cadence-driven behavior.

### Task 1.1: Parser & AST coverage for new functions
**Files:** `src/FlowTime.Expressions/*`, `tests/FlowTime.Expressions.Tests/*`

**Checklist (TDD Order):**
- [x] RED: Add unit tests covering MOD/FLOOR/CEIL/ROUND parsing & evaluation (`ExpressionIntegrationTests`).
- [x] GREEN: Implement parser + evaluator support for MOD/FLOOR/CEIL/ROUND.
- [x] REFACTOR: Ensure dependency extraction + metadata reflect new ops (shared helpers).

### Task 1.2: STEP / PULSE primitives
**Files:** same as above

**Checklist:**
- [x] RED: Tests for STEP (threshold) and PULSE (period/phase/amplitude) evaluation.
- [x] GREEN: Implement functions and ensure time-grid alignment (bin-safe) across both expression engines.
- [x] REFACTOR: Document helper behavior for inputs + amplitude (docs update).

### Task 1.3: Schema & docs
**Files:** `docs/schemas/model.schema.yaml`, `docs/templates/template-authoring.md`, `docs/reference/engine-capabilities.md`

**Checklist:**
- [x] Update docs/reference to mention new functions.
- [x] Add authoring guidance + examples (template authoring guide).
- [x] `dotnet test --filter ExpressionIntegrationTests --nologo`.

**Phase 1 Validation:**
- [x] `dotnet test --filter ExpressionIntegrationTests --nologo`
- [x] Expression doc updates reviewed.

---

# Phase 2: Scheduled Dispatch Engine & Analyzer

**Goal:** Introduce `dispatchSchedule` semantics for backlog/router nodes, enforce via analyzers, and expose metadata in API responses.

### Task 2.1: Schema + backlog execution
**Files:** `docs/schemas/model.schema.yaml`, `src/FlowTime.Core/Models/ModelParser.cs`, `src/FlowTime.Core/Nodes/BacklogNode.cs`, `src/FlowTime.Core/Analysis/InvariantAnalyzer.cs`, new tests in `tests/FlowTime.Core.Tests/ScheduledDispatchTests.cs`

**Checklist:**
- [x] RED: Add core tests verifying backlog releases only on scheduled bins.
- [ ] GREEN: Implement `dispatchSchedule` parsing + backlog behavior.
- [ ] Update `ClassContributionBuilder` / `InvariantAnalyzer` as needed.

### Task 2.2: Analyzer & CLI surfacing
**Files:** `src/FlowTime.Sim.Core/Analysis/TemplateInvariantAnalyzer.cs`, `tests/FlowTime.Sim.Tests/*`, `src/FlowTime.Cli/Program.cs`

**Checklist:**
- [ ] RED: Analyzer tests for invalid schedule configs and “never dispatches” warnings.
- [ ] GREEN: Implement warnings + CLI verbose output for schedules.
- [ ] Ensure diagnostics bubble into run warnings / API logs.

### Task 2.3: API metadata
**Files:** `src/FlowTime.API/Services/GraphService.cs`, `StateQueryService.cs`, DTOs/contracts.

**Checklist:**
- [ ] Expose `dispatchSchedule` in `/graph` + `/state_window`.
- [ ] UI-friendly metadata (period, phase, capacity override).
- [ ] Add unit/API tests if applicable.

**Phase 2 Validation:**
- [ ] `dotnet test --filter ScheduledDispatchTests --nologo`
- [ ] `dotnet test --filter TemplateInvariantAnalyzerTests --nologo`
- [ ] CLI smoke showcasing verbose schedule output.

---

# Phase 3: Templates, UI, Cache Refresh

**Goal:** Apply scheduled dispatch to real templates, surface cues in the UI, and add a template cache refresh capability.

### Task 3.1: Template updates & new example
**Files:** `templates/transportation-basic-classes.yaml`, new warehouse/picker template, docs under `docs/templates/*`

**Checklist:**
- [ ] RED: Update router/template regression tests to expect bursty dispatch.
- [ ] GREEN: Implement schedule config + new example template.
- [ ] Regenerate canonical runs (`flow-sim generate` + engine CLI) capturing analyzer output.

### Task 3.2: UI indicators
**Files:** `src/FlowTime.UI/*` (Topology, RunCard, chips)

**Checklist:**
- [ ] Show schedule metadata (chip/icon) when nodes declare `dispatchSchedule`.
- [ ] Ensure inspector displays period/phase/capacity details.
- [ ] UI tests covering scheduled node badge.

### Task 3.3: Template cache refresh command
**Files:** CLI and/or API service, docs.

**Checklist:**
- [ ] Implement cache invalidation command/button.
- [ ] Document usage and update telemetry/run workflows.

### Task 3.4: Release prep
- [ ] Run `dotnet build`, `dotnet test --nologo`.
- [ ] Update milestone tracker with analyzer runs + run IDs.
- [ ] Draft `docs/releases/CL-M-04.03.02.md`.

**Phase 3 Validation:**
- [ ] Transportation + warehouse deterministic runs regenerated with schedules.
- [ ] UI manual smoke verifying scheduled badges.

---

## Testing & Validation

| Test | Status | Notes |
|------|--------|-------|
| `dotnet test --filter ExpressionIntegrationTests --nologo` | ✅ | Confirms MOD/FLOOR/CEIL/ROUND/STEP/PULSE implementation |
| `dotnet test --filter ScheduledDispatchTests --nologo` | ⏳ | Covers backlog cadence |
| `dotnet test --filter TemplateInvariantAnalyzerTests --nologo` | ⏳ | Analyzer warnings |
| `dotnet test --nologo` | ⏳ | Full suite (perf skips expected) |
| `flow-sim generate --id transportation-basic-classes` | ⏳ | Analyzer/CLI verification |

---

## Issues Encountered

- _None yet_

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
