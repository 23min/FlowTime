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

## Validation
- `dotnet build FlowTime.sln`
- `dotnet test FlowTime.Api.Tests/FlowTime.Api.Tests.csproj`
- `dotnet test FlowTime.sln`

## Tracking
- Milestone doc created (this file).
- Follow the standard tracking template under `docs/milestones/tracking/` when implementation begins.
