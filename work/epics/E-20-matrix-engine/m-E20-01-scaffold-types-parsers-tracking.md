# Tracking: m-E20-01 Scaffold, Types, and Parsers

**Status:** in-progress
**Started:** 2026-04-09
**Epic:** [E-20 Matrix Engine](./spec.md)
**Milestone spec:** [m-E20-01-scaffold-types-parsers.md](./m-E20-01-scaffold-types-parsers.md)
**Branch:** `milestone/m-E20-01-scaffold-types-parsers` (off `main`)
**Baseline test count (main HEAD):** 1287 passed, 9 skipped, 0 failed

## Acceptance Criteria

- [ ] **AC-1.** Rust workspace at `engine/` with `core/` (library) and `cli/` (binary) crates.
- [ ] **AC-2.** Devcontainer Rust toolchain — `rustup`/`cargo` on `$PATH`, `cargo build`/`cargo test` work.
- [ ] **AC-3.** Model types — all 21 C# model types as Rust structs with serde YAML deserialization.
- [ ] **AC-4.** All existing model fixtures deserialize without error.
- [ ] **AC-5.** Expression parser — recursive descent producing AST (5 variants).
- [ ] **AC-6.** Expression parser parity — 11 test expressions from C# suite produce correct AST.
- [ ] **AC-7.** Reference model fixtures at `engine/fixtures/` — graduated complexity levels.

## Commit Plan (tentative)

- [ ] **Status-sync** — branch, flip statuses, create this tracking doc.
- [ ] **Bundle A** — AC-1 + AC-2: Rust workspace scaffold, devcontainer Rust setup.
- [ ] **Bundle B** — AC-3 + AC-4 + AC-7: Model types, fixture deserialization, reference fixtures.
- [ ] **Bundle C** — AC-5 + AC-6: Expression parser + parity tests.
- [ ] **Wrap** — tracking doc, status reconciliation.

## Implementation Log

_Appended per bundle._

## Test Summary

- **Baseline (.NET):** 1287 passed, 9 skipped, 0 failed
- **Rust tests:** (none yet)
- **Build:** green

## Notes

_Decisions made, issues encountered, deviations from spec._

## Completion

- **Completed:** pending
- **Final Rust test count:** pending
- **Deferred items:** (none yet)
