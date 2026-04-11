<script lang="ts">
	// Interactive time-series chart with axes, hover tooltip, and multi-series overlay.
	// Pure geometry lives in chart-geometry.ts (unit-tested).
	//
	// Usage:
	//   <Chart series={[{ name: 'served', values: [1,2,3], color: '#2563eb' }]} />

	import {
		computeChartGeometry,
		binFromX,
		xFromBin,
		DEFAULT_PADDING,
		type ChartSeries,
	} from './chart-geometry.js';

	interface Props {
		series: ChartSeries[];
		width?: number;
		height?: number;
		title?: string;
	}

	let { series, width = 320, height = 140, title }: Props = $props();

	let svgEl = $state<SVGSVGElement | null>(null);
	let hoverBin = $state<number | null>(null);
	let mouseY = $state<number>(0);

	const layout = $derived({
		width,
		height,
		padding: DEFAULT_PADDING,
	});

	const geometry = $derived(computeChartGeometry(series, layout));

	const DEFAULT_COLORS = ['#2563eb', '#dc2626', '#16a34a', '#ea580c', '#9333ea'];

	function colorFor(index: number, color?: string): string {
		return color ?? DEFAULT_COLORS[index % DEFAULT_COLORS.length];
	}

	function onMouseMove(e: MouseEvent) {
		if (!svgEl) return;
		const rect = svgEl.getBoundingClientRect();
		// Convert client coords to SVG viewBox coords
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

	// Tooltip content: one line per series showing name + value at hoverBin
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
				stroke="#e5e7eb"
				stroke-width="1"
				stroke-dasharray="2,2"
			/>
			<text
				x={geometry.plotLeft - 4}
				y={tick.y + 3}
				text-anchor="end"
				font-size="10"
				fill="#6b7280"
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
				font-size="10"
				fill="#6b7280"
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
			stroke="#9ca3af"
			stroke-width="1"
		/>

		<!-- Series paths -->
		{#each geometry.paths as path, i (path.name)}
			<path
				d={path.d}
				fill="none"
				stroke={colorFor(i, path.color)}
				stroke-width="1.8"
				stroke-linecap="round"
				stroke-linejoin="round"
				data-testid="chart-path-{path.name}"
			/>
		{/each}

		<!-- Hover crosshair -->
		{#if hoverX !== null}
			<line
				x1={hoverX}
				x2={hoverX}
				y1={geometry.plotTop}
				y2={geometry.plotBottom}
				stroke="#9ca3af"
				stroke-width="1"
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
			<div class="text-[10px] text-gray-500 font-mono">bin {hoverBin}</div>
			{#each tooltipLines as line (line.name)}
				<div class="flex items-center gap-1.5">
					<span class="inline-block h-2 w-2 rounded-full" style="background: {line.color};"></span>
					<span class="text-xs font-mono">{line.name}: {formatTooltipValue(line.value)}</span>
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
		font-size: 11px;
		font-weight: 600;
		color: #374151;
		margin-bottom: 4px;
		font-family: ui-monospace, monospace;
	}
	.chart-tooltip {
		position: absolute;
		background: white;
		border: 1px solid #e5e7eb;
		border-radius: 6px;
		padding: 6px 8px;
		pointer-events: none;
		box-shadow: 0 2px 8px rgba(0, 0, 0, 0.08);
		transform: translateX(8px);
		z-index: 10;
		white-space: nowrap;
	}
</style>
