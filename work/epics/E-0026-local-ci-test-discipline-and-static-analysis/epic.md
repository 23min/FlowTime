---
id: E-0026
title: Local + CI Test Discipline and Static Analysis
status: proposed
---

# E-0026 — Local + CI Test Discipline and Static Analysis

## Goal

Bring FlowTime's static-analysis and test-discipline posture up to current best practice across .NET, Rust, and the Svelte UI. Concretely: turn on the toolchain-native quality gates that already exist (analyzers, warnings-as-errors, formatters, clippy, supply-chain advisories), seed the high-leverage test types the codebase does not yet use (property-based, snapshot, mutation, architecture), and make the existing CLAUDE.md process discipline (TDD, branch coverage, truth precedence) machine-checkable rather than self-policed. The result: `main` cannot break silently; new milestones inherit a CI surface that catches the kind of drift CLAUDE.md currently asks reviewers to catch by hand.

## Context

A toolchain audit in 2026-05 surveyed FlowTime against current best practice for .NET 9, Rust 2024, and SvelteKit and found a wide static gap between what the project asks of contributors (in CLAUDE.md) and what the project mechanically enforces. The audit covered: `Directory.Build.props`, `.editorconfig`, the Rust workspace `Cargo.toml`, `engine/`'s missing `[workspace.lints]` / `rustfmt.toml` / `clippy.toml` / `deny.toml` / `rust-toolchain.toml`, the test dependency surface (xUnit only — no FsCheck/CsCheck, no Verify, no Stryker, no NetArchTest; no proptest/insta/cargo-mutants on the Rust side), the pre-commit / pre-push hook chain, and `.github/workflows/build.yml`.

Findings, by tier of "cheapness × leverage":

**Tier 1 — toolchain hardening, broad coverage, near-zero ongoing cost.**

