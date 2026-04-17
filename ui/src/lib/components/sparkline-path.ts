// Pure path computation for the Sparkline component.
//
// Extracted from sparkline.svelte so it can be unit-tested without DOM.
// Given a series of numeric values, computes an SVG path string that
// draws the line chart within the given viewport.
//
// Special cases:
//   - Empty values → empty path
//   - Single value → horizontal line at vertical center
//   - All-same values (min == max) → horizontal line at vertical center
//   - Multi-value varying → polyline normalized to [min, max]
//   - NaN/non-finite values → break the path (start a new M segment)

export interface SparklinePathOptions {
	width: number;
	height: number;
	strokeWidth?: number;
}

export function computeSparklinePath(
	values: number[],
	{ width, height, strokeWidth = 1.5 }: SparklinePathOptions,
): string {
	if (values.length === 0) return '';

	const centerY = height / 2;

	if (values.length === 1) {
		return isFinite(values[0]) ? `M 0 ${centerY} L ${width} ${centerY}` : '';
	}

	// Compute min/max over finite values only
	let min = Infinity;
	let max = -Infinity;
	let finiteCount = 0;
	for (const v of values) {
		if (isFinite(v)) {
			if (v < min) min = v;
			if (v > max) max = v;
			finiteCount++;
		}
	}

	if (finiteCount === 0) return '';

	const range = max - min;
	const stepX = width / (values.length - 1);
	const padY = strokeWidth;
	const usableH = height - 2 * padY;

	const parts: string[] = [];
	let needMove = true;

	for (let i = 0; i < values.length; i++) {
		const v = values[i];
		if (!isFinite(v)) {
			needMove = true;
			continue;
		}
		const x = i * stepX;
		const y = range === 0 ? centerY : padY + usableH - ((v - min) / range) * usableH;
		parts.push(`${needMove ? 'M' : 'L'} ${x.toFixed(2)} ${y.toFixed(2)}`);
		needMove = false;
	}

	return parts.join(' ');
}
