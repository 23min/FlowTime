<script lang="ts">
	import '../app.css';
	import { SidebarProvider, SidebarInset } from '$lib/components/ui/sidebar/index.js';
	import AppSidebar from '$lib/components/app-sidebar.svelte';
	import AppTopbar from '$lib/components/app-topbar.svelte';
	import { theme } from '$lib/stores/theme.svelte.js';

	theme.init();

	let { children } = $props();
</script>

<svelte:head>
	<title>FlowTime</title>
</svelte:head>

<SidebarProvider>
	<AppSidebar />
	<SidebarInset>
		<AppTopbar />
		<main class="flex-1 overflow-auto">
			{@render children()}
		</main>
	</SidebarInset>
</SidebarProvider>
