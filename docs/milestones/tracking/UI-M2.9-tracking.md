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
- [ ] Phase 1: Critical Schema Fixes (0/3 tasks)
- [ ] Phase 2: UI Page Display Updates (0/3 tasks)
- [ ] Phase 3: Testing & Validation (0/4 test cases)

### Test Status
- **Unit Tests:** 0 passing / 0 total (TBD)
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
- [ ] Begin Phase 1: Critical Schema Fixes
- [ ] Start with Task 1.1: Update SimGridInfo Model

---

## Phase 1: Critical Schema Fixes

**Goal:** Restore basic UI functionality by fixing deserialization failures

### Task 1.1: Update SimGridInfo Model

**File:** `ui/FlowTime.UI/Services/FlowTimeSimApiClient.cs`

**Changes Required:**
- Replace `BinMinutes` property with `BinSize` and `BinUnit`
- Add computed `BinMinutes` property with `[JsonIgnore]` for internal display use
- Implement unit conversion logic (seconds/minutes/hours/days)

**Checklist:**
- [ ] Update SimGridInfo class definition
- [ ] Add computed BinMinutes property
- [ ] Write unit test: `Test_SimGridInfo_Deserializes_NewSchema`
- [ ] Write unit test: `Test_SimGridInfo_BinMinutes_Computation`
- [ ] Verify artifact browsing loads without exceptions

**Status:** ⏳ Not Started

---

### Task 1.2: Update GridInfo Model

**File:** `ui/FlowTime.UI/Services/FlowTimeApiModels.cs`

**Changes Required:**
- Update GridInfo record to include `BinSize` and `BinUnit` properties
- Remove `BinMinutes` from primary constructor
- Add computed `BinMinutes` property with `[JsonIgnore]`

**Checklist:**
- [ ] Update GridInfo record definition
- [ ] Add computed BinMinutes property
- [ ] Write unit test: `Test_GridInfo_Deserializes_NewSchema`
- [ ] Write unit test: `Test_GridInfo_BinMinutes_AllUnits`
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
- [ ] Update GraphRunResult record definition
- [ ] Add computed BinMinutes property
- [ ] Update `ApiRunClient.cs` to pass binSize/binUnit
- [ ] Write unit test: `Test_GraphRunResult_PreservesSemanticInfo`
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
