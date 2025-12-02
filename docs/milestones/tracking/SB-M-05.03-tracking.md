# SB-M-05.03 Implementation Tracking

> **Note:** This tracking document is created when work begins on the feature branch.  
> Do not create this until you're ready to start implementation.  
> See [Milestone Rules](milestone-rules-quick-ref.md) for workflow.

**Milestone:** SB-M-05.03 — Queue-Like DSL Parity & DLQ Synthesizer  
**Started:** 2025-11-28  
**Status:** 🔄 In Progress  
**Branch:** `milestone/sb-m-05.03`  
**Assignee:** Codex

---

## Quick Links

- **Milestone Document:** [`docs/milestones/SB-M-05.03.md`](../SB-M-05.03.md)
- **Architecture Note:** [`docs/architecture/service-with-buffer/service-with-buffer-architecture.md`](../../architecture/service-with-buffer/service-with-buffer-architecture.md)
- **Milestone Guide:** [`docs/development/milestone-documentation-guide.md`](../milestone-documentation-guide.md)

---

## Current Status

### Overall Progress
- [ ] Phase 1: Schema & Validator Parity (0/2 tasks)
- [ ] Phase 2: Synthesizer & Runtime Alignment (0/2 tasks)
- [ ] Phase 3: Templates, Docs & Wrap (0/2 tasks)

### Test Status
- **Unit Tests:** 0 passing / 0 total
- **Integration Tests:** 0 passing / 0 total
- **E2E Tests:** 0 passing / X planned

---

## Progress Log

### 2025-11-28 — Kickoff

**Preparation:**
- [x] Read milestone document
- [x] Read related documentation (service-with-buffer architecture note, SB-M-05.02 tracker)
- [x] Create feature branch `milestone/sb-m-05.03`
- [x] Create tracking document

**Next Steps:**
- [x] Begin Phase 1 Task 1.1 (schema RED test)
- [x] Capture plan in milestone tracker once RED test added
- [ ] Implement schema + synthesizer updates

---

### 2025-11-28 — Phase 1 RED

**Changes:**
- Added schema-level acceptance tests for queue/dlq `queueDepth: self`.
- Added parser regression tests expecting implicit queue/DLQ nodes to synthesize backing ServiceWithBuffer nodes.

**Tests:**
- ✅ `dotnet test tests/FlowTime.Tests/FlowTime.Tests.csproj --filter "TemplateSchema_Queue_Allows_Self_QueueDepth|TemplateSchema_Dlq_Allows_Self_QueueDepth" --nologo`
- ❌ `dotnet test tests/FlowTime.Sim.Tests/FlowTime.Sim.Tests.csproj --filter "Template_With_Queue_SelfQueueDepth_Parses|Template_With_Dlq_SelfQueueDepth_Parses" --nologo` *(fails until synthesizer/validator support lands).*

**Next Steps:**
- [ ] Update schema/docs + validator to recognize implicit queue/dlq nodes.
- [ ] Extend synthesizer and runtime plumbing so the new parser tests pass.

### 2025-11-28 — Synthesizer + Validator updates

**Changes:**
- Renamed `ServiceWithBufferNodeSynthesizer` to `QueueNodeSynthesizer` and expanded it to cover `queue`/`dlq` topology nodes (auto-creating backing ServiceWithBuffer nodes when `queueDepth` is `self`/omitted).
- Updated `TemplateValidator` to validate `queueDepth` references (non-"self" values must exist) so mixed implicit/explicit setups are caught.

**Tests:**
- ✅ `dotnet test tests/FlowTime.Sim.Tests/FlowTime.Sim.Tests.csproj --filter "Template_With_Queue_SelfQueueDepth_Parses|Template_With_Dlq_SelfQueueDepth_Parses" --nologo`

**Next Steps:**
- [ ] Update schema docs/spec to mention queue/dlq implicit behavior.
- [ ] Start adapting canonical templates + analyzer surfaces once schema/doc work lands.

### 2025-11-28 — Template cleanup & router regression

**Changes:**
- Removed the last hand-authored ServiceWithBuffer helpers for DLQs in `templates/transportation-basic*.yaml`; the topology now relies solely on implicit `queueDepth` aliases.
- Updated doc stubs (`docs/templates/template-authoring.md`, `docs/architecture/service-with-buffer/...`) to describe queue/dlq parity.
- Extended the cleanup to supply-chain, incident, manufacturing, network-reliability, and it-system templates by deleting all `_depth` helper nodes (queues + DLQs now rely exclusively on the synthesizer).
- Logged the remaining router work (implicit router synthesis, helper removal) in `docs/milestones/SB-M-05.03.md` as a documented gap that spawns the follow-up milestone (`SB-M-05.03-router` placeholder).

