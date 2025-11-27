# TT‑M‑03.32.1 — First-Class DLQ Nodes

Status: ✅ Complete  
Owners: Platform (API/Runtime) + UI

## Overview

TT‑M‑03.32 delivered retry budgets, exhausted-flow tracking, and terminal-edge visualization by convention (aliases + `type: terminal`). This milestone makes DLQs explicit in the model so engines, analyzers, and the UI can reason about dead-letter queues without ad-hoc semantics. The goal is to add a `dlq` node type (or `queueType: dlq`) that enforces DLQ behavior, simplifies template authoring, and unlocks dedicated visuals.

## Motivation & Benefits

1. **Explicit Semantics** – Today a DLQ is just a queue fed by `exhaustedFailures`. A dedicated type lets analyzers and tooling understand that a node is terminal, diagnostic, and not part of the main throughput graph.
2. **Validation & Safety** – DLQs should only accept terminal edges and should not drive regular throughput. With a real type we can block miswired templates (e.g., DLQ feeding arrivals directly).
3. **UI Clarity** – A distinct node type simplifies the canvas logic (no more alias-based terminal badges), enabling custom shapes/labels, filtering, and future DLQ dashboards.

## Scope

### In Scope
- Schema/model updates to represent DLQs (`kind: dlq` or `queueType: dlq`), plus DTO + schema doc changes.
- Analyzer/runtime rules enforcing DLQ invariants (only terminal inbound edges, optional release flow back to manual handling, warnings if misused).
- UI updates: render DLQ nodes distinctly (badge, color, legend) and expose a toggle/filter.
- Template + fixture migration for supplied examples (supply-chain DLQ, IT telemetry templates).

### Out of Scope
- Automated DLQ draining/processing logic (still manual modeling).
- Alerting/monitoring on DLQ thresholds (future governance milestone).

## Deliverables
1. Schema + contract updates (docs/schemas, DTOs, parser/serializer changes).
2. Invariant analyzer + runtime enforcement of DLQ semantics.
3. UI rendering changes (canvas badge/shape, feature toggle, inspector updates).
4. Template/fixture migrations demonstrating `kind: dlq`.
5. Updated documentation (milestone, authoring guide, release notes).

## Dependencies
- Builds directly on TT‑M‑03.32; no external blockers.
- Requires coordination with template owners to migrate DLQ nodes.

## Implementation Summary
- Adopted `kind: dlq` as the canonical marker in schemas, DTOs, and template docs. API/Graph/state contracts now surface DLQ nodes without alias hacks.
- Invariant analyzer + semantic loader emit explicit warnings (`dlq_non_terminal_inbound` / `outbound`) when DLQs receive or emit non-terminal edges; `ModeValidator`, `StateQueryService`, and metrics services treat DLQs as queue-like for latency/series while keeping them out of SLA scoring.
- Topology canvas renders DLQs with a dedicated trapezoid/badge, queue-depth readout, and a feature-bar toggle (`Include DLQ nodes`). JS helpers avoid badges on upstream services, and inspector labels recognize the new kind.
- `templates/supply-chain-multi-tier.yaml` and its golden fixtures migrated to `kind: dlq`, with terminal release edges enforced. Authoring/testing guides now document the schema + analyzer expectations.
- Release notes updated to call out TT‑M‑03.32.1 so future operators know DLQs are first-class.

### Template Enhancement Backlog (Next Up)
With the core DLQ semantics live, we now need to retrofit the remaining canonical templates so their queues carry realistic backlog signals and their loss/failure pathways terminate in DLQs or explicit sinks. The following plan will be executed sequentially during TT‑M‑03.32.1:

> **DLQ Lens (Our Services vs. Dependencies)**  
> DLQs belong to services we operate (where we own retries and escalation). External dependencies should surface failures as losses/terminal queues, not DLQs. Each retrofit will move/introduce DLQs on “our” services, and express downstream vendors/partners as terminal sinks.

1. **IT System – Microservices (`templates/it-system-microservices.yaml`)**
   - Rework `IngressQueue` to use backlog-aware outflow/attrition so queue depth oscillates instead of staying pinned.
   - Introduce a `ManualReconciliation` DLQ fed by auth retry failures (our service), not the database dependency. Add a response queue that returns authorized sessions to customers.
   - Add a terminal queue for `lb_dropped`/`auth_failures` so customer-impact backlog is visible.
