<script lang="ts">
	import { page } from '$app/stores';
	import NetworkIcon from '@lucide/svelte/icons/network';
	import BarChart3Icon from '@lucide/svelte/icons/bar-chart-3';
	import ArchiveIcon from '@lucide/svelte/icons/archive';
	import PlayIcon from '@lucide/svelte/icons/play';
	import SlidersHorizontalIcon from '@lucide/svelte/icons/sliders-horizontal';
	import FlaskConicalIcon from '@lucide/svelte/icons/flask-conical';
	import HeartPulseIcon from '@lucide/svelte/icons/heart-pulse';
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
				{ label: 'Analysis', href: '/analysis', icon: FlaskConicalIcon },
				{ label: 'Health', href: '/health', icon: HeartPulseIcon }
			]
		}
	];
</script>

<Sidebar.Root collapsible="icon">
	<Sidebar.Header class="py-1.5 px-2">
		<Sidebar.Menu>
			<Sidebar.MenuItem>
				<Sidebar.MenuButton size="sm" tooltipContent="Home">
					{#snippet child({ props })}
						<a href="/" {...props}>
							<div
								class="bg-primary text-primary-foreground flex aspect-square size-6 items-center justify-center rounded text-[10px] font-bold"
							>
								FT
							</div>
							<div class="flex flex-col leading-none">
								<span class="text-xs font-semibold">FlowTime</span>
							</div>
						</a>
					{/snippet}
				</Sidebar.MenuButton>
			</Sidebar.MenuItem>
		</Sidebar.Menu>
	</Sidebar.Header>
	<Sidebar.Content>
		{#each navGroups as group}
			<Sidebar.Group class="py-1">
				<Sidebar.GroupLabel class="text-[10px] uppercase tracking-wider px-2 py-0.5">{group.label}</Sidebar.GroupLabel>
				<Sidebar.GroupContent>
					<Sidebar.Menu>
						{#each group.items as item}
							<Sidebar.MenuItem>
								<Sidebar.MenuButton
									isActive={$page.url.pathname.startsWith(item.href)}
									tooltipContent={item.label}
								>
									{#snippet child({ props })}
										<a href={item.href} {...props} class="flex items-center gap-2 px-2 py-1 text-xs">
											<item.icon class="size-3.5" />
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
