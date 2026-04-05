# Skill: Wrap Milestone

Complete a milestone, write summary, and prepare for merge.

## When to Use

All acceptance criteria met, tests passing, build green. User says: "Wrap milestone X", "Finish M1"

## Checklist

1. **Verify completion**
   - [ ] Read milestone spec — check every AC is implemented
   - [ ] Read tracking doc — all ACs checked
   - [ ] Run full test suite — all pass
   - [ ] Build is green

2. **Final review**
   - [ ] No TODO comments left behind (unless intentional and documented)
   - [ ] No debug code or temporary workarounds
   - [ ] Documentation is up to date

3. **Update tracking doc**
   - [ ] Mark milestone as complete with final date
   - [ ] Add final test count and build status

4. **Optional tracker wrap-up**
   - [ ] If the repo uses an external issue tracker, close or update the linked milestone/epic records according to repo-specific rules

5. **Note deferred work**
   - [ ] Any deferred items → `work/gaps.md`

6. **Update CLAUDE.md current work section**
   - [ ] Update the `## Current Work` section in `CLAUDE.md`:
     - Mark current milestone as complete
     - If there's a next milestone in the epic, set it as upcoming
     - If this was the last milestone, note epic as complete
   - [ ] This ensures the next conversation starts with accurate context

7. **Prepare merge**
   - [ ] Ensure all changes are staged
   - [ ] Show staged diff summary (`git diff --staged --stat`)
   - [ ] Prepare commit message (conventional format)
   - [ ] 🛑 **STOP — present staged changes and commit message to user.**
   - [ ] Say: "Ready to commit and merge. Here's what will be committed: [summary]. Commit message: [message]. Shall I commit?"
   - [ ] **Wait for human to explicitly say "commit" before proceeding.**

8. **After user says "commit"** (NOT "ok", NOT "continue" — must say "commit")
   - [ ] Commit with agreed message
   - [ ] 🛑 **STOP — ask: "Push and merge to [branch]?"**
   - [ ] Merge to base branch (or create PR)
   - [ ] Update the project's roadmap path if the milestone was the last in an epic
   - [ ] Move the completed epic folder only if the repo actually uses a completed-epics archive path
   - [ ] Update any repo-specific epic index only if the repo actually uses one

9. **Record learnings** (memory write-back)
   - [ ] Append any new architectural or technical decisions to `work/decisions.md` using this format:
     ```markdown
     ## D-YYYY-MM-DD-NNN: <short title>
     **Status:** active
     **Context:** <why this decision was needed>
     **Decision:** <what was decided>
     **Consequences:** <what follows from this>
     ```
   - [ ] Append implementation learnings to the current agent's history file (`work/agent-history/<agent>.md`):
     - Patterns that worked well
     - Pitfalls encountered and how they were resolved
     - Project conventions discovered or established
   - [ ] Keep entries concise (2-5 lines each). If the history file exceeds ~200 lines, summarize older entries.

## Output

- Committed and merged code (after approval)
- Updated tracking doc (final status)
- Updated `gaps.md` (if deferred work found)
