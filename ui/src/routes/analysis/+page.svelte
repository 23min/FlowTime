<script lang="ts">
	import { onMount } from 'svelte';
	import { flowtime, type RunSummary } from '$lib/api/index.js';
	import Chart from '$lib/components/chart.svelte';
	import SensitivityBarChart from '$lib/components/sensitivity-bar-chart.svelte';
	import ConvergenceChart from '$lib/components/convergence-chart.svelte';
	import AnalysisResultCard from '$lib/components/analysis-result-card.svelte';
	import { intervalMarkerGeometry } from '$lib/components/interval-bar-geometry.js';
	import {
		discoverConstParams,
		discoverTopologyNodeIds,
		queueSeriesIds,
		generateRange,
		parseCustomValues,
		projectSweepMeans,
		type ConstParam,
		type SweepResponse,
		type SensitivityPoint,
	} from '$lib/utils/analysis-helpers.js';
	import {
		defaultSearchBounds,
		validateSearchInterval,
		formatResidual,
	} from '$lib/utils/goal-seek-helpers.js';
	import { SAMPLE_MODELS, type SampleModel } from '$lib/utils/sample-models.js';
	import InfoIcon from '@lucide/svelte/icons/info';
	import type { ChartSeries } from '$lib/components/chart-geometry.js';
	import AlertCircleIcon from '@lucide/svelte/icons/alert-circle';
	import Loader2Icon from '@lucide/svelte/icons/loader-2';
	import PlayIcon from '@lucide/svelte/icons/play';

	type Tab = 'sweep' | 'sensitivity' | 'goal-seek' | 'optimize';

	interface TabInfo {
		title: string;
		summary: string;
		useWhen: string;
		reads: string;
		returns: string;
	}

	const TAB_INFO: Record<Tab, TabInfo> = {
		sweep: {
			title: 'Parameter Sweep',
			summary: 'Evaluate the model once for each value in a range of a single parameter and capture the resulting time series.',
			useWhen: 'Use when you want to see how a metric changes across a discrete set of inputs (e.g. "what does queue depth look like as arrivals grow from 10 to 100?").',
			reads: 'One const-node parameter + a list of values (from/to/step or custom).',
			returns: 'For each value, the full per-bin series of every captured output.',
		},
		sensitivity: {
			title: 'Sensitivity Analysis',
			summary: 'Measure how much a target metric changes when each parameter is perturbed by a small amount (central difference, ±perturbation).',
			useWhen: 'Use when you want to rank parameters by influence on a metric — "which knobs matter most?".',
			reads: 'A set of const-node parameters, a target series id, and a perturbation fraction (default ±5%). The engine uses each param\'s first-bin value as the baseline and patches every bin to the flat perturbed value, so baseline values near the saturation threshold give the most informative gradients.',
			returns: 'One gradient per parameter (∂metric_mean / ∂param), sorted by absolute magnitude. A zero gradient usually means the perturbation was too small to move the metric past a saturation threshold — try a larger perturbation or choose a baseline with more dynamics.',
		},
		'goal-seek': {
			title: 'Goal Seek',
			summary: 'Find the value of a single parameter that makes a target metric match a goal value (1-D bisection search).',
			useWhen: 'Use when you know the outcome you want — "what capacity gives me utilization of exactly 0.8?" — and want the engine to solve for the input.',
			reads: 'One parameter to vary, a bounded search interval, a target metric id, and the desired value.',
			returns: 'The parameter value that meets the goal, plus convergence info.',
		},
		optimize: {
			title: 'Optimization',
			summary: 'Search multiple parameters simultaneously to minimize (or maximize) an objective metric using Nelder-Mead simplex.',
			useWhen: 'Use when several inputs trade off against each other — "find the capacity + staffing mix that minimises flow latency".',
			reads: 'N parameters with bounds, an objective metric id, and a minimize/maximize direction.',
			returns: 'The optimal parameter set, the objective value, and the convergence history.',
		},
	};


	type SourceMode = 'run' | 'sample';

	let runs = $state<RunSummary[]>([]);
	let selectedRunId = $state<string | undefined>();
	let modelYaml = $state<string>('');
	let params = $state<ConstParam[]>([]);
	let topologyNodeIds = $state<string[]>([]);
	let loadError = $state<string | undefined>();
	let loading = $state(false);

	// Source mode: fetch from a run or use a bundled sample model
	let sourceMode = $state<SourceMode>('run');
	let selectedSampleId = $state<string>(SAMPLE_MODELS[0].id);
	let scenarioCollapsed = $state<boolean>(false);

	const activeSample = $derived(
		sourceMode === 'sample'
			? SAMPLE_MODELS.find((s) => s.id === selectedSampleId)
			: undefined
	);

	// Tab state
	let activeTab = $state<Tab>('sweep');

	// Info card visibility per tab (collapsible, persisted)
	let infoHidden = $state<Record<Tab, boolean>>({
		sweep: false,
		sensitivity: false,
		'goal-seek': false,
		optimize: false,
	});

	function toggleInfo(tab: Tab) {
		infoHidden = { ...infoHidden, [tab]: !infoHidden[tab] };
		try {
			localStorage.setItem('ft.analysis.infoHidden', JSON.stringify(infoHidden));
		} catch { /* ignore quota errors */ }
	}

	// Sweep state
	let sweepParamId = $state<string>('');
	let sweepFrom = $state<number>(0);
	let sweepTo = $state<number>(100);
	let sweepStep = $state<number>(10);
	let sweepCustom = $state<string>('');
	let sweepCaptureChips = $state<Set<string>>(new Set());
	let sweepRunning = $state(false);
	let sweepError = $state<string | undefined>();
	let sweepResponse = $state<SweepResponse | undefined>();
	let sweepChartSeriesKey = $state<string>('');

	// Sensitivity state
	let sensSelectedParams = $state<Set<string>>(new Set());
	let sensTargetMetric = $state<string>('served');
	let sensPerturbation = $state<number>(0.05);
	let sensRunning = $state(false);
	let sensError = $state<string | undefined>();
	let sensResponse = $state<
		{ metricSeriesId: string; points: SensitivityPoint[] } | undefined
	>();

	// Goal Seek state — retained in-memory across tab switches; reset on scenario change.
	interface GoalSeekResponse {
		paramValue: number;
		achievedMetricMean: number;
		converged: boolean;
		iterations: number;
		trace: {
			iteration: number;
			paramValue: number;
			metricMean: number;
			searchLo: number;
			searchHi: number;
		}[];
	}

	let goalSeekParamId = $state<string>('');
	let goalSeekSearchLo = $state<number>(0);
	let goalSeekSearchHi = $state<number>(1);
	let goalSeekTargetMetric = $state<string>('served');
	let goalSeekTarget = $state<number>(0);
	let goalSeekTolerance = $state<number>(1e-6);
	let goalSeekMaxIterations = $state<number>(50);
	let goalSeekAdvancedOpen = $state<boolean>(false);
	let goalSeekRunning = $state<boolean>(false);
	let goalSeekError = $state<string | undefined>();
	let goalSeekResponse = $state<GoalSeekResponse | undefined>();
	// Bounds recorded at submission time — used to render the search-interval bar
	// against the original search range, not the final trace bracket.
	let goalSeekSubmittedLo = $state<number>(0);
	let goalSeekSubmittedHi = $state<number>(1);

	// Derived sweep values. Custom CSV input produces a plain array; the
	// range generator reports truncation metadata so the UI can distinguish
	// a legitimate "large sweep" warning from a silent clip at 200 points.
	const sweepRange = $derived.by(() => {
		const trimmed = sweepCustom.trim();
		if (trimmed.length > 0) {
			const values = parseCustomValues(trimmed);
			return { values, truncated: false, requestedCount: values.length };
		}
		return generateRange(sweepFrom, sweepTo, sweepStep, 200);
	});
	const sweepValues = $derived(sweepRange.values);
	const sweepTruncated = $derived(sweepRange.truncated);
	const sweepRequestedCount = $derived(sweepRange.requestedCount);

	const sweepOverLimit = $derived(sweepValues.length > 50);
	const hasModel = $derived(modelYaml.length > 0);

	const canRunSweep = $derived(
		hasModel &&
		sweepParamId.length > 0 &&
		sweepValues.length > 0 &&
		!sweepRunning
	);

	const canRunSensitivity = $derived(
		hasModel &&
		sensSelectedParams.size > 0 &&
		sensTargetMetric.length > 0 &&
		!sensRunning
	);

	const goalSeekIntervalValidation = $derived(
		validateSearchInterval({ lo: goalSeekSearchLo, hi: goalSeekSearchHi }),
	);

	const canRunGoalSeek = $derived(
		hasModel &&
		goalSeekParamId.length > 0 &&
		goalSeekTargetMetric.length > 0 &&
		isFinite(goalSeekTarget) &&
		goalSeekIntervalValidation.ok &&
		!goalSeekRunning
	);

	const goalSeekNormalizedTrace = $derived(
		goalSeekResponse
			? goalSeekResponse.trace.map((p) => ({
					iteration: p.iteration,
					metricMean: p.metricMean,
				}))
			: [],
	);

	const goalSeekResidual = $derived(
		goalSeekResponse
			? Math.abs(goalSeekResponse.achievedMetricMean - goalSeekTarget)
			: NaN,
	);

	const goalSeekNotBracketed = $derived(
		goalSeekResponse !== undefined &&
			!goalSeekResponse.converged &&
			goalSeekResponse.iterations === 0,
	);

	const GOAL_SEEK_INTERVAL_BAR_WIDTH = 420;

	const goalSeekIntervalGeom = $derived(
		goalSeekResponse
			? intervalMarkerGeometry({
					lo: goalSeekSubmittedLo,
					hi: goalSeekSubmittedHi,
					value: goalSeekResponse.paramValue,
					width: GOAL_SEEK_INTERVAL_BAR_WIDTH,
				})
			: { ok: false as const },
	);

	// Sweep chart series transformation
	const sweepMeans = $derived.by(() => {
		if (!sweepResponse) return new Map();
		return projectSweepMeans(sweepResponse);
	});

	const sweepSeriesKeys = $derived(Array.from(sweepMeans.keys()));

	const sweepChartSeries = $derived.by<ChartSeries[]>(() => {
		if (!sweepResponse || !sweepChartSeriesKey) return [];
		const rows = sweepMeans.get(sweepChartSeriesKey);
		if (!rows) return [];
		return [
			{
				name: sweepChartSeriesKey,
				values: rows.map((r: { mean: number }) => r.mean),
				color: 'var(--ft-viz-teal)',
			},
		];
	});

	onMount(async () => {
		// Restore tab from localStorage
		const storedTab = localStorage.getItem('ft.analysis.tab');
		if (storedTab === 'sweep' || storedTab === 'sensitivity' || storedTab === 'goal-seek' || storedTab === 'optimize') {
			activeTab = storedTab;
		}
		const storedMode = localStorage.getItem('ft.analysis.source');
		if (storedMode === 'sample' || storedMode === 'run') sourceMode = storedMode;

		const storedSample = localStorage.getItem('ft.analysis.sample');
		if (storedSample && SAMPLE_MODELS.some((s) => s.id === storedSample)) {
			selectedSampleId = storedSample;
		}

		try {
			const storedInfo = localStorage.getItem('ft.analysis.infoHidden');
			if (storedInfo) {
				const parsed = JSON.parse(storedInfo) as Partial<Record<Tab, boolean>>;
				infoHidden = { ...infoHidden, ...parsed };
			}
		} catch { /* ignore parse errors */ }

		const result = await flowtime.listRuns(1, 50);
		if (result.success && result.value) {
			runs = result.value.items;
		}

		if (sourceMode === 'sample') {
			loadSampleModel(selectedSampleId);
		} else if (runs.length > 0) {
			await selectRun(runs[0].runId);
		}
	});

	function applyModelYaml(yaml: string) {
		modelYaml = yaml;
		params = discoverConstParams(yaml);
		topologyNodeIds = discoverTopologyNodeIds(yaml);
		sweepResponse = undefined;
		sensResponse = undefined;
		goalSeekResponse = undefined;
		sweepError = undefined;
		sensError = undefined;
		goalSeekError = undefined;
		if (params.length > 0) {
			sweepParamId = params[0].id;
			sweepFrom = params[0].baseline * 0.5;
			sweepTo = params[0].baseline * 2;
			sweepStep = Math.max(0.001, (sweepTo - sweepFrom) / 10);
			sensSelectedParams = new Set(params.map((p) => p.id));
			goalSeekParamId = params[0].id;
			const bounds = defaultSearchBounds(params[0].baseline);
			goalSeekSearchLo = bounds.lo;
			goalSeekSearchHi = bounds.hi;
		} else {
			sweepParamId = '';
			sensSelectedParams = new Set();
			goalSeekParamId = '';
			goalSeekSearchLo = 0;
			goalSeekSearchHi = 1;
		}
		// Pick a sensible default target metric: first topology node's queue depth,
		// or the first const param, or fall back to 'queue'.
		const suggested = queueSeriesIds(topologyNodeIds);
		if (suggested.length > 0) {
			sensTargetMetric = suggested[0];
			goalSeekTargetMetric = suggested[0];
		} else if (params.length > 0) {
			sensTargetMetric = params[0].id;
			goalSeekTargetMetric = params[0].id;
		} else {
			sensTargetMetric = 'queue';
			goalSeekTargetMetric = 'queue';
		}
		goalSeekTarget = 0;
		goalSeekTolerance = 1e-6;
		goalSeekMaxIterations = 50;
	}

	/**
	 * When the user picks a different param in the Goal Seek selector, reset
	 * the search bounds to the new parameter's `0.5× / 2×` defaults so the
	 * interval is sensibly framed without manual re-entry.
	 */
	function onGoalSeekParamChange(newId: string) {
		goalSeekParamId = newId;
		const p = params.find((pp) => pp.id === newId);
		if (p) {
			const bounds = defaultSearchBounds(p.baseline);
			goalSeekSearchLo = bounds.lo;
			goalSeekSearchHi = bounds.hi;
		}
	}

	// Target metric chips derived from the current model: topology queue series
	// first, then the const-node ids (every const node is itself an output series).
	const targetSuggestions = $derived([
		...queueSeriesIds(topologyNodeIds),
		...params.map((p) => p.id),
	]);

	async function selectRun(runId: string) {
		selectedRunId = runId;
		loading = true;
		loadError = undefined;
		modelYaml = '';
		params = [];
		sweepResponse = undefined;
		sensResponse = undefined;
		sweepError = undefined;
		sensError = undefined;

		const modelResult = await flowtime.getRunModel(runId);
		if (modelResult.success && modelResult.value) {
			applyModelYaml(modelResult.value);
		} else {
			loadError = modelResult.error ?? 'Failed to load model';
		}
		loading = false;
	}

	function loadSampleModel(id: string) {
		selectedSampleId = id;
		const sample = SAMPLE_MODELS.find((s) => s.id === id);
		if (!sample) return;
		loadError = undefined;
		try {
			localStorage.setItem('ft.analysis.sample', id);
		} catch { /* ignore quota errors */ }
		applyModelYaml(sample.yaml);
	}

	function setSourceMode(mode: SourceMode) {
		sourceMode = mode;
		localStorage.setItem('ft.analysis.source', mode);
		if (mode === 'sample') {
			loadSampleModel(selectedSampleId);
		} else if (selectedRunId) {
			selectRun(selectedRunId);
		} else if (runs.length > 0) {
			selectRun(runs[0].runId);
		} else {
			modelYaml = '';
			params = [];
		}
	}

	function setTab(tab: Tab) {
		activeTab = tab;
		localStorage.setItem('ft.analysis.tab', tab);
	}

	function toggleSweepCapture(id: string) {
		const next = new Set(sweepCaptureChips);
		if (next.has(id)) next.delete(id);
		else next.add(id);
		sweepCaptureChips = next;
	}

	function toggleSensParam(id: string) {
		const next = new Set(sensSelectedParams);
		if (next.has(id)) next.delete(id);
		else next.add(id);
		sensSelectedParams = next;
	}

	async function runSweep() {
		if (!canRunSweep) return;
		sweepRunning = true;
		sweepError = undefined;
		sweepResponse = undefined;

		const body = {
			yaml: modelYaml,
			paramId: sweepParamId,
			values: sweepValues,
			captureSeriesIds: sweepCaptureChips.size > 0 ? Array.from(sweepCaptureChips) : undefined,
		};
		const result = await flowtime.sweep(body);
		if (result.success && result.value) {
			sweepResponse = result.value;
			const keys = Object.keys(result.value.points[0]?.series ?? {});
			sweepChartSeriesKey = keys[0] ?? '';
		} else {
			sweepError = result.error ?? 'Sweep failed';
		}
		sweepRunning = false;
	}

	async function runGoalSeek() {
		if (!canRunGoalSeek) return;
		goalSeekRunning = true;
		goalSeekError = undefined;
		goalSeekResponse = undefined;

		const body = {
			yaml: modelYaml,
			paramId: goalSeekParamId,
			metricSeriesId: goalSeekTargetMetric,
			target: goalSeekTarget,
			searchLo: goalSeekSearchLo,
			searchHi: goalSeekSearchHi,
			tolerance: goalSeekTolerance,
			maxIterations: goalSeekMaxIterations,
		};
		// Snapshot the submitted bounds so the interval bar renders against
		// the original range even if the user edits the inputs post-run.
		goalSeekSubmittedLo = goalSeekSearchLo;
		goalSeekSubmittedHi = goalSeekSearchHi;

		const result = await flowtime.goalSeek(body);
		if (result.success && result.value) {
			goalSeekResponse = result.value;
		} else {
			goalSeekError = result.error ?? 'Goal seek failed';
		}
		goalSeekRunning = false;
	}

	async function runSensitivity() {
		if (!canRunSensitivity) return;
		sensRunning = true;
		sensError = undefined;
		sensResponse = undefined;

		const result = await flowtime.sensitivity({
			yaml: modelYaml,
			paramIds: Array.from(sensSelectedParams),
			metricSeriesId: sensTargetMetric,
			perturbation: sensPerturbation,
		});
		if (result.success && result.value) {
			sensResponse = result.value;
		} else {
			sensError = result.error ?? 'Sensitivity failed';
		}
		sensRunning = false;
	}

	function fmtNum(v: number): string {
		if (!isFinite(v)) return '—';
		if (Math.abs(v) >= 1000) return v.toFixed(0);
		if (Math.abs(v) >= 10) return v.toFixed(1);
		if (Math.abs(v) >= 1) return v.toFixed(2);
		return v.toFixed(3);
	}
