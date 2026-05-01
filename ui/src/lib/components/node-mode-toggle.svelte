<!--
  Node-mode toggle — m-E21-06 AC15.

  Segmented-control toggle `[ Operational | Full ]` that drives the shared
  view-state store's `nodeMode` field, which in turn drives the `mode` query
  parameter on `GET /v1/runs/{runId}/state_window` for both topology and
  heatmap. Persists in `localStorage` as `ft.view.nodeMode` (handled by the
  store, not here).

  Parity target: Blazor UI's pre-existing "operational nodes" toggle. Two
  states only; chrome-colored like the other toolbar controls.
-->

<script lang="ts">
	import type { NodeMode } from '$lib/stores/view-state.svelte.js';

	interface Props {
		mode: NodeMode;
		onChange: (mode: NodeMode) => void;
	}

	let { mode, onChange }: Props = $props();
</script>

<div class="inline-flex items-center rounded border border-input bg-background" role="group" aria-label="Node mode">
	<button
		type="button"
		class="px-1.5 py-0.5 text-[10px] transition-colors {mode === 'operational'
			? 'bg-foreground text-background font-medium'
			: 'text-muted-foreground hover:text-foreground'}"
		aria-pressed={mode === 'operational'}
		data-node-mode="operational"
		onclick={() => {
			if (mode !== 'operational') onChange('operational');
		}}
	>
		Operational
	</button>
	<button
		type="button"
		class="px-1.5 py-0.5 text-[10px] transition-colors {mode === 'full'
			? 'bg-foreground text-background font-medium'
			: 'text-muted-foreground hover:text-foreground'}"
		aria-pressed={mode === 'full'}
		data-node-mode="full"
		onclick={() => {
			if (mode !== 'full') onChange('full');
		}}
	>
		Full
	</button>
</div>
