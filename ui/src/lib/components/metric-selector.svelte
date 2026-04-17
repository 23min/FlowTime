<script lang="ts">
	import { METRIC_DEFS, type MetricDef } from '$lib/utils/metric-defs.js';

	interface Props {
		selected: MetricDef;
		onSelect: (def: MetricDef) => void;
	}

	let { selected, onSelect }: Props = $props();
</script>

<div class="flex items-center gap-1 flex-wrap">
	{#each METRIC_DEFS as def (def.id)}
		<button
			class="rounded px-1.5 py-0.5 text-[10px] transition-colors {selected.id === def.id
				? 'bg-foreground text-background font-medium'
				: 'bg-muted text-muted-foreground hover:bg-accent hover:text-accent-foreground'}"
			onclick={() => onSelect(def)}
		>
			{def.label}
		</button>
	{/each}
</div>
