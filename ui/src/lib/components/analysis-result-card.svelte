<script lang="ts">
	import type { Snippet } from 'svelte';

	interface Props {
		/** Title shown in the header. */
		title: string;
		/** Optional status badge text (e.g. "converged", "did not converge", "target not reachable"). */
		badge?: string;
		/** Badge tone. teal = success; amber = warning; muted = neutral. */
		badgeTone?: 'teal' | 'amber' | 'muted';
		/**
		 * Meta key/value pairs rendered in a compact grid below the primary value
		 * (e.g. iterations / tolerance / target / residual).
		 */
		meta?: { label: string; value: string }[];
		/** Optional custom header region. Takes precedence over `title`/`badge` props. */
		header?: Snippet;
		/** Primary-value region (large monospace). Required. */
		primaryValue: Snippet;
		/** Optional extra region rendered below the meta grid (e.g. warning block). */
		footer?: Snippet;
	}

	let {
		title,
		badge,
		badgeTone = 'teal',
		meta = [],
		header,
		primaryValue,
		footer,
	}: Props = $props();

	const badgeClasses = $derived.by(() => {
		if (badgeTone === 'amber') {
			return 'bg-amber-500/15 text-amber-600 dark:text-amber-400 border-amber-500/30';
		}
		if (badgeTone === 'muted') {
			return 'bg-muted text-muted-foreground border-border';
		}
		return 'bg-[color:var(--ft-viz-teal)]/15 text-[color:var(--ft-viz-teal)] border-[color:var(--ft-viz-teal)]/40';
	});
</script>

<div
	class="flex flex-col gap-2 border rounded p-2 bg-card"
	data-testid="analysis-result-card"
>
	<!-- Header -->
	<div class="flex items-center justify-between gap-2 border-b pb-1">
		{#if header}
			{@render header()}
		{:else}
			<span class="text-xs font-semibold">{title}</span>
			{#if badge}
				<span
					class="rounded-full border px-2 py-0.5 text-[10px] font-medium {badgeClasses}"
					data-testid="analysis-result-card-badge"
				>{badge}</span>
			{/if}
		{/if}
	</div>

	<!-- Primary value -->
	<div class="font-mono text-lg tabular-nums" data-testid="analysis-result-card-primary">
		{@render primaryValue()}
	</div>

	<!-- Meta grid -->
	{#if meta.length > 0}
		<div
			class="grid grid-cols-[max-content_1fr] gap-x-3 gap-y-0.5 text-[11px]"
			data-testid="analysis-result-card-meta"
		>
			{#each meta as m (m.label)}
				<span class="text-muted-foreground">{m.label}</span>
				<span class="font-mono tabular-nums">{m.value}</span>
			{/each}
		</div>
	{/if}

	{#if footer}
		{@render footer()}
	{/if}
</div>
