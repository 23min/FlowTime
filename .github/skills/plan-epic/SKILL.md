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
   - [ ] Read the project's roadmap path for current epics and priorities
   - [ ] Read the project's epic spec path for related or overlapping epics
   - [ ] Check `work/gaps.md` for previously deferred work that fits

3. **Clarify with user** (ask, don't guess)
   - [ ] Confirm scope boundaries
   - [ ] Identify key constraints (tech stack, timeline, dependencies)
   - [ ] Agree on success criteria

4. **Assign epic ID**
   - [ ] Check the existing epic folders in the project's epic path for the highest existing E-xx number
   - [ ] Assign next sequential number (e.g., if E-14 exists, use E-15)
   - [ ] Epic numbering started at E-10; completed epics before E-10 are unnumbered

5. **Write epic spec**
   - [ ] Create the epic spec in the project's epic path using the epic template
   - [ ] Set `**ID:** E-{NN}` in the spec header
   - [ ] Fill in: goal, context, scope, constraints, success criteria
   - [ ] List known risks and open questions

6. **Optional tracker linkage**
   - [ ] If the repo uses an external issue tracker, create or link the epic record according to repo-specific rules

7. **Update roadmap**
   - [ ] Add epic to the project's roadmap path with E-xx prefix and status `planning`
   - [ ] Update any repo-specific epic index only if the repo actually uses one

## Output

- Epic specification in the project's epic path
- Updated project roadmap and any repo-specific epic index if applicable

## Next Step

→ `plan-milestones` to break the epic into sequenced milestones
