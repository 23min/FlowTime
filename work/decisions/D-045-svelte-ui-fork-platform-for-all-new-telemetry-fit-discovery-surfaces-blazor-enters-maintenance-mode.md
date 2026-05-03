---
id: D-045
title: Svelte UI fork — platform for all new telemetry/fit/discovery surfaces; Blazor enters maintenance mode
status: accepted
---

**Status:** active
**Context:** Through E-11 (Svelte UI M1-M4 + M6), E-17 (Interactive What-If on Svelte), and the E-18 analysis endpoints (`/v1/sweep`, `/v1/sensitivity`, `/v1/goal-seek`, `/v1/optimize`, `/v1/validate`), Svelte proved itself the faster and more expressive surface for expert modeling UX: tighter density, better data-viz story, direct ownership of dag-map, and a cleaner path to WebSocket/streaming protocols. Meanwhile the remaining Blazor backlog (E-11 M5 Inspector, M7 Dashboard, M8 Polish) would either duplicate Svelte work or deliver against paradigms (overlay feature-bar) we now consider inferior to the workbench paradigm. D-028 and D-041 previously kept Blazor as an equal parallel UI without parity requirements; with the E-18 analysis surfaces still UI-less and the expert authoring / telemetry / fit roadmap ahead, it is no longer efficient to build new features twice.
**Decision:** As of 2026-04-15, Svelte UI is the **primary platform** for all new surfaces: workbench/topology evolution, Time Machine analysis (sweep, sensitivity, goal-seek, optimize, validate), telemetry ingestion, model fit, heatmap view, validation surface, and any future expert authoring or discovery work. Blazor UI enters **maintenance mode** — aligned with current Engine/Sim contracts (bug fixes, contract syncs) but no new feature work. E-11 is paused after M6 as a completed historical track: M5 (Inspector) and M8 (Polish) absorb into E-21 (workbench paradigm + polish milestone); M7 (Dashboard) is deferred — the workbench + heatmap views cover the same ground more precisely. New Svelte work is tracked under E-21 — Svelte Workbench & Analysis Surfaces.

**Active post-fork delivery sequence (Option A, locked 2026-04-15):**
1. M-010 SessionModelEvaluator (complete, merged)
2. M-011 .NET Time Machine CLI (complete, merged)
3. E-21 Svelte Workbench & Analysis Surfaces (in-progress) — the UI parity fork
4. E-15 Telemetry Ingestion — Gold Builder → Graph Builder → first dataset path
5. Telemetry Loop & Parity — parity harness (prerequisite for trustworthy fit)
6. m-E18-XX Model Fit — `FitSpec`/`FitRunner`/`POST /v1/fit` composing `ITelemetrySource` + `Optimizer`
7. Chunked evaluation (Mode 6) — after the discovery pipeline works end-to-end

**Consequences:**
- Supersedes D-028 and D-041: Blazor is no longer a parallel-supported UI for new features; it remains supported for current functionality only.
- E-21 is now the only integration branch for expert-workbench work; Blazor equivalents will not be built. Feature requests that target Blazor are rejected unless they are bug fixes or contract syncs.
- Epic sequence lock: m-E18-XX Model Fit does not start until E-15 and Telemetry Loop & Parity have completed, because optimization against real telemetry requires measured drift bounds.
- Ownership shift: dag-map, density tokens, color architecture, TimelineScrubber, and the workbench-panel paradigm are Svelte-only design surfaces going forward. Blazor does not adopt them.
- `FlowTime.UI` (Blazor) remains in the repo and in CI; removal is not in scope of any current epic.
- When a Blazor-era doc or decision references "parallel UI" semantics, treat it as superseded by this decision.
