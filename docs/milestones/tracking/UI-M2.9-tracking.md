# UI-M2.9 Implementation Tracking

**Milestone:** UI-M2.9 — Schema Migration for UI  
**Status:** 🔄 In Progress  
**Branch:** `feature/ui-m2.9/schema-migration`

---

## Quick Links

- **Milestone Document:** [`docs/milestones/UI-M2.9.md`](../UI-M2.9.md)
- **Related Analysis:** [`docs/ui/UI-CURRENT-STATE-ANALYSIS.md`](../../ui/UI-CURRENT-STATE-ANALYSIS.md)
- **Milestone Guide:** [`docs/development/milestone-documentation-guide.md`](../../development/milestone-documentation-guide.md)

---

## Current Status

### Overall Progress
- [x] Phase 1: Critical Schema Fixes (1/3 tasks) ✅ Task 1.1 complete
- [ ] Phase 2: UI Page Display Updates (0/3 tasks)
- [ ] Phase 3: Testing & Validation (0/4 test cases)

### Test Status
- **Unit Tests:** 8 passing / 8 total (SimGridInfo schema tests)
- **Integration Tests:** 0 passing / 0 total (TBD)
- **E2E Tests:** 0 passing / 4 planned

---

## Progress Log

### Session: Milestone Setup

**Preparation:**
- [x] Read milestone document
- [x] Read UI state analysis
- [x] Create feature branch
- [x] Create tracking document

**Next Steps:**
- [x] Begin Phase 1: Critical Schema Fixes
- [x] Complete Task 1.1: Update SimGridInfo Model

---

### Session: Phase 1, Task 1.1 - SimGridInfo Schema Migration

**Changes:**
- Created `SimGridInfoSchemaTests.cs` with 8 comprehensive tests
- Updated `SimGridInfo` class with binSize/binUnit properties
- Added computed BinMinutes with fail-fast validation
- Removed fallback behavior - invalid units throw exception

**Tests (8 passing):**
- ✅ Deserialization of new schema
- ✅ BinMinutes computation (5 theory cases: minutes/hours/days/weeks/case-insensitive)
- ✅ Exception on invalid units
- ✅ JsonIgnore prevents binMinutes in JSON output

**Commits:**
- `cc97360` - feat(ui): update SimGridInfo to new schema (binSize/binUnit)

**Design Decision:**
- Changed from lenient fallback to fail-fast validation
- Matches Engine's TimeUnit.Parse() behavior
- Invalid units indicate Engine bug, not expected scenario

**Next Steps:**
- [ ] Task 1.2: Update GridInfo Model

---

## Phase 1: Critical Schema Fixes

**Goal:** Restore basic UI functionality by fixing deserialization failures

### Task 1.1: Update SimGridInfo Model

**File:** `ui/FlowTime.UI/Services/FlowTimeSimApiClient.cs`

**Changes Required:**
- Replace `BinMinutes` property with `BinSize` and `BinUnit`
- Add computed `BinMinutes` property with `[JsonIgnore]` for internal display use
- Implement unit conversion logic (minutes/hours/days/weeks)

**Checklist:**
- [x] Write unit tests (RED - 8 failing tests) - commit cc97360
- [x] Update SimGridInfo class definition (GREEN - make tests pass) - commit cc97360
- [x] Add computed BinMinutes property with fail-fast validation - commit cc97360
- [x] All 8 tests passing - commit cc97360
- [ ] Verify artifact browsing loads without exceptions (deferred to Phase 3)

**Status:** ✅ Complete

---

### Task 1.2: Update GridInfo Model

**File:** `ui/FlowTime.UI/Services/FlowTimeApiModels.cs`

**Changes Required:**
- Update GridInfo record to include `BinSize` and `BinUnit` properties
- Remove `BinMinutes` from primary constructor
- Add computed `BinMinutes` property with `[JsonIgnore]`

**Checklist:**
- [ ] Write unit test: `Test_GridInfo_Deserializes_NewSchema` (RED - failing)
- [ ] Write unit test: `Test_GridInfo_BinMinutes_AllUnits` (RED - failing)
- [ ] Update GridInfo record definition (GREEN - make tests pass)
- [ ] Add computed BinMinutes property (GREEN - make tests pass)
- [ ] Verify Engine `/v1/run` responses deserialize correctly

