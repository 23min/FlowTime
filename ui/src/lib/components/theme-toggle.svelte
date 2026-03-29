<script lang="ts">
	import SunIcon from '@lucide/svelte/icons/sun';
	import MoonIcon from '@lucide/svelte/icons/moon';
	import MonitorIcon from '@lucide/svelte/icons/monitor';
	import { Button } from '$lib/components/ui/button/index.js';
	import * as DropdownMenu from '$lib/components/ui/dropdown-menu/index.js';
	import { theme, type ThemeMode } from '$lib/stores/theme.svelte.js';

	const items: { mode: ThemeMode; label: string; icon: typeof SunIcon }[] = [
		{ mode: 'light', label: 'Light', icon: SunIcon },
		{ mode: 'dark', label: 'Dark', icon: MoonIcon },
		{ mode: 'system', label: 'System', icon: MonitorIcon }
	];
</script>

<DropdownMenu.Root>
	<DropdownMenu.Trigger>
		{#snippet child({ props })}
			<Button variant="ghost" size="icon" {...props}>
				{#if theme.resolved === 'light'}
					<SunIcon class="size-4" />
				{:else}
					<MoonIcon class="size-4" />
				{/if}
				<span class="sr-only">Toggle theme</span>
			</Button>
		{/snippet}
	</DropdownMenu.Trigger>
	<DropdownMenu.Content align="end">
		{#each items as item}
			<DropdownMenu.Item onclick={() => theme.set(item.mode)}>
				<item.icon class="mr-2 size-4" />
				{item.label}
			</DropdownMenu.Item>
		{/each}
	</DropdownMenu.Content>
</DropdownMenu.Root>
