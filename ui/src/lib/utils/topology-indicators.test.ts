import { describe, it, expect } from 'vitest';
import {
	edgeIndicatorPosition,
	nodeIndicatorPosition,
	parseSvgNumber,
} from './topology-indicators.js';

describe('nodeIndicatorPosition', () => {
	it('places the dot center at the NE shoulder of the node circle', () => {
		// circle at origin, radius 10 — the dot center should sit at
		// (10 * 0.71, -10 * 0.71) = (7.1, -7.1) i.e. on the NE diagonal at the
		// circle's edge.
		const result = nodeIndicatorPosition({ cx: 0, cy: 0, r: 10 });
		expect(result.cx).toBeCloseTo(7.1, 5);
		expect(result.cy).toBeCloseTo(-7.1, 5);
	});

	it('shifts the dot offset by the node center coordinates', () => {
		// Translation of the node center should translate the dot identically
		// — the math is purely additive.
		const result = nodeIndicatorPosition({ cx: 100, cy: 50, r: 10 });
		expect(result.cx).toBeCloseTo(107.1, 5);
		expect(result.cy).toBeCloseTo(42.9, 5);
	});

	it('scales the dot radius proportionally to the node radius', () => {
		// 0.55x of the input radius — keeps the dot visible on tiny nodes and
		// not oversize on large ones.
		expect(nodeIndicatorPosition({ cx: 0, cy: 0, r: 10 }).r).toBeCloseTo(5.5, 5);
		expect(nodeIndicatorPosition({ cx: 0, cy: 0, r: 4 }).r).toBeCloseTo(2.2, 5);
		expect(nodeIndicatorPosition({ cx: 0, cy: 0, r: 100 }).r).toBeCloseTo(55, 5);
	});

	it('handles a zero-radius node degenerately (zero-size dot at center)', () => {
		// Defensive — dag-map will never emit r=0 in production but the helper
		// must not return NaN if it ever does.
		const result = nodeIndicatorPosition({ cx: 5, cy: 5, r: 0 });
		expect(result.cx).toBe(5);
		expect(result.cy).toBe(5);
		expect(result.r).toBe(0);
	});

	it('handles negative coordinates (SVG user-space allows negatives)', () => {
		const result = nodeIndicatorPosition({ cx: -50, cy: -50, r: 10 });
		expect(result.cx).toBeCloseTo(-42.9, 5);
		expect(result.cy).toBeCloseTo(-57.1, 5);
	});
});

describe('edgeIndicatorPosition', () => {
	it('returns the supplied midpoint with a fixed radius of 3', () => {
		const result = edgeIndicatorPosition({ x: 100, y: 200 });
		expect(result.cx).toBe(100);
		expect(result.cy).toBe(200);
		expect(result.r).toBe(3);
	});

	it('does not depend on the magnitude of the input (fixed dot size)', () => {
		// The radius is intentionally constant — independent of any path or
		// stroke geometry — so the dot is readable on both thin and thick edges.
		expect(edgeIndicatorPosition({ x: 0, y: 0 }).r).toBe(3);
		expect(edgeIndicatorPosition({ x: 1000, y: 1000 }).r).toBe(3);
	});

	it('handles negative midpoint coordinates', () => {
		const result = edgeIndicatorPosition({ x: -10, y: -20 });
		expect(result.cx).toBe(-10);
		expect(result.cy).toBe(-20);
	});
});

describe('parseSvgNumber', () => {
	it('parses a plain integer string', () => {
		expect(parseSvgNumber('42')).toBe(42);
	});

	it('parses a decimal string', () => {
		expect(parseSvgNumber('3.14')).toBeCloseTo(3.14, 5);
	});

	it('parses a negative number', () => {
		expect(parseSvgNumber('-7.5')).toBe(-7.5);
	});

	it('trims surrounding whitespace before parsing', () => {
		// The dag-map writes attributes via toFixed(1) so trimming is defensive
		// — but the helper must be robust to whitespace pollution.
		expect(parseSvgNumber('  42  ')).toBe(42);
	});

	it('returns null for an empty string (no attribute value)', () => {
		expect(parseSvgNumber('')).toBeNull();
	});

	it('returns null for a whitespace-only string', () => {
		// `'   '.trim()` becomes `''` which is the empty-string branch.
		expect(parseSvgNumber('   ')).toBeNull();
	});

	it('returns null when the attribute is null (querySelector miss)', () => {
		expect(parseSvgNumber(null)).toBeNull();
	});

	it('returns null when the attribute is undefined', () => {
		expect(parseSvgNumber(undefined)).toBeNull();
	});

	it('returns null for a non-numeric string', () => {
		// Number('foo') is NaN; the !isFinite branch catches that.
		expect(parseSvgNumber('foo')).toBeNull();
	});

	it('returns null for "NaN" literal', () => {
		expect(parseSvgNumber('NaN')).toBeNull();
	});

	it('returns null for "Infinity" literal', () => {
		// Number('Infinity') is Infinity — !isFinite catches it.
		expect(parseSvgNumber('Infinity')).toBeNull();
	});

	it('returns null for "-Infinity" literal', () => {
		expect(parseSvgNumber('-Infinity')).toBeNull();
	});
});
