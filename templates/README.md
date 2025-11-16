# FlowTime Template Notes

FlowTime templates are authored in YAML and validated with [`docs/schemas/template.schema.json`](../docs/schemas/template.schema.json). Each template exposes a set of parameters that FlowTime-Sim accepts when instantiating `model.yaml`. The `flowtime telemetry bundle` CLI expects telemetry parameters to point at files inside a capture directory (for example `--telemetry-param telemetryRequestsSource=OrderService_arrivals.csv`).

PMF nodes can optionally reference a time-of-day profile to produce realistic deterministic curves. See [`docs/templates/profiles.md`](../docs/templates/profiles.md) for the schema and built-in library.

Telemetry workflow recap:

1. Capture canonical telemetry from an Engine run:  
   `flowtime telemetry capture --run-dir <engine run> --output <capture-dir>`
2. Generate a template-specific model via FlowTime-Sim (direct CLI or `scripts/time-travel/run-sim-template.sh`).
3. Bundle the capture + model for Engine consumption:  
   `flowtime telemetry bundle --capture-dir <capture-dir> --model <model.yaml> --output data/runs`

The sections below describe each curated template and the parameters you can override (including telemetry bindings).

## it-system-microservices

| Parameter | Type | Default | Notes |
|-----------|------|---------|-------|
| `bins` | integer | `6` | Number of time periods; forwarded to `grid.bins`. |
| `binSize` | integer | `60` | Minutes per bin; forwarded to `grid.binSize`. |
| `requestPattern` | array&lt;number&gt; | `[100, 150, 200, 180, 120, 80]` | Baseline arrivals for `user_requests`. |
| `loadBalancerCapacity` | array&lt;number&gt; | `[300, 300, 300, 300, 300, 300]` | Capacity for `load_balancer_capacity`. |
| `authCapacity` | array&lt;number&gt; | `[250, 250, 250, 250, 250, 250]` | Capacity for `auth_capacity`. |
| `databaseCapacity` | array&lt;number&gt; | `[180, 180, 180, 180, 180, 180]` | Capacity for `database_capacity`. |
| `telemetryRequestsSource` | string | `""` | Optional `file://` URI pointing at captured arrivals CSV (e.g. `file://telemetry/OrderService_arrivals.csv`). |

Telemetry JSON example:

```json
{
  "telemetryRequestsSource": "file:///workspaces/flowtime-vnext/data/telemetry/order-service_arrivals.csv"
}
```

## transportation-basic

Hub-and-spoke transit network with a central queue and airport retries.

| Parameter | Type | Default | Notes |
|-----------|------|---------|-------|
| `bins` / `binSize` | integer | `6` / `60` | Forwarded to the model grid. |
| `demandNorth`, `demandSouth` | array&lt;number&gt; | `[10, 14, 18, 22, 16, 12]` / `[8, 12, 16, 20, 14, 10]` | Origin arrivals. |
| `capNorthToHub`, `capSouthToHub` | array&lt;number&gt; | `[12, …]` / `[10, …]` | Feeder capacities. |
| `capHub`, `hubDispatchCapacity` | array&lt;number&gt; | `[20, 24, 32, 36, 28, 22]` / `[18, 22, 30, 32, 26, 20]` | Hub processing/dispatch limits. |
| `capAirport`, `capDowntown`, `capIndustrial` | array&lt;number&gt; | see template | Destination capacities. |
| `splitAirport`, `splitIndustrial` | number | `0.3` / `0.2` | Routing ratios out of the hub queue. |
| `airportRetryRate`, `airportRetryFailureRate` | number | `0.12` / `0.25` | Retry semantics surfaced on the Airport line. |
| `telemetryDemandNorthSource`, `telemetryDemandSouthSource` | string | `""` | Optional `file://` URIs for origin arrivals. |

Highlights:
- Origins feed `CentralHub`, which stages riders in `HubQueue` (backlog node) before dispatching downstream; there is no queue abandonment in this milestone so backlog is conserved.
- Airport line maps `attempts`, `failures`, and `retryEcho`, so UI retry chips mirror delivery behavior.

```json
{
  "telemetryDemandNorthSource": "file:///captures/North_arrivals.csv",
  "telemetryDemandSouthSource": "file:///captures/South_arrivals.csv"
}
```

## manufacturing-line

| Parameter | Type | Default | Notes |
|-----------|------|---------|-------|
| `bins` | integer | `6` | Time periods to simulate. |
| `binSize` | integer | `60` | Minutes per bin. |
| `rawMaterialSchedule` | array&lt;number&gt; | `[100, 100, 80, 120, 100, 90]` | Baseline arrivals for `raw_materials`. |
| `assemblyCapacity` | array&lt;number&gt; | `[90, 90, 90, 90, 90, 90]` | Assembly line capacity per bin. |
| `qualityCapacity` | array&lt;number&gt; | `[80, 80, 80, 80, 80, 80]` | Quality control capacity per bin. |
| `qualityRate` | number | `0.95` | Fraction of assembled items that pass quality checks. |
| `productionRate` | number | `12` | Base production rate used when calculating `packaging`. |
| `defectRate` | number | `0.05` | Fraction of items that become defective. |
| `telemetryRawMaterialsSource` | string | `""` | Optional telemetry CSV for `raw_materials`. |

