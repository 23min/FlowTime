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

export const EXAMPLE_MODELS: ExampleModel[] = [
	SIMPLE_PIPELINE,
	QUEUE_WITH_WIP,
	CLASS_DECOMPOSITION,
	CAPACITY_CONSTRAINED,
];

export function findExampleModel(id: string): ExampleModel | undefined {
	return EXAMPLE_MODELS.find((m) => m.id === id);
}