</script>

<svelte:head>
	<title>Analysis - FlowTime</title>
</svelte:head>

<div class="flex h-full flex-col">
	<!-- Toolbar -->
	<div class="flex items-center gap-2 border-b px-2 py-1">
		<span class="text-xs font-semibold text-muted-foreground">Analysis</span>

		<!-- Source mode toggle -->
		<div class="flex items-center rounded border overflow-hidden text-[10px]">
			<button
				class="px-1.5 py-0.5 {sourceMode === 'run'
					? 'bg-foreground text-background font-medium'
					: 'bg-muted text-muted-foreground hover:bg-accent'}"
				onclick={() => setSourceMode('run')}
			>Run</button>
			<button
				class="px-1.5 py-0.5 {sourceMode === 'sample'
					? 'bg-foreground text-background font-medium'
					: 'bg-muted text-muted-foreground hover:bg-accent'}"
				onclick={() => setSourceMode('sample')}
			>Sample</button>
		</div>

		{#if sourceMode === 'run'}
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
			{:else}
				<span class="text-[10px] text-muted-foreground italic">No runs available</span>
			{/if}
		{:else}
			<select
				class="bg-background border-input rounded border px-1.5 py-0.5 text-xs"
				value={selectedSampleId}
				onchange={(e) => loadSampleModel((e.target as HTMLSelectElement).value)}
			>
				{#each SAMPLE_MODELS as s (s.id)}
					<option value={s.id}>{s.title}</option>
				{/each}
			</select>
		{/if}

		{#if loading}
			<Loader2Icon class="size-3 animate-spin text-muted-foreground" />
		{/if}
		{#if params.length > 0}
			<span class="text-[10px] text-muted-foreground">{params.length} param{params.length === 1 ? '' : 's'}</span>
		{/if}
	</div>

	<!-- Scenario card (only when using a bundled sample model) -->
	{#if activeSample}
		{@const sample = activeSample}
		<div class="border-b bg-muted/20 text-xs">
			<button
				class="w-full flex items-center gap-1.5 px-2 py-1 text-left hover:bg-muted/40"
				onclick={() => (scenarioCollapsed = !scenarioCollapsed)}
				aria-expanded={!scenarioCollapsed}
			>
				<InfoIcon class="size-3 text-muted-foreground shrink-0" />
				<span class="font-semibold">{sample.title}</span>
				<span class="text-muted-foreground text-[10px] truncate min-w-0">— {sample.description}</span>
				<span class="ml-auto text-[10px] text-muted-foreground shrink-0">
					{scenarioCollapsed ? 'show' : 'hide'}
				</span>
			</button>
			{#if !scenarioCollapsed}
				<div class="px-2 pb-2 pt-0.5 border-t flex flex-col gap-1.5">
					<p class="text-[11px] leading-snug max-w-[68ch]">{sample.scenario}</p>
					<div class="grid grid-cols-[max-content_1fr] gap-x-3 gap-y-0.5 text-[11px]">
						{#if Object.keys(sample.paramLegend).length > 0}
							<div class="col-span-2 text-[10px] font-semibold text-muted-foreground uppercase tracking-wide">Parameters (sweepable / sensitivity inputs)</div>
							{#each Object.entries(sample.paramLegend) as [id, meaning] (id)}
								<span class="font-mono text-muted-foreground">{id}</span>
								<span>{meaning}</span>
							{/each}
						{/if}
						{#if Object.keys(sample.nodeLegend).length > 0}
							<div class="col-span-2 text-[10px] font-semibold text-muted-foreground uppercase tracking-wide mt-1">Queues (sensitivity targets)</div>
							{#each Object.entries(sample.nodeLegend) as [id, meaning] (id)}
								<span class="font-mono text-muted-foreground">{id}</span>
								<span>{meaning}</span>
							{/each}
						{/if}
					</div>
				</div>
			{/if}
		</div>
	{/if}

	<!-- Tab bar -->
	<div class="flex items-center border-b">
		{#each [
			{ id: 'sweep', label: 'Sweep' },
			{ id: 'sensitivity', label: 'Sensitivity' },
			{ id: 'goal-seek', label: 'Goal Seek' },
			{ id: 'optimize', label: 'Optimize' }
		] as tab (tab.id)}
			<button
				class="px-3 py-1 text-xs border-b-2 transition-colors {activeTab === tab.id
					? 'border-foreground text-foreground font-medium'
					: 'border-transparent text-muted-foreground hover:text-foreground'}"
				onclick={() => setTab(tab.id as Tab)}
			>
				{tab.label}
			</button>
		{/each}
	</div>

	{#if loadError}
		<div class="flex items-center gap-2 p-3 text-xs text-destructive">
			<AlertCircleIcon class="size-3.5" />
			<span>{loadError}</span>
		</div>
	{:else if sourceMode === 'run' && runs.length === 0 && !loading}
		<div class="flex flex-1 flex-col items-center justify-center gap-2">
			<p class="text-muted-foreground text-xs">No runs available.</p>
			<button
				class="rounded bg-foreground text-background px-2 py-1 text-xs font-medium hover:opacity-90"
				onclick={() => setSourceMode('sample')}
			>Use sample model</button>
		</div>
	{:else}
		<div class="flex-1 overflow-auto p-3 flex flex-col gap-3">
			<!-- Info card (collapsible, per-tab) -->
			{#each [TAB_INFO[activeTab]] as info (activeTab)}
				<div class="border rounded bg-muted/30 text-xs">
					<button
						class="w-full flex items-center gap-1.5 px-2 py-1 text-left hover:bg-muted/50"
						onclick={() => toggleInfo(activeTab)}
						aria-expanded={!infoHidden[activeTab]}
					>
						<InfoIcon class="size-3 text-muted-foreground shrink-0" />
						<span class="font-semibold">{info.title}</span>
						<span class="text-muted-foreground text-[10px] truncate min-w-0">— {info.summary}</span>
						<span class="ml-auto text-[10px] text-muted-foreground shrink-0">
							{infoHidden[activeTab] ? 'show' : 'hide'}
						</span>
					</button>
					{#if !infoHidden[activeTab]}
						<div class="px-2 pb-2 pt-0.5 border-t grid grid-cols-[max-content_1fr] gap-x-3 gap-y-0.5 text-[11px]">
							<span class="text-muted-foreground">Use when:</span>
							<span>{info.useWhen}</span>
							<span class="text-muted-foreground">Reads:</span>
							<span>{info.reads}</span>
							<span class="text-muted-foreground">Returns:</span>
							<span>{info.returns}</span>
						</div>
					{/if}
				</div>
			{/each}

			{#if activeTab === 'sweep'}
				<!-- SWEEP TAB -->
				{#if params.length === 0}
					<p class="text-xs text-muted-foreground italic">No const-kind parameters in this model to sweep over.</p>
				{:else}
					<div class="flex flex-col gap-3">
						<!-- Configuration -->
						<div class="flex flex-wrap items-end gap-3 border rounded p-2 bg-card">
							<label class="flex flex-col gap-0.5 text-xs">
								<span class="text-[10px] text-muted-foreground">Parameter</span>
								<select
									class="bg-background border-input rounded border px-1.5 py-0.5 text-xs"
									bind:value={sweepParamId}
								>
									{#each params as p (p.id)}
										<option value={p.id}>{p.id} (base {fmtNum(p.baseline)})</option>
									{/each}
								</select>
							</label>
							<label class="flex flex-col gap-0.5 text-xs">
								<span class="text-[10px] text-muted-foreground">From</span>
								<input
									type="number"
									class="bg-background border-input rounded border px-1.5 py-0.5 text-xs w-20"
									bind:value={sweepFrom}
								/>
							</label>
							<label class="flex flex-col gap-0.5 text-xs">
								<span class="text-[10px] text-muted-foreground">To</span>
								<input
									type="number"
									class="bg-background border-input rounded border px-1.5 py-0.5 text-xs w-20"
									bind:value={sweepTo}
								/>
							</label>
							<label class="flex flex-col gap-0.5 text-xs">
								<span class="text-[10px] text-muted-foreground">Step</span>
								<input
									type="number"
									class="bg-background border-input rounded border px-1.5 py-0.5 text-xs w-20"
									bind:value={sweepStep}
								/>
							</label>
							<label class="flex flex-col gap-0.5 text-xs flex-1 min-w-[200px]">
								<span class="text-[10px] text-muted-foreground">Or custom (comma-separated)</span>
								<input
									type="text"
									class="bg-background border-input rounded border px-1.5 py-0.5 text-xs"
									placeholder="e.g. 10, 20, 50, 100"
									bind:value={sweepCustom}
								/>
							</label>
							<button
								class="rounded bg-foreground text-background px-2 py-1 text-xs font-medium hover:opacity-90 disabled:opacity-40"
								onclick={runSweep}
								disabled={!canRunSweep}
							>
								{#if sweepRunning}
									<Loader2Icon class="inline size-3 animate-spin" />
								{:else}
									<PlayIcon class="inline size-3" />
								{/if}
								Run sweep
							</button>
						</div>

						<!-- Capture series chips (derived from the current model) -->
						<div class="flex items-center gap-1 flex-wrap text-xs">
							<span class="text-[10px] text-muted-foreground">Capture:</span>
							{#if targetSuggestions.length === 0}
								<span class="text-[10px] text-muted-foreground italic">(no series detected; request returns all)</span>
							{:else}
								{#each targetSuggestions as s (s)}
									<button
										class="rounded px-1.5 py-0.5 text-[10px] font-mono {sweepCaptureChips.has(s)
											? 'bg-foreground text-background font-medium'
											: 'bg-muted text-muted-foreground hover:bg-accent'}"
										onclick={() => toggleSweepCapture(s)}
									>{s}</button>
								{/each}
								{#if sweepCaptureChips.size === 0}
									<span class="text-[10px] text-muted-foreground italic">(none selected → all captured)</span>
								{/if}
							{/if}
						</div>

						<!-- Values preview -->
						<div class="text-[10px] text-muted-foreground font-mono" data-testid="sweep-values-preview">
							{sweepValues.length} point{sweepValues.length === 1 ? '' : 's'}:
							{sweepValues.slice(0, 20).map(fmtNum).join(', ')}
							{#if sweepValues.length > 20}…{/if}
							{#if sweepTruncated}
								<span class="text-red-500 ml-2" data-testid="sweep-truncation-warning">
									⚠ truncated — first {sweepValues.length} of {sweepRequestedCount} requested
								</span>
							{:else if sweepOverLimit}
								<span class="text-amber-500 ml-2">⚠ large sweep (&gt; 50 points)</span>
							{/if}
						</div>

						<!-- Error -->
						{#if sweepError}
							<div class="flex items-center gap-1 text-xs text-destructive">
								<AlertCircleIcon class="size-3" />
								<span>{sweepError}</span>
							</div>
						{/if}

						<!-- Results -->
						{#if sweepResponse}
							<div class="flex flex-col gap-2 border rounded p-2 bg-card">
								<div class="flex items-center gap-2">
									<span class="text-xs font-semibold">Chart:</span>
									<select
										class="bg-background border-input rounded border px-1.5 py-0.5 text-xs"
										bind:value={sweepChartSeriesKey}
									>
										{#each sweepSeriesKeys as k (k)}
											<option value={k}>{k}</option>
										{/each}
									</select>
									<span class="text-[10px] text-muted-foreground">(mean over bins per sweep point)</span>
								</div>
								<Chart
									series={sweepChartSeries}
									width={520}
									height={180}
								/>
								<!-- Table -->
								<div class="overflow-auto max-h-[200px] mt-2">
									<table class="text-[10px] font-mono">
										<thead>
											<tr class="text-muted-foreground">
												<th class="text-left pr-3 sticky top-0 bg-card">paramValue</th>
												{#each sweepSeriesKeys as k (k)}
													<th class="text-right pr-3 sticky top-0 bg-card">{k} (mean)</th>
												{/each}
											</tr>
										</thead>
										<tbody>
											{#each sweepResponse.points as pt, i (i)}
												<tr>
													<td class="pr-3 tabular-nums">{fmtNum(pt.paramValue)}</td>
													{#each sweepSeriesKeys as k (k)}
														{@const row = sweepMeans.get(k)?.[i]}
														<td class="text-right pr-3 tabular-nums">{row ? fmtNum(row.mean) : '—'}</td>
													{/each}
												</tr>
											{/each}
										</tbody>
									</table>
								</div>
							</div>
						{/if}
					</div>
				{/if}

			{:else if activeTab === 'sensitivity'}
				<!-- SENSITIVITY TAB -->
				{#if params.length === 0}
					<p class="text-xs text-muted-foreground italic">No const-kind parameters to analyse.</p>
				{:else}
					<div class="flex flex-col gap-3">
						<div class="flex flex-col gap-2 border rounded p-2 bg-card">
							<!-- Param chips -->
							<div class="flex items-center gap-1 flex-wrap text-xs">
								<span class="text-[10px] text-muted-foreground">Parameters:</span>
								{#each params as p (p.id)}
									<button
										class="rounded px-1.5 py-0.5 text-[10px] {sensSelectedParams.has(p.id)
											? 'bg-foreground text-background font-medium'
											: 'bg-muted text-muted-foreground hover:bg-accent'}"
										onclick={() => toggleSensParam(p.id)}
									>{p.id}</button>
								{/each}
							</div>

							<!-- Target metric -->
							<div class="flex items-center gap-2 text-xs flex-wrap">
								<label class="flex flex-col gap-0.5">
									<span class="text-[10px] text-muted-foreground">Target metric</span>
									<input
										type="text"
										class="bg-background border-input rounded border px-1.5 py-0.5 text-xs w-48"
										bind:value={sensTargetMetric}
									/>
								</label>
								<div class="flex flex-wrap items-center gap-1 mt-3">
									{#if targetSuggestions.length === 0}
										<span class="text-[10px] text-muted-foreground italic">
											(type a series id, e.g. queue_queue for topology node "Queue")
										</span>
									{:else}
										<span class="text-[10px] text-muted-foreground">suggested:</span>
									{/if}
									{#each targetSuggestions as t (t)}
										<button
											class="rounded px-1.5 py-0.5 text-[10px] font-mono {sensTargetMetric === t
												? 'bg-foreground text-background font-medium'
												: 'bg-muted text-muted-foreground hover:bg-accent'}"
											onclick={() => (sensTargetMetric = t)}
										>{t}</button>
									{/each}
								</div>
							</div>

							<!-- Perturbation (symmetric: engine evaluates at baseline × (1 ± p)) -->
							<div class="flex items-center gap-2 text-xs">
								<label class="flex items-center gap-2">
									<span class="text-[10px] text-muted-foreground">Perturbation (±):</span>
									<input
										type="range"
										min="0.01"
										max="0.30"
										step="0.01"
										bind:value={sensPerturbation}
										class="w-32"
									/>
									<span class="font-mono tabular-nums text-[10px] w-16">
										±{(sensPerturbation * 100).toFixed(0)}%
									</span>
									<span class="text-[10px] text-muted-foreground">
										(engine evaluates at baseline × 1 ± {(sensPerturbation * 100).toFixed(0)}%)
									</span>
								</label>
							</div>

							<div>
								<button
									class="rounded bg-foreground text-background px-2 py-1 text-xs font-medium hover:opacity-90 disabled:opacity-40"
									onclick={runSensitivity}
									disabled={!canRunSensitivity}
								>
									{#if sensRunning}
										<Loader2Icon class="inline size-3 animate-spin" />
									{:else}
										<PlayIcon class="inline size-3" />
									{/if}
									Run sensitivity
								</button>
							</div>
						</div>

						{#if sensError}
							<div class="flex items-center gap-1 text-xs text-destructive">
								<AlertCircleIcon class="size-3" />
								<span>{sensError}</span>
							</div>
						{/if}

						{#if sensResponse}
							{@const metricId = sensResponse.metricSeriesId}
							{@const sortedPoints = [...sensResponse.points].sort((a, b) => {
								const aFin = isFinite(a.gradient);
								const bFin = isFinite(b.gradient);
								if (aFin && !bFin) return -1;
								if (!aFin && bFin) return 1;
								if (!aFin && !bFin) return 0;
								return Math.abs(b.gradient) - Math.abs(a.gradient);
							})}
							<div class="flex flex-col gap-2 border rounded p-2 bg-card">
								<div class="flex flex-col gap-0.5">
									<div class="text-xs font-semibold">
										∂ <span class="font-mono">{metricId}</span> / ∂ param
									</div>
									<div class="text-[10px] text-muted-foreground">
										At baseline, perturbing each parameter by ±{(sensPerturbation * 100).toFixed(0)}%,
										how much does the <span class="font-mono">{metricId}</span>
										mean change per unit of parameter? Sorted by magnitude.
									</div>
								</div>

								<!-- Bar chart for quick visual ranking -->
								<SensitivityBarChart points={sensResponse.points} />

								<!-- Detailed per-param table with baseline + interpretation -->
								<div class="overflow-auto">
									<table class="w-full text-[11px] font-mono">
										<thead>
											<tr class="text-muted-foreground text-[10px] border-b">
												<th class="text-left pr-3 py-0.5 whitespace-nowrap">Parameter</th>
												<th class="text-right pr-3 py-0.5 whitespace-nowrap">Baseline</th>
												<th class="text-right pr-3 py-0.5 whitespace-nowrap">Gradient</th>
												<th class="text-left py-0.5">Interpretation</th>
											</tr>
										</thead>
										<tbody>
											{#each sortedPoints as p (p.paramId)}
												{@const sign = p.gradient > 0 ? 'increases' : p.gradient < 0 ? 'decreases' : 'is unchanged'}
												{@const mag = isFinite(p.gradient) ? Math.abs(p.gradient).toFixed(2) : '—'}
												<tr class="border-b border-border/50 last:border-0">
													<td class="pr-3 py-0.5 tabular-nums whitespace-nowrap">{p.paramId}</td>
													<td class="pr-3 py-0.5 text-right tabular-nums whitespace-nowrap">{fmtNum(p.baseValue)}</td>
													<td class="pr-3 py-0.5 text-right tabular-nums whitespace-nowrap {p.gradient > 0 ? 'text-[color:var(--ft-viz-teal)]' : p.gradient < 0 ? 'text-[color:var(--ft-viz-coral)]' : 'text-muted-foreground'}">
														{fmtNum(p.gradient)}
													</td>
													<td class="py-0.5 font-sans text-[11px]">
														{#if isFinite(p.gradient) && p.gradient !== 0}
															A +1 change in <span class="font-mono text-[10px]">{p.paramId}</span>
															{sign} the mean <span class="font-mono text-[10px]">{metricId}</span> by {mag}.
														{:else if p.gradient === 0}
															No measurable effect at this baseline / perturbation.
														{:else}
															Gradient is not finite — check the model compiles.
														{/if}
													</td>
												</tr>
											{/each}
										</tbody>
									</table>
								</div>

								<!-- Zero-gradient hint -->
								{#if sortedPoints.length > 0 && sortedPoints.every((p) => !isFinite(p.gradient) || p.gradient === 0)}
									<div class="text-[10px] text-amber-500 border-t pt-1">
										All gradients are zero. The engine patches every bin to a flat value at <em>baseline × (1 ± perturbation)</em>.
										If the baseline is well below the service rate, a small perturbation stays below saturation and the queue never builds.
										Try a larger perturbation, or a sample whose baseline is near the saturation threshold.
									</div>
								{/if}
							</div>
						{/if}
					</div>
				{/if}

			{:else if activeTab === 'goal-seek'}
				<!-- GOAL SEEK TAB -->
				{#if params.length === 0}
					<p class="text-xs text-muted-foreground italic" data-testid="goal-seek-empty">
						No const-kind parameters in this model to seek over.
					</p>
				{:else}
					<div class="flex flex-col gap-3">
						<!-- Configuration -->
						<div class="flex flex-col gap-2 border rounded p-2 bg-card" data-testid="goal-seek-config">
							<div class="flex flex-wrap items-end gap-3">
								<label class="flex flex-col gap-0.5 text-xs">
									<span class="text-[10px] text-muted-foreground">Parameter</span>
									<select
										class="bg-background border-input rounded border px-1.5 py-0.5 text-xs"
										data-testid="goal-seek-param-select"
										value={goalSeekParamId}
										onchange={(e) => onGoalSeekParamChange((e.target as HTMLSelectElement).value)}
									>
										{#each params as p (p.id)}
											<option value={p.id}>{p.id} (base {fmtNum(p.baseline)})</option>
										{/each}
									</select>
								</label>
								<label class="flex flex-col gap-0.5 text-xs">
									<span class="text-[10px] text-muted-foreground">searchLo</span>
									<input
										type="number"
										class="bg-background border-input rounded border px-1.5 py-0.5 text-xs w-24"
										data-testid="goal-seek-lo"
										bind:value={goalSeekSearchLo}
									/>
								</label>
								<label class="flex flex-col gap-0.5 text-xs">
									<span class="text-[10px] text-muted-foreground">searchHi</span>
									<input
										type="number"
										class="bg-background border-input rounded border px-1.5 py-0.5 text-xs w-24"
										data-testid="goal-seek-hi"
										bind:value={goalSeekSearchHi}
									/>
								</label>
								<label class="flex flex-col gap-0.5 text-xs">
									<span class="text-[10px] text-muted-foreground">target</span>
									<input
										type="number"
										step="any"
										class="bg-background border-input rounded border px-1.5 py-0.5 text-xs w-28"
										data-testid="goal-seek-target"
										bind:value={goalSeekTarget}
									/>
								</label>
								<button
									class="rounded bg-foreground text-background px-2 py-1 text-xs font-medium hover:opacity-90 disabled:opacity-40"
									onclick={runGoalSeek}
									disabled={!canRunGoalSeek}
									data-testid="goal-seek-run"
								>
									{#if goalSeekRunning}
										<Loader2Icon class="inline size-3 animate-spin" />
									{:else}
										<PlayIcon class="inline size-3" />
									{/if}
									Run goal seek
								</button>
							</div>

							<!-- Metric + chip shortcuts (parallels sensitivity tab) -->
							<div class="flex items-center gap-2 text-xs flex-wrap">
								<label class="flex flex-col gap-0.5">
									<span class="text-[10px] text-muted-foreground">metricSeriesId</span>
									<input
										type="text"
										class="bg-background border-input rounded border px-1.5 py-0.5 text-xs w-48"
										data-testid="goal-seek-metric"
										bind:value={goalSeekTargetMetric}
									/>
								</label>
								<div class="flex flex-wrap items-center gap-1 mt-3">
									{#if targetSuggestions.length === 0}
										<span class="text-[10px] text-muted-foreground italic">
											(type a series id)
										</span>
									{:else}
										<span class="text-[10px] text-muted-foreground">suggested:</span>
									{/if}
									{#each ['served', 'queue', 'flowLatencyMs', 'utilization', ...targetSuggestions] as t (t)}
										<button
											class="rounded px-1.5 py-0.5 text-[10px] font-mono {goalSeekTargetMetric === t
												? 'bg-foreground text-background font-medium'
												: 'bg-muted text-muted-foreground hover:bg-accent'}"
											onclick={() => (goalSeekTargetMetric = t)}
										>{t}</button>
									{/each}
								</div>
							</div>

							<!-- Validation hint -->
							{#if !goalSeekIntervalValidation.ok}
								<div
									class="flex items-center gap-1 text-[11px] text-amber-600 dark:text-amber-400"
									data-testid="goal-seek-interval-warning"
								>
									<AlertCircleIcon class="size-3" />
									<span>{goalSeekIntervalValidation.reason}</span>
								</div>
							{/if}

							<!-- Advanced disclosure -->
							<div class="flex flex-col gap-1 text-xs">
								<button
									class="self-start text-[11px] text-muted-foreground hover:text-foreground"
									onclick={() => (goalSeekAdvancedOpen = !goalSeekAdvancedOpen)}
									aria-expanded={goalSeekAdvancedOpen}
									data-testid="goal-seek-advanced-toggle"
								>
									{goalSeekAdvancedOpen ? '▼' : '▶'} Advanced
								</button>
								{#if goalSeekAdvancedOpen}
									<div
										class="flex flex-wrap items-end gap-3 pl-2"
										data-testid="goal-seek-advanced"
									>
										<label class="flex flex-col gap-0.5">
											<span class="text-[10px] text-muted-foreground">tolerance</span>
											<input
												type="number"
												step="any"
												class="bg-background border-input rounded border px-1.5 py-0.5 text-xs w-24"
												data-testid="goal-seek-tolerance"
												bind:value={goalSeekTolerance}
											/>
										</label>
										<label class="flex flex-col gap-0.5">
											<span class="text-[10px] text-muted-foreground">maxIterations</span>
											<input
												type="number"
												min="1"
												class="bg-background border-input rounded border px-1.5 py-0.5 text-xs w-24"
												data-testid="goal-seek-max-iterations"
												bind:value={goalSeekMaxIterations}
											/>
										</label>
									</div>
								{/if}
							</div>
						</div>

						{#if goalSeekError}
							<div class="flex items-center gap-1 text-xs text-destructive" data-testid="goal-seek-error">
								<AlertCircleIcon class="size-3" />
								<span>{goalSeekError}</span>
							</div>
						{/if}

						{#if goalSeekResponse}
							{@const resp = goalSeekResponse}
							{@const gbadge = goalSeekNotBracketed
								? 'target not reachable'
								: resp.converged
									? 'converged'
									: 'did not converge'}
							{@const gtone = resp.converged ? 'teal' : 'amber'}
							<AnalysisResultCard
								title="Goal Seek result"
								badge={gbadge}
								badgeTone={gtone}
								meta={[
									{ label: 'achieved', value: fmtNum(resp.achievedMetricMean) },
									{ label: 'target', value: fmtNum(goalSeekTarget) },
									{ label: 'residual', value: formatResidual(goalSeekResidual) },
									{ label: 'iterations', value: String(resp.iterations) },
									{ label: 'tolerance', value: formatResidual(goalSeekTolerance) },
								]}
							>
								{#snippet primaryValue()}
									<span data-testid="goal-seek-param-value">{fmtNum(resp.paramValue)}</span>
									<span class="text-muted-foreground text-xs ml-2 font-sans">
										← {goalSeekParamId}
									</span>
								{/snippet}
								{#snippet footer()}
									{#if goalSeekNotBracketed}
										<div
											class="flex items-start gap-1 text-[11px] text-amber-600 dark:text-amber-400 border-t pt-1"
											data-testid="goal-seek-not-bracketed-warning"
										>
											<AlertCircleIcon class="size-3 mt-0.5 shrink-0" />
											<span>
												Target {fmtNum(goalSeekTarget)} is not reachable inside
												[{fmtNum(goalSeekSubmittedLo)}, {fmtNum(goalSeekSubmittedHi)}].
												Try widening the search bounds so the boundary evaluations
												bracket the target.
											</span>
										</div>
									{:else if !resp.converged}
										<div
											class="flex items-start gap-1 text-[11px] text-amber-600 dark:text-amber-400 border-t pt-1"
											data-testid="goal-seek-max-iterations-warning"
										>
											<AlertCircleIcon class="size-3 mt-0.5 shrink-0" />
											<span>
												Bisection exhausted {resp.iterations} iteration{resp.iterations === 1 ? '' : 's'} without meeting tolerance {formatResidual(goalSeekTolerance)}.
												Increase maxIterations or relax tolerance.
											</span>
										</div>
									{/if}
								{/snippet}
							</AnalysisResultCard>

							<!-- Convergence chart -->
							<div class="flex flex-col gap-1 border rounded p-2 bg-card">
								<span class="text-xs font-semibold">Convergence</span>
								<ConvergenceChart
									trace={goalSeekNormalizedTrace}
									target={goalSeekTarget}
									converged={resp.converged}
									yLabel={goalSeekTargetMetric}
									width={520}
									height={180}
								/>
							</div>

							<!-- Search-interval bar -->
							{#if goalSeekIntervalGeom.ok}
								{@const gi = goalSeekIntervalGeom}
								<div class="flex flex-col gap-1 border rounded p-2 bg-card" data-testid="goal-seek-interval-bar">
									<span class="text-xs font-semibold">Search interval</span>
									<svg
										width={GOAL_SEEK_INTERVAL_BAR_WIDTH}
										height="28"
										viewBox="0 0 {GOAL_SEEK_INTERVAL_BAR_WIDTH} 28"
										role="img"
										aria-label="search interval bar"
									>
										<line
											x1={gi.barStart}
											x2={gi.barEnd}
											y1="14"
											y2="14"
											stroke="var(--muted-foreground)"
											stroke-width="2"
											opacity="0.6"
										/>
										<circle
											cx={gi.barStart}
											cy="14"
											r="3"
											fill="var(--muted-foreground)"
										/>
										<circle
											cx={gi.barEnd}
											cy="14"
											r="3"
											fill="var(--muted-foreground)"
										/>
										<line
											x1={gi.markerX}
											x2={gi.markerX}
											y1="4"
											y2="24"
											stroke={resp.converged ? 'var(--ft-viz-teal)' : 'var(--ft-viz-amber)'}
											stroke-width="2"
											data-testid="goal-seek-interval-marker"
										/>
									</svg>
									<div class="flex justify-between text-[10px] text-muted-foreground font-mono">
										<span>{fmtNum(goalSeekSubmittedLo)}</span>
										<span>paramValue = {fmtNum(resp.paramValue)}{gi.clamped ? ' (clamped)' : ''}</span>
										<span>{fmtNum(goalSeekSubmittedHi)}</span>
									</div>
								</div>
							{/if}
						{/if}
					</div>
				{/if}

			{:else if activeTab === 'optimize'}
				<div class="flex items-center justify-center h-full">
					<p class="text-xs text-muted-foreground italic">
						Optimize surface — coming in m-E21-05.
					</p>
				</div>
			{/if}
		</div>
	{/if}
</div>
