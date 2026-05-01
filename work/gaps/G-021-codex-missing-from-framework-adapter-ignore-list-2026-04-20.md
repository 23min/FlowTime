---
id: G-021
title: '`.codex/` missing from framework adapter-ignore list (2026-04-20)'
status: open
---

### Why this is a gap

The `.ai/sync.sh` refactor merged in `23min/ai-workflow#5` formalised the track-vs-ignore convention (CLAUDE.md tracked; all other adapters gitignored) and made `sync.sh` reconcile `.gitignore` on every run. The `ADAPTER_ENTRIES` array in `sync.sh` covers `.github/copilot-instructions.md`, `.github/skills/`, `.claude/agents/`, `.claude/skills/`, and `.claude/rules/ai-framework.md` — but **not `.codex/instructions.md`**, which is equally a generated adapter.

`flowtime-vnext` handled this locally by appending `.codex/` to its own `.gitignore` and running `git rm --cached .codex/instructions.md`. Works for this repo, but any other consumer of the framework would need to replicate the same manual step.

### Resolution path

Follow-up PR to `23min/ai-workflow`: add `.codex/instructions.md` (or `.codex/`) to `ADAPTER_ENTRIES` in `sync.sh`, update the corresponding test assertions in `tests/test-sync.sh`, and add a MIGRATIONS entry noting the additional ignore line so existing consumers untrack their tracked `.codex/instructions.md`.

### Immediate implications

- Do not re-add `.codex/instructions.md` to git tracking in this repo.
- When the framework adds the entry, local override becomes redundant and can be simplified (drop `.codex/` from this repo's `.gitignore`).

### Reference

- `23min/ai-workflow#5` — sync.sh consolidation PR (merged 2026-04-20)
- This repo's `.gitignore` — `.codex/` entry added locally

---
