---
id: M-061
title: Project Scaffold & Shell
status: done
parent: E-11
acs:
  - id: AC-1
    title: '`pnpm dev` serves the app at localhost:5173'
    status: met
  - id: AC-2
    title: Sidebar collapses/expands with smooth transition
    status: met
  - id: AC-3
    title: Theme toggle persists to localStorage and respects system preference
    status: met
  - id: AC-4
    title: All route stubs render without errors
    status: met
---

## Goal

Standing SvelteKit app with sidebar layout and theme toggle.

## Acceptance criteria

### AC-1 — `pnpm dev` serves the app at localhost:5173

### AC-2 — Sidebar collapses/expands with smooth transition

### AC-3 — Theme toggle persists to localStorage and respects system preference

### AC-4 — All route stubs render without errors
## Deliverables

- [x] SvelteKit project in `ui/` with TypeScript, Tailwind v4, shadcn-svelte
- [x] Root layout with collapsible left sidebar (Time Travel, Tools nav groups)
- [x] Top bar with breadcrumb area and theme toggle
- [x] Empty route stubs for all in-scope pages
- [x] DevContainer: port 5173 forwarded
- [x] Design tokens defined (spacing, elevation, transitions)
