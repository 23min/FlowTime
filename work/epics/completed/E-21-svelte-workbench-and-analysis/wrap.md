# Epic wrap — E-21 Svelte Workbench & Analysis Surfaces

**Date:** 2026-05-01
**Closed by:** Peter Bruinsma
**Integration target:** main
**Epic branch:** `epic/E-21-svelte-workbench-and-analysis`
**Merge commit:** `8d0d50b` (epic → main, 2026-05-01)

## Milestones delivered

- m-E21-01 Workbench Foundation — merged `b974958` (2026-04-17)
- m-E21-02 Metric Selector & Edge Cards — merged `6aad705` (2026-04-17)
- m-E21-03 Sweep & Sensitivity Surfaces — merged `ea62041` (2026-04-17; ultrareview follow-ups `dece926` 2026-04-20)
- m-E21-04 Goal Seek Surface — merged `8c4898f` (2026-04-22)
- m-E21-05 Optimize Surface — merged `a94fc66` (2026-04-22)
- m-E21-06 Heatmap View — merged `c742de3` (2026-04-26)
- m-E21-07 Validation Surface — merged `db6547f` (2026-04-28)
- m-E21-08 Visual Polish & Dark Mode QA — merged `a5d6a02` (2026-04-28)

## Summary

Transformed the Svelte UI from a Blazor-parallel clone into the primary platform for expert flow analysis and Time Machine surfaces. Workbench paradigm: topology as navigation + click-to-pin inspection panel; `/analysis` route with tabbed Time Machine surfaces (sweep, sensitivity, goal-seek, optimize); heatmap view alongside topology; validation panel + topology warning indicators; dark/light theme with calm chrome and vivid data-viz palette; topology keyboard + ARIA bar matching the heatmap; full bidirectional cross-link for nodes and edges; loading skeletons and a transitions rule. Eight milestones, no backend write-path additions (two read-only carve-outs ratified by ADRs D-2026-04-17-033 and D-2026-04-21-034). New chrome tokens introduced over the epic: `--ft-pin`, `--ft-highlight`, `--ft-warn`, `--ft-err`, `--ft-info`, `--ft-focus`. dag-map gained click/hover events + `selected` render option as a first-class library surface (general-purpose; not FlowTime-specific).

## ADRs ratified

Inline in epic spec (`work/epics/E-21-svelte-workbench-and-analysis/spec.md` § ADRs):

- ADR-E21-01 — New epic E-21 rather than resuming E-11
- ADR-E21-02 — dag-map click/hover in the library, not a wrapper
- ADR-E21-03 — `/analysis` tabbed surfaces (single route, not separate routes)
- ADR-E21-04 — Dashboard (E-11 M7) deferred — workbench + heatmap cover the ground
- ADR-E21-05 — Density pass is m-E21-01 scope, not polish

Plus inline milestone-scoped ADRs:

- ADR-m-E21-06-01 — typed `<ViewSwitcher>` (inline views, no registry)
- ADR-m-E21-06-02 — shared full-window 99p-clipped color scale (topology straight-swap from per-bin)

Per-decision references in `work/decisions.md`:

- D-2026-04-17-033 — read-only run-adjacent endpoints carve-out (`GET /v1/runs/{runId}/model`)
- D-2026-04-21-034 — additive `trace` field on `/v1/goal-seek` and `/v1/optimize`
- D-2026-04-26 — m-E21-06 → epic branch backfill merge
- D-2026-04-28 — color-blind validation + pattern encoding deferred to a follow-up (m-E21-08)

## Follow-ups carried forward

Active gaps in `work/gaps.md`:

- **Topology DAG keyboard / ARIA retrofit** — closed by m-E21-08 AC1 (entry can be archived).
- **Data-viz palette colour-blind validation** — open; deferred per user decision 2026-04-28. Pattern encoding (`--ft-pattern-encode` toggle) + simulator pass remain to plan.
- **Bidirectional card ↔ view selection (reverse cross-link)** — closed by m-E21-08 AC2 + AC3 (entry can be archived).
- **Heatmap sliding-window scrubber** — open; needs its own milestone (separate from E-21).
- **`transportation-basic` regressed: 3 `edge_flow_mismatch_incoming` warnings after E-24 unification** — open, **engine-side investigation required.** Reproduction artifact: `data/runs/run_20260428T165413Z_6ed5974e/run.json`. **Do not delete.** Filed during m-E21-08 dogfooding.
- **Tests are too weak: surveyed-output-only canaries cannot detect drift; need deterministic golden-output assertions** — open, planned to land **before E-22 Model Fit**. Filed during m-E21-08 dogfooding.

