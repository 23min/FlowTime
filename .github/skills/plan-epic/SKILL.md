# Skill: Plan Epic

Scope, refine, and document a new epic.

## When to Use

User says: "Plan feature X", "Design the system for Y", "I need to build Z"

## Checklist

1. **Understand the request**
   - [ ] What problem does this solve?
   - [ ] Who benefits?
   - [ ] What are the boundaries (in-scope / out-of-scope)?

2. **Check existing context**
   - [ ] Read `ROADMAP.md` for current epics and priorities
   - [ ] Read `work/epics/` for related or overlapping epics
   - [ ] Check `work/gaps.md` for previously deferred work that fits

3. **Clarify with user** (ask, don't guess)
   - [ ] Confirm scope boundaries
   - [ ] Identify key constraints (tech stack, timeline, dependencies)
   - [ ] Agree on success criteria

4. **Write epic spec**
   - [ ] Create `work/epics/<epic-slug>/spec.md` using epic template
   - [ ] Fill in: goal, context, scope, constraints, success criteria
   - [ ] List known risks and open questions

5. **Update roadmap**
   - [ ] Add epic to `ROADMAP.md` with status `planning`

## Output

- `work/epics/<epic-slug>/spec.md` — the epic specification
- Updated `ROADMAP.md`

## Next Step

→ `plan-milestones` to break the epic into sequenced milestones
