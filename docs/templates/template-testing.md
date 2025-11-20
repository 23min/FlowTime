# Template Testing Guide

This guide explains how to validate templates locally before handing them to engine/UI teams. It combines parameter validation, invariant analysis, and fixture verification so regressions are caught early.

## 1. Validate Parameters

Use the FlowTime Sim CLI to ensure default parameters and overrides are valid:

```bash
dotnet run --project src/FlowTime.Sim.Cli -- validate template \
  --id supply-chain-multi-tier \
  --templates-dir templates
```

- `--params overrides.json` applies a JSON object with parameter overrides.
- The command exits non-zero when required parameters are missing, out of bounds, or of the wrong type.

## 2. Generate + Analyze the Model

`generate` produces an engine-ready model and automatically runs the invariant analyzers (same logic used inside FlowTime.Sim.Service and FlowTime.API):

```bash
dotnet run --project src/FlowTime.Sim.Cli -- generate \
  --id supply-chain-multi-tier \
  --templates-dir templates \
  --mode simulation \
  --out /tmp/supply-chain.yaml
```

What to expect:

- Successful generation prints nothing (and writes `/tmp/supply-chain.yaml`).
- If invariants fail (e.g., `served > arrivals`, negative queues, missing retry budget), the CLI prints ⚠ warnings with node ids and bin numbers. Treat warnings as blockers.
- You can add `--verbose` to inspect metadata, mode, and whether the topology/window were included.

## 3. Verify Retry Governance Wiring

For templates that configure `maxAttempts`:

1. Ensure the model emits `exhaustedFailures` and `retryBudgetRemaining` series.
2. Mark DLQ nodes with `kind: dlq`. They behave like queues for telemetry, but analyzers expect every inbound/outbound edge to declare `type: terminal`. This prevents DLQs from rejoining the throughput graph.
3. Confirm terminal edges (type `terminal`, measure `exhaustedFailures`) flow to the expected DLQ/escalation node, including release edges leaving the DLQ.
4. Analyzer warnings (`missing_exhausted_failures_series`, `missing_retry_budget_series`, `dlq_non_terminal_inbound`, `dlq_non_terminal_outbound`) indicate misconfigured semantics.
5. When DLQs are modeled as pure backlogs (served = 0), the queue-latency analyzer emits informational messages (“latency could not be computed”). These are expected; the alternative would be to fake a served series, which misrepresents terminal behavior. Call them out in release notes so operators know they can ignore them.

## 4. Update Fixtures and Goldens

When template outputs change:

- Update the fixture bundle under `fixtures/<template-id>/` (CSV files + `model.yaml`) so deterministic tests reflect the new series.
- Re-run API/UI tests to refresh golden responses:

  ```bash
  dotnet test FlowTime.Api.Tests/FlowTime.Api.Tests.csproj
  dotnet test FlowTime.UI.Tests/FlowTime.UI.Tests.csproj
  ```

  Approved JSON files live under `tests/FlowTime.Api.Tests/Golden/…`.

## 5. Full Solution Tests

Before pushing:

```bash
dotnet build FlowTime.sln
dotnet test FlowTime.sln
```

- `dotnet test FlowTime.sln` runs unit/integration tests plus the template analyzers indirectly through fixture-based API/UI tests.
- Performance suite failures (e.g., `FlowTime.Tests.Performance.M2PerformanceTests.Test_PMF_vs_Const_Performance_Baseline`) are known and should be reported in review comments.

## 6. Document Analyzer Usage

When introducing new template features (retry governance, terminal edges, etc.), update the authoring/testing docs so future contributors can follow the correct steps. The CLI commands above are the canonical way to replicate analyzer output locally.
