# Tracking: m-E20-07 .NET Subprocess Bridge

**Milestone:** m-E20-07
**Epic:** E-20 Matrix Engine
**Status:** complete
**Branch:** `milestone/m-E20-07-rust-bridge`

## Progress

| AC | Description | Status |
|----|-------------|--------|
| AC-1 | SHA256 hashing in Rust writer | done |
| AC-2 | RustEngineRunner in FlowTime.Core | done |
| AC-3 | Configuration switch | done |
| AC-4 | Integration tests (14 tests) | done |

## Files Changed

### Rust engine
- `engine/Cargo.toml` — added `sha2` workspace dependency
- `engine/core/Cargo.toml` — added `sha2` dependency
- `engine/core/src/writer.rs` — `write_artifacts_with_yaml()`, `manifest.json` writer, SHA256 helpers, 4 tests
- `engine/cli/src/main.rs` — `read_model_yaml()` helper, `eval` passes YAML text to writer

### C# bridge
- `src/FlowTime.Core/Execution/RustEngineRunner.cs` (new) — subprocess bridge + `RustEngineException`
- `src/FlowTime.API/Program.cs` — opt-in DI registration
- `src/FlowTime.API/appsettings.json` — `RustEngine` config section

### Tests
- `tests/FlowTime.Integration.Tests/RustEngineBridgeTests.cs` (new) — 14 integration tests

## Decisions

- D-2026-04-10-030 (active): SHA256 model + series hashing ported. Sim-specific provenance deferred.
- UTF-8 without BOM for YAML temp files (serde_yaml rejects BOM as multi-document).
- Case-insensitive series matching in parity tests (Rust lowercases topology node IDs).
