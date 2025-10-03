# SIM M2.9 Integration Tasks - Breakdown & Analysis

**Date:** October 3, 2025  
**Status:** 📋 Ready to Execute  
**Estimated Time:** ~1.5 hours (documentation only)

---

## Task 5: Verify Sim Output (VALIDATION FIRST) ✅

**Status:** ✅ COMPLETE - No code changes needed

**Analysis:**
- ✅ ProvenanceMetadata model uses C# property names (PascalCase)
- ✅ JSON serialization uses `PropertyNamingPolicy = JsonNamingPolicy.CamelCase`
- ✅ Located in Program.cs line 297 (API response)
- ✅ Located in Program.cs line 294 (file persistence)
- ✅ All 11 provenance unit tests passing
- ✅ All 10 API integration tests passing
- ✅ All 10 CLI integration tests passing

**Conclusion:** Our implementation already outputs camelCase JSON correctly. No code changes required.

---

## Task 1: Verify Field Naming Consistency ✅

**Status:** ✅ COMPLETE  
**Actual:** 10 min  
**File:** `docs/guides/engine-m2.9-provenance-spec.md`

**Action Items:**
- [x] Scan document for any snake_case references
- [x] Verify all JSON examples use camelCase
- [x] Verify all field references use camelCase
- [x] Add explicit note about System.Text.Json camelCase serialization

**Completed:**
Added serialization note after "Field Naming" section explaining System.Text.Json with JsonNamingPolicy.CamelCase.

---

## Task 2: Update Header Format Documentation ✅

**Status:** ✅ COMPLETE  
**Actual:** 25 min  
**File:** `docs/guides/engine-m2.9-provenance-spec.md`

**Action Items:**
- [x] Clarify "Option A" that header contains **full JSON object** (not URL-encoded, not Base64)
- [x] Document that Engine deserializes the JSON string from header value
- [x] Add example showing exact header format: `X-Model-Provenance: {"source":"flowtime-sim",...}`
- [x] Emphasize **both** approaches supported (header + embedded)
- [x] Remove "Recommended" label from Option A (both are equal)

**Completed:**
Added "Header Format Details" section with explicit guidance on JSON string format and Engine deserialization.

---

## Task 3: Document Actual Engine Response Format ✅

**Status:** ✅ COMPLETE  
**Actual:** 20 min  
**File:** `docs/guides/engine-m2.9-provenance-spec.md`

**Action Items:**
- [x] Update "UI Calls Engine" section with complete response
- [x] Add fields: `runId`, `grid`, `order`, `series`, `artifactsPath`, `modelHash`
- [x] Note that response format is Engine-owned, document what Sim expects
- [x] Update integration test examples with realistic responses

**Completed:**
Updated sequence diagram and "Example: Complete Provenance Flow" section with actual Engine response fields.

---

## Task 4: Mark Query Endpoints as M2.10 ✅

**Status:** ✅ COMPLETE  
**Actual:** 20 min  
**File:** `docs/guides/engine-m2.9-provenance-spec.md`

**Action Items:**
- [x] Add clear milestone marker: "## M2.9 Scope" and "## M2.10 Future"
- [x] Move "Support Provenance Queries" section to M2.10
- [x] Move "Query by templateId/modelId" examples to M2.10
- [x] Update "What Engine Needs to Implement" section (remove queries)
- [x] Update "Validation Checklist" (separate M2.9 vs M2.10 items)
- [x] Keep focus on M2.9: accept, store, retrieve single artifact

**Completed:**
Separated milestone scope throughout document. Updated executive summary, validation checklist, test scenarios, and summary with clear M2.9/M2.10 distinctions.

---

## Execution Plan

### Step 1: Read Current Spec
- Understand existing content
- Identify all sections that need updates

### Step 2: Execute Task 1 (Field Naming)
- Scan for snake_case
- Add explicit camelCase note
- Quick win

### Step 3: Execute Task 4 (Milestone Markers)
- Restructure document with M2.9 / M2.10 sections
- Move query content to future section
- Sets clear scope

### Step 4: Execute Task 2 (Header Format)
- Update Option A description
- Add clarifying examples
- Remove "Recommended" bias

### Step 5: Execute Task 3 (Response Format)
- Update example responses
- Add Engine response fields
- Note Engine owns this format

### Step 6: Validation
- Read through entire updated spec
- Ensure internal consistency
- Check all examples align

### Step 7: Run Tests
- `dotnet test` to confirm no regressions
- All 128 tests should still pass

### Step 8: Commit
- Commit message: `docs(sim): align M2.9 spec with Engine implementation`
- Reference: SIM-M2.7 Phase 4 follow-up

---

## Post-Task Actions

After completing Sim documentation tasks:
1. **Notify Engine team** that Sim side is ready
2. **Wait for Engine team** to complete their Phase 1 tasks
3. **Integration testing** after Engine updates
4. **Update this task document** with integration test results

---

## Success Criteria ✅ ALL COMPLETE

✅ **Task 5:** Sim output confirmed camelCase (validation only) - COMPLETE  
✅ **Task 1:** All field names consistently camelCase - COMPLETE  
✅ **Task 2:** Header format clearly documented (full JSON string) - COMPLETE  
✅ **Task 3:** Engine response format accurately documented - COMPLETE  
✅ **Task 4:** M2.9 vs M2.10 scope clearly separated - COMPLETE  
✅ **All tests passing:** 128 tests (11 unit + 10 API + 10 CLI + 97 existing)  
✅ **No code changes:** Documentation updates only  
✅ **Ready for Engine team:** Spec updated and validated  

---

## Notes

- **Zero code changes required** - our implementation is already correct
- Documentation updates ensure alignment with Engine team
- Engine team doing parallel work on their side
- Integration testing planned after Engine Phase 1 complete
- M2.10 query features deferred to future milestone
