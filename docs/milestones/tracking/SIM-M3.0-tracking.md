# SIM-M3.0 Tracking — Time-Travel Foundations

**Milestone Doc:** `docs/milestones/SIM-M3.0-time-travel-foundations.md`  
**Work Branch:** `feature/time-travel-m3`  
**Status:** 🔄 Active (tracking live progress for SIM-M3.0)

---

## Working Agreements
- Maintain RED → GREEN → REFACTOR cadence for every change.
- Update this tracking log after each notable commit, test cycle, or decision.
- Keep parity between milestone requirements and implementation tasks; escalate questions early.

---

## Progress Log
| Step | Description | Evidence / Notes | Status |
|------|-------------|------------------|--------|
| 0 | Tracking document created and baseline plan captured | This file added on `feature/time-travel-m3` | ✅ |
| 1 | TDD plan confirmed (tests enumerated and ordered) | See TDD Plan section | 🔄 |
| 2 | Schema/DTO extensions implemented under test | Pending | ☐ |
| 3 | Generation + manifest updates implemented under test | Pending | ☐ |
| 4 | Validation enhancements implemented under test | Pending | ☐ |
| 5 | Documentation + regression updates complete | Pending | ☐ |

Legend: ✅ complete · 🔄 in progress · ☐ not started

---

## TDD Plan (Authoritative)

### Phase 1 — Schema & DTO Extensions
1. **Unit Test:** `TemplateParser_WithWindowAndTopology_LoadsModelVersion11`  
   - RED: Fixture template containing window/topology fails to parse.  
   - GREEN: Parser populates DTOs; inferred `modelVersion` remains unset until generator stage and assigns default `classes: ["*"]` when omitted.  
   - REFACTOR: Extract DTOs (`WindowDto`, `TopologyNodeDto`, `TopologyEdgeDto`, `SemanticMapDto`) if needed.
2. **Unit Test:** `ParameterSubstitution_NestedObjects_AppliesToWindowStart`  
   - RED: `${startTimestamp}` remains literal inside `window.start`.  
   - GREEN: Substitution resolves nested values.  
   - REFACTOR: Harden recursion safeguards.
3. **Unit Test:** `TemplateParser_InvalidTopologyMissingQueueSemantic_ReturnsErrorCode`  
   - RED: Parser/validator allows incomplete semantics.  
   - GREEN: Returns `Semantics.QueueSeriesMissing`.

### Phase 2 — Generation & Artifact Updates
4. **Unit Test:** `NodeBasedTemplateService_GeneratesModelVersion11WhenTopologyPresent`  
   - RED: Generated YAML lacks `modelVersion` or retains `1.0`.  
   - GREEN: Output contains `modelVersion: 1.1`, preserves order.  
   - REFACTOR: Centralize model version inference helper.
5. **Snapshot Test:** `GeneratedModel_WithTopology_MatchesGoldenSnapshot`  
   - RED: Snapshot absent or mismatched.  
   - GREEN: Snapshot stored under `fixtures/templates/time-travel/`; ensures deterministic YAML.  
6. **Unit Test:** `ProvenanceService_WritesWindowMetadata`  
   - RED: Provenance JSON missing `window.start` or `modelVersion`.  
   - GREEN: Keys present and match generated data.

### Phase 3 — Validation Enhancements
7. **Unit Test:** `WindowValidator_RejectsNonUtcTimezone` → expects code `SIM_WIN_004` and constant `Window.UnsupportedTimezone`.  
8. **Unit Test:** `TopologyValidator_RejectsUnknownEdgeTarget` → expects code `SIM_TOP_004` (constant `Topology.UnknownNode`).  
9. **Unit Test:** `TopologyValidator_DetectsCycleWithoutDelay` → expects code `SIM_TOP_007` (constant `Topology.UndelayedCycle`).  
10. **Integration Test:** `TemplatesGenerateCli_InvalidEdge_ShowsDeterministicErrorCode`  
    - RED: CLI returns generic failure.  
    - GREEN: CLI exposes specific error code (e.g., `SIM_TOP_004`) and constant name in message.  
11. **Integration Test:** `TemplatesGenerateCli_WithTopology_ProducesModelVersion11Artifacts`  
    - GREEN when CLI run produces YAML/JSON with topology, manifest metadata, provenance updates, and no validation errors.

### Regression Guardrails
- Maintain existing `modelVersion: 1.0` snapshots for legacy templates.  
- Hash comparison test ensuring unchanged artifacts when topology absent.  
- Store new golden artifacts in `fixtures/templates/time-travel/` for consistent lookup.

---

## Task Breakdown (Sync with Milestone Phases)

### Phase 1 — Schema & DTO Extensions
- [ ] Implement DTOs and parser adjustments.
- [ ] Wire parameter substitution for nested structures.
- [ ] Update template parsing tests per TDD plan (tests 1–3).

### Phase 2 — Generation & Artifact Updates
- [ ] Extend service layer and writers to emit new metadata.  
- [ ] Capture snapshots and provenance updates (tests 4–6).  
- [ ] Update documentation references (guide + milestone).

### Phase 3 — Validation Enhancements
- [ ] Implement window/topology/semantics validators (tests 7–11).  
- [ ] Hook validators into CLI/service workflows.  
- [ ] Ensure error catalog alignment with milestone document.

---

## Open Questions / TODO For Review
- **Resolved:** `classes` defaults to `["*"]`; parser/generator enforce this invariant.
- **Resolved:** Golden fixtures live in `fixtures/templates/time-travel/`.
- **Resolved:** Error codes follow hybrid format — numeric code (`SIM_<CAT>_<NNN>`) plus descriptive constant (e.g., `Window.UnsupportedTimezone`). Implement helper catalog (`ValidationErrorCodes`) to keep mapping centralized.

---

## Next Action
- Finalize outstanding answers to open questions, then begin by writing RED tests for Phase 1.
