<script lang="ts">
	import { onDestroy } from 'svelte';
	import {
		EngineSession,
		type ParamInfo,
		type CompileResult,
		type EngineGraph,
		type WarningInfo,
	} from '$lib/api/engine-session.js';
	import { paramControlConfig, kindLabel } from '$lib/api/param-controls.js';
	import { EXAMPLE_MODELS, type ExampleModel } from '$lib/api/example-models.js';
	import { createDebouncer } from '$lib/utils/debounce.js';
	import {
		formatValue,
		formatSeries,
		isCompilerTemp,
		parseClassSeries,
	} from '$lib/utils/format.js';
	import type { ChartSeries } from '$lib/components/chart-geometry.js';
	import {
		buildMetricMap,
		buildEdgeMetricMap,
		normalizeMetricMap,
	} from '$lib/api/topology-metrics.js';
	import { adaptEngineGraph } from '$lib/api/graph-adapter.js';
	import {
		groupWarningsByNode,
		nodesWithWarnings,
		warningBannerTitle,
		warningSummary,
	} from '$lib/api/warnings.js';
	import Chart from '$lib/components/chart.svelte';
	import DagMapView from '$lib/components/dag-map-view.svelte';
	import AlertCircleIcon from '@lucide/svelte/icons/alert-circle';
	import AlertTriangleIcon from '@lucide/svelte/icons/alert-triangle';
	import RefreshCwIcon from '@lucide/svelte/icons/refresh-cw';
	import Loader2Icon from '@lucide/svelte/icons/loader-2';
	import type { GraphResponse } from '$lib/api/types.js';

	// Derive WebSocket URL from API base
	const API_BASE = import.meta.env.VITE_API_BASE ?? 'http://localhost:8081';
	const WS_URL = API_BASE.replace(/^http/, 'ws') + '/v1/engine/session';

	// ── State ──
	let selectedModel = $state<ExampleModel>(EXAMPLE_MODELS[0]);
	let params = $state<ParamInfo[]>([]);
	let series = $state<Record<string, number[]>>({});
	let overrides = $state<Record<string, number>>({});
	let compileStatus = $state<'idle' | 'compiling' | 'ready' | 'error'>('idle');
	let evalInFlight = $state<boolean>(false);
	let lastElapsedUs = $state<number | null>(null);
	let error = $state<string | null>(null);

	// Graph state — only updated on compile (never on eval), so dag-map-view
	// keeps its layout stable across parameter tweaks.
	let engineGraph = $state<EngineGraph | null>(null);
	let graphResponse = $state<GraphResponse | null>(null);

	// Warnings state — refreshed on every compile AND every eval.
	let warnings = $state<WarningInfo[]>([]);

	// DOM handle for the topology graph container — used to apply warning highlights
	// to SVG nodes via a post-render effect.
	let topologyGraphEl = $state<HTMLDivElement | null>(null);

	// ── Session lifecycle ──
	let session: EngineSession | null = null;

	const sliderDebouncer = createDebouncer<Record<string, number>>(50);
	const inputDebouncer = createDebouncer<Record<string, number>>(150);

	function getSession(): EngineSession {
		if (!session) {
			session = new EngineSession(WS_URL);
		}
		return session;
	}

	async function compileModel(model: ExampleModel) {
		compileStatus = 'compiling';
		error = null;
		try {
			const result: CompileResult = await getSession().compile(model.yaml);
			params = result.params;
			series = result.series;
			warnings = result.warnings ?? [];
			// Set graph ONCE on compile — never in runEval. This keeps
			// dag-map-view's layout stable across parameter tweaks.
			engineGraph = result.graph;
			graphResponse = adaptEngineGraph(result.graph);
			overrides = {};
			// Seed overrides with defaults for scalar params (so reset works cleanly)
			for (const p of result.params) {
				if (typeof p.default === 'number') {
					overrides[p.id] = p.default;
				}
			}
			compileStatus = 'ready';
			// Trigger an initial eval with defaults so the latency badge populates
			await runEval({ ...overrides });
		} catch (e) {
			compileStatus = 'error';
			error = e instanceof Error ? e.message : String(e);
		}
	}

	// Derived metric map for topology heatmap. Recomputes when series changes,
	// but does NOT touch graphResponse — so dag-map-view layout is preserved.
	// Each node is colored by its own primary series (by name, with topology
	// queue fallback). Values are normalized to [0, 1] for dag-map's colorScale.
	const metricMap = $derived.by(() => {
		if (!engineGraph) return new Map();
		return normalizeMetricMap(buildMetricMap(engineGraph, series));
	});

	// Derived edge metric map for topology edge heatmap (m-E17-05).
	// Key format: `${fromId}\u2192${toId}` — confirmed from dag-map/src/render.js:151.
	// Each edge is colored by the from-node's primary series mean.
	// Recomputes on series change; does NOT trigger re-layout (edgeMetrics is
	// only consumed by renderSVG, not layoutMetro — see dag-map-view.svelte).
	const edgeMetricMap = $derived.by(() => {
		if (!engineGraph) return new Map();
		return normalizeMetricMap(buildEdgeMetricMap(engineGraph, series));
	});

	// Derived warning state (m-E17-04)
	const warningGroups = $derived(groupWarningsByNode(warnings));
	const flaggedNodes = $derived(nodesWithWarnings(warnings));
	const bannerTitle = $derived(warningBannerTitle(warnings));

	// Side effect: after metric/graph/warning updates are applied to the DOM,
	// toggle the `.has-warning` class on flagged SVG node wrappers so they
	// get the highlight styling defined in the page's style block.
	$effect(() => {
		// Re-run when any of these change — Svelte tracks the reads
		void flaggedNodes;
		void metricMap;
		void graphResponse;
		if (!topologyGraphEl) return;

		// Defer one microtask so dag-map-view's {@html svg} has been applied
		queueMicrotask(() => {
			if (!topologyGraphEl) return;
			const nodeEls = topologyGraphEl.querySelectorAll<SVGElement>('[data-node-id]');
			for (const el of nodeEls) {
				const id = el.getAttribute('data-node-id');
				if (id && flaggedNodes.has(id)) {
					el.classList.add('has-warning');
				} else {
					el.classList.remove('has-warning');
				}
			}
		});
	});

	// Grouped chart series: for each base series, collect its per-class children.
	// The Chart component renders the group as a multi-series overlay.
	// Colors: base is blue, per-class rotate through a palette.
	const PER_CLASS_COLORS = ['#16a34a', '#ea580c', '#9333ea', '#dc2626', '#0891b2'];

	interface ChartGroup {
		baseName: string;
		series: ChartSeries[];
		// Raw values list for data-testid exposure (base + per-class)
		all: { name: string; values: number[] }[];
	}

	const chartGroups = $derived.by<ChartGroup[]>(() => {
		// Collect base series and per-class children into a map keyed by base name
		const byBase = new Map<string, { base?: number[]; classes: Map<string, number[]> }>();

		for (const [name, values] of Object.entries(series)) {
			if (isCompilerTemp(name)) continue;
			const parsed = parseClassSeries(name);
			if (parsed) {
				if (!byBase.has(parsed.base)) {
					byBase.set(parsed.base, { classes: new Map() });
				}
				byBase.get(parsed.base)!.classes.set(parsed.classId, values);
			} else {
				if (!byBase.has(name)) {
					byBase.set(name, { classes: new Map() });
				}
				byBase.get(name)!.base = values;
			}
		}

		const groups: ChartGroup[] = [];
		for (const [baseName, data] of byBase) {
			const chartSeries: ChartSeries[] = [];
			const all: { name: string; values: number[] }[] = [];

			if (data.base) {
				chartSeries.push({ name: baseName, values: data.base, color: '#2563eb' });
				all.push({ name: baseName, values: data.base });
			}

			// Sort class ids for stable ordering
			const classIds = Array.from(data.classes.keys()).sort();
			classIds.forEach((classId, i) => {
				const values = data.classes.get(classId)!;
				chartSeries.push({
					name: classId,
					values,
					color: PER_CLASS_COLORS[i % PER_CLASS_COLORS.length],
				});
				all.push({ name: `${baseName}__class_${classId}`, values });
			});

			if (chartSeries.length > 0) {
				groups.push({ baseName, series: chartSeries, all });
			}
		}

		return groups;
	});

	async function runEval(ov: Record<string, number>) {
		if (!session || compileStatus !== 'ready') return;
		evalInFlight = true;
		error = null;
		try {
			const result = await session.eval(ov);
			series = result.series;
			warnings = result.warnings ?? [];
			lastElapsedUs = result.elapsed_us;
		} catch (e) {
			error = e instanceof Error ? e.message : String(e);
		} finally {
			evalInFlight = false;
		}
	}

	function onSliderChange(paramId: string, value: number) {
		overrides = { ...overrides, [paramId]: value };
		sliderDebouncer.schedule((ov) => runEval(ov), { ...overrides });
	}

	function onSliderEnd() {
		sliderDebouncer.flush();
	}

	function onInputChange(paramId: string, value: number) {
		overrides = { ...overrides, [paramId]: value };
		inputDebouncer.schedule((ov) => runEval(ov), { ...overrides });
	}

	async function resetDefaults() {
		sliderDebouncer.cancel();
		inputDebouncer.cancel();
		const defaults: Record<string, number> = {};
		for (const p of params) {
			if (typeof p.default === 'number') {
				defaults[p.id] = p.default;
			}
		}
		overrides = defaults;
		await runEval(defaults);
	}

	async function selectModel(model: ExampleModel) {
		sliderDebouncer.cancel();
		inputDebouncer.cancel();
		selectedModel = model;
		await compileModel(model);
	}

	// Initial load
	$effect(() => {
		compileModel(selectedModel);
		// Only run on first mount — not when selectedModel changes (that's handled in selectModel)
		return () => {};
	});

	onDestroy(() => {
		sliderDebouncer.cancel();
		inputDebouncer.cancel();
		session?.close();
	});

