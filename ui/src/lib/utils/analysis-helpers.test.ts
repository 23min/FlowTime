import { describe, it, expect } from 'vitest';
import {
	discoverConstParams,
	discoverTopologyNodeIds,
	toSnakeCase,
	queueSeriesIds,
	generateRange,
	parseCustomValues,
	seriesMean,
	projectSweepMeans,
	sortByAbsGradient,
	maxAbsGradient,
	type SweepResponse,
	type SensitivityPoint,
} from './analysis-helpers.js';

describe('discoverConstParams', () => {
	it('returns [] for empty input', () => {
		expect(discoverConstParams('')).toEqual([]);
	});

	it('returns [] for non-string input', () => {
		// @ts-expect-error — deliberately passing wrong type
		expect(discoverConstParams(null)).toEqual([]);
	});

	it('returns [] on YAML parse error', () => {
		expect(discoverConstParams('nodes: [unclosed')).toEqual([]);
	});

	it('returns [] when top-level is not an object', () => {
		expect(discoverConstParams('just a string')).toEqual([]);
	});

	it('returns [] when nodes field is missing', () => {
		expect(discoverConstParams('grid: {bins: 4}')).toEqual([]);
	});

	it('returns [] when nodes is not an array', () => {
		expect(discoverConstParams('nodes: notAnArray')).toEqual([]);
	});

	it('extracts const params with baseline from values[0]', () => {
		const yaml = `
nodes:
  - id: arrivals
    kind: const
    values: [10, 10, 10, 10]
  - id: capacity
    kind: const
    values: [50, 50, 50, 50]
`;
		const params = discoverConstParams(yaml);
		expect(params).toHaveLength(2);
		expect(params[0]).toEqual({ id: 'arrivals', baseline: 10, values: [10, 10, 10, 10] });
		expect(params[1]).toEqual({ id: 'capacity', baseline: 50, values: [50, 50, 50, 50] });
	});

	it('skips nodes of other kinds', () => {
		const yaml = `
nodes:
  - id: a
    kind: const
    values: [1]
  - id: b
    kind: queue
    values: [2]
`;
		const params = discoverConstParams(yaml);
		expect(params).toHaveLength(1);
		expect(params[0].id).toBe('a');
	});

	it('skips const nodes with missing id', () => {
		const yaml = `
nodes:
  - kind: const
    values: [1, 2]
`;
		expect(discoverConstParams(yaml)).toEqual([]);
	});

	it('skips const nodes with non-string id', () => {
		const yaml = `
nodes:
  - id: 123
    kind: const
    values: [1]
`;
		expect(discoverConstParams(yaml)).toEqual([]);
	});

	it('skips const nodes with missing values array', () => {
		const yaml = `
nodes:
  - id: a
    kind: const
`;
		expect(discoverConstParams(yaml)).toEqual([]);
	});

	it('skips const nodes with empty values array', () => {
		const yaml = `
nodes:
  - id: a
    kind: const
    values: []
`;
		expect(discoverConstParams(yaml)).toEqual([]);
	});

	it('skips const nodes whose values[0] is non-finite', () => {
		const yaml = `
nodes:
  - id: a
    kind: const
    values: [.nan, 1]
`;
		// NaN encoded as .nan in YAML
		expect(discoverConstParams(yaml)).toEqual([]);
	});

	it('returns [] when yaml.load returns null', () => {
		expect(discoverConstParams('null')).toEqual([]);
	});

	it('returns [] when yaml.load returns an array (non-object top-level)', () => {
		// YAML list becomes an array, typeof is 'object' but .nodes is undefined → !Array.isArray(undefined) path
		expect(discoverConstParams('- a\n- b')).toEqual([]);
	});

	it('skips null / non-object entries', () => {
		const yaml = `
nodes:
  - null
  - id: b
    kind: const
    values: [2]
  - "string-entry"
`;
		const params = discoverConstParams(yaml);
		expect(params.map((p) => p.id)).toEqual(['b']);
	});
});

