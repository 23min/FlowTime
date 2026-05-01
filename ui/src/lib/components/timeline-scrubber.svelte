<script lang="ts">
	import { Button } from '$lib/components/ui/button/index.js';
	import PlayIcon from '@lucide/svelte/icons/play';
	import PauseIcon from '@lucide/svelte/icons/pause';
	import SkipBackIcon from '@lucide/svelte/icons/skip-back';
	import SkipForwardIcon from '@lucide/svelte/icons/skip-forward';
	import { computeTicks, computePointerPct } from './timeline-scrubber-ticks.js';

	interface Props {
		binCount: number;
		currentBin: number;
		onBinChange: (bin: number) => void;
		/** Playback controls render only when play callback is provided. */
		playing?: boolean;
		onTogglePlay?: () => void;
		onPrev?: () => void;
		onNext?: () => void;
		/** Hide the pointer (e.g., when in Mean/aggregate mode). */
		pointerHidden?: boolean;
		/** Optional testid for the root track, for Playwright selectors. */
		trackTestId?: string;
	}

	let {
		binCount,
		currentBin,
		playing = false,
		onBinChange,
		onTogglePlay,
		onPrev,
		onNext,
		pointerHidden = false,
		trackTestId,
	}: Props = $props();

	const showControls = $derived(onTogglePlay !== undefined && onPrev !== undefined && onNext !== undefined);

	let dragging = $state(false);

	const pointerPct = $derived(computePointerPct(binCount, currentBin));
	const ticks = $derived(computeTicks(binCount));

	function onInput(e: Event) {
		const val = parseInt((e.target as HTMLInputElement).value);
		onBinChange(val);
	}

	function onPointerDown() { dragging = true; }
	function onPointerUp() { dragging = false; }
</script>

<div class="flex flex-col gap-0.5">
	<!-- Track with ticks, pointer, and invisible range input -->
	<div class="timeline-track" class:is-disabled={binCount <= 1} data-testid={trackTestId}>
		<svg class="timeline-svg" viewBox="0 0 100 12" preserveAspectRatio="none">
			<rect class="timeline-track-bg" x="0" y="4" width="100" height="4" />
			{#each ticks.minor as pct}
				<line class="timeline-tick timeline-tick--minor" x1={pct} x2={pct} y1="3" y2="9" />
			{/each}
			{#each ticks.major as tick}
				<line class="timeline-tick" x1={tick.pct} x2={tick.pct} y1="1" y2="11" />
			{/each}
		</svg>
		{#if !pointerHidden}
			<div
				class="timeline-pointer"
				class:is-dragging={dragging}
				style="left: {pointerPct}%"
			></div>
		{/if}
		<input
			class="timeline-range"
			type="range"
			min="0"
			max={binCount - 1}
			step="1"
			value={currentBin}
			disabled={binCount <= 1}
			oninput={onInput}
			onpointerdown={onPointerDown}
			onpointerup={onPointerUp}
			onpointercancel={onPointerUp}
			onpointerleave={onPointerUp}
		/>
	</div>

	<!-- Labels -->
	<div class="timeline-labels">
		{#each ticks.major as tick (tick.bin)}
			<span class="timeline-label" style="left: {tick.pct}%">{tick.label}</span>
		{/each}
	</div>

	<!-- Controls row -->
	{#if showControls}
		<div class="flex items-center gap-1">
			<div class="flex items-center gap-0.5">
				<Button variant="ghost" size="icon" class="size-5" onclick={onPrev} disabled={currentBin === 0}>
					<SkipBackIcon class="size-2.5" />
				</Button>
				<Button variant="ghost" size="icon" class="size-5" onclick={onTogglePlay}>
					{#if playing}
						<PauseIcon class="size-2.5" />
					{:else}
						<PlayIcon class="size-2.5" />
					{/if}
				</Button>
				<Button variant="ghost" size="icon" class="size-5" onclick={onNext} disabled={currentBin >= binCount - 1}>
					<SkipForwardIcon class="size-2.5" />
				</Button>
			</div>
			<span class="text-muted-foreground text-[10px] font-mono tabular-nums ml-1">
				Bin {currentBin} / {binCount - 1}
			</span>
		</div>
	{/if}
</div>

<style>
	.timeline-track {
		position: relative;
		height: 24px;
		border: 1px solid var(--border);
		background: var(--muted);
		transition: border-color 0.15s ease;
	}

	.timeline-track.is-disabled {
		opacity: 0.4;
	}

	.timeline-track:focus-within {
		border-color: var(--ring);
	}

	.timeline-svg {
		width: 100%;
		height: 12px;
		display: block;
	}

	.timeline-track-bg {
		fill: transparent;
	}

	.timeline-tick {
		stroke: var(--muted-foreground);
		stroke-width: 0.5;
		opacity: 0.5;
	}

	.timeline-tick--minor {
		stroke-width: 0.3;
		opacity: 0.3;
	}

	.timeline-pointer {
		position: absolute;
		top: 0;
		bottom: 0;
		width: 2px;
		background: var(--ft-viz-teal);
		pointer-events: none;
		transform: translateX(-50%);
		transition: left 0.08s linear;
	}

	.timeline-pointer.is-dragging {
		transition: none;
	}

	/* Invisible range input covering the full track for drag interaction */
	.timeline-range {
		position: absolute;
		inset: 0;
		width: 100%;
		height: 100%;
		margin: 0;
		background: transparent;
		-webkit-appearance: none;
		appearance: none;
		cursor: pointer;
	}

	.timeline-range:focus-visible {
		outline: none;
	}

	.timeline-range::-webkit-slider-runnable-track {
		height: 100%;
		background: transparent;
		border: none;
	}

	.timeline-range::-webkit-slider-thumb {
		-webkit-appearance: none;
		width: 1px;
		height: 1px;
		background: transparent;
	}

	.timeline-range::-moz-range-track {
		height: 100%;
		background: transparent;
		border: none;
	}

	.timeline-range::-moz-range-thumb {
		width: 1px;
		height: 1px;
		background: transparent;
		border: none;
	}

	.timeline-labels {
		position: relative;
		height: 12px;
	}

	.timeline-label {
		position: absolute;
		transform: translateX(-50%);
		font-size: 9px;
		font-family: ui-monospace, monospace;
		color: var(--muted-foreground);
		white-space: nowrap;
	}
</style>