</script>

<div class="flex h-full flex-col gap-6 p-6" data-testid="what-if-page">
	<!-- Header -->
	<div class="flex items-start justify-between">
		<div>
			<h1 class="text-2xl font-bold">What-If</h1>
			<p class="text-muted-foreground mt-1 text-sm">
				Tweak parameters and see results update in real time.
			</p>
		</div>
		{#if lastElapsedUs !== null}
			<div
				class="rounded-lg border bg-green-50 px-3 py-2 text-xs"
				data-testid="latency-badge"
			>
				<div class="text-green-700">Last eval</div>
				<div class="font-mono text-lg font-bold text-green-900">
					<span data-testid="latency-us">{lastElapsedUs}</span> µs
				</div>
			</div>
		{/if}
	</div>

	<!-- Model picker -->
	<div class="rounded-lg border p-4" data-testid="model-picker">
		<div class="mb-3 text-sm font-semibold">Model</div>
		<div class="grid grid-cols-1 gap-2 sm:grid-cols-3">
			{#each EXAMPLE_MODELS as model}
				<button
					class="rounded-md border p-3 text-left transition-colors {selectedModel.id === model.id
						? 'border-blue-500 bg-blue-50'
						: 'hover:bg-gray-50'}"
					onclick={() => selectModel(model)}
					data-testid="model-button-{model.id}"
				>
					<div class="font-medium">{model.name}</div>
					<div class="text-muted-foreground mt-1 text-xs">{model.description}</div>
				</button>
			{/each}
		</div>
	</div>

	<!-- Error banner -->
	{#if error}
		<div class="flex items-start gap-2 rounded-lg border border-red-300 bg-red-50 p-3 text-sm">
			<AlertCircleIcon class="mt-0.5 size-4 text-red-600" />
			<div class="flex-1">
				<div class="font-medium text-red-800">Error</div>
				<div class="mt-1 text-red-700">{error}</div>
			</div>
		</div>
	{/if}

	<!-- Warnings banner (m-E17-04) -->
	{#if bannerTitle !== null}
		<div
			class="flex items-start gap-2 rounded-lg border border-amber-300 bg-amber-50 p-3 text-sm"
			data-testid="warnings-banner"
		>
			<AlertTriangleIcon class="mt-0.5 size-4 text-amber-600" />
			<div class="flex-1">
				<div class="font-medium text-amber-800" data-testid="warnings-banner-title">
					{bannerTitle}
				</div>
				<div class="mt-1 flex flex-wrap gap-x-3 gap-y-0.5 font-mono text-[11px] text-amber-700">
					{#each warnings.slice(0, 5) as w (w.node_id + w.code)}
						<span>{warningSummary(w)}</span>
					{/each}
					{#if warnings.length > 5}
						<span class="italic">…and {warnings.length - 5} more</span>
					{/if}
				</div>
			</div>
		</div>
	{/if}

	<!-- Loading state -->
	{#if compileStatus === 'compiling'}
		<div class="flex items-center gap-2 text-sm">
			<Loader2Icon class="size-4 animate-spin" />
			<span>Compiling model…</span>
		</div>
	{:else if compileStatus === 'ready'}
		<!-- Main 2-column layout -->
		<div class="grid grid-cols-1 gap-6 lg:grid-cols-[320px_1fr]" data-testid="what-if-ready">
			<!-- Parameter panel -->
			<div class="rounded-lg border p-4" data-testid="param-panel">
				<div class="mb-3 flex items-center justify-between">
					<div class="text-sm font-semibold">Parameters</div>
					<button
						class="flex items-center gap-1 rounded border px-2 py-1 text-xs hover:bg-gray-50 disabled:opacity-50"
						onclick={resetDefaults}
						disabled={evalInFlight}
						data-testid="reset-button"
					>
						<RefreshCwIcon class="size-3" />
						Reset
					</button>
				</div>

				{#if params.length === 0}
					<div class="text-muted-foreground text-sm italic" data-testid="no-params">
						No tweakable parameters in this model.
					</div>
				{:else}
					<div class="space-y-4">
						{#each params as param (param.id)}
							{@const config = paramControlConfig(param)}
							<div class="space-y-1" data-testid="param-row-{param.id}">
								<div class="flex items-center justify-between gap-2">
									<label for="p-{param.id}" class="text-sm font-medium">{param.id}</label>
									<span
										class="rounded bg-gray-100 px-1.5 py-0.5 font-mono text-[10px] text-gray-600"
									>
										{kindLabel(param.kind)}
									</span>
								</div>

								{#if config.type === 'scalar'}
									<div class="flex items-center gap-2">
										<input
											id="p-{param.id}"
											type="range"
											min={config.min}
											max={config.max}
											step={config.step}
											value={overrides[param.id] ?? config.initial}
											oninput={(e) =>
												onSliderChange(param.id, parseFloat((e.target as HTMLInputElement).value))}
											onchange={onSliderEnd}
											class="flex-1"
											data-testid="slider-{param.id}"
										/>
										<input
											type="number"
											min={config.min}
											max={config.max}
											step={config.step}
											value={overrides[param.id] ?? config.initial}
											oninput={(e) =>
												onInputChange(param.id, parseFloat((e.target as HTMLInputElement).value))}
											class="w-20 rounded border px-2 py-1 text-sm"
											data-testid="input-{param.id}"
										/>
									</div>
									<div class="text-muted-foreground text-[10px]">
										default: <span class="font-mono">{formatValue(config.initial)}</span>
									</div>
								{:else}
									<!-- Vector: read-only display -->
									<div class="rounded bg-gray-50 p-2 font-mono text-xs" data-testid="vector-{param.id}">
										{formatSeries(config.values)}
									</div>
									<div class="text-muted-foreground text-[10px]">read-only (vector)</div>
								{/if}
							</div>
						{/each}
					</div>
				{/if}
			</div>

			<!-- Series display -->
			<div class="flex flex-col gap-6">
				<!-- Topology graph -->
				{#if graphResponse && graphResponse.nodes.length > 0}
					<div class="rounded-lg border p-4" data-testid="topology-panel">
						<div class="mb-3 flex items-center justify-between">
							<div class="text-sm font-semibold">Topology</div>
							<div class="text-muted-foreground text-[10px]">
								heatmap: each node colored by its own series mean
							</div>
						</div>
						<div
							bind:this={topologyGraphEl}
							class="topology-graph-container h-64 overflow-hidden"
							data-testid="topology-graph"
						>
							<DagMapView graph={graphResponse} metrics={metricMap} edgeMetrics={edgeMetricMap} />
						</div>
					</div>

					<!-- Warnings panel (m-E17-04) -->
					{#if warningGroups.length > 0}
						<div class="rounded-lg border border-amber-200 p-4" data-testid="warnings-panel">
							<div class="mb-3 flex items-center gap-2">
								<AlertTriangleIcon class="size-4 text-amber-600" />
								<div class="text-sm font-semibold">Warnings</div>
							</div>
							<div class="space-y-3">
								{#each warningGroups as group (group.nodeId)}
									<div
										class="rounded border border-amber-200 bg-amber-50 p-2"
										data-testid="warning-group-{group.nodeId}"
									>
										<div class="mb-1 font-mono text-xs font-semibold text-amber-900">
											{group.nodeId}
										</div>
										<div class="space-y-1">
											{#each group.warnings as w (w.code + w.bins.join(','))}
												<div
													class="text-[11px]"
													data-testid="warning-row-{group.nodeId}-{w.code}"
												>
													<span
														class="font-mono font-medium text-amber-800"
													>{w.code}</span>
													<span class="text-amber-700"> — {w.message}</span>
													{#if w.bins.length > 0}
														<span class="text-muted-foreground">
															({w.bins.length} bin{w.bins.length === 1 ? '' : 's'})
														</span>
													{/if}
												</div>
											{/each}
										</div>
									</div>
								{/each}
							</div>
						</div>
					{/if}
				{/if}

				<!-- Charts panel -->
				<div class="rounded-lg border p-4" data-testid="series-panel">
					<div class="mb-3 flex items-center justify-between">
						<div class="text-sm font-semibold">Charts</div>
						{#if evalInFlight}
							<div class="text-muted-foreground flex items-center gap-1 text-xs" data-testid="eval-spinner">
								<Loader2Icon class="size-3 animate-spin" />
								evaluating…
							</div>
						{/if}
					</div>

					<div class="grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-3">
						{#each chartGroups as group (group.baseName)}
							<div class="rounded border p-2" data-testid="series-row-{group.baseName}">
								<div class="mb-1 flex items-center justify-between gap-2">
									<div class="font-mono text-xs font-medium truncate">{group.baseName}</div>
									{#if group.series.length > 1}
										<div class="flex items-center gap-1.5 text-[10px]">
											{#each group.series as s (s.name)}
												<div class="flex items-center gap-0.5">
													<span
														class="inline-block h-1.5 w-1.5 rounded-full"
														style="background: {s.color};"
													></span>
													<span class="text-muted-foreground font-mono">{s.name}</span>
												</div>
											{/each}
										</div>
									{/if}
								</div>
								<Chart series={group.series} width={240} height={100} />
								<div class="mt-1 space-y-0.5">
									{#each group.all as entry (entry.name)}
										<div
											class="text-muted-foreground font-mono text-[10px] truncate"
											data-testid="series-values-{entry.name}"
										>
											{formatSeries(entry.values)}
										</div>
									{/each}
								</div>
							</div>
						{/each}
					</div>
				</div>
			</div>
		</div>
	{/if}
</div>

<style>
	/* Topology warning highlight — applied by the $effect hook in the script. */
	/* dag-map-view's SVG is injected via {@html}, so these rules use :global. */
	.topology-graph-container :global(.has-warning circle),
	.topology-graph-container :global(.has-warning rect) {
		stroke: #d97706;
		stroke-width: 3;
	}

	/* Secondary "aura" so the highlight reads against the heatmap fill. */
	.topology-graph-container :global(.has-warning circle) {
		filter: drop-shadow(0 0 3px rgba(217, 119, 6, 0.6));
	}
</style>
