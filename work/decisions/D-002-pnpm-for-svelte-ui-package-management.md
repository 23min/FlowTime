---
id: D-002
title: pnpm for Svelte UI package management
status: accepted
---

**Status:** active
**Context:** Root repo uses npm (for Playwright tests). The `ui/` project needed a package manager.
**Decision:** Use pnpm — aligns with shadcn-svelte documentation conventions, already installed in devcontainer (v10.33).
**Consequences:** `ui/` has pnpm-lock.yaml, not package-lock.json. init.sh runs `pnpm install --frozen-lockfile` for ui/.
