<script lang="ts">
	// Minimal inline SVG sparkline. No dependencies.
	// Path computation is in sparkline-path.ts (pure, unit-tested).

	import { computeSparklinePath } from './sparkline-path.js';

	interface Props {
		values: number[];
		width?: number;
		height?: number;
		stroke?: string;
		strokeWidth?: number;
	}

	let {
		values,
		width = 120,
		height = 30,
		stroke = 'currentColor',
		strokeWidth = 1.5,
	}: Props = $props();

	const pathData = $derived(computeSparklinePath(values, { width, height, strokeWidth }));
</script>

<svg {width} {height} viewBox="0 0 {width} {height}" class="overflow-visible">
	<path
		d={pathData}
		fill="none"
		{stroke}
		stroke-width={strokeWidth}
		stroke-linecap="round"
		stroke-linejoin="round"
	/>
</svg>
