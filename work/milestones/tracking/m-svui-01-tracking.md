# Tracking: m-svui-01-scaffold

**Started:** 2026-03-29

## Progress

| AC | Status | Notes |
|----|--------|-------|
| Dev server at :5173 | done | `pnpm dev` works, `pnpm build` passes |
| Sidebar collapse/expand | done | shadcn-svelte Sidebar with `collapsible="icon"` |
| Theme toggle | done | dark/light/system with localStorage (`ft.theme`), FOUC prevention |
| Route stubs | done | 7 routes, all rendering |

## Decisions

- Used pnpm (not npm) — aligns with shadcn-svelte conventions
- Manual shadcn-svelte init (CLI is interactive-only) — created components.json + utils.ts manually
- Pinned bits-ui to 2.15.0 — 2.16.4 has broken dist/types.js imports
- Used zinc base color with "new-york" style for enterprise aesthetic
- localStorage key `ft.theme` matches existing Blazor UI for potential cross-UI consistency
