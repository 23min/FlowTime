import { describe, it, expect } from 'vitest';
import {
	computeChartGeometry,
	binFromX,
	xFromBin,
	DEFAULT_PADDING,
	type ChartLayout,
	type ChartSeries,
} from './chart-geometry';

const LAYOUT: ChartLayout = {
	width: 300,
	height: 120,
	padding: DEFAULT_PADDING,
};

// Plot area: left=36, right=300-8=292, top=8, bottom=120-20=100
// plotWidth=256, plotHeight=92

describe('computeChartGeometry', () => {
	it('returns empty geometry for empty input', () => {
		const g = computeChartGeometry([], LAYOUT);
		expect(g.paths).toEqual([]);
		expect(g.bins).toBe(0);
		expect(g.xTicks).toEqual([]);
		expect(g.yTicks).toEqual([]);
	});

	it('returns empty geometry when all series are empty arrays', () => {
		const g = computeChartGeometry([{ name: 'x', values: [] }], LAYOUT);
		expect(g.paths).toEqual([]);
		expect(g.bins).toBe(0);
	});

	it('computes paths for a single series', () => {
		const series: ChartSeries[] = [{ name: 'a', values: [0, 5, 10] }];
		const g = computeChartGeometry(series, LAYOUT);

		expect(g.paths).toHaveLength(1);
		expect(g.paths[0].name).toBe('a');
		expect(g.bins).toBe(3);
		expect(g.yMin).toBe(0);
		expect(g.yMax).toBe(10);

		// Path should start with M, then have L commands
		expect(g.paths[0].d).toMatch(/^M /);
		expect(g.paths[0].d).toContain('L ');
	});

	it('uses shared y-range across multiple series', () => {
		const series: ChartSeries[] = [
			{ name: 'a', values: [0, 10] },
			{ name: 'b', values: [5, 15] },
		];
		const g = computeChartGeometry(series, LAYOUT);
		expect(g.yMin).toBe(0);
		expect(g.yMax).toBe(15);
		expect(g.paths).toHaveLength(2);
	});

	it('pads y-range when all values are flat', () => {
		const series: ChartSeries[] = [{ name: 'a', values: [5, 5, 5] }];
		const g = computeChartGeometry(series, LAYOUT);
		expect(g.yMin).toBeLessThan(5);
		expect(g.yMax).toBeGreaterThan(5);
	});

	it('pads y-range when all values are zero', () => {
		const series: ChartSeries[] = [{ name: 'a', values: [0, 0, 0] }];
		const g = computeChartGeometry(series, LAYOUT);
		expect(g.yMax).toBeGreaterThan(0);
	});

	it('uses the shortest series length when lengths differ', () => {
		const series: ChartSeries[] = [
			{ name: 'a', values: [1, 2, 3, 4] },
			{ name: 'b', values: [5, 6] },
		];
		const g = computeChartGeometry(series, LAYOUT);
		expect(g.bins).toBe(2);
	});

	it('x-positions span the plot area', () => {
		const series: ChartSeries[] = [{ name: 'a', values: [1, 2, 3] }];
		const g = computeChartGeometry(series, LAYOUT);

		// First point at plotLeft=36, last at plotRight=292
		const d = g.paths[0].d;
		expect(d).toContain('M 36.00');
		expect(d).toContain('L 292.00');
	});

	it('y-positions: max value at top, min value at bottom', () => {
		const series: ChartSeries[] = [{ name: 'a', values: [0, 10] }];
		const g = computeChartGeometry(series, LAYOUT);

		// v=0 (min) at plotBottom=100; v=10 (max) at plotTop=8
		const d = g.paths[0].d;
		expect(d).toContain('100.00');
		expect(d).toContain('8.00');
	});

	it('computes x-axis ticks', () => {
		const series: ChartSeries[] = [{ name: 'a', values: [1, 2, 3, 4, 5, 6, 7, 8] }];
		const g = computeChartGeometry(series, LAYOUT);

		expect(g.xTicks.length).toBeGreaterThan(0);
		expect(g.xTicks.length).toBeLessThanOrEqual(5);
		// First tick at 0, last at bins-1
		expect(g.xTicks[0].label).toBe('0');
		expect(g.xTicks[g.xTicks.length - 1].label).toBe('7');
	});

	it('computes y-axis ticks with min/mid/max', () => {
		const series: ChartSeries[] = [{ name: 'a', values: [0, 5, 10] }];
		const g = computeChartGeometry(series, LAYOUT);

		expect(g.yTicks).toHaveLength(3);
		expect(g.yTicks[0].label).toBe('0');
		expect(g.yTicks[1].label).toBe('5');
		expect(g.yTicks[2].label).toBe('10');
	});

	it('single-bin series produces a single tick', () => {
		const series: ChartSeries[] = [{ name: 'a', values: [42] }];
		const g = computeChartGeometry(series, LAYOUT);
		expect(g.bins).toBe(1);
		expect(g.xTicks).toHaveLength(1);
		expect(g.xTicks[0].label).toBe('0');
	});

	it('preserves series color', () => {
		const series: ChartSeries[] = [{ name: 'a', values: [1, 2], color: '#ff0000' }];
		const g = computeChartGeometry(series, LAYOUT);
		expect(g.paths[0].color).toBe('#ff0000');
	});
});

// ── binFromX / xFromBin ──

describe('binFromX', () => {
	it('returns null for empty bins', () => {
		expect(binFromX(100, 0, 200, 0)).toBeNull();
	});

	it('returns 0 for single bin', () => {
		expect(binFromX(100, 0, 200, 1)).toBe(0);
	});

	it('clamps to 0 when mouse is left of plot', () => {
		expect(binFromX(-50, 0, 200, 5)).toBe(0);
	});

	it('clamps to bins-1 when mouse is right of plot', () => {
		expect(binFromX(500, 0, 200, 5)).toBe(4);
	});

	it('returns mid bin for center of plot', () => {
		// bins=5, plotLeft=0, plotRight=200 → step = 50
		// center = 100 → frac = 0.5 → bin = round(2) = 2
		expect(binFromX(100, 0, 200, 5)).toBe(2);
	});

	it('rounds to nearest bin', () => {
		// bins=4, plotLeft=0, plotRight=300
		// x=100 → frac = 0.333 → bin idx = round(1) = 1
		expect(binFromX(100, 0, 300, 4)).toBe(1);
	});
});

describe('xFromBin', () => {
	it('returns plotLeft for bin 0', () => {
		expect(xFromBin(0, 36, 292, 4)).toBe(36);
	});

	it('returns plotRight for last bin', () => {
		expect(xFromBin(3, 36, 292, 4)).toBe(292);
	});

	it('returns midpoint for middle bin', () => {
		// bins=3, plotLeft=0, plotRight=100 → mid bin at x=50
		expect(xFromBin(1, 0, 100, 3)).toBe(50);
	});

	it('returns plotLeft for single-bin chart', () => {
		expect(xFromBin(0, 50, 200, 1)).toBe(50);
	});
});
