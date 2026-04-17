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
	} from '$lib/api/warnings.js';
	import Chart from '$lib/components/chart.svelte';
	import DagMapView from '$lib/components/dag-map-view.svelte';
	import TimelineScrubber from '$lib/components/timeline-scrubber.svelte';
	import AlertCircleIcon from '@lucide/svelte/icons/alert-circle';
	import AlertTriangleIcon from '@lucide/svelte/icons/alert-triangle';
	import RefreshCwIcon from '@lucide/svelte/icons/refresh-cw';
	import Loader2Icon from '@lucide/svelte/icons/loader-2';
	import type { GraphResponse } from '$lib/api/types.js';

	// Derive WebSocket URL from API base.
	// In dev (no VITE_API_BASE set) use the current page origin so the Vite proxy
	// forwards the WebSocket upgrade to the API (port 5173 → 8081).
	// In production set VITE_API_BASE to the API origin.
	const API_BASE =
		import.meta.env.VITE_API_BASE ??
		(typeof window !== 'undefined' ? window.location.origin : 'http://localhost:8081');
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
	let bins = $state<number>(0);

	// Time scrubber state (m-E17-06). null = mean mode (default); number = bin T.
	let selectedBin = $state<number | null>(null);

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
			bins = result.bins;
			warnings = result.warnings ?? [];
			// Reset scrubber to mean mode when a new model is compiled.
			selectedBin = null;
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

	// Derived metric map for topology heatmap. Recomputes when series or selectedBin
	// changes, but does NOT touch graphResponse — so dag-map-view layout is preserved.
	// In mean mode (selectedBin=null): each node colored by its series mean.
	// In scrubber mode (selectedBin=T): each node colored by its series value at bin T.
	const metricMap = $derived.by(() => {
		if (!engineGraph) return new Map();
		const bin = selectedBin !== null ? selectedBin : undefined;
		return normalizeMetricMap(buildMetricMap(engineGraph, series, bin));
	});

	// Derived edge metric map for topology edge heatmap (m-E17-05 / m-E17-06).
	const edgeMetricMap = $derived.by(() => {
		if (!engineGraph) return new Map();
		const bin = selectedBin !== null ? selectedBin : undefined;
		return normalizeMetricMap(buildEdgeMetricMap(engineGraph, series, bin));
	});

	// Derived warning state (m-E17-04)
	const warningGroups = $derived(groupWarningsByNode(warnings));
	const flaggedNodes = $derived(nodesWithWarnings(warnings));
	const bannerTitle = $derived(warningBannerTitle(warnings));

	// Side effect: after metric/graph/warning updates are applied to the DOM,
	// toggle the `.has-warning` class on flagged SVG node wrappers so they
	// get the highlight styling defined in the page's style block.
	$effect(() => {
		void flaggedNodes;
		void metricMap;
		void graphResponse;
		void warningGroups; // ensure connectors are redrawn when the warning panel appears
		if (!topologyGraphEl) return;

		queueMicrotask(() => {
			if (!topologyGraphEl) return;

			// 1. Apply / remove .has-warning on SVG node wrappers.
			const nodeEls = topologyGraphEl.querySelectorAll<SVGElement>('[data-node-id]');
			for (const el of nodeEls) {
				const id = el.getAttribute('data-node-id');
				if (id && flaggedNodes.has(id)) {
					el.classList.add('has-warning');
				} else {
					el.classList.remove('has-warning');
				}
			}

			// 2. Bezier connectors from each warning node to its overlay entry.
			//    Drawn in a separate absolute SVG so we never touch dag-map's SVG.
			const container = topologyGraphEl.parentElement;
			container?.querySelector('.warning-connectors-svg')?.remove();
			if (!container || flaggedNodes.size === 0) return;

			const cr = container.getBoundingClientRect();
			const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
			svg.classList.add('warning-connectors-svg');
			svg.style.cssText =
				'position:absolute;inset:0;width:100%;height:100%;pointer-events:none;overflow:visible;z-index:10;';
			container.appendChild(svg);

			for (const el of nodeEls) {
				const id = el.getAttribute('data-node-id');
				if (!id || !flaggedNodes.has(id)) continue;

				const shape = el.querySelector<SVGGraphicsElement>('circle, rect');
				if (!shape) continue;

				const sr = shape.getBoundingClientRect();
				// Start: bottom-centre of the node shape
				const nx = sr.left + sr.width / 2 - cr.left;
				const ny = sr.bottom - cr.top;

				const warnEl = container.querySelector(`[data-testid="warning-group-${id}"]`);
				if (!warnEl) continue;

				const wr = warnEl.getBoundingClientRect();
				// End: a few pixels before the warning text starts (left of text, not overlapping)
				const wx = wr.left - cr.left - 12;
				const wy = wr.top + wr.height / 2 - cr.top;

				// Down → left → down path with rounded corners
				// Mid-Y: halfway between node and warning entry
				const midY = ny + (wy - ny) * 0.45;
				const r = Math.min(6, Math.abs(wx - nx) * 0.3, Math.abs(wy - ny) * 0.15);
				const goLeft = wx < nx;
				const path = document.createElementNS('http://www.w3.org/2000/svg', 'path');
				path.setAttribute(
					'd',
					`M ${nx} ${ny}` +
					` L ${nx} ${midY - r}` +
					` Q ${nx} ${midY}, ${nx + (goLeft ? -r : r)} ${midY}` +
					` L ${wx + (goLeft ? r : -r)} ${midY}` +
					` Q ${wx} ${midY}, ${wx} ${midY + r}` +
					` L ${wx} ${wy}`,
				);
				path.setAttribute('stroke', '#f59e0b');
				path.setAttribute('stroke-width', '1.5');
				path.setAttribute('fill', 'none');
				path.setAttribute('stroke-dasharray', '4 3');
				path.setAttribute('opacity', '0.85');
				svg.appendChild(path);

				// Terminal dot at the warning entry end
				const dot = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
				dot.setAttribute('cx', String(wx));
				dot.setAttribute('cy', String(wy));
				dot.setAttribute('r', '3');
				dot.setAttribute('fill', '#f59e0b');
				svg.appendChild(dot);
			}
		});
	});

	// Grouped chart series: for each base series, collect its per-class children.
	const PER_CLASS_COLORS = [
		'var(--ft-viz-green)',
		'var(--ft-viz-coral)',
		'var(--ft-viz-purple)',
		'var(--ft-viz-pink)',
		'var(--ft-viz-amber)',
	];

	interface ChartGroup {
		baseName: string;
		series: ChartSeries[];
		all: { name: string; values: number[] }[];
	}

	const chartGroups = $derived.by<ChartGroup[]>(() => {
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
				chartSeries.push({ name: baseName, values: data.base, color: 'var(--ft-viz-teal)' });
				all.push({ name: baseName, values: data.base });
			}

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
		return () => {};
	});

	onDestroy(() => {
		sliderDebouncer.cancel();
		inputDebouncer.cancel();
		session?.close();
	});

