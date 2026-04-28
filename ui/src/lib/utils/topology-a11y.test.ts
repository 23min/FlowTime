import { describe, it, expect } from 'vitest';
import { buildNodeAriaLabel, buildEdgeAriaLabel } from './topology-a11y.js';

describe('buildNodeAriaLabel', () => {
	it('full shape — id + class + metric label + numeric value', () => {
		expect(
			buildNodeAriaLabel({
				nodeId: 'A',
				className: 'cust',
				metricLabel: 'Utilization',
				metricValue: 0.42,
			}),
		).toBe('A (cust) — Utilization: 0.42');
	});

	it('formats numeric values with two-decimal default — large number', () => {
		expect(
			buildNodeAriaLabel({
				nodeId: 'queue.foo',
				className: 'wip',
				metricLabel: 'Arrivals',
				metricValue: 12345.6789,
			}),
		).toBe('queue.foo (wip) — Arrivals: 12345.68');
	});

	it('omits parenthesised class segment when class is unknown (undefined)', () => {
		expect(
			buildNodeAriaLabel({
				nodeId: 'A',
				className: undefined,
				metricLabel: 'Utilization',
				metricValue: 0.42,
			}),
		).toBe('A — Utilization: 0.42');
	});

	it('omits parenthesised class segment when class is null', () => {
		expect(
			buildNodeAriaLabel({
				nodeId: 'A',
				className: null,
				metricLabel: 'Utilization',
				metricValue: 0.42,
			}),
		).toBe('A — Utilization: 0.42');
	});

	it('omits parenthesised class segment when class is empty string', () => {
		expect(
			buildNodeAriaLabel({
				nodeId: 'A',
				className: '',
				metricLabel: 'Utilization',
				metricValue: 0.42,
			}),
		).toBe('A — Utilization: 0.42');
	});

	it('renders "no data" when metric value is undefined', () => {
		expect(
			buildNodeAriaLabel({
				nodeId: 'A',
				className: 'cust',
				metricLabel: 'Utilization',
				metricValue: undefined,
			}),
		).toBe('A (cust) — Utilization: no data');
	});

	it('renders "no data" when metric value is null', () => {
		expect(
			buildNodeAriaLabel({
				nodeId: 'A',
				className: 'cust',
				metricLabel: 'Utilization',
				metricValue: null,
			}),
		).toBe('A (cust) — Utilization: no data');
	});

	it('renders "no data" when metric value is NaN', () => {
		expect(
			buildNodeAriaLabel({
				nodeId: 'A',
				className: 'cust',
				metricLabel: 'Utilization',
				metricValue: Number.NaN,
			}),
		).toBe('A (cust) — Utilization: no data');
	});

	it('combines unknown class and missing value in the same fallback', () => {
		expect(
			buildNodeAriaLabel({
				nodeId: 'A',
				className: undefined,
				metricLabel: 'Utilization',
				metricValue: undefined,
			}),
		).toBe('A — Utilization: no data');
	});
});

describe('buildEdgeAriaLabel', () => {
	it('plain "from → to" shape', () => {
		expect(buildEdgeAriaLabel({ from: 'A', to: 'B' })).toBe('edge from A to B');
	});

	it('preserves dotted node ids verbatim', () => {
		expect(buildEdgeAriaLabel({ from: 'queue.foo', to: 'sink.bar' })).toBe(
			'edge from queue.foo to sink.bar',
		);
	});
});
