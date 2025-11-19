<!-- moved from docs/time-travel/UI-M3-roadmap.md -->
# UI M3 Roadmap — Time-Travel UI (Minimal First)

**Purpose:** Pragmatic two-phase plan to modernize the MudBlazor UI for M3 and ship a minimal, reliable time‑travel experience based on run bundles (formerly referred to as “gold data”). No transactional/customer drilldown; focus on stability and clarity.

> **Terminology note:** Historical references to “gold bundles/data” map to the canonical run bundle (per-series CSVs plus the `aggregates/` placeholder).

---

## Phases Overview

- Phase 1 — Align With M3 (Refactor and Restore)
  - Bring the existing UI in sync with M3 engine/sim changes.
  - Review and rationalize navigation. Restore Artifacts. Rework Simulate to feed time‑travel runs. Decide fate of Analyze.
  - Establish REST data access services for run bundles so UI surfaces can consume `/v1/runs` endpoints.
- Phase 2 — Minimal Time-Travel Visualizations (from docs/ui/time-travel-visualizations-3.md)
  - SLA Dashboard tiles with mini bars (no forecasting).
  - Flow Topology (entire graph) with global scrubber and heat‑map node coloring.
  - Node Detail Panel with simple line charts by node type.

---

## Navigation Plan (MudBlazor Left Menu)

- Add top‑level: Time-Travel
  - Dashboard (SLA)
  - Topology
  - Run Orchestration
  - Artifacts (link to restored page)
- Existing
  - Simulate: rework to emit gold runs compatible with time‑travel
  - Analyze: hidden for now (UI-M-03.13 Option A); revisit with Phase 2 analytics

Note: Implementation will touch `ui/FlowTime.UI/Layout/ExpertLayout.razor` (menu structure) and add routes/pages for the three views plus orchestration.

---

## Data Contracts (Gold, Minimal)

- `GET /v1/runs/{runId}/graph` — nodes (`id`, `kind`, `semantics`, optional `ui`) plus edges (`from`, `to`, `weight`)
- `GET /v1/runs/{runId}/state_window` — `binMinutes`, aligned timestamps, per-node series payload
- `GET /v1/runs/{runId}/metrics` — per-service SLA aggregates and normalized mini sparkline values

Fallback (when UI runs against raw bundles): `runs/{runId}/graph.json`, `state_window.json`, `metrics.json` retain the same shapes. The API writes `metrics.json` during run creation so offline consumers stay in sync without recomputing SLA aggregates.

---

## Phase 1 — Align With M3 (Refactor and Restore)

Session 1: Baseline + Build Health
- Outcome: UI builds/loads with current dependencies; remove broken routes temporarily.
- Checks: Solution builds; app boots; left nav renders without runtime errors.

Session 2: Artifacts Page Restoration
- Outcome: “Artifacts” lists available run bundles, basic metadata (runId, time range), and open actions.
- Data: read from local run bundle folder(s); no API required.
- Checks: Open action deep‑links to Time‑Travel pages with the selected `runId`.

Session 3: Simulate → Gold Run Integration
- Outcome: Simulate workflows output run bundles discoverable by Artifacts and Time‑Travel pages.
- Data: reuse CLI/engine workflow described in `docs/operations/telemetry-capture-guide.md`.
- Checks: After simulation, new run appears under Artifacts; Topology/SLA can open it.

Session 4: Analyze Section Decision
- Outcome: Either hide (temporarily) or repurpose to a “Data Access Test” for gold time‑series.
- Checks: If kept, verify it can read `state_window.json` and display a basic health check.

Session 5: Left Nav Structure + Routes (Skeleton)
- Outcome: Insert “Time‑Travel” group with Dashboard, Topology, Run Orchestration, and Artifacts.
- Checks: Navigates to placeholder pages; no functional charts yet.

Session 6: Data Access Adapter
- Outcome: UI service encapsulating gold access (file‑backed) with the same shapes planned for future API.
- Contracts: `graph.json`, `state_window.json`, `metrics.json`.
- Checks: Simple page reads each file and renders textual summaries.

