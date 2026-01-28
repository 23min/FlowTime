# Skill: epic-refine

Purpose: run a human-in-the-loop preflight to confirm epic scope, decisions, and constraints before any milestone specs are drafted.

Use when:
- A new epic is starting and we need shared context and decisions up front.
- The epic is specified but still ambiguous or has open questions.

Inputs:
- Epic name and slug (maps to docs/architecture/<epic-slug>/)
- High-level goal or problem statement
- Known constraints (time, data, platform, teams)
- Known dependencies (milestones, systems, external services)

Process:
1) Locate or create the epic folder under docs/architecture/<epic-slug>/.
2) Review existing epic docs and roadmap entries (if any):
   - docs/architecture/<epic-slug>/README.md
   - docs/architecture/epic-roadmap.md
   - docs/ROADMAP.md
3) Run a structured Q&A and capture decisions in a short notes block.
4) Produce an initial milestone outline for the epic.
5) Confirm open questions and owners; do not proceed to drafting milestones until resolved or explicitly deferred.

Structured Q&A (capture answers verbatim):
- Goal: what is the epic trying to enable or fix?
- Success criteria: what observable outcomes signal success?
- In scope: what must be included to claim completion?
- Out of scope: what is explicitly excluded?
- Dependencies: what milestones, systems, or teams must align first?
- Data/contracts: any schema or API changes required?
- Surfaces: which products are affected (API, UI, CLI, Sim, Core)?
- Risks: what could derail or invalidate the plan?
- Testing: how will we validate correctness and regressions?
- Observability: what metrics, logs, or diagnostics are needed?
- Security: any auth, privacy, or access changes?
- Rollout: phased delivery, migration, or compatibility steps?
- Documentation: which docs must change at epic or milestone completion?

Outputs:
- Epic summary (one paragraph) and decisions list.
- Open questions list with owners.
- Milestone outline (IDs optional but preferred) with rough sequencing.
- A clear handoff to milestone-draft for each planned milestone.

Notes:
- Do not start coding or implementation planning here.
- Use milestone-draft once the epic decisions are confirmed.
