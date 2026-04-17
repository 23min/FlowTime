<script lang="ts">
	import { onMount } from 'svelte';
	import { flowtime, type RunSummary, type GraphResponse } from '$lib/api/index.js';
	import DagMapView from '$lib/components/dag-map-view.svelte';
	import WorkbenchCard from '$lib/components/workbench-card.svelte';
	import WorkbenchEdgeCard from '$lib/components/workbench-edge-card.svelte';
	import MetricSelector from '$lib/components/metric-selector.svelte';
	import TimelineScrubber from '$lib/components/timeline-scrubber.svelte';
	import { workbench } from '$lib/stores/workbench.svelte.js';
	import {
		extractNodeMetrics,
		extractEdgeMetrics,
		findHighestUtilizationNode,
	} from '$lib/utils/workbench-metrics.js';
	import {
		buildMetricMapForDefFiltered,
		buildSparklineSeries,
		discoverClasses,
		type MetricDef,
	} from '$lib/utils/metric-defs.js';
	import { bindEvents } from 'dag-map';
	import AlertCircleIcon from '@lucide/svelte/icons/alert-circle';

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
	let playing = $state(false);
	let playInterval: ReturnType<typeof setInterval> | undefined;

	// Snapshot state (for heatmap + node/edge cards at current bin)
	let stateNodes = $state<Record<string, unknown>[]>([]);
	let stateEdges = $state<Record<string, unknown>[]>([]);

	// Window state (for sparklines — loaded once per run)
	let windowNodes = $state<Record<string, unknown>[]>([]);

	// Graph node kinds (for card display)
	let nodeKinds = $state<Map<string, string>>(new Map());

	// Class filter
	let availableClasses = $state<string[]>([]);
	let activeClasses = $state<Set<string>>(new Set());

	// Splitter state
	let splitRatio = $state(65);
	let dragging = $state(false);
	let containerEl: HTMLDivElement | undefined = $state();

	// dag-map event cleanup
	let dagContainer: HTMLDivElement | undefined = $state();
	let cleanupEvents: (() => void) | undefined;

	// Heatmap metrics, rebuilt when selected metric, snapshot, or class filter changes
	const currentMetrics = $derived.by<Map<string, NodeMetric> | undefined>(() => {
		if (stateNodes.length === 0) return undefined;
		return buildMetricMapForDefFiltered(stateNodes, workbench.selectedMetric, activeClasses);
	});

	// Sparkline values per node, for selected metric + class filter
	const sparklineMap = $derived.by<Map<string, number[]>>(() => {
		if (windowNodes.length === 0) return new Map();
		return buildSparklineSeries(windowNodes, workbench.selectedMetric, activeClasses);
	});

	// Bind dag-map click events whenever the SVG re-renders
	$effect(() => {
		if (dagContainer) {
			if (cleanupEvents) cleanupEvents();
			cleanupEvents = bindEvents(dagContainer, {
				onNodeClick: (nodeId: string) => {
					const kind = nodeKinds.get(nodeId);
					workbench.toggle(nodeId, kind);
				},
				onEdgeClick: (from: string, to: string) => {
					workbench.toggleEdge(from, to);
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

	// Apply edge selection highlighting via CSS after each render
	$effect(() => {
		void workbench.selectedEdgeKeys; // track dependency
		if (!dagContainer) return;
		dagContainer.querySelectorAll('.edge-selected').forEach((el) => {
			el.classList.remove('edge-selected');
		});
		for (const edge of workbench.pinnedEdges) {
			const selector = `[data-edge-from="${edge.from}"][data-edge-to="${edge.to}"]:not([data-edge-hit])`;
			dagContainer.querySelectorAll(selector).forEach((el) => {
				el.classList.add('edge-selected');
			});
		}
	});

	const selectedIds = $derived(workbench.selectedIds);
	const hasPinnedItems = $derived(workbench.pinned.length > 0 || workbench.pinnedEdges.length > 0);

	onMount(async () => {
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
		stateNodes = [];
		stateEdges = [];
		windowNodes = [];
		currentBin = 0;
		binCount = 0;
		availableClasses = [];
		activeClasses = new Set();
		stopPlayback();
		workbench.clear();

		const [graphResult] = await Promise.all([
			flowtime.getGraph(runId),
			flowtime.getRun(runId)
		]);

		if (graphResult.success && graphResult.value) {
			graph = graphResult.value;
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
			// Fetch window for sparklines (one call for full range)
			const windowResult = await flowtime.getStateWindow(runId, 0, binCount - 1);
			if (windowResult.success && windowResult.value) {
				windowNodes = windowResult.value.nodes as Record<string, unknown>[];
			}

			// Snapshot for heatmap + cards
			await loadBin(0);

			// Discover classes (from snapshot data)
			const classes = discoverClasses(stateNodes);
			availableClasses = classes;

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
			stateEdges = stateResult.value.edges as Record<string, unknown>[];
		}
	}

	function getNodeState(nodeId: string): Record<string, unknown> | undefined {
		return stateNodes.find((n) => (n.id as string) === nodeId);
	}

	function getEdgeState(from: string, to: string): Record<string, unknown> | undefined {
		return stateEdges.find((e) => {
			const eFrom = (e.from as string) ?? (e.sourceId as string);
			const eTo = (e.to as string) ?? (e.targetId as string);
			return eFrom === from && eTo === to;
		});
	}

	function onMetricSelect(def: MetricDef) {
		workbench.selectedMetric = def;
	}

	function toggleClass(cls: string) {
		const next = new Set(activeClasses);
		if (next.has(cls)) {
			next.delete(cls);
		} else {
			next.add(cls);
		}
		activeClasses = next;
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

		<div class="mx-1 h-3 w-px bg-border"></div>

		<!-- Metric selector -->
		<MetricSelector selected={workbench.selectedMetric} onSelect={onMetricSelect} />

		<!-- Class filter -->
		{#if availableClasses.length > 0}
			<div class="mx-1 h-3 w-px bg-border"></div>
			<span class="text-[10px] text-muted-foreground">Class:</span>
			<div class="flex items-center gap-1">
				{#each availableClasses as cls (cls)}
					<button
						class="rounded px-1.5 py-0.5 text-[10px] transition-colors {activeClasses.has(cls)
							? 'bg-foreground text-background font-medium'
							: 'bg-muted text-muted-foreground hover:bg-accent'}"
						onclick={() => toggleClass(cls)}
					>
						{cls}
					</button>
				{/each}
				{#if activeClasses.size > 0}
					<button
						class="text-[10px] text-muted-foreground hover:text-foreground px-1"
						onclick={() => (activeClasses = new Set())}
						aria-label="Clear class filter"
					>
						clear
					</button>
				{/if}
			</div>
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
		<!-- Timeline scrubber — above the graph like Blazor -->
		{#if binCount > 0}
			<div class="px-2 py-1 border-b">
				<TimelineScrubber
					{binCount}
					{currentBin}
					{playing}
					onBinChange={(bin) => { stopPlayback(); loadBin(bin); }}
					onTogglePlay={togglePlayback}
					onPrev={prevBin}
					onNext={nextBin}
				/>
			</div>
		{/if}

		<!-- Split: DAG + Workbench -->
		<div class="flex-1 flex flex-col min-h-0">
			<!-- DAG area -->
			<div style="height: {splitRatio}%" class="overflow-auto" bind:this={dagContainer}>
				<DagMapView {graph} metrics={currentMetrics} selected={selectedIds} />
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
				{#if !hasPinnedItems}
					<div class="flex h-full items-center justify-center text-muted-foreground text-xs">
						Click a node or edge to inspect
					</div>
				{:else}
					<div class="flex gap-2 p-2 flex-wrap items-start">
						{#each workbench.pinned as pin (pin.id)}
							{@const nodeState = getNodeState(pin.id)}
							<WorkbenchCard
								nodeId={pin.id}
								kind={pin.kind}
								metrics={nodeState ? extractNodeMetrics(nodeState) : []}
								sparklineValues={sparklineMap.get(pin.id) ?? []}
								sparklineLabel={workbench.selectedMetric.label.toLowerCase()}
								{currentBin}
								onClose={() => workbench.unpin(pin.id)}
							/>
						{/each}
						{#each workbench.pinnedEdges as edge (`${edge.from}\u2192${edge.to}`)}
							{@const edgeState = getEdgeState(edge.from, edge.to)}
							<WorkbenchEdgeCard
								from={edge.from}
								to={edge.to}
								metrics={edgeState ? extractEdgeMetrics(edgeState) : []}
								onClose={() => workbench.unpinEdge(edge.from, edge.to)}
							/>
						{/each}
					</div>
				{/if}
			</div>
		</div>
	{:else if runs.length === 0}
		<div class="flex flex-1 items-center justify-center">
			<p class="text-muted-foreground text-xs">No runs available. Run a model first.</p>
		</div>
	{/if}
</div>

<style>
	:global(.edge-selected) {
		stroke: var(--ft-viz-amber) !important;
		stroke-width: 3 !important;
		opacity: 1 !important;
	}
</style>
