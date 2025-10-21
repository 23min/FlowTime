# TT-M-03.18 Implementation Tracking

**Milestone:** TT-M-03.18 — Telemetry Auto-Capture (Seeded RNG Support)  
**Status:** 📋 Planned  
**Branch:** `feature/time-travel-telemetry-seeded`  
**Assignee:** [TBD]

---

## Progress Log

> Add dated entries as work progresses (design notes, PRs, manual validation).

### YYYY-MM-DD — Milestone Kickoff
- Reviewed TT-M-03.18 spec; confirmed RNG/PMF scope.
- Drafted TDD plan (backend seed helper, API integration, UI messaging).

---

## Current Status

### Overall Progress
- [ ] Seed helper implemented and tested
- [ ] Telemetry capture metadata updated (seed/provenance)
- [ ] `/v1/runs` exposes seeded status + metadata
- [ ] UI surfaces seed messaging
- [ ] Documentation refreshed

### Test Status
- Backend unit tests: pending
- API integration tests: pending
- UI tests: pending
- Manual verification: pending

---

## Risks & Notes
- Verify seeds are deterministic and logged safely.
- Ensure metadata persists even if capture directory is moved/renamed.
- Existing deterministic workflow must remain unaffected.

---

## Next Steps
1. Add failing unit tests for seed helper logic.
2. Add failing `/v1/runs` integration tests covering seeded generation/reuse.
3. Implement backend changes until tests pass.
4. Update UI models/tests with seed messaging.
5. Refresh docs and run manual verification checklist.

---

## References
- docs/milestones/TT-M-03.18.md
- docs/milestones/TT-M-03.17.md
- docs/operations/telemetry-capture-guide.md
