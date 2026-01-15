# Telemetry Loop and Parity

**Status:** Proposed (concept documented; automation missing)

## Purpose

Ensure the **telemetry loop** is a measurable contract, not just a narrative: synthetic (model) runs should match telemetry replay runs within defined tolerances. This epic defines parity checks, comparison tooling, and acceptance criteria for telemetry ingestion pipelines.

## Definition of the Loop

1. **Model and Sim** produce a baseline run.
2. **Telemetry capture** generates a bundle from that baseline.
3. **Telemetry replay** creates a new run from the bundle.
4. Outputs from steps 1 and 3 are compared for parity.

## Scope

### In Scope
- Parity criteria for `/state` and `/state_window` outputs.
- Diff tooling and automated checks.
- Tolerance rules and drift classification.
- Reporting artifacts for regressions.

### Out of Scope
- Telemetry ingestion implementation details (Telemetry Ingestion epic).
- Engine semantic derivations (Engine Semantics Layer epic).
- UI overlay scenarios.

## Current State (2026-01)

- The loop is documented in time-travel architecture and the engine semantics layer proposal.
- Telemetry capture is available via CLI/API.
- Parity validation is manual or fixture-based; no automated parity harness exists.

## Parity Principles

- **Contract-first**: use published schemas and stable series keys.
- **Tolerance-aware**: allow small numerical drift where aggregation or rounding is expected.
- **Traceable**: link diffs to the exact run, bundle, and ingestion version.

## Suggested Milestone Decomposition

1. **Parity harness v1**: diff tool that compares baseline vs telemetry replay.
2. **CI gating**: automated parity checks for synthetic fixtures.
3. **Operational dashboards**: summarize drift trends and ingestion regressions.

## Dependencies

- Telemetry Ingestion and Canonical Bundles.
- Engine Semantics Layer.

## References

- `docs/architecture/time-travel/time-travel-architecture-ch6-decision-log.md`
- `docs/architecture/time-travel/time-travel-architecture-ch1-overview.md`
