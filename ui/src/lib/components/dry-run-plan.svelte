<script lang="ts">
	import type { RunCreatePlan } from '$lib/api/types.js';
	import * as Card from '$lib/components/ui/card/index.js';
	import * as Alert from '$lib/components/ui/alert/index.js';
	import { Badge } from '$lib/components/ui/badge/index.js';
	import { Button } from '$lib/components/ui/button/index.js';
	import { Separator } from '$lib/components/ui/separator/index.js';
	import FileIcon from '@lucide/svelte/icons/file';
	import AlertTriangleIcon from '@lucide/svelte/icons/alert-triangle';
	import EyeIcon from '@lucide/svelte/icons/eye';
	import ArrowLeftIcon from '@lucide/svelte/icons/arrow-left';

	interface Props {
		plan: RunCreatePlan;
		onback: () => void;
	}

	let { plan, onback }: Props = $props();
</script>

<Card.Root>
	<Card.Header>
		<div class="flex items-center gap-3">
			<EyeIcon class="text-muted-foreground size-5" />
			<Card.Title>Dry Run Preview</Card.Title>
			<Badge variant="outline" class="text-xs">{plan.mode}</Badge>
		</div>
	</Card.Header>

	<Card.Content class="space-y-4">
		<div class="grid grid-cols-2 gap-4 text-sm">
			<div>
				<span class="text-muted-foreground">Template</span>
				<p>{plan.templateId}</p>
			</div>
			<div>
				<span class="text-muted-foreground">Output</span>
				<p class="truncate font-mono text-xs">{plan.outputRoot}</p>
			</div>
		</div>

		{#if Object.keys(plan.parameters).length > 0}
			<Separator />
			<div>
				<h4 class="mb-2 text-sm font-medium">Parameters</h4>
				<div class="bg-muted rounded-md p-3">
					<pre class="text-xs">{JSON.stringify(plan.parameters, null, 2)}</pre>
				</div>
			</div>
		{/if}

		{#if plan.files.length > 0}
			<Separator />
			<div>
				<h4 class="mb-2 text-sm font-medium">
					Files ({plan.files.length})
				</h4>
				<div class="space-y-1">
					{#each plan.files as file}
						<div class="flex items-center gap-2 text-xs">
							<FileIcon class="text-muted-foreground size-3" />
							<span class="text-muted-foreground">{file.nodeId}</span>
							<span class="font-mono">{file.metric}</span>
						</div>
					{/each}
				</div>
			</div>
		{/if}

		{#if plan.warnings.length > 0}
			<Alert.Root variant="destructive">
				<AlertTriangleIcon class="size-4" />
				<Alert.Title>Plan Warnings ({plan.warnings.length})</Alert.Title>
				<Alert.Description>
					<ul class="mt-1 list-inside list-disc text-xs">
						{#each plan.warnings as warning}
							<li>{warning.message}</li>
						{/each}
					</ul>
				</Alert.Description>
			</Alert.Root>
		{/if}
	</Card.Content>

	<Card.Footer>
		<Button variant="outline" size="sm" onclick={onback}>
			<ArrowLeftIcon class="mr-2 size-4" />
			Back
		</Button>
	</Card.Footer>
</Card.Root>
