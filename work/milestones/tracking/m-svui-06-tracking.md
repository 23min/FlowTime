# Tracking: Run Orchestration

**Milestone:** m-svui-06
**Branch:** milestone/m-svui-06
**Started:** 2026-04-02
**Status:** in-progress

## Acceptance Criteria

- [x] AC1: Templates render as cards in a responsive grid with title, description, domain icon, and version badge
- [x] AC2: Search input filters cards by name/description in real-time
- [x] AC3: Selecting a card fetches template detail and shows config panel (bundle reuse mode, RNG seed, advanced params)
- [x] AC4: Can execute a model run and see results (run ID, metadata, warnings, nav links) when complete
- [x] AC5: Preview/dry-run mode shows the execution plan without creating a run
- [x] AC6: Loading, error, and empty states handled gracefully (skeletons, error cards, empty message)
- [x] AC7: No raw JSON parameter field visible by default (hidden in Advanced section)

## Implementation Log

| Phase | What | Tests | Status |
|-------|------|-------|--------|
| 1 | API types + sim.ts methods | 0 | done |
| 2 | shadcn-svelte components (badge, collapsible, radio-group, alert) | 0 | done |
| 3 | domain-icon utility + template-card component | 0 | done |
| 4 | run-config-panel + run-result + dry-run-plan components | 0 | done |
| 5 | Main page assembly (run/+page.svelte) | 0 | done |
| 6 | Smoke tests + build verification | 0 | done |

## Test Summary

- **Total tests:** 0
- **Passing:** 0
- **Build:** green (UI build clean, pre-existing failures in Api.Tests/FlowTime.Tests unrelated to M6)

## Notes

- Pre-existing 18 test failures on main (Api.Tests schema tests, FlowTime.Tests) — not M6 related
- Mode shown as read-only badge per user feedback

## Completion

- **Completed:** pending
- **Final test count:** TBD
- **Deferred items:** (none)
