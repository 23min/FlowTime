import { describe, it, expect } from 'vitest';
import {
	defaultLayout,
	barAreaWidth,
	barCenter,
	barGeometry,
	fmtBarValue,
} from './sensitivity-bar-geometry.js';

describe('defaultLayout', () => {
	it('returns a layout with expected widths', () => {
		const l = defaultLayout(500);
		expect(l.labelWidth).toBe(120);
		expect(l.valueWidth).toBe(56);
		expect(l.width).toBe(500);
	});
});

describe('barAreaWidth', () => {
	it('subtracts label and value widths from total', () => {
		expect(barAreaWidth(defaultLayout(500))).toBe(500 - 120 - 56);
	});

	it('enforces minimum of 80px', () => {
		// width=150 → 150-120-56 = -26, but min 80
		expect(barAreaWidth(defaultLayout(150))).toBe(80);
	});

	it('minimum applies even at very small widths', () => {
		expect(barAreaWidth(defaultLayout(0))).toBe(80);
	});
});

describe('barCenter', () => {
	it('places center at labelWidth + area/2', () => {
		const l = defaultLayout(500);
		const area = barAreaWidth(l);
		expect(barCenter(l)).toBe(120 + area / 2);
	});
});

describe('barGeometry', () => {
	const layout = defaultLayout(500);
	const center = barCenter(layout);
	const area = barAreaWidth(layout);

	it('returns zero-width muted bar for non-finite gradient', () => {
		const g = barGeometry(NaN, 10, layout);
		expect(g.w).toBe(0);
		expect(g.x).toBe(center);
		expect(g.color).toBe('var(--muted-foreground)');
	});

	it('returns zero-width muted bar for Infinity gradient', () => {
		const g = barGeometry(Infinity, 10, layout);
		expect(g.w).toBe(0);
		expect(g.color).toBe('var(--muted-foreground)');
	});

	it('returns zero-width muted bar when max is 0', () => {
		const g = barGeometry(5, 0, layout);
		expect(g.w).toBe(0);
		expect(g.color).toBe('var(--muted-foreground)');
	});

	it('returns teal bar starting at center for positive gradient', () => {
		const g = barGeometry(5, 10, layout);
		expect(g.x).toBe(center);
		expect(g.w).toBeCloseTo(area / 4, 5); // 5/10 * area/2
		expect(g.color).toBe('var(--ft-viz-teal)');
	});

	it('returns coral bar ending at center for negative gradient', () => {
		const g = barGeometry(-5, 10, layout);
		expect(g.w).toBeCloseTo(area / 4, 5);
		expect(g.x).toBeCloseTo(center - g.w, 5);
		expect(g.color).toBe('var(--ft-viz-coral)');
	});

	it('full-width bar when gradient equals max', () => {
		const g = barGeometry(10, 10, layout);
		expect(g.w).toBeCloseTo(area / 2, 5);
	});

	it('tiny bar for tiny gradient', () => {
		const g = barGeometry(0.01, 10, layout);
		expect(g.w).toBeCloseTo((area / 2) * 0.001, 5);
	});

	it('zero gradient produces zero-width teal (non-negative branch)', () => {
		const g = barGeometry(0, 10, layout);
		expect(g.w).toBe(0);
		expect(g.color).toBe('var(--ft-viz-teal)');
	});
});

describe('fmtBarValue', () => {
	it('returns em-dash for non-finite', () => {
		expect(fmtBarValue(NaN)).toBe('—');
		expect(fmtBarValue(Infinity)).toBe('—');
		expect(fmtBarValue(-Infinity)).toBe('—');
	});

	it('formats large values without decimals (|v| >= 100)', () => {
		expect(fmtBarValue(123)).toBe('123');
		expect(fmtBarValue(-123.5)).toBe('-124');
		expect(fmtBarValue(1000)).toBe('1000');
	});

	it('formats mid-range values with two decimals (|v| in [1, 100))', () => {
		expect(fmtBarValue(2.5)).toBe('2.50');
		expect(fmtBarValue(-3)).toBe('-3.00');
		expect(fmtBarValue(99.99)).toBe('99.99');
	});

	it('formats small values with three decimals (|v| < 1)', () => {
		expect(fmtBarValue(0.5)).toBe('0.500');
		expect(fmtBarValue(-0.123)).toBe('-0.123');
		expect(fmtBarValue(0)).toBe('0.000');
	});

	it('boundary values', () => {
		expect(fmtBarValue(100)).toBe('100');
		expect(fmtBarValue(1)).toBe('1.00');
		expect(fmtBarValue(0.9999999)).toBe('1.000');
	});
});
