# Milestone Prompt Template

Use this template when asking Codex to create or update a milestone. It captures the required structure, authoritative sources, and desired output location so responses stay consistent.

```text
I need a milestone spec for <Milestone ID> — <Title>.
Use docs/development/milestone-documentation-guide.md and docs/development/milestone-rules-quick-ref.md for structure and rules.
Scope, acceptance criteria, and test coverage should come from:
- docs/architecture/time-travel/time-travel-planning-roadmap.md
- docs/architecture/time-travel/time-travel-planning-decisions.md
If additional architectural detail is needed, reference:
- docs/architecture/time-travel/time-travel-architecture-ch3-components.md
- docs/architecture/time-travel/time-travel-architecture-ch5-implementation-roadmap.md
Create the milestone document at docs/milestones/<Milestone ID>.md following the standard outline.
```

### Notes
- Replace `<Milestone ID>` and `<Title>` with the specific milestone (e.g., `M-03.01 — Time-Travel APIs`).
- Add any milestone-specific context (e.g., dependencies, known constraints) after the template block if needed.
- When executing a milestone (updating status or tracking progress), point Codex to the existing milestone doc and corresponding tracking file instead of creating a new spec.
