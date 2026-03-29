<script lang="ts">
	import { onMount } from 'svelte';
	import { flowtime, sim, type ServiceInfo } from '$lib/api/index.js';
	import * as Card from '$lib/components/ui/card/index.js';
	import { Button } from '$lib/components/ui/button/index.js';
	import RefreshCwIcon from '@lucide/svelte/icons/refresh-cw';

	type HealthState = {
		status: 'checking' | 'healthy' | 'error';
		info?: ServiceInfo;
		error?: string;
	};

	let api = $state<HealthState>({ status: 'checking' });
	let simApi = $state<HealthState>({ status: 'checking' });
	let refreshing = $state(false);

	async function checkHealth() {
		refreshing = true;
		api = { status: 'checking' };
		simApi = { status: 'checking' };

		const [apiResult, simResult] = await Promise.all([
			flowtime.healthDetailed(),
			sim.healthDetailed()
		]);

		api = apiResult.success
			? { status: 'healthy', info: apiResult.value }
			: { status: 'error', error: apiResult.error };

		simApi = simResult.success
			? { status: 'healthy', info: simResult.value }
			: { status: 'error', error: simResult.error };

		refreshing = false;
	}

	onMount(() => {
		checkHealth();
	});

	function statusColor(status: HealthState['status']) {
		switch (status) {
			case 'healthy':
				return 'bg-emerald-500';
			case 'error':
				return 'bg-red-500';
			default:
				return 'bg-muted-foreground animate-pulse';
		}
	}

	function statusLabel(status: HealthState['status']) {
		switch (status) {
			case 'healthy':
				return 'Healthy';
			case 'error':
				return 'Unreachable';
			default:
				return 'Checking...';
		}
	}
</script>

<svelte:head>
	<title>Health - FlowTime</title>
</svelte:head>

<div class="space-y-6">
	<div class="flex items-center justify-between">
		<h1 class="text-2xl font-semibold">Health Status</h1>
		<Button variant="outline" size="sm" onclick={checkHealth} disabled={refreshing}>
			<RefreshCwIcon class="mr-2 size-4 {refreshing ? 'animate-spin' : ''}" />
			Refresh
		</Button>
	</div>

	<div class="grid gap-4 sm:grid-cols-2">
		{#each [
			{ label: 'FlowTime API', port: 8080, state: api },
			{ label: 'Simulation API', port: 8090, state: simApi }
		] as service}
			<Card.Root>
				<Card.Header>
					<div class="flex items-center gap-3">
						<span class="size-3 rounded-full {statusColor(service.state.status)}"></span>
						<Card.Title>{service.label}</Card.Title>
					</div>
					<Card.Description>localhost:{service.port}</Card.Description>
				</Card.Header>
				<Card.Content>
					<div class="text-sm space-y-1">
						<div class="flex justify-between">
							<span class="text-muted-foreground">Status</span>
							<span class="font-medium">{statusLabel(service.state.status)}</span>
						</div>
						{#if service.state.info}
							<div class="flex justify-between">
								<span class="text-muted-foreground">Version</span>
								<span class="font-mono text-xs">{service.state.info.build.version}</span>
							</div>
							{#if service.state.info.build.commitHash}
								<div class="flex justify-between">
									<span class="text-muted-foreground">Commit</span>
									<span class="font-mono text-xs"
										>{service.state.info.build.commitHash.slice(0, 8)}</span
									>
								</div>
							{/if}
							<div class="flex justify-between">
								<span class="text-muted-foreground">Environment</span>
								<span>{service.state.info.build.environment}</span>
							</div>
						{/if}
						{#if service.state.error}
							<p class="text-destructive text-xs mt-2">{service.state.error}</p>
						{/if}
					</div>
				</Card.Content>
			</Card.Root>
		{/each}
	</div>
</div>
