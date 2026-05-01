---
id: D-029
title: E-19 owns current-surface orchestration cleanup; E-18 owns the headless foundation
status: accepted
---

**Status:** active
**Context:** Current template-driven run creation lives in `FlowTime.Sim.Service`, with storage-backed drafts, archived run bundles, bundle import flows, and catalog-era residue still visible on active first-party surfaces. The repo also has a planned headless future in E-18. Without an explicit boundary, today's Sim orchestration and archive/import choreography can harden into the accidental programmable contract.
**Decision:** E-19 owns inventorying, narrowing, and deleting current Sim/UI/catalog/storage compatibility seams and publishing the supported-surface matrix. E-18 owns only the replacement headless foundation: runtime parameter identity, deterministic overrides, reevaluation APIs, evaluation SDK, and headless CLI/sidecar over compiled graphs. Current Sim authoring/orchestration, storage-backed drafts, archived run bundles, and bundle import flows are not the default path forward unless E-19 explicitly retains a surface.
**Consequences:** Planning docs must treat today's Sim orchestration as either current-surface residue or explicitly retained authoring support, not as the future programmable contract. New consumers of draft/catalog/bundle-ref surfaces should be avoided until E-19 inventory is complete. `docs/architecture/template-draft-model-run-bundle-boundary.md` is the terminology and ownership reference for this boundary.
**Subsequent update (2026-04-07):** The word "headless" in this decision is retained as historical text. The execution component is now named `FlowTime.TimeMachine` (the Time Machine) — see D-031. The scope of E-18 is unchanged; only the component name is updated.
