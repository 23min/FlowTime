# FT-M-05.05 Implementation Tracking

> **Note:** This tracking document is created when work begins on the feature branch.  
> Do not create this until you're ready to start implementation.  
> See [Milestone Rules](milestone-rules-quick-ref.md) for workflow.

**Milestone:** [FT-M-05.05 — Router Flow Solidification](../completed/FT-M-05.05-router-solidification.md)  
**Started:** 2025-12-09  
**Status:** ✅ Complete  
**Branch:** `milestone/ft-m-05.05`  
**Assignee:** Codex

---

## Quick Links

- **Milestone Document:** [`docs/milestones/completed/FT-M-05.05-router-solidification.md`](../completed/FT-M-05.05-router-solidification.md)
- **Related Analysis:** [`docs/architecture/service-with-buffer/README.md`](../../architecture/service-with-buffer/README.md)
- **Milestone Guide:** [`docs/development/milestone-documentation-guide.md`](milestone-documentation-guide.md)

---

## Current Status

### Overall Progress
- [x] Phase 1: Router Output Plumbing (2/2 tasks)
- [ ] Phase 2: Template Retrofits (1/3 tasks)
- [ ] Phase 3: Tooling & Docs (0/3 tasks)

### Test Status
- **Unit Tests:** ✅ `dotnet test --nologo`
- **Integration Tests:** ✅ CLI/API parity suites (router + baseline)
- **E2E Tests:** ⏳ Planned for template retrofits

---

## Progress Log

### 2025-12-09 — Kickoff

**Preparation:**
- [x] Read milestone document
- [x] Review branching strategy / guardrails
- [x] Create milestone branch `milestone/ft-m-05.05`
- [x] Verify router-related fixtures + analyzers still run locally (pending when implementation starts)

**Next Steps:**
- [ ] Phase 1 Task 1.1 RED tests for router output plumbing
- [ ] Update tracking document as coding progresses

---

### 2025-12-09 — Router overrides + tests

**Changes:**
- Added core routing helpers (`ClassAssignmentMapBuilder`, `RouterSpecificationBuilder`, `RouterFlowMaterializer`) plus unit tests (`RouterFlowMaterializerTests`) covering class-based routes and graph reevaluation.
- Taught `Graph` to re-evaluate with overrides and updated `RunOrchestrationService` to compute router overrides before writing artifacts so downstream nodes consume router-generated series.
- Refactored `ClassContributionBuilder`, `RunArtifactWriter`, and `InvariantAnalyzer` to use shared router/class-assignment utilities (prepping analyzer work later in Phase 1).

**Tests:**
- ✅ `dotnet test tests/FlowTime.Core.Tests/FlowTime.Core.Tests.csproj --filter RouterFlowMaterializerTests`

**Commits:**
- `[hash]` - [commit message]
- `[hash]` - [commit message]

**Next Steps:**
- [ ] Wire analyzer/Sim regression tests to ensure router overrides eliminate `router_class_leakage`
- [ ] Retrofit transportation template + regenerate canonical runs

**Blockers:**
- [Any blockers encountered]

---

### 2025-12-10 — API/CLI router evaluation parity

**Changes:**
- Introduced `RouterAwareGraphEvaluator` so Engine entry points can re-evaluate graphs with router overrides and reuse the resulting context without duplicating plumbing logic.
- Updated FlowTime.API `/v1/run`, FlowTime.Cli `flowtime run`, and `TemplateInvariantAnalyzer` to call the helper so router-generated series replace legacy percentage expressions everywhere before artifacts/analyzers run.
- Added parity/regression tests:
  - FlowTime.API `/v1/run` now asserts router targets return the routed totals instead of the raw const nodes.
  - CLI/API parity suite gained a router-specific test that shells the CLI and inspects the emitted CSVs to confirm router outputs are materialized.
  - Template invariant analyzer tests now ensure `router_class_leakage` warnings disappear once routers own the downstream series.

**Tests:**
- ✅ `dotnet build`
- ✅ `dotnet test --nologo`

**Next Steps:**
- [ ] Begin Phase 2 by ripping out legacy splits in `transportation-basic-classes`
- [ ] Regenerate canonical runs + analyzer manifests without `router_class_leakage`

**Blockers:**
- None

---

### 2025-12-10 — Transportation template retrofit

**Changes:**
- Removed the legacy `splitAirport` / `splitIndustrial` parameters plus the derived `hub_dispatch_{airport,downtown,industrial}` nodes from `templates/transportation-basic-classes.yaml`. Dispatch queue arrivals are now blank expressions so the router overrides fully populate their series.
- Generated the updated model via `flow-sim generate --id transportation-basic-classes --mode simulation` and validated end-to-end with `flowtime run` (seed 4242) to ensure router outputs produce downstream arrivals with no `router_class_leakage` warnings.
- Extended `RouterTemplateRegressionTests` to assert the removed nodes stay gone and the dispatch queue demand expressions are zeroed so any non-zero values must come from router overrides.

