# UI‑M‑03.21.01 Implementation Tracking — Artifacts UX Refresh

**Milestone:** UI‑M‑03.21.01 — Artifacts UX Refresh  
**Status:** Completed (2025-11-02)  
**Branch:** feature/ui-m-0321-01-artifacts-cards  
**Assignee:** UI Team

---

## Progress Log
- 2025-10-28 — Milestone drafted to modernize the Artifacts experience.
- 2025-10-29 — Scope finalized: standard card density, drawer details, visible Dashboard/Topology actions, chips + search with query persistence, pagination (100/page), no skeletons, lazy details.
- 2025-11-02 — Implementation completed; cards, drawer, filters/search/sort, and tests merged to feature branch.

---

## Checklist

### Card Layout (Standard Density)
- [x] Responsive card grid (auto; min 300px; 12–16px gaps)
- [x] Fields: template title, short runId, created (UTC), warnings summary, mode, grid summary
- [x] Initials avatar fallback; optional template icon when present
- [x] Card works as accessible button (aria-label)

### Details Drawer (Desktop)
- [x] Drawer with telemetry summary, provenance, warnings
- [x] Lazy-load details on open; no caching
- [x] ESC closes; labelled-by card title

### Actions
- [x] Visible card buttons: Dashboard, Topology
- [x] Telemetry generation in drawer (overwrite default OFF; no confirm)

### Filters/Sorting/Search
- [x] Chips: mode, warnings
- [x] Search box: template name / runId
- [x] Sorting chips: Created (default desc), Status, Template
- [x] State persisted in query params (read defaults from localStorage)

### Pagination & Deep Linking
- [x] Pagination (100/page)
- [x] /artifacts?runId=… opens drawer; filters restored from URL

### Loading/Empty States
- [x] Friendly loader/empty state (no skeletons)
- [x] Inline alert + retry on fetch errors

### Accessibility & Testing
- [x] Keyboard interactions: Enter/Space open; ESC close; focus order
- [x] Unit tests: filter/sort/state persistence
- [x] Render tests: card count, drawer open, actions visible
- [x] A11y smoke: roles/labels/keys

---

## Commands
```bash
# UI tests
dotnet test tests/FlowTime.UI.Tests/FlowTime.UI.Tests.csproj
```
