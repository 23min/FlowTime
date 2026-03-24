# ALWAYS_DO

Purpose: global guardrails that apply to every session, regardless of role.

## Core guardrails
- Follow `.github/copilot-instructions.md` and relevant docs under `docs/development/`.
- No time or effort estimates in docs or plans.
- Use `rg`/`fd` for searches; avoid destructive git.
- Keep docs/schemas/templates aligned when touching contracts.
- Tests must be deterministic; avoid external network calls.
- Use Mermaid for diagrams; avoid ASCII art boxes.
- Prefer minimal, precise edits; avoid broad refactors without context.

## Session hygiene
- Confirm epic context before milestone execution; if missing, run `epic-refine`/`epic-start`.
- Use TDD: RED -> GREEN -> REFACTOR; list tests first in tracking docs.
- Keep milestone specs stable; use tracking docs for progress updates.

## Build/test
- Build and test before handoff when asked to implement changes.
- If full test suite is too slow, run per-project tests and record results.
