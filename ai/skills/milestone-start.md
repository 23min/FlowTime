# Skill: milestone-start

Purpose: start or resume work on an existing milestone.

Use when:
- The milestone spec exists and is marked 📋 Planned or 🔄 In Progress.

Inputs:
- Milestone ID
- Epic slug

Preflight:
- If epic context is missing, run epic-refine or epic-start first.

Process:
1) Open milestone spec in docs/milestones/.
2) If starting fresh, create tracking doc from docs/development/TEMPLATE-tracking.md.
3) Update tracking doc with TDD plan (tests first).
4) Create or confirm branch per docs/development/branching-strategy.md.
5) Begin RED -> GREEN -> REFACTOR.

Outputs:
- Active tracking doc.
- Branch ready for implementation.
