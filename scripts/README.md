# Development Scripts

This directory now splits the tooling into two tracks:

- `legacy/m2.10/` – archived API smoke-tests and YAML payloads from the M2.10 workflow (manual use).
- `m3/` – fixture-driven utilities that run the modern integration suite.

## Running Fixture Tests (M3)

```
cd scripts/m3
chmod +x run-fixture-tests.sh
./run-fixture-tests.sh
```

This script simply invokes the `FixtureIntegrationTests` inside `tests/FlowTime.Core.Tests`.

## Legacy Smoke Tests (M2.10)

The legacy scripts are still available for manual API verification if you need them (`test-api-integration.sh`, `test-download.sh`, associated YAML models). They may require additional setup and are not part of the automated M3 validation pipeline.
