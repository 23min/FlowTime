import { describe, it, expect } from 'vitest';
import { formatValue, formatSeries, isInternalSeries } from './format';

describe('formatValue', () => {
	it('formats integers without decimal', () => {
		expect(formatValue(0)).toBe('0');
		expect(formatValue(5)).toBe('5');
		expect(formatValue(-10)).toBe('-10');
		expect(formatValue(1000)).toBe('1000');
	});

	it('formats floats to 2 decimal places', () => {
		expect(formatValue(0.5)).toBe('0.50');
		expect(formatValue(3.14159)).toBe('3.14');
		expect(formatValue(-1.2345)).toBe('-1.23');
	});

	it('handles very small non-zero floats', () => {
		expect(formatValue(0.001)).toBe('0.00');
	});

	it('handles very large floats', () => {
		expect(formatValue(1234567.89)).toBe('1234567.89');
	});

	it('handles negative integers', () => {
		expect(formatValue(-42)).toBe('-42');
	});
});

describe('formatSeries', () => {
	it('formats empty array', () => {
		expect(formatSeries([])).toBe('[]');
	});

	it('formats single value', () => {
		expect(formatSeries([5])).toBe('[5]');
	});

	it('formats integer values with comma separator', () => {
		expect(formatSeries([1, 2, 3])).toBe('[1, 2, 3]');
	});

	it('formats mixed int and float values', () => {
		expect(formatSeries([1, 2.5, 3])).toBe('[1, 2.50, 3]');
	});

	it('formats all floats', () => {
		expect(formatSeries([1.1, 2.2, 3.3])).toBe('[1.10, 2.20, 3.30]');
	});
});

describe('isInternalSeries', () => {
	it('returns false for normal series names', () => {
		expect(isInternalSeries('arrivals')).toBe(false);
		expect(isInternalSeries('served')).toBe(false);
		expect(isInternalSeries('queue_depth')).toBe(false);
	});

	it('returns true for __temp_ prefixed names', () => {
		expect(isInternalSeries('__temp_0')).toBe(true);
		expect(isInternalSeries('__temp_42')).toBe(true);
	});

	it('returns true for any __ prefixed name', () => {
		expect(isInternalSeries('__edge_foo')).toBe(true);
		expect(isInternalSeries('__anything')).toBe(true);
	});

	it('returns true for per-class column names', () => {
		expect(isInternalSeries('arrivals__class_Order')).toBe(true);
		expect(isInternalSeries('served__class_Refund')).toBe(true);
	});

	it('is case-sensitive', () => {
		// Uppercase variants are not filtered
		expect(isInternalSeries('ARRIVALS')).toBe(false);
	});

	it('empty string is not internal', () => {
		expect(isInternalSeries('')).toBe(false);
	});
});
