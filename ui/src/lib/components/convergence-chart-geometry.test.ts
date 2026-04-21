import { describe, it, expect } from 'vitest';
import {
	convergenceChartGeometry,
	type ConvergenceTracePoint,
} from './convergence-chart-geometry.js';

const LAYOUT = { width: 400, height: 200, padding: { top: 12, right: 12, bottom: 28, left: 44 } };

describe('convergenceChartGeometry — empty / degenerate', () => {
	it('returns empty geometry for empty trace', () => {
		const g = convergenceChartGeometry({ trace: [], ...LAYOUT });
		expect(g.points).toEqual([]);
		expect(g.path).toBe('');
		expect(g.yMin).toBe(0);
		expect(g.yMax).toBe(1);
	});

	it('returns a single point for single-entry trace (no path connects one point)', () => {
		const trace: ConvergenceTracePoint[] = [{ iteration: 0, metricMean: 0.5 }];
		const g = convergenceChartGeometry({ trace, ...LAYOUT });
		expect(g.points).toHaveLength(1);
		expect(g.path).toBe('');
	});

	it('filters out non-finite metricMean entries', () => {
		const trace: ConvergenceTracePoint[] = [
			{ iteration: 0, metricMean: 0.5 },
			{ iteration: 1, metricMean: NaN },
			{ iteration: 2, metricMean: 0.7 },
		];
		const g = convergenceChartGeometry({ trace, ...LAYOUT });
		expect(g.points).toHaveLength(2);
		expect(g.points[0].iteration).toBe(0);
		expect(g.points[1].iteration).toBe(2);
	});
});

describe('convergenceChartGeometry — path + projection', () => {
	it('projects multi-point trace to SVG path starting with M', () => {
		const trace: ConvergenceTracePoint[] = [
			{ iteration: 0, metricMean: 0 },
			{ iteration: 1, metricMean: 0.5 },
			{ iteration: 2, metricMean: 1 },
		];
		const g = convergenceChartGeometry({ trace, ...LAYOUT });
		expect(g.path.startsWith('M ')).toBe(true);
		expect(g.points).toHaveLength(3);
	});

	it('places two iteration-0 entries at the same x (overlapping boundary points)', () => {
		const trace: ConvergenceTracePoint[] = [
			{ iteration: 0, metricMean: 0.2 },
			{ iteration: 0, metricMean: 0.9 },
			{ iteration: 1, metricMean: 0.5 },
		];
		const g = convergenceChartGeometry({ trace, ...LAYOUT });
		expect(g.points).toHaveLength(3);
		expect(g.points[0].x).toBe(g.points[1].x);
		expect(g.points[0].x).not.toBe(g.points[2].x);
	});

	it('final point is marked as final, others are not', () => {
		const trace: ConvergenceTracePoint[] = [
			{ iteration: 0, metricMean: 0 },
			{ iteration: 1, metricMean: 0.5 },
			{ iteration: 2, metricMean: 1 },
		];
		const g = convergenceChartGeometry({ trace, ...LAYOUT });
		expect(g.points[0].isFinal).toBe(false);
		expect(g.points[1].isFinal).toBe(false);
		expect(g.points[2].isFinal).toBe(true);
	});

	it('pads y-range for flat metric (all equal)', () => {
		const trace: ConvergenceTracePoint[] = [
			{ iteration: 0, metricMean: 5 },
			{ iteration: 1, metricMean: 5 },
			{ iteration: 2, metricMean: 5 },
		];
		const g = convergenceChartGeometry({ trace, ...LAYOUT });
		expect(g.yMin).toBeLessThan(5);
		expect(g.yMax).toBeGreaterThan(5);
	});

	it('pads y-range for flat zero metric', () => {
		const trace: ConvergenceTracePoint[] = [
			{ iteration: 0, metricMean: 0 },
			{ iteration: 1, metricMean: 0 },
		];
		const g = convergenceChartGeometry({ trace, ...LAYOUT });
		expect(g.yMin).toBeLessThan(0);
		expect(g.yMax).toBeGreaterThan(0);
	});

	it('handles monotonically increasing trace (y descends on screen)', () => {
		const trace: ConvergenceTracePoint[] = [
			{ iteration: 0, metricMean: 0 },
			{ iteration: 1, metricMean: 0.5 },
			{ iteration: 2, metricMean: 1 },
		];
		const g = convergenceChartGeometry({ trace, ...LAYOUT });
		// Higher metric → smaller y (SVG y grows down)
		expect(g.points[0].y).toBeGreaterThan(g.points[2].y);
	});

	it('non-monotonic trace with dip places middle below endpoints', () => {
		const trace: ConvergenceTracePoint[] = [
			{ iteration: 0, metricMean: 1 },
			{ iteration: 1, metricMean: 0 },
			{ iteration: 2, metricMean: 1 },
		];
		const g = convergenceChartGeometry({ trace, ...LAYOUT });
		expect(g.points[1].y).toBeGreaterThan(g.points[0].y);
		expect(g.points[1].y).toBeGreaterThan(g.points[2].y);
	});
});

