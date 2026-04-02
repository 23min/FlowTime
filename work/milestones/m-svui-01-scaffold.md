# Milestone: m-svui-01-scaffold — Project Scaffold & Shell

**Epic:** Svelte UI (`work/epics/E-11-svelte-ui/spec.md`)
**Status:** done

## Goal

Standing SvelteKit app with sidebar layout and theme toggle.

## Acceptance Criteria

- [x] `pnpm dev` serves the app at localhost:5173
- [x] Sidebar collapses/expands with smooth transition
- [x] Theme toggle persists to localStorage and respects system preference
- [x] All route stubs render without errors

## Deliverables

- [x] SvelteKit project in `ui/` with TypeScript, Tailwind v4, shadcn-svelte
- [x] Root layout with collapsible left sidebar (Time Travel, Tools nav groups)
- [x] Top bar with breadcrumb area and theme toggle
- [x] Empty route stubs for all in-scope pages
- [x] DevContainer: port 5173 forwarded
- [x] Design tokens defined (spacing, elevation, transitions)
