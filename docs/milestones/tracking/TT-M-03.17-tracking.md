# TT-M-03.17 Implementation Tracking

**Milestone:** TT-M-03.17 — Telemetry Auto-Capture Orchestration  
**Status:** 🚧 In Progress  
**Branch:** `feature/time-travel-telemetry-autocapture`  
**Assignee:** [TBD]

---

## Progress Log

> Add dated entries as implementation proceeds (design notes, PRs, manual validation).

### 2025-10-21 — Plan realignment: Explicit generation
- Replaced auto-generation approach with explicit telemetry generation endpoint plan.
- Drafted `telemetry-generation-explicit.md` and updated roadmap/milestones.
- Next: implement endpoint + run metadata summary; realign UI to Artifacts/Run detail.

---

## Current Status

### Overall Progress
- [ ] Telemetry generation endpoint implemented
- [ ] Run metadata telemetry summary added
- [ ] Artifacts/Run detail UI updated (generate + replay selection)
- [x] Documentation updated (working plan + roadmap)

### Test Status
- Endpoint tests: pending
- Run metadata summary tests: pending
- UI tests: pending
- Manual verification: pending

---

## Risks & Notes
- Watch for long-running captures delaying UI response; ensure progress logs are visible.
- Confirm capture reuse logic is idempotent to prevent duplicate work.
- Stochastic templates remain out-of-scope; ensure failure messaging is explicit.

---

## Next Steps
1. Implement `POST /v1/telemetry/captures` and tests.
2. Add telemetry availability summary to run detail/list.
3. Update UI (Run detail) with generation action and replay gating.
4. Refresh capture guide and screenshots.

---

## References
- docs/milestones/TT-M-03.17.md
- docs/operations/telemetry-capture-guide.md
- docs/architecture/time-travel/ui-m3-roadmap.md