describe('convergenceChartGeometry — target reference line', () => {
	it('returns null targetY when target is not provided', () => {
		const trace: ConvergenceTracePoint[] = [
			{ iteration: 0, metricMean: 0 },
			{ iteration: 1, metricMean: 1 },
		];
		const g = convergenceChartGeometry({ trace, ...LAYOUT });
		expect(g.targetY).toBeNull();
	});

	it('projects target into plot area when within y-range', () => {
		const trace: ConvergenceTracePoint[] = [
			{ iteration: 0, metricMean: 0 },
			{ iteration: 1, metricMean: 1 },
		];
		const g = convergenceChartGeometry({ trace, ...LAYOUT, target: 0.5 });
		expect(g.targetY).not.toBeNull();
		if (g.targetY !== null) {
			expect(g.targetY).toBeGreaterThan(LAYOUT.padding.top);
			expect(g.targetY).toBeLessThan(LAYOUT.height - LAYOUT.padding.bottom);
		}
	});

	it('extends y-range to include target below data', () => {
		const trace: ConvergenceTracePoint[] = [
			{ iteration: 0, metricMean: 0.5 },
			{ iteration: 1, metricMean: 0.6 },
		];
		const g = convergenceChartGeometry({ trace, ...LAYOUT, target: 0 });
		expect(g.yMin).toBeLessThanOrEqual(0);
	});

	it('extends y-range to include target above data', () => {
		const trace: ConvergenceTracePoint[] = [
			{ iteration: 0, metricMean: 0.5 },
			{ iteration: 1, metricMean: 0.6 },
		];
		const g = convergenceChartGeometry({ trace, ...LAYOUT, target: 1 });
		expect(g.yMax).toBeGreaterThanOrEqual(1);
	});

	it('ignores non-finite target', () => {
		const trace: ConvergenceTracePoint[] = [
			{ iteration: 0, metricMean: 0 },
			{ iteration: 1, metricMean: 1 },
		];
		const g = convergenceChartGeometry({ trace, ...LAYOUT, target: NaN });
		expect(g.targetY).toBeNull();
	});
});

describe('convergenceChartGeometry — axis tick helpers', () => {
	it('produces x ticks from unique iteration values', () => {
		const trace: ConvergenceTracePoint[] = [
			{ iteration: 0, metricMean: 0 },
			{ iteration: 0, metricMean: 1 },
			{ iteration: 1, metricMean: 0.5 },
			{ iteration: 2, metricMean: 0.7 },
		];
		const g = convergenceChartGeometry({ trace, ...LAYOUT });
		// At least two unique iterations → at least two x ticks
		expect(g.xTicks.length).toBeGreaterThanOrEqual(2);
		expect(g.xTicks[0].label).toBe('0');
	});

	it('produces y ticks with min and max labels', () => {
		const trace: ConvergenceTracePoint[] = [
			{ iteration: 0, metricMean: 0 },
			{ iteration: 1, metricMean: 1 },
		];
		const g = convergenceChartGeometry({ trace, ...LAYOUT });
		expect(g.yTicks.length).toBeGreaterThanOrEqual(2);
	});
});