describe('generateRange', () => {
	it('generates an ascending range', () => {
		expect(generateRange(0, 10, 2)).toEqual([0, 2, 4, 6, 8, 10]);
	});

	it('handles from == to', () => {
		expect(generateRange(5, 5, 1)).toEqual([5]);
	});

	it('returns [] when step is zero', () => {
		expect(generateRange(0, 10, 0)).toEqual([]);
	});

	it('returns [] when step is negative', () => {
		expect(generateRange(0, 10, -1)).toEqual([]);
	});

	it('returns [] when from > to', () => {
		expect(generateRange(10, 5, 1)).toEqual([]);
	});

	it('returns [] when from is non-finite', () => {
		expect(generateRange(NaN, 10, 1)).toEqual([]);
	});

	it('returns [] when to is non-finite', () => {
		expect(generateRange(0, Infinity, 1)).toEqual([]);
	});

	it('returns [] when step is non-finite', () => {
		expect(generateRange(0, 10, NaN)).toEqual([]);
	});

	it('caps output at maxPoints', () => {
		const out = generateRange(0, 1000, 1, 5);
		expect(out).toHaveLength(5);
		expect(out).toEqual([0, 1, 2, 3, 4]);
	});

	it('uses default maxPoints of 200', () => {
		const out = generateRange(0, 10000, 1);
		expect(out).toHaveLength(200);
	});

	it('handles sub-1 step precisely', () => {
		const out = generateRange(0, 1, 0.25);
		expect(out).toEqual([0, 0.25, 0.5, 0.75, 1]);
	});

	it('does not overshoot "to" due to floating-point drift', () => {
		const out = generateRange(0, 0.3, 0.1);
		expect(out[out.length - 1]).toBeCloseTo(0.3, 10);
		expect(out).toHaveLength(4);
	});
});

describe('parseCustomValues', () => {
	it('parses comma-separated numbers', () => {
		expect(parseCustomValues('1, 2, 3')).toEqual([1, 2, 3]);
	});

	it('tolerates whitespace', () => {
		expect(parseCustomValues('  1 ,  2,3  ')).toEqual([1, 2, 3]);
	});

	it('drops empty entries', () => {
		expect(parseCustomValues('1,,2,')).toEqual([1, 2]);
	});

	it('drops non-numeric entries', () => {
		expect(parseCustomValues('1, foo, 3')).toEqual([1, 3]);
	});

	it('returns [] for empty input', () => {
		expect(parseCustomValues('')).toEqual([]);
	});

	it('returns [] for non-string input', () => {
		// @ts-expect-error
		expect(parseCustomValues(null)).toEqual([]);
	});

	it('returns [] for all-non-numeric', () => {
		expect(parseCustomValues('foo, bar')).toEqual([]);
	});

	it('accepts negative and decimal values', () => {
		expect(parseCustomValues('-1.5, 0, 2.75')).toEqual([-1.5, 0, 2.75]);
	});
});

describe('seriesMean', () => {
	it('returns mean of finite values', () => {
		expect(seriesMean([1, 2, 3, 4])).toBe(2.5);
	});

	it('ignores non-finite entries', () => {
		expect(seriesMean([1, NaN, 3, Infinity])).toBe(2);
	});

	it('returns NaN for empty array', () => {
		expect(seriesMean([])).toBeNaN();
	});

	it('returns NaN for all-non-finite array', () => {
		expect(seriesMean([NaN, Infinity, -Infinity])).toBeNaN();
	});

	it('returns NaN for non-array input', () => {
		// @ts-expect-error
		expect(seriesMean(null)).toBeNaN();
	});

	it('handles a single-value array', () => {
		expect(seriesMean([42])).toBe(42);
	});
});

