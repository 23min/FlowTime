---
id: M-033
title: Artifacts, CLI, and Integration
status: done
parent: E-20
---

## Goal

Add artifact writing (per-series CSVs, index.json, run.json) and complete the CLI so the Rust engine can be invoked as a standalone binary that reads a YAML model and produces a run directory with all output artifacts. This is the final milestone — after it, the Rust engine is a complete standalone replacement for the C# evaluation pipeline.

## Context

M-032 delivered derived metrics and invariant analysis. The engine now handles the full computation pipeline: parsing, compilation, evaluation, derived metrics, and warnings. 113 Rust tests passing.

The CLI already has `parse`, `plan`, and `eval` commands. The `eval` command prints series to stdout. This milestone extends `eval` to write structured artifacts to an output directory, matching the C# engine's output format.

## Acceptance Criteria

1. **AC-1: CSV series writer.** Write each named (non-temp) column as a CSV file:
   - Format: `bin_index,value\n` header, then `{t},{value}\n` per bin
   - File naming: `{seriesId}.csv` (using the column name from the column map)
   - Written to `{output}/series/` directory
   - Values formatted in invariant culture (`.` decimal separator, no thousands separator)

2. **AC-2: series/index.json.** Write a JSON index of all output series:
   - Schema: `{ "schemaVersion": 1, "grid": {...}, "series": [{id, path, points}] }`
   - Grid: bins, binSize, binUnit from the model
   - One entry per non-temp series, referencing its CSV path

3. **AC-3: run.json.** Write run metadata:
   - Schema: `{ "schemaVersion": 1, "engineVersion": "0.1.0", "grid": {...}, "warnings": [...], "series": [{id, path}] }`
   - Includes evaluation warnings from invariant analysis
   - Warning format: `{ "nodeId", "code", "message", "severity" }`

4. **AC-4: CLI eval --output flag.** Extend the `eval` command:
   - `flowtime-engine eval <model.yaml> --output <dir>` — evaluates and writes artifacts to `<dir>`
   - Creates `<dir>/series/` directory structure
   - Writes CSVs + index.json + run.json
   - Without `--output`, prints summary to stdout (existing behavior)
   - Exit code 0 on success, 1 on error

5. **AC-5: CLI validate command.** Add `validate` command:
   - `flowtime-engine validate <model.yaml>` — parses, compiles, and runs analysis without artifact writing
   - Prints warnings to stdout as JSON
   - Exit code 0 if no errors, 1 if compilation fails

6. **AC-6: Round-trip parity test.** End-to-end test:
   - Load a reference model fixture, run `eval --output`, verify the CSV contents match expected values
   - Verify index.json is valid JSON with correct series count
   - Verify run.json contains grid and warnings

7. **AC-7: Existing tests unbroken.** All 113 existing Rust tests still pass.

## Technical Notes

- **New module: `writer.rs`** in `flowtime-core` handles artifact writing. It takes an `EvalResult` + `ModelDefinition` and writes to a directory. The CLI calls this module.
- **CSV precision:** Use `{value}` default f64 formatting (full precision). The C# engine uses invariant culture which is equivalent.
- **No hashing in this milestone.** The C# engine produces SHA256 hashes for series, model, and scenario. Deferring hashing to keep scope tight — can add in a follow-up.
- **No manifest.json in this milestone.** The manifest includes RNG/provenance data that the Rust engine doesn't produce yet.
- **No per-class series output.** Per-class columns use internal naming (`__class_`) and are not written as separate artifacts yet.
- **Temp column filtering:** Columns starting with `__temp_` are internal intermediates and are not written.

## Out of Scope

- SHA256 hashing of artifacts — follow-up
- manifest.json (RNG, provenance) — follow-up
- Per-class series output — follow-up
- Parquet aggregate output — future
- .NET subprocess bridge wiring — future (separate integration work)
- stdin/stdout pipeline mode — future

## Dependencies

- M-032 complete (derived metrics, analysis, warnings)
