<script lang="ts">
	import * as Card from '$lib/components/ui/card/index.js';
	import NetworkIcon from '@lucide/svelte/icons/network';
	import BarChart3Icon from '@lucide/svelte/icons/bar-chart-3';
	import ArchiveIcon from '@lucide/svelte/icons/archive';
	import PlayIcon from '@lucide/svelte/icons/play';

	const sections = [
		{
			title: 'Topology',
			description: 'Explore process topology with time-travel visualization.',
			href: '/time-travel/topology',
			icon: NetworkIcon
		},
		{
			title: 'Dashboard',
			description: 'SLA metrics and node-level performance.',
			href: '/time-travel/dashboard',
			icon: BarChart3Icon
		},
		{
			title: 'Artifacts',
			description: 'Browse simulation runs and results.',
			href: '/time-travel/artifacts',
			icon: ArchiveIcon
		},
		{
			title: 'Run Model',
			description: 'Execute a model and view results.',
			href: '/run',
			icon: PlayIcon
		}
	];
</script>

<svelte:head>
	<title>Home - FlowTime</title>
</svelte:head>

<div class="space-y-6">
	<div>
		<h1 class="text-3xl font-bold tracking-tight">FlowTime</h1>
		<p class="text-muted-foreground mt-1">Discrete-event process simulation and analysis.</p>
	</div>
	<div class="grid gap-4 sm:grid-cols-2">
		{#each sections as section}
			<a href={section.href} class="block">
				<Card.Root
					class="transition-shadow duration-[var(--duration-micro)] hover:shadow-md"
				>
					<Card.Header>
						<div class="flex items-center gap-3">
							<div
								class="bg-muted flex size-10 items-center justify-center rounded-lg"
							>
								<section.icon class="text-muted-foreground size-5" />
							</div>
							<Card.Title>{section.title}</Card.Title>
						</div>
					</Card.Header>
					<Card.Content>
						<p class="text-muted-foreground text-sm">{section.description}</p>
					</Card.Content>
				</Card.Root>
			</a>
		{/each}
	</div>
</div>
