<script lang="ts">
	import { onMount } from 'svelte';
	import { flowtime, type RunSummary, type GraphResponse } from '$lib/api/index.js';
	import DagMapView from '$lib/components/dag-map-view.svelte';
	import * as Card from '$lib/components/ui/card/index.js';
	import { Button } from '$lib/components/ui/button/index.js';
	import AlertCircleIcon from '@lucide/svelte/icons/alert-circle';
	import PlayIcon from '@lucide/svelte/icons/play';
	import PauseIcon from '@lucide/svelte/icons/pause';
	import SkipBackIcon from '@lucide/svelte/icons/skip-back';
	import SkipForwardIcon from '@lucide/svelte/icons/skip-forward';

	interface NodeMetric {
		value: number;
		label?: string;
	}

	let runs = $state<RunSummary[]>([]);
	let selectedRunId = $state<string | undefined>();
	let graph = $state<GraphResponse | undefined>();
	let loading = $state(false);
	let error = $state<string | undefined>();

	// Timeline state
	let binCount = $state(0);
	let currentBin = $state(0);
	let metrics = $state<Map<string, NodeMetric> | undefined>();
	let playing = $state(false);
	let playInterval: ReturnType<typeof setInterval> | undefined;

	onMount(async () => {
		const result = await flowtime.listRuns(1, 50);
		if (result.success && result.value) {
			runs = result.value.items;
			if (runs.length > 0) {
				selectRun(runs[0].runId);
			}
		}
		return () => { if (playInterval) clearInterval(playInterval); };
	});

	async function selectRun(runId: string) {
		selectedRunId = runId;
		loading = true;
		error = undefined;
		graph = undefined;
		metrics = undefined;
		currentBin = 0;
		binCount = 0;
		stopPlayback();

		const [graphResult, runResult] = await Promise.all([
			flowtime.getGraph(runId),
			flowtime.getRun(runId)
		]);

		if (graphResult.success && graphResult.value) {
			graph = graphResult.value;
		} else {
			error = graphResult.error ?? 'Failed to load graph';
			loading = false;
			return;
		}

		// Get bin count from run index
		const indexResult = await flowtime.getRunIndex(runId);
		if (indexResult.success && indexResult.value) {
			binCount = indexResult.value.grid?.bins ?? 0;
		}

		loading = false;

		if (binCount > 0) {
			loadBin(0);
		}
	}

	async function loadBin(bin: number) {
		if (!selectedRunId || bin < 0 || (binCount > 0 && bin >= binCount)) return;
		currentBin = bin;

		const stateResult = await flowtime.getState(selectedRunId, bin);
		if (stateResult.success && stateResult.value) {
			const newMetrics = buildMetrics(stateResult.value.nodes);
			console.log(`[topology] bin ${bin}: ${newMetrics.size} metrics`,
				[...newMetrics.entries()].slice(0, 3).map(([k, v]) => `${k}=${v.label}`).join(', '));
			metrics = newMetrics;
		}
	}

	function buildMetrics(nodes: Record<string, unknown>[]): Map<string, NodeMetric> {
		const m = new Map<string, NodeMetric>();
		for (const node of nodes) {
			const id = node.id as string;
			if (!id) continue;

			const derived = node.derived as Record<string, unknown> | undefined;
			if (!derived) continue;

			const util = derived.utilization as number | undefined | null;
			const throughput = derived.throughputRatio as number | undefined | null;

			if (util !== undefined && util !== null && isFinite(util)) {
				const pct = Math.round(util * 100);
				m.set(id, { value: util, label: `${pct}%` });
			} else if (throughput !== undefined && throughput !== null && isFinite(throughput)) {
				m.set(id, { value: 1 - throughput, label: `${Math.round(throughput * 100)}%` });
			}
		}
		return m;
	}

	function prevBin() { if (currentBin > 0) loadBin(currentBin - 1); }
	function nextBin() { if (currentBin < binCount - 1) loadBin(currentBin + 1); }

	function togglePlayback() {
		if (playing) {
			stopPlayback();
		} else {
			playing = true;
			playInterval = setInterval(() => {
				if (currentBin >= binCount - 1) {
					currentBin = 0;
				}
				loadBin(currentBin + 1);
			}, 500);
		}
	}

	function stopPlayback() {
		playing = false;
		if (playInterval) { clearInterval(playInterval); playInterval = undefined; }
	}
</script>

<svelte:head>
	<title>Topology - FlowTime</title>
</svelte:head>

<div class="space-y-4">
	<div class="flex items-center gap-4">
		<h1 class="text-2xl font-semibold">Topology</h1>
		{#if runs.length > 0}
			<select
				class="bg-background border-input rounded-md border px-3 py-1.5 text-sm"
				value={selectedRunId}
				onchange={(e) => selectRun((e.target as HTMLSelectElement).value)}
			>
				{#each runs as run}
					<option value={run.runId}>{run.templateTitle ?? run.runId}</option>
				{/each}
			</select>
		{/if}
	</div>

	{#if error}
		<Card.Root class="border-destructive">
			<Card.Content class="flex items-center gap-3 pt-6">
				<AlertCircleIcon class="text-destructive size-5" />
				<p class="text-sm">{error}</p>
			</Card.Content>
		</Card.Root>
	{:else if loading}
		<div class="bg-muted h-96 animate-pulse rounded-lg"></div>
	{:else if graph}
		<div class="bg-card rounded-lg border">
			<DagMapView {graph} {metrics} />
		</div>

		{#if binCount > 0}
			<div class="bg-card flex items-center gap-3 rounded-lg border px-4 py-3">
				<div class="flex items-center gap-1">
					<Button variant="ghost" size="icon" onclick={prevBin} disabled={currentBin === 0}>
						<SkipBackIcon class="size-4" />
					</Button>
					<Button variant="ghost" size="icon" onclick={togglePlayback}>
						{#if playing}
							<PauseIcon class="size-4" />
						{:else}
							<PlayIcon class="size-4" />
						{/if}
					</Button>
					<Button variant="ghost" size="icon" onclick={nextBin} disabled={currentBin >= binCount - 1}>
						<SkipForwardIcon class="size-4" />
					</Button>
				</div>
				<input
					type="range"
					min="0"
					max={binCount - 1}
					value={currentBin}
					oninput={(e) => { stopPlayback(); loadBin(parseInt((e.target as HTMLInputElement).value)); }}
					class="flex-1"
				/>
				<span class="text-muted-foreground w-20 text-right text-sm font-mono">
					{currentBin + 1} / {binCount}
				</span>
			</div>
		{/if}
	{:else if runs.length === 0}
		<Card.Root>
			<Card.Content class="flex flex-col items-center gap-2 py-12">
				<p class="text-muted-foreground">No runs available. Run a model first.</p>
			</Card.Content>
		</Card.Root>
	{/if}
</div>
