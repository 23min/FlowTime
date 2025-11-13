# FlowTime Template Notes

FlowTime templates are authored in YAML and validated with [`docs/schemas/template.schema.json`](../docs/schemas/template.schema.json). Each template exposes a set of parameters that FlowTime-Sim accepts when instantiating `model.yaml`. The `flowtime telemetry bundle` CLI expects telemetry parameters to point at files inside a capture directory (for example `--telemetry-param telemetryRequestsSource=OrderService_arrivals.csv`).

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

| Parameter | Type | Default | Notes |
|-----------|------|---------|-------|
| `bins` | integer | `6` | Time periods to simulate. |
| `binSize` | integer | `60` | Minutes per bin. |
| `demandPattern` | array&lt;number&gt; | `[10, 15, 20, 25, 18, 12]` | Baseline passenger arrivals. |
| `capacityPattern` | array&lt;number&gt; | `[15, 18, 25, 30, 22, 16]` | Vehicle capacity per bin. |
| `telemetryDemandSource` | string | `""` | Optional telemetry CSV for `passenger_demand`. |

```json
{
  "telemetryDemandSource": "file:///workspaces/flowtime-vnext/data/telemetry/passenger_demand.csv"
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

| Parameter | Type | Default | Notes |
|-----------|------|---------|-------|
| `bins` | integer | `6` | Time periods to simulate. |
| `binSize` | integer | `60` | Minutes per bin. |
| `demandPattern` | array&lt;number&gt; | `[80, 120, 160, 140, 100, 60]` | Customer demand for `customer_demand`. |
| `supplierCapacity` | array&lt;number&gt; | `[200, 200, 200, 200, 200, 200]` | Supplier production capacity. |
| `distributorCapacity` | array&lt;number&gt; | `[150, 150, 150, 150, 150, 150]` | Distribution centre capacity. |
| `retailerCapacity` | array&lt;number&gt; | `[120, 120, 120, 120, 120, 120]` | Retail capacity per bin. |
| — | — | — | This base template models direct flow without explicit buffers (served is capped at demand). For buffer modeling, see the warehouse variant below. |
| `telemetryDemandSource` | string | `""` | Optional telemetry CSV for `customer_demand`. |

```json
{
  "telemetryDemandSource": "file:///workspaces/flowtime-vnext/data/telemetry/customer_demand.csv"
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

## supply-chain-multi-tier-warehouse

This variant introduces an explicit warehouse buffer between Supplier and Distributor. The Supplier plans production with a buffer multiplier (e.g., 20% build-ahead), ships to the Warehouse, and downstream pulls from inventory subject to Distributor and Retailer capacities. SLA channels remain conservative: per-node `served` values are capped by the corresponding `arrivals`/pull, so ratios never exceed 1.0.

## supply-chain-incident-retry

Deterministic 24-hour IT operations incident workflow demonstrating retry semantics:

- Static example (no parameters); see `templates/supply-chain-incident-retry.yaml`.
- Service node exposes `arrivals`, `served`, `failures`, `attempts`, and computed `retryEcho` via `CONV(failures, [0.0, 0.6, 0.3, 0.1])`.
- Topology includes both throughput (served-driven queue inflow) and effort edges (attempt-driven analytics load with multiplier/lag).
- Useful for validating retry chips, inspector stacks, and effort edge rendering across API/UI layers.

| Parameter | Type | Default | Notes |
|-----------|------|---------|-------|
| `bins` | integer | `6` | Time periods to simulate. |
| `binSize` | integer | `60` | Minutes per bin. |
| `demandPattern` | array<number> | `[80, 120, 160, 140, 100, 60]` | Customer demand for `customer_demand`. |
| `supplierCapacity` | array<number> | `[200, 200, 200, 200, 200, 200]` | Supplier production capacity. |
| `distributorCapacity` | array<number> | `[150, 150, 150, 150, 150, 150]` | Distribution capacity. |
| `retailerCapacity` | array<number> | `[120, 120, 120, 120, 120, 120]` | Retail capacity. |
| `bufferSize` | number | `1.2` | Planned production multiplier feeding the warehouse. |
| `initialStock` | number | `0` | Initial warehouse inventory (bin 0). |
| `telemetryDemandSource` | string | `""` | Optional telemetry CSV for `customer_demand`. |

Key series:
- `planned_production` and `supplier_shipments` feed the warehouse.
- `warehouse_shipments` respects downstream pull (`customer_demand`) and capacities.

Topology nodes: Supplier (service) → Warehouse (router) → Distributor (service) → Retailer (service).

Notes:
- Arrays for demand/capacity parameters must contain exactly `grid.bins` values. If you raise `bins`, provide matching-length arrays or the engine throws at evaluation time.

## supply-chain-multi-tier-warehouse-1d5m

Fixed 24-hour variant of the warehouse template preconfigured for a 1‑day window with 5‑minute bins (288). Demand is shaped by hour and expanded to 5‑minute bins; capacities are constant across the day. No parameters — ready to run.

Metadata:
- Title: "Multi-Tier Supply Chain with Warehouse (1day, 5m)"
- Grid: `bins: 288`, `binSize: 5`, `binUnit: minutes`

Topology nodes: Supplier (service) → Warehouse (service) → Distributor (service) → Retailer (service).

---

## Queues, SHIFT, and initial conditions (authoring guidance)

FlowTime expressions support `SHIFT(series, k)` and it is safe for non‑self references (e.g., `SHIFT(demand, 1)`). To model an accumulating queue depth (`Q[t] = max(0, Q[t−1] + inflow[t] − outflow[t])`), a self‑reference is required. Templates must then:

1) Provide an initial condition in topology for the queue node (e.g., `initialCondition.queueDepth: 0`).
2) Define `queue_depth` from the previous bin:

```yaml
topology:
  nodes:
    - id: DistributorQueue
      kind: queue
      semantics:
        arrivals: queue_inflow
        served: queue_outflow
        queueDepth: queue_depth
      initialCondition:
        queueDepth: 0

nodes:
  - id: queue_depth
    kind: expr
    expr: "MAX(0, SHIFT(queue_depth, 1) + queue_inflow - queue_outflow)"
```

Note: As of this milestone, catalog templates avoid self‑`SHIFT` patterns to keep models simple. Several existing templates use per‑bin inflow/outflow (and sometimes a delta proxy) for queues rather than true accumulation. The snippet above shows the intended approach for accumulating backlog once we adopt it broadly in the catalog. If you use self‑`SHIFT`, ensure an initial condition is declared, or validation will fail.
