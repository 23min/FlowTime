# Template Authoring Guide

Designing FlowTime templates follows a predictable structure: metadata and parameters at the top, grid/window configuration, topology semantics, and the computational nodes/outputs that back those semantics. This guide captures the current best practices so new templates land with the correct wiring, retry governance semantics, and analyzer coverage.

## Directory Structure

- Author templates under `templates/`. Each template lives in its own YAML file.
- Deterministic fixture bundles (used for API/UI tests) live under `fixtures/`.
- Telemetry capture and provenance live under the generated run directories (`data/runs/<runId>`), so templates **never** reference absolute paths on the author’s machine.

## Required Sections

```yaml
schemaVersion: 1
generator: flowtime-sim
metadata:
  id: supply-chain-multi-tier
  title: Multi-Tier Supply Chain
  description: …
  version: 3.0.0
window:
  start: 2025-01-01T00:00:00Z
  timezone: UTC

parameters:
  - name: bins
    type: integer
    default: 288
    minimum: 12
    maximum: 288

grid:
  bins: ${bins}
  binSize: 5
  binUnit: minutes
```

- **metadata** must include a stable `id`, `title`, and semantic version. `generator` is always `flowtime-sim`.
- **parameters** expose user-controlled knobs with proper typing and bounds.
- **grid/window** define the evaluation domain. Always specify `binUnit`.

## Topology Semantics

Each operational node belongs in `topology.nodes`. Semantics reference the model nodes that produce each time series:

```yaml
topology:
  nodes:
    - id: Delivery
      kind: service
      semantics:
        arrivals: queue_outflow
        served: deliveries_served
        errors: delivery_errors
        attempts: delivery_attempts
        failures: retry_failures
        exhaustedFailures: exhausted_failures
        retryEcho: retry_echo
        retryKernel: [0.0, 0.6, 0.3, 0.1]
        retryBudgetRemaining: retry_budget_remaining
        capacity: delivery_capacity
        processingTimeMsSum: delivery_processing_time_ms_sum
        servedCount: deliveries_served
        maxAttempts: ${maxDeliveryAttempts}
        exhaustedPolicy: "returns"
        aliases:
          attempts: "Delivery attempts"
          failures: "Failed retries"
          retryEcho: "Retry backlog"
```

### Retry Governance Fields

TT‑M‑03.32 introduced three retry governance fields. When a service node supports retries:

- **`maxAttempts`** (literal or parameter) declares the policy. Use it whenever you emit `retryBudgetRemaining`.
- **`exhaustedFailures`** references the series capturing work that exceeded the budget. Connect this to a downstream DLQ/escalation node via a `terminal` edge:

  ```yaml
  edges:
    - id: delivery_to_returns
      from: Delivery:exhaustedFailures
      to: Returns:arrivals
      type: terminal
      measure: exhaustedFailures
  ```

- **`retryBudgetRemaining`** aggregates the unused attempt budget per bin. Example:

  ```yaml
  - id: retry_budget_capacity
    kind: expr
    expr: "delivery_base_load * ${maxDeliveryAttempts}"

  - id: retry_budget_remaining
    kind: expr
    expr: "MAX(0, retry_budget_capacity - delivery_attempts)"
  ```

Templates that declare `maxAttempts` **must** provide both `exhaustedFailures` and `retryBudgetRemaining` so analyzers and the UI stay consistent.

### Terminal Nodes

Whenever work becomes unrecoverable, model an explicit destination (queue, DLQ service, manual triage). Provide aliases for arrivals/served/queue metrics so the UI surfaces domain-specific labels.

## Computational Nodes and Outputs

- Keep transformation nodes (`expr`, `pmf`, `backlog`) in the `nodes:` section outside topology.
- Every semantic reference must point to a node defined here.
- Add outputs for the time series you want to export (CSV) or inspect:

  ```yaml
  outputs:
    - series: deliveries_served
      as: deliveries.csv
    - series: exhausted_failures
      as: exhausted_failures.csv
    - series: retry_budget_remaining
      as: retry_budget_remaining.csv
  ```

## Analyzer Expectations

The invariant analyzers (engine and sim) enforce:

- Non-negative arrivals/served/errors/attempts/failures/exhausted/budget series.
- `served <= arrivals`, `failures <= attempts`, `exhaustedFailures <= failures`.
- Queue/backlog conservation.
- Presence of `exhaustedFailures` and `retryBudgetRemaining` whenever `maxAttempts` is declared.

Violations surface as warnings in CLI output and will prevent templates from passing CI.

## Recommended Workflow

1. Edit the template YAML under `templates/`.
2. Generate a model to validate invariants:

   ```bash
   dotnet run --project src/FlowTime.Sim.Cli -- generate \
     --id supply-chain-multi-tier \
     --templates-dir templates \
     --out /tmp/supply-chain.yaml
   ```

   The CLI prints analyzer warnings (if any) immediately after generation.

3. Update fixtures (`fixtures/<template-id>/…`) so deterministic tests cover the new metrics.
4. Run `dotnet test FlowTime.sln` before opening a PR.

Documenting these steps keeps retries, terminal edges, and telemetry contracts consistent across all domains.***
