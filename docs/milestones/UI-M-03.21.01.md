# UI‑M‑03.21.01 — Artifacts UX Refresh

**Status:** Proposed  
**Owner:** UI Team  
**Prerequisites:** UI‑M‑03.20 (SLA Dashboard)

---

## Goals
- Replace the tabular Artifacts list with responsive card tiles that surface key run metadata (Standard density).
- Introduce a contextual details drawer (desktop focus; mobile not a target) with telemetry status, provenance, and quick actions without leaving the list.
- Add first-class visible action buttons on each card (Dashboard, Topology; consider additional as space allows).
- Provide chip-based filters and a search box aligned with the dashboard experience; persist selection via query params (read defaults from localStorage).
- Establish visual cues (initials avatar + optional template icon) for template/Run types to improve scanability.

## Deliverables
1. Card layout (Standard density)
   - Fields: template title, runId (shortened), created time (UTC), status (warnings/health), mode, grid summary.
   - Visuals: initials avatar fallback; use template-provided icon when available.
   - Grid: auto; min card width 260–280px; 12–16px gaps.
2. Details drawer (desktop)
   - Sections: telemetry summary + manifest/provenance; actions; warnings.
   - Activation: clicking a card (Enter/Space via keyboard) opens the drawer; ESC closes.
   - Lazy-load details on open; no preloading/caching required.
3. Quick actions (visible on card)
   - Buttons: Dashboard, Topology (visible). Optional third action if space allows (e.g., Open Artifact).
   - Telemetry generation remains available inside the drawer; overwrite default OFF; no extra confirmation.
4. Filter/sort/search/persistence
   - Filters: chips for status, mode, warnings; text search (template name/runId).
   - Sorting: select for Created (desc by default), Status, Template.
   - Persistence: encode current state in query params; on first load, read defaults from localStorage.
5. Deep links
   - Opening /artifacts?runId=… auto-opens drawer for that run; filters/sort state recovered from query params.

## Success Criteria
- Card view renders tens of runs smoothly; no noticeable layout shift when interacting.
- Operators can open Dashboard/Topology for any run in ≤ 1 click from a card.
- Drawer exposes at least the same metadata as the current summary section, plus quick actions.
- A11y: cards act as accessible buttons (aria-label), Enter/Space opens drawer, ESC closes, drawer labelled-by card title; focus visible and deterministic.
- Deep linking works for runId and filter state via URL.

## Out of Scope
- Deep topology redesigns or new analytics views.
- Mobile-first layout (ensure it doesn’t break, but not a target).
- Template icon asset pipeline (use optional icon metadata when present; fallback to initials avatar).

## Timeline
- **Planning:** immediately after UI‑M‑03.20 completion.
- **Implementation:** estimated 1 sprint (dataset is tens of runs; no client-side caching required).

## Implementation Notes (Decisions Locked)
1. Density: Standard (title, short runId, created, status, mode, grid, warnings).
2. Scaling: Pagination (start with 50–100/page; default 100).
3. Details: Drawer on desktop; no separate mobile track.
4. Actions: Visible Dashboard/Topology buttons on cards; telemetry generation in drawer; overwrite OFF; no confirm dialog.
5. Filters: Chips + search; persist to query params; read defaults from localStorage.
6. Iconography: Initials avatar fallback; use template icon when available (optional metadata field).
7. Grid: CSS auto grid; min 260–280px; 12–16px gaps.
8. A11y: card-as-button; labelled drawer; ESC close; focus trap not required on desktop.
9. Performance: No special caching; lazy-load details on drawer open.
10. Loading/Empty: Friendly loader/empty states (no skeletons).
11. Data: Summaries list + lazy details on open.
12. Deep linking: runId opens drawer; filters in URL.
13. Testing: Implement “all we can do without heavy mocking” (unit tests for filter/sort/state; render tests for card count/drawer/actions; a11y smoke for key interactions).