**Tests:**
- ✅ `dotnet test tests/FlowTime.Sim.Tests/FlowTime.Sim.Tests.csproj --filter "Template_With_Queue_SelfQueueDepth_Parses|Template_With_Dlq_SelfQueueDepth_Parses|RouterTemplateRegressionTests" --nologo`
- ✅ `dotnet test tests/FlowTime.Tests/FlowTime.Tests.csproj --filter TemplateBundleValidationTests --nologo`

**Next Steps:**
- [ ] Sweep remaining canonical templates (supply-chain, etc.) for leftover helper nodes.
- [ ] Continue updating docs/release notes to reflect the implicit DSL.
- [ ] Spec + implement router implicit nodes once SB-M-05.03 core work lands (tracked follow-up milestone).

### 2025-11-28 — Supply-chain helper removal

**Changes:**
- Deleted the explicit `restock_backlog`, `recover_backlog`, and `scrap_backlog` ServiceWithBuffer nodes (base + classes templates now rely entirely on synthesized queue depth series).
- Left the `queueDepth` aliases/output CSVs intact so the synthesized series continue to flow into reporting/exports.
- Follow-up: converted the remaining distributor/Rejections/SupplierShortfall queues to `queueDepth: self` and dropped the legacy backlog CSV exports, so no `_backlog` references remain anywhere in the supply-chain templates.

**Tests:**
- ✅ `dotnet test tests/FlowTime.Tests/FlowTime.Tests.csproj --filter TemplateBundleValidationTests --nologo`

**Next Steps:**
- [ ] Re-run bundle/analyzer generation for the supply-chain templates after the SIM refresh (manual validation).

---

## Phase 1: Schema & Validator Parity

**Goal:** Allow `queue` and `dlq` topology nodes to omit helper backlog nodes (using `queueDepth: self`), document the behavior, and prevent mixed implicit/explicit queue definitions.

### Task 1.1: Schema & Template Schema docs accept queue/dlq self-depth
**Status:** ✅ Completed (tracked earlier)

### Task 1.2: Validator guardrails for implicit queue/dlq nodes
**Status:** ✅ Completed (tracked earlier)

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

## Phase 2: Synthesizer & Runtime Alignment

**Goal:** Extend the synthesizer to every queue-like semantic, align analyzer/CLI/UI surfaces to rely on logical types, and ensure exports still discover synthesized queue series.

### Task 2.1: Rename & extend synthesizer for queue/dlq nodes
**Status:** ✅ Completed (tracked earlier)

### Task 2.2: Analyzer/CLI/UI parity
**Status:** ✅ Completed (tracked earlier)

---

## Phase 3: Templates, Docs & Wrap

**Goal:** Remove remaining helper backlog nodes from canonical templates, align docs/release notes, and validate end-to-end.

### Task 3.1: Template migrations & analyzer runs
**Files:** `templates/transportation-basic*.yaml`, `templates/warehouse-picker-waves.yaml`, `templates/supply-chain-*.yaml`, `tests/FlowTime.Sim.Tests/Templates/*`, `tests/FlowTime.Tests/TemplateBundleValidationTests.cs`

- [x] Remove helper nodes + reroute outputs to synthesized IDs where safe (supply-chain base/classes, transportation base; documented remaining templates for follow-up).
- [x] `dotnet test tests/FlowTime.Tests/FlowTime.Tests.csproj --filter TemplateBundleValidationTests --nologo`

**Status:** ✅ Completed (remaining templates tracked under follow-up milestone)

### Task 3.2: Docs, release, full verification
**Files:** `docs/templates/template-authoring.md`, `templates/README.md`, `docs/architecture/service-with-buffer/service-with-buffer-architecture.md`, `docs/milestones/SB-M-05.03.md`, `docs/releases/SB-M-05.03.md`

- [x] Update docs + milestone/spec and drafted release note
- [x] `dotnet build` & `dotnet test --nologo`
- [x] Manual UI verification + release note + tracker wrap

**Status:** ✅ Completed

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
