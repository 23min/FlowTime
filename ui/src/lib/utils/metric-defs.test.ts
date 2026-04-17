import { describe, it, expect } from 'vitest';
import {
	extractMetricValue,
	extractMetricValueFiltered,
	buildMetricMapForDef,
	buildMetricMapForDefFiltered,
	buildSparklineSeries,
	discoverClasses,
	METRIC_DEFS,
} from './metric-defs.js';

describe('extractMetricValue', () => {
	it('extracts a nested value', () => {
		const node = { derived: { utilization: 0.85 } };
		expect(extractMetricValue(node, 'derived.utilization')).toBe(0.85);
	});

	it('extracts a top-level value', () => {
		const node = { arrivals: 42 };
		expect(extractMetricValue(node, 'arrivals')).toBe(42);
	});

	it('returns undefined for missing path', () => {
		const node = { derived: {} };
		expect(extractMetricValue(node, 'derived.utilization')).toBeUndefined();
	});

	it('returns undefined for null intermediate', () => {
		const node = { derived: null };
		expect(extractMetricValue(node, 'derived.utilization')).toBeUndefined();
	});

	it('returns undefined for NaN', () => {
		const node = { derived: { utilization: NaN } };
		expect(extractMetricValue(node, 'derived.utilization')).toBeUndefined();
	});

	it('returns undefined for Infinity', () => {
		const node = { derived: { utilization: Infinity } };
		expect(extractMetricValue(node, 'derived.utilization')).toBeUndefined();
	});

	it('returns undefined for empty object', () => {
		expect(extractMetricValue({}, 'derived.utilization')).toBeUndefined();
	});

	it('handles zero correctly', () => {
		const node = { metrics: { errors: 0 } };
		expect(extractMetricValue(node, 'metrics.errors')).toBe(0);
	});

	it('returns undefined when intermediate is a string (non-object)', () => {
		const node = { derived: 'not-an-object' };
		expect(extractMetricValue(node, 'derived.utilization')).toBeUndefined();
	});

	it('returns undefined when intermediate is a number (non-object)', () => {
		const node = { derived: 42 };
		expect(extractMetricValue(node, 'derived.utilization')).toBeUndefined();
	});

	it('returns undefined when final value is a string', () => {
		const node = { derived: { utilization: 'high' } };
		expect(extractMetricValue(node, 'derived.utilization')).toBeUndefined();
	});
});

describe('buildMetricMapForDef', () => {
	const utilDef = METRIC_DEFS.find((d) => d.id === 'utilization')!;

	it('builds a map of node metrics', () => {
		const nodes = [
			{ id: 'a', derived: { utilization: 0.5 } },
			{ id: 'b', derived: { utilization: 0.9 } },
		];
		const m = buildMetricMapForDef(nodes, utilDef);
		expect(m.size).toBe(2);
		expect(m.get('a')?.value).toBe(0.5);
		expect(m.get('a')?.label).toBe('50%');
		expect(m.get('b')?.label).toBe('90%');
	});

	it('skips nodes without the metric', () => {
		const nodes = [
			{ id: 'a', derived: { utilization: 0.5 } },
			{ id: 'b', derived: {} },
			{ id: 'c' },
		];
		const m = buildMetricMapForDef(nodes, utilDef);
		expect(m.size).toBe(1);
	});

	it('skips nodes without id', () => {
		const nodes = [{ derived: { utilization: 0.5 } }];
		const m = buildMetricMapForDef(nodes, utilDef);
		expect(m.size).toBe(0);
	});

	it('works with queue metric', () => {
		const queueDef = METRIC_DEFS.find((d) => d.id === 'queue')!;
		const nodes = [{ id: 'a', metrics: { queue: 14.5 } }];
		const m = buildMetricMapForDef(nodes, queueDef);
		expect(m.get('a')?.label).toBe('14.5');
	});

	it('works with flow latency metric', () => {
		const latDef = METRIC_DEFS.find((d) => d.id === 'flowLatency')!;
		const nodes = [{ id: 'a', derived: { flowLatencyMs: 1500 } }];
		const m = buildMetricMapForDef(nodes, latDef);
		expect(m.get('a')?.label).toBe('1.5s');
	});

	it('formats sub-second latency as ms', () => {
		const latDef = METRIC_DEFS.find((d) => d.id === 'flowLatency')!;
		const nodes = [{ id: 'a', derived: { flowLatencyMs: 340 } }];
		const m = buildMetricMapForDef(nodes, latDef);
		expect(m.get('a')?.label).toBe('340ms');
	});
});

