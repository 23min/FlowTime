<script lang="ts">
	import XIcon from '@lucide/svelte/icons/x';
	import ArrowRightIcon from '@lucide/svelte/icons/arrow-right';

	interface MetricEntry {
		label: string;
		value: string;
	}

	interface Props {
		from: string;
		to: string;
		metrics?: MetricEntry[];
		onClose?: () => void;
	}

	let { from, to, metrics = [], onClose }: Props = $props();
</script>

<div class="bg-card border rounded p-1.5 text-xs min-w-[160px] max-w-[200px] flex flex-col gap-1">
	<!-- Header -->
	<div class="flex items-center justify-between gap-1">
		<div class="flex items-center gap-0.5 min-w-0 text-[10px] font-mono">
			<span class="truncate">{from}</span>
			<ArrowRightIcon class="size-2.5 shrink-0 text-muted-foreground" />
			<span class="truncate">{to}</span>
		</div>
		{#if onClose}
			<button
				onclick={onClose}
				class="text-muted-foreground hover:text-foreground shrink-0 p-0.5 -m-0.5 rounded"
				aria-label="Unpin {from} to {to}"
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
	{:else}
		<div class="text-[10px] text-muted-foreground italic">edge</div>
	{/if}
</div>
