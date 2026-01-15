# Telemetry Ingestion and Canonical Bundles

**Status:** Proposed (capture is shipped; ingestion service is not)

## Purpose

Define how raw telemetry is transformed into **canonical FlowTime bundles** that the engine can consume. This epic owns ingestion, validation, and bundle assembly. It does not define engine semantics or UI behavior.

## Scope

### In Scope
- TelemetryLoader or equivalent ingestion service.
- Mapping rules from raw telemetry to canonical node series.
- Manifest generation and data quality warnings.
- Class coverage metadata and gap-fill policy.

### Out of Scope
- Engine semantic derivations (utilization, latency, retry governance).
- Overlay scenarios and derived runs.
- UI workflows beyond basic availability signals.

## Current State (2026-01)

- **Capture exists**: API `/telemetry/captures` and CLI `flowtime telemetry capture` can generate telemetry bundles from existing runs.
- **Bundle contract exists**: `docs/schemas/telemetry-manifest.schema.json` defines the canonical manifest.
- **TelemetryLoader service** (ADX/KQL or lake ingestion) is not implemented.
- The UI replay flow is documented but not consolidated across all docs.

## Canonical Bundle Contract

Bundles consist of:
- `model.yaml`
- `manifest.json` (window, grid, files, warnings)
- `series/index.json`
- CSV series files

Ingestion rules should ensure:
- Stable series naming and units.
- Consistent bin alignment and gap handling.
- Explicit warnings for missing or low-quality data.

## Design Principles

- **Deterministic outputs**: same inputs yield same bundle hashes.
- **Schema-first**: ingestion must validate against published schemas.
- **No semantics**: ingestion is responsible for aggregation and alignment, not derived metrics.

## Dependencies

- **Engine semantics layer**: ingestion outputs must align with canonical series semantics and bundle schemas to keep `/state` contracts stable across sources.

## Suggested Milestone Decomposition

1. **TelemetryLoader v1**: ingest from a single source (ADX or parquet) with strict schema validation.
2. **Data quality rules**: gap detection, zero-fill policies, class coverage metadata.
3. **Operational tooling**: CLI/SDK wrappers, run provenance, ingestion diagnostics.

## References

- `docs/architecture/time-travel/telemetry-generation-explicit.md`
- `docs/reference/engine-capabilities.md`
- `docs/schemas/telemetry-manifest.schema.json`
