// Pure geometry for the Chart component.
//
// Computes SVG path data, axis tick positions, and bin-index from cursor X.
// No DOM dependencies — unit-tested.

export interface ChartSeries {
	name: string;
	values: number[];
	color?: string;
}

export interface ChartLayout {
	width: number;
	height: number;
	padding: {
		top: number;
		right: number;
		bottom: number;
		left: number;
	};
}

export interface ChartGeometry {
	/** SVG path strings per series, in the same order as input. */
	paths: { name: string; color?: string; d: string }[];
	/** Plot-area X-range in SVG coords. */
	plotLeft: number;
	plotRight: number;
	/** Plot-area Y-range in SVG coords. */
	plotTop: number;
	plotBottom: number;
	/** Data range (shared y-axis across all series). */
	yMin: number;
	yMax: number;
	/** Number of bins (x-axis length). */
	bins: number;
	/** X tick positions and labels for the bin axis. */
	xTicks: { x: number; label: string }[];
	/** Y tick positions and labels for the value axis. */
	yTicks: { y: number; label: string }[];
}

export const DEFAULT_PADDING = { top: 8, right: 8, bottom: 20, left: 36 };

/**
 * Compute the full chart geometry for one or more series.
 *
 * All series must have the same length. If they differ, the shortest length wins.
 * If no series have values, returns an empty geometry.
 */
export function computeChartGeometry(
	series: ChartSeries[],
	layout: ChartLayout,
): ChartGeometry {
	const pad = layout.padding;
	const plotLeft = pad.left;
	const plotRight = layout.width - pad.right;
	const plotTop = pad.top;
	const plotBottom = layout.height - pad.bottom;
	const plotWidth = Math.max(1, plotRight - plotLeft);
	const plotHeight = Math.max(1, plotBottom - plotTop);

	// Filter out empty series and find shared length
	const nonEmpty = series.filter((s) => s.values.length > 0);
	if (nonEmpty.length === 0) {
		return {
			paths: [],
			plotLeft,
			plotRight,
			plotTop,
			plotBottom,
			yMin: 0,
			yMax: 1,
			bins: 0,
			xTicks: [],
			yTicks: [],
		};
	}

	const bins = Math.min(...nonEmpty.map((s) => s.values.length));

	// Global Y range
	let yMin = Infinity;
	let yMax = -Infinity;
	for (const s of nonEmpty) {
		for (let i = 0; i < bins; i++) {
			const v = s.values[i];
			if (v < yMin) yMin = v;
			if (v > yMax) yMax = v;
		}
	}
	// Handle flat series (min == max) by padding the range
	if (yMin === yMax) {
		const delta = yMin === 0 ? 1 : Math.abs(yMin) * 0.1;
		yMin -= delta;
		yMax += delta;
	}
	const yRange = yMax - yMin;

	const stepX = bins > 1 ? plotWidth / (bins - 1) : 0;

	// Build path strings
	const paths = nonEmpty.map((s) => {
		const parts: string[] = [];
		for (let i = 0; i < bins; i++) {
			const x = plotLeft + i * stepX;
			const y = plotBottom - ((s.values[i] - yMin) / yRange) * plotHeight;
			parts.push(`${i === 0 ? 'M' : 'L'} ${x.toFixed(2)} ${y.toFixed(2)}`);
		}
		return { name: s.name, color: s.color, d: parts.join(' ') };
	});

	// X ticks: first, middle, last (at most 5 ticks)
	const xTicks = computeXTicks(bins, plotLeft, plotRight);
	// Y ticks: min, mid, max
	const yTicks = computeYTicks(yMin, yMax, plotBottom, plotTop);

	return {
		paths,
		plotLeft,
		plotRight,
		plotTop,
		plotBottom,
		yMin,
		yMax,
		bins,
		xTicks,
		yTicks,
	};
}

function computeXTicks(bins: number, left: number, right: number): { x: number; label: string }[] {
	if (bins === 0) return [];
	if (bins === 1) return [{ x: left, label: '0' }];

	// Use at most 5 evenly-spaced ticks, but never more than `bins`
	const maxTicks = Math.min(5, bins);
	const step = (bins - 1) / (maxTicks - 1);
	const ticks: { x: number; label: string }[] = [];
	for (let i = 0; i < maxTicks; i++) {
		const binIdx = Math.round(i * step);
		const x = left + (binIdx / (bins - 1)) * (right - left);
		ticks.push({ x, label: binIdx.toString() });
	}
	return ticks;
}

function computeYTicks(
	yMin: number,
	yMax: number,
	bottom: number,
	top: number,
): { y: number; label: string }[] {
	// 3 ticks: min, mid, max
	const mid = (yMin + yMax) / 2;
	const values = [yMin, mid, yMax];
	return values.map((v) => ({
		y: bottom - ((v - yMin) / (yMax - yMin)) * (bottom - top),
		label: formatTick(v),
	}));
}

function formatTick(v: number): string {
	if (Number.isInteger(v)) return v.toString();
	// Show up to 2 decimal places but trim trailing zeros
	return parseFloat(v.toFixed(2)).toString();
}

/**
 * Given a mouse X coordinate in SVG space, return the nearest bin index (0-indexed).
 * Returns null if the chart has no bins.
 */
export function binFromX(
	mouseX: number,
	plotLeft: number,
	plotRight: number,
	bins: number,
): number | null {
	if (bins === 0) return null;
	if (bins === 1) return 0;
	const clamped = Math.max(plotLeft, Math.min(plotRight, mouseX));
	const frac = (clamped - plotLeft) / (plotRight - plotLeft);
	return Math.round(frac * (bins - 1));
}

/**
 * Given a bin index, return the X coordinate within the plot area.
 */
export function xFromBin(
	binIdx: number,
	plotLeft: number,
	plotRight: number,
	bins: number,
): number {
	if (bins === 0) return plotLeft;
	if (bins === 1) return plotLeft;
	return plotLeft + (binIdx / (bins - 1)) * (plotRight - plotLeft);
}
