# Skill: milestone-draft

**Trigger phrases:** "create milestone spec", "draft milestone", "plan milestone", "write milestone document"

## Purpose

Draft milestone specification documents after epic-refine is complete. The milestone spec is the authoritative plan for implementation—it must be clear, complete, and actionable.

## Use When
- The epic is refined and you're ready to create milestone specs
- A milestone doesn't exist yet
- An existing milestone needs complete rewrite for clarity

## Inputs
- Epic slug and context
- Milestone ID and title
- Dependencies (what must complete first)
- Target statement (one-sentence goal)
- Decisions or constraints from epic-refine

## Guardrails

**ALWAYS:**
- ✅ Write testable acceptance criteria
- ✅ Define clear scope boundaries (in/out)
- ✅ Use Mermaid for diagrams
- ✅ Include comprehensive test plan
- ✅ Make it implementation-ready

**NEVER:**
- ❌ Include time or effort estimates
- ❌ Use vague requirements
- ❌ Skip test plan
- ❌ Use ASCII art for diagrams
- ❌ Leave scope ambiguous

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
