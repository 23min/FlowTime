# SB-M-05.01 — ServiceWithBuffer Node Type (Breaking Introduction)

**Status:** ✅ Completed  
**Epic:** Service With Buffer (architecture epic under `docs/service-with-buffer/`)  
**Depends on:** CL‑M‑04.03.02 (Scheduled Dispatch & Flow Control Primitives)  
**Target:** Introduce `kind: serviceWithBuffer` as the canonical node type for services with explicit queues/buffers, remove `kind: backlog` from the public surface, and ensure the engine and UI treat ServiceWithBuffer as an operational node with a queue badge.

---

## Overview

CL‑M‑04.03.02 introduces scheduled dispatch semantics and expression primitives that support cadence-based flows. SB‑M‑05.01 builds on that by making **ServiceWithBuffer** a first-class node type and eliminating backlog from the public contract.

SB‑M‑05.01 does **not** aim for backward compatibility:

- Templates, docs, and UI must be updated to use `kind: serviceWithBuffer` (or corresponding topology concepts) instead of `kind: backlog`.
- Where helpful during implementation, temporary loader shims may support `backlog`, but the final state of this milestone is **forward-only**: new models and tooling should rely solely on ServiceWithBuffer.

---

## Requirements

### FR1 — Schema: Introduce ServiceWithBuffer, Remove Backlog

- Update `docs/schemas/model.schema.yaml`:
  - Define `kind: serviceWithBuffer` as the **only** node kind representing a service that owns a queue/buffer.
  - Remove `kind: backlog` from the public schema definition.
  - Ensure the ServiceWithBuffer schema includes:
    - Service semantics (arrivals, served, errors, capacity, routing).
    - Queue semantics (named backlog/queueDepth series, optional loss series).
    - Schedule semantics (`dispatchSchedule`, reusing the CL‑M‑04.03.02 design).
- Engine changes:
  - Map `kind: serviceWithBuffer` from templates into the existing backlog execution machinery, refactoring type names where it improves clarity.
  - Remove backlog as a publicly addressable node kind in template loading; any internal adapters that still mention backlog must be explicitly temporary and not part of the exposed contract.
- Tests:
  - Add tests under `tests/FlowTime.Core.Tests` (or appropriate schema/loader tests) verifying that ServiceWithBuffer nodes:
    - Load successfully from the new schema shape.
    - Produce the same numerical behavior as the pre‑SB‑M‑05.01 backlog-based templates when those templates are mechanically migrated.

### FR2 — Architecture & Authoring Docs

- Architecture docs:
  - Finalize and reference `docs/service-with-buffer/service-with-buffer-architecture.md` from:
    - `docs/architecture/whitepaper.md` (high-level node taxonomy section).
    - `docs/reference/engine-capabilities.md` (node kinds overview).
- Template authoring docs:
  - Update `docs/templates/template-authoring.md` to:
    - Rename or reframe the "Backlog nodes" section as "Services with buffers".
    - Explain that `kind: backlog` is the legacy spelling; `kind: serviceWithBuffer` is preferred for new templates.
    - Clarify the separation of concerns:
      - Queue visuals (`kind: queue` in topology) vs. ServiceWithBuffer behavior nodes.
-- Examples:
  - Update canonical templates (e.g., `templates/transportation-basic-classes.yaml`, `templates/warehouse-picker-waves.yaml`) so that any node previously modeled as a backlog is now modeled as `kind: serviceWithBuffer`.
  - Remove or rewrite any explicit backlog examples.

### FR3 — UI & API Surface Alignment

- `/graph` and `/state_window`:
  - Ensure these responses expose a stable logical type for backlog/serviceWithBuffer nodes, e.g.:
    - `nodeLogicalType: "serviceWithBuffer"`.
  - Preserve any existing fields relied upon by the UI for node kind; do not break current consumers.
- UI topology rendering (`src/FlowTime.UI`):
  - Treat ServiceWithBuffer nodes as **operational service nodes**:
    - Same base visual language as existing `service` nodes.
    - Add a small isosceles-trapezoid **queue badge** (reusing the visual metaphor currently used for queues) on the node to indicate explicit buffering.
  - When `dispatchSchedule` metadata is present (from CL‑M‑04.03.02):
    - Render an appropriate chip or icon (e.g., "Dispatch every 6 bins").
    - Make clear in tooltips that this is a **service schedule**, not a property of the queue visual.

### FR4 — Analyzer & CLI Wording

- Analyzer adjustments (`FlowTime.Generator` / `FlowTime.Sim.Core` analyzers, as applicable):
  - Update user-facing messages to use "service with buffer" terminology exclusively, for example:
    - "Service with buffer `X` has a dispatch schedule that never fires" rather than "Backlog node `X` never dispatches".
  - Update analyzer logic so it recognizes ServiceWithBuffer as the owner of queue and schedule semantics (no special casing for backlog).
- CLI (`FlowTime.Sim.Cli`):
  - For `flow-sim generate --verbose`, when describing nodes backed by backlog/serviceWithBuffer:
    - Print a short summary including buffer name, dispatch schedule (if present), and capacity.
    - Use "Service with buffer" language in the textual summary.

### FR5 — Guardrails

- This milestone is allowed to be **breaking** with respect to:
  - Schema kinds (removal of `backlog` in favor of `serviceWithBuffer`).
  - Template YAML that references `backlog`.
  - UI assumptions about backlog vs service nodes.
- It is **not** a numerical behavior change milestone:
  - Existing golden runs that are ported mechanically from backlog to ServiceWithBuffer should produce equivalent time series.
  - Any intentional numerical changes (if discovered necessary) must be explicitly documented and justified.

