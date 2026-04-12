// Bundled example models for the What-If page.
//
// Each entry is a self-contained model YAML string plus metadata.
// These are static imports — no file I/O, no network.

export interface ExampleModel {
	id: string;
	name: string;
	description: string;
	yaml: string;
}

const SIMPLE_PIPELINE: ExampleModel = {
	id: 'simple-pipeline',
	name: 'Simple pipeline',
	description: 'Const arrivals feeding an expression. Tweak arrivals to see served scale.',
	yaml: `grid:
  bins: 4
  binSize: 1
  binUnit: hours
nodes:
  - id: arrivals
    kind: const
    values: [10, 10, 10, 10]
  - id: served
    kind: expr
    expr: "arrivals * 0.8"
`,
};

const QUEUE_WITH_WIP: ExampleModel = {
	id: 'queue-with-wip',
	name: 'Queue with WIP limit',
	description:
		'Service-with-buffer topology with a WIP cap. Drop the limit to see overflow increase.',
	yaml: `grid:
  bins: 6
  binSize: 1
  binUnit: hours
nodes:
  - id: arrivals
    kind: const
    values: [20, 20, 20, 20, 20, 20]
  - id: served
    kind: const
    values: [5, 5, 5, 5, 5, 5]
topology:
  nodes:
    - id: Queue
      kind: serviceWithBuffer
      wipLimit: 50
      semantics:
        arrivals: arrivals
        served: served
  edges: []
  constraints: []
`,
};

const CLASS_DECOMPOSITION: ExampleModel = {
	id: 'class-decomposition',
	name: 'Class decomposition',
	description:
		'Two classes competing for shared capacity. Tweak class rates to see the split.',
	yaml: `schemaVersion: 1
classes:
  - id: Order
  - id: Refund
grid:
  bins: 4
  binSize: 1
  binUnit: hours
nodes:
  - id: arrivals
    kind: const
    values: [10, 10, 10, 10]
  - id: served
    kind: expr
    expr: "MIN(arrivals, 8)"
traffic:
  arrivals:
    - nodeId: arrivals
      classId: Order
      pattern:
        kind: constant
        ratePerBin: 6
    - nodeId: arrivals
      classId: Refund
      pattern:
        kind: constant
        ratePerBin: 4
`,
};

const CAPACITY_CONSTRAINED: ExampleModel = {
	id: 'capacity-constrained',
	name: 'Capacity constrained',
	description:
		'Served forced equal to arrivals; exceeds capacity by default. Raise capacity or drop arrivals to clear the warning.',
	yaml: `grid:
  bins: 4
  binSize: 1
  binUnit: hours
nodes:
  - id: arrivals
    kind: const
    values: [15, 15, 15, 15]
  - id: capacity
    kind: const
    values: [10, 10, 10, 10]
  - id: served
    kind: expr
    expr: "arrivals"
topology:
  nodes:
    - id: Service
      kind: serviceWithBuffer
      semantics:
        arrivals: arrivals
        served: served
        capacity: capacity
  edges: []
  constraints: []
`,
};

// ─── Advanced demo models ────────────────────────────────────────────────────
// These exercise every E-17 feature: multi-node topology, edge heatmap, class
// decomposition, time-varying scrubber, and capacity-constraint warnings.

const SAAS_API_PLATFORM: ExampleModel = {
	id: 'saas-api-platform',
	name: 'SaaS API platform',
	description:
		'Three-stage pipeline (Gateway → Cache → Database). DB is the bottleneck — raise db_cap past 90 to clear the peak warning. Scrub through time to watch the queue build and drain.',
	yaml: `schemaVersion: 1
grid:
  bins: 8
  binSize: 1
  binUnit: hours
nodes:
  # Incoming request load — ramps up through the morning, peaks at bin 3, then winds down.
  # Vector param: shape is fixed; slide the capacity nodes to model infra changes.
  - id: requests
    kind: const
    values: [15, 35, 65, 90, 85, 60, 35, 15]

  # Per-stage capacity — each becomes a slider (uniform across bins).
  - id: gateway_cap
    kind: const
    values: [100, 100, 100, 100, 100, 100, 100, 100]

  - id: cache_cap
    kind: const
    values: [70, 70, 70, 70, 70, 70, 70, 70]

  # Database is the bottleneck: default cap (45) < peak requests (90).
  # Raise db_cap above 90 to clear the queue_depth_mismatch warning.
  - id: db_cap
    kind: const
    values: [45, 45, 45, 45, 45, 45, 45, 45]

  # Database tries to serve all incoming requests (no self-throttling).
  # When requests > db_cap, the engine raises a queue_depth_mismatch warning.
  - id: db_served
    kind: expr
    expr: "requests"

topology:
  nodes:
    - id: Gateway
      kind: serviceWithBuffer
      wipLimit: 200
      semantics:
        arrivals: requests
        served: gateway_cap

    - id: Cache
      kind: serviceWithBuffer
      wipLimit: 100
      semantics:
        arrivals: requests
        served: cache_cap

    - id: Database
      kind: serviceWithBuffer
      wipLimit: 60
      semantics:
        arrivals: requests
        served: db_served
        capacity: db_cap

  edges:
    - from: Gateway
      to: Cache
    - from: Cache
      to: Database

  constraints: []
`,
};

