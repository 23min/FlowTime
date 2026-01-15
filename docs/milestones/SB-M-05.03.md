# SB-M-05.03 — Queue-Like DSL Parity & DLQ Synthesizer

**Status:** ✅ Complete  
**Epic:** Service With Buffer (`docs/architecture/service-with-buffer/`)  
**Depends on:** SB‑M‑05.02 (implicit ServiceWithBuffer nodes + queue latency status)  
**Goal:** Extend the implicit ServiceWithBuffer synthesizer to every queue-like semantic (visual queue shells, DLQs, and service-owned buffers) so templates never carry helper backlog nodes, and align analyzers/UI/CLI around the unified DSL.

---

## Overview

SB‑M‑05.02 let topology-level `kind: serviceWithBuffer` nodes stand alone by synthesizing the backing execution node when `queueDepth` was omitted or set to `self`. Canonical templates still carry a mixture of explicit helper nodes in three places:

1. **Visual queue shells:** `kind: queue` nodes that only exist for topology styling still reference bespoke `*_queue_depth` series declared under `nodes:`. They need the same implicit series behavior as ServiceWithBuffer nodes so we do not hand-author level series.
2. **DLQs / terminal buffers:** DLQ nodes reference helper depth series (`airport_dlq_depth`, etc.) because the synthesizer does not run for `kind: dlq` even though the runtime treats them like queues. This prevents authors from relying on the DSL for terminal buffers.
3. **Legacy helper outputs:** Templates still export queue CSVs by referring to the helper node IDs, so removing those nodes requires synthesized aliases/outputs.

This milestone unifies the DSL so anything that exposes queue semantics—ServiceWithBuffer nodes, queue visuals, DLQs—can declare `queueDepth: self` (or omit it) and the loader fills in the backing node + outputs. The work also standardizes analyzer messages and UI badges so queued nodes behave identically regardless of their logical type.

---

## Functional Requirements

### FR1 — Schema & Validator Parity
- Schema (`docs/schemas/model.schema.yaml` + template schema docs) must explicitly state that `queueDepth` is optional/`self` for `kind: queue`, `kind: dlq`, and `kind: serviceWithBuffer` semantics.
- Validator must allow those kinds to omit helper nodes while still rejecting dangling references for other kinds.
- Document that topology nodes can optionally request named queue series for CSV/export (`queueDepth: hub_queue_depth`) even if the helper node is synthesized.

### FR2 — Synthesizer Expansion & Back-Compat Safeguards
- Extend `ServiceWithBufferNodeSynthesizer` (rename to `QueueNodeSynthesizer`) so it:
  - Creates backing nodes for any queue-like topology node lacking a concrete node definition (queue, dlq, serviceWithBuffer).
  - Recognizes existing helper nodes and avoids duplicating series (author-provided overrides still work).
  - Emits deterministic IDs when the author leaves `queueDepth` blank to keep outputs stable.
- Provide guardrails (warnings/errors) when templates mix implicit + explicit queue definitions for the same node to avoid double-counting.

### FR3 — Analyzer, CLI, and UI Alignment
- Analyzer rules should reference the normalized logical type instead of raw kind (`queue_latency_gate_closed`, conservation checks, DLQ attrition) so warnings stay consistent after helper nodes disappear.
- CLI + UI surfaces must treat synthesized DLQs/queue visuals exactly like today: badges, warning filters, and download links should derive from the logical type, not from helper node IDs.
- Add regression coverage that a topology node using `queueDepth: self` still emits queue CSVs, queue sparkline chips, and paused badges without manual wiring.

### FR4 — Docs & Templates
- Update `docs/templates/template-authoring.md`, `templates/README.md`, and the ServiceWithBuffer architecture note to describe the expanded synthesizer, the acceptable values for `queueDepth`, and how DLQ nodes behave.
- Refresh canonical templates (transportation-basic, transportation-basic-classes, warehouse-picker-waves, supply-chain variants) to remove any remaining helper backlog nodes, ensuring outputs rely on the synthesized IDs.
- Document the migration guidance (what authors delete, how to rename outputs) plus known analyzer expectations.

### Router Follow-up (Documented Gap)
- Routers still rely on explicit helper expressions (e.g., `returns_router_arrivals`, `returns_router_served`). Capture the work required to make topology-level `kind: router` nodes self-contained (implicit synthesis for their metrics, hidden helpers, and analyzer/UI coverage).
- The router gap is tracked as a follow-on milestone (`SB-M-05.03-router` placeholder) so we can spec/implement implicit routers similar to ServiceWithBuffer. This milestone documents the requirement but defers implementation.

---

## Phases & Deliverables

1. **Phase 1 — Schema & Validator Updates**
   - RED tests that queue/dlq topology nodes accept `queueDepth: self` and that helper-less DLQs fail prior to the change.
   - Update schema docs + validators and make them reject mixed implicit/explicit duplicates.
2. **Phase 2 — Synthesizer & Runtime Alignment**
   - Extend the synthesizer + loader; add regression tests that templates lacking helper nodes still parse and produce the same graph.
   - Update analyzer/CLI/UI logic to rely on logical types and ensure CSV/export plumbing finds synthesized series.
3. **Phase 3 — Template Migration & Docs**
   - Sweep canonical templates to remove helper backlog nodes and rerun analyzers.
   - Update docs, milestone tracker, and release notes; run full `dotnet build` + `dotnet test --nologo`.
   - Document the router gap and queue-heavy templates that still rely on backlog series; reference SB-M-05.03-router follow-up.

---

## Test Plan

- **Schema/Validator:** `TemplateSchemaTests` and new negative/positive cases for queue + DLQ nodes with `queueDepth: self/omitted`.
- **Synthesizer:** Dedicated tests in `TemplateParserTests` verifying synthesis for queue/dlq/serviceWithBuffer nodes, deterministic naming, and co-existence with explicit overrides.
- **Templates:** `TemplateBundleValidationTests` and router/dispatch regression tests updated to the implicit DSL.
- **Analyzer/UI:** `StateEndpointTests` plus UI render tests covering DLQ/queue badges without helper nodes.
- **Full Regression:** `dotnet build`, `dotnet test --nologo`; manual verification on transportation-basic(-classes) and warehouse picker templates to confirm queue latency badges + outputs behave unchanged.

---

## Tracking

- Create `docs/milestones/tracking/SB-M-05.03-tracking.md` when work begins and log RED→GREEN steps per phase.
