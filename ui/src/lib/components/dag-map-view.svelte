<script lang="ts">
	import { dagMap } from 'dag-map';
	import type { GraphResponse } from '$lib/api/types.js';
	import { theme as appTheme } from '$lib/stores/theme.svelte.js';

	interface Props {
		graph: GraphResponse;
	}

	let { graph }: Props = $props();

	// FlowTime edges use port-qualified IDs like "NodeA:out" → "NodeB:in"
	function stripPort(id: string): string {
		const idx = id.indexOf(':');
		return idx >= 0 ? id.substring(0, idx) : id;
	}

	const darkTheme = {
		paper: 'transparent',
		ink: 'hsl(0 0% 98%)',
		muted: 'hsl(240 5% 64.9%)',
		border: 'hsl(240 3.7% 15.9%)',
		classes: {
			core: '#94E2D5',
			data: '#89B4FA',
			warn: '#F38BA8',
			infra: '#F9E2AF',
			pure: '#A6E3A1'
		}
	};

	const lightTheme = {
		paper: 'transparent',
		ink: 'hsl(240 10% 3.9%)',
		muted: 'hsl(240 3.8% 46.1%)',
		border: 'hsl(240 5.9% 90%)',
		classes: {
			core: '#2B8A8E',
			data: '#3D5BA9',
			warn: '#C45B4A',
			infra: '#D4944C',
			pure: '#4A8C5C'
		}
	};

	const svg = $derived.by(() => {
		// Read resolved theme to make this reactive
		const isDark = appTheme.resolved === 'dark';
		const dagTheme = isDark ? darkTheme : lightTheme;

		const nodeIds = new Set(graph.nodes.map((n) => n.id));
		const edgeSet = new Set<string>();
		const edges: [string, string][] = [];
		for (const e of graph.edges) {
			const from = stripPort(e.from);
			const to = stripPort(e.to);
			if (!nodeIds.has(from) || !nodeIds.has(to)) continue;
			const key = `${from}->${to}`;
			if (!edgeSet.has(key)) {
				edgeSet.add(key);
				edges.push([from, to]);
			}
		}

		const dag = {
			nodes: graph.nodes.map((n) => ({
				id: n.id,
				label: n.id,
				cls: 'core'
			})),
			edges
		};

		const result = dagMap(dag, {
			theme: dagTheme,
			routing: 'bezier',
			showLegend: false,
			title: ' ',
			subtitle: null,
			scale: 1.5
		});

		return result.svg;
	});
</script>

<div class="dag-map-container overflow-auto p-4">
	{@html svg}
</div>

<style>
	.dag-map-container :global(svg) {
		max-width: 100%;
		height: auto;
	}
</style>
