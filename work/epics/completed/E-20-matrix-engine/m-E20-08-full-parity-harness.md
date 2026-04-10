# Milestone: Full Parity Harness

**ID:** m-E20-08
**Epic:** E-20 Matrix Engine
**Status:** in-progress
**Branch:** `milestone/m-E20-08-full-parity-harness` (off `main`)
**Depends on:** m-E20-07 (bridge, complete)

## Goal

Establish an automated parity test that runs every Rust engine fixture (21 models) through both the Rust and C# engines and compares series values. Produces a green/red matrix showing exactly which models match and which diverge, and where. This is the baseline before any engine core work.

## Context

m-E20-07 delivered the .NET subprocess bridge and 3 parity tests (simple const+expr, topology queue, negative/precision values). The E-20 spec promises a "full parity harness" across reference models. Only 3 of 21 fixtures are tested today — topology, routing, constraint, PMF, and class-enabled models are untested.

The `outputs:` filtering feature (YAML `outputs` section) is parsed by the Rust model but not used. Some models rely on it to select output series. This must be implemented for the harness to test those models correctly.

## Acceptance Criteria

1. **AC-1: `outputs:` filtering in Rust compiler.** When the model has an `outputs` section, the Rust engine filters its output to only include the listed series. The `as` field renames the series in the output. When no `outputs` section is present, all non-temp series are included (current behavior).

2. **AC-2: Parameterized parity test.** A single test method that:
   - Iterates over all `engine/fixtures/*.yaml` files
   - Evaluates each through the Rust engine (via `RustEngineRunner`)
   - Evaluates each through the C# engine (`ModelService.ParseAndConvert` → `RouterAwareGraphEvaluator.Evaluate`)
   - Compares shared series values bin-by-bin with configurable tolerance (default: 1e-10)
   - Uses case-insensitive series matching (Rust lowercases topology node IDs)
   - Reports per-fixture, per-series pass/fail with divergence details on failure

3. **AC-3: All non-class, non-edge fixtures pass parity.** The following fixtures must produce identical series values in both engines:
   - `hello.yaml`, `simple-const.yaml` — trivial models
   - `complex-pmf.yaml`, `pmf.yaml` — PMF nodes
   - `http-service.yaml` — expression-based service
   - `topology-simple-queue.yaml`, `topology-backpressure.yaml`, `topology-cascading-overflow.yaml`, `topology-wip-limit.yaml`, `topology-dispatch.yaml`, `topology-retry-echo.yaml` — topology models
   - `constraint-below-capacity.yaml`, `constraint-proportional.yaml` — constraint allocation
   - `router-weight.yaml`, `router-with-constraint.yaml` — weight-based routing
   - `retry-service-time.yaml` — retry kernels
   - `order-system.yaml`, `microservices.yaml` — complex multi-node models

4. **AC-4: Class and edge fixtures documented.** Fixtures that use classes (`class-enabled.yaml`, `router-class.yaml`, `router-mixed.yaml`) are tested but expected divergences are documented. The harness marks them as "known divergence — per-class decomposition not yet implemented" rather than failing the test run.

5. **AC-5: Parity matrix output.** The test run produces a clear summary (in test output or a generated report) showing pass/fail status for each fixture. This becomes the baseline for m-E20-09.

## Out of Scope

- Per-class column decomposition (m-E20-09)
- Edge series materialization (m-E20-09)
- Artifact layout changes (m-E20-10)
- Any changes to the C# engine

## Key References

- `engine/fixtures/` — 21 YAML fixtures
- `engine/core/src/model.rs` — `OutputDefinition` struct (parsed, not yet used)
- `tests/FlowTime.Integration.Tests/RustEngineBridgeTests.cs` — existing 14 bridge tests
- `work/gaps.md` — Rust Engine Parity section