```json
{
  "telemetryRawMaterialsSource": "file:///workspaces/flowtime-vnext/data/telemetry/raw_materials.csv"
}
```

## supply-chain-multi-tier

End-to-end purchase order flow with warehouse buffering, distributor backlog, and delivery retries.

| Parameter | Type | Default | Notes |
|-----------|------|---------|-------|
| `bins` | integer | `6` | Planning buckets (forwarded to `grid`). |
| `binSize` | integer | `60` | Minutes per bucket. |
| `demandPattern` | array&lt;number&gt; | `[80, 120, 160, 140, 100, 60]` | Purchase orders feeding `purchase_orders`. |
| `supplierCapacity` | array&lt;number&gt; | `[220, 220, 210, 195, 180, 150]` | Supplier ship limit. |
| `warehouseReleaseCap` | array&lt;number&gt; | `[190, 200, 205, 185, 160, 130]` | Warehouse release capacity. |
| `fulfillmentCapacity` | array&lt;number&gt; | `[170, 170, 165, 160, 150, 120]` | Distributor pull capacity (`queue_outflow`). |
| `deliveryCapacity` | array&lt;number&gt; | `[150, 155, 150, 140, 135, 110]` | Delivery fleet capacity. |
| `bufferMultiplier` | number | `1.15` | Supplier build-ahead multiplier. |
| `retryRate` | number | `0.18` | Fraction of pulled loads needing a retry. |
| `retryFailureRate` | number | `0.35` | Fraction of retry attempts that still fail. |
| `telemetryDemandSource` | string | `""` | Optional telemetry CSV for `purchase_orders`. |

Highlights:
- Supplier → Warehouse → DistributionQueue → Delivery mirrors the canonical topology, with aliases describing purchase orders, staged loads, backlog, and delivered units.
- `DistributionQueue` uses the backlog node (`queueDepth`) so the UI queue chip reflects accumulated staged loads (no shrink/attrition is modeled in this milestone).
- Delivery node surfaces retry semantics (`attempts`, `failures`, `retryEcho`) so retry chips render alongside total errors.

```json
{
  "telemetryDemandSource": "file:///workspaces/flowtime-vnext/data/telemetry/purchase_orders.csv"
}
```

## network-reliability

| Parameter | Type | Default | Notes |
|-----------|------|---------|-------|
| `bins` | integer | `12` | Time periods to simulate. |
| `binSize` | integer | `60` | Minutes per bin. |
| `rngSeed` | integer | `42` | RNG seed forwarded to the template `rng` block. |
| `baseLoad` | array&lt;number&gt; | `[100, 120, 150, 180, 200, 190, 170, 160, 140, 130, 110, 105]` | Baseline requests for `base_requests`. |
| `serverBaseCapacity` | integer | `150` | Base server capacity before performance variation. |
| `telemetryBaseLoadSource` | string | `""` | Optional telemetry CSV for `base_requests`. |

```json
{
  "telemetryBaseLoadSource": "file:///workspaces/flowtime-vnext/data/telemetry/base_load.csv"
}
```

### Manual authoring vs. automated bundling

When FlowTime-Sim runs in a different environment than Engine:

1. Generate the template model and provenance using Sim (`flowtime-sim` CLI or service).  
2. Copy the generated `model.yaml` and optional `provenance.json` to the Engine workspace.  
3. Run `flowtime telemetry bundle --capture-dir <capture> --model <model.yaml> --provenance <provenance.json> --output <data/runs>` to produce the canonical run directory.  
4. Share the resulting `data/runs/<runId>` directory with any consumers that call `/state`.

This keeps template ownership with Sim while ensuring Engine always consumes canonical bundles.

## supply-chain-incident-retry

Deterministic 24-hour IT operations incident workflow demonstrating retry semantics:

- Static example (no parameters); see `templates/supply-chain-incident-retry.yaml`.
- Service node exposes `arrivals`, `served`, `failures`, `attempts`, and computed `retryEcho` via `CONV(failures, [0.0, 0.6, 0.3, 0.1])`.
- Topology includes both throughput (served-driven queue inflow) and effort edges (attempt-driven analytics load with multiplier/lag).
- Useful for validating retry chips, inspector stacks, and effort edge rendering across API/UI layers.

| Parameter | Type | Default | Notes |
|-----------|------|---------|-------|
## Queues, SHIFT, and initial conditions (authoring guidance)

FlowTime still supports self‑referential `SHIFT` expressions, but catalog templates now prefer the dedicated backlog node. Provide inflow/outflow/loss series plus a topology seed and the engine maintains `queueDepth` automatically:

```yaml
topology:
  nodes:
    - id: DistributorQueue
      kind: queue
      semantics:
        arrivals: queue_inflow
        served: queue_outflow
        errors: queue_losses
        queueDepth: queue_depth
      initialCondition:
        queueDepth: 12

nodes:
  - id: queue_depth
    kind: backlog
    inflow: queue_inflow
    outflow: queue_outflow
    loss: queue_losses
```

If you hand-author `queue_depth := MAX(0, SHIFT(queue_depth, 1) + …)`, remember to keep `initialCondition.queueDepth`—the validator still requires it for legacy patterns.
