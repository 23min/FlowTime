# Release SB-M-05.02 — ServiceWithBuffer DSL Simplification & Queue Latency Semantics

**Release Date:** 2025-11-28  
**Type:** Milestone delivery (schema+UI alignment)  
**Key Fixtures:** `templates/transportation-basic-classes.yaml`, `templates/warehouse-picker-waves.yaml`

## Overview

SB-M-05.02 finishes the ServiceWithBuffer rollout that began in SB-M-05.01. Template authors can now declare ServiceWithBuffer stages directly in the topology (`queueDepth: self` or any alias) without creating helper backlog nodes, and the runtime synthesizes the execution nodes automatically. The engine, API, CLI, and UI now annotate scheduled queues with a `queueLatencyStatus`, so paused dispatch gates surface as informative badges instead of opaque "latency unavailable" warnings. Transportation + warehouse examples were refreshed to the simplified DSL, documentation was updated across the authoring guide/architecture notes, and a refresh command was added so Sim/API pick up template edits without restarts.

## Key Changes

1. **Schema, Validator, and Loader**
   - `docs/schemas/model.schema.yaml` + `template.schema.json/md` now allow `queueDepth: self` (or omission) whenever `kind: serviceWithBuffer` is used in topology nodes, making those declarations authoritative.
   - `QueueNodeSynthesizer` (formerly `ServiceWithBufferNodeSynthesizer`) runs during parse time to create the hidden execution node + queue series whenever the topology lacks an explicit helper, and schema tests cover both success/failure paths.
   - `TemplateSchemaTests`/`TemplateParserTests` gained coverage for implicit queue depth plus dispatch schedule metadata to guard against regressions.
2. **Templates & Documentation**
   - Transportation (classes + base) and warehouse picker templates now describe ServiceWithBuffer nodes directly in their topology, eliminating dozens of helper nodes while keeping aliases/output ids intact.
   - `docs/templates/template-authoring.md`, `templates/README.md`, and `docs/architecture/service-with-buffer/...` document the implicit DSL, scheduled dispatch authoring, the new queue-latency status, and the refresh-templates workflow.
   - `docs/milestones/completed/SB-M-05.02.md`, the milestone tracker, and the architecture roadmap were updated so SB-M-05.02 is the canonical reference for these behaviors.
3. **Queue Latency Semantics & Surface Area**
   - Engine + contracts expose `queueLatencyStatus` via new DTOs (`QueueLatencyStatusDescriptor` on `NodeMetrics`/`NodeSeries`) while analyzer warnings now emit `queue_latency_gate_closed` instead of the generic "latency uncomputable" banner.
   - FlowTime.API, CLI, and UI propagate the metadata end-to-end: paused dispatch gates render a "Paused (gate closed)" badge on topology cards/sparklines, CLI suppresses duplicate warnings, and JS canvas chips use the same color palette as the run cards.
   - Docs highlight the new semantics so operators know paused queues are intentional rather than telemetry gaps.

## Tests

- `dotnet build`
- `dotnet test --nologo` *(all suites pass except the known perf benchmark `FlowTime.Tests.Performance.M2PerformanceTests.Test_PMF_Mixed_Workload_Performance`, which still exceeds the baseline and remains deferred to the epic-wide perf sweep)*

## Known Issues / Follow-ups

- Mixed workload perf benchmark remains above threshold (tracked separately; no regression introduced here).
- DLQ nodes still render via the legacy `kind: dlq` surface and therefore require explicit queue depth helpers; SB-M-05.03 will evaluate synthesizing those as well so DLQs can participate in the implicit DSL.

## Verification Artifacts

- `dotnet run --project src/FlowTime.Sim.Cli -- generate --id transportation-basic-classes --templates-dir templates`
- `dotnet run --project src/FlowTime.Sim.Cli -- generate --id warehouse-picker-waves --templates-dir templates`
- Manual UI verification: regenerated both runs, loaded the topology page, confirmed class chips + paused badges render with the implicit ServiceWithBuffer semantics.
