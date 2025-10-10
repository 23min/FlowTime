# M3 Fixture Scripts

Helpers for running the M3.0 fixture pipeline.

- `run-fixture-tests.sh` – executes the fixture integration suite (`FixtureIntegrationTests`).
- `api-smoke.sh` – starts the FlowTime API (if needed), posts the order-system fixture, and inspects the run index.

Usage:
```
chmod +x run-fixture-tests.sh api-smoke.sh
./run-fixture-tests.sh
./api-smoke.sh   # requires dotnet runtime and API dependencies
```

The API smoke test relies on the fixtures under `fixtures/` and mirrors the legacy workflow with updated models.
