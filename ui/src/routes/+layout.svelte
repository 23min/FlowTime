<script lang="ts">
	import '../app.css';
	import { SidebarProvider, SidebarInset } from '$lib/components/ui/sidebar/index.js';
	import AppSidebar from '$lib/components/app-sidebar.svelte';
	import AppTopbar from '$lib/components/app-topbar.svelte';

	let { children } = $props();
</script>

<svelte:head>
	<title>FlowTime</title>
</svelte:head>

<SidebarProvider>
	<AppSidebar />
	<SidebarInset>
		<AppTopbar />
		<main class="flex-1 p-6">
			{@render children()}
		</main>
	</SidebarInset>
</SidebarProvider>
