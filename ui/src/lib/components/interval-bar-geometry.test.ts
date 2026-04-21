import { describe, it, expect } from 'vitest';
import { intervalMarkerGeometry, type IntervalBarInput } from './interval-bar-geometry.js';

describe('intervalMarkerGeometry', () => {
	const base: IntervalBarInput = { lo: 0, hi: 100, value: 50, width: 200 };

	it('returns { ok: true } with value mid-bar for value at midpoint', () => {
		const g = intervalMarkerGeometry(base);
		expect(g.ok).toBe(true);
		if (g.ok) {
			expect(g.markerX).toBe(100);
			expect(g.barStart).toBe(0);
			expect(g.barEnd).toBe(200);
			expect(g.clamped).toBe(false);
		}
	});

	it('places marker at barStart when value === lo', () => {
		const g = intervalMarkerGeometry({ ...base, value: 0 });
		expect(g.ok).toBe(true);
		if (g.ok) {
			expect(g.markerX).toBe(0);
			expect(g.clamped).toBe(false);
		}
	});

	it('places marker at barEnd when value === hi', () => {
		const g = intervalMarkerGeometry({ ...base, value: 100 });
		expect(g.ok).toBe(true);
		if (g.ok) {
			expect(g.markerX).toBe(200);
			expect(g.clamped).toBe(false);
		}
	});

	it('clamps to barStart and flags clamped when value < lo', () => {
		const g = intervalMarkerGeometry({ ...base, value: -10 });
		expect(g.ok).toBe(true);
		if (g.ok) {
			expect(g.markerX).toBe(0);
			expect(g.clamped).toBe(true);
		}
	});

	it('clamps to barEnd and flags clamped when value > hi', () => {
		const g = intervalMarkerGeometry({ ...base, value: 150 });
		expect(g.ok).toBe(true);
		if (g.ok) {
			expect(g.markerX).toBe(200);
			expect(g.clamped).toBe(true);
		}
	});

	it('returns ok: false for degenerate interval (lo === hi)', () => {
		const g = intervalMarkerGeometry({ lo: 10, hi: 10, value: 10, width: 200 });
		expect(g.ok).toBe(false);
	});

	it('returns ok: false for inverted interval (lo > hi)', () => {
		const g = intervalMarkerGeometry({ lo: 10, hi: 5, value: 7, width: 200 });
		expect(g.ok).toBe(false);
	});

	it('returns ok: false for non-finite lo', () => {
		const g = intervalMarkerGeometry({ lo: NaN, hi: 100, value: 50, width: 200 });
		expect(g.ok).toBe(false);
	});

	it('returns ok: false for non-finite hi', () => {
		const g = intervalMarkerGeometry({ lo: 0, hi: Infinity, value: 50, width: 200 });
		expect(g.ok).toBe(false);
	});

	it('returns ok: false for non-finite value', () => {
		const g = intervalMarkerGeometry({ lo: 0, hi: 100, value: NaN, width: 200 });
		expect(g.ok).toBe(false);
	});

	it('returns ok: false for non-finite width', () => {
		const g = intervalMarkerGeometry({ lo: 0, hi: 100, value: 50, width: NaN });
		expect(g.ok).toBe(false);
	});

	it('returns ok: false for zero or negative width', () => {
		expect(intervalMarkerGeometry({ lo: 0, hi: 100, value: 50, width: 0 }).ok).toBe(false);
		expect(intervalMarkerGeometry({ lo: 0, hi: 100, value: 50, width: -10 }).ok).toBe(false);
	});

	it('handles negative intervals correctly', () => {
		const g = intervalMarkerGeometry({ lo: -100, hi: -50, value: -75, width: 200 });
		expect(g.ok).toBe(true);
		if (g.ok) {
			expect(g.markerX).toBe(100);
			expect(g.clamped).toBe(false);
		}
	});
});