describe('projectSweepMeans', () => {
	const response: SweepResponse = {
		paramId: 'arrivals',
		points: [
			{ paramValue: 10, series: { served: [8, 8, 8, 8], queue: [0, 0, 0, 0] } },
			{ paramValue: 20, series: { served: [16, 16, 16, 16], queue: [1, 2, 3, 4] } },
		],
	};

	it('projects means per series per point', () => {
		const m = projectSweepMeans(response);
		expect(m.get('served')).toEqual([
			{ paramValue: 10, mean: 8 },
			{ paramValue: 20, mean: 16 },
		]);
		expect(m.get('queue')).toEqual([
			{ paramValue: 10, mean: 0 },
			{ paramValue: 20, mean: 2.5 },
		]);
	});

	it('returns empty map when points is empty', () => {
		expect(projectSweepMeans({ paramId: 'x', points: [] }).size).toBe(0);
	});

	it('returns empty map when response is null/undefined', () => {
		// @ts-expect-error
		expect(projectSweepMeans(null).size).toBe(0);
	});

	it('returns empty map when points is not an array', () => {
		// @ts-expect-error
		expect(projectSweepMeans({ paramId: 'x', points: 'nope' }).size).toBe(0);
	});

	it('handles points whose series has a missing key', () => {
		const r: SweepResponse = {
			paramId: 'x',
			points: [
				{ paramValue: 1, series: { a: [1, 2] } },
				{ paramValue: 2, series: {} }, // missing 'a'
			],
		};
		const m = projectSweepMeans(r);
		expect(m.get('a')).toEqual([
			{ paramValue: 1, mean: 1.5 },
			{ paramValue: 2, mean: NaN },
		]);
	});
});

describe('sortByAbsGradient', () => {
	it('sorts by absolute magnitude descending', () => {
		const input: SensitivityPoint[] = [
			{ paramId: 'a', baseValue: 1, gradient: -2 },
			{ paramId: 'b', baseValue: 1, gradient: 5 },
			{ paramId: 'c', baseValue: 1, gradient: 1 },
		];
		expect(sortByAbsGradient(input).map((p) => p.paramId)).toEqual(['b', 'a', 'c']);
	});

	it('does not mutate input', () => {
		const input: SensitivityPoint[] = [
			{ paramId: 'a', baseValue: 1, gradient: 1 },
			{ paramId: 'b', baseValue: 1, gradient: 2 },
		];
		const before = [...input];
		sortByAbsGradient(input);
		expect(input).toEqual(before);
	});

	it('puts non-finite gradients last', () => {
		const input: SensitivityPoint[] = [
			{ paramId: 'a', baseValue: 1, gradient: NaN },
			{ paramId: 'b', baseValue: 1, gradient: 3 },
			{ paramId: 'c', baseValue: 1, gradient: Infinity },
		];
		const sorted = sortByAbsGradient(input);
		expect(sorted[0].paramId).toBe('b');
		// Non-finite relative ordering is unspecified but stable; both should come after 'b'.
		expect(sorted.slice(1).map((p) => p.paramId).sort()).toEqual(['a', 'c']);
	});

	it('returns empty array for empty input', () => {
		expect(sortByAbsGradient([])).toEqual([]);
	});

	it('handles all non-finite gradients', () => {
		const input: SensitivityPoint[] = [
			{ paramId: 'a', baseValue: 1, gradient: NaN },
			{ paramId: 'b', baseValue: 1, gradient: Infinity },
		];
		expect(sortByAbsGradient(input)).toHaveLength(2);
	});
});

describe('maxAbsGradient', () => {
	it('returns the max absolute value', () => {
		expect(
			maxAbsGradient([
				{ paramId: 'a', baseValue: 1, gradient: -3 },
				{ paramId: 'b', baseValue: 1, gradient: 1.5 },
			])
		).toBe(3);
	});

	it('ignores non-finite values', () => {
		expect(
			maxAbsGradient([
				{ paramId: 'a', baseValue: 1, gradient: NaN },
				{ paramId: 'b', baseValue: 1, gradient: 2 },
			])
		).toBe(2);
	});

	it('returns 0 for empty input', () => {
		expect(maxAbsGradient([])).toBe(0);
	});

	it('returns 0 when all gradients are non-finite', () => {
		expect(
			maxAbsGradient([
				{ paramId: 'a', baseValue: 1, gradient: NaN },
				{ paramId: 'b', baseValue: 1, gradient: Infinity },
			])
		).toBe(0);
	});
});

