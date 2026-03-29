<script lang="ts">
	import { onMount } from 'svelte';
	import { page } from '$app/stores';
	import { flowtime, type Artifact } from '$lib/api/index.js';
	import * as Card from '$lib/components/ui/card/index.js';
	import { Button } from '$lib/components/ui/button/index.js';
	import { Separator } from '$lib/components/ui/separator/index.js';
	import ArrowLeftIcon from '@lucide/svelte/icons/arrow-left';
	import FileTextIcon from '@lucide/svelte/icons/file-text';
	import AlertCircleIcon from '@lucide/svelte/icons/alert-circle';

	let artifact = $state<Artifact | undefined>();
	let loading = $state(true);
	let error = $state<string | undefined>();

	let selectedFile = $state<string | undefined>();
	let fileContent = $state<string | undefined>();
	let fileLoading = $state(false);

	async function loadArtifact() {
		loading = true;
		const id = decodeURIComponent($page.params.id);
		const result = await flowtime.getArtifact(id);
		if (result.success && result.value) {
			artifact = result.value;
		} else {
			error = result.error ?? 'Artifact not found';
		}
		loading = false;
	}

	async function loadFile(fileName: string) {
		if (!artifact) return;
		selectedFile = fileName;
		fileLoading = true;
		fileContent = undefined;
		const result = await flowtime.getArtifactFile(artifact.id, fileName);
		if (result.success) {
			fileContent = result.value;
		} else {
			fileContent = `Error loading file: ${result.error}`;
		}
		fileLoading = false;
	}

	onMount(loadArtifact);

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
	<title>{artifact?.title ?? 'Artifact'} - FlowTime</title>
</svelte:head>

<div class="space-y-6">
	<a href="/time-travel/artifacts" class="text-muted-foreground hover:text-foreground inline-flex items-center gap-1 text-sm transition-colors">
		<ArrowLeftIcon class="size-4" />
		Back to artifacts
	</a>

	{#if error}
		<Card.Root class="border-destructive">
			<Card.Content class="flex items-center gap-3 pt-6">
				<AlertCircleIcon class="text-destructive size-5" />
				<p class="text-sm">{error}</p>
			</Card.Content>
		</Card.Root>
	{:else if loading}
		<div class="space-y-4">
			<div class="bg-muted h-8 w-64 animate-pulse rounded"></div>
			<div class="bg-muted h-32 animate-pulse rounded-lg"></div>
		</div>
	{:else if artifact}
		<div>
			<h1 class="text-2xl font-semibold">{artifact.title}</h1>
			<div class="text-muted-foreground mt-1 flex gap-4 text-sm">
				<span>{artifact.type}</span>
				<span>{formatDate(artifact.created)}</span>
				<span>{formatSize(artifact.totalSize)}</span>
			</div>
		</div>

		{#if Object.keys(artifact.metadata).length > 0}
			<Card.Root>
				<Card.Header>
					<Card.Title class="text-base">Metadata</Card.Title>
				</Card.Header>
				<Card.Content>
					<dl class="grid grid-cols-[auto_1fr] gap-x-6 gap-y-1 text-sm">
						{#each Object.entries(artifact.metadata) as [key, value]}
							<dt class="text-muted-foreground">{key}</dt>
							<dd class="font-mono text-xs">{typeof value === 'object' ? JSON.stringify(value) : String(value)}</dd>
						{/each}
					</dl>
				</Card.Content>
			</Card.Root>
		{/if}

		<Card.Root>
			<Card.Header>
				<Card.Title class="text-base">Files ({artifact.files.length})</Card.Title>
			</Card.Header>
			<Card.Content>
				<div class="flex gap-4">
					<div class="w-48 shrink-0 space-y-0.5">
						{#each artifact.files as fileName}
							<button
								class="text-sm w-full text-left rounded px-2 py-1 transition-colors {selectedFile === fileName ? 'bg-accent text-accent-foreground' : 'hover:bg-muted'}"
								onclick={() => loadFile(fileName)}
							>
								<span class="flex items-center gap-2">
									<FileTextIcon class="size-3.5 shrink-0" />
									<span class="truncate">{fileName}</span>
								</span>
							</button>
						{/each}
					</div>
					<Separator orientation="vertical" class="!h-auto" />
					<div class="flex-1 min-w-0">
						{#if fileLoading}
							<div class="bg-muted h-48 animate-pulse rounded"></div>
						{:else if fileContent !== undefined}
							<pre class="bg-muted overflow-auto rounded-lg p-4 text-xs max-h-[60vh]"><code>{fileContent}</code></pre>
						{:else}
							<p class="text-muted-foreground text-sm py-8 text-center">Select a file to view its contents.</p>
						{/if}
					</div>
				</div>
			</Card.Content>
		</Card.Root>
	{/if}
</div>
