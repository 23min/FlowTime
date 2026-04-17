<script lang="ts">
	import type { SensitivityPoint } from '$lib/utils/analysis-helpers.js';
	import { sortByAbsGradient, maxAbsGradient } from '$lib/utils/analysis-helpers.js';
	import {
		defaultLayout,
		barCenter,
		barGeometry,
		fmtBarValue,
	} from './sensitivity-bar-geometry.js';

	interface Props {
		points: SensitivityPoint[];
		width?: number;
		rowHeight?: number;
	}

	let { points, width = 420, rowHeight = 18 }: Props = $props();

	const sorted = $derived(sortByAbsGradient(points));
	const max = $derived(maxAbsGradient(points));
	const layout = $derived(defaultLayout(width));
	const center = $derived(barCenter(layout));
</script>

{#if sorted.length === 0}
	<div class="text-xs text-muted-foreground italic p-2">No sensitivity data.</div>
{:else}
	<svg {width} height={sorted.length * rowHeight + 4} viewBox="0 0 {width} {sorted.length * rowHeight + 4}" role="img" aria-label="sensitivity bar chart">
		<!-- Center rule -->
		<line
			x1={center}
			x2={center}
			y1="0"
			y2={sorted.length * rowHeight + 4}
			stroke="var(--border)"
			stroke-width="0.5"
		/>
		{#each sorted as p, i (p.paramId)}
			{@const geom = barGeometry(p.gradient, max, layout)}
			{@const y = i * rowHeight + 2}
			<!-- Param label -->
			<text
				x={layout.labelWidth - 6}
				y={y + rowHeight / 2 + 3}
				text-anchor="end"
				font-size="10"
				font-family="ui-monospace, monospace"
				fill="var(--foreground)"
			>{p.paramId}</text>
			<!-- Bar -->
			<rect
				x={geom.x}
				y={y + 3}
				width={geom.w}
				height={rowHeight - 6}
				fill={geom.color}
				opacity="0.8"
			/>
			<!-- Gradient value -->
			<text
				x={width - 4}
				y={y + rowHeight / 2 + 3}
				text-anchor="end"
				font-size="9"
				font-family="ui-monospace, monospace"
				fill="var(--muted-foreground)"
			>{fmtBarValue(p.gradient)}</text>
		{/each}
	</svg>
{/if}
