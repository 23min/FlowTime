# Skill: gap-triage

Purpose: record gaps discovered during work and decide whether to include now or defer.

Use when:
- A missing requirement, design hole, or unexpected limitation is found.

Inputs:
- Gap description
- Where it was found (file, test, doc)
- Impact and risk

Process:
1) Record the gap in the backlog section of docs/ROADMAP.md (or the agreed backlog doc).
2) Classify: scope gap, design gap, implementation gap, or documentation gap.
3) Decide disposition with the user:
   - Include in current milestone (update scope + tests)
   - Defer to a specific milestone (preferred)
   - Defer to a future epic (if broader)
4) If deferred, record the target epic/milestone and rationale.
5) If included now, update the milestone spec and tracking doc.

Outputs:
- Gap recorded with owner and target.
- Milestone scope updated if the gap is pulled in.
