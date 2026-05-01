---
id: G-020
title: Ultrareview findings on `epic/E-21-svelte-workbench-and-analysis` (2026-04-20)
status: open
---

### Why this is a gap

The `/ultrareview` sweep against `main` for the E-21 epic branch surfaced four low-severity issues — one pre-existing path-traversal pattern in the Engine API and three nit-level UX/correctness defects on the new `/analysis` and topology surfaces. None block the milestone wrap; all are worth tracking so they do not decay into tolerated coexistence.

### Findings

1. **Path-traversal pattern on `GET /v1/runs/{runId}/model` (pre-existing class).**
   `src/FlowTime.API/Program.cs:1090-1117` passes `runId` straight to `Path.Combine(artifactsDirectory, runId)` with no validation. A `runId` of `..` resolves `runPath` outside the artifacts root; if a `model/model.yaml` happens to exist there, it is served as `text/yaml`. Bounded by ASP.NET's single-segment route constraint (no embedded `/`) and the fixed `model/model.yaml` suffix, but the defensive check is still missing. Same shape on sibling endpoints: lines 1071, 1126, 1188, 1224. Fix: extract a shared `GetRunDirectorySafe(artifactsDirectory, runId)` helper (regex allow-list on `runId`, or `Path.GetFullPath` + `StartsWith(artifactsDirectory)` canonicalisation) and apply to all call sites in one pass.

2. **`selectedSampleId` not persisted on `/analysis`.**
   `ui/src/routes/analysis/+page.svelte:229-255` persists `ft.analysis.tab`, `ft.analysis.source`, and `ft.analysis.infoHidden` to `localStorage` but not the chosen sample id. On reload with `sourceMode='sample'`, `selectedSampleId` resets to `SAMPLE_MODELS[0].id` and `onMount` silently loads that instead of the user's last choice. Fix: add `localStorage.setItem('ft.analysis.sample', id)` inside `loadSampleModel` and a matching read in `onMount`, with a `SAMPLE_MODELS.some(...)` guard against removed samples.

3. **Sweep silently truncated to 200 points with only generic `> 50` warning.**
   `ui/src/lib/utils/analysis-helpers.ts:58-79` caps `generateRange` output at `maxPoints=200` but the `/analysis` Sweep tab only emits `⚠ large sweep (> 50 points)`. A user entering `from=0/to=1000/step=1` (expecting 1001 points) sees `200 points` with the generic warning; 801 values are dropped with no distinct truncation signal. Fix: detect `out.length === maxPoints && (to - from)/step + 1 > maxPoints` and surface a dedicated `truncated — first 200 of N` indicator, or extend `generateRange` to return `{ values, truncated, requestedCount }`.

4. **Unescaped node ids in topology edge-highlight CSS selector.**
   `ui/src/routes/time-travel/topology/+page.svelte:108-120` interpolates `edge.from`/`edge.to` directly into a CSS attribute selector. A node id containing `"`, `\`, or `]` makes `querySelectorAll` throw `SyntaxError`; because the effect clears `.edge-selected` before the loop, one bad id both unselects existing edges and blocks all future highlight updates for the session. Fix: wrap interpolations with `CSS.escape()`, or wrap `querySelectorAll` in `try/catch` with a `console.warn` fallback.

### Status

Not scheduled. Candidate owners:

- Finding 1 → E-19 follow-up or a standalone patch alongside the deferred `POST /v1/run`/`POST /v1/graph` retirement; should close the whole class (all 5 call sites), not patch a single endpoint.
- Findings 2–4 → M-044 polish milestone, or a patch on the epic branch before epic wrap if priorities permit.

### Immediate implications

- Do not add new `Path.Combine(artifactsDirectory, {userInput})` call sites without the shared safe helper.
- Do not add new `localStorage`-backed UI state on `/analysis` without mirroring the sample-id persistence gap — persist all three axes together (tab, source, sample).
- Do not interpolate graph ids into CSS selectors elsewhere; prefer `CSS.escape` as the default for any new DAG-map handlers.
- Do not raise the `generateRange` cap above 200 without also fixing the truncation signal — a higher cap without a clearer indicator makes the silent-drop cliff worse, not better.

### Reference

- Remote review task id `rtdmj8ob8` (2026-04-20)
- `src/FlowTime.API/Program.cs:1071,1090,1126,1188,1224`
- `ui/src/routes/analysis/+page.svelte:172-199,229-266,601-605`
- `ui/src/lib/utils/analysis-helpers.ts:58-79`
- `ui/src/routes/time-travel/topology/+page.svelte:108-120`

---
