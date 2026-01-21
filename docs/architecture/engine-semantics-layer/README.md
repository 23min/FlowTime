# Engine Semantics Layer

**Status:** Proposed (core semantics are shipped; this epic formalizes the pipeline role)

## Purpose

Define FlowTime Engine as the **semantics layer** that converts canonical run bundles into consistent stateful flow views. The goal is to make the engine a reusable contract for UIs and BI tools, not just the backend for the current UI.

## Why This Matters

- **Single source of meaning**: derived metrics and warnings live in one place, so the UI and BI tools do not reimplement formulas or interpretation rules.
- **Stable contracts across sources**: whether data comes from simulation or telemetry replay, the same `/state` and `/state_window` semantics apply.
- **Trust and validation**: invariants (conservation, queue depth mismatch, retry governance) are enforced centrally and reported in a consistent way.
- **Change isolation**: ingestion pipelines can evolve as long as they emit canonical bundles; consumers stay stable.
- **Parity readiness**: the telemetry loop can compare outputs because the engine provides a single, deterministic interpretation layer.

## Example: Bundle Input to Stateful View

**Input bundle (canonical artifacts):**
- `model.yaml`
- `series/index.json`
- Per-bin CSV series such as arrivals, served, capacity, queue depth
- `manifest.json` and `run.json` metadata

**Engine outputs (stable contracts):**
- Derived series such as utilization, queue latency, flow latency, service time
- Warnings when inputs are missing or inconsistent, instead of silent fallbacks
- `/state` and `/state_window` responses that any consumer can rely on

**Consumer impact:**
- The UI renders the engine-derived series directly.
- BI tools and analyzers use the same series without duplicating business logic.

## Scope

### In Scope
- Derived metrics (utilization, queue latency, flow latency, service time).
- Validation and invariants (conservation, queue depth mismatch, retry governance).
- Stable output contracts (`/state`, `/state_window`, `/graph`, `/metrics`).
- Schema discipline and golden regression coverage.

### Out of Scope
- Telemetry ingestion or loader services (see Telemetry Ingestion epic).
- Telemetry capture UX and replay workflow details.
- Overlay runs or what-if scenarios (see Overlays epic).
- Registry, import/export, or catalog services.

## Current State (2026-01)

- Engine already emits stateful outputs and warnings for both simulation and telemetry-mode bundles.
- Contracts are defined in `docs/schemas/` and validated by API/UI tests.
- The engine is still treated primarily as a UI backend, not a pipeline stage with explicit SLA and external consumers.

## Inputs (Canonical Bundles)

The engine consumes **canonical run bundles**:
- `model.yaml` and topology semantics (KISS schema).
- Series CSVs and `series/index.json`.
- `manifest.json` and `run.json` metadata.

Bundle creation is handled elsewhere (telemetry ingestion or capture). This epic defines how the engine interprets bundles, not how they are produced.

## Outputs (Contracts)

Engine outputs are stable, versioned contracts:
- `/v1/runs/{id}/state`
- `/v1/runs/{id}/state_window`
- `/v1/runs/{id}/graph`
- `/v1/runs/{id}/metrics`

These responses must remain compatible across data sources (simulation, telemetry, synthetic capture).

## Semantics Catalog

The authoritative series and warning semantics are documented here:
- `docs/architecture/engine-semantics-layer/semantics-catalog.md`

This catalog defines each node/edge series (units, origin, aggregation, gating) and the warning families emitted by the engine. Consumers must treat these outputs as authoritative and avoid client-side derivation.

## Validation and Semantics

- Invariant warnings are first-class outputs (persisted in `run.json` and state responses).
- Retry and terminal-edge governance is enforced at the engine layer.
- Missing inputs yield explicit nulls and warnings rather than silent fallbacks.

## Dependencies and Related Epics

- **Telemetry Ingestion and Canonical Bundles** (how bundles are produced).
- **Telemetry Loop and Parity** (how synthetic and telemetry runs are compared).
- **Edge Time Bins / Edge Metrics** (future expansion of semantics).
- **Scenario Overlays** (derived runs that reuse engine semantics).

## Suggested Milestone Decomposition

1. **Contract hardening**: tighten schema enforcement, golden tests, and doc alignment.
2. **External consumption**: clarify BI/analytics contract expectations and export surfaces.
3. **Edge-aware semantics**: integrate EdgeTimeBin once available.

## References

- `docs/reference/engine-capabilities.md`
- `docs/architecture/run-provenance.md`
- `docs/architecture/time-travel/time-travel-architecture-ch2-data-contracts.md`
