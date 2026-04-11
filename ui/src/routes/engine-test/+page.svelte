<script lang="ts">
	import { onDestroy } from 'svelte';
	import { EngineSession, type ParamInfo } from '$lib/api/engine-session.js';

	// Smoke test page for the engine session WebSocket bridge (m-E17-01).
	// Proves the end-to-end path: browser → .NET WebSocket proxy → Rust engine session.

	const SIMPLE_MODEL = `grid:
  bins: 4
  binSize: 1
  binUnit: hours
nodes:
  - id: arrivals
    kind: const
    values: [10, 10, 10, 10]
  - id: served
    kind: expr
    expr: "arrivals * 0.5"
`;

	// Derive WebSocket URL from API base (fall back to localhost:8081)
	const API_BASE = import.meta.env.VITE_API_BASE ?? 'http://localhost:8081';
	const WS_URL = API_BASE.replace(/^http/, 'ws') + '/v1/engine/session';

	let status = $state<'idle' | 'connecting' | 'connected' | 'error'>('idle');
	let error = $state<string | null>(null);
	let params = $state<ParamInfo[]>([]);
	let servedSeries = $state<number[]>([]);
	let arrivalsOverride = $state<number>(10);
	let elapsedUs = $state<number | null>(null);

	let session: EngineSession | null = null;

	async function connectAndCompile() {
		status = 'connecting';
		error = null;
		try {
			session = new EngineSession(WS_URL);
			const result = await session.compile(SIMPLE_MODEL);
			params = result.params;
			servedSeries = result.series.served ?? [];
			status = 'connected';
		} catch (e) {
			error = e instanceof Error ? e.message : String(e);
			status = 'error';
		}
	}

	async function evalWithOverride() {
		if (!session) return;
		try {
			const result = await session.eval({ arrivals: arrivalsOverride });
			servedSeries = result.series.served ?? [];
			elapsedUs = result.elapsed_us;
		} catch (e) {
			error = e instanceof Error ? e.message : String(e);
		}
	}

	onDestroy(() => {
		session?.close();
	});
</script>

<div class="mx-auto max-w-3xl space-y-6 p-8">
	<div>
		<h1 class="text-2xl font-bold">Engine Session Smoke Test</h1>
		<p class="text-muted-foreground mt-1 text-sm">
			Verifies the m-E17-01 WebSocket bridge end-to-end: browser → .NET proxy → Rust engine.
		</p>
	</div>

	<div class="rounded-lg border p-4">
		<div class="mb-2 text-sm font-medium">WebSocket URL</div>
		<code class="text-xs">{WS_URL}</code>
	</div>

	{#if status === 'idle'}
		<button
			class="rounded bg-blue-600 px-4 py-2 text-white hover:bg-blue-700"
			onclick={connectAndCompile}
		>
			Connect &amp; compile model
		</button>
	{:else if status === 'connecting'}
		<p class="text-sm">Connecting…</p>
	{:else if status === 'error'}
		<div class="rounded border border-red-300 bg-red-50 p-4">
			<div class="font-medium text-red-700">Error</div>
			<div class="text-sm text-red-600">{error}</div>
		</div>
		<button
			class="rounded bg-blue-600 px-4 py-2 text-white hover:bg-blue-700"
			onclick={connectAndCompile}
		>
			Retry
		</button>
	{:else}
		<div class="rounded-lg border p-4">
			<h2 class="mb-2 font-semibold">Parameters</h2>
			<table class="w-full text-sm">
				<thead>
					<tr class="border-b text-left">
						<th class="py-1 pr-4">ID</th>
						<th class="py-1 pr-4">Kind</th>
						<th class="py-1">Default</th>
					</tr>
				</thead>
				<tbody>
					{#each params as p}
						<tr class="border-b last:border-0">
							<td class="py-1 pr-4 font-mono">{p.id}</td>
							<td class="py-1 pr-4">{p.kind}</td>
							<td class="py-1 font-mono">{JSON.stringify(p.default)}</td>
						</tr>
					{/each}
				</tbody>
			</table>
		</div>

		<div class="rounded-lg border p-4">
			<h2 class="mb-2 font-semibold">Eval with override</h2>
			<label class="block text-sm">
				arrivals:
				<input
					type="number"
					bind:value={arrivalsOverride}
					class="ml-2 rounded border px-2 py-1"
					min="0"
					step="1"
				/>
			</label>
			<button
				class="mt-2 rounded bg-green-600 px-4 py-2 text-white hover:bg-green-700"
				onclick={evalWithOverride}
			>
				Eval
			</button>
			{#if elapsedUs !== null}
				<p class="text-muted-foreground mt-2 text-xs">
					Last eval: {elapsedUs} µs
				</p>
			{/if}
		</div>

		<div class="rounded-lg border p-4">
			<h2 class="mb-2 font-semibold">served series</h2>
			<div class="flex flex-wrap gap-2">
				{#each servedSeries as v, i}
					<div class="rounded bg-gray-100 px-3 py-1 font-mono text-sm">
						[{i}] = {v}
					</div>
				{/each}
			</div>
		</div>
	{/if}
</div>
