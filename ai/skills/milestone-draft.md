# Skill: milestone-draft

Purpose: draft milestone specification documents after epic-refine is complete.

Use when:
- The epic is refined and you are ready to create milestone specs.
- The milestone does not yet exist, or needs a full rewrite to align with the guide.

Inputs:
- Epic slug and context
- Milestone ID and title
- Dependencies and target statement
- Any decisions or constraints captured during epic-refine

Guardrails:
- Follow docs/development/milestone-documentation-guide.md.
- Follow docs/development/milestone-rules-quick-ref.md.
- No time estimates.
- Diagrams must use Mermaid.

Process:
1) Ensure epic context exists. If missing, run epic-refine first.
2) Create or update the milestone spec under docs/milestones/ (filename may include a descriptive suffix).
3) Populate required sections with testable acceptance criteria and explicit scope boundaries.
4) Include a TDD-ready implementation plan that calls out RED -> GREEN -> REFACTOR.
5) Add or update references in:
   - docs/architecture/epic-roadmap.md (epic status and milestone list)
   - docs/ROADMAP.md (high-level status)
   - docs/architecture/<epic-slug>/README.md (if it lists milestones)

Outputs:
- A complete milestone spec in docs/milestones/.
- Clear dependencies and success criteria.
- TDD-ready implementation phases and test plan.

Notes:
- Do not start implementation in this skill.
- Use milestone-start when the milestone is ready to begin.