Session 7: Run Orchestration Page (Skeleton)
- Outcome: Page to kick off telemetry bundling + ingest (bootstrap workflow), show progress and final `runId`.
- Data: orchestrates CLI/engine steps; surfaces logs; results discoverable by Artifacts.
- Checks: Finishes with a link to open the run in Topology/SLA.

Session 8: QA + Docs
- Outcome: Pass through basic flows; update inline help; capture known gaps.
- Checks: Regression pass on Simulate/Artifacts/Time‑Travel routing.

Session 9: Seeded Telemetry Loop (RNG Templates)
- Outcome: Auto-capture handles templates with PMF/RNG nodes by storing deterministic seeds; telemetry bundles become reproducible.
- Checks: Seed logged in metadata, UI surfaces seed info, replay uses generated bundle without reseeding. (TT-M-03.19 now provides default seed=123, optional override, and exposes the value in run detail views.)

---

## Phase 2 — Minimal Time-Travel Visualizations
(Primary source: `docs/ui/time-travel-visualizations-3.md`)

Session 1: SLA Dashboard (Tiles + Mini Bars)
- Outcome: Tiles showing flow name, SLA % (binsMet/total), status icon, and mini‑bar sparkline.
- Data: `metrics.json.flows[]` with `slaPct`, `binsMet`, `binsTotal`, `mini[0..1]`.
- Checks: Sort/filter; click navigates to Topology (focus current `runId`).

Session 2: Global Top Bar + Scrubber (State)
- Outcome: Range presets and start/end bound the visible window; a single global scrubber drives all views.
- Data: Time range maps to bin indices for `state_window.json`.
- Checks: Keyboard controls (←/→, Space) move bins; UI state observable across pages.

Session 3: Topology Canvas (Graph + Coloring)
- Outcome: Render entire graph (nodes/edges). Color nodes by SLA/util thresholds at current bin; Gray on no data.
- Data: `graph.json` + current `state_window` bin.
- Checks: Pan/zoom works; performance stays responsive with 20 nodes/flow.

Session 4: Node Detail Panel (Lines)
- Outcome: Right‑side panel with line charts; defaults by node type (queue: Q/Lat/Arr vs Srv; service: Lat/Util/Err).
- Data: Sliced series from `state_window.json` within range.
- Checks: Panel is sticky while scrubbing; current bin value highlights on lines.

Session 5: SLA ↔ Topology Linking
- Outcome: Clicking a tile opens Topology and centers relevant nodes; node selection persists.
- Checks: Back navigation keeps range/scrubber state.

Session 6: Performance + A11y Pass
- Outcome: Verify scrub update budget (≤200 ms), basic keyboard nav, colorblind‑aware indicators.
- Checks: Lightweight perf traces; documented color/label alternatives.

Session 7: Documentation + Stabilization
- Outcome: Help text, glossary, and in‑product tips; confirm contracts; polish edge cases.
- Checks: Sign‑off with product and ops stakeholders.

---

## Risks & Mitigations
- Missing/partial gold artifacts → Display graceful empty states; stub pages; log diagnostics.
- Large graphs → Canvas; avoid per‑frame heavy DOM; throttle redraw on scrub.
- Endpoint lag → Keep file‑based adapter behind an interface; swap to REST later without UI churn.

---

## Deliverables Summary
- Restored Artifacts page and Time-Travel navigation.
- Minimal SLA Dashboard, Topology with scrubber, Node Detail panel.
- Run Orchestration page (skeleton) that executes bundling/ingest workflow and registers runs.
- Explicit telemetry generation: operator-initiated action and API endpoint to create telemetry bundles; UI surfaces availability (not paths) and generated-at/ warning count.
- Data adapter for gold files (`graph.json`, `state_window.json`, `metrics.json`).

---

## References
- docs/ui/time-travel-visualizations-3.md
- docs/operations/telemetry-capture-guide.md
- ui/FlowTime.UI/Layout/ExpertLayout.razor

---

## Milestone Breakdown (Accepted Mapping)

We use UI-M-XX.XX with bands per phase to keep sessions small and reviewable.

