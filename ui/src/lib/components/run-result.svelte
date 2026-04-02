<script lang="ts">
	import type { RunCreateResponse } from '$lib/api/types.js';
	import * as Card from '$lib/components/ui/card/index.js';
	import * as Alert from '$lib/components/ui/alert/index.js';
	import { Badge } from '$lib/components/ui/badge/index.js';
	import { Button } from '$lib/components/ui/button/index.js';
	import CheckCircleIcon from '@lucide/svelte/icons/check-circle';
	import AlertTriangleIcon from '@lucide/svelte/icons/alert-triangle';
	import NetworkIcon from '@lucide/svelte/icons/network';
	import ArchiveIcon from '@lucide/svelte/icons/archive';
	import ArrowLeftIcon from '@lucide/svelte/icons/arrow-left';

	interface Props {
		result: RunCreateResponse;
		templateTitle: string;
		onback: () => void;
	}

	let { result, templateTitle, onback }: Props = $props();

	const runId = $derived(result.metadata?.runId ?? 'unknown');
	const mode = $derived(result.metadata?.mode ?? 'unknown');
</script>

<Card.Root>
	<Card.Header>
		<div class="flex items-center gap-3">
			<CheckCircleIcon class="size-5 text-emerald-500" />
			<Card.Title>Run Complete</Card.Title>
			{#if result.wasReused}
				<Badge variant="secondary" class="text-xs">Reused</Badge>
			{/if}
		</div>
	</Card.Header>

	<Card.Content class="space-y-4">
		<div class="grid grid-cols-2 gap-4 text-sm">
			<div>
				<span class="text-muted-foreground">Run ID</span>
				<p class="font-mono text-xs">{runId}</p>
			</div>
			<div>
				<span class="text-muted-foreground">Template</span>
				<p>{templateTitle}</p>
			</div>
			<div>
				<span class="text-muted-foreground">Mode</span>
				<p>
					<Badge variant="outline" class="text-xs">{mode}</Badge>
				</p>
			</div>
			{#if result.metadata?.rng}
				<div>
					<span class="text-muted-foreground">RNG Seed</span>
					<p class="font-mono">{result.metadata.rng.seed}</p>
				</div>
			{/if}
		</div>

		{#if result.warnings.length > 0}
			<Alert.Root variant="destructive">
				<AlertTriangleIcon class="size-4" />
				<Alert.Title>Warnings ({result.warnings.length})</Alert.Title>
				<Alert.Description>
					<ul class="mt-1 list-inside list-disc text-xs">
						{#each result.warnings as warning}
							<li>{warning.message}</li>
						{/each}
					</ul>
				</Alert.Description>
			</Alert.Root>
		{/if}
	</Card.Content>

	<Card.Footer class="flex gap-3">
		<Button variant="outline" size="sm" onclick={onback}>
			<ArrowLeftIcon class="mr-2 size-4" />
			Back
		</Button>
		<a href="/time-travel/topology">
			<Button variant="default" size="sm">
				<NetworkIcon class="mr-2 size-4" />
				Open Topology
			</Button>
		</a>
		<a href="/time-travel/artifacts">
			<Button variant="outline" size="sm">
				<ArchiveIcon class="mr-2 size-4" />
				View Artifacts
			</Button>
		</a>
	</Card.Footer>
</Card.Root>
