import { describe, it, expect } from 'vitest';
import {
	formatBinAbsolute,
	formatBinOffset,
	formatBinTime,
} from './heatmap-time-format.js';

/**
 * Time formatting for heatmap bin axis + cell tooltips (m-E21-06 AC3 + AC11).
 *
 * `formatBinAbsolute(iso)` renders the UTC hour:minute from an ISO timestamp
 * and falls back to the raw string on parse failure. `formatBinOffset(bin,
 * binSize, binUnit)` produces `+HH:MM` strings relative to bin 0. The
 * composite `formatBinTime(bin, timestamps, grid)` picks whichever form
 * applies: absolute when a matching timestamp exists, offset otherwise.
 */

describe('formatBinAbsolute', () => {
	it('formats a valid ISO timestamp as HH:MM (UTC)', () => {
		expect(formatBinAbsolute('2025-01-01T09:30:00Z')).toBe('09:30');
		expect(formatBinAbsolute('2025-01-01T00:00:00Z')).toBe('00:00');
		expect(formatBinAbsolute('2025-01-01T23:59:00Z')).toBe('23:59');
	});

	it('falls back to the raw string on parse failure', () => {
		expect(formatBinAbsolute('not-a-date')).toBe('not-a-date');
	});
});

describe('formatBinOffset', () => {
	it('converts minutes into +HH:MM', () => {
		expect(formatBinOffset(0, 1, 'minutes')).toBe('+00:00');
		expect(formatBinOffset(90, 1, 'minutes')).toBe('+01:30');
		expect(formatBinOffset(2, 30, 'minutes')).toBe('+01:00');
	});

	it('handles seconds', () => {
		expect(formatBinOffset(60, 1, 'seconds')).toBe('+00:01');
		expect(formatBinOffset(3600, 1, 'seconds')).toBe('+01:00');
	});

	it('handles hours', () => {
		expect(formatBinOffset(3, 1, 'hours')).toBe('+03:00');
		expect(formatBinOffset(1, 2, 'hours')).toBe('+02:00');
	});

	it('handles days', () => {
		expect(formatBinOffset(1, 1, 'days')).toBe('+24:00');
	});

	it('handles negative offset (bin below start — theoretical)', () => {
		expect(formatBinOffset(-60, 1, 'minutes')).toBe('-01:00');
	});

	it('defaults to minutes for unknown units', () => {
		expect(formatBinOffset(60, 1, 'eons' as 'minutes')).toBe('+01:00');
	});
});

describe('formatBinTime (composite)', () => {
	it('uses absolute time when a matching timestamp exists', () => {
		expect(
			formatBinTime(0, ['2025-01-01T09:30:00Z'], { binSize: 1, binUnit: 'minutes' })
		).toBe('09:30');
	});

	it('falls back to offset when no timestamps array is provided', () => {
		expect(formatBinTime(30, undefined, { binSize: 1, binUnit: 'minutes' })).toBe('+00:30');
	});

	it('falls back to offset when the timestamp for the bin is missing', () => {
		expect(
			formatBinTime(5, ['2025-01-01T09:00:00Z'], { binSize: 1, binUnit: 'minutes' })
		).toBe('+00:05');
	});

	it('uses a default grid when none is supplied (minutes / binSize=1)', () => {
		expect(formatBinTime(5, undefined, undefined)).toBe('+00:05');
	});
});
