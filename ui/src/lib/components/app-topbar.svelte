<script lang="ts">
	import { Separator } from '$lib/components/ui/separator/index.js';
	import { SidebarTrigger } from '$lib/components/ui/sidebar/index.js';
	import AppBreadcrumb from './app-breadcrumb.svelte';
	import ThemeToggle from './theme-toggle.svelte';
</script>

<header class="flex h-8 shrink-0 items-center gap-1.5 border-b px-2">
	<SidebarTrigger class="-ml-1" />
	<Separator orientation="vertical" class="mr-2 !h-4" />
	<AppBreadcrumb />
	<div class="ml-auto flex items-center gap-1">
		<ThemeToggle />
	</div>
</header>
