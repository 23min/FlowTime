<script lang="ts">
	import { onMount } from 'svelte';
	import { flowtime, type Artifact } from '$lib/api/index.js';
	import * as Card from '$lib/components/ui/card/index.js';
	import { Button } from '$lib/components/ui/button/index.js';
	import RefreshCwIcon from '@lucide/svelte/icons/refresh-cw';
	import FileIcon from '@lucide/svelte/icons/file';
	import AlertCircleIcon from '@lucide/svelte/icons/alert-circle';

	let artifacts = $state<Artifact[]>([]);
	let total = $state(0);
	let loading = $state(true);
	let error = $state<string | undefined>();

	async function load() {
		loading = true;
		error = undefined;
		const result = await flowtime.listArtifacts({ limit: 100, sortBy: 'created', sortOrder: 'desc' });
		if (result.success && result.value) {
			artifacts = result.value.artifacts;
			total = result.value.total;
		} else {
			error = result.error ?? 'Failed to load artifacts';
		}
		loading = false;
	}

	onMount(load);

	function formatDate(iso: string) {
		return new Date(iso).toLocaleDateString('en-GB', {
			day: 'numeric',
			month: 'short',
			year: 'numeric',
			hour: '2-digit',
			minute: '2-digit'
		});
	}

	function formatSize(bytes: number) {
		if (bytes < 1024) return `${bytes} B`;
		if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
		return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
	}
</script>

<svelte:head>
	<title>Artifacts - FlowTime</title>
</svelte:head>

<div class="space-y-6">
	<div class="flex items-center justify-between">
		<div>
			<h1 class="text-2xl font-semibold">Run Artifacts</h1>
			{#if !loading && !error}
				<p class="text-muted-foreground text-sm mt-1">{total} artifact{total !== 1 ? 's' : ''}</p>
			{/if}
		</div>
		<Button variant="outline" size="sm" onclick={load} disabled={loading}>
			<RefreshCwIcon class="mr-2 size-4 {loading ? 'animate-spin' : ''}" />
			Refresh
		</Button>
	</div>

	{#if error}
		<Card.Root class="border-destructive">
			<Card.Content class="flex items-center gap-3 pt-6">
				<AlertCircleIcon class="text-destructive size-5" />
				<p class="text-sm">{error}</p>
			</Card.Content>
		</Card.Root>
	{:else if loading}
		<div class="grid gap-3">
			{#each Array(3) as _}
				<div class="bg-muted h-24 animate-pulse rounded-lg"></div>
			{/each}
		</div>
	{:else if artifacts.length === 0}
		<Card.Root>
			<Card.Content class="flex flex-col items-center gap-2 py-12">
				<FileIcon class="text-muted-foreground size-10" />
				<p class="text-muted-foreground">No artifacts found.</p>
				<p class="text-muted-foreground text-sm">Run a model to create artifacts.</p>
			</Card.Content>
		</Card.Root>
	{:else}
		<div class="grid gap-3">
			{#each artifacts as artifact}
				<a href="/time-travel/artifacts/{encodeURIComponent(artifact.id)}" class="block">
					<Card.Root class="transition-shadow duration-[var(--duration-micro)] hover:shadow-md">
						<Card.Content class="flex items-center justify-between pt-6">
							<div class="space-y-1">
								<p class="font-medium">{artifact.title}</p>
								<div class="text-muted-foreground flex gap-4 text-xs">
									<span>{formatDate(artifact.created)}</span>
									<span>{artifact.type}</span>
									<span>{formatSize(artifact.totalSize)}</span>
									<span>{artifact.files.length} file{artifact.files.length !== 1 ? 's' : ''}</span>
								</div>
							</div>
							{#if artifact.tags.length > 0}
								<div class="flex gap-1">
									{#each artifact.tags as tag}
										<span
											class="bg-secondary text-secondary-foreground rounded-md px-2 py-0.5 text-xs"
											>{tag}</span
										>
									{/each}
								</div>
							{/if}
						</Card.Content>
					</Card.Root>
				</a>
			{/each}
		</div>
	{/if}
</div>
