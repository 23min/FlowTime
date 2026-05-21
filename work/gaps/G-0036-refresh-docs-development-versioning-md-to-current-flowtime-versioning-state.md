---
id: G-0036
title: Refresh docs/development/versioning.md to current FlowTime versioning state
status: open
---
## What's missing

`docs/development/versioning.md` is anchored on version `0.3.1` and the `UI-M-02.01` milestone (see the "Current Version" section and the per-csproj `VersionPrefix` example), both of which predate the v3 aiwf re-platform. Current FlowTime versioning has moved well past `0.3.x` — recent release notes under `docs/releases/` show 0.6.x/0.7.x — and milestone naming has shifted from `UI-M-NN.NN` to canonical `E-NNNN`/`M-NNNN` aiwf entity ids. The XML snippets cite stale literal version strings; the prose around them references milestone slugs that have been retired.

## Why it matters

This doc is the canonical reference for "what version should I set on this csproj?" and "how do I describe a milestone-driven release?". Out-of-date guidance produces version-format mistakes — milestone-completion PRs land with stale `VersionPrefix` values, releases get titled against retired milestone names, and the doc fails as a contract for any new contributor (human or AI) following the convention from cold. The cost accrues silently: every release surfaces a discrepancy that has to be reconciled by reading the doc, noticing it's wrong, and asking around or guessing from prior commits.
