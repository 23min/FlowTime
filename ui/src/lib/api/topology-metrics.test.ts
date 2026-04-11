import { describe, it, expect } from 'vitest';
import {
	defaultMetric,
	availableMetrics,
	seriesMean,
	buildMetricMap,
	buildEdgeMetricMap,
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

	// per-bin mode (AC-10)
	it('uses value at bin 0 when bin=0', () => {
		const series = { arrivals: [10, 20, 30] };
		const map = buildMetricMap(graph, series, 0);
		expect(map.get('arrivals')?.value).toBe(10);
	});

	it('uses value at last bin when bin=N-1', () => {
		const series = { arrivals: [10, 20, 30] };
		const map = buildMetricMap(graph, series, 2);
		expect(map.get('arrivals')?.value).toBe(30);
	});

	it('falls back to mean when bin is out of range (>= length)', () => {
		const series = { arrivals: [10, 20, 30] };
		const map = buildMetricMap(graph, series, 99);
		expect(map.get('arrivals')?.value).toBe(20); // mean
	});

	it('falls back to mean when bin is negative', () => {
		const series = { arrivals: [10, 20, 30] };
		const map = buildMetricMap(graph, series, -1);
		expect(map.get('arrivals')?.value).toBe(20); // mean
	});

	it('falls back to mean when bin is undefined (no bin arg)', () => {
		const series = { arrivals: [10, 20, 30] };
		const map = buildMetricMap(graph, series);
		expect(map.get('arrivals')?.value).toBe(20); // unchanged mean behaviour
	});

	it('per-bin and mean produce different values for non-uniform series', () => {
		const series = { arrivals: [0, 100] };
		const binMap = buildMetricMap(graph, series, 0);
		const meanMap = buildMetricMap(graph, series);
		expect(binMap.get('arrivals')?.value).not.toBe(meanMap.get('arrivals')?.value);
	});
});

// ── buildEdgeMetricMap ──
//
// Edge key format: `${fromId}\u2192${toId}` (Unicode right arrow →)
// Confirmed from dag-map/src/render.js:151

describe('buildEdgeMetricMap', () => {
	// Simple two-node graph: arrivals → Service
	const graph: EngineGraph = {
		nodes: [
			{ id: 'arrivals', kind: 'const' },
			{ id: 'Service', kind: 'servicewithbuffer' },
		],
		edges: [{ from: 'arrivals', to: 'Service' }],
	};

	it('uses Unicode arrow key: from→to', () => {
		const series = { arrivals: [10, 10, 10] };
		const map = buildEdgeMetricMap(graph, series);
		// Key must be the exact Unicode arrow character
		expect(map.has('arrivals\u2192Service')).toBe(true);
	});

	it('does NOT use ASCII arrow key', () => {
		const series = { arrivals: [10, 10, 10] };
		const map = buildEdgeMetricMap(graph, series);
		expect(map.has('arrivals->Service')).toBe(false);
	});

	it('assigns the mean of the from-node series', () => {
		const series = { arrivals: [10, 20, 30] };
		const map = buildEdgeMetricMap(graph, series);
		expect(map.get('arrivals\u2192Service')?.value).toBe(20);
	});

	it('omits edge when from-node has no series', () => {
		const series = { served: [5, 5, 5] }; // no 'arrivals' series
		const map = buildEdgeMetricMap(graph, series);
		expect(map.size).toBe(0);
	});

	it('handles multiple edges — correct keys and values', () => {
		const multiGraph: EngineGraph = {
			nodes: [
				{ id: 'a', kind: 'const' },
				{ id: 'b', kind: 'expr' },
				{ id: 'c', kind: 'expr' },
			],
			edges: [
				{ from: 'a', to: 'b' },
				{ from: 'b', to: 'c' },
			],
		};
		const series = { a: [2, 4], b: [6, 8] };
		const map = buildEdgeMetricMap(multiGraph, series);
		expect(map.get('a\u2192b')?.value).toBe(3);   // mean([2,4])
		expect(map.get('b\u2192c')?.value).toBe(7);   // mean([6,8])
		expect(map.size).toBe(2);
	});

	it('map size equals edges with known from-series', () => {
		// 3 edges, only 2 from-nodes have series
		const g: EngineGraph = {
			nodes: [
				{ id: 'x', kind: 'const' },
				{ id: 'y', kind: 'expr' },
				{ id: 'z', kind: 'expr' },
				{ id: 'w', kind: 'expr' },
			],
			edges: [
				{ from: 'x', to: 'y' },
				{ from: 'y', to: 'z' },
				{ from: 'z', to: 'w' }, // 'z' has no series
			],
		};
		const series = { x: [1], y: [2] };
		const map = buildEdgeMetricMap(g, series);
		expect(map.size).toBe(2);
	});

	it('uses queue column fallback for topology from-nodes', () => {
		// Edge from topology node MyQueue (whose series is "my_queue_queue") to expr
		const g: EngineGraph = {
			nodes: [
				{ id: 'MyQueue', kind: 'servicewithbuffer' },
				{ id: 'downstream', kind: 'expr' },
			],
			edges: [{ from: 'MyQueue', to: 'downstream' }],
		};
		const series = { my_queue_queue: [3, 6, 9] };
		const map = buildEdgeMetricMap(g, series);
		expect(map.get('MyQueue\u2192downstream')?.value).toBe(6); // mean([3,6,9])
	});

	it('attaches label equal to from-node id', () => {
		const series = { arrivals: [15] };
		const map = buildEdgeMetricMap(graph, series);
		expect(map.get('arrivals\u2192Service')?.label).toBe('arrivals');
	});

	it('returns empty map when graph has no edges', () => {
		const noEdgeGraph: EngineGraph = {
			nodes: [{ id: 'solo', kind: 'const' }],
			edges: [],
		};
		const map = buildEdgeMetricMap(noEdgeGraph, { solo: [1, 2, 3] });
		expect(map.size).toBe(0);
	});

	// per-bin mode (AC-11)
	it('uses value at bin 0 for edge when bin=0', () => {
		const series = { arrivals: [5, 15, 25] };
		const map = buildEdgeMetricMap(graph, series, 0);
		expect(map.get('arrivals\u2192Service')?.value).toBe(5);
	});

	it('uses value at bin N-1 for edge when bin=N-1', () => {
		const series = { arrivals: [5, 15, 25] };
		const map = buildEdgeMetricMap(graph, series, 2);
		expect(map.get('arrivals\u2192Service')?.value).toBe(25);
	});

	it('falls back to mean for edge when bin is out of range', () => {
		const series = { arrivals: [5, 15, 25] };
		const map = buildEdgeMetricMap(graph, series, 99);
		expect(map.get('arrivals\u2192Service')?.value).toBe(15); // mean
	});

	it('falls back to mean for edge when bin is undefined', () => {
		const series = { arrivals: [5, 15, 25] };
		const map = buildEdgeMetricMap(graph, series);
		expect(map.get('arrivals\u2192Service')?.value).toBe(15); // mean
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