---

## Phases & Deliverables

1. **Phase 1 — Schema & Engine (RED→GREEN):**
  - Introduce `kind: serviceWithBuffer` in the schema and remove `kind: backlog` from the public contract.
  - Wire engine node-kind resolution to treat ServiceWithBuffer as the owner of queue + schedule semantics.
  - Add tests confirming ServiceWithBuffer-based templates match the numerical behavior of their backlog-based predecessors.

2. **Phase 2 — Docs & Authoring Alignment:**
   - Update architecture docs and link the epic doc into `whitepaper` / `engine-capabilities`.
   - Revise template authoring docs to introduce "Service with buffer" as the primary concept.
   - Add at least one documented template example using `serviceWithBuffer`.

3. **Phase 3 — UI & Analyzer/CLI Wording:**
   - Adjust `/graph` / `/state_window` outputs as needed (non-breaking additions only).
   - Update UI to render backlog/serviceWithBuffer nodes as primary service nodes with buffer chips.
   - Refresh analyzer messages and CLI verbose output to prefer "service with buffer" language.

Each phase follows FlowTime milestone guardrails (TDD where applicable, minimal scope creep, full `dotnet build` + `dotnet test` before hand-off). Any behavior-affecting changes discovered during implementation must either be rolled back or explicitly documented and evaluated.

---

## Test Plan

-- **Schema & Engine:**
  - Add schema tests ensuring `kind: serviceWithBuffer` validates for the intended node shape and that `kind: backlog` is no longer accepted in the public schema.
  - Add engine-level tests (or reuse existing backlog tests via mechanical migration) to demonstrate that simulations using ServiceWithBuffer produce the same metrics as the previous backlog-based tests.

- **Docs & Examples:**
  - Run existing doc-driven or sample tests (if any) against both legacy and new spelling.

- **UI:**
  - Manual verification in the UI that nodes with backlog/serviceWithBuffer:
    - Render as services with buffer badges.
    - Show schedule chips/tooltips when `dispatchSchedule` is present.

- **Analyzer & CLI:**
  - Unit/integration tests for analyzer messages touching backlog/serviceWithBuffer nodes.
  - CLI snapshot tests (where available) for `flow-sim generate --verbose` output containing the new wording.

-- **Regression:**
  - Full `dotnet test --nologo` across the solution.
  - Re-run a small set of golden templates that have been migrated from backlog to ServiceWithBuffer to confirm no numerical differences.

---

## Implementation Notes (running log)

| Date | Area | Notes |
| --- | --- | --- |
| 2025-11-27 | Schema/tests | Added TemplateSchema coverage to ensure `kind: serviceWithBuffer` validates and `kind: backlog` is rejected. Updated `docs/schemas/model.schema.(yaml\|md)` + `template.schema.json/md`. |
| 2025-11-27 | Engine | Renamed `BacklogNode` → `ServiceWithBufferNode`, normalized `ModelParser` kind handling, and wired TemplateValidator/SimModelBuilder/ClassContributionBuilder to the new spelling. `dotnet test --nologo tests/FlowTime.Core.Tests/FlowTime.Core.Tests.csproj` passes. |
| 2025-11-27 | CLI/analyzers | `flow-sim generate` succeeds for `transportation-basic` and `warehouse-picker-waves` post-migration (warehouse still reports the known bursty warnings tracked from earlier milestones). `TemplateInvariantAnalyzerTests` pass under `dotnet test --nologo --filter TemplateInvariantAnalyzerTests`. |
| 2025-11-27 | Docs | Updated `docs/architecture/whitepaper.md`, `docs/reference/engine-capabilities.md`, and `docs/templates/template-authoring.md` to reference ServiceWithBuffer terminology, plus refreshed `templates/README.md` snippets. |
| 2025-11-28 | API/UI/CLI | `/graph`, `/state`, and `/state_window` now emit `nodeLogicalType` + dispatch schedules for ServiceWithBuffer nodes (contracts + DTOs updated). `FlowTime.UI` consumes the new metadata so service-with-buffer nodes render like services with queue badges + schedule chips (JS canvas + inspector updated). Analyzer/CLI fixtures were refreshed to keep messaging aligned, and API/UI/CLI tests updated. |

### 2025-11-27 - Template migration validation

**Templates regenerated:**
- transportation-basic
- warehouse-picker-waves (expected bursty warnings retained)

**Commands:**
```
dotnet run --project src/FlowTime.Sim.Cli -- generate --id transportation-basic --templates-dir templates --mode simulation --out data/run_sb_m_0501_transportation.yaml
dotnet run --project src/FlowTime.Sim.Cli -- generate --id warehouse-picker-waves --templates-dir templates --mode simulation --out data/run_sb_m_0501_warehouse.yaml
```

**Warnings:**
- Warehouse template still emits the known informational warnings (queue latency during zero served bins, served vs arrivals spikes) documented in CL-M-04.03.x; carry forward to SB-M-05.01 release notes.

## Known Gaps / Follow-Up

The current milestone intentionally left two technical gaps that are now the focus of `SB-M-05.02`:

- **DSL simplification:** ServiceWithBuffer topology nodes still require explicit helper nodes (`queueDepth` series) to participate in execution. SB‑M‑05.02 allows a topology-only declaration (implicit queueDepth) so modelers never author hidden helpers.
- **Queue latency semantics for gated services:** When a dispatch schedule holds backlog (served = 0, depth > 0) the engine/UI expose `latencyMinutes = null`. SB‑M‑05.02 introduces explicit “paused” semantics so operators see why latency is undefined instead of a bare null.

See `work/milestones/completed/SB-M-05.02.md` for the full spec.
