import { describe, it, expect } from 'vitest';
import { computeSparklinePath } from './sparkline-path';

describe('computeSparklinePath', () => {
	it('returns empty string for empty values', () => {
		expect(computeSparklinePath([], { width: 100, height: 30 })).toBe('');
	});

	it('draws horizontal line for single value', () => {
		const path = computeSparklinePath([5], { width: 100, height: 30 });
		// centerY = 15
		expect(path).toBe('M 0 15 L 100 15');
	});

	it('draws horizontal line for all-same values (min == max)', () => {
		const path = computeSparklinePath([5, 5, 5, 5], { width: 120, height: 30 });
		// stepX = 40, centerY = 15
		expect(path).toBe('M 0.00 15.00 L 40.00 15.00 L 80.00 15.00 L 120.00 15.00');
	});

	it('normalizes multi-value series to [min, max] with Y inverted', () => {
		// values=[0, 10], width=100, height=30, strokeWidth=1.5
		// stepX = 100, padY = 1.5, usableH = 27
		// v=0 (min): y = 1.5 + 27 - 0 = 28.5
		// v=10 (max): y = 1.5 + 27 - 27 = 1.5
		const path = computeSparklinePath([0, 10], { width: 100, height: 30 });
		expect(path).toBe('M 0.00 28.50 L 100.00 1.50');
	});

	it('handles three values with intermediate normalization', () => {
		// values=[0, 5, 10], width=100, height=30, strokeWidth=1.5
		// stepX = 50, padY = 1.5, usableH = 27
		// v=0: y = 28.5; v=5: y = 1.5 + 27 - 13.5 = 15; v=10: y = 1.5
		const path = computeSparklinePath([0, 5, 10], { width: 100, height: 30 });
		expect(path).toBe('M 0.00 28.50 L 50.00 15.00 L 100.00 1.50');
	});

	it('handles negative values correctly', () => {
		// values=[-10, 0, 10], range = 20
		// v=-10 (min): y at bottom; v=10 (max): y at top
		const path = computeSparklinePath([-10, 0, 10], { width: 100, height: 30 });
		// padY=1.5, usableH=27
		// v=-10: y = 1.5 + 27 - 0 = 28.5
		// v=0: y = 1.5 + 27 - 13.5 = 15
		// v=10: y = 1.5
		expect(path).toBe('M 0.00 28.50 L 50.00 15.00 L 100.00 1.50');
	});

	it('respects custom strokeWidth for padding', () => {
		// strokeWidth=4 → padY=4, usableH=22
		// values=[0, 10]: v=0: y = 4+22-0=26; v=10: y = 4+22-22=4
		const path = computeSparklinePath([0, 10], { width: 100, height: 30, strokeWidth: 4 });
		expect(path).toBe('M 0.00 26.00 L 100.00 4.00');
	});

	it('scales X proportionally for 4+ points', () => {
		// width=120, values=4, stepX=40
		const path = computeSparklinePath([1, 2, 3, 4], { width: 120, height: 30 });
		// Check that X values are at 0, 40, 80, 120
		expect(path).toContain('M 0.00 ');
		expect(path).toContain(' L 40.00 ');
		expect(path).toContain(' L 80.00 ');
		expect(path).toContain(' L 120.00 ');
	});

	it('does not produce NaN or Infinity for any input', () => {
		const inputs = [
			[0],
			[0, 0],
			[1e-10, 1e-10],
			[1e10, 2e10],
			[-1e10, 1e10],
			[0.5],
		];
		for (const values of inputs) {
			const path = computeSparklinePath(values, { width: 100, height: 30 });
			expect(path).not.toContain('NaN');
			expect(path).not.toContain('Infinity');
		}
	});
});