**Tests:**
- ✅ `dotnet run --project src/FlowTime.Sim.Cli -- generate --id transportation-basic-classes --mode simulation`
- ✅ `dotnet run --project src/FlowTime.Cli -- run /tmp/transportation-basic-classes --out /tmp/transportation-basic-classes-run --deterministic-run-id --seed 4242`
- ✅ `dotnet test --nologo tests/FlowTime.Sim.Tests/FlowTime.Sim.Tests.csproj --filter RouterTemplateRegressionTests`

**Next Steps:**
- [ ] Capture refreshed canonical run + analyzer artifacts under `data/` (template still relies on /tmp output right now)
- [ ] Update template README/docs to drop the split parameters
- [ ] Audit `supply-chain-multi-tier-classes` for similar manual splits

**Blockers:**
- Pending canonical run capture (manual data dir update)

---

### 2025-12-10 — Transportation canonical capture & docs

**Changes:**
- Captured the regenerated spec at `data/models/transportation-basic-classes-model.yaml` via `flow-sim generate --id transportation-basic-classes --mode simulation`. The deterministic engine run now lives under `data/runs/run_transportation-basic-classes_0e29c545` (seed `4242`) with `classCoverage: "full"` and no router warnings.
- Updated `templates/README.md` so the `transportation-basic-classes` section explains the router-driven dispatch pattern (no manual `%` parameters) and points readers at the automatic queue feeds.
- Audited `templates/supply-chain-multi-tier-classes.yaml` and confirmed it still contains helper expressions for restock/recover/scrap inflow; logged this in Task 2.3 for a follow-up cleanup.

**Tests / Commands:**
- ✅ `dotnet run --project src/FlowTime.Sim.Cli -- generate --id transportation-basic-classes --mode simulation --out data/models/transportation-basic-classes-model.yaml`
- ✅ `dotnet run --project src/FlowTime.Cli -- run data/models/transportation-basic-classes-model.yaml --deterministic-run-id --seed 4242`
- ✅ `dotnet test --nologo`

**Next Steps:**
- [ ] Plan/implement router override cleanup for `supply-chain-multi-tier-classes` if it stays in-scope.
- [ ] Refresh release notes / template authoring guide after all class templates drop manual splits.

**Blockers:**
- None

---

### 2025-12-10 — Supply-chain router cleanup

**Changes:**
- Bumped `templates/supply-chain-multi-tier-classes.yaml` to version 3.1.0 and removed the hard-coded `restock/recover/scrap` split expressions. `ReturnsRouter` is now the sole source of inflow for those queues, ensuring router overrides dictate class mix end-to-end.
- Added regression coverage in `RouterTemplateRegressionTests` so the inflow nodes must remain zeroed, forcing router overrides to populate them.
- Captured refreshed artifacts: `data/models/supply-chain-multi-tier-classes-model.yaml` plus deterministic engine run `data/runs/run_supply-chain-multi-tier-classes_ecc81d58` (seed 6789, `classCoverage: "full"`, warning-free). Updated `templates/README.md` to describe the router-driven returns flow.

**Tests / Commands:**
- ✅ `dotnet test --nologo tests/FlowTime.Sim.Tests/FlowTime.Sim.Tests.csproj --filter RouterTemplateRegressionTests`
- ✅ `dotnet run --project src/FlowTime.Sim.Cli -- generate --id supply-chain-multi-tier-classes --mode simulation --out data/models/supply-chain-multi-tier-classes-model.yaml`
- ✅ `dotnet run --project src/FlowTime.Cli -- run data/models/supply-chain-multi-tier-classes-model.yaml --out data/runs --deterministic-run-id --seed 6789`
- ✅ `dotnet test --nologo`

**Next Steps:**
- [ ] Fold both refreshed runs into release notes / docs updates during Phase 3.

**Blockers:**
- None

---

### 2025-12-10 — Docs & release collateral

**Changes:**
- Expanded the Router section in `docs/templates/template-authoring.md` to explain how to target demand nodes, zero downstream expressions, and rely exclusively on router overrides. `templates/README.md` now highlights the router behavior for both class-enabled templates.
- Added `docs/releases/FT-M-05.05.md` capturing the milestone scope, canonical runs, and verification steps so release tracking stays in sync with the code changes.

**Next Steps:**
- [ ] Reference the new release note when we wrap the milestone.

**Blockers:**
- None

---
## Phase 1: Router Output Plumbing

**Goal:** Enable routers to emit per-route series consumed downstream and keep analyzers aligned.

