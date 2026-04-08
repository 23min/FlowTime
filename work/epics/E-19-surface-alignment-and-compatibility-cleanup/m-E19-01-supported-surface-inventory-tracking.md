# Tracking: m-E19-01 Supported Surface Inventory, Boundary ADR & Exit Criteria

**Status:** completed
**Epic:** [E-19 Surface Alignment & Compatibility Cleanup](./spec.md)
**Milestone spec:** [m-E19-01-supported-surface-inventory.md](./m-E19-01-supported-surface-inventory.md)

## Acceptance Criteria

- [x] AC1. Extended the boundary ADR in [docs/architecture/template-draft-model-run-bundle-boundary.md](../../../docs/architecture/template-draft-model-run-bundle-boundary.md) with responsibility clarification, current/transitional/target diagrams, and the client-agnostic validation principle.
- [x] AC2. Published the supported-surface matrix in [docs/architecture/supported-surfaces.md](../../../docs/architecture/supported-surfaces.md), including shared framing and the Blazor/Svelte support policy.
- [x] AC3. Completed the route, UI call-site, public-contract, schema, template, example, and current-doc sweeps recorded in the matrix and appendices.
- [x] AC4. Cited A1-A6 decisions from the milestone spec in the matrix instead of rearguing them row by row.
- [x] AC5. Updated [work/decisions.md](../../decisions.md) with short entries for the shared framing, A1-A5, and the Blazor/Svelte support policy. A6 and Time Machine naming were already recorded and remain referenced.
- [x] AC6. Kept the E-18 spec content aligned to Time Machine naming and tiered-validation scope, then synced broader roadmap/current-work surfaces in the same pass.
- [x] AC7. Updated the [CLAUDE.md](../../../CLAUDE.md) Current Work section to show m-E19-01 complete and m-E19-02 next.
- [x] AC8. Reconciled E-19 status surfaces in [work/epics/E-19-surface-alignment-and-compatibility-cleanup/spec.md](./spec.md), [ROADMAP.md](../../../ROADMAP.md), and [work/epics/epic-roadmap.md](../../epic-roadmap.md).
- [x] AC9. This tracking document now exists and records milestone completion.
- [x] AC10. No runtime code, DTO, schema, template, example, or endpoint was deleted as part of m-E19-01; only planning and status artifacts were changed.

## Notes

- Runtime deletions and client cleanup remain owned by m-E19-02.
- Schema, template, example, and stale current-doc cleanup remain owned by m-E19-03.
- Blazor stale-wrapper removal and shared-contract cleanup remain owned by m-E19-04.
- E-18 owns the Time Machine replacement surface and the eventual deletion-by-default of Sim orchestration endpoints.
