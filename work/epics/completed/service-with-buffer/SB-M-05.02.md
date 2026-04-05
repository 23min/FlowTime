# SB-M-05.02 — ServiceWithBuffer DSL Simplification & Queue Latency Semantics

**Status:** ✅ Completed  
**Epic:** Service With Buffer (`work/epics/completed/service-with-buffer/`)  
**Depends on:** SB‑M‑05.01 (public ServiceWithBuffer introduction)  
**Goal:** Let modelers declare ServiceWithBuffer stages directly in topology (no helper nodes) and improve queue‑latency semantics so scheduled drains surface “paused” status instead of nulls.

---

## Overview

SB‑M‑05.01 delivered `kind: serviceWithBuffer` but still requires modelers to define hidden helper nodes that produce the queue depth series (`queueDepth: picker_wave_backlog`) and leaves queue latency null whenever a dispatch schedule suppresses `served`. DB‑M‑05.02 removes both friction points:

1. **DSL simplification:** Topology nodes become authoritative. When a modeler sets `kind: serviceWithBuffer`, the loader synthesizes the underlying execution node and queue series automatically—no extra YAML, no hidden nodes. Existing canonical templates migrate to the simplified form.
2. **Queue latency semantics:** The engine annotates bins where `served = 0` but backlog > 0 as “Paused (gate closed)” so `/state`/UI/CLI tell operators *why* latency is undefined. Analyzer warnings shift from “latency could not be computed” to explicit advisory metadata.

---

## Functional Requirements

### FR1 — Implicit ServiceWithBuffer Nodes

- Schema updates (`docs/schemas/model.schema.*`, `docs/schemas/template.schema.json/md`):
  - Allow `queueDepth` to be omitted or set to `"self"` for `kind: serviceWithBuffer`.
  - Document that topology declarations can stand alone; the loader fills in execution details.
- Loader / validator:
  - Detect ServiceWithBuffer topology nodes lacking explicit helper nodes and synthesize the necessary execution node + queue series.
  - Reject legacy backlog nodes outright (no shims).
- Templates/examples:
  - Update every canonical template to use the implicit form.
  - Remove hidden helper definitions and metadata flags (`graph.hidden`).
- Tests:
  - Add unit coverage in `TemplateSchemaTests`, `ModelParser` tests, and a regression template verifying implicit ServiceWithBuffer behavior.

### FR2 — Queue Latency Semantics for Scheduled Gates

- Engine:
  - Extend state derivation so each node exposes a `queueLatencyStatus` (e.g., `running`, `paused_gate_closed`).
  - When latency is undefined because `served == 0`, emit the new status and keep `latencyMinutes` null.
- API / Contracts:
  - Add optional `QueueLatencyStatus` (and tooltip text) to `NodeMetrics`/`NodeSeries`.
  - Update goldens (`state*.json`) to include the new field for scheduled queues.
- UI / CLI:
  - Render “Paused (gate closed)” badges where latency is null but status indicates a closed gate.
  - Remove the prior informational warning banner from topology/CLI output.
- Analyzer:
  - Replace `latency_uncomputable_bins` with a softer advisory that references the new status.

### FR3 — Docs & Tracking

- Update `docs/templates/template-authoring.md`, `templates/README.md`, and the ServiceWithBuffer architecture note to describe the implicit DSL and new latency status.
- Capture the previously documented CL‑M‑04.03.x limitations (helper nodes + missing latency reason) and note how SB‑M‑05.02 resolves them.
- Log the change in `work/epics/completed/service-with-buffer/SB-M-05.02-log.md` (new tracking doc).
- Reference SB‑M‑05.02 in `work/epics/epic-roadmap.md` and `work/milestones/README.md`.

---

## Phases & Deliverables

1. **Phase 1 — Schema & Loader**
   - RED tests for implicit ServiceWithBuffer nodes.
   - Implement parser/validator changes and migrate canonical templates.
2. **Phase 2 — Queue Latency Semantics**
   - Extend engine/state builders, update contracts + goldens, adjust analyzer/CLI/UI.
3. **Phase 3 — Docs, Validation, Wrap**
   - Refresh authoring docs, add tracking notes, and run full `dotnet build` + `dotnet test --nologo`.

---

## Test Plan

- Schema/loader unit tests (`TemplateSchemaTests`, `ModelParserTests`).
- `flow-sim generate` for representative templates (transportation-basic, warehouse-picker-waves) to confirm implicit nodes run correctly.
- API golden updates (`tests/FlowTime.Api.Tests/Golden/*`) covering the new queue-latency status field.
- UI snapshot/interaction tests to ensure the paused badge and backlog chip behave consistently.
- Full regression suite: `dotnet build`, `dotnet test --nologo`.

---

## Tracking

- Create `work/epics/completed/service-with-buffer/SB-M-05.02-log.md` when implementation starts.
