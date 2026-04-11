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
		return `M 0 ${centerY} L ${width} ${centerY}`;
	}

	let min = Infinity;
	let max = -Infinity;
	for (const v of values) {
		if (v < min) min = v;
		if (v > max) max = v;
	}

	const range = max - min;
	const stepX = width / (values.length - 1);
	const padY = strokeWidth;
	const usableH = height - 2 * padY;

	// If all values are identical, draw a horizontal line at vertical center
	if (range === 0) {
		return values
			.map((_, i) => {
				const x = i * stepX;
				return `${i === 0 ? 'M' : 'L'} ${x.toFixed(2)} ${centerY.toFixed(2)}`;
			})
			.join(' ');
	}

	return values
		.map((v, i) => {
			const x = i * stepX;
			// Higher values → lower Y (SVG y grows downward)
			const y = padY + usableH - ((v - min) / range) * usableH;
			return `${i === 0 ? 'M' : 'L'} ${x.toFixed(2)} ${y.toFixed(2)}`;
		})
		.join(' ');
}
