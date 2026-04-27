<script lang="ts">
	import { onMount } from 'svelte';
	import { flowtime, type RunSummary, type GraphResponse, type RunIndex } from '$lib/api/index.js';
	import DagMapView from '$lib/components/dag-map-view.svelte';
	import HeatmapView from '$lib/components/heatmap-view.svelte';
	import ViewSwitcher from '$lib/components/view-switcher.svelte';
	import NodeModeToggle from '$lib/components/node-mode-toggle.svelte';
	import WorkbenchCard from '$lib/components/workbench-card.svelte';
	import WorkbenchEdgeCard from '$lib/components/workbench-edge-card.svelte';
	import MetricSelector from '$lib/components/metric-selector.svelte';
	import TimelineScrubber from '$lib/components/timeline-scrubber.svelte';
	import { workbench } from '$lib/stores/workbench.svelte.js';
	import { viewState } from '$lib/stores/view-state.svelte.js';
	import { validation } from '$lib/stores/validation.svelte.js';
	import ValidationPanel from '$lib/components/validation-panel.svelte';
	import {
		extractNodeMetrics,
		extractEdgeMetrics,
		findHighestUtilizationNode,
	} from '$lib/utils/workbench-metrics.js';
	import {
		buildSparklineSeries,
		buildNormalizedMetricMap,
		computeMetricDomainFromWindow,
		discoverClasses,
		type MetricDef,
	} from '$lib/utils/metric-defs.js';
	import { sortHeatmapRows, type SortMode, type HeatmapRowInput } from '$lib/utils/heatmap-sort.js';
	import { buildEdgeSelector, escapeAttributeValue } from '$lib/utils/topology-selectors.js';
	import {
		edgeIndicatorPosition,
		nodeIndicatorPosition,
		parseSvgNumber,
	} from '$lib/utils/topology-indicators.js';
	import { severityChromeToken } from '$lib/utils/validation-helpers.js';
	import { bindEvents } from 'dag-map';
	import AlertCircleIcon from '@lucide/svelte/icons/alert-circle';

	interface NodeMetric {
		value: number;
		label?: string;
	}

	let runs = $state<RunSummary[]>([]);
	let selectedRunId = $state<string | undefined>();
	let graph = $state<GraphResponse | undefined>();
	let runIndex = $state<RunIndex | undefined>();
	let loading = $state(false);
	let error = $state<string | undefined>();

	// Snapshot state (for node/edge cards at current bin)
	let stateNodes = $state<Record<string, unknown>[]>([]);
	let stateEdges = $state<Record<string, unknown>[]>([]);

	// Window state (for sparklines, heatmap, and shared color domain — loaded once per run/mode)
	let windowNodes = $state<Record<string, unknown>[]>([]);
	let windowTimestamps = $state<string[] | undefined>();

	// Graph node kinds (for card display)
	let nodeKinds = $state<Map<string, string>>(new Map());

	// Class filter
	let availableClasses = $state<string[]>([]);

	// Splitter state
	let splitRatio = $state(65);
	let dragging = $state(false);
	let containerEl: HTMLDivElement | undefined = $state();

	// dag-map event cleanup
	let dagContainer: HTMLDivElement | undefined = $state();
	let cleanupEvents: (() => void) | undefined;

	// Playback state (route-local — the scrubber position itself lives in viewState)
	let playInterval: ReturnType<typeof setInterval> | undefined;

	const views = [
		{ id: 'topology', label: 'Topology', shortcut: 'Alt+1' },
		{ id: 'heatmap', label: 'Heatmap', shortcut: 'Alt+2' },
	];

	// ---- derived metric state ------------------------------------------------------
	// Shared color-scale domain over the full window (m-E21-06 AC5 + ADR-02). Both
	// topology and heatmap color from this.
	const sharedDomain = $derived.by(() => {
		if (windowNodes.length === 0) return null;
		return computeMetricDomainFromWindow(
			windowNodes,
			workbench.selectedMetric,
			viewState.activeClasses,
		);
	});

	const currentMetrics = $derived.by<Map<string, NodeMetric> | undefined>(() => {
		if (stateNodes.length === 0) return undefined;
		return buildNormalizedMetricMap(
			stateNodes,
			workbench.selectedMetric,
			viewState.activeClasses,
			sharedDomain,
		);
	});

	const sparklineMap = $derived.by<Map<string, number[]>>(() => {
		if (windowNodes.length === 0) return new Map();
		return buildSparklineSeries(windowNodes, workbench.selectedMetric, viewState.activeClasses);
	});

	// Bind dag-map click events whenever the SVG re-renders
	$effect(() => {
		if (dagContainer) {
			if (cleanupEvents) cleanupEvents();
			cleanupEvents = bindEvents(dagContainer, {
				onNodeClick: (nodeId: string) => {
					const kind = nodeKinds.get(nodeId);
					const wasPinned = workbench.isPinned(nodeId);
					workbench.toggle(nodeId, kind);
					if (!wasPinned) {
						// Click pinned a new node → mark it as the selected card to match
						// heatmap semantics (clicking a cell pins + selects). `currentBin`
						// is the natural bin anchor since topology is a single-bin view.
						viewState.setSelectedCell(nodeId, viewState.currentBin);
					} else if (viewState.selectedCell?.nodeId === nodeId) {
						// Click unpinned the previously-selected node → drop the marker so
						// no card shows stale selection chrome.
						viewState.clearSelectedCell();
					}
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
			const selector = buildEdgeSelector(edge);
			try {
				dagContainer.querySelectorAll(selector).forEach((el) => {
					el.classList.add('edge-selected');
				});
			} catch (err) {
				console.warn('topology: edge selector failed', { edge, err });
			}
		}
	});

	// Apply m-E21-07 AC7 / AC8 warning indicators after each topology render.
	// Reads from `validation.nodeSeverityById` / `validation.edgeSeverityById`
	// (single source of truth per AC11) and injects sibling SVG circles into
	// the dag-map-rendered SVG.
	//
	// Re-run triggers:
	//   - validation maps change (different run → different warnings)
	//   - currentMetrics change (dag-map re-renders the SVG for new metric values)
	//   - selectedIds change (dag-map re-renders for selection-ring redraw)
	//   - dagContainer mounts
	//
	// Cleanup-then-apply pattern matches the edge-selected effect above; the
	// SVG_NS guard keeps appended circles in the SVG namespace so they
	// inherit the surrounding `<svg>` coordinate system.
	$effect(() => {
		void validation.nodeSeverityById; // track dependency
		void validation.edgeSeverityById; // track dependency
		void currentMetrics; // SVG re-renders on metric change — re-apply
		void selectedIds; // SVG re-renders on selection change — re-apply
		void viewState.activeView; // dag-map remounts when switching topology ↔ heatmap
		if (!dagContainer) return;
		// When the active view is heatmap, the dag-map SVG is unmounted —
		// querySelectorAll just no-ops below, but we may as well skip cleanly.
		if (viewState.activeView !== 'topology') return;

		// Cleanup any indicators from a prior pass — the effect owns them
		// exclusively (data-warning-indicator attribute is the seam).
		dagContainer
			.querySelectorAll('[data-warning-indicator]')
			.forEach((el) => el.remove());

		const SVG_NS = 'http://www.w3.org/2000/svg';

		// --- Node indicators (AC7) ----------------------------------------------
		for (const [nodeId, severity] of Object.entries(validation.nodeSeverityById)) {
			const token = severityChromeToken(severity);
			if (token === null) continue; // unknown severity → no chrome treatment
			const groupSelector = `[data-node-id="${escapeAttributeValue(nodeId)}"]`;
			let group: Element | null;
			try {
				group = dagContainer.querySelector(groupSelector);
			} catch (err) {
				console.warn('topology: node indicator selector failed', { nodeId, err });
				continue;
			}
			if (!group) continue;
			// The dag-map render emits an inner `<circle data-id="...">` carrying
			// cx/cy/r — the canonical node geometry. Grab it directly so the
			// indicator follows whatever the layout chose for this node (depth /
			// interchange / scale variants all live on this circle).
			const innerCircle = group.querySelector(
				`circle[data-id="${escapeAttributeValue(nodeId)}"]`,
			);
			if (!innerCircle) continue;
			const cx = parseSvgNumber(innerCircle.getAttribute('cx'));
			const cy = parseSvgNumber(innerCircle.getAttribute('cy'));
			const r = parseSvgNumber(innerCircle.getAttribute('r'));
			if (cx === null || cy === null || r === null) continue;
			const dot = nodeIndicatorPosition({ cx, cy, r });
			const el = document.createElementNS(SVG_NS, 'circle');
			el.setAttribute('cx', dot.cx.toFixed(2));
			el.setAttribute('cy', dot.cy.toFixed(2));
			el.setAttribute('r', dot.r.toFixed(2));
			el.setAttribute('fill', `var(${token})`);
			el.setAttribute('stroke', 'var(--background)');
			el.setAttribute('stroke-width', '0.5');
			el.setAttribute('pointer-events', 'none');
			el.setAttribute('data-warning-indicator', 'node');
			el.setAttribute('data-warning-node-id', nodeId);
			el.setAttribute('data-warning-severity', severity);
			group.appendChild(el);
		}

		// --- Edge indicators (AC8) ----------------------------------------------
		// edgeWarnings keys are opaque (RunWarning.EdgeIds is whatever the
		// analyser persisted) — we parse against the workbench `from→to`
		// convention. If the format doesn't match, skip (no-op fallback,
		// matches the validation panel's edge-row click semantics).
		const ARROW = '→'; // → (matches dag-map's edgeIndex key format)
		for (const [edgeId, severity] of Object.entries(validation.edgeSeverityById)) {
			const token = severityChromeToken(severity);
			if (token === null) continue;
			const idx = edgeId.indexOf(ARROW);
			if (idx <= 0 || idx === edgeId.length - 1) continue;
			const from = edgeId.slice(0, idx);
			const to = edgeId.slice(idx + 1);
			const selector = buildEdgeSelector({ from, to });
			let path: SVGPathElement | null;
			try {
				path = dagContainer.querySelector(selector) as SVGPathElement | null;
			} catch (err) {
				console.warn('topology: edge indicator selector failed', { edgeId, err });
				continue;
			}
			if (!path) continue;
			// getTotalLength / getPointAtLength are only available on
			// SVGGeometryElement — feature-detect rather than assume.
			if (
				typeof path.getTotalLength !== 'function' ||
				typeof path.getPointAtLength !== 'function'
			) {
				continue;
			}
			const totalLength = path.getTotalLength();
			if (!Number.isFinite(totalLength) || totalLength <= 0) continue;
			const mid = path.getPointAtLength(totalLength / 2);
			const dot = edgeIndicatorPosition({ x: mid.x, y: mid.y });
			const el = document.createElementNS(SVG_NS, 'circle');
			el.setAttribute('cx', dot.cx.toFixed(2));
			el.setAttribute('cy', dot.cy.toFixed(2));
			el.setAttribute('r', dot.r.toFixed(2));
			el.setAttribute('fill', `var(${token})`);
			el.setAttribute('stroke', 'var(--background)');
			el.setAttribute('stroke-width', '0.75');
			el.setAttribute('pointer-events', 'none');
			el.setAttribute('data-warning-indicator', 'edge');
			el.setAttribute('data-warning-edge-id', edgeId);
			el.setAttribute('data-warning-severity', severity);
			// Append to the path's parent <svg> so the dot is on top and not
			// constrained by the path element itself (paths cannot have child
			// SVG nodes).
			const svgRoot = path.ownerSVGElement;
			if (svgRoot) svgRoot.appendChild(el);
		}
	});

	const selectedIds = $derived(workbench.selectedIds);
	const hasPinnedItems = $derived(workbench.pinned.length > 0 || workbench.pinnedEdges.length > 0);

	onMount(() => {
		const stored = localStorage.getItem('ft.topology.split');
		if (stored) {
			const v = parseFloat(stored);
			if (isFinite(v) && v >= 20 && v <= 80) splitRatio = v;
		}

		(async () => {
			const result = await flowtime.listRuns(1, 50);
			if (result.success && result.value) {
				runs = result.value.items;
				if (runs.length > 0) {
					selectRun(runs[0].runId);
				}
			}
		})();
		return () => {
			if (playInterval) clearInterval(playInterval);
		};
	});

	async function selectRun(runId: string) {
		selectedRunId = runId;
		loading = true;
		error = undefined;
		graph = undefined;
		runIndex = undefined;
		stateNodes = [];
		stateEdges = [];
		windowNodes = [];
		windowTimestamps = undefined;
		viewState.setCurrentBin(0);
		viewState.setBinCount(0);
		availableClasses = [];
		viewState.clearClasses();
		stopPlayback();
		workbench.clear();
		validation.setResponse(null);

		const [graphResult] = await Promise.all([flowtime.getGraph(runId), flowtime.getRun(runId)]);

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
			runIndex = indexResult.value;
			viewState.setBinCount(indexResult.value.grid?.bins ?? 0);
		}

		loading = false;

		if (viewState.binCount > 0) {
			await loadWindow();
			await loadBin(0);

			const classes = discoverClasses(stateNodes);
			availableClasses = classes;

			const best = findHighestUtilizationNode(stateNodes);
			if (best) {
				workbench.pin(best.id, best.kind);
			}
		}
	}

	async function loadWindow() {
		if (!selectedRunId || viewState.binCount <= 0) return;
		const windowResult = await flowtime.getStateWindow(
			selectedRunId,
			0,
			viewState.binCount - 1,
			viewState.nodeMode,
		);
		if (windowResult.success && windowResult.value) {
			windowNodes = windowResult.value.nodes as Record<string, unknown>[];
			windowTimestamps = windowResult.value.timestampsUtc;
			// Push the same response into the validation store (AC11 single source
			// of truth). The store derives the row list + per-node + per-edge
			// severity-max maps from `warnings[]` and `edgeWarnings`; the panel and
			// the topology AC7 / AC8 indicators (next chunk) read from it.
			validation.setResponse(windowResult.value);
		}
	}

	async function loadBin(bin: number) {
		if (!selectedRunId || bin < 0 || (viewState.binCount > 0 && bin >= viewState.binCount)) return;
		viewState.setCurrentBin(bin);

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
		viewState.toggleClass(cls);
	}

	function prevBin() {
		if (viewState.currentBin > 0) loadBin(viewState.currentBin - 1);
	}
	function nextBin() {
		if (viewState.currentBin < viewState.binCount - 1) loadBin(viewState.currentBin + 1);
	}

	function togglePlayback() {
		if (viewState.playing) {
			stopPlayback();
		} else {
			viewState.setPlaying(true);
			playInterval = setInterval(() => {
				let next = viewState.currentBin + 1;
				if (next >= viewState.binCount) next = 0;
				loadBin(next);
			}, 500);
		}
	}

	function stopPlayback() {
		viewState.setPlaying(false);
		if (playInterval) {
			clearInterval(playInterval);
			playInterval = undefined;
		}
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

	// Heatmap side-effects --------------------------------------------------------
	function onHeatmapPinAndScrub(nodeId: string, bin: number) {
		stopPlayback();
		const kind = nodeKinds.get(nodeId);
		if (!workbench.isPinned(nodeId)) {
			workbench.pin(nodeId, kind);
		}
		if (bin !== viewState.currentBin) {
			loadBin(bin);
		}
	}

	function onHeatmapUnpin(nodeId: string) {
		unpinAndClearSelection(nodeId);
	}

	// Unpin a node AND clear the persistent selection marker if it pointed at that
	// node. Used by every unpin surface (heatmap pin glyph, topology click-to-toggle,
	// workbench card close button) so selection stays consistent — a stale highlight
	// on a no-longer-pinned card would be worse than no highlight at all.
	function unpinAndClearSelection(nodeId: string) {
		workbench.unpin(nodeId);
		if (viewState.selectedCell?.nodeId === nodeId) {
			viewState.clearSelectedCell();
		}
	}

	// Node-mode toggle re-fetches the window response (the server-side filter by
	// node kind lives in /v1/runs/{runId}/state_window). When the user flips
	// operational ↔ full, we do NOT touch the scrubber position or class filter —
	// both views simply re-render against the fresh response.
	async function onNodeModeChange(mode: 'operational' | 'full') {
		if (mode === viewState.nodeMode) return;
		viewState.setNodeMode(mode);
		if (selectedRunId && viewState.binCount > 0) {
			await loadWindow();
		}
	}

	const SORT_MODES: SortMode[] = ['topological', 'id', 'max', 'mean', 'variance'];

	const heatmapGraphEdges = $derived.by(() => {
		if (!graph) return [] as { from: string; to: string }[];
		return graph.edges.map((e) => ({ from: e.from, to: e.to }));
	});

	// Pinned node cards follow the active sort (topological / id / max / mean /
	// variance) in BOTH heatmap and topology views — sort is a graph-wide preference,
	// not heatmap-specific. The sort dropdown only surfaces in the heatmap toolbar,
	// but its effect carries over: pick "topological" in heatmap, switch to topology,
	// cards stay in topological order. Edge cards are unaffected (no sort equivalent).
	const sortedPinnedNodes = $derived.by(() => {
		if (workbench.pinned.length <= 1) return workbench.pinned;
		const rowInputs: HeatmapRowInput[] = workbench.pinned.map((pin) => ({
			id: pin.id,
			series: sparklineMap.get(pin.id) ?? [],
		}));
		const sorted = sortHeatmapRows(rowInputs, {
			mode: viewState.sortMode,
			edges: heatmapGraphEdges,
		});
		const byId = new Map(workbench.pinned.map((p) => [p.id, p]));
		return sorted.map((r) => byId.get(r.id)).filter((p): p is NonNullable<typeof p> => p !== undefined);
	});
</script>

<svelte:window onmousemove={onDrag} onmouseup={stopDrag} />

<svelte:head>
	<title>Topology - FlowTime</title>
</svelte:head>

<div class="flex h-full flex-col min-w-0" bind:this={containerEl}>
	<!-- Toolbar -->
	<div class="flex items-center gap-2 border-b px-2 py-1" data-heatmap-toolbar>
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
						class="rounded px-1.5 py-0.5 text-[10px] transition-colors {viewState.activeClasses.has(cls)
							? 'bg-foreground text-background font-medium'
							: 'bg-muted text-muted-foreground hover:bg-accent'}"
						onclick={() => toggleClass(cls)}
					>
						{cls}
					</button>
				{/each}
				{#if viewState.activeClasses.size > 0}
					<button
						class="text-[10px] text-muted-foreground hover:text-foreground px-1"
						onclick={() => viewState.clearClasses()}
						aria-label="Clear class filter"
					>
						clear
					</button>
				{/if}
			</div>
		{/if}

		<!-- Node-mode toggle (AC15) -->
		<div class="mx-1 h-3 w-px bg-border"></div>
		<NodeModeToggle mode={viewState.nodeMode} onChange={onNodeModeChange} />

		<!-- Heatmap-specific controls: sort + row-stability -->
		{#if viewState.activeView === 'heatmap'}
			<div class="mx-1 h-3 w-px bg-border"></div>
			<span class="text-[10px] text-muted-foreground">Sort:</span>
			<select
				class="bg-background border-input rounded border px-1 py-0.5 text-[10px]"
				value={viewState.sortMode}
				onchange={(e) => viewState.setSortMode((e.target as HTMLSelectElement).value as SortMode)}
				data-testid="heatmap-sort-select"
			>
				{#each SORT_MODES as m}
					<option value={m}>{m}</option>
				{/each}
			</select>
			<label class="flex items-center gap-1 text-[10px] text-muted-foreground">
				<input
					type="checkbox"
					class="align-middle"
					checked={viewState.rowStabilityOn}
					onchange={(e) =>
						viewState.setRowStabilityOn((e.target as HTMLInputElement).checked)}
					data-testid="heatmap-row-stability"
				/>
				Keep filtered rows
			</label>
			<label class="flex items-center gap-1 text-[10px] text-muted-foreground">
				<input
					type="checkbox"
					class="align-middle"
					checked={viewState.fitWidth}
					onchange={(e) => viewState.setFitWidth((e.target as HTMLInputElement).checked)}
					data-testid="heatmap-fit-width"
				/>
				Fit to width
			</label>
		{/if}
	</div>

	<!-- View switcher -->
	{#if graph}
		<div class="px-2 pt-1">
			<ViewSwitcher
				views={views}
				active={viewState.activeView}
				onChange={(id) => viewState.setView(id as 'topology' | 'heatmap')}
			/>
		</div>
	{/if}

	{#if error}
		<div class="flex items-center gap-2 p-3 text-xs text-destructive">
			<AlertCircleIcon class="size-3.5" />
			<span>{error}</span>
		</div>
	{:else if loading}
		<div class="flex-1 bg-muted animate-pulse"></div>
	{:else if graph}
		<!-- Timeline scrubber — above the canvas for both views -->
		{#if viewState.binCount > 0}
			<div class="px-2 py-1 border-b">
				<TimelineScrubber
					binCount={viewState.binCount}
					currentBin={viewState.currentBin}
					playing={viewState.playing}
					onBinChange={(bin) => {
						stopPlayback();
						loadBin(bin);
					}}
					onTogglePlay={togglePlayback}
					onPrev={prevBin}
					onNext={nextBin}
				/>
			</div>
		{/if}

		<!-- Split: canvas (topology | heatmap) + Workbench -->
		<div class="flex-1 flex flex-col min-h-0 min-w-0">
			<!-- Canvas area. `min-w-0` is load-bearing: without it, flex-item intrinsic
			     sizing lets the SVG's default width (up to binCount * 18 px for wide
			     runs) grow this div beyond the viewport, defeating both `overflow-auto`
			     and the heatmap's fit-to-width math. -->
			<div
				style="height: {splitRatio}%"
				class="overflow-auto min-w-0"
				bind:this={dagContainer}
			>
				{#if viewState.activeView === 'topology'}
					<DagMapView {graph} metrics={currentMetrics} selected={selectedIds} />
				{:else if viewState.activeView === 'heatmap'}
					<HeatmapView
						{windowNodes}
						timestampsUtc={windowTimestamps}
						graphEdges={heatmapGraphEdges}
						metric={workbench.selectedMetric}
						binCount={viewState.binCount}
						currentBin={viewState.currentBin}
						activeClasses={viewState.activeClasses}
						pinnedIds={viewState.pinnedIds}
						sortMode={viewState.sortMode}
						rowStabilityOn={viewState.rowStabilityOn}
						grid={runIndex?.grid}
						selectedCell={viewState.selectedCell}
						fitWidth={viewState.fitWidth}
						onPinAndScrub={onHeatmapPinAndScrub}
						onUnpin={onHeatmapUnpin}
						onCellSelect={(id, bin) => viewState.setSelectedCell(id, bin)}
					/>
				{/if}
			</div>

			<!-- Splitter handle -->
			<div
				class="h-1 cursor-row-resize border-y border-border hover:bg-accent flex-shrink-0 {dragging
					? 'bg-accent'
					: ''}"
				role="separator"
				aria-orientation="horizontal"
				onmousedown={startDrag}
			></div>

			<!-- Workbench panel — two-column layout (m-E21-07 AC2 / AC3):
			     Left column: ValidationPanel. Width is driven by validation.state —
			     `issues` → 300 px (per confirmation 1, not user-resizable);
			     `empty` (post-load, zero warnings) → zero width via display:none so
			     the pinned-card region reclaims the full panel width.
			     Right column: existing pinned-card flex row. -->
			<div style="height: {100 - splitRatio}%" class="overflow-hidden bg-background flex min-h-0 min-w-0">
				{#if validation.state === 'issues' || (loading && selectedRunId !== undefined)}
					<div
						class="border-r border-border shrink-0 overflow-hidden"
						style="width: 300px"
					>
						<ValidationPanel loading={loading} />
					</div>
				{/if}
				<div class="flex-1 min-w-0 overflow-auto">
					{#if !hasPinnedItems}
						<div class="flex h-full items-center justify-center text-muted-foreground text-xs">
							Click a node or edge to inspect
						</div>
					{:else}
						<div class="flex gap-2 p-2 flex-wrap items-start">
							{#each sortedPinnedNodes as pin (pin.id)}
								{@const nodeState = getNodeState(pin.id)}
								<WorkbenchCard
									nodeId={pin.id}
									kind={pin.kind}
									metrics={nodeState ? extractNodeMetrics(nodeState) : []}
									sparklineValues={sparklineMap.get(pin.id) ?? []}
									sparklineLabel={workbench.selectedMetric.label.toLowerCase()}
									currentBin={viewState.currentBin}
									selected={viewState.selectedCell?.nodeId === pin.id}
									onClose={() => unpinAndClearSelection(pin.id)}
								/>
							{/each}
							{#each workbench.pinnedEdges as edge (`${edge.from}→${edge.to}`)}
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
