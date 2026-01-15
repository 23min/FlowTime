# FlowTime Roadmap — Updated 2025-11-23

This roadmap supersedes the previous legacy plan (now archived under `docs/archive/ROADMAP-2025-06-legacy.md`). It reflects the current state of FlowTime Engine + Sim, acknowledges what the time-travel epic delivered, and outlines the next waves of work. Use this document as the high-level planning guide; architecture **epics** and milestone docs provide the implementation detail (see `docs/architecture/epic-roadmap.md` and `docs/development/epics-and-milestones.md`).

## Scope & Assumptions
- Engine remains responsible for deterministic execution, artifact generation, and `/state` APIs (see `docs/flowtime-engine-charter.md`).
- FlowTime.Sim owns template authoring, stochastic inputs, and template catalog endpoints.
- Time-travel V1 (TT‑M‑03.32.1) is feature-complete: DLQs first-class, template retrofits done, `/state` + `/state_window` live (see `docs/architecture/time-travel/status-2025-11-23.md`).
- The roadmap below captures cross-epic gaps plus future aspirations called out in the whitepaper (`docs/architecture/whitepaper.md`).

## Recently Delivered
- Time-travel APIs (`/runs/{id}/state`, `/state_window`), telemetry capture/bundle pipeline, CLI orchestration.
- DLQ and backlog retrofits across canonical templates; analyzers enforce DLQ semantics.
- Updated docs: DLQ lens, template authoring/testing guides, expression extensions roadmap.

## Near-Term Focus (Next Epic Candidates)
1. **EdgeTimeBin / Edge Metrics (name TBD)**  
   - Goal: derive or surface per-edge throughput/attempt volumes for heat maps, conservation checks, and future edge overlays.  
   - Work: define schema (likely derived from node series), update `/state_window` or companion API, extend UI to consume.  
   - Status: not analyzed yet; needs dedicated design session.
2. **Telemetry Ingestion & Canonical Bundles**  
   - Architecture docs still assume an ADX/KQL loader feeding Engine; today we rely on CLI capture/bundles. Synthetic telemetry already defines the contract/schema, so any ingestion must emit artifacts that match the canonical bundle format.  
   - Work: decide whether to implement the loader, replace it with improved CLI workflows, and align ingestion with `docs/architecture/telemetry-ingestion/README.md`.
3. **Topology Layout Metadata**  
   - Templates currently omit `topology.ui` hints; UI uses heuristics.  
   - Work: either formalize deterministic layout rules or allow templates to carry optional positions/lane metadata to stabilize DAG rendering.
4. **Analyzer Cross-Node Checks**  
   - Need invariant rules that compare queue arrivals to upstream `served` (and similar cross-node relationships) to catch semantic mistakes early.  
   - Depends on either edge metrics or explicit topology lookup support in analyzers.
5. **Classes & Router Solidification / Topology Perf (FT‑M‑05.05 / 05.06)**  
   - FT‑M‑05.05: Rewire class-enabled templates (transportation, supply chain) to consume router outputs directly instead of legacy percentage splits; eliminates `router_class_leakage` warnings and keeps class chips accurate.  
   - FT‑M‑05.06: Once routers are solid, throttle topology hover interactions (JS/interop batching, inspector debounce) to reduce WASM load and keep the UI responsive on class-heavy runs.
6. **Expression Extensions & Conditional Logic / Services With Buffers**  
   - Track the features itemized in `docs/architecture/expression-extensions-roadmap.md` (ABS/SQRT/POW, EMA/DELAY, IF, router/autoscale helpers).  
   - Prioritize based on upcoming use cases (e.g., autoscale epic, smooth retry policies, and the ServiceWithBuffer epic under `docs/architecture/service-with-buffer/`).
6. **Retention / Bundle Promotion Helpers**  
   - TT‑M‑03.17 deferred run retention policy and CLI helpers for promoting local bundles to shared libraries.  
   - Scope policy + tooling to avoid artifact sprawl.

## Mid-Term / Aspirational
**Engine Semantics Layer** – Treat the engine as the semantics layer between canonical bundles and downstream UIs/BI tools; see `docs/architecture/engine-semantics-layer/README.md` for the detailed proposal.  

**Telemetry Loop & Parity** – Ensure synthetic runs and telemetry replays match within defined tolerances; see `docs/architecture/telemetry-loop-parity/README.md`.  

**Scenario Overlays & What-If Runs** – Derived overlay runs for capacity, parallelism, and arrivals experiments with deterministic provenance; see `docs/architecture/overlays/overlays.md`.  

**Flow-Aware Anomaly & Pathology Detection** – Build on the time-binned DAG model to detect anomalies and recurring flow pathologies (e.g., retry storms, slow drains), group them into incidents, and surface incident-focused stories and dashboards for SREs and stakeholders; see `docs/architecture/anomaly-detection/README.md`.  

**AI Analyst over the Digital Twin (MCP)** – Expose FlowTime graph and state APIs via MCP so AI assistants can act as read-only analysts over runs: answering incident questions, comparing scenarios, and drafting summaries using structured tools rather than ad-hoc API calls; see `docs/architecture/ai/`.  

**Ptolemy-Inspired Semantics & Directors** – Keep time/coordination semantics explicit (e.g., a DiscreteTime director seam) and selectively borrow ideas like modal models, typed ports, and determinacy contracts to future-proof FlowTime’s engine while staying DT-first; see `docs/architecture/ptolemy/README.md`.  

## References
- `docs/architecture/time-travel/status-2025-11-23.md` — Latest epic + global gap status.  
- `docs/architecture/whitepaper.md` — Engine vision + future primitives.  
- `docs/architecture/expression-extensions-roadmap.md` — Justification for advanced operators.  
- `docs/architecture/epic-roadmap.md` — Architecture epics, their ordering, and links to detailed docs.  
- `docs/development/epics-and-milestones.md` — How epics map to milestones and where to place new work.  
- `docs/flowtime-engine-charter.md` — Engine remit and non-goals.
