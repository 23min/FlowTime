import { describe, it, expect } from 'vitest';
import {
	defaultMetric,
	availableMetrics,
	seriesMean,
	buildMetricMap,
	seriesRange,
	normalizeMetricMap,
	type MetricMap,
} from './topology-metrics';
import type { EngineGraph } from './engine-session';

// ── seriesMean ──

describe('seriesMean', () => {
	it('returns 0 for empty series', () => {
		expect(seriesMean([])).toBe(0);
	});

	it('returns the value for single-element series', () => {
		expect(seriesMean([42])).toBe(42);
	});

	it('computes arithmetic mean', () => {
		expect(seriesMean([1, 2, 3, 4])).toBe(2.5);
	});

	it('handles negative values', () => {
		expect(seriesMean([-1, 0, 1])).toBe(0);
	});

	it('handles floating-point values', () => {
		expect(seriesMean([1.5, 2.5])).toBe(2);
	});
});

// ── defaultMetric ──

describe('defaultMetric', () => {
	it('returns the first non-internal series name', () => {
		const series = { served: [1, 2], arrivals: [3, 4] };
		expect(defaultMetric(series)).toBe('served');
	});

	it('skips internal series', () => {
		const series = { __temp_0: [1, 2], served: [3, 4] };
		expect(defaultMetric(series)).toBe('served');
	});

	it('returns null when no non-internal series exist', () => {
		expect(defaultMetric({ __temp_0: [1] })).toBeNull();
	});

	it('returns null for empty series', () => {
		expect(defaultMetric({})).toBeNull();
	});
});

// ── availableMetrics ──

describe('availableMetrics', () => {
	it('returns all non-internal series names', () => {
		const series = { arrivals: [1], served: [2], __temp_0: [3] };
		const metrics = availableMetrics(series);
		expect(metrics).toContain('arrivals');
		expect(metrics).toContain('served');
		expect(metrics).not.toContain('__temp_0');
	});

	it('returns empty array when only internal series', () => {
		expect(availableMetrics({ __temp_0: [1], __edge_x: [2] })).toEqual([]);
	});
});

// ── buildMetricMap ──

describe('buildMetricMap', () => {
	const graph: EngineGraph = {
		nodes: [
			{ id: 'arrivals', kind: 'const' },
			{ id: 'served', kind: 'expr' },
			{ id: 'Queue', kind: 'servicewithbuffer' },
		],
		edges: [],
	};

	it('assigns each node its own series mean', () => {
		const series = {
			arrivals: [10, 10, 10],
			served: [8, 8, 8],
			// Topology queue column: queue_column_id("Queue") = "queue_queue"
			queue_queue: [1, 2, 3],
		};
		const map = buildMetricMap(graph, series);

		expect(map.get('arrivals')?.value).toBe(10);
		expect(map.get('served')?.value).toBe(8);
		expect(map.get('Queue')?.value).toBe(2);
	});

	it('omits nodes without matching series', () => {
		const series = { arrivals: [10] };
		const map = buildMetricMap(graph, series);

		expect(map.has('arrivals')).toBe(true);
		expect(map.has('served')).toBe(false);
		expect(map.has('Queue')).toBe(false);
	});

	it('prefers exact name match over queue fallback', () => {
		// If a topology node happens to share its id with a series, use that
		const series = {
			Queue: [100],
			q_queue: [1, 2, 3],
		};
		const map = buildMetricMap(graph, series);
		expect(map.get('Queue')?.value).toBe(100);
	});

	it('handles multi-word PascalCase topology ids', () => {
		const g: EngineGraph = {
			nodes: [{ id: 'MyService', kind: 'servicewithbuffer' }],
			edges: [],
		};
		const series = { my_service_queue: [4, 6, 8] };
		const map = buildMetricMap(g, series);
		expect(map.get('MyService')?.value).toBe(6);
	});

	it('attaches label equal to node id', () => {
		const series = { arrivals: [5] };
		const map = buildMetricMap(graph, series);
		expect(map.get('arrivals')?.label).toBe('arrivals');
	});

	it('returns empty map when no nodes match', () => {
		const map = buildMetricMap(graph, { totally_different: [1] });
		expect(map.size).toBe(0);
	});
});

