<script lang="ts">
	import { onMount } from 'svelte';
	import { flowtime, type RunSummary } from '$lib/api/index.js';
	import Chart from '$lib/components/chart.svelte';
	import SensitivityBarChart from '$lib/components/sensitivity-bar-chart.svelte';
	import {
		discoverConstParams,
		generateRange,
		parseCustomValues,
		projectSweepMeans,
		type ConstParam,
		type SweepResponse,
		type SensitivityPoint,
	} from '$lib/utils/analysis-helpers.js';
	import type { ChartSeries } from '$lib/components/chart-geometry.js';
	import AlertCircleIcon from '@lucide/svelte/icons/alert-circle';
	import Loader2Icon from '@lucide/svelte/icons/loader-2';
	import PlayIcon from '@lucide/svelte/icons/play';

	type Tab = 'sweep' | 'sensitivity' | 'goal-seek' | 'optimize';

	const COMMON_SERIES = ['arrivals', 'served', 'errors', 'queue', 'utilization', 'flowLatencyMs'];
	const COMMON_TARGETS = ['served', 'queue', 'flowLatencyMs', 'utilization'];

	let runs = $state<RunSummary[]>([]);
	let selectedRunId = $state<string | undefined>();
	let modelYaml = $state<string>('');
	let params = $state<ConstParam[]>([]);
	let loadError = $state<string | undefined>();
	let loading = $state(false);

	// Tab state
	let activeTab = $state<Tab>('sweep');

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

	// Derived sweep values
	const sweepValues = $derived.by<number[]>(() => {
		const trimmed = sweepCustom.trim();
		if (trimmed.length > 0) return parseCustomValues(trimmed);
		return generateRange(sweepFrom, sweepTo, sweepStep, 200);
	});

	const sweepOverLimit = $derived(sweepValues.length > 50);
	const canRunSweep = $derived(
		!!selectedRunId &&
		sweepParamId.length > 0 &&
		sweepValues.length > 0 &&
		!sweepRunning
	);

	const canRunSensitivity = $derived(
		!!selectedRunId &&
		sensSelectedParams.size > 0 &&
		sensTargetMetric.length > 0 &&
		!sensRunning
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

		const result = await flowtime.listRuns(1, 50);
		if (result.success && result.value) {
			runs = result.value.items;
			if (runs.length > 0) {
				await selectRun(runs[0].runId);
			}
		}
	});

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
			modelYaml = modelResult.value;
			params = discoverConstParams(modelYaml);
			if (params.length > 0) {
				sweepParamId = params[0].id;
				sweepFrom = params[0].baseline * 0.5;
				sweepTo = params[0].baseline * 2;
				sweepStep = Math.max(0.001, (sweepTo - sweepFrom) / 10);
				sensSelectedParams = new Set(params.map((p) => p.id));
			} else {
				sweepParamId = '';
				sensSelectedParams = new Set();
			}
		} else {
			loadError = modelResult.error ?? 'Failed to load model';
		}
		loading = false;
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
		{#if loading}
			<Loader2Icon class="size-3 animate-spin text-muted-foreground" />
		{/if}
		{#if params.length > 0}
			<span class="text-[10px] text-muted-foreground">{params.length} param{params.length === 1 ? '' : 's'}</span>
		{/if}
	</div>

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
	{:else if runs.length === 0 && !loading}
		<div class="flex flex-1 items-center justify-center">
			<p class="text-muted-foreground text-xs">No runs available. Run a model first.</p>
		</div>
	{:else}
		<div class="flex-1 overflow-auto p-3">
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

						<!-- Capture series chips -->
						<div class="flex items-center gap-1 flex-wrap text-xs">
							<span class="text-[10px] text-muted-foreground">Capture:</span>
							{#each COMMON_SERIES as s (s)}
								<button
									class="rounded px-1.5 py-0.5 text-[10px] {sweepCaptureChips.has(s)
										? 'bg-foreground text-background font-medium'
										: 'bg-muted text-muted-foreground hover:bg-accent'}"
									onclick={() => toggleSweepCapture(s)}
								>{s}</button>
							{/each}
							{#if sweepCaptureChips.size === 0}
								<span class="text-[10px] text-muted-foreground italic">(all)</span>
							{/if}
						</div>

						<!-- Values preview -->
						<div class="text-[10px] text-muted-foreground font-mono">
							{sweepValues.length} point{sweepValues.length === 1 ? '' : 's'}:
							{sweepValues.slice(0, 20).map(fmtNum).join(', ')}
							{#if sweepValues.length > 20}…{/if}
							{#if sweepOverLimit}
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
								<div class="flex items-center gap-1 mt-3">
									{#each COMMON_TARGETS as t (t)}
										<button
											class="rounded px-1.5 py-0.5 text-[10px] {sensTargetMetric === t
												? 'bg-foreground text-background font-medium'
												: 'bg-muted text-muted-foreground hover:bg-accent'}"
											onclick={() => (sensTargetMetric = t)}
										>{t}</button>
									{/each}
								</div>
							</div>

							<!-- Perturbation -->
							<div class="flex items-center gap-2 text-xs">
								<label class="flex items-center gap-2">
									<span class="text-[10px] text-muted-foreground">Perturbation:</span>
									<input
										type="range"
										min="0.01"
										max="0.30"
										step="0.01"
										bind:value={sensPerturbation}
										class="w-32"
									/>
									<span class="font-mono tabular-nums text-[10px] w-12">
										{(sensPerturbation * 100).toFixed(0)}%
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
							<div class="flex flex-col gap-2 border rounded p-2 bg-card">
								<div class="text-xs font-semibold">
									∂({sensResponse.metricSeriesId}) / ∂param — sorted by |gradient|
								</div>
								<SensitivityBarChart points={sensResponse.points} />
							</div>
						{/if}
					</div>
				{/if}

			{:else if activeTab === 'goal-seek' || activeTab === 'optimize'}
				<div class="flex items-center justify-center h-full">
					<p class="text-xs text-muted-foreground italic">
						{activeTab === 'goal-seek' ? 'Goal Seek' : 'Optimize'} surface — coming in m-E21-04.
					</p>
				</div>
			{/if}
		</div>
	{/if}
</div>
