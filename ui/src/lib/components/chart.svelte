<script lang="ts">
	// Interactive time-series chart with axes, hover tooltip, and multi-series overlay.
	// Pure geometry lives in chart-geometry.ts (unit-tested).
	//
	// Usage:
	//   <Chart series={[{ name: 'served', values: [1,2,3], color: 'var(--ft-viz-teal)' }]} />

	import {
		computeChartGeometry,
		binFromX,
		xFromBin,
		crosshairX,
		DEFAULT_PADDING,
		type ChartSeries,
	} from './chart-geometry.js';

	interface Props {
		series: ChartSeries[];
		width?: number;
		height?: number;
		title?: string;
		/** When set, renders a dashed vertical crosshair at this bin index. */
		crosshairBin?: number;
	}

	let { series, width = 320, height = 140, title, crosshairBin }: Props = $props();

	let svgEl = $state<SVGSVGElement | null>(null);
	let hoverBin = $state<number | null>(null);
	let mouseY = $state<number>(0);

	const layout = $derived({
		width,
		height,
		padding: DEFAULT_PADDING,
	});

	const geometry = $derived(computeChartGeometry(series, layout));

	const DEFAULT_COLORS = [
		'var(--ft-viz-teal)',
		'var(--ft-viz-coral)',
		'var(--ft-viz-blue)',
		'var(--ft-viz-amber)',
		'var(--ft-viz-green)',
		'var(--ft-viz-pink)',
		'var(--ft-viz-purple)',
	];

	function colorFor(index: number, color?: string): string {
		return color ?? DEFAULT_COLORS[index % DEFAULT_COLORS.length];
	}

	function onMouseMove(e: MouseEvent) {
		if (!svgEl) return;
		const rect = svgEl.getBoundingClientRect();
		const x = ((e.clientX - rect.left) / rect.width) * width;
		const y = ((e.clientY - rect.top) / rect.height) * height;
		mouseY = y;
		hoverBin = binFromX(x, geometry.plotLeft, geometry.plotRight, geometry.bins);
	}

	function onMouseLeave() {
		hoverBin = null;
	}

	const hoverX = $derived(
		hoverBin !== null
			? xFromBin(hoverBin, geometry.plotLeft, geometry.plotRight, geometry.bins)
			: null,
	);

	const tooltipLines = $derived.by(() => {
		if (hoverBin === null) return [];
		return series
			.filter((s) => s.values.length > hoverBin!)
			.map((s, i) => ({
				name: s.name,
				value: s.values[hoverBin!],
				color: colorFor(i, s.color),
			}));
	});

	function formatTooltipValue(v: number): string {
		if (Number.isInteger(v)) return v.toString();
		return v.toFixed(2);
	}
</script>

<div class="chart-container" data-testid="chart">
	{#if title}
		<div class="chart-title">{title}</div>
	{/if}

	<svg
		bind:this={svgEl}
		{width}
		{height}
		viewBox="0 0 {width} {height}"
		onmousemove={onMouseMove}
		onmouseleave={onMouseLeave}
		role="img"
		aria-label={title ?? 'time series chart'}
	>
		<!-- Y-axis grid lines + labels -->
		{#each geometry.yTicks as tick, i (i)}
			<line
				x1={geometry.plotLeft}
				x2={geometry.plotRight}
				y1={tick.y}
				y2={tick.y}
				stroke="var(--border)"
				stroke-width="0.5"
				stroke-dasharray="2,2"
			/>
			<text
				x={geometry.plotLeft - 4}
				y={tick.y + 3}
				text-anchor="end"
				font-size="9"
				fill="var(--muted-foreground)"
				font-family="ui-monospace, monospace"
			>
				{tick.label}
			</text>
		{/each}

		<!-- X-axis labels -->
		{#each geometry.xTicks as tick, i (i)}
			<text
				x={tick.x}
				y={geometry.plotBottom + 14}
				text-anchor="middle"
				font-size="9"
				fill="var(--muted-foreground)"
				font-family="ui-monospace, monospace"
			>
				{tick.label}
			</text>
		{/each}

		<!-- X-axis baseline -->
		<line
			x1={geometry.plotLeft}
			x2={geometry.plotRight}
			y1={geometry.plotBottom}
			y2={geometry.plotBottom}
			stroke="var(--border)"
			stroke-width="0.5"
		/>

		<!-- Series paths -->
		{#each geometry.paths as path, i (path.name)}
			<path
				d={path.d}
				fill="none"
				stroke={colorFor(i, path.color)}
				stroke-width="1"
				stroke-linecap="round"
				stroke-linejoin="round"
				data-testid="chart-path-{path.name}"
			/>
		{/each}

		<!-- Time-scrubber crosshair (m-E17-06) — fixed bin position from parent -->
		{#if crosshairBin !== undefined && crosshairX(crosshairBin, geometry) !== null}
			<line
				x1={crosshairX(crosshairBin, geometry)!}
				x2={crosshairX(crosshairBin, geometry)!}
				y1={geometry.plotTop}
				y2={geometry.plotBottom}
				stroke="var(--muted-foreground)"
				stroke-width="1"
				stroke-dasharray="4 2"
				pointer-events="none"
				data-testid="crosshair"
			/>
		{/if}

		<!-- Hover crosshair -->
		{#if hoverX !== null}
			<line
				x1={hoverX}
				x2={hoverX}
				y1={geometry.plotTop}
				y2={geometry.plotBottom}
				stroke="var(--muted-foreground)"
				stroke-width="0.5"
				stroke-dasharray="3,3"
				pointer-events="none"
			/>
		{/if}
	</svg>

	{#if hoverBin !== null && tooltipLines.length > 0}
		<div
			class="chart-tooltip"
			style="left: {hoverX ?? 0}px; top: {Math.max(0, mouseY - 40)}px;"
			data-testid="chart-tooltip"
		>
			<div class="tooltip-bin">bin {hoverBin}</div>
			{#each tooltipLines as line (line.name)}
				<div class="flex items-center gap-1.5">
					<span class="inline-block h-1.5 w-1.5 rounded-full" style="background: {line.color};"></span>
					<span class="text-[10px] font-mono">{line.name}: {formatTooltipValue(line.value)}</span>
				</div>
			{/each}
		</div>
	{/if}
</div>

<style>
	.chart-container {
		position: relative;
		display: inline-block;
	}
	.chart-title {
		font-size: 10px;
		font-weight: 600;
		color: var(--foreground);
		margin-bottom: 2px;
		font-family: ui-monospace, monospace;
	}
	.chart-tooltip {
		position: absolute;
		background: var(--popover);
		border: 1px solid var(--border);
		border-radius: 4px;
		padding: 4px 6px;
		pointer-events: none;
		box-shadow: 0 2px 8px rgba(0, 0, 0, 0.15);
		transform: translateX(8px);
		z-index: 10;
		white-space: nowrap;
		color: var(--popover-foreground);
	}
	.tooltip-bin {
		font-size: 9px;
		color: var(--muted-foreground);
		font-family: ui-monospace, monospace;
	}
</style>
