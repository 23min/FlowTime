/**
 * Pure geometry for a single-interval SVG bar with a marker.
 *
 * Used by Goal Seek's search-interval visualization (one bar, marker at the
 * final paramValue) and — planned for m-E21-05 — Optimize's per-param range
 * table (one mini bar per row, marker at each optimum).
 */

export interface IntervalBarInput {
	lo: number;
	hi: number;
	value: number;
	width: number;
}

export type IntervalBarGeometry =
	| { ok: false }
	| {
			ok: true;
			barStart: number;
			barEnd: number;
			markerX: number;
			clamped: boolean;
	  };

export function intervalMarkerGeometry(input: IntervalBarInput): IntervalBarGeometry {
	const { lo, hi, value, width } = input;
	if (
		!isFinite(lo) ||
		!isFinite(hi) ||
		!isFinite(value) ||
		!isFinite(width) ||
		width <= 0 ||
		lo >= hi
	) {
		return { ok: false };
	}
	const barStart = 0;
	const barEnd = width;
	let clamped = false;
	let effective = value;
	if (value < lo) {
		effective = lo;
		clamped = true;
	} else if (value > hi) {
		effective = hi;
		clamped = true;
	}
	const frac = (effective - lo) / (hi - lo);
	const markerX = barStart + frac * (barEnd - barStart);
	return { ok: true, barStart, barEnd, markerX, clamped };
}
