<script lang="ts">
	import { onMount } from 'svelte';
	import {
		sim,
		type TemplateSummary,
		type TemplateDetail,
		type RunCreateResponse,
		type BundleReuseMode
	} from '$lib/api/index.js';
	import TemplateCard from '$lib/components/template-card.svelte';
	import RunConfigPanel from '$lib/components/run-config-panel.svelte';
	import RunResult from '$lib/components/run-result.svelte';
	import DryRunPlan from '$lib/components/dry-run-plan.svelte';
	import * as Card from '$lib/components/ui/card/index.js';
	import { Button } from '$lib/components/ui/button/index.js';
	import { Input } from '$lib/components/ui/input/index.js';
	import AlertCircleIcon from '@lucide/svelte/icons/alert-circle';
	import RefreshCwIcon from '@lucide/svelte/icons/refresh-cw';
	import SearchIcon from '@lucide/svelte/icons/search';
	import LoaderIcon from '@lucide/svelte/icons/loader';

	type PagePhase = 'selecting' | 'configuring' | 'running' | 'success' | 'error' | 'preview';

	let templates = $state<TemplateSummary[]>([]);
	let selectedTemplateId = $state<string | undefined>();
	let selectedTemplate = $state<TemplateDetail | undefined>();
	let searchQuery = $state('');
	let phase = $state<PagePhase>('selecting');
	let loading = $state(true);
	let error = $state<string | undefined>();
	let runResult = $state<RunCreateResponse | undefined>();
	let busy = $state(false);

	const filteredTemplates = $derived(
		templates.filter((t) => {
			if (!searchQuery) return true;
			const q = searchQuery.toLowerCase();
			return t.title.toLowerCase().includes(q) || t.description.toLowerCase().includes(q);
		})
	);

	onMount(() => {
		loadTemplates();
	});

	async function loadTemplates() {
		loading = true;
		error = undefined;
		const result = await sim.listTemplates();
		if (result.success && result.value) {
			templates = result.value;
		} else {
			error = result.error ?? 'Failed to load templates';
		}
		loading = false;
	}

	async function selectTemplate(id: string) {
		if (selectedTemplateId === id && phase === 'configuring') {
			selectedTemplateId = undefined;
			selectedTemplate = undefined;
			phase = 'selecting';
			return;
		}

		selectedTemplateId = id;
		selectedTemplate = undefined;

		const result = await sim.getTemplate(id);
		if (result.success && result.value) {
			selectedTemplate = result.value;
			phase = 'configuring';
		} else {
			error = result.error ?? 'Failed to load template details';
			phase = 'error';
		}
	}

	interface RunConfig {
		reuseMode: BundleReuseMode;
		rngSeed: number;
		parameters: Record<string, unknown>;
	}

	function buildRequest(config: RunConfig, dryRun: boolean) {
		const template = selectedTemplate!;
		// Simulation mode always works. Telemetry mode requires capture CSVs on disk
		// which may not exist yet — default to simulation for reliability.
		const mode = 'simulation';

		return {
			templateId: template.id,
			mode,
			parameters:
				Object.keys(config.parameters).length > 0 ? config.parameters : undefined,
			rng: { kind: 'pcg32', seed: config.rngSeed },
			options: {
				dryRun,
				deterministicRunId: config.reuseMode !== 'fresh',
				overwriteExisting: config.reuseMode === 'regenerate'
			}
		};
	}

	async function executeRun(config: RunConfig) {
		busy = true;
		phase = 'running';
		error = undefined;

		const request = buildRequest(config, false);
		const result = await sim.createRun(request);

		busy = false;
		if (result.success && result.value) {
			runResult = result.value;
			phase = 'success';
		} else {
			error = result.error ?? 'Run failed';
			phase = 'error';
		}
	}

	async function previewRun(config: RunConfig) {
		busy = true;
		phase = 'running';
		error = undefined;

		const request = buildRequest(config, true);
		const result = await sim.createRun(request);

		busy = false;
		if (result.success && result.value) {
			runResult = result.value;
			phase = 'preview';
		} else {
			error = result.error ?? 'Preview failed';
			phase = 'error';
		}
	}

	function backToSelection() {
		phase = selectedTemplate ? 'configuring' : 'selecting';
		runResult = undefined;
		error = undefined;
	}
</script>

<svelte:head>
	<title>Run Model - FlowTime</title>
</svelte:head>

<div class="space-y-6">
	<div class="flex items-center justify-between">
		<h1 class="text-2xl font-semibold">Run Model</h1>
		{#if !loading && templates.length > 0}
			<Button variant="ghost" size="icon" onclick={loadTemplates}>
				<RefreshCwIcon class="size-4" />
			</Button>
		{/if}
	</div>

	{#if error && phase === 'error'}
		<Card.Root class="border-destructive">
			<Card.Content class="flex items-center gap-3 pt-6">
				<AlertCircleIcon class="text-destructive size-5 shrink-0" />
				<p class="text-sm">{error}</p>
				<Button variant="outline" size="sm" class="ml-auto shrink-0" onclick={backToSelection}>
					Back
				</Button>
			</Card.Content>
		</Card.Root>
	{/if}

	{#if loading}
		<div class="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
			{#each { length: 6 } as _}
				<div class="bg-muted h-40 animate-pulse rounded-lg"></div>
			{/each}
		</div>
	{:else if templates.length === 0 && !error}
		<Card.Root>
			<Card.Content class="flex flex-col items-center gap-2 py-12">
				<p class="text-muted-foreground">No templates available.</p>
				<p class="text-muted-foreground text-sm">Make sure the Sim API is running.</p>
			</Card.Content>
		</Card.Root>
	{:else if phase === 'selecting' || phase === 'configuring'}
		<div class="relative">
			<SearchIcon
				class="text-muted-foreground absolute top-1/2 left-3 size-4 -translate-y-1/2"
			/>
			<Input
				type="text"
				placeholder="Search templates..."
				class="pl-10"
				bind:value={searchQuery}
			/>
		</div>

		{#if filteredTemplates.length === 0}
			<p class="text-muted-foreground py-8 text-center text-sm">
				No templates match "{searchQuery}"
			</p>
		{:else}
			<div class="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
				{#each filteredTemplates as template (template.id)}
					<TemplateCard
						{template}
						selected={selectedTemplateId === template.id}
						onclick={() => selectTemplate(template.id)}
					/>
				{/each}
			</div>
		{/if}

		{#if selectedTemplate && phase === 'configuring'}
			<RunConfigPanel
				template={selectedTemplate}
				{busy}
				onrun={executeRun}
				onpreview={previewRun}
			/>
		{/if}
	{:else if phase === 'running'}
		<Card.Root>
			<Card.Content class="flex flex-col items-center gap-3 py-12">
				<LoaderIcon class="text-muted-foreground size-8 animate-spin" />
				<p class="text-muted-foreground text-sm">Running model...</p>
			</Card.Content>
		</Card.Root>
	{:else if phase === 'success' && runResult && !runResult.isDryRun}
		<RunResult
			result={runResult}
			templateTitle={selectedTemplate?.title ?? ''}
			onback={backToSelection}
		/>
	{:else if phase === 'preview' && runResult?.isDryRun && runResult.plan}
		<DryRunPlan plan={runResult.plan} onback={backToSelection} />
	{/if}
</div>
