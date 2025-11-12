# Metric Alias Authoring Guide

## Purpose

Metric aliases let template owners surface domain-specific terminology (e.g., “Ticket Submissions” or “Denied Claims”) while preserving FlowTime’s canonical metric names (`attempts`, `served`, `retryEcho`, …). Aliases are optional and live next to each topology node’s semantics. When present, the API and UI render the alias and fall back to the canonical label automatically.

## Supported Metrics

Aliases can be declared for any of the per-node metrics listed below (case-insensitive; underscores allowed). Stick to concise ASCII strings unless the template already uses extended characters.

| Canonical Key | Description |
| --- | --- |
| `arrivals` | Upstream demand entering the node |
| `served` | Successfully processed units |
| `errors` | Failures routed downstream |
| `attempts` | Total attempts/effort spent |
| `failures` | Attempts that failed |
| `retryEcho` | Deferred retries / retry mass |
| `queue` / `queueDepth` | Queue depth / backlog |
| `capacity` | Capacity series |
| `externalDemand` | Externally injected demand |
| `processingTimeMsSum` | Aggregated processing time |
| `servedCount` | Count used to derive service time |

> Tip: You can include multiple spellings in templates (e.g., `queue` and `queueDepth`); the engine normalizes lookups so UI/API consumers see the alias regardless of which spelling edges use.

## Authoring Checklist

1. **Pick the right nodes** — Focus on human-facing nodes (services, queues, routers) where the canonical terms do not match the business vocabulary.
2. **Add the `aliases` block** under each node’s `semantics` stanza. Example:

    ```yaml
    topology:
      nodes:
        - id: claims_router
          kind: service
          semantics:
            arrivals: file:ClaimsRouter_arrivals.csv
            served: file:ClaimsRouter_served.csv
            errors: file:ClaimsRouter_errors.csv
            attempts: file:ClaimsRouter_attempts.csv
            aliases:
              served: "Claims adjudicated"
              attempts: "Claim submissions"
              retryEcho: "Pending rework"
    ```

3. **Stay concise & ASCII** — Keep labels short, capitalized, and ≤ 32 characters. Use existing Unicode only when the template already depends on it.
4. **Validate locally** — Re-run `dotnet build FlowTime.sln` (or the template’s validation pipeline) so schema changes are caught early.
5. **Document intent** — If aliases encode regulated terminology, capture the reference in the template README or milestone so reviewers understand the mapping.

## Runtime Behavior

- `/v1/runs/{id}/graph`, `/state`, and `/state_window` now include an `aliases` dictionary per node.
- The topology inspector, dependency list, and canvas tooltips display the alias first and fall back to the canonical label if no alias exists.
- Goldens/tests enforce the new schema so future changes don’t regress the alias payloads.

## Errors vs. Failures (Retries)

- **`errors`** should capture *all* failed attempts that leave the service during that bin. This is the number you route to unresolved/backlog/DLQ nodes.
- **`failures`** is reserved for *internal retry attempts* that failed. Only wire this when the service actually spins an internal retry loop; leave it unmapped for services without retries.
- The UI uses `errors` for the general “Failed work” chip and `failures` for the retry loop chips (`Retries`, `Failed retries`, `Retry echo`). Keeping the semantics clean prevents duplicate/confusing readings in the inspector.

## Effort Edges (Support Workload)

Some downstream work scales with attempts rather than served throughput (analytics, fraud review, audit logging, etc.). Represent those relationships with **effort edges** in your topology:

- Set `type: effort` on the edge and provide a `multiplier` (and optional `lag`) describing how much dependent work each upstream attempt generates.

    ```yaml
    - id: effort_analytics
      from: IncidentIntake:out
      to: SupportAnalytics:in
      type: effort
      measure: load
      multiplier: 0.4   # 0.4 analytics tasks per intake attempt
      lag: 1            # optional delay in bins
    ```

- Effort edges **do not carry throughput**. They model supporting effort that is proportional to attempts, so keep using standard throughput edges for customer-facing flow between services/queues.
- The canvas renders effort edges as dashed blue lines (with multiplier labels when present) so operators can distinguish support workload from throughput.

Document why the template includes each effort edge (“Analytics load scales with ticket attempts,” etc.) so reviewers and operators understand the dependency semantics.

Use this guide whenever you add or update templates so operators always see the terminology they expect without compromising FlowTime’s canonical contracts.
