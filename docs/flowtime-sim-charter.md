# FlowTime-Sim Charter (v1.0)

> **🔗 Charter Ecosystem**: This charter operates within the broader [FlowTime-Engine Charter](../../flowtime-vnext/docs/flowtime-engine-charter.md) ecosystem. Together they define the **Engine+Sim** architecture supporting the artifacts-centric workflow: **[Models] → [Runs] → [Artifacts] → [Learn]**.

**Date:** 2025-09-20
**Audience:** Engineers, architects, UI developers, and contributors to FlowTime.
**Status:** Active scope definition.

---

## Purpose (one-liner)

FlowTime-Sim is the **modeling front-end** for the FlowTime Engine+Sim ecosystem. It generates model artifacts, stochastic input patterns, and templates for systems, but **never computes telemetry** - that remains the exclusive domain of FlowTime-Engine.

---

## Scope (what FlowTime-Sim *will* do)

* **Model generation & editing**

  * Produce model artifacts (`model.yaml`, `catalog.json`).
  * Provide templates (checkout, API chain, pub/sub, queue/retry).
  * Offer YAML editing with schema validation.

* **Stochastic input generation**

  * Encode PMFs, distributions, and stochastic processes for arrivals, retries, service times, and delays.
  * Provide parameterizable kernels (e.g., Poisson arrivals, lognormal service times).
  * Support shape-first modeling (e.g., normalized day-of-week PMFs).

* **Educational and demo use cases**

  * Supply ready-to-run demo models for showing FlowTime’s spreadsheet metaphor.
  * Help users explore queueing concepts (retries, backlogs, fan-out, autoscaling).

* **UI integration**

  * Templates panel with step-by-step wizard:

    1. Select template.
    2. Configure stochastic inputs (PMFs, scalars).
    3. Preview DAG.
    4. Save as Model artifact.
  * Model artifacts then appear in the registry and can be executed by FlowTime-Engine.

---

## Non-Scope (what FlowTime-Sim *will not* do)

* **No telemetry generation**

  * Sim does not export Gold/Silver telemetry.
  * Engine is the only component that produces telemetry.

* **No queue/backlog execution**

  * Sim does not run models, apply overlays, or compute latency.
  * Engine owns all DAG execution.

* **No telemetry contract evolution**

  * Sim does not own Treehouse/FlowTime telemetry schema.
  * It only emits models; telemetry contracts are enforced by Engine.

* **No persistent run artifacts**

  * Sim saves models only; runs and telemetry artifacts come from Engine.

---

## Interfaces

* **Output → FlowTime-Engine**

  * Model artifact (`model.yaml`, `catalog.json`).
  * Optional preview (`preview.svg`, DAG metadata).

* **Input ← FlowTime UI**

  * Template selection.
  * Stochastic input parameters.
  * Manual YAML editing.

* **Artifacts Registry**

  * Every saved Sim model is a Model artifact in the registry.
  * Actions on a model card: Run in Engine, Edit, Duplicate, Delete.

---

## Value

* Clean separation: Sim = *authoring*, Engine = *execution*.
* Enables modeling of stochastic systems without duplicating Engine semantics.
* Provides **educational value** via templates and DAG previews.
* Keeps telemetry generation **centralized** in Engine, ensuring consistency.

---

## Future Directions

* **Expanded libraries**: richer distributions, custom kernels, seasonal PMFs.
* **Scenario authoring**: generate families of models with parameter sweeps.
* **Domain templates**: finance, IoT, messaging, etc.
* **Interactive DAG sketcher**: graphical drag-and-drop modeling (future milestone).

---

## UI & Workflow Implications

* **Models tab** is the entry point for Sim.
* **Wizard pattern** (template → configure → preview → save).
* **Registry integration** ensures models are persistent and reusable in Engine.
* **No /run endpoint** — Sim does not execute. Users always run models via Engine.
* **DAG preview**: immediate visualization of structure + stochastic input shapes.

---

## Summary

* FlowTime-Sim = **model authoring + stochastic input generator**.
* FlowTime-Engine = **executor + telemetry generator**.
* Sim outputs **Model artifacts**, which Engine consumes to produce **Run artifacts** (telemetry).
* Together they support closed-loop modeling: **author → run → compare → refine**.

This charter is explicit enough to drive milestones:

* SIM-M0: minimal templates and YAML output.
* SIM-M1: stochastic input nodes (PMFs, distributions).
* SIM-M2: DAG preview.
* SIM-M3: model registry integration.
* SIM-Mx: scenario sweeps, domain templates, interactive sketcher.
