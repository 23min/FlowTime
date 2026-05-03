# Skill: epic-refine

**Trigger phrases:** "refine the epic", "clarify epic scope", "what are we building?", "define epic boundaries", "epic planning session"

## Purpose

Run a human-in-the-loop preflight to confirm epic scope, decisions, and constraints before any milestone specs are drafted. This is the critical first step that prevents scope creep and misalignment.

## Use When
- A new epic is starting and you need shared context and decisions up front
- The epic is specified but still ambiguous or has open questions
- Stakeholders need to align on goals and boundaries
- You're unsure what to build or why

## Inputs
- Epic name and slug (short identifier, e.g., "classes", "ui-perf", "service-buffer")
- High-level goal or problem statement
- Known constraints (platform, data, dependencies, teams)
- Known dependencies (other epics, milestones, external systems)

## Process

### 1. Create Epic Structure
- Create epic folder: `docs/architecture/<epic-slug>/`
- Initialize `README.md` in the epic folder
- Add entry to roadmap document

### 2. Review Existing Context
Check if related documentation exists:
- Epic folder and README
- Roadmap entries
- Related architecture docs
- Existing milestones or features

### 3. Run Structured Q&A
Capture answers to all questions below. Don't skip any—they prevent costly rework later.

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
