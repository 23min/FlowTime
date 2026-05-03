# Tracking: m-E20-01 Scaffold, Types, and Parsers

**Status:** completed
**Started:** 2026-04-09
**Epic:** [E-20 Matrix Engine](./spec.md)
**Milestone spec:** [m-E20-01-scaffold-types-parsers.md](./m-E20-01-scaffold-types-parsers.md)
**Branch:** `milestone/m-E20-01-scaffold-types-parsers` (off `epic/E-20-matrix-engine`)
**Baseline test count (main HEAD):** 1287 passed, 9 skipped, 0 failed

## Acceptance Criteria

- [x] **AC-1.** Rust workspace at `engine/` with `core/` (library) and `cli/` (binary) crates.
- [x] **AC-2.** Devcontainer Rust toolchain — `rustup`/`cargo` on `$PATH`, `cargo build`/`cargo test` work.
- [x] **AC-3.** Model types — all 21 C# model types as Rust structs with serde YAML deserialization.
- [x] **AC-4.** All existing model fixtures deserialize without error.
- [x] **AC-5.** Expression parser — recursive descent producing AST (5 variants).
- [x] **AC-6.** Expression parser parity — 11 test expressions from C# suite produce correct AST.
- [x] **AC-7.** Reference model fixtures at `engine/fixtures/` — graduated complexity levels.

## Commit Plan

- [x] **Status-sync** — branch, flip statuses, create this tracking doc.
- [x] **Bundle A+B+C** — All ACs delivered in single implementation commit: Rust workspace, model types with serde, 9 reference fixtures with deserialization tests, expression parser with 11 parity tests.
- [x] **Rule fix** — Epic integration branches changed from optional to required.
- [x] **Wrap** — tracking doc, status reconciliation.

## Implementation Log

### Status-sync — 2026-04-09

Branch `milestone/m-E20-01-scaffold-types-parsers` created off `main` HEAD (`c2fb67e`). Epic branch `epic/E-20-matrix-engine` created (same base). Status flipped across 6 surfaces.

### Bundle A+B+C — 2026-04-09

All ACs implemented in a single commit since the work was naturally coupled:

- **Rust workspace**: `engine/Cargo.toml` (workspace), `engine/core/` (flowtime-core lib), `engine/cli/` (flowtime-engine bin). Workspace dependencies: serde, serde_yaml, serde_json.
- **Model types**: 21 structs in `engine/core/src/model.rs` (~200 LOC). All fields use `#[serde(rename_all = "camelCase")]` + `#[serde(default)]`. `ParallelismValue` enum handles scalar/string disambiguation. `OutputDefinition.as` renamed to `as_name` with `#[serde(rename = "as")]`.
- **Fixtures**: 9 YAML files at `engine/fixtures/` copied from `examples/` and `fixtures/`: simple-const, hello, pmf, complex-pmf, class-enabled, http-service, microservices, order-system, retry-service-time.
- **Fixture tests**: 10 integration tests in `engine/core/tests/fixture_deserialization.rs` — 9 individual + 1 sweep. All pass.
- **Expression parser**: Recursive descent in `engine/core/src/expr.rs` (~300 LOC). 5 AST variants (Literal, ArrayLiteral, NodeRef, BinaryOp, FunctionCall). 4 binary operators. Handles nested expressions, arrays, unary minus, scientific notation.
- **Expression tests**: 12 unit tests covering all AC-6 parity expressions + error cases.
- **CLI**: Parses model YAML and prints summary (node/topology/grid counts).

### Rule fix — 2026-04-09

Epic integration branches changed from "optional" to required in `.ai-repo/rules/project.md` and `docs/development/branching-strategy.md`. Every numbered epic must have `epic/E-{NN}-<slug>`.

## Test Summary

- **Baseline (.NET):** 1287 passed, 9 skipped, 0 failed
- **Rust tests:** 24 passed, 0 failed (14 unit + 10 integration)
- **Build:** green (both .NET and Rust)

## Notes

- Rust 1.94.1 installed via `rustup`. Not yet persistent in devcontainer config — manual install for now. Future: add Rust feature to `.devcontainer/devcontainer.json`.
- `serde_yaml` 0.9.34 is marked deprecated upstream (maintenance mode). Works fine for our needs. If issues arise, `serde_yml` is the successor crate.
- Bundled A+B+C because the work was naturally sequential and small enough for one commit. Spec's tentative 3-bundle plan was overkill for the actual scope.

## Completion

- **Completed:** 2026-04-09
- **Final Rust test count:** 24 passed, 0 failed
- **Deferred items:** (none)
