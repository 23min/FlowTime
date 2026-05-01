---
id: D-003
title: Manual shadcn-svelte initialization
status: accepted
---

**Status:** active
**Context:** `shadcn-svelte init` CLI is interactive-only (prompts for preset selection), cannot be run non-interactively in CI or automation.
**Decision:** Manually create `components.json`, `utils.ts`, and `app.css` theme variables. Add components individually via `yes | pnpm dlx shadcn-svelte add <component>`.
**Consequences:** Works in non-TTY environments. Must manually keep components.json aligned with shadcn-svelte schema on upgrades.
