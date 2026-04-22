# Doc-Gardening Log

Append-only record of lint and garden runs. Prefix convention: `## [YYYY-MM-DD HH:MM] <mode> | <trigger>` — greppable with `grep "^## \[" docs/log.md`.

## [2026-04-22 12:00] lint:full | bootstrap on-demand
- First run — no prior index. Bootstrapped `docs/index.md` from scratch.
- Index: 93 docs, 70 unique `authoritative_for` topics, 0 topic conflicts, 311 unique symbols in `by_symbol` reverse index.
- source_sha: 831b0a3
- docs_tree_hash: 156272c8
- doc_health: 81 (freshness 1.00, reference_integrity ~0.95, decision_currency 1.00, orphan_rate 0.957, coverage 0.15, conflict_rate 1.00)
- Findings: 8 (0 fix-now, all logged for follow-up garden session)
  - 4 orphan candidates (notes/*)
  - 1 doc TODO (docs/architecture/headless-engine-architecture.md:213 — Open Questions)
  - 3 template-drift candidates (docs still using `**Status:**` body line: run-provenance, supported-surfaces, template-draft-model-run-bundle-boundary)
  - 1 removed-feature-doc candidate (docs/development/TEMPLATE-tracking.md — template file, check if stale)
- Notes:
  - Agent-assisted scan. Per-doc `sections` field populated for 18/93 docs (mostly architecture/); other subtrees have `sections: —` placeholder. Coverage metric (0.15) reflects this shallow section-level indexing and is the primary lever for improving doc_health in future garden passes.
  - `reference_integrity` is an estimate; no exhaustive symbol cross-reference against `src/` was done. Spot-check suggested no broken references but a deep scan is a follow-up.
  - `decision_currency`: no docs were found citing superseded decisions, but the sweep was shallow.

## [2026-04-22 13:30] lint:scoped | wrap-milestone m-E21-04
- Change-set: milestone commits `5988f5c..HEAD` on `milestone/m-E21-04-goal-seek-optimize` (40 files, of which 2 docs under `docs/architecture/` and `docs/notes/` reference the touched endpoints).
- Index status: fresh (bootstrapped earlier today, source_sha 831b0a3).
- doc_health: 81 → 81 (Δ 0) — contract-drift finding resolved by fix-now; denominator is loose (reference_integrity is an estimate per bootstrap notes); no component recomputed at scoped precision.
- Findings: 1 (1 fix-now, 0 gap)
  - Contract drift: `docs/architecture/time-machine-analysis-modes.md` response examples for `POST /v1/goal-seek` (lines ~158-166) and `POST /v1/optimize` (lines ~194-202) omitted the new additive `trace` field landed under D-2026-04-21-034.
  - **Resolution — fix-now (human gate).** Human-gated decision on this wrap: contract-drift findings warrant a per-finding gate rather than a silent gap. Both examples extended in-place to include representative `trace` entries; trace-semantics paragraphs added under each response block pointing readers at D-2026-04-21-034 and the milestone spec for authoritative detail. Fix applied in the same wrap commit. Process gap filed as upstream `23min/ai-workflow#18` — wrap-milestone should promote contract-drift and removed-feature-doc findings to a human gate inside Step 3, instead of letting the subagent log/dismiss at its discretion.
- Notes:
  - `docs/notes/ui-optimization-explorer-vision.md` mentions optimize but is an exploration note — not contract truth; no fix required.
  - No removed-feature / superseded-decision / code-reference drift triggered by the milestone change-set.
