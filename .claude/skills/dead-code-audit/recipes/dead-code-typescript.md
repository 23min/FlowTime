---
name: typescript
fileExts: [.ts, .tsx, .svelte, .js, .mjs, .cjs]
excludePaths: [node_modules/, dist/, .svelte-kit/, build/, coverage/, .claude/worktrees/]
tool: knip
toolCmd: "pnpm dlx knip --reporter json"
---

# Dead-code recipe: TypeScript (knip)

## Setup

`knip` is invoked via `pnpm dlx` so no global install is needed — first run downloads it. The repo uses pnpm workspaces (`pnpm-workspace.yaml`) and knip is workspace-aware. Run from the repo root.

If knip needs configuration (e.g., custom entry points beyond what it auto-detects), drop a `knip.json` at the repo root or per-package.

## Things to look out for in this stack

- **Svelte 5 runes** (`$state`, `$derived`, `$effect`, `$props`) — runes are compiler-recognized identifiers, not regular imports. Should not appear as unused.
- **SvelteKit route discovery** — files named `+page.svelte`, `+layout.svelte`, `+page.server.ts`, `+page.ts` are discovered by file-system convention, not import. knip's SvelteKit plugin handles this — verify it's enabled.
- **Vite plugin discovery** — `vite.config.ts` loads plugins by import side-effects; some plugin packages may appear "unused" from the source-graph view but are consumed at build time.
- **vitest test discovery** — `*.test.ts` / `*.spec.ts` files matched by glob in `vitest.config.ts`; their imports of test fixtures may look unused.
- **Playwright spec discovery** — `tests/ui/specs/**` matched by `playwright.config.ts`; spec files and their imports of helpers in `tests/ui/helpers/`.
- **shadcn-svelte components** — imported via `$lib/components/ui/<name>` aliases resolved by Vite. Check `svelte.config.js` aliases before flagging.
- **Type-only exports** — `export type { Foo }` re-exports may not be flagged correctly by all tools; cross-check with explicit type imports.
- **dag-map library** in `lib/dag-map/` is consumed by `ui/` workspace — its exports are an internal-monorepo public surface.

## Public surface notes

- **`lib/dag-map/`** is a separate package with its own `package.json`. Its exports are the public API consumed by `ui/`. Treat exported symbols in `lib/dag-map/src/` as live unless every workspace consumer's import is also unreachable.
- **`tools/mcp-server/`** is a runtime entry-point package; its `bin` script and main entry are live. Internal exports may be dead candidates.
- **`ui/src/lib/`** exports may be referenced via `$lib` alias from any `+page.svelte` / `+layout.svelte` / route file. Verify alias resolution before flagging.
- **`ui/src/routes/`** — every `+page.svelte` / `+layout.svelte` / `+page.ts` / `+page.server.ts` / `+layout.ts` is a SvelteKit-discovered entry point.

## Tool-specific notes

- **High-signal categories:** `unused exports`, `unused files`, `unused dependencies` (production deps, not devDependencies).
- **Review carefully (false positives common):** `unused devDependencies` — shared dev tooling like `prettier`, `eslint` plugins, type packages used only in `tsconfig.json` may look unused.
- **Output format:** JSON via `--reporter json`. Top-level shape: `{ files: [...], dependencies: { unused: [...], unlisted: [...] }, devDependencies: { unused: [...] }, exports: { ... } }`.
- **knip's auto-config** detects most setup automatically (SvelteKit, Vite, Vitest, Playwright). If it misses something, add an explicit entry in `knip.json` rather than suppressing.
- **Run scope:** knip operates over the whole monorepo by default; the skill's per-recipe filter narrows results to the milestone change-set after the tool runs.