// ── seriesRange ──

describe('seriesRange', () => {
	it('computes min/max across all non-internal series', () => {
		const series = {
			a: [1, 5, 3],
			b: [0, 10, 7],
		};
		const range = seriesRange(series);
		expect(range.min).toBe(0);
		expect(range.max).toBe(10);
	});

	it('ignores internal series', () => {
		const series = {
			arrivals: [1, 2, 3],
			__temp_0: [1000, 2000],
		};
		const range = seriesRange(series);
		expect(range.max).toBe(3);
	});

	it('returns [0, 1] for empty input', () => {
		expect(seriesRange({})).toEqual({ min: 0, max: 1 });
	});

	it('returns [0, 1] when all series are internal', () => {
		expect(seriesRange({ __temp_0: [5] })).toEqual({ min: 0, max: 1 });
	});

	it('handles negative values', () => {
		const range = seriesRange({ x: [-10, -5, 0, 5] });
		expect(range.min).toBe(-10);
		expect(range.max).toBe(5);
	});
});

// ── normalizeMetricMap ──

describe('normalizeMetricMap', () => {
	it('returns empty map for empty input', () => {
		const result = normalizeMetricMap(new Map());
		expect(result.size).toBe(0);
	});

	it('normalizes values to [0, 1] range', () => {
		const map: MetricMap = new Map([
			['a', { value: 10 }],
			['b', { value: 20 }],
			['c', { value: 30 }],
		]);
		const result = normalizeMetricMap(map);
		expect(result.get('a')?.value).toBe(0);
		expect(result.get('b')?.value).toBe(0.5);
		expect(result.get('c')?.value).toBe(1);
	});

	it('handles flat distribution (all equal) with mid-scale 0.5', () => {
		const map: MetricMap = new Map([
			['a', { value: 5 }],
			['b', { value: 5 }],
			['c', { value: 5 }],
		]);
		const result = normalizeMetricMap(map);
		for (const entry of result.values()) {
			expect(entry.value).toBe(0.5);
		}
	});

	it('handles single-node map', () => {
		const map: MetricMap = new Map([['only', { value: 42 }]]);
		const result = normalizeMetricMap(map);
		expect(result.get('only')?.value).toBe(0.5);
	});

	it('preserves raw value as label when no label present', () => {
		const map: MetricMap = new Map([
			['a', { value: 10 }],
			['b', { value: 20 }],
		]);
		const result = normalizeMetricMap(map);
		expect(result.get('a')?.label).toBe('10');
		expect(result.get('b')?.label).toBe('20');
	});

	it('preserves existing label', () => {
		const map: MetricMap = new Map([['x', { value: 10, label: 'custom' }]]);
		const result = normalizeMetricMap(map);
		expect(result.get('x')?.label).toBe('custom');
	});

	it('does not mutate input map', () => {
		const map: MetricMap = new Map([['a', { value: 100 }]]);
		normalizeMetricMap(map);
		expect(map.get('a')?.value).toBe(100);
	});

	it('handles negative values', () => {
		const map: MetricMap = new Map([
			['a', { value: -10 }],
			['b', { value: 0 }],
			['c', { value: 10 }],
		]);
		const result = normalizeMetricMap(map);
		expect(result.get('a')?.value).toBe(0);
		expect(result.get('b')?.value).toBe(0.5);
		expect(result.get('c')?.value).toBe(1);
	});

	it('formats integer label without decimals', () => {
		const map: MetricMap = new Map([['a', { value: 42 }], ['b', { value: 100 }]]);
		const result = normalizeMetricMap(map);
		expect(result.get('a')?.label).toBe('42');
	});

	it('formats float label with 2 decimals', () => {
		const map: MetricMap = new Map([
			['a', { value: 3.14159 }],
			['b', { value: 2.71828 }],
		]);
		const result = normalizeMetricMap(map);
		expect(result.get('a')?.label).toBe('3.14');
	});
});
