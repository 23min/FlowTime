# FlowTime Roadmap — Updated 2025-11-23

This roadmap supersedes the previous legacy plan (now archived under `docs/archive/ROADMAP-2025-06-legacy.md`). It reflects the current state of FlowTime Engine + Sim, acknowledges what the time-travel epic delivered, and outlines the next waves of work. Use this document as the high-level planning guide; milestone docs and architecture notes provide the implementation detail.

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
2. **TelemetryLoader Service**  
   - Architecture docs still assume an ADX/KQL loader feeding Engine; today we rely on CLI capture/bundles. Synthetic telemetry already defines the contract/schema (`docs/schemas/time-travel-state.schema.json`), so any eventual ADX ingestion must produce artifacts that match what the synthetic loop emits.  
   - Work: decide whether to implement the loader, replace it with improved CLI workflows, or update docs to match reality.
3. **Topology Layout Metadata**  
   - Templates currently omit `topology.ui` hints; UI uses heuristics.  
   - Work: either formalize deterministic layout rules or allow templates to carry optional positions/lane metadata to stabilize DAG rendering.
4. **Analyzer Cross-Node Checks**  
   - Need invariant rules that compare queue arrivals to upstream `served` (and similar cross-node relationships) to catch semantic mistakes early.  
   - Depends on either edge metrics or explicit topology lookup support in analyzers.
5. **Expression Extensions & Conditional Logic**  
   - Track the features itemized in `docs/architecture/expression-extensions-roadmap.md` (ABS/SQRT/POW, EMA/DELAY, IF, router/autoscale helpers).  
   - Prioritize based on upcoming use cases (e.g., autoscale epic, smooth retry policies).
6. **Retention / Bundle Promotion Helpers**  
   - TT‑M‑03.17 deferred run retention policy and CLI helpers for promoting local bundles to shared libraries.  
   - Scope policy + tooling to avoid artifact sprawl.

## Mid-Term / Aspirational
- **Router & Autoscale Primitives** – First-class node types for routing shares, autoscale feedback loops (whitepaper §2, expression roadmap).  
- **Per-Class Modeling & PMF Sampling** – Support classed flows/multi-class fairness, stochastic PMF sampling rather than expected values.  
- **Advanced Retry/Service-Time Overlays** – Edge heatmaps, retry service-time overlays (see TT‑M‑03.27 deferred items).  
- **Telemetry Calibration** – Parameter fitting from telemetry (retry kernels, capacities).  
- **Uncertainty / Monte Carlo** – Percentile bands, repeated runs with randomness (whitepaper future direction).  
- **Performance Scaling / WASM Runtime** – Larger DAG support and potential in-browser evaluation for demos.

## References
- `docs/architecture/time-travel/status-2025-11-23.md` — Latest epic + global gap status.  
- `docs/architecture/whitepaper.md` — Engine vision + future primitives.  
- `docs/architecture/expression-extensions-roadmap.md` — Justification for advanced operators.  
- `docs/flowtime-engine-charter.md` — Engine remit and non-goals.
