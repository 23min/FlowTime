# UI-M-03.13 — Analyze Section Decision

**Status:** ✅ Completed  
**Dependencies:** ✅ UI-M-03.10 (UI Baseline & Build Health), ✅ UI-M-03.11 (Artifacts Restoration), ✅ UI-M-03.12 (Simulate → Gold Run Integration)  
**Decision:** Option A — Hide Analyze for now (nav entry removed; revisit in Phase 2 alongside new visuals).  
**Target:** Decide the fate of the legacy “Analyze” section so the navigation reflects the M3 gold workflows. Either hide the route (temporarily) or repurpose it as a lightweight gold-data health check that proves we can read `state_window.json` data.

---

## Overview

The existing `Analyze` page still markets FlowTime Engine capabilities that pre-date the gold-based roadmap. We need to tighten the nav: either remove the entry until analytical views return (Phase 2), or convert it into a simple, accurate “Gold Data Health Check” that reads the same bundles the rest of the M3 UI consumes. The decision must be intentional and documented, with the nav/menu and landing page updated accordingly.

We previously removed the Analyze nav item in code (Expert layout + home hero). This milestone captures that reality as the official product decision so downstream docs and planning artifacts stop implying that Analyze is still visible today.

### Strategic Context
- Motivation: Avoid confusing users with stale Engine references while we focus on gold-first workflows.
- Impact: Clean nav, accurate messaging; optionally provide a minimal diagnostic if we keep the page.
- Dependencies: Gold bundles now exist (UI-M-03.12); nav group already created (UI-M-03.14 was pre-satisfied).

---

## Scope

### In Scope ✅
1. Audit the current `Analyze` page content and nav references.
2. Choose and implement one of:
   - **Option A (Hide):** Remove `Analyze` from nav/routes until Phase 2 visuals land.
   - **Option B (Repurpose):** Rename to “Gold Diagnostics” (or similar) and display a brief summary of a selected run using the gold data adapter (read-only textual health check).
3. Update the home page card(s) and any cross-links so they no longer point to stale Engine messaging.
4. Document the decision in the milestone + roadmap.  
   **Outcome:** Option A (hide) confirmed; nav already omits Analyze, docs reference “Hide Analyze for now,” and the home hero now highlights Time-Travel.

### Out of Scope ❌
- Building dashboards or charts (belongs to Phase 2).
- Implementing the Time-Travel data service (handled by UI-M-03.15).
- Updating CLI docs.

### Future Work
- UI-M-03.15 introduces a reusable REST Time-Travel data service—if Option B was chosen, migrate the diagnostics to use that surface.
- Phase 2 milestones will replace the placeholder with real time-travel visualizations.

---

## Requirements

### Functional Requirements

#### FR1: Content Audit & Decision
- Description: Review existing `Analyze` page and nav entries; choose Option A (hide) or Option B (repurpose).
- Acceptance:
  - [x] Decision recorded in the milestone doc (with rationale).
  - [x] Roadmap updated to reflect the interim state (Session 4 marked complete).

#### FR2: Nav / Routing Update
- Description: Align navigation with the decision.
- Acceptance:
  - **Option A:** remove/hide `Analyze` link from Expert layout, home hero card, docs, and any other entry points.
  - **Option B:** rename nav entry + route, set a new page title/description, and ensure links go to the updated diagnostic page.

#### FR3: Minimal Diagnostic (Option B only)
- Description: If repurposed, render a concise gold bundle health check.
- Acceptance:
  - [ ] Accept a runId (query param or picker) and read basic metadata (`run.json`, optional warnings) using existing utilities. *(N/A — Option A chosen)*
  - [ ] Display status (OK / warnings / missing files) as text; no charts/tables required at this stage. *(N/A — Option A chosen)*

### Non-Functional Requirements
- **NFR1:** Messaging must align with current capabilities—no references to deterministic FlowTime Engine analytics unless they are still available.
- **NFR2:** Implementation should be easily reversible once Phase 2 visualizations arrive.

---

## Implementation Plan

1. **Audit & Decide**
   - Review `Analyze.razor`, home page cards, and nav entries.
   - Choose Option A or B; record rationale. ✔ (Option A)
2. **Update Nav + Content**
   - Apply the decision: hide or rename entry, adjust home card copy.
   - Ensure breadcrumb/title reflect the new state. ✔ (home panel updated to Time‑Travel)
3. **Diagnostics (if Option B)**
   - Add basic run selection (manual runId input is acceptable).
   - Display run metadata + warning count using current services (no new adapters yet). ❌ (not applicable — Option A)
4. **Docs & Tests**
   - Update roadmap/milestone docs.
   - Smoke-test navigation and home page links. ✔

---

## Test Plan

- **Manual:**
  - Confirm the nav shows/hides the Analyze entry as expected.
  - Verify home page buttons route correctly.
  - (Option B) Provide a runId and confirm metadata/warnings render.
- **Automated:** Not required for this decision; rely on existing UI tests.

---

## Success Criteria

- Navigation aligns with the chosen direction (no stale Engine marketing).
- Home page hero/CTA reflects the decision and no longer directs users to outdated flows.
- (Option B) Diagnostic page provides a truthful gold bundle summary without graphics.
- Roadmap + milestone docs updated with the outcome.

---

## References

- `docs/architecture/time-travel/ui-m3-roadmap.md`
- `src/FlowTime.UI/Pages/Analyze.razor`
- `src/FlowTime.UI/Layout/ExpertLayout.razor`
- `src/FlowTime.UI/Pages/Home.razor`
- `docs/development/milestone-documentation-guide.md`
