# Scenario Overlays and What-If Runs

**Status:** Proposed  
**Last Updated:** 2026-01-14

This document defines the **Scenario Overlays** epic: a first-class, reproducible way to create "what-if" runs by applying a small, validated patch to a baseline run without mutating it. The intent is to consolidate overlay concepts that currently live in historical docs (time-travel decision log, engine charter) into a single, up-to-date architecture reference.

## Motivation

FlowTime already produces deterministic, time-binned runs (telemetry and simulation). Users want to answer questions like:

- "What if I doubled parallelism?"
- "What if arrivals spike for 3 bins?"
- "What if capacity is capped during a shift?"

Today, answering those questions requires manual model edits or a separate run definition. Scenario overlays make this a **repeatable, auditable** workflow that stays within the canonical run artifact system.

## Definitions

- **Baseline run:** A canonical run bundle (model + series + metadata).
- **Overlay:** A minimal patch that overrides or augments baseline inputs (e.g., `parallelism`, arrivals series, capacity).
- **Derived run:** A new run artifact created by applying an overlay to a baseline run.
- **Overlay chain:** A derived run created from another derived run (overlay of overlay).

## Goals

1. **Reproducibility:** overlays produce deterministic, versioned derived runs.
2. **Auditability:** derived runs keep provenance linking back to the baseline and overlay definition.
3. **Minimal change surface:** overlays target *inputs* (arrivals/capacity/parallelism/schedules), not arbitrary outputs.
4. **Compatibility:** overlays work for both telemetry-backed and simulation-backed runs.
5. **Comparability:** API and UI can compare baseline vs overlay using the same `/state`, `/state_window`, and `/graph` shapes.

## Non-Goals

- No automatic control loops or autoscaling engine.
- No ML-driven anomaly detection logic (handled in a separate epic).
- No mutable in-place run edits; overlays always create new run artifacts.
- No attempt to represent every possible edit (keep overlay scope intentionally narrow).

## Architecture Overview

1. **Resolve baseline run**  
   Validate that the base run exists and is immutable.
2. **Validate overlay descriptor**  
   Ensure references are valid and values are within permitted ranges.
3. **Apply overlay**  
   Produce a derived model or derived series as needed.
4. **Generate derived run artifacts**  
   Persist a full run bundle with new `runId`, but link provenance to the baseline.
5. **Serve as normal**  
   Derived runs use existing `/state`, `/state_window`, and `/graph` APIs; no special UI logic is required to read them.

## Overlay Descriptor (Conceptual)

Overlay descriptors should remain **simple and explicit**. The format can be JSON or YAML; the key is the contract.

Recommended overlay targets:

- **Scalars:** `parallelism`, `capacity`, `maxAttempts`
- **Series overrides:** `arrivals`, `served`, `errors`, `queueDepth`, `capacity` (for telemetry-backed overlays)
- **Schedule adjustments:** `dispatchSchedule` parameters (period, phase, capacitySeries)

Overlay rules:

- Overlays must **not** redefine topology structure (no new nodes or edges).
- Overlays must not mutate baseline artifacts; they yield a **new derived run**.
- Overlay patches are **applied last**; base model + telemetry inputs remain the default.

## Determinism and Provenance

Derived runs must preserve deterministic IDs and provenance metadata:

- `runId` derived from `{baseRunId, overlayHash}` (or explicit user override).
- Provenance includes base run reference and overlay descriptor hash.
- The UI should be able to show "Derived from run X with overlay Y".

## Validation Rules

Overlay validation should be strict to avoid misleading what-if results:

- **Shape checks:** overlay series length matches baseline window length.
- **Type checks:** numeric vs percent units enforced where applicable.
- **Domain checks:** `parallelism >= 1`, `capacity >= 0`, no negative arrivals.
- **Scope checks:** overlay does not introduce unknown node ids or series keys.

## API Surface (Conceptual)

The API can expose overlays as a run orchestration variant:

- `POST /runs/{baseRunId}/overlays` -> returns a new runId
- `GET /runs/{runId}` -> includes `baseRunId` and overlay metadata
- `GET /runs/{runId}/overlay` -> returns the overlay descriptor (if present)

The key principle: derived runs **look like normal runs** to the rest of the system.

## UI & UX Implications

Overlays enable a natural "scenario" experience:

- A "What-if" panel can tweak inputs (parallelism, capacity, arrivals).
- UI creates a derived run and optionally pins a baseline for side-by-side comparison.
- Inspector tooltips should surface overlay provenance when present.

This epic does **not** require a new chart type; it reuses the existing time-travel UI with a comparison mode layered on top.

## Relationship to Flow-Aware Anomaly & Pathology Detection

Scenario overlays are a natural companion to anomaly detection:

- Anomaly detection says "capacity is insufficient."
- Overlays let the user test "what if we add 2 workers?"

This pairing is why overlays belong in the bridge-work layer after E-16 and the resumed p3c/p3b analytical work, and before E-17-style live interaction. They should provide the derived-run and comparison contract that later UI/session work consumes rather than re-inventing.

## Recommended Sequencing

- Land after E-16 stabilizes authoritative state/graph facts and run provenance surfaces.
- Prefer after resumed Phase 3 p3c + p3b so variability- and WIP-aware experiments use the same canonical overlay contract.
- Use overlays as bridge work before E-17 interactive sessions; live controls should call into derived-run/comparison semantics rather than define a second what-if model.

## Milestone Decomposition (Suggested)

**Phase 1: Overlay Contracts + Artifacts**
- Define overlay schema + validation rules.
- Implement derived run creation and provenance.
- Add API endpoints.

**Phase 2: Overlay Comparison**
- Baseline vs overlay diff API (optional).
- UI compare mode with aligned bins and legends.

**Phase 3: UX and Guardrails**
- What-if panel, parameter presets, and guardrails.
- Documentation and user guidance.

## Open Questions

- Should telemetry overlays allow served/queue overrides, or only arrivals/capacity?
- How to prevent "overlapping overlays" from hiding base data issues?
- Should overlay chains be allowed, or flattened into a single derived run?
- How should overlays impact reproducibility across schema versions?

---

**Canonical references:** the overlay ideas in the time-travel decision log and the engine charter are now considered historical context. This document is the current source of truth for the overlays epic.
