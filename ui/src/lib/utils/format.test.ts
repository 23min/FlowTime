import { describe, it, expect } from 'vitest';
import {
	formatValue,
	formatSeries,
	isInternalSeries,
	isCompilerTemp,
	isPerClassSeries,
	parseClassSeries,
} from './format';

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

	it('returns true for __edge_ prefixed names', () => {
		expect(isInternalSeries('__edge_foo')).toBe(true);
	});

	it('returns false for per-class column names (user-visible now)', () => {
		// Per-class series are user-visible in class-based models
		expect(isInternalSeries('arrivals__class_Order')).toBe(false);
		expect(isInternalSeries('served__class_Refund')).toBe(false);
	});

	it('is case-sensitive', () => {
		expect(isInternalSeries('ARRIVALS')).toBe(false);
	});

	it('empty string is not internal', () => {
		expect(isInternalSeries('')).toBe(false);
	});
});

describe('isCompilerTemp', () => {
	it('matches only __temp_ and __edge_ prefixes', () => {
		expect(isCompilerTemp('__temp_0')).toBe(true);
		expect(isCompilerTemp('__edge_a_b_flowVolume')).toBe(true);
		expect(isCompilerTemp('arrivals')).toBe(false);
		expect(isCompilerTemp('arrivals__class_Order')).toBe(false);
	});

	it('does not match bare __ prefix', () => {
		// e.g. hypothetical __foo that isn't __temp_ or __edge_
		expect(isCompilerTemp('__foo')).toBe(false);
	});
});

describe('isPerClassSeries', () => {
	it('matches columns containing __class_', () => {
		expect(isPerClassSeries('arrivals__class_Order')).toBe(true);
		expect(isPerClassSeries('served__class_Refund')).toBe(true);
	});

	it('does not match base series', () => {
		expect(isPerClassSeries('arrivals')).toBe(false);
		expect(isPerClassSeries('served')).toBe(false);
	});

	it('does not match compiler temps', () => {
		expect(isPerClassSeries('__temp_0')).toBe(false);
	});
});

describe('parseClassSeries', () => {
	it('parses a valid per-class name', () => {
		expect(parseClassSeries('arrivals__class_Order')).toEqual({
			base: 'arrivals',
			classId: 'Order',
		});
	});

	it('parses class id with underscores', () => {
		expect(parseClassSeries('served__class_Premium_Tier')).toEqual({
			base: 'served',
			classId: 'Premium_Tier',
		});
	});

	it('returns null for non-class name', () => {
		expect(parseClassSeries('arrivals')).toBeNull();
	});

	it('returns null when class id is empty', () => {
		expect(parseClassSeries('arrivals__class_')).toBeNull();
	});

	it('returns null when base is empty', () => {
		expect(parseClassSeries('__class_Order')).toBeNull();
	});
});