</script>

<div class="flex h-full flex-col gap-3 p-4" data-testid="what-if-page">
	<!-- Header -->
	<div class="flex items-center justify-between">
		<div>
			<h1 class="text-xl font-bold">What-If</h1>
			<p class="text-muted-foreground text-xs">
				Tweak parameters and see results update in real time.
			</p>
		</div>
		{#if lastElapsedUs !== null}
			<div
				class="rounded border bg-muted px-3 py-1.5 text-xs"
				data-testid="latency-badge"
			>
				<div class="text-muted-foreground">Last eval</div>
				<div class="font-mono text-lg font-bold text-foreground">
					<span data-testid="latency-us">{lastElapsedUs}</span> µs
				</div>
			</div>
		{/if}
	</div>

	<!-- Error banner -->
	{#if error}
		<div class="flex items-start gap-2 rounded border border-destructive/50 bg-destructive/10 p-3 text-sm">
			<AlertCircleIcon class="mt-0.5 size-4 text-destructive" />
			<div class="flex-1">
				<div class="font-medium text-destructive">Error</div>
				<div class="mt-1 text-destructive/80">{error}</div>
			</div>
		</div>
	{/if}

	<!-- Main layout: left sidebar + right content -->
	<div class="flex flex-1 gap-4 overflow-hidden">

		<!-- LEFT SIDEBAR: model picker + params -->
		<div class="flex w-64 flex-shrink-0 flex-col gap-3 overflow-y-auto">

			<!-- Model picker (compact vertical list) -->
			<div class="rounded-lg border p-3" data-testid="model-picker">
				<div class="mb-2 text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">
					Model
				</div>
				<div class="space-y-0.5">
					{#each EXAMPLE_MODELS as model}
						<button
							class="w-full rounded px-2 py-1.5 text-left text-sm transition-colors {selectedModel.id === model.id
								? 'bg-accent font-medium text-accent-foreground'
								: 'hover:bg-accent/50'}"
							onclick={() => selectModel(model)}
							title={model.description}
							data-testid="model-button-{model.id}"
						>
							{model.name}
						</button>
					{/each}
				</div>
			</div>

			<!-- Param panel (only when ready) -->
			{#if compileStatus === 'compiling'}
				<div class="flex items-center gap-2 px-1 text-sm text-muted-foreground">
					<Loader2Icon class="size-4 animate-spin" />
					<span>Compiling…</span>
				</div>
			{:else if compileStatus === 'ready'}
				<div class="rounded-lg border p-3" data-testid="param-panel">
					<div class="mb-3 flex items-center justify-between">
						<div class="text-sm font-semibold">Parameters</div>
						<button
							class="flex items-center gap-1 rounded border px-2 py-1 text-xs hover:bg-accent disabled:opacity-50"
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
											class="rounded bg-muted px-1.5 py-0.5 font-mono text-[10px] text-muted-foreground"
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
												class="w-20 rounded border bg-background px-2 py-1 text-sm"
												data-testid="input-{param.id}"
											/>
										</div>
										<div class="text-muted-foreground text-[10px]">
											default: <span class="font-mono">{formatValue(config.initial)}</span>
										</div>
									{:else}
										<!-- Vector: read-only display -->
										<div
											class="rounded bg-muted p-2 font-mono text-xs"
											data-testid="vector-{param.id}"
										>
											{formatSeries(config.values)}
										</div>
										<div class="text-muted-foreground text-[10px]">read-only (vector)</div>
									{/if}
								</div>
							{/each}
						</div>
					{/if}
				</div>
			{/if}
		</div>

		<!-- RIGHT CONTENT: topology + time + charts -->
		{#if compileStatus === 'compiling'}
			<div class="flex flex-1 items-center justify-center text-sm text-muted-foreground">
				<Loader2Icon class="mr-2 size-4 animate-spin" />
				Compiling model…
			</div>
		{:else if compileStatus === 'ready'}
			<div class="flex flex-1 flex-col gap-3 overflow-y-auto" data-testid="what-if-ready">

				<!-- Topology panel — warnings are an absolute overlay, never shift layout -->
				{#if graphResponse && graphResponse.nodes.length > 0}
					<div class="rounded-lg border p-4" data-testid="topology-panel">
						<div class="mb-2 flex items-center justify-between">
							<div class="text-sm font-semibold">Topology</div>
							<div class="flex items-center gap-3">
								<!-- Warning count badge in header — zero layout impact -->
								{#if bannerTitle !== null}
									<div
										class="flex items-center gap-1 text-xs text-amber-500"
										data-testid="warnings-banner"
									>
										<AlertTriangleIcon class="size-3 text-amber-500" />
										<span data-testid="warnings-banner-title">{bannerTitle}</span>
									</div>
								{/if}
								<div class="text-muted-foreground text-[10px]">
									heatmap · {selectedBin !== null ? `bin ${selectedBin}` : 'mean'}
								</div>
							</div>
						</div>

						<!-- Fixed-height container; warnings overlay never shifts siblings -->
						<div class="relative h-64">
							<div
								bind:this={topologyGraphEl}
								class="topology-graph-container h-full overflow-hidden"
								data-testid="topology-graph"
							>
								<DagMapView
									graph={graphResponse}
									metrics={metricMap}
									edgeMetrics={edgeMetricMap}
								/>
							</div>

							<!-- Warnings: absolutely positioned at bottom — zero layout shift -->
							{#if warningGroups.length > 0}
								<div
									class="absolute inset-x-0 bottom-0 border-t border-amber-500/30 bg-amber-500/10 pl-8 pr-3 py-2 backdrop-blur-sm"
									data-testid="warnings-panel"
								>
									<div class="space-y-0.5">
										{#each warningGroups as group (group.nodeId)}
											<div
												class="flex flex-wrap items-baseline gap-x-2 text-[11px]"
												data-testid="warning-group-{group.nodeId}"
											>
												<span class="font-mono font-semibold text-amber-500"
													>{group.nodeId}</span
												>
												{#each group.warnings as w (w.code + w.bins.join(','))}
													<span
														data-testid="warning-row-{group.nodeId}-{w.code}"
													>
														<span class="font-mono font-medium text-amber-400">{w.code}</span>
														<span class="text-foreground/70"> — {w.message}</span>
														{#if w.bins.length > 0}
															<span class="text-muted-foreground">
																({w.bins.length} bin{w.bins.length === 1 ? '' : 's'})
															</span>
														{/if}
													</span>
												{/each}
											</div>
										{/each}
									</div>
								</div>
							{/if}
						</div>
					</div>

					<!-- Time scrubber (m-E17-06) — only when model has > 1 bin -->
					{#if bins > 1}
						<div class="rounded-lg border px-3 py-2" data-testid="time-scrubber-panel">
							<div class="flex items-center gap-2">
								<div class="text-xs font-semibold shrink-0">Time</div>
								<div class="flex-1 min-w-0">
									<TimelineScrubber
										binCount={bins}
										currentBin={selectedBin ?? 0}
										onBinChange={(b) => (selectedBin = b)}
										pointerHidden={selectedBin === null}
										trackTestId="bin-scrubber"
									/>
								</div>
								<div class="text-muted-foreground w-14 text-right font-mono text-[11px] shrink-0">
									{selectedBin !== null ? `Bin ${selectedBin}` : 'Mean'}
								</div>
								<button
									class="rounded border px-2 py-1 text-xs shrink-0 {selectedBin === null
										? 'border-primary bg-accent'
										: 'hover:bg-accent/50'}"
									onclick={() => (selectedBin = null)}
									data-testid="bin-mean-toggle"
								>Mean</button>
							</div>
						</div>
					{/if}
				{/if}

				<!-- Charts panel (compact) -->
				<div class="rounded-lg border p-3" data-testid="series-panel">
					<div class="mb-2 flex items-center justify-between">
						<div class="text-sm font-semibold">Charts</div>
						{#if evalInFlight}
							<div
								class="text-muted-foreground flex items-center gap-1 text-xs"
								data-testid="eval-spinner"
							>
								<Loader2Icon class="size-3 animate-spin" />
								evaluating…
							</div>
						{/if}
					</div>

					<div class="grid grid-cols-2 gap-2 xl:grid-cols-3 2xl:grid-cols-4">
						{#each chartGroups as group (group.baseName)}
							<div class="rounded border p-2" data-testid="series-row-{group.baseName}">
								<div class="mb-1 flex items-center justify-between gap-1">
									<div class="truncate font-mono text-xs font-medium">{group.baseName}</div>
									{#if group.series.length > 1}
										<div class="flex shrink-0 items-center gap-1.5 text-[10px]">
											{#each group.series as s (s.name)}
												<span class="flex items-center gap-0.5">
													<span
														class="inline-block h-1.5 w-1.5 rounded-full"
														style="background: {s.color};"
													></span>
													<span class="text-muted-foreground font-mono">{s.name}</span>
												</span>
											{/each}
										</div>
									{/if}
								</div>
								<Chart
									series={group.series}
									width={200}
									height={60}
									crosshairBin={selectedBin ?? undefined}
								/>
								<!-- Values preserved for test selectors; hidden from view -->
								{#each group.all as entry (entry.name)}
									<div
										class="sr-only"
										data-testid="series-values-{entry.name}"
									>{formatSeries(entry.values)}</div>
								{/each}
							</div>
						{/each}
					</div>
				</div>

			</div>
		{/if}
	</div>
</div>

<style>
	/* Warning highlights — applied via the $effect hook; SVG is {@html} so :global required. */

	/* Node shape: pulsing amber stroke + expanding glow */
	.topology-graph-container :global(.has-warning circle),
	.topology-graph-container :global(.has-warning rect) {
		stroke: #f59e0b;
		stroke-width: 2.5;
		animation: warning-node-pulse 1.4s ease-in-out infinite;
	}

	@keyframes warning-node-pulse {
		0%, 100% {
			stroke-width: 2.5;
			filter: drop-shadow(0 0 3px rgba(245, 158, 11, 0.4));
		}
		50% {
			stroke-width: 4;
			filter: drop-shadow(0 0 14px rgba(245, 158, 11, 1));
		}
	}

	/* Node label: colour-shifts in sync with the shape pulse */
	.topology-graph-container :global(.has-warning text) {
		animation: warning-label-pulse 1.4s ease-in-out infinite;
	}

	@keyframes warning-label-pulse {
		0%, 100% {
			fill: #d97706;
			filter: none;
		}
		50% {
			fill: #f59e0b;
			filter: drop-shadow(0 0 4px rgba(245, 158, 11, 0.9));
		}
	}
</style>
