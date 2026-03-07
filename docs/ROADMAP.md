# FlowTime Roadmap — Updated 2026-01-24

This roadmap supersedes the previous legacy plan (now archived under `docs/archive/ROADMAP-2025-06-legacy.md`). It reflects the current state of FlowTime Engine + Sim, acknowledges what the time-travel epic delivered, and outlines the next waves of work. Use this document as the high-level planning guide; architecture **epics** and milestone docs provide the implementation detail (see `docs/architecture/epic-roadmap.md` and `docs/development/epics-and-milestones.md`).

## Scope & Assumptions
- Engine remains responsible for deterministic execution, artifact generation, and `/state` APIs (see `docs/flowtime-engine-charter.md`).
- FlowTime.Sim owns template authoring, stochastic inputs, and template catalog endpoints.
- Time-travel V1 (TT‑M‑03.32.1) is feature-complete: DLQs first-class, template retrofits done, `/state` + `/state_window` live (see `docs/architecture/time-travel/status-2025-11-23.md`).
- The roadmap below captures cross-epic gaps plus future aspirations called out in the whitepaper (`docs/architecture/whitepaper.md`).
- Product-level scope is summarized in `docs/flowtime-charter.md`.

## Recently Delivered
- Time-travel APIs (`/runs/{id}/state`, `/state_window`) plus telemetry capture/bundles and CLI orchestration.
- Evaluation integrity (compile-to-DAG) and centralized model compiler.
- Edge time bins, conservation checks, and edge overlays in the UI.
- MCP modeling + analysis server (drafts, data intake, profile fitting, run/import/inspect loop, storage-backed drafts/bundles).
- Engine semantics layer contract hardening (M‑09.01).
- Dependency constraints Option A + Option B foundations (with follow-up enforcement in-flight).

## Near-Term Focus (Next Epics)
1. **Dependency Constraints & Shared Resources (follow-up)**  
   - Complete pattern enforcement and MCP guidance (M‑10.03).  
   - Keep Option A/Option B aligned with engine semantics and surface constraint status clearly in the UI.
2. **Visualizations (Chart Gallery / Demo Lab)**  
   - Dedicated UI space to prototype role-focused charts using FlowTime-derived metrics and raw telemetry comparisons.
3. **Telemetry Ingestion + Canonical Bundles**  
   - Implement ingestion services or hardened CLI workflows that emit canonical bundles aligned with engine schemas.
4. **Path Analysis & Subgraph Queries**  
   - Path-level queries and derived metrics based on edge time bins for UI and MCP use.

## Mid-Term / Aspirational
**Telemetry Loop & Parity** – Ensure synthetic runs and telemetry replays match within defined tolerances; see `docs/architecture/telemetry-loop-parity/README.md`.  

**Scenario Overlays & What-If Runs** – Derived overlay runs for capacity, parallelism, and arrivals experiments with deterministic provenance; see `docs/architecture/overlays/overlays.md`.  

**Flow-Aware Anomaly & Pathology Detection** – Build on the time-binned DAG model to detect anomalies and recurring flow pathologies (e.g., retry storms, slow drains), group them into incidents, and surface incident-focused stories and dashboards for SREs and stakeholders; see `docs/architecture/anomaly-detection/README.md`.  

**Ptolemy-Inspired Semantics & Directors** – Keep time/coordination semantics explicit (e.g., a DiscreteTime director seam) and selectively borrow ideas like modal models, typed ports, and determinacy contracts to future-proof FlowTime’s engine while staying DT-first; see `docs/architecture/ptolemy/README.md`.  

## References
- `docs/architecture/time-travel/status-2025-11-23.md` — Latest epic + global gap status.  
- `docs/architecture/whitepaper.md` — Engine vision + future primitives.  
- `docs/architecture/expression-extensions-roadmap.md` — Justification for advanced operators.  
- `docs/architecture/epic-roadmap.md` — Architecture epics, their ordering, and links to detailed docs.  
- `docs/development/epics-and-milestones.md` — How epics map to milestones and where to place new work.  
- `docs/flowtime-engine-charter.md` — Engine remit and non-goals.
