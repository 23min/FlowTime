# Testing strategy

This repository separates tests by concern:

- Core unit and contract tests: `tests/FlowTime.Tests`
  - Scope: algorithms, graph, series, time grid, validation, determinism.
  - No web dependencies. Fast and deterministic.
- API slice tests: `tests/FlowTime.Api.Tests`
  - Scope: Minimal API endpoints, request/response shapes, happy and error paths.
  - Uses `WebApplicationFactory<Program>` from `Microsoft.AspNetCore.Mvc.Testing` to host the app in-memory.
- End-to-end (optional, future): `tests/FlowTime.E2E.Tests`
  - Scope: full system flows using the CLI or hosted API (potentially with Testcontainers).

## How to run

- All tests: use the workspace task "test" or run `dotnet test` from the repo root.
- Only core tests: run in `tests/FlowTime.Tests`.
- Only API tests: run in `tests/FlowTime.Api.Tests`.

## Guidelines

- Favor black-box tests at public boundaries. Keep internals flexible.
- Add a happy path and at least one boundary/negative case per feature.
- Keep API tests resilient by asserting on contract, not incidental formatting.
- When public behavior changes intentionally, update tests and document why in the PR.

### Upcoming (M1 / M1.5) Additions

Artifact & contract tests (landing with "Contracts Parity" milestone M1):

- JSON Schema validation for `run.json`, `manifest.json`, `index.json`.
- Hash stability tests: reordering YAML keys must not change `modelHash` / `scenarioHash`.
- Series integrity: recompute per-series hash from CSV bytes and match `manifest.json` values.
- Determinism: identical model + seed ⇒ identical hashes & series bytes (excluding time-based runId components if present).
- Negative: corrupt a CSV then run an integrity check expecting mismatch detection (future CLI command or test helper).

Expression tests (M1.5):
- Parser correctness (precedence, associativity, references).
- Built-ins (e.g., SHIFT) with boundary cases (shift beyond range → zeros or clamp as specified).

Backlog & latency tests (future M7):
- Conservation: cumulative(inflow) - cumulative(served) == backlog[last].
- Latency zero-division handling (served[t] == 0). Warning emission captured in manifest.

Synthetic adapter tests (SYN-M0):
- Round-trip: read artifacts → reconstruct series → byte-equal to originals.
- Hash agreement when adapter regenerates manifest.

## Notes

- The API project exposes `partial class Program` to enable `WebApplicationFactory` discovery.
- The `.http` file under `apis/FlowTime.API` contains example requests for manual checks.