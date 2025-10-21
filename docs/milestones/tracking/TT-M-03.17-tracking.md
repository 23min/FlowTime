# TT-M-03.17 Implementation Tracking

**Milestone:** TT-M-03.17 — Telemetry Auto-Capture Orchestration  
**Status:** 📋 Planned  
**Branch:** `feature/time-travel-telemetry-autocapture`  
**Assignee:** [TBD]

---

## Progress Log

> Add dated entries as implementation proceeds (design notes, PRs, manual validation).

### YYYY-MM-DD — Milestone Kickoff
- Reviewed TT-M-03.17 spec; aligned on API/UI scope.
- TDD plan drafted (unit + integration + UI tests).

---

## Current Status

### Overall Progress
- [ ] Backend auto-capture helper implemented
- [ ] `/v1/runs` API extended with auto-capture flow
- [ ] UI orchestration messaging updated
- [ ] Documentation refreshed (guide + roadmap)

### Test Status
- Backend unit tests: pending
- API integration tests: pending
- UI tests: pending
- Manual verification: pending

---

## Risks & Notes
- Watch for long-running captures delaying UI response; ensure progress logs are visible.
- Confirm capture reuse logic is idempotent to prevent duplicate work.
- Stochastic templates remain out-of-scope; ensure failure messaging is explicit.

---

## Next Steps
1. Add failing unit tests for auto-capture detection/generation.
2. Add failing `/v1/runs` integration tests covering generate/reuse/error paths.
3. Implement backend helpers and wiring until tests pass.
4. Update UI messaging/tests.
5. Refresh docs and perform manual validation checklist.

---

## References
- docs/milestones/TT-M-03.17.md
- docs/operations/telemetry-capture-guide.md
- docs/architecture/time-travel/ui-m3-roadmap.md
