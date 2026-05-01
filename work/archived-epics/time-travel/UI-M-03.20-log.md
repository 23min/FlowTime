# UI‑M‑03.20 Implementation Tracking — SLA Dashboard

**Milestone:** UI‑M‑03.20 — SLA Dashboard (tiles + mini bars)  
**Status:** Completed  
**Branch:** feature/time-travel-ui-m3  
**Assignee:** UI Team

---

## Progress Log
- 2025-10-24 — Drafted milestone and tracker; confirmed endpoint shape with TT‑M‑03.21.
- 2025-10-27 — Implemented dashboard page, metrics client (with fallback), and baseline tests.
- 2025-10-28 — Added direct navigation buttons from Artifacts to Dashboard/Topology.
- 2025-10-29 — Wrapped milestone; a11y sweep + integration verification completed, follow-up UI‑M‑03.21.01 logged for Artifacts UX overhaul.

---

## Checklist

### Dashboard Page
- [x] Route + menu entry under Time‑Travel
- [x] Tile component (name, SLA%, binsMet/binsTotal, mini-bar)
- [x] Sorting & basic filters (status)
- [x] Keyboard/a11y pass (labels, focus order)

### Data Adapter
- [x] REST client for `/v1/runs/{id}/metrics`
- [x] Fallback to `state_window` if metrics endpoint unavailable
- [x] Error/loading states (skeletons, retry)

### Tests
- [x] Tile render unit tests
- [x] Integration test: dashboard fetch renders tiles for a demo run

---

## Commands
```bash
# UI tests
dotnet test tests/FlowTime.UI.Tests/FlowTime.UI.Tests.csproj
```

---

## Follow-up Planning
- **Proposed UI-M-03.21B — Artifacts UX Refresh**
  - Replace the tabular run list with responsive cards plus a details panel.
  - Expose direct actions (Dashboard, Topology, Generate Telemetry) on each card.
  - Add quick filters/sorting chips mirrored from the dashboard experience.
  - Introduce iconography/imagery per run type to improve scanability.
  - Draft milestone after UI‑M‑03.20 closes; target kickoff immediately afterward.
