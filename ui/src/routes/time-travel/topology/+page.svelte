<script lang="ts">
	import { onMount } from 'svelte';
	import { flowtime, type RunSummary, type GraphResponse } from '$lib/api/index.js';
	import DagMapView from '$lib/components/dag-map-view.svelte';
	import * as Card from '$lib/components/ui/card/index.js';
	import AlertCircleIcon from '@lucide/svelte/icons/alert-circle';

	let runs = $state<RunSummary[]>([]);
	let selectedRunId = $state<string | undefined>();
	let graph = $state<GraphResponse | undefined>();
	let loading = $state(false);
	let error = $state<string | undefined>();

	onMount(async () => {
		const result = await flowtime.listRuns(1, 50);
		if (result.success && result.value) {
			runs = result.value.items;
			if (runs.length > 0) {
				selectRun(runs[0].runId);
			}
		}
	});

	async function selectRun(runId: string) {
		selectedRunId = runId;
		loading = true;
		error = undefined;
		graph = undefined;

		const graphResult = await flowtime.getGraph(runId);
		if (graphResult.success && graphResult.value) {
			graph = graphResult.value;
		} else {
			error = graphResult.error ?? 'Failed to load graph';
		}
		loading = false;
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
			<DagMapView {graph} />
		</div>
	{:else if runs.length === 0}
		<Card.Root>
			<Card.Content class="flex flex-col items-center gap-2 py-12">
				<p class="text-muted-foreground">No runs available. Run a model first.</p>
			</Card.Content>
		</Card.Root>
	{/if}
</div>
