# UI‑M‑03.20 — SLA Dashboard (tiles + mini bars)

Purpose
- Deliver the first Phase 2 visualization: a minimal, performant SLA dashboard built on top of canonical run bundles and new backend helpers.

User Story
- As an operator, I want to open a run and immediately see SLA status for key services/flows along with a compact sparkline so I can judge health and drill into topology.

Dependencies
- Backend: `GET /v1/runs/{id}/metrics` (or `metrics.json` artifact) providing `slaPct`, `binsMet`, `binsTotal`, `mini[]` per service/flow (see TT‑M‑03.21).
- Backend: `GET /v1/runs/{id}/state_window` for fallback or richer drill‑downs.

Deliverables
- Dashboard page (MudBlazor): tiles for each service/flow in the run.
  - Tile fields: name, SLA % (rounded), binsMet/binsTotal, color/status, mini-bar sparkline.
  - Sorting (by SLA%), basic filters (status: red/yellow/green/gray).
  - Clicking a tile navigates to Topology with the same `runId` and highlights the service (future session UI‑M‑03.24).
- UI service adapter: fetch metrics via REST; graceful fallback if endpoint is missing (compute from `state_window`).
- Route and menu entry under Time‑Travel → Dashboard.

Acceptance Criteria
- Opening Dashboard with a valid `runId` renders tiles with sane defaults within 200ms of metrics fetch.
- Tiles show SLA% and mini-bars consistent with backend (tolerate ±0.5% rounding).
- Empty/partial metrics yields graceful UI (placeholder tile, retry affordance).
- Basic keyboard navigation works and tiles are screen-reader friendly (title, SLA% announced).

Implementation Notes
- Keep tiles lightweight; render mini-bars using a simple canvas/SVG sequence.
- Normalize `mini[]` to [0,1]; color scales follow backend thresholds (green/yellow/red/gray) for consistency.
- Defer advanced interactions (range presets, drill-in) to later sessions.

Files to Create/Modify
- `src/FlowTime.UI/Pages/TimeTravel/Dashboard.razor` (NEW)
- `src/FlowTime.UI/Services/TimeTravelMetricsClient.cs` (NEW)
- `src/FlowTime.UI/Layout/ExpertLayout.razor` (menu link)
- Tests: `tests/FlowTime.UI.Tests/TimeTravel/DashboardTests.cs`

Risks & Mitigations
- Endpoint lag: show shimmer/loading skeletons; timeout with soft error.
- Large runs: paginate tiles or filter by tag; soft-limit tile count (configurable).
- Visual density: adopt compact layout and clip long names.

Timeline
- 2–3 days UI work, gated by TT‑M‑03.21 metrics availability (or fallback path from `state_window`).

