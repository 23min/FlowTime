<script lang="ts">
	import Sparkline from './sparkline.svelte';
	import XIcon from '@lucide/svelte/icons/x';

	interface MetricEntry {
		label: string;
		value: string;
	}

	interface Props {
		nodeId: string;
		kind?: string;
		metrics?: MetricEntry[];
		sparklineValues?: number[];
		sparklineLabel?: string;
		currentBin?: number;
		/** When true, the title text renders in the turquoise selection color (matches
		 *  the heatmap cell-selection overlay), tying the card to the selected tile. */
		selected?: boolean;
		onClose?: () => void;
	}

	let {
		nodeId,
		kind,
		metrics = [],
		sparklineValues = [],
		sparklineLabel = 'utilization',
		currentBin,
		selected = false,
		onClose,
	}: Props = $props();
</script>

<div class="bg-card border rounded p-1.5 text-xs min-w-[160px] max-w-[200px] flex flex-col gap-1">
	<!-- Header -->
	<div class="flex items-center justify-between gap-1">
		<div class="flex items-center gap-1 min-w-0">
			<span
				class="font-semibold truncate"
				style={selected ? 'color: var(--ft-highlight)' : undefined}
				data-selected={selected ? 'true' : undefined}
			>{nodeId}</span>
			{#if kind}
				<span class="text-muted-foreground text-[10px] shrink-0">{kind}</span>
			{/if}
		</div>
		{#if onClose}
			<button
				onclick={onClose}
				class="text-muted-foreground hover:text-foreground shrink-0 p-0.5 -m-0.5 rounded"
				aria-label="Unpin {nodeId}"
			>
				<XIcon class="size-3" />
			</button>
		{/if}
	</div>

	<!-- Metrics -->
	{#if metrics.length > 0}
		<div class="grid grid-cols-2 gap-x-2 gap-y-0.5 text-[10px]">
			{#each metrics as m}
				<span class="text-muted-foreground truncate">{m.label}</span>
				<span class="text-right font-mono tabular-nums">{m.value}</span>
			{/each}
		</div>
	{/if}

	<!-- Sparkline -->
	{#if sparklineValues.length > 1}
		<div class="relative mt-0.5">
			<div class="text-[9px] text-muted-foreground mb-0.5">{sparklineLabel}</div>
			<div class="relative">
				<Sparkline
					values={sparklineValues}
					width={160}
					height={24}
					stroke="var(--ft-viz-teal)"
					strokeWidth={1}
				/>
				{#if currentBin !== undefined && currentBin >= 0 && currentBin < sparklineValues.length}
					{@const x = sparklineValues.length > 1 ? (currentBin / (sparklineValues.length - 1)) * 160 : 0}
					<svg class="absolute inset-0" width="160" height="24" viewBox="0 0 160 24">
						<line x1={x} y1="0" x2={x} y2="24" stroke="var(--ft-viz-amber)" stroke-width="1" opacity="0.85" />
					</svg>
				{/if}
			</div>
		</div>
	{/if}
</div>
