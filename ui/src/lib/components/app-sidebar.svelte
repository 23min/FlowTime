<script lang="ts">
	import { page } from '$app/stores';
	import NetworkIcon from '@lucide/svelte/icons/network';
	import BarChart3Icon from '@lucide/svelte/icons/bar-chart-3';
	import ArchiveIcon from '@lucide/svelte/icons/archive';
	import PlayIcon from '@lucide/svelte/icons/play';
	import HeartPulseIcon from '@lucide/svelte/icons/heart-pulse';
	import HomeIcon from '@lucide/svelte/icons/home';
	import SlidersHorizontalIcon from '@lucide/svelte/icons/sliders-horizontal';
	import * as Sidebar from '$lib/components/ui/sidebar/index.js';

	const navGroups = [
		{
			label: 'Time Travel',
			items: [
				{ label: 'Topology', href: '/time-travel/topology', icon: NetworkIcon },
				{ label: 'Dashboard', href: '/time-travel/dashboard', icon: BarChart3Icon },
				{ label: 'Artifacts', href: '/time-travel/artifacts', icon: ArchiveIcon }
			]
		},
		{
			label: 'Tools',
			items: [
				{ label: 'Run Model', href: '/run', icon: PlayIcon },
				{ label: 'What-If', href: '/what-if', icon: SlidersHorizontalIcon },
				{ label: 'Health', href: '/health', icon: HeartPulseIcon }
			]
		}
	];
</script>

<Sidebar.Root collapsible="icon">
	<Sidebar.Header>
		<Sidebar.Menu>
			<Sidebar.MenuItem>
				<Sidebar.MenuButton size="lg" tooltipContent="Home">
					{#snippet child({ props })}
						<a href="/" {...props}>
							<div
								class="bg-primary text-primary-foreground flex aspect-square size-8 items-center justify-center rounded-lg text-xs font-bold"
							>
								FT
							</div>
							<div class="flex flex-col gap-0.5 leading-none">
								<span class="font-semibold">FlowTime</span>
								<span class="text-xs text-muted-foreground">Process Mining</span>
							</div>
						</a>
					{/snippet}
				</Sidebar.MenuButton>
			</Sidebar.MenuItem>
		</Sidebar.Menu>
	</Sidebar.Header>
	<Sidebar.Content>
		{#each navGroups as group}
			<Sidebar.Group>
				<Sidebar.GroupLabel>{group.label}</Sidebar.GroupLabel>
				<Sidebar.GroupContent>
					<Sidebar.Menu>
						{#each group.items as item}
							<Sidebar.MenuItem>
								<Sidebar.MenuButton
									isActive={$page.url.pathname.startsWith(item.href)}
									tooltipContent={item.label}
								>
									{#snippet child({ props })}
										<a href={item.href} {...props}>
											<item.icon class="size-4" />
											<span>{item.label}</span>
										</a>
									{/snippet}
								</Sidebar.MenuButton>
							</Sidebar.MenuItem>
						{/each}
					</Sidebar.Menu>
				</Sidebar.GroupContent>
			</Sidebar.Group>
		{/each}
	</Sidebar.Content>
	<Sidebar.Rail />
</Sidebar.Root>