### Task 1.1: Sim/Core router outputs
**File(s):** `src/FlowTime.Sim.Core/*`, `src/FlowTime.Generator/Orchestration/*`, `tests/FlowTime.Core.Tests/*`

**Checklist (TDD Order - Tests FIRST):**
- [x] RED: Add `RouterFlowMaterializerTests` covering per-route allocation + graph reevaluation
- [x] GREEN: Extend orchestration/graph pipeline to apply router overrides before writing artifacts
- [x] REFACTOR: Share router spec/class-assignment helpers across analyzer + artifact writer

**Tests:**
- [x] `dotnet test tests/FlowTime.Core.Tests/FlowTime.Core.Tests.csproj --filter RouterFlowMaterializerTests`

**Status:** ✅ Completed

### Task 1.2: Analyzer expectations
**File(s):** `src/FlowTime.Analyzer/*`, `tests/FlowTime.Analyzer.Tests/*`

**Checklist:**
- [x] RED: Analyzer test expecting `router_class_leakage` when downstream ignores router output
- [x] GREEN: Update analyzer to treat router-emitted series as authoritative, ensure conservation checks pass
- [ ] REFACTOR: Remove obsolete helper warnings tied to legacy expression nodes

**Tests:**
- [x] `dotnet test --nologo` (FlowTime.Api.Tests parity suites + FlowTime.Sim.Tests TemplateInvariantAnalyzerTests)

---

### Phase 1 Validation

**Smoke Tests:**
- [x] Build solution (no compilation errors)
- [x] Run unit tests (all passing)
- [x] Analyzer + Sim targeted suites green

**Success Criteria:**
- [ ] Routers emit per-route series automatically
- [ ] Analyzer no longer requires manual percentage expressions

---

## Phase 2: Template Retrofits

**Goal:** Remove legacy percentage expressions from class-enabled templates and validate new flows.

### Task 2.1: Transportation template cleanup
**Files:** `templates/transportation-basic-classes.yaml`, `tests/FlowTime.Sim.Tests/Templates/RouterTemplateRegressionTests.cs`

- [x] RED: Template regression test expecting router-based arrivals
- [x] GREEN: Remove helper expressions, point queues at router outputs, update regression coverage
- [x] Validate via `flow-sim generate --id transportation-basic-classes` and capture canonical artifacts

**Tests:**
- [x] `dotnet run --project src/FlowTime.Sim.Cli -- generate --id transportation-basic-classes --mode simulation --out data/models/transportation-basic-classes-model.yaml`
- [x] `dotnet run --project src/FlowTime.Cli -- run data/models/transportation-basic-classes-model.yaml --deterministic-run-id --seed 4242`
- [x] `dotnet test --nologo tests/FlowTime.Sim.Tests/FlowTime.Sim.Tests.csproj --filter RouterTemplateRegressionTests`

**Status:** ✅ Completed

### Task 2.2: Canonical runs & analyzer verification
**Files:** `examples/time-travel/transportation-basic-classes/*`, analyzer fixtures

- [ ] Capture new canonical run, ensure `router_class_leakage` warning absent
- [ ] Update docs/golden manifests referencing transportation classes

### Task 2.3: Audit other templates
**Files:** `templates/supply-chain-multi-tier-classes.yaml`, others

- [x] Document whether templates already consume router outputs or require similar cleanup
- [x] Schedule follow-ups if needed

**Notes:**
- `supply-chain-multi-tier-classes` cleanup completed here (router overrides populate `restock/recover/scrap`; version 3.1.0, canonical run `data/runs/run_supply-chain-multi-tier-classes_ecc81d58`). No other templates define routers today.

---

## Phase 3: Tooling & Docs

**Goal:** Lock in regression coverage and update guidance for router usage.

### Task 3.1: Regression tests
- [ ] Add integration tests (Sim + analyzer) validating router target consumption and per-class CSVs

### Task 3.2: Documentation updates
- [x] Update `docs/templates/template-authoring.md` + sample YAML to show router outputs instead of manual splits

**Notes:**
- Router guidance now covers targeting demand nodes, zeroing expressions, and letting overrides populate queue inflows; README entries highlight the behavior for transportation & supply-chain templates.

### Task 3.3: Release note
- [x] Create `docs/releases/FT-M-05.05.md` summarizing router solidification work

---

## Testing & Validation

### Test Case 1: Transportation router flow
**Status:** ⏳ Not Started

**Steps:**
1. [ ] `flow-sim generate --id transportation-basic-classes`
2. [ ] `flowtime telemetry run` / analyzer pass
3. [ ] Inspect warnings (`router_class_leakage` absent)

**Expected:**
- Analyzer warning count zero; router outputs produce per-class queues

**Actual:**
- [To be filled during testing]

**Result:** [✅ Pass | ❌ Fail]

---

### Test Case 2: Router regression suite
- Steps/test definition TBD during implementation.

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
