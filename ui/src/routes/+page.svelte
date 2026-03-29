<script lang="ts">
	import { onMount } from 'svelte';
	import { flowtime, sim } from '$lib/api/index.js';
	import * as Card from '$lib/components/ui/card/index.js';
	import NetworkIcon from '@lucide/svelte/icons/network';
	import BarChart3Icon from '@lucide/svelte/icons/bar-chart-3';
	import ArchiveIcon from '@lucide/svelte/icons/archive';
	import PlayIcon from '@lucide/svelte/icons/play';

	let apiOk = $state<boolean | null>(null);
	let simOk = $state<boolean | null>(null);
	let runCount = $state<number | null>(null);

	onMount(async () => {
		const [apiRes, simRes, runsRes] = await Promise.all([
			flowtime.health(),
			sim.health(),
			flowtime.listRuns(1, 1)
		]);
		apiOk = apiRes.success;
		simOk = simRes.success;
		if (runsRes.success && runsRes.value) runCount = runsRes.value.totalCount;
	});

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
			icon: ArchiveIcon,
			badge: runCount
		},
		{
			title: 'Run Model',
			description: 'Execute a model and view results.',
			href: '/run',
			icon: PlayIcon
		}
	];

	function statusDot(ok: boolean | null) {
		if (ok === null) return 'bg-muted-foreground animate-pulse';
		return ok ? 'bg-emerald-500' : 'bg-red-500';
	}
</script>

<svelte:head>
	<title>Home - FlowTime</title>
</svelte:head>

<div class="space-y-6">
	<div class="flex items-start justify-between">
		<div>
			<h1 class="text-3xl font-bold tracking-tight">FlowTime</h1>
			<p class="text-muted-foreground mt-1">Discrete-event process simulation and analysis.</p>
		</div>
		<div class="flex items-center gap-4 text-sm">
			<span class="flex items-center gap-2">
				<span class="size-2 rounded-full {statusDot(apiOk)}"></span>
				API
			</span>
			<span class="flex items-center gap-2">
				<span class="size-2 rounded-full {statusDot(simOk)}"></span>
				Sim
			</span>
		</div>
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
							<div class="flex items-center gap-2">
								<Card.Title>{section.title}</Card.Title>
								{#if section.badge !== undefined && section.badge !== null}
									<span class="bg-primary text-primary-foreground rounded-full px-2 py-0.5 text-xs font-medium">
										{section.badge}
									</span>
								{/if}
							</div>
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
