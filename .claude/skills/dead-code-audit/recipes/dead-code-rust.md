---
name: rust
fileExts: [.rs]
excludePaths: [target/, .claude/worktrees/]
tool: clippy
toolCmd: "cargo clippy --manifest-path engine/Cargo.toml --workspace --all-targets --message-format=json -- -W dead_code -W unused_imports -W unused_variables"
---

# Dead-code recipe: Rust (clippy `dead_code`)

## Setup

`cargo clippy` is part of every Rust toolchain — no extra install needed. Verify clippy is available via `cargo clippy --version`. The toolCmd runs with `-W` (warn) on dead-code lints rather than `-D` (deny), so the build never fails on findings; the skill parses the warning stream.

If `engine/Cargo.toml` is not the workspace root (it is, currently), update the `--manifest-path` argument in this recipe.

## Things to look out for in this stack

- **`#[cfg(...)]`-gated code** — clippy lints only the active feature set. Code behind `#[cfg(feature = "x")]` may be flagged dead under default features but live under another feature flag. Check `engine/*/Cargo.toml` `[features]` tables before flagging.
- **`#[derive(...)]` macro-generated code** — derives expand at compile time; unused fields on derived types may be flagged but the derive (e.g., `Serialize`, `Deserialize`) reads them. Check for `serde` derives before flagging public struct fields.
- **`pub` items consumed only by tests** — items marked `pub` for testability but used only in `#[cfg(test)]` modules may look unused in the production build. Check `tests/`, `tests/integration/`, and inline `#[cfg(test)] mod tests {}` blocks.
- **FFI / `unsafe extern "C"`** — entry points called by non-Rust code (the .NET `SessionModelEvaluator` bridges to the Rust engine via stdin/stdout protocol) may not have a Rust-source caller. The `flowtime-engine session` binary's main protocol handler is the canonical example.
- **Trait dispatch via `Box<dyn Trait>` / generic bounds** — implementations may appear unused at the call site because the call resolves through a trait object. Check trait registry / DI patterns (`engine/core/src/`) before flagging an `impl Trait for Foo` block.
- **Workspace member crates with cross-crate consumers** — `engine/core/` is consumed by `engine/cli/` and any binary in `engine/`. Public items in `engine/core/src/` may have no in-crate caller but be reachable from sibling workspace members.
- **Bench / example targets** — `--all-targets` includes them; their entry points are conventionally `pub fn bench_*` or `pub fn main` in `examples/`, picked up by Cargo.

## Public surface notes

- **Session subprocess protocol** (`engine/cli/src/`) — the `flowtime-engine session` binary speaks MessagePack over stdin/stdout to the .NET `SessionModelEvaluator`. The protocol handlers and command dispatchers are the **only** items consumed by the .NET side; treat them as live regardless of in-Rust callers.
- **`engine/core/`** — public API consumed by `engine/cli/` and integration tests. Any public item without an in-Rust caller still in this crate may be on the protocol surface; check the .NET-side `SessionModelEvaluator` and `RustModelEvaluator` for matching commands before flagging.
- **CLI argument parsing** (clap-derived structs) — fields on argument structs are read by the parser via reflection-like macro expansion; appearing-unused is normal.

## Tool-specific notes

- **Lints enabled:** `dead_code` (unused functions, types, constants), `unused_imports`, `unused_variables`. Clippy's default lint set already includes these as `warn`; the explicit `-W` ensures they remain enabled regardless of crate-level `#![allow]` attributes (which the skill should flag separately if any are present and broad).
- **Suppress as too noisy:** `clippy::needless_lifetimes`, `clippy::module_inception` — style lints, not dead-code signal.
- **Output format:** JSON via `--message-format=json`. Each line is one diagnostic; filter for `"reason": "compiler-message"` and inspect `message.code.code == "dead_code"` (or `unused_imports`, etc.) and `message.spans[].file_name` + `line_start`.
- **Run scope:** `--workspace --all-targets` covers all member crates and all target kinds (lib, bin, test, example, bench). The skill's per-recipe filter narrows results to the milestone change-set after clippy runs.
- **Caveat on `#[allow(dead_code)]`:** if a file has a top-of-file `#![allow(dead_code)]` clippy will not flag anything in that file. Treat the presence of broad `allow(dead_code)` as itself a finding worth surfacing — the LLM should grep for these and report them in the blind-spot sweep.
