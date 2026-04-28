<script lang="ts">
	import Sparkline from './sparkline.svelte';
	import XIcon from '@lucide/svelte/icons/x';
	import { severityChromeToken, severityRowBackground } from '$lib/utils/validation-helpers.js';

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
		/** When set, a small filled chrome-token dot renders in the title row,
		 *  signalling the pinned node has at least one validation warning. The
		 *  dot composes with `selected` — both can render at once. Hidden when
		 *  undefined. (m-E21-07 workbench-card warning dot follow-up.) */
		warningSeverity?: 'error' | 'warning' | 'info';
		/** Click handler for the card body — fires when the user clicks anywhere
		 *  on the card except the close button (which stops propagation). The
		 *  parent wires this to the same selection convention as topology-node
		 *  and validation-row clicks (`viewState.setSelectedCell(...)`), so
		 *  every selection surface stays symmetric. */
		onSelect?: () => void;
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
		warningSeverity,
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

	// Severity → chrome token. `severityChromeToken` returns `null` for unknown
	// literals; the prop type narrows to the known three-literal union, so the
	// only `null` path here is "prop undefined" (handled by the `{#if}` guard).
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
		<div class="flex items-center gap-1 min-w-0">
			{#if warningToken}
				<!-- Warning dot — placed before the title text so it sits inline with
				     the (optional) turquoise `selected` text colour without overlap.
				     Same dot density as the validation-panel row chip from `382add5`. -->
				<span
					class="inline-block size-1.5 rounded-full shrink-0"
					style="background: var({warningToken})"
					data-warning-dot="node"
					data-warning-severity={warningSeverity}
					aria-hidden="true"
				></span>
			{/if}
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
				onclick={(e) => { e.stopPropagation(); onClose?.(); }}
				class="text-muted-foreground hover:text-foreground shrink-0 p-0.5 -m-0.5 rounded"
				aria-label="Unpin {nodeId}"
			>
				<XIcon class="size-3" />
			</button>
		{/if}
	</div>

	<!-- Sparkline (above properties for visual primacy) -->
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

	<!-- Metrics -->
	{#if metrics.length > 0}
		<div class="grid grid-cols-2 gap-x-2 gap-y-0.5 text-[10px]">
			{#each metrics as m}
				<span class="text-muted-foreground truncate">{m.label}</span>
				<span class="text-right font-mono tabular-nums">{m.value}</span>
			{/each}
		</div>
	{/if}
</div>
