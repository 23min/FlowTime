import { describe, it, expect } from 'vitest';
import { computeTicks, computePointerPct } from './timeline-scrubber-ticks.js';

describe('computeTicks', () => {
	it('returns empty arrays for binCount of 0', () => {
		expect(computeTicks(0)).toEqual({ major: [], minor: [] });
	});

	it('returns empty arrays for binCount of 1', () => {
		expect(computeTicks(1)).toEqual({ major: [], minor: [] });
	});

	it('produces two major ticks (0 and last) for small binCount', () => {
		// binCount=2 → targetLabels = min(10, max(2, 0)) = 2; step = max(1, 1) = 1
		// loop pushes bin=0; then bin=1. Last is already included, so no extra push.
		const result = computeTicks(2);
		expect(result.major).toHaveLength(2);
		expect(result.major[0]).toEqual({ bin: 0, pct: 0, label: '0' });
		expect(result.major[1]).toEqual({ bin: 1, pct: 100, label: '1' });
	});

	it('ensures last bin is included when step misses it', () => {
		// binCount=12 → targetLabels=2 (floor(12/5)=2), step=round(11/1)=11
		// loop pushes bin=0, bin=11 — both covered. Last branch: already included, skip.
		const result = computeTicks(12);
		expect(result.major.some((t) => t.bin === 0)).toBe(true);
		expect(result.major.some((t) => t.bin === 11)).toBe(true);
	});

	it('appends last bin when loop does not reach it', () => {
		// Construct a case where step doesn't land on last bin.
		// binCount=11 → targetLabels=min(10, max(2, 2))=2, step=round(10/1)=10
		// loop: bin=0, bin=10 — both pushed. Last is included naturally.
		// Try binCount=7 → targetLabels=max(2, 1)=2, step=round(6/1)=6
		// loop: bin=0, bin=6. Last included.
		// Try binCount=6 → targetLabels=max(2, 1)=2, step=round(5/1)=5
		// loop: bin=0, bin=5. Last included.
		// Try binCount=51 → targetLabels=min(10, max(2, 10))=10, step=round(50/9)=6
		// loop: 0, 6, 12, 18, 24, 30, 36, 42, 48 — last (50) missing, appended.
		const result = computeTicks(51);
		expect(result.major.some((t) => t.bin === 50)).toBe(true);
		// Verify the last tick has pct=100 and came from the append branch
		const last = result.major[result.major.length - 1];
		expect(last.bin).toBe(50);
		expect(last.pct).toBe(100);
	});

	it('produces minor ticks between major pairs', () => {
		// binCount=21 → targetLabels=min(10, max(2, 4))=4, step=round(20/3)=7
		// loop: 0, 7, 14. Last (20) missing → appended.
		// major: [0, 7, 14, 20]
		// minor midpoints: (0+7)/2=3.5→4, (7+14)/2=10.5→11, (14+20)/2=17
		const result = computeTicks(21);
		expect(result.major.map((t) => t.bin)).toEqual([0, 7, 14, 20]);
		expect(result.minor).toHaveLength(3);
	});

	it('skips minor tick when midpoint equals a boundary', () => {
		// binCount=3 → targetLabels=2, step=round(2/1)=2
		// loop: 0, 2 — major: [0, 2]
		// midpoint (0+2)/2=1 — different from both endpoints → one minor tick
		const result = computeTicks(3);
		expect(result.major.map((t) => t.bin)).toEqual([0, 2]);
		expect(result.minor).toHaveLength(1);

		// binCount=2 → major: [0, 1], midpoint round((0+1)/2)=1 equals boundary → skipped
		const small = computeTicks(2);
		expect(small.minor).toHaveLength(0);
	});

	it('produces correct percentages for all major ticks', () => {
		const result = computeTicks(11);
		for (const t of result.major) {
			expect(t.pct).toBeCloseTo((t.bin / 10) * 100, 5);
		}
	});

	it('labels are bin numbers as strings', () => {
		const result = computeTicks(5);
		for (const t of result.major) {
			expect(t.label).toBe(String(t.bin));
		}
	});

	it('handles very large binCount without exploding major tick count', () => {
		const result = computeTicks(10000);
		expect(result.major.length).toBeLessThanOrEqual(11); // 10 from loop + possible 1 appended
	});
});

describe('computePointerPct', () => {
	it('returns 0 for binCount of 0', () => {
		expect(computePointerPct(0, 0)).toBe(0);
	});

	it('returns 0 for binCount of 1 regardless of bin', () => {
		expect(computePointerPct(1, 0)).toBe(0);
	});

	it('computes percentage proportionally for multi-bin', () => {
		expect(computePointerPct(11, 0)).toBe(0);
		expect(computePointerPct(11, 5)).toBe(50);
		expect(computePointerPct(11, 10)).toBe(100);
	});

	it('handles mid-range bin in a larger series', () => {
		expect(computePointerPct(101, 25)).toBe(25);
	});
});
