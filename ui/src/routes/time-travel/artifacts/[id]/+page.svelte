<script lang="ts">
	import { page } from '$app/stores';
</script>

<svelte:head>
	<title>Artifact {$page.params.id} - FlowTime</title>
</svelte:head>

<div class="space-y-4">
	<h1 class="text-2xl font-semibold">Artifact: {$page.params.id}</h1>
	<p class="text-muted-foreground">Run metadata and file viewer. Coming in M2.</p>
</div>