- `Directory.Build.props` does not set `TreatWarningsAsErrors`, `EnableNETAnalyzers`, `AnalysisLevel`, or `Nullable` repo-wide. The Roslynator analyzers are wired but gated behind `RoslynatorAnalyze=true` and only the dead-code skill loads them.
- The Rust workspace at `engine/Cargo.toml` declares no `[workspace.lints]` block; there is no `rustfmt.toml`, no `clippy.toml`, no `deny.toml`, no `rust-toolchain.toml`. clippy and `cargo fmt` are not enforced anywhere.
- The pre-commit hook does aiwf tree-discipline plus `STATUS.md` regeneration; the pre-push hook is empty. No format gate, no build gate, no clippy gate runs locally before push. `main` can break on push and only CI catches it.
- `.github/workflows/build.yml` runs `dotnet test` per-project with hang detection. It does **not** run `dotnet format --verify-no-changes`, does not enable analyzers, has **no Rust job at all** (the entire `engine/` workspace is invisible to CI), has **no Svelte UI job** (the `ui/` SvelteKit app's `svelte-check` / `vitest` / Playwright never run in CI), has no coverage gate, and has no supply-chain or vulnerability scan.

**Tier 2 — test types not yet present in the codebase that would have high leverage given FlowTime's domain.**

- **No property-based testing** anywhere. The conservation invariant ("for any valid model, sum of incoming edge flows equals node inflow at every bin") is exactly the kind of invariant that should be verified across a generated input space, not against three hand-coded examples. Same for expression evaluator algebraic laws and binning math. Candidate libraries: FsCheck or CsCheck on .NET (pick one); proptest on Rust.
- **No snapshot / golden-file testing.** M-0068 (Golden-Output Canary, in flight under E-0025) will hand-roll a fixture-comparison harness because Verify.Xunit / insta are not in the dep tree. Adopting Verify and insta would simplify M-0068's harness, give every future milestone the same primitive, and standardize the regeneration workflow CLAUDE.md hints at but has no convention for.
- **No mutation testing.** Stryker.NET (.NET) and cargo-mutants (Rust) measure how much of the test suite would actually catch a real bug. For analyzer-heavy code (`InvariantAnalyzer`, expression evaluator, schema validators), mutation score is the only honest answer to "did your tests really cover the branch you said they did."
- **No architecture testing.** CLAUDE.md's truth-discipline guards ("no FlowTime.Core project references FlowTime.UI", "no class in Adapters projects reconstructs semantic identity from file stems / kind / logicalType heuristics") are currently enforced by reviewer attention. NetArchTest can encode them as compiled tests.

**Tier 3 — niche but valuable.**

- **No fuzzing.** YAML / template parsers on both sides have surface-attack characteristics (untrusted file input, deeply-nested types, scalar coercion edge cases). cargo-fuzz on the Rust parser and SharpFuzz on the Sim template loader would harden them automatically.
- **No coverage threshold gate.** CLAUDE.md mandates "every reachable conditional branch needs a test before declaring done; perform a line-by-line audit before the commit-approval prompt." That rule is verifiable mechanically by `coverlet.collector` with a configured floor; today it is verified by humans.
- **No supply-chain or vulnerability scan.** Neither `dotnet list package --vulnerable` nor `cargo deny check advisories` runs in CI. Transitive CVE exposure is invisible.

**CLAUDE.md gap.** The CLAUDE.md test-discipline section names TDD, branch coverage, and the per-test-type running tactics, but does not yet name (a) when each test type is appropriate (example vs. property vs. snapshot vs. mutation), (b) snapshot-update hygiene (a hard rule will matter once M-0068's golden canary lands), or (c) what "branch coverage" means once a coverage gate exists.

**What this epic delivers.** Three sequenced milestones that close the three tiers, plus a CLAUDE.md update milestone that names the new conventions. Tier 1 is invisible at the API surface — it only stops `main` from breaking — and is the obvious lift to land first. Tier 2 introduces the property/snapshot/mutation/architecture primitives and seeds representative tests with each so future milestones have a pattern to follow. Tier 3 adds fuzzing, the coverage floor, and supply-chain scanning. The CLAUDE.md update can be folded into Tier 2 (so the snapshot-hygiene rule lands the same time as Verify) but is split out here so it can be discussed independently of the implementation.

### Coordination with E-0025

E-0025's M-0068 (Golden-Output Canary) and this epic's Tier 2 (which introduces Verify.Xunit and insta) overlap in scope. The cheapest sequencing is to land E-0026 Tier 2 **before** M-0068 starts implementation, so M-0068 builds on `Verify` instead of hand-rolling fixture comparison. If sequencing reverses (M-0068 ships first), Tier 2 becomes a refactor of M-0068's hand-rolled harness onto Verify. Both orderings work; the first is cheaper. E-0025 is not formally blocked on this epic — Tier 1 in particular can land in parallel with M-0067/M-0069 with no interaction.

## Scope

### In scope

- **Repo-wide .NET hardening.** `Directory.Build.props` sets `TreatWarningsAsErrors=true`, `EnableNETAnalyzers=true`, `AnalysisLevel=latest-recommended`, `Nullable=enable` on `src/` projects. Test projects (`tests/`) carry the analyzers but keep warnings as warnings to avoid friction during red-phase work. Pre-existing warnings are fixed in a single sweep; the codebase is small enough that a baseline-and-burn-down strategy is unnecessary.
- **Rust workspace hardening.** `engine/Cargo.toml` gains a `[workspace.lints]` block turning on clippy + clippy::pedantic at warn, `unsafe_code = forbid`. A `rust-toolchain.toml` pins the toolchain version and required components (`rustfmt`, `clippy`, `llvm-tools`). A `deny.toml` configures license + advisory checks. A `rustfmt.toml` and a `clippy.toml` fix workspace-wide style.
- **Local pre-push gate.** `.git/hooks/pre-push.local` (chained by aiwf, see CLAUDE.md "hooks compose rather than collide") runs `dotnet format --verify-no-changes`, `dotnet build -warnaserror`, `cargo fmt --check`, `cargo clippy --workspace -- -D warnings`. Hard constraint: pre-push completes in under 30s on the devcontainer. No tests run in pre-push — the cost is reserved for CI.
- **CI: format-and-analyze job.** A new CI job runs `dotnet format --verify-no-changes` and `dotnet build` with `RoslynatorAnalyze=true`. Fails the build on any analyzer or formatter complaint.
- **CI: Rust job.** A new CI job builds and tests the `engine/` workspace, runs `cargo fmt --check` and `cargo clippy --workspace -- -D warnings`, and runs `cargo deny check`.
- **CI: Svelte UI job.** A new CI job installs the `ui/` workspace, runs `svelte-check`, `vitest` once non-watch, and Playwright. Playwright follows the existing graceful-skip pattern from CLAUDE.md (skip cleanly when API/dev-server unavailable rather than fail).
- **Property-based testing seeded.** FsCheck (or CsCheck — milestone picks one) added to `tests/FlowTime.Core.Tests`. proptest added to `engine/core`. Representative property tests for: (a) the conservation invariant on randomly-generated valid models, (b) expression evaluator algebraic laws (associativity / commutativity where defined; identity laws), (c) binning math (`bin(t, binSize)` round-trip and boundary). The point is to seed the pattern and prove it works in this codebase, not exhaustively port every example test.
- **Snapshot testing seeded.** Verify.Xunit added to `tests/FlowTime.Integration.Tests` for run-artifact shape. insta added to `engine/core` for parser/serializer output. Adopted in lockstep across at least one representative test per surface so the regeneration workflow is exercised.
- **Mutation testing seeded.** Stryker.NET configured for `FlowTime.Core` (the analyzer-heavy code is the highest-ROI target). cargo-mutants configured for `engine/core`. Both run as a manual CI job (not per-commit) and as a nightly scheduled run; a baseline mutation score is established and a floor is documented (the actual floor value is set in the milestone, after the baseline is measured).
- **Architecture testing seeded.** NetArchTest.Rules suite encoding selected CLAUDE.md truth-discipline rules: e.g., `FlowTime.Core` does not depend on `FlowTime.UI`; classes in `*.Adapters.*` projects do not branch on `kind` / `logicalType` / file-stem heuristics to reconstruct semantic identity; private fields use camelCase without leading underscore (the analyzer rule that already lives in `.editorconfig`, but encoded as a test for visibility). The exact rule list is finalized inside the milestone — the constraint is that every rule has a one-line CLAUDE.md citation.
- **Fuzzing.** cargo-fuzz target added for the engine YAML / template parser. SharpFuzz target added for `FlowTime.Sim.Core`'s template loader. Both run as a nightly CI job.
- **Coverage gate.** coverlet.collector wired into CI; a coverage floor configured for `FlowTime.Core` (initial value chosen empirically — the milestone measures current coverage and sets the floor at the measured value, so the gate becomes a "do not regress" floor rather than an aspirational target).
- **Supply-chain scan.** `dotnet list package --vulnerable --include-transitive` runs in CI. `cargo deny check advisories` runs in CI. Both fail on any non-allowlisted advisory.
- **CLAUDE.md updates.** Test-type selection rule added to the test-discipline section: "If an AC is universally quantified over a domain, prefer a property test over example tests." Snapshot hygiene rule added: "Snapshot updates land in their own commit, separate from the code change that caused them, with a one-line justification in the commit body." TDD section strengthened to name when each test type is appropriate (example, property, snapshot, mutation).

### Out of scope

- **Reformatting the entire codebase to a new style.** The formatter gate enforces `dotnet format` and `cargo fmt` defaults; it does not introduce a new style. Pre-existing stylistic warnings are fixed; pre-existing deliberate style choices stand.
- **Replacing xUnit with another runner.** xUnit stays. The new test-type libraries integrate with xUnit; they do not replace it.
- **Replacing the existing per-project CI test layout.** The CI test jobs continue to use `dotnet test` per-project with `--blame-hang --blame-hang-timeout 60s`. The new CI jobs (format, Rust, UI, coverage, supply chain) run alongside the existing layout.
- **Performance benchmarking infrastructure.** `criterion` for Rust and `BenchmarkDotNet` for .NET would each be valuable, but they are a separate epic — they answer "how fast" rather than "is it correct," and their CI integration model is different (track-over-time vs. fail-on-threshold).
- **Promoting the InvariantAnalyzer warnings to CI gate.** That work is already filed as gap [G-0035](../../gaps/G-035-promote-invariantanalyzer-warnings-to-ci-gate.md) and is logically separate (it gates on engine output, not on toolchain). G-0035 may be addressed alongside or after this epic; it is not part of this epic's scope.
- **Refactoring tests to fix mutation-test surfaced gaps.** Establishing the baseline mutation score and the floor is in scope; using the score to drive new test-writing on uncovered branches is a follow-on gap (filed during the milestone if specific high-value gaps surface).
- **Replacing the `.git/hooks/pre-commit` STATUS-regen behavior.** The local pre-commit hook regenerates `STATUS.md` and tries to `git add` it; under the current `.gitignore` (`STATUS.md` un-tracked) the `git add` exits non-zero, breaking the hook chain. This is an upstream aiwf bug, filed separately, and is **not** part of this epic. (The epic's hook work is the new pre-push.local; pre-commit stays as installed.)
- **Multi-platform CI (Windows, macOS).** Linux-only CI matches the current matrix.
- **CI on every commit to non-`main` branches.** Workflow triggers stay as configured (`push: main`, `pull_request: main`).

## Constraints

- **Pre-push under 30 seconds on the devcontainer.** Hard wall-clock constraint. Format / build / clippy only — no tests in pre-push, ever.
- **Tier 1 lands in a single sweep.** Pre-existing analyzer warnings are fixed in the same milestone that turns on `TreatWarningsAsErrors`. No baseline-and-burn-down strategy. The codebase is small enough; deferring is more expensive than fixing.
- **No coexistence between gated and ungated states.** The format gate, the analyzer gate, the clippy gate are turned on once and stay on. No "land it on a branch and slowly enable per-project" pattern — that produces the same drift the gates exist to catch.
- **Mutation testing runs nightly, not per-commit.** Stryker.NET on `FlowTime.Core` and cargo-mutants on `engine/core` are slow (multi-minute). They are scheduled nightly and triggerable manually. Per-PR they run only on labelled PRs.
- **Fuzzing runs nightly.** Same reasoning. Fuzz targets are reproducible from the seed corpus stored in-repo; failures produce a minimized reproducer that is committed for replay.
- **Coverage floor is "do not regress," not aspirational.** The floor is set to the measured baseline minus a small buffer (e.g., 1pp). Raising the floor is a separate decision; lowering it requires a written justification in the commit body.
- **Supply-chain scan is allow-list governed.** Known-acceptable advisories (e.g., a transitive dependency the team has decided to live with for a documented reason) live in an allow-list file (`.github/supply-chain-allowlist.yaml` or per-tool config) with the decision recorded in a D-NNN entry.
- **CLAUDE.md updates land alongside the code that enforces them.** The snapshot-hygiene rule is added in the same commit that introduces Verify; the test-type selection rule is added in the same commit that introduces FsCheck. CLAUDE.md does not pre-announce conventions before the tooling exists.
- **No reintroduction of deprecated patterns.** Per CLAUDE.md: no snake_case JSON; no `binMinutes`; private fields camelCase without leading underscore. The architecture tests encode the project-level rules; the analyzers + `.editorconfig` encode the per-language rules.
- **Project rules.** .NET 9 / C# 13; invariant culture; camelCase JSON payloads.

## Success criteria

<!-- Reference-phrased; counts that drift over time are pulled from referenced lists, not reproduced inline. -->

- [ ] `Directory.Build.props` declares the four hardening properties (`TreatWarningsAsErrors`, `EnableNETAnalyzers`, `AnalysisLevel=latest-recommended`, `Nullable=enable`) for `src/` projects; `dotnet build FlowTime.sln` succeeds with zero warnings under the new configuration.
- [ ] `engine/Cargo.toml` declares `[workspace.lints]` with clippy + clippy::pedantic at warn and `unsafe_code = forbid`; `cargo clippy --workspace -- -D warnings` is clean. `engine/rust-toolchain.toml`, `engine/rustfmt.toml`, `engine/clippy.toml`, `engine/deny.toml` exist and are referenced by CI.
- [ ] `.git/hooks/pre-push.local` exists, is executable, and chains under aiwf's hook composition convention. The configured commands (format-verify, build-warnaserror, cargo fmt, cargo clippy) all run; total wall-clock under 30 seconds on the devcontainer at typical change-set sizes.
- [ ] `.github/workflows/build.yml` (or a sibling workflow file) runs every CI job referenced in this epic's *Scope → In scope* section; each job fails the build on its respective gate's failure mode.
- [ ] Every test-type primitive listed in *Scope → In scope* is present in the dep tree of at least one project, has at least one representative test demonstrating use, and has a one-line CLAUDE.md citation explaining when to use it.
- [ ] The mutation-test baseline score is captured in a milestone wrap artefact and the configured floor in CI is named in the workflow file. Both numbers are referenced from the wrap.md so future drift is visible.
- [ ] Architecture tests (NetArchTest) encode at least the truth-discipline rules listed in *Scope → In scope* under "Architecture testing seeded." Each test method has a one-line citation back to CLAUDE.md or the originating decision.
- [ ] Coverage floor is set in CI; the floor value is committed in the workflow file; a baseline measurement is captured in the milestone wrap artefact.
- [ ] Fuzz targets exist for both YAML/template parsers; nightly CI runs them; the seed corpus is committed; a reproducer-replay path is documented.
- [ ] Supply-chain scan runs in CI; the allow-list mechanism is in place; any advisory the team has decided to accept is named in a D-NNN entry referenced from the allow-list.
- [ ] CLAUDE.md contains the new test-type selection rule, the snapshot-hygiene rule, and the strengthened TDD section. Each rule is dated and references the milestone that introduced it.
- [ ] Full test suite green at epic close. No new flaky tests introduced. The CI run time at epic close is reported in `wrap.md` for posterity (the new jobs add cost; the cost is named).
- [ ] On epic completion, the epic frontmatter is promoted to `status: done` via `aiwf promote E-26 done`; `ROADMAP.md` is regenerated via `aiwf render roadmap --write`; a wrap artefact at `work/epics/E-26-local-ci-test-discipline-and-static-analysis/wrap.md` captures the baseline mutation score, the coverage floor, the new CI runtime, and any deferred follow-up gaps.

## Open questions

| Question | Blocking? | Resolution path |
|---|---|---|
| FsCheck or CsCheck for .NET property-based testing? | No | Decided inside the Tier 2 milestone after a brief comparison spike (5 minutes of dep-tree investigation; both libraries integrate with xUnit). Default leaning: FsCheck (more mature, larger community); CsCheck if the team prefers C# generators over F# `Arb`. |
| Initial coverage floor for `FlowTime.Core`? | No | Set inside the Tier 3 milestone at the measured baseline minus 1pp. The number is empirical, not aspirational. |
| Initial mutation-test floor for `FlowTime.Core` and `engine/core`? | No | Same approach: measure baseline in the Tier 2 milestone, set the floor at baseline minus a small buffer. Document the chosen buffer. |
| Do we set up a separate "nightly" CI workflow file, or use a scheduled trigger inside `build.yml`? | No | Decided inside the Tier 2 milestone. Strawman: a sibling `nightly.yml` so the day-job CI surface stays compact and readable. |
| Architecture-test rule list — fix it now or evolve over time? | No | Seed with the rules in *Scope → In scope* under "Architecture testing seeded"; the milestone authors them. New rules ship in subsequent milestones (or as patches) when CLAUDE.md gains new truth-discipline guards. |
| Are there pre-existing CLAUDE.md rules that should be encoded as architecture tests but aren't in the scope list yet? | No | Resolved inside the Tier 2 milestone via a re-read of CLAUDE.md against NetArchTest's primitive set. The bar is "encodable mechanically and worth catching at compile time" — not every rule qualifies. |
| Should `.editorconfig` analyzer codes be tightened beyond the current Roslynator subset? | No | Decided inside the Tier 1 milestone. If turning on `EnableNETAnalyzers` + `AnalysisLevel=latest-recommended` surfaces additional warnings worth keeping, they stay; if some are noise, they are suppressed in `.editorconfig` with a one-line citation. |
| Snapshot-test format — Verify default (`.received.txt` / `.verified.txt`), JSON, or YAML? | No | Decided inside the Tier 2 milestone alongside the M-0068 fixture-format choice (per the *Coordination with E-0025* note). Strawman: JSON-with-stable-key-order, matching what M-0068 will need anyway. |

## Risks

| Risk | Impact | Mitigation |
|---|---|---|
| Turning on `TreatWarningsAsErrors` repo-wide surfaces a wave of pre-existing warnings, expanding the Tier 1 milestone scope. | Med | The codebase is small enough (under 200 source files in `src/`) that a single sweep is realistic. The Tier 1 milestone budgets time for the sweep; if the warning surface turns out to be larger than a single milestone can absorb, the milestone splits the sweep into a follow-up milestone rather than the alternative of a baseline-and-burn-down (which historically rots). |
| The pre-push gate's wall-clock budget (under 30s) is impossible to hit on the devcontainer because cold `dotnet build` alone exceeds it. | Med | The gate uses `dotnet build` against a warm build cache (assume the developer just ran tests); first-push-after-clean is allowed to be slow. The 30s budget applies to the steady-state developer flow. If the budget is unachievable even warm, the gate drops `dotnet build` and keeps `dotnet format --verify-no-changes` + `cargo fmt --check` + `cargo clippy` — those alone catch most "broken main" pushes. |
| Mutation testing on `FlowTime.Core` is so slow nightly CI overruns the GitHub Actions free-tier window. | Low | `Stryker.NET` and `cargo-mutants` both support filtering and incremental mode. The nightly job runs against changed files only when run on PR, full sweep weekly. The exact schedule is set inside the Tier 2 milestone. |
| Adopting Verify / insta now diverges from the M-0068 fixture-format choice if M-0068 ships first. | Low | This epic flags the coordination explicitly. If M-0068 ships first with a hand-rolled comparator, the Tier 2 milestone refactors M-0068's harness onto Verify in the same change that introduces Verify. The refactor is mechanical — the comparator semantics are identical. |
| The new CI jobs push total CI time past the team's tolerance, slowing every PR. | Med | The Tier 1 milestone reports the new wall-clock at landing. If the budget is uncomfortable, the slowest jobs are split to a separate `slow.yml` triggered on a label or on `main` push only. The fast path stays fast. |
| Architecture tests catch a lot of pre-existing violations the project has never enforced, and the milestone budget overruns. | Med | The milestone scopes the rule list conservatively and ships only rules whose surface is already clean (or fixable in the same change). Rules that surface large existing violations are filed as gaps and shipped in follow-on milestones rather than blocking this epic. |
| The Rust workspace's missing `rust-toolchain.toml` means CI uses whatever the GitHub Actions image has installed; pinning a different version causes a one-time regression in CI baseline behavior. | Low | The Tier 1 milestone pins to whatever the developer is using locally (`rustc --version` from the devcontainer) and adjusts CI accordingly. The pin is captured in the wrap.md so future bumps are deliberate. |
| Supply-chain advisories include something the team must accept (transitive dep on a CVE'd version with no upgrade available). | Low | The allow-list mechanism handles this case by design. Each accepted advisory gets a D-NNN entry; the entry has an expiry condition (e.g., "drop when X upgrades to Y"). |
| The new pre-push.local hook conflicts with developers who push frequently to feature branches. | Low | The hook runs on `git push`, not on commit; small batches are fine. Developers who push every commit either accept the cost or skip with `--no-verify` per their judgment (and CLAUDE.md's hook policy: do not skip without a reason). |
| Aiwf's pre-commit hook regression (the current `STATUS.md` `git add` failure) blocks contributors from making any aiwf-managed commit. | Med | Out of scope for this epic. Filed as a separate gap; the workaround is documented in the gap. This epic's hook work installs a `pre-push.local`, not a `pre-commit.local`, so it does not interact with the broken pre-commit path. |

## Milestones

<!-- Sequencing rationale: Tier 1 lands first because it is broad-coverage cheap-cost and unblocks every later
     milestone (the analyzers and CI gates make later test-discipline work measurable). Tier 2 lands second because
     the new test types (property/snapshot/mutation/architecture) are higher-leverage than Tier 3 and because
     adopting Verify/insta in Tier 2 simplifies E-0025's M-0068 if Tier 2 lands first. Tier 3 lands third because
     fuzzing, the coverage floor, and supply-chain scan are valuable but their cost is justified once the cheaper
     gates have caught their share of bugs. CLAUDE.md updates land in lockstep with each milestone (each milestone
     ships the rule additions for its own primitives) so CLAUDE.md never references conventions that don't yet exist. -->

- [M-0070 — **Tier 1: Toolchain hardening + CI gates**](M-070-tier-1-toolchain-hardening-and-ci-gates.md) — Directory.Build.props hardening; Rust workspace lints + toolchain pinning; pre-push.local local gate; new CI jobs (format/analyze, Rust, Svelte UI). Pre-existing warnings fixed in the same milestone. Adds the snapshot-hygiene rule placeholder if Tier 2 hasn't landed yet (else punts to Tier 2). · depends on: —
- [M-0071 — **Tier 2: Property + snapshot + mutation + architecture testing**](M-071-tier-2-property-snapshot-mutation-architecture-testing.md) — FsCheck/CsCheck or proptest; Verify.Xunit and insta; Stryker.NET and cargo-mutants on a manual/nightly job; NetArchTest with the seeded rule list. CLAUDE.md updates: test-type selection rule, snapshot hygiene rule, strengthened TDD rule. Coordinate with E-0025 M-0068. · depends on: M-0070
- [M-0072 — **Tier 3: Fuzzing + coverage floor + supply-chain scan**](M-072-tier-3-fuzzing-coverage-floor-supply-chain-scan.md) — cargo-fuzz and SharpFuzz targets; coverlet.collector with floor; `dotnet list package --vulnerable` and `cargo deny check advisories` in CI; allow-list mechanism with first D-NNN entry seeded if needed. · depends on: M-0071

The CLAUDE.md updates are not a dedicated milestone — they ride along with each tier's milestone (each milestone updates the rules its primitives introduce). If the user wants a dedicated milestone for the CLAUDE.md sweep instead, the sequencing splits cleanly: M-0070 (Tier 1), M-0071 (Tier 2), M-0072 (Tier 3), M-0073 (CLAUDE.md sweep + final polish).

## References

- Gap [G-0035](../../gaps/G-035-promote-invariantanalyzer-warnings-to-ci-gate.md) — adjacent CI gate work; logically separate but motivates the same "make process discipline mechanical" framing this epic adopts.
- Epic [E-0025](../E-25-engine-truth-gate/epic.md) — Engine Truth Gate; M-0068's golden canary work overlaps with this epic's Tier 2 (Verify / insta). See *Context → Coordination with E-0025*.
- `Directory.Build.props` — current state; modified by M-0070.
- `.editorconfig` — current naming + Roslynator codes; expanded by M-0070.
- `engine/Cargo.toml` — current state (no `[workspace.lints]`); expanded by M-0070.
- `.github/workflows/build.yml` — current per-project test layout; new jobs added by every milestone in this epic.
- `.git/hooks/pre-commit`, `.git/hooks/pre-push` — current hook chain (aiwf-installed); this epic adds `.git/hooks/pre-push.local` and does not modify pre-commit.
- `CLAUDE.md` → "Hard Rules" section (TDD, branch coverage, branch discipline) — the human-policed rules this epic makes machine-checkable.
- `CLAUDE.md` → "Testing tactics" section — extended by this epic's CLAUDE.md updates with test-type selection guidance.
- `CLAUDE.md` → "Truth Discipline → Guards" — the source for NetArchTest rule encoding.
- `tests/FlowTime.Integration.Tests/TemplateWarningSurveyTests.cs` — existing canary; the `val-warn` / `run-warn` baseline mechanism is the prior art for the "lock down what the engine says" pattern this epic generalises.
- Audit findings (2026-05-05) — consolidated in this epic's *Context* section.
