---
id: M-070
title: 'Tier 1: Toolchain hardening and CI gates'
status: draft
parent: E-26
---

## Goal

Turn on the .NET / Rust / Svelte toolchain-native quality gates that already exist in the relevant ecosystems and that FlowTime currently leaves off. Concretely: enable analyzers, warnings-as-errors, formatters, and clippy across the repo; pin the Rust toolchain; install a `.git/hooks/pre-push.local` local gate (under 30s wall-clock); add format/analyze, Rust, and Svelte UI jobs to CI. After this milestone, `main` cannot break silently from a missed format pass, an unsuppressed analyzer warning, an unrun clippy, or a Rust workspace that built only on the developer's laptop. Pre-existing warnings are fixed in this milestone (single sweep, no baseline-and-burn-down).

## Acceptance criteria

<!-- Strawman AC list. Detailed AC bodies are filled in by aiwfx-plan-milestones before the milestone moves to in_progress. -->

- [ ] AC-1 — `Directory.Build.props` enables `TreatWarningsAsErrors`, `EnableNETAnalyzers`, `AnalysisLevel=latest-recommended`, `Nullable=enable` for `src/` projects.
- [ ] AC-2 — Test projects under `tests/` keep analyzers but warnings stay as warnings (not errors).
- [ ] AC-3 — All pre-existing analyzer warnings are fixed under the new configuration; `dotnet build FlowTime.sln` is warning-clean.
- [ ] AC-4 — `engine/Cargo.toml` declares `[workspace.lints]` (clippy + clippy::pedantic at warn, `unsafe_code = forbid`).
- [ ] AC-5 — `engine/rust-toolchain.toml` pins toolchain version + components (rustfmt, clippy, llvm-tools).
- [ ] AC-6 — `engine/rustfmt.toml`, `engine/clippy.toml`, `engine/deny.toml` exist with documented config.
- [ ] AC-7 — `cargo clippy --workspace -- -D warnings` is clean.
- [ ] AC-8 — `.git/hooks/pre-push.local` exists, is executable, runs `dotnet format --verify-no-changes` + `dotnet build -warnaserror` + `cargo fmt --check` + `cargo clippy --workspace -- -D warnings`, completes under 30s on the devcontainer at warm-cache change-set sizes.
- [ ] AC-9 — CI: a format-and-analyze job runs `dotnet format --verify-no-changes` and `dotnet build` with `RoslynatorAnalyze=true`.
- [ ] AC-10 — CI: a Rust job builds + tests `engine/`, runs `cargo fmt --check`, `cargo clippy --workspace -- -D warnings`, and `cargo deny check`.
- [ ] AC-11 — CI: a Svelte UI job runs `svelte-check`, `vitest` (single-pass), and Playwright with the existing graceful-skip pattern.
- [ ] AC-12 — Branch coverage on every gate change.
- [ ] AC-13 — Full repo test suite green at milestone close; new CI total wall-clock reported in milestone wrap.
