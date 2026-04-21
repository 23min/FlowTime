<script lang="ts">
	import {
		convergenceChartGeometry,
		type ConvergenceTracePoint,
	} from './convergence-chart-geometry.js';

	interface Props {
		trace: ConvergenceTracePoint[];
		/** Optional horizontal reference line (e.g. goal-seek target). */
		target?: number;
		/** Controls line + final-point color. Teal when converged, amber otherwise. */
		converged?: boolean;
		/** Caller-supplied y-axis label (e.g. "metric mean", "queue depth"). */
		yLabel?: string;
		width?: number;
		height?: number;
	}

	let {
		trace,
		target,
		converged = false,
		yLabel = 'metric mean',
		width = 480,
		height = 200,
	}: Props = $props();

	const padding = { top: 12, right: 12, bottom: 28, left: 44 };
	const geom = $derived(
		convergenceChartGeometry({ trace, width, height, padding, target }),
	);

	const lineColor = $derived(
		converged ? 'var(--ft-viz-teal)' : 'var(--ft-viz-amber)',
	);
</script>

{#if geom.points.length === 0}
	<div class="text-xs text-muted-foreground italic p-2" data-testid="convergence-chart-empty">
		No convergence trace to plot.
	</div>
{:else}
	<svg
		{width}
		{height}
		viewBox="0 0 {width} {height}"
		role="img"
		aria-label="convergence chart"
		data-testid="convergence-chart"
	>
		<!-- Y axis line -->
		<line
			x1={geom.plotLeft}
			x2={geom.plotLeft}
			y1={geom.plotTop}
			y2={geom.plotBottom}
			stroke="var(--border)"
			stroke-width="0.5"
		/>
		<!-- X axis line -->
		<line
			x1={geom.plotLeft}
			x2={geom.plotRight}
			y1={geom.plotBottom}
			y2={geom.plotBottom}
			stroke="var(--border)"
			stroke-width="0.5"
		/>

		<!-- Y ticks + gridlines -->
		{#each geom.yTicks as tick, i (i)}
			<line
				x1={geom.plotLeft}
				x2={geom.plotRight}
				y1={tick.y}
				y2={tick.y}
				stroke="var(--border)"
				stroke-width="0.25"
				stroke-dasharray="2 2"
				opacity="0.6"
			/>
			<text
				x={geom.plotLeft - 4}
				y={tick.y + 3}
				text-anchor="end"
				font-size="9"
				font-family="ui-monospace, monospace"
				fill="var(--muted-foreground)"
			>{tick.label}</text>
		{/each}

		<!-- X ticks -->
		{#each geom.xTicks as tick, i (i)}
			<text
				x={tick.x}
				y={geom.plotBottom + 12}
				text-anchor="middle"
				font-size="9"
				font-family="ui-monospace, monospace"
				fill="var(--muted-foreground)"
			>{tick.label}</text>
		{/each}

		<!-- Y axis label -->
		<text
			x={12}
			y={(geom.plotTop + geom.plotBottom) / 2}
			text-anchor="middle"
			font-size="9"
			font-family="ui-sans-serif, system-ui"
			fill="var(--muted-foreground)"
			transform="rotate(-90 12 {(geom.plotTop + geom.plotBottom) / 2})"
		>{yLabel}</text>

		<!-- X axis label -->
		<text
			x={(geom.plotLeft + geom.plotRight) / 2}
			y={height - 6}
			text-anchor="middle"
			font-size="9"
			font-family="ui-sans-serif, system-ui"
			fill="var(--muted-foreground)"
		>iteration</text>

		<!-- Target reference line -->
		{#if geom.targetY !== null}
			<line
				x1={geom.plotLeft}
				x2={geom.plotRight}
				y1={geom.targetY}
				y2={geom.targetY}
				stroke="var(--muted-foreground)"
				stroke-width="1"
				stroke-dasharray="4 3"
				opacity="0.8"
				data-testid="convergence-chart-target-line"
			/>
		{/if}

		<!-- Convergence path -->
		{#if geom.path}
			<path
				d={geom.path}
				fill="none"
				stroke={lineColor}
				stroke-width="1.5"
				stroke-linecap="round"
				stroke-linejoin="round"
			/>
		{/if}

		<!-- Points -->
		{#each geom.points as pt, i (i)}
			<circle
				cx={pt.x}
				cy={pt.y}
				r={pt.isFinal ? 4 : 2.5}
				fill={pt.isFinal ? lineColor : 'var(--background)'}
				stroke={lineColor}
				stroke-width={pt.isFinal ? 1.5 : 1}
				data-testid={pt.isFinal ? 'convergence-chart-final-point' : 'convergence-chart-point'}
			/>
		{/each}
	</svg>
{/if}
