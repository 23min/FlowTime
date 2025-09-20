# FlowTime-Engine Charter (v1.0)

**Date:** 2025-09-20
**Audience:** Engineers, architects, UI developers, and contributors to FlowTime.
**Status:** Active scope definition.

---

## Purpose (one-liner)

FlowTime-Engine is the **execution core** of FlowTime. It evaluates models deterministically, computes flows across services and queues, and produces telemetry artifacts for analysis and comparison. It is the **sole generator of telemetry** in the FlowTime ecosystem.

---

## Scope (what FlowTime-Engine *will* do)

* **Deterministic execution**

  * Evaluate DAGs of nodes and edges on the canonical time grid.
  * Support stateful primitives: backlog, latency, retries, autoscaling, routing, fan-out.

* **Telemetry generation**

  * Export run outputs as standardized telemetry artifacts (Treehouse-compatible “Binned v0”: CSV/Parquet + `catalog.json`).
  * Include latency histograms where possible, so downstream quantiles can be recomputed.
  * Guarantee determinism: same model + seed → same telemetry.

* **Telemetry import (Replay Mode)**

  * Hydrate a run directly from Gold telemetry (CSV/Parquet + catalog).
  * Replay results faithfully (bit-for-bit identical).

* **Telemetry as input (Constrained Mode, later milestone)**

  * Allow telemetry to anchor **source nodes** while the engine computes downstream behavior.
  * Useful for “what-if” overlays on top of real arrivals.

* **Scenario execution (Overlay)**

  * Apply overlays to adjust arrivals, capacities, routing, outages.
  * Compute a comparison run (baseline vs scenario).
  * Produce quantitative comparison metrics (MAE, SMAPE, correlation, %Δ).

* **API-first runtime**

  * All features exposed via REST endpoints (`/run`, `/graph`, `/export`, `/compare`).
  * CLI and UI are thin layers over the same API surface.
  * Artifact determinism and schema validation at API boundaries.

* **Artifacts registry integration**

  * Every run produces a **Run artifact** with telemetry + catalog.
  * Artifacts are persisted and discoverable in the UI registry.
  * Artifacts are first-class citizens: runs, models, and telemetry can all be compared or replayed.

---

## Non-Scope (what FlowTime-Engine *will not* do)

* **No stochastic modeling templates**

  * Engine does not generate stochastic arrivals, retry kernels, or templates.
  * That is FlowTime-Sim’s role.

* **No telemetry contract ownership drift**

  * Engine does not invent its own telemetry formats.
  * Telemetry contracts evolve in a versioned, additive way (v0, v0.1, v1).

* **No free-form UI modeling**

  * Engine executes runs; it does not provide modeling editors.
  * YAML editing and template creation live in FlowTime-Sim and UI.

* **No long-term storage responsibility**

  * Engine emits artifacts; storage/indexing is handled by the artifacts registry service.

---

## Interfaces

* **Input ← FlowTime-Sim / UI**

  * Model artifacts: `model.yaml`, `catalog.json`.
  * Overlay artifacts: `overlay.yaml`.
  * Telemetry artifacts: Gold CSV/Parquet + catalog.

* **Output → FlowTime UI / external tools**

  * Telemetry artifacts: binned telemetry (`binned_v0.csv`/Parquet) + `catalog.json`.
  * Overlay comparison results: `diff.json`.
  * Graph inspection: `/graph` endpoint (nodes + edges).

* **UI flows**

  * **Runs wizard**:

    1. Select Input (Model or Telemetry)
    2. Configure Run (grid, overlay, checkbox for telemetry export)
    3. Compute
    4. Results (DAG view, metrics, artifacts)
  * **Compare button** on results:

    * Prompt for a second input (Model or Telemetry).
    * Engine runs/replays both and produces side-by-side results.

---

## Value

* Provides a **single source of truth** for all FlowTime computations.
* Ensures telemetry is **consistent, reproducible, and externally consumable**.
* Bridges stochastic modeling (Sim) with real-world observability (Treehouse pipeline).
* Supports both **what-if scenarios** and **analysis of real telemetry**.
* Serves as the **contract enforcer** for all telemetry artifacts.

---

## Future Directions

* **Extended primitives**: DLQ, multi-class fairness, advanced retries.
* **Calibration**: fit parameters (capacity, routing, retry kernels) from telemetry.
* **Uncertainty modeling**: Monte Carlo runs with percentile bands.
* **Performance scaling**: larger DAGs, distributed execution.
* **WASM runtime**: run Engine in-browser for interactive demos.

---

## UI & Workflow Implications

* **Artifacts Registry**: UI always persists runs, models, and telemetry as artifacts with catalogs.
* **No “forgetting”**: every artifact is discoverable, searchable, and reusable.
* **Telemetry import** is just another input option in Runs (no separate menu).
* **Compare** is contextual: launched from a run result or artifact card.
* **Wizard pattern** ensures step-by-step configuration (input → configure → compute → results).
* **Check-box toggle**: users explicitly choose whether to generate/export telemetry artifacts.

---

## Summary

* FlowTime-Engine = **executor + telemetry generator**.
* FlowTime-Sim = **model authoring + stochastic input generator**.
* **Telemetry artifacts** are the lingua franca: all runs, replays, and comparisons flow through them.
* **UI flows** are centered on Runs + Compare, with the artifacts registry as the persistent backbone.

This charter is explicit enough to **anchor milestones**:

* M2.5: close the loop (model → run → export → import → compare).
* M4: overlays and scenario runs.
* M7: backlog + latency primitives.
* M9: telemetry import & fitting.
* M14: calibration & drift detection.
