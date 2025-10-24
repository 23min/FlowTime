# UI‑M‑03.20 Implementation Tracking — SLA Dashboard

**Milestone:** UI‑M‑03.20 — SLA Dashboard (tiles + mini bars)  
**Status:** Planned  
**Branch:** feature/ui-m-0320-sla-dashboard  
**Assignee:** UI Team

---

## Progress Log
- 2025-10-24 — Drafted milestone and tracker; confirmed endpoint shape with TT‑M‑03.21.

---

## Checklist

### Dashboard Page
- [ ] Route + menu entry under Time‑Travel
- [ ] Tile component (name, SLA%, binsMet/binsTotal, mini-bar)
- [ ] Sorting & basic filters (status)
- [ ] Keyboard/a11y pass (labels, focus order)

### Data Adapter
- [ ] REST client for `/v1/runs/{id}/metrics`
- [ ] Fallback to `state_window` if metrics endpoint unavailable
- [ ] Error/loading states (skeletons, retry)

### Tests
- [ ] Tile render unit tests
- [ ] Integration test: dashboard fetch renders tiles for a demo run

---

## Commands
```bash
# UI tests
dotnet test tests/FlowTime.UI.Tests/FlowTime.UI.Tests.csproj
```

