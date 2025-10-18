# UI-M-03.12 Implementation Tracking

**Milestone:** UI-M-03.12 — Simulate → Gold Run Integration  
**Status:** ⛔ Blocked (awaiting backend M-03.02.01)  
**Branch:** `feature/time-travel-ui-m3`  
**Assignee:** [TBD]

---

## Blocker Summary

- Backend milestone **M-03.02.01 — Simulation Run Orchestration** must ship before the Simulate UI can generate runs via `/v1/runs`.
- Current `/v1/runs` implementation only supports telemetry mode and requires a capture directory; simulation mode fails with "Telemetry bindings were supplied but capture directory is missing." (see CLI/API logs 2025-10-18).
- UI work is paused; changes were stashed (`ui simulate experiment`) pending backend delivery.

---

## Next Steps

1. Backend team completes M-03.02.01 (simulation orchestration and canonical artifact emission).
2. Rebase/restore UI branch once backend APIs are available.
3. Re-run UI-M-03.12 tasks (Simulate form, logs, validation) against updated `/v1/runs`.
4. Update this tracker when backend milestone ships; transition status to 🚧 In Progress.

---

## References

- `docs/milestones/M-03.02.01.md`
- `docs/architecture/time-travel/time-travel-planning-roadmap.md`
- Simulation API payloads/logs (2025-10-18 Dev Journal)

