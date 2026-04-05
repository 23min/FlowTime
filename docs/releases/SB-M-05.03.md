# Release SB-M-05.03 — Queue-Like DSL Parity

**Release Date:** 2025-11-28  
**Type:** Milestone delivery (schema/runtime update)  
**Key Templates:** supply-chain (base/classes), transportation-basic, transportation-basic-classes

## Overview

SB‑M‑05.03 completes the queue/DLQ DSL work started in SB‑M‑05.02. Any topology node that models a queue—`serviceWithBuffer`, `queue`, or `dlq`—can now declare `queueDepth: self` (or omit it) and the loader synthesizes the backing ServiceWithBuffer node automatically. This eliminates hand-authored helper nodes/series and keeps the graph clean. Analyzer/CLI/UI surfaces now key off logical type rather than helper IDs, and canonical templates (supply-chain, transportation, etc.) were migrated to the implicit DSL. Remaining router/helper work has been documented in the milestone and tracked as a follow-up.

## Key Changes

1. **Schema & Loader**
   - `docs/schemas/model.schema.yaml` and template schema docs now call out `queueDepth: self` support for `queue`, `dlq`, and `serviceWithBuffer` nodes.
   - The loader’s `QueueNodeSynthesizer` (formerly `ServiceWithBufferNodeSynthesizer`) creates hidden ServiceWithBuffer nodes whenever queue/dlq topology nodes omit helper series. Synthesized nodes carry `metadata.graph.hidden="true"` so they don’t clutter the topology.
   - Validator guards against mixed implicit/explicit queue definitions.
2. **Templates & Docs**
   - Supply-chain (base + classes), transportation-basic, transportation-basic-classes, and other canonical templates removed their backlog helper nodes and now rely solely on the implicit DSL. Remaining templates that still reference backlog series in expressions are documented for the router follow-up milestone.
   - `docs/templates/template-authoring.md` and `work/epics/completed/service-with-buffer/…` describe the new synthesizer behavior; template README entries highlight the SB‑M‑05.03 update.
3. **Analyzer / CLI / UI**
   - By-class warnings and queue latency badges now look at logical type, so implicitly synthesized queues behave exactly like explicit ones.
   - Retry badge rendering was tightened (chips sit closer to the node, badge appears after the chips), and synthesized helper nodes no longer appear in graph payloads.

## Tests

- `dotnet build`
- `dotnet test --nologo` (all suites pass; perf benchmarks remain skipped as expected)
- Targeted regressions: `TemplateBundleValidationTests`, `RouterTemplateRegressionTests`, parser/schema tests for queue/dlq synthesis.

## Known Gaps / Follow-ups

- Router nodes still require helper series for arrivals/served/capacity. `work/epics/completed/service-with-buffer/SB-M-05.03.md` documents the router follow-up milestone (“SB-M-05.03-router”) which will give routers the same implicit synthesizer treatment.
- Templates that still reference backlog series in expressions (warehouse picker, IT systems, incident retry, manufacturing, network reliability, transportation classes) will be refactored as part of the router/queue follow-up; for now those aliases remain intentional.

## Verification

- Supply-chain and transportation runs regenerated via FlowTime-Sim + FlowTime.API; topology no longer shows helper backlog nodes and queue latency badges render cleanly.
- UI retry badges/chips verified on transportation runs after the spacing tweak.
