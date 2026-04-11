<script lang="ts">
	import { onDestroy } from 'svelte';
	import {
		EngineSession,
		type ParamInfo,
		type CompileResult,
	} from '$lib/api/engine-session.js';
	import { paramControlConfig, kindLabel } from '$lib/api/param-controls.js';
	import { EXAMPLE_MODELS, type ExampleModel } from '$lib/api/example-models.js';
	import { createDebouncer } from '$lib/utils/debounce.js';
	import { formatValue, formatSeries, isInternalSeries } from '$lib/utils/format.js';
	import Sparkline from '$lib/components/sparkline.svelte';
	import AlertCircleIcon from '@lucide/svelte/icons/alert-circle';
	import RefreshCwIcon from '@lucide/svelte/icons/refresh-cw';
	import Loader2Icon from '@lucide/svelte/icons/loader-2';

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

	async function runEval(ov: Record<string, number>) {
		if (!session || compileStatus !== 'ready') return;
		evalInFlight = true;
		error = null;
		try {
			const result = await session.eval(ov);
			series = result.series;
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
			<div class="rounded-lg border p-4" data-testid="series-panel">
				<div class="mb-3 flex items-center justify-between">
					<div class="text-sm font-semibold">Series</div>
					{#if evalInFlight}
						<div class="text-muted-foreground flex items-center gap-1 text-xs" data-testid="eval-spinner">
							<Loader2Icon class="size-3 animate-spin" />
							evaluating…
						</div>
					{/if}
				</div>

				<div class="space-y-3">
					{#each Object.entries(series).filter(([name]) => !isInternalSeries(name)) as [name, values] (name)}
						<div class="rounded border p-3" data-testid="series-row-{name}">
							<div class="mb-2 flex items-center justify-between gap-2">
								<div class="font-mono text-sm font-medium">{name}</div>
								<Sparkline {values} width={140} height={28} stroke="#2563eb" />
							</div>
							<div class="text-muted-foreground font-mono text-xs" data-testid="series-values-{name}">
								{formatSeries(values)}
							</div>
						</div>
					{/each}
				</div>
			</div>
		</div>
	{/if}
</div>
