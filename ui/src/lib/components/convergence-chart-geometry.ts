/**
 * Pure geometry for ConvergenceChart.
 *
 * Consumes a **normalized** trace shape (see AC5 in the m-E21-04 spec):
 *     Array<{ iteration: number; metricMean: number }>
 *
 * Each caller adapts its response into this shape before passing it in. The
 * chart never branches on surface type. Goal Seek's boundary-evaluation
 * entries land as two points at `iteration: 0` — the geometry handles
 * overlapping x without special-casing.
 */

export interface ConvergenceTracePoint {
	iteration: number;
	metricMean: number;
}

export interface ConvergenceChartInput {
	trace: ConvergenceTracePoint[];
	width: number;
	height: number;
	padding: { top: number; right: number; bottom: number; left: number };
	/** Optional horizontal reference line; ignored when non-finite. */
	target?: number;
}

export interface ConvergenceChartPoint {
	iteration: number;
	metricMean: number;
	x: number;
	y: number;
	isFinal: boolean;
}

export interface ConvergenceChartGeometry {
	points: ConvergenceChartPoint[];
	/** SVG path data connecting points in input order; empty when < 2 points. */
	path: string;
	plotLeft: number;
	plotRight: number;
	plotTop: number;
	plotBottom: number;
	yMin: number;
	yMax: number;
	/** SVG y-coordinate of the target reference line, or null when no target. */
	targetY: number | null;
	xTicks: { x: number; label: string }[];
	yTicks: { y: number; label: string }[];
}

export function convergenceChartGeometry(
	input: ConvergenceChartInput,
): ConvergenceChartGeometry {
	const { trace, width, height, padding, target } = input;
	const plotLeft = padding.left;
	const plotRight = width - padding.right;
	const plotTop = padding.top;
	const plotBottom = height - padding.bottom;
	const plotWidth = Math.max(1, plotRight - plotLeft);
	const plotHeight = Math.max(1, plotBottom - plotTop);

	const finitePoints = (trace ?? []).filter(
		(p) =>
			p &&
			typeof p.iteration === 'number' &&
			isFinite(p.iteration) &&
			typeof p.metricMean === 'number' &&
			isFinite(p.metricMean),
	);

	if (finitePoints.length === 0) {
		return {
			points: [],
			path: '',
			plotLeft,
			plotRight,
			plotTop,
			plotBottom,
			yMin: 0,
			yMax: 1,
			targetY: null,
			xTicks: [],
			yTicks: [],
		};
	}

	// X axis: iterations
	const iterations = finitePoints.map((p) => p.iteration);
	const iMin = Math.min(...iterations);
	const iMax = Math.max(...iterations);
	const iRange = iMax - iMin;

	// Y axis: metric means, optionally extended to include the target.
	let yMin = Math.min(...finitePoints.map((p) => p.metricMean));
	let yMax = Math.max(...finitePoints.map((p) => p.metricMean));
	const hasTarget = typeof target === 'number' && isFinite(target);
	if (hasTarget) {
		if ((target as number) < yMin) yMin = target as number;
		if ((target as number) > yMax) yMax = target as number;
	}
	if (yMin === yMax) {
		const delta = yMin === 0 ? 1 : Math.abs(yMin) * 0.1;
		yMin -= delta;
		yMax += delta;
	}
	const yRange = yMax - yMin;

	const projectX = (iteration: number): number => {
		if (iRange === 0) return (plotLeft + plotRight) / 2;
		return plotLeft + ((iteration - iMin) / iRange) * plotWidth;
	};
	const projectY = (metric: number): number => {
		return plotBottom - ((metric - yMin) / yRange) * plotHeight;
	};

	const lastIndex = finitePoints.length - 1;
	const points: ConvergenceChartPoint[] = finitePoints.map((p, i) => ({
		iteration: p.iteration,
		metricMean: p.metricMean,
		x: projectX(p.iteration),
		y: projectY(p.metricMean),
		isFinal: i === lastIndex,
	}));

	let path = '';
	if (points.length >= 2) {
		const parts: string[] = [];
		for (let i = 0; i < points.length; i++) {
			parts.push(
				`${i === 0 ? 'M' : 'L'} ${points[i].x.toFixed(2)} ${points[i].y.toFixed(2)}`,
			);
		}
		path = parts.join(' ');
	}

	const targetY = hasTarget ? projectY(target as number) : null;

	// X ticks from unique iterations (up to 6 evenly-spaced labels).
	const uniqueIterations = Array.from(new Set(iterations)).sort((a, b) => a - b);
	const tickCount = Math.min(uniqueIterations.length, 6);
	const xTicks: { x: number; label: string }[] = [];
	if (tickCount === 1) {
		xTicks.push({ x: projectX(uniqueIterations[0]), label: String(uniqueIterations[0]) });
	} else {
		const stride = (uniqueIterations.length - 1) / (tickCount - 1);
		for (let i = 0; i < tickCount; i++) {
			const idx = Math.round(i * stride);
			const it = uniqueIterations[idx];
			xTicks.push({ x: projectX(it), label: String(it) });
		}
	}

	// Y ticks: min, mid, max.
	const yMid = (yMin + yMax) / 2;
	const yTicks = [yMin, yMid, yMax].map((v) => ({
		y: projectY(v),
		label: formatYTick(v),
	}));

	return {
		points,
		path,
		plotLeft,
		plotRight,
		plotTop,
		plotBottom,
		yMin,
		yMax,
		targetY,
		xTicks,
		yTicks,
	};
}

function formatYTick(v: number): string {
	if (Number.isInteger(v)) return v.toString();
	const abs = Math.abs(v);
	if (abs >= 100) return v.toFixed(0);
	if (abs >= 1) return v.toFixed(2);
	if (abs >= 0.01) return v.toFixed(3);
	return v.toExponential(1);
}
