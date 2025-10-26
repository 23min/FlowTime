# UI‑M‑03.21.01 Implementation Tracking — Artifacts UX Refresh

**Milestone:** UI‑M‑03.21.01 — Artifacts UX Refresh  
**Status:** Planned (scope locked 2025-10-29)  
**Branch:** feature/ui-m-0321-01-artifacts-cards  
**Assignee:** UI Team

---

## Progress Log
- 2025-10-28 — Milestone drafted to modernize the Artifacts experience.
- 2025-10-29 — Scope finalized: standard card density, drawer details, visible Dashboard/Topology actions, chips + search with query persistence, pagination (100/page), no skeletons, lazy details.

---

## Checklist

### Card Layout (Standard Density)
- [ ] Responsive card grid (auto; min 260–280px; 12–16px gaps)
- [ ] Fields: template title, short runId, created (UTC), status/warnings, mode, grid summary
- [ ] Initials avatar fallback; optional template icon when present
- [ ] Card works as accessible button (aria-label)

### Details Drawer (Desktop)
- [ ] Drawer with telemetry summary, provenance, warnings
- [ ] Lazy-load details on open; no caching
- [ ] ESC closes; labelled-by card title

### Actions
- [ ] Visible card buttons: Dashboard, Topology
- [ ] Telemetry generation in drawer (overwrite default OFF; no confirm)

### Filters/Sorting/Search
- [ ] Chips: status, mode, warnings
- [ ] Search box: template name / runId
- [ ] Sorting select: Created (default desc), Status, Template
- [ ] State persisted in query params (read defaults from localStorage)

### Pagination & Deep Linking
- [ ] Pagination (100/page)
- [ ] /artifacts?runId=… opens drawer; filters restored from URL

### Loading/Empty States
- [ ] Friendly loader/empty state (no skeletons)
- [ ] Inline alert + retry on fetch errors

### Accessibility & Testing
- [ ] Keyboard interactions: Enter/Space open; ESC close; focus order
- [ ] Unit tests: filter/sort/state persistence
- [ ] Render tests: card count, drawer open, actions visible
- [ ] A11y smoke: roles/labels/keys

---

## Commands
```bash
# UI tests
dotnet test tests/FlowTime.UI.Tests/FlowTime.UI.Tests.csproj
```
