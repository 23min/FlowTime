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

## Notes

- The API project exposes `partial class Program` to enable `WebApplicationFactory` discovery.
- The `.http` file under `apis/FlowTime.API` contains example requests for manual checks.