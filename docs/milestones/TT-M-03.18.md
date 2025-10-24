# TT-M-03.18 — Phase 1 Closeout (Ready for Minimal Visualizations)

Status: In review  
Dependencies: ✅ TT‑M‑03.17 (Explicit Telemetry Generation), ✅ UI‑M‑03.10..03.16  
Owners: Time‑Travel Platform / UI Core  
Intent: Replace the previous “replay selection polish” scope with a Phase 1 closeout that confirms readiness to start Phase 2 (Minimal Time‑Travel Visualizations).

---

## Summary

TT‑M‑03.17 delivered explicit telemetry generation, availability surfacing, and UI actions/gating. The run detail view already exposes telemetry availability, generated‑at timestamp, and warning counts without showing filesystem paths, and the replay actions are correctly gated by availability. As such, no additional UI polish is required here.

This milestone finalizes Phase 1 by aligning docs/roadmap and confirming test coverage, so we can begin Phase 2 work (SLA tiles, scrubber, topology, node panels).

---

## What’s Complete (Phase 1)

- Explicit generation endpoint + service: `POST /v1/telemetry/captures` (409 on collision; overwrite supported).
- API responses include minimal telemetry summary on run list/detail: `available`, `generatedAtUtc`, `warningCount`, `sourceRunId` (no paths).
- UI run detail: telemetry chip, generated‑at, warning count; actions to Generate, Replay from model, Replay from telemetry; gating enforced by availability and `canReplay`.
- Artifacts list: Telemetry column (Yes/No) and Created (UTC) inferred from runId when needed.
- Tests: API + UI suites passing; manual verification checklist exercised.

---

## Remaining Before Phase 2

- Roadmap alignment: update the UI M3 milestone mapping to reflect explicit capture (TT‑M‑03.17 complete) and mark TT‑M‑03.18 as Phase 1 Closeout; move seeded RNG to TT‑M‑03.19.
- Documentation sync: ensure capture guide and architecture notes consistently refer to explicit generation and telemetry summary fields; remove stale “auto‑capture” language where present.
- Phase 2 data contracts and access plan:
  - Decide REST vs file adapter for `graph` and `metrics`; today we expose `/v1/runs/{id}/index`, `/state`, `/state_window`, and `series/*`.
  - If REST: add `GET /v1/runs/{id}/graph` (derive from model.yaml) and a minimal `GET /v1/runs/{id}/metrics` (SLA aggregates/mini‑bars).
  - If file adapter: document required files (`graph.json`, `state_window.json`, `metrics.json`) and how they’re produced.
- UI scaffolding for Phase 2: global scrubber state, data plumbing to Dashboard/Topology/Node panel pages.

None of these items require additional Phase 1 implementation work beyond docs/roadmap updates; they are entry points for Phase 2.

---

## Acceptance Criteria (Phase 1 Closeout)

- Docs reflect explicit capture flow and telemetry summary; roadmap milestone mapping updated (TT‑M‑03.18 = closeout; TT‑M‑03.19 = seeded RNG, deferred).
- API and UI tests green; manual flows validated:
  - Simulation run shows telemetry unavailable; replay‑from‑telemetry disabled.
  - After generation, availability flips to available; timestamp and warnings surface; replay‑from‑telemetry enabled.
  - Re‑generate without overwrite returns 409 and is surfaced by the UI; overwrite succeeds.
- No filesystem paths shown in run list/detail; internal directories remain hidden.

---

## Notes

- Viewer route remains out of scope for Phase 1; Phase 2 will implement Dashboard/Topology/Node detail views.
- Seeded RNG support is tracked under TT‑M‑03.19 and does not block Phase 2 visualizations.

---

## References
- docs/milestones/TT-M-03.17.md
- docs/architecture/time-travel/ui-m3-roadmap.md
- docs/operations/telemetry-capture-guide.md
