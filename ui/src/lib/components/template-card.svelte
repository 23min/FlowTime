<script lang="ts">
	import type { TemplateSummary } from '$lib/api/types.js';
	import { getDomainIcon } from './domain-icon.js';
	import * as Card from '$lib/components/ui/card/index.js';
	import { Badge } from '$lib/components/ui/badge/index.js';

	interface Props {
		template: TemplateSummary;
		selected?: boolean;
		onclick: () => void;
	}

	let { template, selected = false, onclick }: Props = $props();

	const Icon = $derived(getDomainIcon(template));
</script>

<button class="w-full text-left" {onclick}>
	<Card.Root
		class="transition-shadow duration-150 hover:shadow-md {selected
			? 'ring-primary ring-2'
			: ''}"
	>
		<Card.Header>
			<div class="flex items-center gap-3">
				<div class="bg-muted flex size-10 shrink-0 items-center justify-center rounded-lg">
					<Icon class="text-muted-foreground size-5" />
				</div>
				<div class="min-w-0 flex-1">
					<div class="flex items-center gap-2">
						<Card.Title class="truncate">{template.title}</Card.Title>
						<Badge variant="secondary" class="shrink-0 text-xs">v{template.version}</Badge>
					</div>
				</div>
			</div>
		</Card.Header>
		<Card.Content>
			<p class="text-muted-foreground line-clamp-2 text-sm">{template.description}</p>
			{#if template.tags.length > 0}
				<div class="mt-2 flex flex-wrap gap-1">
					{#each template.tags.slice(0, 4) as tag}
						<Badge variant="outline" class="text-xs">{tag}</Badge>
					{/each}
				</div>
			{/if}
		</Card.Content>
	</Card.Root>
</button>
