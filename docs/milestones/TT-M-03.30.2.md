# TT‑M‑03.30.2 — Template Buffer Realism & PMF Adoption

Status: 🟡 Planned  
Owners: Platform (Templates) + Architecture  
References: docs/architecture/retry-modeling.md, docs/templates/metric-alias-authoring.md, docs/development/milestone-documentation-guide.md

---

## Overview

Follow-up to TT‑M‑03.30.1 that focuses on raising the realism of our catalog templates. Two levers:

1. **Explicit buffers/queues** wherever the real-world system has staging points (logistics, transportation, IT workflows, manufacturing).
2. **Probabilistic inputs** using PMFs instead of hard-coded const arrays so template runs capture variance without copy/pasting hourly values.

## Goals

- Audit every published template; add `kind: queue` nodes + consumers wherever backlog/buffer behavior exists.
- Refresh sample data to use PMFs (or hybrid const/PMF) for arrivals, service times, and retry backlogs.
- Update documentation so template maintainers know when to introduce queues and how to switch to PMFs.
- Backfill goldens/tests so API/UI reflect the new topology/series outputs.

## Scope

### In Scope
- Template changes for: incident workflow, supply chain variants, transportation, manufacturing line, network models, etc.
- PMF-based series (with kernel guidance) for arrival, demand, and retry distributions.
- Visual polish for retry/buffer UX (retry loops, queue styling, inspector tax summary).
- API/UI regression fixes due to modified topology (graph goldens, state window goldens, UI snapshots).
- Documentation updates: template authoring guide, retry modeling note, release entry.

### Out of Scope
- Runtime customization/UI toggles for enabling/disabling new queues (tracked separately).
- Marketplace template ingestion or third-party content migration.

## Requirements

1. **Template updates**
   - Identify buffer spots per template and add queue nodes + consumers.
   - Ensure semantics lists only metrics the node actually produces (avoiding phantom retry chips).
   - Replace `const values` describing demand/arrivals with `pmf` nodes or exprs referencing PMFs where feasible.

2. **API Contract / Goldens**
   - Update graph/state goldens to reflect new topology nodes/edges and changed series.
   - Add coverage for new PMF series where state endpoints expose them.

3. **UI/Engine Adjustments**
   - **Internal retries:** add a short arc looping from the service’s right edge back to the top of the attempts chip. The arc color should reflect retry ratio (green → yellow → red). Retry-related chips stay next to attempts/failures; only the actual retry loop shows the “extra” work.
   - **Edges:** service-to-consumer edges display only Served volume (attempts stay internal to the loop). Served chips can be removed/repurposed accordingly.
   - **Inspector:** include “Retry tax” (% of attempts that are retries) in the summary block.
- **Queue accumulation (Backlog node):** implement a first‑class stateful backlog node so templates model `Q[t] = max(0, Q[t−1] + inflow[t] − outflow[t])` with a topology initial seed (no self‑SHIFT cycles). Topology semantics map `queue` to this node; UI reads depth directly. Loss series (optional) subtracts drops before clamping.
- **Queue styling:** render queue nodes as pill-shaped capsules and show the depth inline.
- **Focus controls:** remove “Queue depth” from the global focus chips and drop the queue-depth badge toggle in the topology panel (queues always show depth inline). Focus metrics apply only to service nodes.
- Update UI tests/goldens once the visuals change.
- **Retry loop clarity:** keep Attempts in the top row (so they still balance Arrivals + Retries) and only render the sideways “U” loop when we have non-zero retry tax. When the loop does render, we now show a horizontal chip stack (`Retries`, `Failed retries`, `Retry echo`) next to it, and hide both the loop and the chips entirely when there is no retry activity. Templates map `errors` to the full set of failed attempts while `failures` is reserved for internal retry attempts so the UI can keep those signals distinct.

4. **Documentation**
   - Add a “buffer modeling” section to `docs/templates/metric-alias-authoring.md` (or new doc) describing when to use queue nodes.
   - Extend retry-modeling doc to mention PMF usage guidelines.
   - Release note summarizing the template refresh.

## Acceptance Criteria

- AC1: All catalog templates explicitly model at least one queue/buffer where the real system has staging, using the Backlog node for true accumulation (no per-bin delta proxies).
- AC2: Templates previously using deterministic const series for arrivals/demand now expose PMFs (or justify remaining const data).
- AC3: API/state/graph goldens updated and tests passing (aside from known external failures).
- AC4: UI renders expected chips (no retry chips on queues) and topology includes the new consumer nodes.
- AC5: Documentation references the new modeling guidance + templates tagged in release tracking.

## Validation

| Command | Result |
| --- | --- |
| `dotnet build FlowTime.sln` | ✅ |
| `dotnet test FlowTime.sln` | ⚠️ Known `FlowTime.Sim.Tests.NodeBased.ModelGenerationTests.GenerateModelAsync_WithConstNodes_EmbedsProvenance` failure tracked separately. |

## Implementation Plan (Draft)

1. **Template audit & planning** – inventory current nodes, identify buffer gaps, draft PMF replacements.
2. **Iterative template refresh** – update one template at a time; run targeted `dotnet test` for affected areas.
3. **API/UI golden updates** – refresh state/graph/metrics goldens plus UI snapshots once templates settle.
4. **Docs & release note** – capture new guidance and summarize changes in TT tracking + release file.

## Risks & Mitigations

- **Template regression risk**: queue additions can change throughput math. Mitigation: keep before/after validation tables per template.
- **Golden churn**: multiple templates change simultaneously. Mitigation: stage updates per template to keep diffs reviewable.
- **PMF accuracy**: naive PMFs could produce unrealistic demand. Mitigation: derive PMFs from existing const arrays to preserve means.

## Tracking

- Add TT‑M‑03.30.2 entry to milestone tracking doc with checklist per template (queue addition + PMF adoption).
