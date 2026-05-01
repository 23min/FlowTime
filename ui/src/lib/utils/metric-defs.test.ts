import { describe, it, expect } from 'vitest';
import {
	extractMetricValue,
	extractMetricValueFiltered,
	buildSparklineSeries,
	buildNormalizedMetricMap,
	computeMetricDomainFromWindow,
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

describe('computeMetricDomainFromWindow (m-E21-06 AC5 + ADR-02)', () => {
	const utilDef = METRIC_DEFS.find((d) => d.id === 'utilization')!;

	it('derives [min, p99] over all finite series values', () => {
		const windowNodes = [
			{ id: 'a', series: { utilization: [0.1, 0.2, 0.3] } },
			{ id: 'b', series: { utilization: [0.4, 0.5, 0.6] } },
		];
		const domain = computeMetricDomainFromWindow(windowNodes, utilDef, new Set());
		expect(domain).not.toBeNull();
		expect(domain![0]).toBe(0.1);
		expect(domain![1]).toBeGreaterThan(0.3);
	});

	it('returns null when no finite values exist', () => {
		const windowNodes = [{ id: 'a', series: { utilization: [null, null] } }];
		const domain = computeMetricDomainFromWindow(windowNodes, utilDef, new Set());
		expect(domain).toBeNull();
	});

	it('returns null for empty input', () => {
		const domain = computeMetricDomainFromWindow([], utilDef, new Set());
		expect(domain).toBeNull();
	});

	it('honors the active class filter', () => {
		const windowNodes = [
			{
				id: 'a',
				byClass: {
					express: { utilization: [0.5, 0.6] },
					standard: { utilization: [100, 200] },
				},
			},
		];
		const domain = computeMetricDomainFromWindow(windowNodes, utilDef, new Set(['express']));
		expect(domain).not.toBeNull();
		expect(domain![0]).toBe(0.5);
		expect(domain![1]).toBeLessThan(1);
	});

	it('skips nodes without an id', () => {
		const windowNodes = [
			{ series: { utilization: [0.1, 0.2] } },
			{ id: 'b', series: { utilization: [0.3, 0.4] } },
		];
		const domain = computeMetricDomainFromWindow(windowNodes, utilDef, new Set());
		expect(domain).not.toBeNull();
		expect(domain![0]).toBe(0.3);
	});

	it('ignores NaN / Infinity in the series', () => {
		const windowNodes = [
			{ id: 'a', series: { utilization: [0.1, NaN, Infinity, 0.3] } },
		];
		const domain = computeMetricDomainFromWindow(windowNodes, utilDef, new Set());
		expect(domain).not.toBeNull();
		expect(domain![0]).toBe(0.1);
		expect(Number.isFinite(domain![1])).toBe(true);
	});
});

describe('buildNormalizedMetricMap (m-E21-06 AC5 + ADR-02)', () => {
	const utilDef = METRIC_DEFS.find((d) => d.id === 'utilization')!;
	const queueDef = METRIC_DEFS.find((d) => d.id === 'queue')!;

	it('returns empty map when domain is null', () => {
		const nodes = [{ id: 'a', derived: { utilization: 0.5 } }];
		const m = buildNormalizedMetricMap(nodes, utilDef, new Set(), null);
		expect(m.size).toBe(0);
	});

	it('normalizes raw values into [0, 1] against the domain', () => {
		const nodes = [
			{ id: 'a', derived: { utilization: 0 } },
			{ id: 'b', derived: { utilization: 0.5 } },
			{ id: 'c', derived: { utilization: 1 } },
		];
		const m = buildNormalizedMetricMap(nodes, utilDef, new Set(), [0, 1]);
		expect(m.get('a')?.value).toBe(0);
		expect(m.get('b')?.value).toBe(0.5);
		expect(m.get('c')?.value).toBe(1);
	});

	it('formats the label from the RAW value, not the normalized one', () => {
		const nodes = [{ id: 'a', metrics: { queue: 50 } }];
		const m = buildNormalizedMetricMap(nodes, queueDef, new Set(), [0, 100]);
		expect(m.get('a')?.value).toBe(0.5);
		expect(m.get('a')?.label).toBe('50.0');
	});

	it('clamps values above the 99p domain max to 1', () => {
		const nodes = [{ id: 'a', metrics: { queue: 1000 } }];
		const m = buildNormalizedMetricMap(nodes, queueDef, new Set(), [0, 100]);
		expect(m.get('a')?.value).toBe(1);
	});

	it('omits nodes whose raw value cannot be extracted', () => {
		const nodes = [
			{ id: 'a', derived: { utilization: 0.5 } },
			{ id: 'b', metrics: {} }, // no utilization
		];
		const m = buildNormalizedMetricMap(nodes, utilDef, new Set(), [0, 1]);
		expect(m.has('a')).toBe(true);
		expect(m.has('b')).toBe(false);
	});

	it('honors the class filter path', () => {
		const nodes = [
			{
				id: 'a',
				derived: { utilization: 0.9 },
				byClass: { express: { utilization: 0.2 } },
			},
		];
		const m = buildNormalizedMetricMap(nodes, utilDef, new Set(['express']), [0, 1]);
		expect(m.get('a')?.value).toBe(0.2);
	});

	it('skips nodes without an id', () => {
		const nodes = [
			{ derived: { utilization: 0.5 } },
			{ id: 'b', derived: { utilization: 0.7 } },
		];
		const m = buildNormalizedMetricMap(nodes, utilDef, new Set(), [0, 1]);
		expect(m.size).toBe(1);
		expect(m.has('b')).toBe(true);
	});
});
