# Retry + Service Time Fixture

Deterministic 4-bin slice that exercises retry attempts/failures, retry rate, and service time. CSVs mirror the `/state_window` goldens used in tests (5-minute bins starting at 2025-01-01T00:00Z).

- `model.yaml` references the CSVs in this folder for arrivals/served/errors/attempts/failures, retry echo/kernel, capacity, processing time, and queue depth.
- Topology edges include an attempts edge with `multiplier=2` and `lag=1` plus a failures edge with `multiplier=0.5`.

Quick use:
1. Copy this folder under your artifacts root as a run model (e.g., `data/runs/run_retry_fixture/model/`).
2. Provide `metadata.json`/`provenance.json` as needed (or reuse the test harness writers), then query `/v1/runs/run_retry_fixture/state_window?startBin=0&endBin=3` to see the server-computed edge slice.
3. Use the same run with the UI; the topology page consumes the server edge slice automatically when rendering retry overlays.