describe('discoverClasses', () => {
	it('returns empty array when no classes', () => {
		const nodes = [{ id: 'a', derived: {} }];
		expect(discoverClasses(nodes)).toEqual([]);
	});

	it('discovers classes from byClass', () => {
		const nodes = [
			{ id: 'a', byClass: { express: {}, standard: {} } },
			{ id: 'b', byClass: { express: {}, priority: {} } },
		];
		expect(discoverClasses(nodes)).toEqual(['express', 'priority', 'standard']);
	});

	it('handles nodes without byClass', () => {
		const nodes = [{ id: 'a' }, { id: 'b', byClass: { x: {} } }];
		expect(discoverClasses(nodes)).toEqual(['x']);
	});

	it('deduplicates class ids seen in multiple nodes', () => {
		const nodes = [
			{ id: 'a', byClass: { x: {}, y: {} } },
			{ id: 'b', byClass: { x: {}, z: {} } },
		];
		expect(discoverClasses(nodes)).toEqual(['x', 'y', 'z']);
	});
});

describe('extractMetricValueFiltered (snapshot)', () => {
	it('returns top-level value when filter is empty', () => {
		const node = { derived: { utilization: 0.85 }, byClass: { x: { utilization: 0.5 } } };
		expect(extractMetricValueFiltered(node, 'derived.utilization', new Set())).toBe(0.85);
	});

	it('sums across filtered classes using flat key', () => {
		const node = {
			derived: { utilization: 0.85 },
			byClass: {
				express: { utilization: 0.3 },
				standard: { utilization: 0.4 },
			},
		};
		const v = extractMetricValueFiltered(node, 'derived.utilization', new Set(['express', 'standard']));
		expect(v).toBeCloseTo(0.7, 5);
	});

	it('strips metrics. prefix when reading from byClass', () => {
		const node = {
			metrics: { queue: 10 },
			byClass: { express: { queue: 3 }, standard: { queue: 4 } },
		};
		const v = extractMetricValueFiltered(node, 'metrics.queue', new Set(['express']));
		expect(v).toBe(3);
	});

	it('returns undefined when no active classes have data', () => {
		const node = { byClass: { other: { utilization: 0.5 } } };
		const v = extractMetricValueFiltered(node, 'derived.utilization', new Set(['missing']));
		expect(v).toBeUndefined();
	});

	it('returns undefined when byClass missing', () => {
		const node = { derived: { utilization: 0.85 } };
		const v = extractMetricValueFiltered(node, 'derived.utilization', new Set(['x']));
		expect(v).toBeUndefined();
	});

	it('skips non-finite class values', () => {
		const node = {
			byClass: {
				a: { utilization: NaN },
				b: { utilization: 0.4 },
			},
		};
		const v = extractMetricValueFiltered(node, 'derived.utilization', new Set(['a', 'b']));
		expect(v).toBe(0.4);
	});

	it('skips class entries where the key is missing', () => {
		const node = {
			byClass: {
				a: { /* no utilization */ },
				b: { utilization: 0.3 },
			},
		};
		const v = extractMetricValueFiltered(node, 'derived.utilization', new Set(['a', 'b']));
		expect(v).toBe(0.3);
	});

	it('skips class entries where the key is null', () => {
		const node = {
			byClass: {
				a: { utilization: null },
				b: { utilization: 0.3 },
			},
		};
		const v = extractMetricValueFiltered(node, 'derived.utilization', new Set(['a', 'b']));
		expect(v).toBe(0.3);
	});

	it('works with path that has no derived/metrics prefix', () => {
		const node = { byClass: { x: { custom: 5 } } };
		const v = extractMetricValueFiltered(node, 'custom', new Set(['x']));
		expect(v).toBe(5);
	});
});

