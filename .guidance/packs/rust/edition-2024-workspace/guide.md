# When working in Rust

**Version & house style:** read `Cargo.toml` (`edition`, `rust-version`) /
`rust-toolchain.toml` for the version; the repo's own conventions win. My engines use
**edition 2024** and a **Cargo workspace** (`resolver = "2"`) with version and dependencies
centralized in `[workspace.package]` / `[workspace.dependencies]`, referenced as
`{ workspace = true }`.

## Toolchain
- **`cargo fmt`** (default rustfmt, no custom config) and **`cargo clippy -- -D warnings`**
  — clippy clean with warnings denied (the cross-language zero-warnings default).
- **`cargo test`** with the built-in harness (`#[test]`, `#[cfg(test)] mod tests`); no extra
  test framework unless a crate genuinely needs one.
- Gate before handing work back: `cargo fmt --check` + `cargo clippy -- -D warnings` +
  `cargo test`.

## Serialization
- **serde** with `#[derive(Serialize, Deserialize)]` for wire types (json / yaml /
  messagepack via `rmp-serde`); name the wire shape explicitly and validate on read.

## Idioms — added best practice (prune anything you don't want)
- Errors via `Result<T, E>` and `?`; avoid `.unwrap()` / `.expect()` outside tests, or pair
  an `expect` with a message that states the invariant.
- Prefer a typed error enum — **`thiserror`** in libraries, **`anyhow`** at application edges.
  *(Neither is in your engine today; this is a suggestion, not your current setup.)*
- Borrow rather than clone on hot paths: take `&str` / `&[T]` in signatures, return owned
  types. Make illegal states unrepresentable with enums; exhaustive `match`, no catch-all
  `_` arm on domain enums.
- Keep a platform-neutral `core` crate free of I/O; put CLI/IO in the `cli` crate (your
  workspace already splits this way).
