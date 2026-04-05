# Skill: Draft Spec

Write a milestone specification with clear acceptance criteria.

## When to Use

Milestone plan exists. User says: "Write spec for milestone X", "Draft M1"

## Checklist

1. **Gather context**
   - [ ] Read epic spec for overall goals
   - [ ] Read milestone plan for this milestone's place in sequence
   - [ ] Read prior milestone specs/artifacts (if M2+)
   - [ ] Check existing code for conventions and patterns

2. **Write the spec** using milestone template
   - [ ] **Goal:** 1-2 sentences — what this milestone achieves
   - [ ] **Context:** What exists before this milestone
   - [ ] **Acceptance Criteria:** Numbered, testable, pass/fail
   - [ ] **Technical Notes:** Implementation hints, patterns to follow
   - [ ] **Out of Scope:** What this milestone explicitly does NOT do
   - [ ] **Dependencies:** What must exist before starting

3. **Quality checks**
   - [ ] Each AC is independently testable
   - [ ] ACs don't overlap or contradict
   - [ ] No vague criteria ("should be good", "well-tested")
   - [ ] File paths and names follow project conventions

4. **Save spec**
   - [ ] Write to the project's milestone spec path

5. **Optional tracker linkage**
   - [ ] If the repo uses an external issue tracker, link the milestone spec according to repo-specific rules

6. **User approval**
   - [ ] Review spec with user
   - [ ] Incorporate feedback
   - [ ] Get explicit "approved" before proceeding

## Output

- Approved milestone spec in the project's milestone path

## Next Step

→ `start-milestone` to begin implementation