describe('buildMetricMapForDefFiltered', () => {
	const utilDef = METRIC_DEFS.find((d) => d.id === 'utilization')!;

	it('uses aggregate when filter empty', () => {
		const nodes = [{ id: 'a', derived: { utilization: 0.85 } }];
		const m = buildMetricMapForDefFiltered(nodes, utilDef, new Set());
		expect(m.get('a')?.value).toBe(0.85);
	});

	it('uses filtered classes when filter active', () => {
		const nodes = [
			{
				id: 'a',
				derived: { utilization: 0.85 },
				byClass: { express: { utilization: 0.3 } },
			},
		];
		const m = buildMetricMapForDefFiltered(nodes, utilDef, new Set(['express']));
		expect(m.get('a')?.value).toBe(0.3);
	});

	it('omits nodes without matching class data', () => {
		const nodes = [
			{ id: 'a', derived: { utilization: 0.85 } },
			{ id: 'b', byClass: { express: { utilization: 0.3 } } },
		];
		const m = buildMetricMapForDefFiltered(nodes, utilDef, new Set(['express']));
		expect(m.has('a')).toBe(false);
		expect(m.get('b')?.value).toBe(0.3);
	});

	it('skips nodes without id', () => {
		const nodes = [
			{ derived: { utilization: 0.5 } }, // no id
			{ id: 'b', derived: { utilization: 0.7 } },
		];
		const m = buildMetricMapForDefFiltered(nodes, utilDef, new Set());
		expect(m.size).toBe(1);
		expect(m.has('b')).toBe(true);
	});
});

