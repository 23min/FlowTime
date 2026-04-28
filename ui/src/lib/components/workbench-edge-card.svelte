<script lang="ts">
	import XIcon from '@lucide/svelte/icons/x';
	import ArrowRightIcon from '@lucide/svelte/icons/arrow-right';
	import { severityChromeToken, severityRowBackground } from '$lib/utils/validation-helpers.js';

	interface MetricEntry {
		label: string;
		value: string;
	}

	interface Props {
		from: string;
		to: string;
		metrics?: MetricEntry[];
		/** When set, a small filled chrome-token dot renders in the title row,
		 *  signalling the pinned edge has at least one validation warning.
		 *  Mirrors the node-card affordance for visual symmetry. Hidden when
		 *  undefined. (m-E21-07 workbench-card warning dot follow-up.) */
		warningSeverity?: 'error' | 'warning' | 'info';
		/** When true, the card border renders in the turquoise selection
		 *  colour, mirroring the node-card `selected` convention and the
		 *  validation-panel matching-row border. Hidden when false. */
		selected?: boolean;
		/** Click handler for the card body — mirrors workbench-card's `onSelect`.
		 *  Clicking the close button stops propagation. */
		onSelect?: () => void;
		onClose?: () => void;
	}

	let {
		from,
		to,
		metrics = [],
		warningSeverity,
		selected = false,
		onSelect,
		onClose,
	}: Props = $props();

	function onCardKeydown(e: KeyboardEvent) {
		if (!onSelect) return;
		if (e.key === 'Enter' || e.key === ' ') {
			e.preventDefault();
			onSelect();
		}
	}

	const warningToken = $derived(
		warningSeverity ? severityChromeToken(warningSeverity) : null,
	);
	const warningBg = $derived(
		warningSeverity ? severityRowBackground(warningSeverity) : null,
	);
</script>

<!-- svelte-ignore a11y_no_noninteractive_tabindex -->
<div
	class="border rounded p-1.5 text-xs min-w-[160px] max-w-[200px] flex flex-col gap-1"
	class:bg-card={!warningBg}
	class:cursor-pointer={!!onSelect}
	style={[
		warningBg ? `background: var(${warningBg})` : null,
		selected ? 'border-color: var(--ft-highlight)' : null,
	].filter(Boolean).join('; ') || undefined}
	role={onSelect ? 'button' : undefined}
	tabindex={onSelect ? 0 : undefined}
	onclick={onSelect}
	onkeydown={onCardKeydown}
>
	<!-- Header -->
	<div class="flex items-center justify-between gap-1">
		<div class="flex items-center gap-0.5 min-w-0 text-[10px] font-mono">
			{#if warningToken}
				<!-- Warning dot — sibling of the from→to title text, inline at the
				     start of the title row. Mirrors workbench-card's node-side dot
				     for visual symmetry. -->
				<span
					class="inline-block size-1.5 rounded-full shrink-0 mr-1"
					style="background: var({warningToken})"
					data-warning-dot="edge"
					data-warning-severity={warningSeverity}
					aria-hidden="true"
				></span>
			{/if}
			<span class="truncate">{from}</span>
			<ArrowRightIcon class="size-2.5 shrink-0 text-muted-foreground" />
			<span class="truncate">{to}</span>
		</div>
		{#if onClose}
			<button
				onclick={(e) => { e.stopPropagation(); onClose?.(); }}
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
