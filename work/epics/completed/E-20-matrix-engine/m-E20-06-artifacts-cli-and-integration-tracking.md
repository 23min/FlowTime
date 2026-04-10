# Tracking: m-E20-06 Artifacts, CLI, and Integration

**Status:** complete
**Branch:** `milestone/m-E20-06-artifacts-cli-and-integration`
**Started:** 2026-04-09
**Completed:** 2026-04-09

## Acceptance Criteria

- [x] **AC-1:** CSV series writer (`bin_index,value` format, temp columns excluded) — 2 tests
- [x] **AC-2:** series/index.json (schema, grid, series list with points) — 1 test
- [x] **AC-3:** run.json (engineVersion, grid, warnings, series) — 1 test
- [x] **AC-4:** CLI `eval --output <dir>` writes artifacts — manual e2e verified
- [x] **AC-5:** CLI `validate` outputs JSON warnings — manual e2e verified
- [x] **AC-6:** Round-trip parity (CSV content + JSON structure verified) — writer tests
- [x] **AC-7:** All 113 original tests pass alongside 6 new tests — 119 total

## Test Summary

| Suite | Count | Status |
|-------|-------|--------|
| Core unit tests | 97 | pass |
| Fixture integration tests | 22 | pass |
| **Total** | **119** | **all pass** |

## Files Changed

- `engine/core/src/writer.rs` — new module: CSV writer, index.json, run.json
- `engine/core/src/lib.rs` — writer module registration
- `engine/cli/src/main.rs` — `--output` flag, `validate` command, improved eval output