2. **Manufacturing Line (`templates/manufacturing-line.yaml`)**
   - Update `WipQueue` outflow to consume backlog and model loss due to excessive wait.
   - Route persistent QC/packaging failures (`qc_retry_failures`, `quality_errors`) to DLQ/terminal queues representing scrap, downgrade, or supplier chargebacks.
3. **Network Reliability (`templates/network-reliability.yaml`)**
   - Give `RequestQueue` proper backlog dynamics (capacity draws down backlog instead of only current inflow).
   - Add DLQs for `core_retry_failures`/`database_failures` (e.g., “CustomerMakeGood”).
   - Model packet attrition (`request_queue_spill`) as a terminal queue tied to large depth.
4. **Supply-Chain Incident Retry (`templates/supply-chain-incident-retry.yaml`)**
   - Fix `IncidentQueue` depth math (backlog-aware outflow) plus attrition.
   - Add DLQs/terminal queues for `incident_retry_failures` and `support_queue_errors` to represent escalations/abandonment.
5. **Transportation Basic (`templates/transportation-basic.yaml`)**
   - Improve `HubQueue` outflow + attrition.
   - Create DLQs or terminal sinks for repeated airport retries and unmet demand per destination.
6. **Supply-Chain Multi-Tier (`templates/supply-chain-multi-tier.yaml`)**
   - Already uses DLQs, but `DistributionQueue` still needs backlog-aware outflow so the main queue depth behaves realistically.
   - Evaluate additional sinks for supplier shortages/delivery misses.

Each retrofit will update the relevant template, regenerate fixtures/goldens, and document the DLQ semantics in the template authoring guides. We will reassess whether both supply-chain templates are necessary after the above work (see Tracking doc for the consolidation analysis task).

### Retrofit Workflow & Validation

When touching any template in this milestone, follow the canonical workflow so Sim + Engine analyzers run and fixtures stay aligned:

1. **Edit & inspect** – Update the YAML (queue math, DLQs, terminal edges) under `templates/`. Keep changes ASCII and respect `.editorconfig`.
2. **Parameter validation** – `dotnet run --project src/FlowTime.Sim.Cli -- validate template --id <template-id> --templates-dir templates` to ensure defaults/overrides still type-check.
3. **Generate + Sim analyzer** – `dotnet run --project src/FlowTime.Sim.Cli -- generate --id <template-id> --templates-dir templates --mode simulation --out /tmp/<template-id>.yaml`. This command automatically runs the invariant analyzers (same as FlowTime.Sim Service) and prints warnings if anything regresses.
4. **Engine analyzer sweep** – `dotnet test tests/FlowTime.Tests/FlowTime.Tests.csproj --filter TemplateBundleValidationTests.AllTemplatesGenerateValidBundlesWithDefaults` (or `dotnet test FlowTime.Tests --filter TemplateBundleValidationTests`) so the engine-side bundle validator parses the generated model. This mirrors what FlowTime.Engine does when loading canonical artifacts.
5. **Fixtures + goldens** – If telemetry/outputs change, update `fixtures/<template-id>/` and rerun `dotnet test FlowTime.Api.Tests/FlowTime.Api.Tests.csproj` (refresh JSON goldens) plus any UI golden suites impacted.
6. **Full solution tests** – Finish with `dotnet build FlowTime.sln` and `dotnet test FlowTime.sln`, documenting known perf guard failures (e.g., PMF scaling tests) in the milestone hand-off.
7. **Docs** – Record notable analyzer warnings (e.g., DLQ latency info) and screenshots in `docs/templates/template-authoring.md` / `template-testing.md`, plus call out any template deprecations (e.g., if we consolidate the two supply-chain templates).

Only after the above steps pass should a retrofit be considered “done.”

## Validation
- `dotnet build FlowTime.sln`
- `dotnet test FlowTime.Api.Tests/FlowTime.Api.Tests.csproj`
- `dotnet test FlowTime.sln`

## Tracking
- Milestone doc created (this file).
- Follow the standard tracking template under `docs/milestones/tracking/` when implementation begins.

### Deferred (Post TT‑M‑03.32.1)
- Add analyzer checks that correlate queue arrivals with upstream `served` metrics so mismatched semantics (e.g., pointing arrivals at backlog-adjusted demand) are caught automatically. This requires topology-aware validation or edge-level telemetry, so we will tackle it in the next epic after the time-travel deliverables.