**Status:** ⏳ Not Started

---

### Task 1.3: Update GraphRunResult

**File:** `ui/FlowTime.UI/Services/RunClientContracts.cs`

**Changes Required:**
- Update GraphRunResult record to include `BinSize` and `BinUnit`
- Remove `BinMinutes` from primary constructor
- Add computed `BinMinutes` property for UI display

**Checklist:**
- [ ] Write unit test: `Test_GraphRunResult_PreservesSemanticInfo` (RED - failing)
- [ ] Update GraphRunResult record definition (GREEN - make tests pass)
- [ ] Add computed BinMinutes property (GREEN - make tests pass)
- [ ] Update `ApiRunClient.cs` to pass binSize/binUnit (GREEN - make tests pass)
- [ ] Verify model execution completes without exceptions

**Status:** ⏳ Not Started

---

## Phase 2: UI Page Display Updates

**Goal:** Update UI pages to display semantic grid information

### Task 2.1: Update TemplateRunner Display

**File:** `ui/FlowTime.UI/Pages/TemplateRunner.razor`

**Changes Required:**
- Display grid as `{BinSize} {BinUnit}` instead of `{BinMinutes} minutes`
- Update result display logic

**Checklist:**
- [ ] Update grid display markup
- [ ] Test template execution shows semantic units
- [ ] Verify "1 hour" displays correctly (not "60 minutes")

**Status:** ⏳ Not Started

---

### Task 2.2: Update Simulate Display

**File:** `ui/FlowTime.UI/Pages/Simulate.razor`

**Changes Required:**
- Update execution results to display semantic units
- Ensure grid info displays correctly

**Checklist:**
- [ ] Update result display markup
- [ ] Test direct execution workflow
- [ ] Verify semantic time units display

**Status:** ⏳ Not Started

---

### Task 2.3: Update Artifacts Display

**File:** `ui/FlowTime.UI/Pages/Artifacts.razor`

**Changes Required:**
- Display artifact grid info with new schema format
- Show `{BinSize} {BinUnit}` in artifact list

**Checklist:**
- [ ] Update artifact metadata display
- [ ] Test artifact browsing workflow
- [ ] Verify semantic units in artifact list

**Status:** ⏳ Not Started

---

## Phase 3: Testing & Validation

**Goal:** Ensure all workflows work end-to-end

### Test Case 1: Template Workflow
- [ ] Select template with `binSize: 1, binUnit: hours`
- [ ] Generate model
- [ ] Execute in Engine
- [ ] Verify UI displays "1 hour" NOT "60 minutes"

**Status:** ⏳ Not Started

---

### Test Case 2: Direct Execution
- [ ] Paste NEW schema YAML
- [ ] Execute
- [ ] Verify UI parses and displays correctly

**Status:** ⏳ Not Started

---

### Test Case 3: Artifact Browsing
- [ ] View run created by Engine v0.6.1+
- [ ] Fetch `series/index.json`
- [ ] Verify grid info displays correctly
- [ ] Fetch series CSV

**Status:** ⏳ Not Started

---

### Test Case 4: Error Handling
- [ ] Attempt OLD schema YAML (if user has legacy examples)
- [ ] Engine should reject with clear error message
- [ ] Verify UI displays error appropriately

**Status:** ⏳ Not Started

---

## Completion Checklist

### Phase 1 (Critical Fixes) ✅
- [ ] SimGridInfo updated with binSize/binUnit
- [ ] GridInfo updated with binSize/binUnit
- [ ] GraphRunResult preserves semantic information
- [ ] ApiRunClient passes through schema fields
- [ ] Unit tests passing for schema parsing

### Phase 2 (Display Updates) ✅
- [ ] TemplateRunner displays semantic units
- [ ] Simulate page displays grid correctly
- [ ] Artifacts page displays grid correctly
- [ ] All UI pages use new schema

### Phase 3 (Validation) ✅
- [ ] Template workflow tested end-to-end
- [ ] Direct execution tested
- [ ] Artifact browsing tested
- [ ] Error handling verified

### Final Milestone Completion ✅
- [ ] All tests passing (unit + integration)
- [ ] UI can execute models via Engine
- [ ] UI can browse artifacts with new schema
- [ ] No deserialization exceptions
- [ ] Documentation updated
- [ ] Ready for merge to milestone branch
