# TT-M-03.17 Implementation Tracking

**Milestone:** TT-M-03.17 — Telemetry Auto-Capture Orchestration  
**Status:** ✅ Completed  
**Branch:** `feature/time-travel-ui-m3`  
**Assignee:** Time-Travel Platform

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
- [x] Telemetry generation endpoint implemented
- [x] Run metadata telemetry summary added
- [x] Artifacts/Run detail UI updated (generate + replay selection)
- [x] Telemetry bundles default to run-scoped `model/telemetry/` (shared library optional)
- [x] Docs/contracts updated to replace `gold/` placeholder with `aggregates/`
- [x] Documentation updated (working plan + roadmap)

### Test Status
- Endpoint tests: ✅ (`TelemetryCaptureEndpointsTests`)
- Run metadata summary tests: ✅ (goldens updated in `RunOrchestrationGoldenTests`)
- UI tests: ✅ (existing suites pass; availability chip exercised)
- Manual verification: ✅ (simulation run → capture → availability, conflict, overwrite)

---

## Risks & Notes
- Watch for long-running captures delaying UI response; ensure progress logs are visible.
- Confirm capture reuse logic is idempotent to prevent duplicate work.
- Stochastic templates remain out-of-scope; ensure failure messaging is explicit.
- Path move requires coordination with replay consumers/tests when we switch to run-scoped telemetry.

---

## Next Steps
1. Optional: add replay viewer route (`/time-travel/view?runId=...`).
2. Optional: script to migrate legacy telemetry manifests to canonical location.
3. Refresh capture guide screenshots in a docs-only follow-up.

---

## References
- docs/milestones/TT-M-03.17.md
- docs/operations/telemetry-capture-guide.md
- docs/architecture/time-travel/ui-m3-roadmap.md
