---
id: M-0072
title: 'Tier 3: Fuzzing coverage floor and supply chain scan'
status: draft
parent: E-0026
depends_on: [M-0071]
---

## Goal

Add the third tier of test discipline: fuzzing on YAML / template parsers (cargo-fuzz on the Rust engine, SharpFuzz on `FlowTime.Sim.Core`), a coverage floor enforced in CI (`coverlet.collector` with floor measured at baseline minus a documented buffer), and supply-chain advisory scanning (`dotnet list package --vulnerable` and `cargo deny check advisories`) with an allow-list mechanism for accepted advisories. Each accepted advisory gets a D-NNN entry with an expiry condition. After this milestone, the existing CLAUDE.md "every reachable conditional branch needs a test" rule has a machine-checkable backstop, parser robustness has automated probing, and transitive CVE exposure is visible to CI.

## Acceptance criteria

<!-- Strawman AC list. Detailed AC bodies are filled in by aiwfx-plan-milestones before the milestone moves to in_progress. -->

- [ ] AC-1 — cargo-fuzz target added for the engine YAML / template parser.
- [ ] AC-2 — SharpFuzz target added for `FlowTime.Sim.Core` template loader.
- [ ] AC-3 — Seed corpus committed under `engine/fuzz/corpus/` (or equivalent) with documented provenance.
- [ ] AC-4 — Fuzz targets run as a nightly CI job; failures produce a minimized reproducer that is committed for replay.
- [ ] AC-5 — `coverlet.collector` wired into CI; coverage report generated on every PR.
- [ ] AC-6 — Coverage floor configured for `FlowTime.Core`; baseline measured and floor set at baseline minus a documented buffer.
- [ ] AC-7 — Coverage floor commit references the baseline measurement and buffer rationale.
- [ ] AC-8 — `dotnet list package --vulnerable --include-transitive` runs in CI; fails on any non-allowlisted advisory.
- [ ] AC-9 — `cargo deny check advisories` runs in CI; fails on any non-allowlisted advisory.
- [ ] AC-10 — Allow-list mechanism in place (per-tool config or sibling YAML); accepted advisories carry a D-NNN entry with an expiry condition.
- [ ] AC-11 — At least one D-NNN entry seeded if the initial scan surfaces an advisory the team chooses to accept (else the milestone documents the empty-allow-list state and the policy for first additions).
- [ ] AC-12 — Branch coverage on the new infrastructure.
- [ ] AC-13 — Full repo test suite green at milestone close.
- [ ] AC-14 — Epic closure housekeeping: epic frontmatter promoted to `done` via `aiwf promote E-26 done`; `ROADMAP.md` regenerated; `wrap.md` captures baseline mutation score, coverage floor, new CI runtime, deferred follow-up gaps.