describe('discoverTopologyNodeIds', () => {
	it('returns [] for empty input', () => {
		expect(discoverTopologyNodeIds('')).toEqual([]);
	});

	it('returns [] for non-string input', () => {
		// @ts-expect-error
		expect(discoverTopologyNodeIds(null)).toEqual([]);
	});

	it('returns [] on parse error', () => {
		expect(discoverTopologyNodeIds('bad: [unclosed')).toEqual([]);
	});

	it('returns [] when top-level is not an object', () => {
		expect(discoverTopologyNodeIds('scalar')).toEqual([]);
	});

	it('returns [] when topology is missing', () => {
		expect(discoverTopologyNodeIds('nodes: []')).toEqual([]);
	});

	it('returns [] when topology is not an object', () => {
		expect(discoverTopologyNodeIds('topology: scalar')).toEqual([]);
	});

	it('returns [] when topology.nodes is missing', () => {
		expect(discoverTopologyNodeIds('topology: {edges: []}')).toEqual([]);
	});

	it('returns [] when topology.nodes is not an array', () => {
		expect(discoverTopologyNodeIds('topology:\n  nodes: scalar')).toEqual([]);
	});

	it('extracts node ids', () => {
		const yaml = `
topology:
  nodes:
    - id: Alpha
      kind: serviceWithBuffer
    - id: Beta
      kind: queue
  edges: []
`;
		expect(discoverTopologyNodeIds(yaml)).toEqual(['Alpha', 'Beta']);
	});

	it('skips entries without string id', () => {
		const yaml = `
topology:
  nodes:
    - id: 123
      kind: queue
    - id: Good
      kind: queue
    - kind: queue
`;
		expect(discoverTopologyNodeIds(yaml)).toEqual(['Good']);
	});

	it('skips null/non-object entries', () => {
		const yaml = `
topology:
  nodes:
    - null
    - id: X
      kind: queue
`;
		expect(discoverTopologyNodeIds(yaml)).toEqual(['X']);
	});

	it('skips entries with empty string id', () => {
		const yaml = `
topology:
  nodes:
    - id: ""
      kind: queue
    - id: Y
      kind: queue
`;
		expect(discoverTopologyNodeIds(yaml)).toEqual(['Y']);
	});
});

describe('toSnakeCase', () => {
	it('returns empty string for empty input', () => {
		expect(toSnakeCase('')).toBe('');
	});

	it('lowercases a single-word id', () => {
		expect(toSnakeCase('Queue')).toBe('queue');
	});

	it('inserts underscore before uppercase in camelCase', () => {
		expect(toSnakeCase('FooBar')).toBe('foo_bar');
	});

	it('handles consecutive uppercase as acronym', () => {
		// "DLQ" → "dlq"; "HTTPService" → "http_service"
		expect(toSnakeCase('DLQ')).toBe('dlq');
		expect(toSnakeCase('HTTPService')).toBe('http_service');
	});

	it('preserves non-letter characters unchanged', () => {
		expect(toSnakeCase('Queue_1')).toBe('queue_1');
		expect(toSnakeCase('Alpha-Beta')).toBe('alpha-_beta');
	});

	it('leaves lowercase-only ids unchanged', () => {
		expect(toSnakeCase('already_snake')).toBe('already_snake');
	});

	it('lowercase at start has no underscore prefix', () => {
		expect(toSnakeCase('fooBar')).toBe('foo_bar');
	});
});

describe('queueSeriesIds', () => {
	it('maps node ids to snake_case_queue series', () => {
		expect(queueSeriesIds(['Queue', 'Alpha', 'BetaNode'])).toEqual([
			'queue_queue',
			'alpha_queue',
			'beta_node_queue',
		]);
	});

	it('returns [] for empty input', () => {
		expect(queueSeriesIds([])).toEqual([]);
	});
});
