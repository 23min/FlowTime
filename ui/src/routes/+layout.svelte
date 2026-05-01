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
	<!-- `min-w-0` on SidebarInset is load-bearing: it's a flex-row item inside
	     SidebarProvider and its default intrinsic min-width = its content's min-width.
	     Without this, wide content (e.g. a 288-bin heatmap at default cell size) pushes
	     SidebarInset past the viewport, causing the ENTIRE page (topbar, breadcrumb,
	     scrubber, canvas) to scroll horizontally as a unit. See also `<main min-w-0>`
	     below which chains the fix down to the topology route. -->
	<SidebarInset class="min-w-0">
		<AppTopbar />
		<!-- `min-w-0` is load-bearing: as a flex item inside SidebarInset, <main>
		     without it sizes to its content's intrinsic min-width, letting wide
		     children (e.g. a 288-bin heatmap at default cell size) push the whole
		     page past the viewport and defeat both the overflow-auto here and
		     per-page fit-to-width logic. -->
		<main class="flex-1 overflow-auto min-w-0">
			{@render children()}
		</main>
	</SidebarInset>
</SidebarProvider>
