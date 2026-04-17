<script lang="ts">
	import { onMount, tick } from 'svelte';
	import { flowtime, type RunSummary, type GraphResponse } from '$lib/api/index.js';
	import DagMapView from '$lib/components/dag-map-view.svelte';
	import WorkbenchCard from '$lib/components/workbench-card.svelte';
	import { Button } from '$lib/components/ui/button/index.js';
	import { workbench } from '$lib/stores/workbench.svelte.js';
	import { extractNodeMetrics, findHighestUtilizationNode } from '$lib/utils/workbench-metrics.js';
	import { bindEvents } from 'dag-map';
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

	// State data for workbench cards
	let stateNodes = $state<Record<string, unknown>[]>([]);

	// Graph node kinds (for card display)
	let nodeKinds = $state<Map<string, string>>(new Map());

	// Splitter state
	let splitRatio = $state(65);
	let dragging = $state(false);
	let containerEl: HTMLDivElement | undefined = $state();

	// dag-map event cleanup
	let dagContainer: HTMLDivElement | undefined = $state();
	let cleanupEvents: (() => void) | undefined;

	// Bind dag-map click events whenever the SVG re-renders
	$effect(() => {
		if (dagContainer) {
			if (cleanupEvents) cleanupEvents();
			cleanupEvents = bindEvents(dagContainer, {
				onNodeClick: (nodeId: string) => {
					const kind = nodeKinds.get(nodeId);
					workbench.toggle(nodeId, kind);
				},
			});
		}
		return () => {
			if (cleanupEvents) {
				cleanupEvents();
				cleanupEvents = undefined;
			}
		};
	});

	// Derive selected set for dag-map rendering
	const selectedIds = $derived(workbench.selectedIds);

	onMount(async () => {
		// Restore split ratio from localStorage
		const stored = localStorage.getItem('ft.topology.split');
		if (stored) {
			const v = parseFloat(stored);
			if (isFinite(v) && v >= 20 && v <= 80) splitRatio = v;
		}

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
		stateNodes = [];
		currentBin = 0;
		binCount = 0;
		stopPlayback();
		workbench.clear();

		const [graphResult] = await Promise.all([
			flowtime.getGraph(runId),
			flowtime.getRun(runId)
		]);

		if (graphResult.success && graphResult.value) {
			graph = graphResult.value;
			// Build node kind map from graph
			const kinds = new Map<string, string>();
			for (const node of graphResult.value.nodes) {
				if (node.kind) kinds.set(node.id, node.kind);
			}
			nodeKinds = kinds;
		} else {
			error = graphResult.error ?? 'Failed to load graph';
			loading = false;
			return;
		}

		const indexResult = await flowtime.getRunIndex(runId);
		if (indexResult.success && indexResult.value) {
			binCount = indexResult.value.grid?.bins ?? 0;
		}

		loading = false;

		if (binCount > 0) {
			await loadBin(0);
			// Auto-pin highest utilization node
			const best = findHighestUtilizationNode(stateNodes);
			if (best) {
				workbench.pin(best.id, best.kind);
			}
		}
	}

	async function loadBin(bin: number) {
		if (!selectedRunId || bin < 0 || (binCount > 0 && bin >= binCount)) return;
		currentBin = bin;

		const stateResult = await flowtime.getState(selectedRunId, bin);
		if (stateResult.success && stateResult.value) {
			stateNodes = stateResult.value.nodes as Record<string, unknown>[];
			const newMetrics = buildMetrics(stateNodes);
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

	function getNodeState(nodeId: string): Record<string, unknown> | undefined {
		return stateNodes.find((n) => (n.id as string) === nodeId);
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

	// Splitter drag handlers
	function startDrag(e: MouseEvent) {
		dragging = true;
		e.preventDefault();
	}

	function onDrag(e: MouseEvent) {
		if (!dragging || !containerEl) return;
		const rect = containerEl.getBoundingClientRect();
		const pct = ((e.clientY - rect.top) / rect.height) * 100;
		splitRatio = Math.max(20, Math.min(80, pct));
	}

	function stopDrag() {
		if (dragging) {
			dragging = false;
			localStorage.setItem('ft.topology.split', String(splitRatio));
		}
	}
</script>

<svelte:window onmousemove={onDrag} onmouseup={stopDrag} />

<svelte:head>
	<title>Topology - FlowTime</title>
</svelte:head>

<div
	class="flex h-full flex-col"
	bind:this={containerEl}
>
	<!-- Toolbar -->
	<div class="flex items-center gap-2 border-b px-2 py-1">
		<span class="text-xs font-semibold text-muted-foreground">Topology</span>
		{#if runs.length > 0}
			<select
				class="bg-background border-input rounded border px-1.5 py-0.5 text-xs"
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
		<div class="flex items-center gap-2 p-3 text-xs text-destructive">
			<AlertCircleIcon class="size-3.5" />
			<span>{error}</span>
		</div>
	{:else if loading}
		<div class="flex-1 bg-muted animate-pulse"></div>
	{:else if graph}
		<!-- Split: DAG + Workbench -->
		<div class="flex-1 flex flex-col min-h-0">
			<!-- DAG area -->
			<div style="height: {splitRatio}%" class="overflow-auto" bind:this={dagContainer}>
				<DagMapView {graph} {metrics} selected={selectedIds} />
			</div>

			<!-- Splitter handle -->
			<div
				class="h-1 cursor-row-resize border-y border-border hover:bg-accent flex-shrink-0 {dragging ? 'bg-accent' : ''}"
				role="separator"
				aria-orientation="horizontal"
				onmousedown={startDrag}
			></div>

			<!-- Workbench panel -->
			<div style="height: {100 - splitRatio}%" class="overflow-auto bg-background">
				{#if workbench.pinned.length === 0}
					<div class="flex h-full items-center justify-center text-muted-foreground text-xs">
						Click a node to inspect
					</div>
				{:else}
					<div class="flex gap-2 p-2 flex-wrap items-start">
						{#each workbench.pinned as pin (pin.id)}
							{@const nodeState = getNodeState(pin.id)}
							<WorkbenchCard
								nodeId={pin.id}
								kind={pin.kind}
								metrics={nodeState ? extractNodeMetrics(nodeState) : []}
								{currentBin}
								onClose={() => workbench.unpin(pin.id)}
							/>
						{/each}
					</div>
				{/if}
			</div>
		</div>

		<!-- Timeline bar -->
		{#if binCount > 0}
			<div class="flex items-center gap-2 px-2 py-1 border-t">
				<div class="flex items-center gap-0.5">
					<Button variant="ghost" size="icon" class="size-6" onclick={prevBin} disabled={currentBin === 0}>
						<SkipBackIcon class="size-3" />
					</Button>
					<Button variant="ghost" size="icon" class="size-6" onclick={togglePlayback}>
						{#if playing}
							<PauseIcon class="size-3" />
						{:else}
							<PlayIcon class="size-3" />
						{/if}
					</Button>
					<Button variant="ghost" size="icon" class="size-6" onclick={nextBin} disabled={currentBin >= binCount - 1}>
						<SkipForwardIcon class="size-3" />
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
				<span class="text-muted-foreground w-16 text-right text-[10px] font-mono tabular-nums">
					{currentBin + 1}/{binCount}
				</span>
			</div>
		{/if}
	{:else if runs.length === 0}
		<div class="flex flex-1 items-center justify-center">
			<p class="text-muted-foreground text-xs">No runs available. Run a model first.</p>
		</div>
	{/if}
</div>
