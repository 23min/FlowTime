# Milestone: .NET Subprocess Bridge

**ID:** m-E20-07
**Epic:** E-20 Matrix Engine
**Status:** complete
**Branch:** `milestone/m-E20-07-rust-bridge` (off `main`)

## Goal

Bridge the Rust `flowtime-engine` binary into the .NET API as a subprocess call, with SHA256 hashing for provenance (per D-2026-04-10-030), and a configuration switch to run the Rust engine alongside (not replacing) the C# engine.

## Context

m-E20-06 delivered the Rust CLI binary and artifact writer (CSVs, index.json, run.json). The E-20 spec lists the ".NET subprocess bridge" and "full parity harness" as in-scope deliverables (spec line 66). The epic was marked complete before these were built. This milestone delivers the bridge and foundational parity tests.

D-2026-04-10-030 established the provenance strategy: port SHA256 basics (model hash + per-series hashes in manifest.json) as part of the bridge work. Sim-specific provenance is deferred until the bridge is exercised by real Sim runs.

## Acceptance Criteria

1. **AC-1: SHA256 hashing in Rust writer.** The Rust artifact writer computes:
   - SHA256 of the raw model YAML text (model hash)
   - SHA256 of each series CSV file (series hashes)
   - Writes `manifest.json` with `modelHash` and per-series `hash` fields
   - Hash format: `"sha256:{hex}"`
   - When no YAML text is available, `modelHash` is `null`

2. **AC-2: RustEngineRunner in FlowTime.Core.** A subprocess bridge class that:
   - Writes model YAML to a temp file (UTF-8, no BOM)
   - Invokes `flowtime-engine eval <model.yaml> --output <dir>`
   - Reads back `run.json`, `manifest.json`, and series CSVs
   - Returns typed DTOs (grid, warnings, series values, manifest with hashes)
   - Cleans up temp directory on both success and failure (finally block)
   - Configurable process timeout (default 60s) with process tree kill on expiry
   - Clean `RustEngineException` for missing binary, non-zero exit, and timeout

3. **AC-3: Configuration switch.** Opt-in via `appsettings.json`:
   - `RustEngine:Enabled` (default `false`)
   - `RustEngine:BinaryPath` (default: auto-discover from solution root)
   - DI registration in `Program.cs` when enabled
   - Does not replace C# evaluation path — both engines available side by side

4. **AC-4: Integration tests.** At least one parity test plus error coverage:
   - C#/Rust parity on simple const+expr model
   - C#/Rust parity on topology model (serviceWithBuffer queue)
   - C#/Rust parity on negative/precision values
   - Empty model (0 nodes)
   - Invalid YAML error handling
   - Binary not found error handling
   - Process timeout handling
   - Temp directory cleanup on success and failure
   - Manifest hash presence and determinism

## Out of Scope

- Replacing the C# evaluation path in `POST /v1/run`
- Sim-specific provenance fields (template IDs, parameter bindings)
- Plan hashing (deferred to E-17/E-18)
- Per-class output support
- Full C# parity harness across all fixture models

## Test Summary

- 4 Rust unit tests (manifest structure, hash determinism, null hash, SHA256 correctness)
- 14 C# integration tests (see AC-4)
- All 123 existing Rust tests pass
- All 1,301 existing .NET tests pass