const ECOMMERCE_ORDER_PIPELINE: ExampleModel = {
	id: 'ecommerce-order-pipeline',
	name: 'E-commerce order pipeline',
	description:
		'Four-stage fulfilment (Intake → Validation → Payment → Shipping) with Standard and Express classes. Each stage chains to the next through throughput expressions. Payment is under-provisioned at peak — raise payment_cap above 100 to clear the warning and watch per-class series shift.',
	yaml: `schemaVersion: 1
classes:
  - id: Standard
  - id: Express
grid:
  bins: 10
  binSize: 1
  binUnit: hours
nodes:
  # Incoming order volume — midday peak at bin 5 (100 orders/hr).
  # Vector param (read-only in the panel); slide capacity nodes to model provisioning.
  - id: order_volume
    kind: const
    values: [15, 30, 50, 70, 90, 100, 85, 65, 40, 20]

  # Per-stage capacities — each becomes a slider.
  - id: intake_cap
    kind: const
    values: [120, 120, 120, 120, 120, 120, 120, 120, 120, 120]

  - id: validation_cap
    kind: const
    values: [110, 110, 110, 110, 110, 110, 110, 110, 110, 110]

  # Payment bottleneck: default cap (55) < peak orders (100) from bin 3 onward.
  # Raise payment_cap above 100 to clear the served_exceeds_capacity warning.
  - id: payment_cap
    kind: const
    values: [55, 55, 55, 55, 55, 55, 55, 55, 55, 55]

  - id: shipping_cap
    kind: const
    values: [100, 100, 100, 100, 100, 100, 100, 100, 100, 100]

  # Chained throughput expressions — each stage's actual output feeds the next.
  - id: intake_out
    kind: expr
    expr: "MIN(order_volume, intake_cap)"

  - id: validation_out
    kind: expr
    expr: "MIN(intake_out, validation_cap)"

  # Payment tries to serve everything arriving from Validation (no self-throttling).
  # When validation_out > payment_cap the engine raises a served_exceeds_capacity warning.
  - id: payment_served
    kind: expr
    expr: "validation_out"

  # Actual throughput leaving Payment — what Shipping receives.
  - id: payment_out
    kind: expr
    expr: "MIN(validation_out, payment_cap)"

# Class split: 80 % Standard, 20 % Express.
# The engine normalises these ratios against the actual per-bin order_volume.
traffic:
  arrivals:
    - nodeId: order_volume
      classId: Standard
      pattern:
        kind: constant
        ratePerBin: 8
    - nodeId: order_volume
      classId: Express
      pattern:
        kind: constant
        ratePerBin: 2

topology:
  nodes:
    - id: Intake
      kind: serviceWithBuffer
      wipLimit: 200
      semantics:
        arrivals: order_volume
        served: intake_cap

    - id: Validation
      kind: serviceWithBuffer
      wipLimit: 150
      semantics:
        arrivals: intake_out
        served: validation_cap

    - id: Payment
      kind: serviceWithBuffer
      wipLimit: 80
      semantics:
        arrivals: validation_out
        served: payment_served
        capacity: payment_cap

    - id: Shipping
      kind: serviceWithBuffer
      wipLimit: 150
      semantics:
        arrivals: payment_out
        served: shipping_cap

  edges:
    - from: Intake
      to: Validation
    - from: Validation
      to: Payment
    - from: Payment
      to: Shipping

  constraints: []
`,
};

export const EXAMPLE_MODELS: ExampleModel[] = [
	SIMPLE_PIPELINE,
	QUEUE_WITH_WIP,
	CLASS_DECOMPOSITION,
	CAPACITY_CONSTRAINED,
	SAAS_API_PLATFORM,
	ECOMMERCE_ORDER_PIPELINE,
];

export function findExampleModel(id: string): ExampleModel | undefined {
	return EXAMPLE_MODELS.find((m) => m.id === id);
}
