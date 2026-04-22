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