## Handoff

E-21 closes the Svelte fork decision (2026-04-15). Svelte is now the platform for new telemetry/fit/discovery surfaces; Blazor remains in maintenance mode at current functionality. The workbench paradigm — topology + click-to-pin cards + tabbed analysis + heatmap layered view + validation panel + bidirectional cross-link — is the foundation downstream UI work composes against.

**Unblocked successor work:**

- **E-22 Model Fit + Chunked Evaluation + `FlowTime.Pipeline` SDK** — the analysis-tab UI surface is now in place; once Engine-side fit lands, the Svelte `/analysis` route gains a Fit tab natively.
- **E-15 Telemetry Ingestion** — the `state_window` consumer-side type is now wide enough that telemetry-driven runs can surface warnings on the workbench without further client work.

**Deliberately left open:**

- Color-blind validation + pattern encoding (gap entry above; user decision 2026-04-28).
- Heatmap sliding-window scrubber for high-bin-count runs (gap entry above).
- Engine-side investigation of the `transportation-basic` warning regression (gap entry above; **must be triaged before treating other templates' warning sets as authoritative**).
- Golden-output testing-rigor milestone (gap entry above; **strongly recommended before E-22**).

**Test counts at epic close:**

- ui-vitest: **919 / 0** across 38 files (m-E21-01 baseline 217; m-E21-08 close 919; net +702 over the epic).
- `lib/dag-map/` node-test: **304 / 0**.
- svelte-check: **413 / 2** (pre-existing baseline; no regression introduced by any milestone).
- .NET build: clean.
- Playwright specs added by E-21: `svelte-workbench`, `svelte-analysis`, `svelte-analysis-followup`, `svelte-heatmap`, `svelte-validation`, `svelte-topology-a11y`, `svelte-node-selected`, `svelte-edge-selected`, `svelte-dark-mode`, `svelte-loading-skeletons`, `svelte-transitions`, `svelte-what-if` (E-17 carry-over). All pass in isolation; combined-spec runs surface intermittent flake from shared `page.route` mock state (tracked, not blocking).

**Workflow notes:**

- `wf-graph promote E-21 --to complete` was unavailable in this devcontainer (binary not on PATH). Status surfaces flipped manually: `work/graph.yaml` E-21 entry, epic spec frontmatter (`**Status:** complete` + `**Completed:** 2026-04-28`), `ROADMAP.md`, `work/epics/epic-roadmap.md`, `CLAUDE.md`. Atomic-write guarantee forfeit until wf-graph adoption — recorded as a workflow-rigor note.
- `/tmp` hygiene probe (Step 0.5) reported 5996 candidates — bulk are E-18 m-E18-14 CLI parity test artifacts (`/tmp/flowtime_cli_parity_*` dirs, prior epic) and tool cache; E-21-specific session leftovers (api-startup.log, ui-startup.log, page-all.diff, pw-a11y.log, analysis.html) cleaned. **Probe finding: 5 files cleaned; remaining ~5991 candidates are pre-existing prior-epic / tool-cache; not E-21 scratch.**
- Scratch cleaned (Step 5.5): `.ai-repo/scratch/` directory does not exist in this repo — nothing to clean. No orphan milestone subdirs.
- Doc-lint (Step 2.5) skipped — repo has no `docs/index.md` and the `wf-doc-lint` skill is repo-prepared but not yet bootstrapped here. No contract surfaces touched (`docs/architecture/contracts/` empty for this epic). Recorded as a workflow gap rather than blocking the wrap.
- Contract-verify gate (Step 2.25) — N/A. `.ai-repo/config/contracts.json` not present; consumer hasn't adopted schema-backed contracts yet.

## Doc findings

Skipped per Handoff note above (`docs/index.md` absent, `wf-doc-lint` not bootstrapped). Workflow gap recorded; not a contract-class drift.

## Contract-verify findings

N/A — `.ai-repo/config/contracts.json` absent; no contract bundles in scope.