- Phase 1 — Align With M3 (Refactor and Restore)
  - UI-M-03.10 — UI Baseline & Build Health
  - UI-M-03.11 — Restore Artifacts Page
  - UI-M-03.12 — Simulate → Gold Run Integration
  - UI-M-03.13 — Analyze Section Decision (complete — Option A “Hide Analyze for now”)
  - UI-M-03.14 — Time‑Travel Nav & Routes (skeleton) (complete — nav group + placeholders live)
  - UI-M-03.15 — Gold Data Access Service (REST) (complete — REST client + data service)
  - UI-M-03.16 — Run Orchestration Page (skeleton)
  - TT-M-03.17 — Explicit Telemetry Generation + Availability (complete)
  - TT-M-03.18 — Phase 1 Closeout (docs/roadmap alignment; no replay UI changes)
  - TT-M-03.19 — Seeded RNG & Provenance (in progress)
  - UI-M-03.18 — QA & Docs Pass

- Phase 2 — Minimal Time‑Travel Visualizations
  - UI-M-03.20 — SLA Dashboard (tiles + mini bars)
  - UI-M-03.21 — Global Top Bar + Range + Scrubber
  - UI-M-03.22 — Topology Canvas (graph + coloring)
  - UI-M-03.22.2 — Topology Canvas Polish (expr tooltip sparkline + inspector)
  - UI-M-03.23 — Node Detail Panel (simple lines)
  - UI-M-03.24 — SLA ↔ Topology Linking
  - UI-M-03.25 — Performance + A11y Pass
  - UI-M-03.26 — Documentation & Stabilization

Future extensions can continue at UI-M-03.30+.

---

## Epic — Retry + Service Time (TT‑M‑03.28..31)

Purpose: make retries and service time first‑class, end‑to‑end. Start with contracts and fixtures, then add UI overlays and stabilization.

- TT‑M‑03.28 — Retries Foundations (Attempts/Success/Failure + Retry Rate)
  - Templates: add dependency retry series in example system(s).
  - API: add `edges` section to `/state_window` carrying attempts/success/failure and derived `retryRate` with guards.
  - UI: inspector shows outgoing dependency retry rates for focused node; click highlights edge.
  - Doc: contract snippet and examples.

- TT‑M‑03.29 — Service Time (S) Derivation (Processing Time Sum)
  - Templates/fixtures: add per‑bin `processingTimeMsSum` and `servedCount` across the gallery (incident workflow, IT microservices, every supply-chain variant, manufacturing, network reliability, transportation).
  - API: derive `serviceTimeMs`; include in `/state` and `/state_window` for services.
  - UI: inspector Service Time chart + horizon; Feature Bar adds “Service Time” color basis with static 400 ms/700 ms thresholds (green/yellow/red).

- TT‑M‑03.30 — UI Overlays (Retries + Service Time)
  - TT‑M‑03.30.1 — Domain terminology aliases (template ➜ API ➜ UI wiring complete; see `docs/milestones/TT-M-03.30.1.md`)
  - Edge overlays: color by RetryRate or Attempts; legend and toggles in Feature Bar; persistence.
  - Derivation stays client-side for 03.30: sample node series from `/state_window` + graph metadata to avoid changing the contract; API `edges` slice moves to 03.31.
  - Node basis: allow coloring by Service Time; tooltip includes S at current bin.
  - Inspector ↔ canvas: hover/selection linking between retry list and edges.

- TT‑M‑03.31 — E2E Fixtures, Goldens, Docs (Closeout)
  - Reproducible fixtures; API golden snapshots; UI tests for overlays/basis; operator docs and demo script.
  - ✅ May 2025 update: `/state_window` now emits retry `edges` (attempts/failures/retryRate) when requested; UI consumes server-provided series. Fixture lives at `fixtures/time-travel/retry-service-time/` with README + CSVs. Golden snapshot `state-window-edges-approved.json` pins the payload. Docs + demo checklist captured in `docs/milestones/TT-M-03.31.md`.

References
- docs/milestones/TT-M-03.28.md
- docs/milestones/TT-M-03.29.md
- docs/milestones/TT-M-03.30.md
- docs/milestones/TT-M-03.31.md
