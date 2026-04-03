# Milestone: Phase 3a.1 — Analytical Projection Hardening

**ID:** m-ec-p3a1
**Epic:** Engine Correctness & Analytical Primitives
**Status:** draft

## Goal

Harden the analytical state projection and contract surfaces introduced in Phase 3a so derived metrics are emitted consistently, honestly, and safely across snapshot, window, metadata, and UI contracts. This answers: "Can downstream consumers trust analytical outputs as first-class API surface, not just math helpers?"

## Context

Phase 3a introduced the right engine primitive: `CycleTimeComputer` now computes `queueTimeMs`, `serviceTimeMs`, `cycleTimeMs`, and `flowEfficiency` in Core. Review of the p3a implementation showed that the remaining risk is not the engine math itself; it is the projection layer in `StateQueryService` and the contract surfaces around it.

The same queue/service capability decision is still made in several places across snapshot, window, flow-latency composition, queue latency status, warning emission, and metadata. That duplication caused drift during p3a verification:
- logicalType-resolved `serviceWithBuffer` behavior can diverge from explicit `kind: serviceWithBuffer`
- metadata can advertise analytical series that are not actually emitted
- per-class analytical fields are not fully mirrored through the UI DTO layer
- non-finite analytical values are not explicitly hardened before serialization
- AC-5 warning applicability and tolerance policy are still underspecified in code

These are not new analytical primitives. They are correctness and contract hardening for the analytical layer, and they should be resolved before p3b/p3c/p3d add more derived metrics on top of the same projection surface.

## Acceptance Criteria

1. **AC-1: Analytical capability parity across node forms.** Explicit `kind: serviceWithBuffer` nodes and logicalType-resolved `serviceWithBuffer` nodes behave identically anywhere analytical queue/service semantics are exposed. Snapshot and window responses stay aligned for:
   - `latencyMinutes`
   - `queueLatencyStatus`
   - `queueTimeMs`
   - `serviceTimeMs`
   - `cycleTimeMs`
   - `flowLatencyMs`
   - `flowEfficiency`
   - stationarity warning eligibility

2. **AC-2: Honest analytical metadata and contract symmetry.** `seriesMetadata` only advertises derived keys that are actually emitted in the payload. Service-only nodes do not advertise `queueTimeMs`; queue-only nodes do not advertise `serviceTimeMs` or `flowEfficiency`. Per-class analytical fields are exposed end-to-end through `ClassMetrics`, snapshot payloads, and `TimeTravelClassMetricsDto`.

3. **AC-3: Finite numeric safety for analytical metrics.** `queueTimeMs`, `serviceTimeMs`, `cycleTimeMs`, and `flowEfficiency` follow the Phase 1 Tier 2 null policy for invalid or non-finite inputs/results. No `NaN` or `Infinity` reaches serialized API output in snapshot or window responses.

4. **AC-4: Stationarity warning policy is explicit and applicable.** `littles-law-non-stationary` is emitted only when `queueTimeMs` is actually being estimated via Little's Law for that node and window. The tolerance source is explicit and overridable through one named configuration point or options value, with a default of 25%.

5. **AC-5: Targeted tests prove the negative cases.** Core and API tests cover:
   - logicalType-resolved `serviceWithBuffer` parity
   - pure-service nodes lacking `queueTimeMs` metadata
   - queue-like nodes missing required inputs and therefore lacking stationarity warnings
   - per-class DTO parity for analytical fields
   - `NaN`/`Infinity` analytical inputs returning null rather than leaking through the API

6. **AC-6: Tests and gate.** `dotnet build` is green. Targeted Core and API suites for this milestone are green. No new failures are introduced; the same pre-existing schema meta-resolution failures remain excluded from the gate unless they are separately fixed.

## Technical Notes

- Prefer a small internal helper or record, e.g. `AnalyticalCapabilities`, resolved once per node from `kind`, `logicalType`, and available inputs. Use that in snapshot, window, flow-latency, metadata, and warning generation instead of repeating ad hoc predicates.
- Keep the graph-level analytical primitive in Core. This milestone hardens the API/state adapter layer rather than moving the full projection pipeline into Core.
- If AC-4 configurability does not justify full application configuration yet, a scoped options object in the state-query path is acceptable as long as the tolerance source is explicit and testable.
- Add assertion-style tests for the new negative cases before updating golden snapshots. Snapshot diffs should confirm behavior, not be the only proof of behavior.

## Out of Scope

- New analytical primitives (WIP limits, variability, constraint enforcement, bottleneck ranking)
- Schema/meta-schema repair in `StateResponseSchemaTests`
- Moving the full state projection architecture out of `StateQueryService`
- UI feature work beyond contract/DTO parity

## Dependencies

- Phase 1 complete ✅
- Phase 3a implementation exists and is the input to this hardening pass
- This milestone is a gate before Phase 3 continues with p3b/p3c/p3d

## References

- `work/milestones/m-ec-p3a-review.md`
- `work/milestones/m-ec-p3a-review-codex.md`
- `work/milestones/m-ec-p3a-cycle-time.md`