describe('buildSparklineSeries (window)', () => {
	const utilDef = METRIC_DEFS.find((d) => d.id === 'utilization')!;

	it('extracts per-node series from window nodes', () => {
		const windowNodes = [
			{ id: 'a', series: { utilization: [0.1, 0.2, 0.3] } },
			{ id: 'b', series: { utilization: [0.5, 0.6, 0.7] } },
		];
		const m = buildSparklineSeries(windowNodes, utilDef, new Set());
		expect(m.get('a')).toEqual([0.1, 0.2, 0.3]);
		expect(m.get('b')).toEqual([0.5, 0.6, 0.7]);
	});

	it('converts null values to NaN', () => {
		const windowNodes = [{ id: 'a', series: { utilization: [0.1, null, 0.3] } }];
		const m = buildSparklineSeries(windowNodes, utilDef, new Set());
		const arr = m.get('a')!;
		expect(arr[0]).toBe(0.1);
		expect(isNaN(arr[1])).toBe(true);
		expect(arr[2]).toBe(0.3);
	});

	it('skips nodes without the series', () => {
		const windowNodes = [
			{ id: 'a', series: { utilization: [0.1, 0.2] } },
			{ id: 'b', series: { other: [0.5, 0.6] } },
		];
		const m = buildSparklineSeries(windowNodes, utilDef, new Set());
		expect(m.has('a')).toBe(true);
		expect(m.has('b')).toBe(false);
	});

	it('sums across active classes from byClass', () => {
		const windowNodes = [
			{
				id: 'a',
				series: { utilization: [1, 2, 3] },
				byClass: {
					express: { utilization: [0.1, 0.2, 0.3] },
					standard: { utilization: [0.4, 0.5, 0.6] },
				},
			},
		];
		const m = buildSparklineSeries(windowNodes, utilDef, new Set(['express', 'standard']));
		const arr = m.get('a')!;
		expect(arr[0]).toBeCloseTo(0.5, 5);
		expect(arr[1]).toBeCloseTo(0.7, 5);
		expect(arr[2]).toBeCloseTo(0.9, 5);
	});

	it('returns single-class values when only one class active', () => {
		const windowNodes = [
			{
				id: 'a',
				byClass: {
					express: { utilization: [0.1, 0.2, 0.3] },
					standard: { utilization: [0.9, 0.9, 0.9] },
				},
			},
		];
		const m = buildSparklineSeries(windowNodes, utilDef, new Set(['express']));
		expect(m.get('a')).toEqual([0.1, 0.2, 0.3]);
	});

	it('skips nodes without id', () => {
		const windowNodes = [
			{ series: { utilization: [0.1, 0.2] } }, // no id
			{ id: 'b', series: { utilization: [0.3, 0.4] } },
		];
		const m = buildSparklineSeries(windowNodes, utilDef, new Set());
		expect(m.size).toBe(1);
		expect(m.has('b')).toBe(true);
	});

	it('skips nodes with no series field at all (empty filter)', () => {
		const windowNodes = [{ id: 'a' }];
		const m = buildSparklineSeries(windowNodes, utilDef, new Set());
		expect(m.has('a')).toBe(false);
	});

	it('skips nodes whose series value is not an array', () => {
		const windowNodes = [{ id: 'a', series: { utilization: 'not-an-array' } }];
		const m = buildSparklineSeries(windowNodes, utilDef, new Set());
		expect(m.has('a')).toBe(false);
	});

	it('class-mode skips nodes without byClass', () => {
		const windowNodes = [{ id: 'a', series: { utilization: [0.1, 0.2] } }];
		const m = buildSparklineSeries(windowNodes, utilDef, new Set(['express']));
		expect(m.has('a')).toBe(false);
	});

	it('class-mode skips a class missing from byClass', () => {
		const windowNodes = [
			{ id: 'a', byClass: { express: { utilization: [0.1, 0.2] } } },
		];
		// Active set includes a class the node does not have; express still contributes
		const m = buildSparklineSeries(windowNodes, utilDef, new Set(['express', 'missing']));
		expect(m.get('a')).toEqual([0.1, 0.2]);
	});

	it('class-mode skips when the series key is missing or non-array in a class', () => {
		const windowNodes = [
			{
				id: 'a',
				byClass: {
					express: { utilization: [0.1, 0.2] },
					standard: { utilization: 'nope' },
				},
			},
		];
		const m = buildSparklineSeries(windowNodes, utilDef, new Set(['express', 'standard']));
		// Only express contributes
		expect(m.get('a')).toEqual([0.1, 0.2]);
	});

	it('class-mode: later class with finite value fills prior NaN slot', () => {
		// First class contributes [NaN, 0.2], second contributes [0.1, 0.1]
		// Expected: slot 0 → 0.1 (from second class), slot 1 → 0.3 (sum)
		const windowNodes = [
			{
				id: 'a',
				byClass: {
					express: { utilization: [null, 0.2] },
					standard: { utilization: [0.1, 0.1] },
				},
			},
		];
		const m = buildSparklineSeries(windowNodes, utilDef, new Set(['express', 'standard']));
		const arr = m.get('a')!;
		expect(arr[0]).toBeCloseTo(0.1, 5);
		expect(arr[1]).toBeCloseTo(0.3, 5);
	});

	it('class-mode: NaN in second class leaves prior value unchanged', () => {
		// First class [0.2, 0.3], second class [NaN, 0.1]
		// Expected: slot 0 → 0.2 (untouched), slot 1 → 0.4 (sum)
		const windowNodes = [
			{
				id: 'a',
				byClass: {
					express: { utilization: [0.2, 0.3] },
					standard: { utilization: [null, 0.1] },
				},
			},
		];
		const m = buildSparklineSeries(windowNodes, utilDef, new Set(['express', 'standard']));
		const arr = m.get('a')!;
		expect(arr[0]).toBeCloseTo(0.2, 5);
		expect(arr[1]).toBeCloseTo(0.4, 5);
	});

	it('returns undefined (skipped) when no active class has any data', () => {
		const windowNodes = [
			{ id: 'a', byClass: { other: { utilization: [0.1, 0.2] } } },
		];
		const m = buildSparklineSeries(windowNodes, utilDef, new Set(['missing']));
		expect(m.has('a')).toBe(false);
	});

	it('skips nodes whose series is an empty array', () => {
		const windowNodes = [{ id: 'a', series: { utilization: [] } }];
		const m = buildSparklineSeries(windowNodes, utilDef, new Set());
		expect(m.has('a')).toBe(false);
	});

	it('class-mode caps summation at the shorter array length', () => {
		// First class has 3 values, second has 2 — inner loop iterates min(3,2)=2.
		const windowNodes = [
			{
				id: 'a',
				byClass: {
					express: { utilization: [1, 2, 3] },
					standard: { utilization: [10, 20] },
				},
			},
		];
		const m = buildSparklineSeries(windowNodes, utilDef, new Set(['express', 'standard']));
		const arr = m.get('a')!;
		expect(arr[0]).toBe(11);
		expect(arr[1]).toBe(22);
		// Third slot is not summed because standard has no value at index 2 — retains first class value
		expect(arr[2]).toBe(3);
	});
});
