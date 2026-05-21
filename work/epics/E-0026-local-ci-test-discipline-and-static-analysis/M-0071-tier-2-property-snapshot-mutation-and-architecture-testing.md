---
id: M-0071
title: 'Tier 2: Property snapshot mutation and architecture testing'
status: draft
parent: E-0026
depends_on: [M-0070]
---

## Goal

Seed the four high-leverage test types FlowTime does not yet use: property-based, snapshot, mutation, and architecture. Adopt FsCheck or CsCheck (.NET) and proptest (Rust) for property tests; Verify.Xunit (.NET) and insta (Rust) for snapshot tests; Stryker.NET and cargo-mutants for mutation tests (manual / nightly CI); NetArchTest.Rules for architecture tests encoding selected CLAUDE.md truth-discipline guards. Land at least one representative test per primitive to prove the pattern in this codebase. Update CLAUDE.md to name when each test type is appropriate (test-type selection rule), the snapshot-update hygiene rule, and the strengthened TDD section. Coordinate with E-0025 M-0068 — if M-0068 has not yet shipped, M-0068 builds on Verify; if it has, M-0068's hand-rolled comparator is refactored onto Verify in this milestone.

## Acceptance criteria

<!-- Strawman AC list. Detailed AC bodies are filled in by aiwfx-plan-milestones before the milestone moves to in_progress. -->

- [ ] AC-1 — Property-based testing: FsCheck (or CsCheck) added to `tests/FlowTime.Core.Tests`; the choice is recorded with the rejected alternative named.
- [ ] AC-2 — Property-based testing: proptest added to `engine/core`.
- [ ] AC-3 — At least one representative property test for the conservation invariant on randomly-generated valid models.
- [ ] AC-4 — At least one representative property test for expression evaluator algebraic laws.
- [ ] AC-5 — At least one representative property test for binning math (round-trip, boundaries).
- [ ] AC-6 — Snapshot testing: Verify.Xunit added to `tests/FlowTime.Integration.Tests` with a representative test for run-artifact shape.
- [ ] AC-7 — Snapshot testing: insta added to `engine/core` with a representative test for parser/serializer output.
- [ ] AC-8 — Mutation testing: Stryker.NET configured for `FlowTime.Core` with documented config and floor.
- [ ] AC-9 — Mutation testing: cargo-mutants configured for `engine/core` with documented config and floor.
- [ ] AC-10 — Mutation tests run in a manual/nightly CI job (not per-commit); workflow file references the schedule.
- [ ] AC-11 — Architecture testing: NetArchTest.Rules suite encodes at least: `FlowTime.Core` does not depend on `FlowTime.UI`; classes in Adapters projects do not branch on `kind` / `logicalType` / file-stem heuristics; private-field naming convention.
- [ ] AC-12 — Each architecture-test method carries a one-line citation back to CLAUDE.md or the originating decision.
- [ ] AC-13 — CLAUDE.md test-discipline section: test-type selection rule added.
- [ ] AC-14 — CLAUDE.md test-discipline section: snapshot-update hygiene rule added.
- [ ] AC-15 — CLAUDE.md TDD section: strengthened to name when each test type is appropriate.
- [ ] AC-16 — Mutation-test baseline scores captured in milestone wrap; floor values committed in workflow file.
- [ ] AC-17 — Coordination with E-0025 M-0068: either Verify shipped before M-0068 starts, or M-0068's comparator refactored onto Verify in the same change as Verify adoption.
- [ ] AC-18 — Branch coverage on the new test infrastructure.
- [ ] AC-19 — Full repo test suite green at milestone close.
