<script lang="ts">
	import { onMount } from 'svelte';
	import { flip } from 'svelte/animate';
	import { fade } from 'svelte/transition';
	import { flowtime, type RunSummary, type GraphResponse, type RunIndex } from '$lib/api/index.js';
	import DagMapView from '$lib/components/dag-map-view.svelte';
	import HeatmapView from '$lib/components/heatmap-view.svelte';
	import ViewSwitcher from '$lib/components/view-switcher.svelte';
	import NodeModeToggle from '$lib/components/node-mode-toggle.svelte';
	import WorkbenchCard from '$lib/components/workbench-card.svelte';
	import WorkbenchEdgeCard from '$lib/components/workbench-edge-card.svelte';
	import MetricSelector from '$lib/components/metric-selector.svelte';
	import TimelineScrubber from '$lib/components/timeline-scrubber.svelte';
	import { workbench } from '$lib/stores/workbench.svelte.js';
	import { viewState } from '$lib/stores/view-state.svelte.js';
	import { validation } from '$lib/stores/validation.svelte.js';
	import ValidationPanel from '$lib/components/validation-panel.svelte';
	import {
		extractNodeMetrics,
		extractEdgeMetrics,
		findHighestUtilizationNode,
	} from '$lib/utils/workbench-metrics.js';
	import {
		buildSparklineSeries,
		buildNormalizedMetricMap,
		computeMetricDomainFromWindow,
		discoverClasses,
		type MetricDef,
	} from '$lib/utils/metric-defs.js';
	import { sortHeatmapRows, type SortMode, type HeatmapRowInput } from '$lib/utils/heatmap-sort.js';
	import { buildEdgeSelector, escapeAttributeValue } from '$lib/utils/topology-selectors.js';
	import {
		nodeIndicatorPosition,
		parseSvgNumber,
		triangleIndicatorPoints,
	} from '$lib/utils/topology-indicators.js';
	import { pickWarningSeverity, severityChromeToken } from '$lib/utils/validation-helpers.js';
	import { buildNodeAriaLabel, buildEdgeAriaLabel } from '$lib/utils/topology-a11y.js';
	import { Skeleton } from '$lib/components/ui/skeleton/index.js';
	import { bindEvents } from 'dag-map';
	import AlertCircleIcon from '@lucide/svelte/icons/alert-circle';

	interface NodeMetric {
		value: number;
		label?: string;
	}

	function formatRunOption(run: RunSummary): string {
		const title = run.templateTitle ?? run.templateId ?? run.runId;
		const tail = run.runId.includes('_') ? run.runId.slice(run.runId.lastIndexOf('_') + 1) : run.runId;
		const ts = run.createdUtc ? new Date(run.createdUtc) : null;
		const time = ts && !isNaN(ts.getTime())
			? `${String(ts.getMonth() + 1).padStart(2, '0')}-${String(ts.getDate()).padStart(2, '0')} ${String(ts.getHours()).padStart(2, '0')}:${String(ts.getMinutes()).padStart(2, '0')}`
			: '';
		const warn = run.warningCount > 0 ? ` ⚠ ${run.warningCount}` : '';
		return time ? `${title} · ${time} · ${tail}${warn}` : `${title} · ${tail}${warn}`;
	}

	let runs = $state<RunSummary[]>([]);
	let selectedRunId = $state<string | undefined>();
	let graph = $state<GraphResponse | undefined>();
	let runIndex = $state<RunIndex | undefined>();
	let loading = $state(false);
	// m-E21-08 AC5 — true while flowtime.getStateWindow(...) is in flight (after
	// the run-load phase resolves). Drives the canvas + workbench skeleton so
	// the surface no longer flickers from empty → populated mid-fetch.
	let loadingWindow = $state(false);
	let error = $state<string | undefined>();

	// Snapshot state (for node/edge cards at current bin)
	let stateNodes = $state<Record<string, unknown>[]>([]);
	let stateEdges = $state<Record<string, unknown>[]>([]);

	// Window state (for sparklines, heatmap, and shared color domain — loaded once per run/mode)
	let windowNodes = $state<Record<string, unknown>[]>([]);
	let windowTimestamps = $state<string[] | undefined>();

	// Graph node kinds (for card display)
	let nodeKinds = $state<Map<string, string>>(new Map());

	// Class filter
	let availableClasses = $state<string[]>([]);

	// Splitter state
	let splitRatio = $state(65);
	let dragging = $state(false);
	let containerEl: HTMLDivElement | undefined = $state();

	// dag-map event cleanup
	let dagContainer: HTMLDivElement | undefined = $state();
	let cleanupEvents: (() => void) | undefined;

	// Playback state (route-local — the scrubber position itself lives in viewState)
	let playInterval: ReturnType<typeof setInterval> | undefined;

	const views = [
		{ id: 'topology', label: 'Topology', shortcut: 'Alt+1' },
		{ id: 'heatmap', label: 'Heatmap', shortcut: 'Alt+2' },
	];

	// ---- derived metric state ------------------------------------------------------
	// Shared color-scale domain over the full window (m-E21-06 AC5 + ADR-02). Both
	// topology and heatmap color from this.
	const sharedDomain = $derived.by(() => {
		if (windowNodes.length === 0) return null;
		return computeMetricDomainFromWindow(
			windowNodes,
			workbench.selectedMetric,
			viewState.activeClasses,
		);
	});

	const currentMetrics = $derived.by<Map<string, NodeMetric> | undefined>(() => {
		if (stateNodes.length === 0) return undefined;
		return buildNormalizedMetricMap(
			stateNodes,
			workbench.selectedMetric,
			viewState.activeClasses,
			sharedDomain,
		);
	});

	const sparklineMap = $derived.by<Map<string, number[]>>(() => {
		if (windowNodes.length === 0) return new Map();
		return buildSparklineSeries(windowNodes, workbench.selectedMetric, viewState.activeClasses);
	});

	// Bind dag-map click events whenever the SVG re-renders
	$effect(() => {
		if (dagContainer) {
			if (cleanupEvents) cleanupEvents();
			cleanupEvents = bindEvents(dagContainer, {
				onNodeClick: (nodeId: string) => {
					const kind = nodeKinds.get(nodeId);
					const wasPinned = workbench.isPinned(nodeId);
					workbench.toggle(nodeId, kind);
					if (!wasPinned) {
						// Click pinned a new node → mark it as the selected card to match
						// heatmap semantics (clicking a cell pins + selects). `currentBin`
						// is the natural bin anchor since topology is a single-bin view.
						viewState.setSelectedCell(nodeId, viewState.currentBin);
					} else if (viewState.selectedCell?.nodeId === nodeId) {
						// Click unpinned the previously-selected node → drop the marker so
						// no card shows stale selection chrome.
						viewState.clearSelectedCell();
					}
				},
				onEdgeClick: (from: string, to: string) => {
					// m-E21-08 AC3 — pin AND select. Match the node-click symmetry:
					// clicking an edge pins it to the workbench, sets it as the selected
					// edge for the chrome cross-link, and toggles off both states when
					// clicking an already-selected pinned edge.
					const key = `${from}→${to}`;
					const wasPinned = workbench.selectedEdgeKeys.has(key);
					workbench.toggleEdge(from, to);
					if (!wasPinned) {
						viewState.setSelectedEdge(from, to);
					} else if (
						viewState.selectedEdge?.from === from &&
						viewState.selectedEdge?.to === to
					) {
						viewState.clearSelectedEdge();
					}
				},
			});
		}
		return () => {
			if (cleanupEvents) {
				cleanupEvents();
				cleanupEvents = undefined;
			}
		};
	});

	// Apply edge-pinned highlighting via CSS after each render. Drives the
	// amber stroke on every pinned edge. m-E21-08 AC3 renamed this from
	// .edge-selected → .edge-pinned so selection can have its own single-edge
	// chrome (see the .edge-selected effect below).
	$effect(() => {
		void workbench.selectedEdgeKeys; // track dependency
		if (!dagContainer) return;
		dagContainer.querySelectorAll('.edge-pinned').forEach((el) => {
			el.classList.remove('edge-pinned');
		});
		for (const edge of workbench.pinnedEdges) {
			const selector = buildEdgeSelector(edge);
			try {
				dagContainer.querySelectorAll(selector).forEach((el) => {
					el.classList.add('edge-pinned');
				});
			} catch (err) {
				console.warn('topology: edge-pinned selector failed', { edge, err });
			}
		}
	});

	// m-E21-08 AC3 — single-edge selection chrome. Driven by viewState.selectedEdge,
	// independent of pinned state. An edge can be both pinned (amber stroke) and
	// selected (turquoise stroke at heavier weight); .edge-selected is defined
	// after .edge-pinned in the <style> block so the selection treatment wins by
	// source order.
	$effect(() => {
		void viewState.selectedEdge;
		void currentMetrics;
		void selectedIds;
		void viewState.activeView;
		if (!dagContainer) return;
		if (viewState.activeView !== 'topology') return;

		dagContainer.querySelectorAll('.edge-selected').forEach((el) => {
			el.classList.remove('edge-selected');
		});

		const sel = viewState.selectedEdge;
		if (!sel) return;
		const selector = buildEdgeSelector(sel);
		try {
			dagContainer.querySelectorAll(selector).forEach((el) => {
				el.classList.add('edge-selected');
			});
		} catch (err) {
			console.warn('topology: edge-selected selector failed', { sel, err });
		}
	});

	// m-E21-08 AC2 — topology .node-selected stroke rule. Closes the m-E21-06
	// asymmetric one-way cross-link for nodes: when viewState.selectedCell
	// names a node (set by topology-click, card-click, heatmap-cell-click,
	// or validation-row-click), the matching dag-map node group renders a
	// turquoise --ft-highlight stroke. Mirrors the .edge-selected effect
	// pattern above; cleanup-then-apply, attribute-escape on the selector.
	//
	// Re-run triggers:
	//   - viewState.selectedCell changes (different node selected, or cleared)
	//   - currentMetrics / selectedIds — dag-map re-renders the SVG; class lost
	//   - viewState.activeView — skip on heatmap (SVG unmounted)
	//   - dagContainer mounts
	$effect(() => {
		void viewState.selectedCell?.nodeId;
		void currentMetrics;
		void selectedIds;
		void viewState.activeView;
		if (!dagContainer) return;
		if (viewState.activeView !== 'topology') return;

		dagContainer.querySelectorAll('.node-selected').forEach((el) => {
			el.classList.remove('node-selected');
		});

		const sel = viewState.selectedCell?.nodeId;
		if (!sel) return;
		try {
			const group = dagContainer.querySelector(
				`[data-node-id="${escapeAttributeValue(sel)}"]`,
			);
			group?.classList.add('node-selected');
		} catch (err) {
			console.warn('topology: node-selected selector failed', { sel, err });
		}
	});

	// Apply m-E21-07 AC7 / AC8 warning indicators after each topology render.
	// Reads from `validation.nodeSeverityById` / `validation.edgeSeverityById`
	// (single source of truth per AC11) and injects sibling SVG circles into
	// the dag-map-rendered SVG.
	//
	// Re-run triggers:
	//   - validation maps change (different run → different warnings)
	//   - currentMetrics change (dag-map re-renders the SVG for new metric values)
	//   - selectedIds change (dag-map re-renders for selection-ring redraw)
	//   - dagContainer mounts
	//
	// Cleanup-then-apply pattern matches the edge-selected effect above; the
	// SVG_NS guard keeps appended circles in the SVG namespace so they
	// inherit the surrounding `<svg>` coordinate system.
	$effect(() => {
		void validation.nodeSeverityById; // track dependency
		void validation.edgeSeverityById; // track dependency
		void currentMetrics; // SVG re-renders on metric change — re-apply
		void selectedIds; // SVG re-renders on selection change — re-apply
		void viewState.activeView; // dag-map remounts when switching topology ↔ heatmap
		if (!dagContainer) return;
		// When the active view is heatmap, the dag-map SVG is unmounted —
		// querySelectorAll just no-ops below, but we may as well skip cleanly.
		if (viewState.activeView !== 'topology') return;

		// Cleanup any indicators from a prior pass — the effect owns them
		// exclusively (data-warning-indicator attribute is the seam).
		dagContainer
			.querySelectorAll('[data-warning-indicator]')
			.forEach((el) => el.remove());

		const SVG_NS = 'http://www.w3.org/2000/svg';

		// --- Node indicators (AC7) ----------------------------------------------
		for (const [nodeId, severity] of Object.entries(validation.nodeSeverityById)) {
			const token = severityChromeToken(severity);
			if (token === null) continue; // unknown severity → no chrome treatment
			const groupSelector = `[data-node-id="${escapeAttributeValue(nodeId)}"]`;
			let group: Element | null;
			try {
				group = dagContainer.querySelector(groupSelector);
			} catch (err) {
				console.warn('topology: node indicator selector failed', { nodeId, err });
				continue;
			}
			if (!group) continue;
			// The dag-map render emits an inner `<circle data-id="...">` carrying
			// cx/cy/r — the canonical node geometry. Grab it directly so the
			// indicator follows whatever the layout chose for this node (depth /
			// interchange / scale variants all live on this circle).
			const innerCircle = group.querySelector(
				`circle[data-id="${escapeAttributeValue(nodeId)}"]`,
			);
			if (!innerCircle) continue;
			const cx = parseSvgNumber(innerCircle.getAttribute('cx'));
			const cy = parseSvgNumber(innerCircle.getAttribute('cy'));
			const r = parseSvgNumber(innerCircle.getAttribute('r'));
			if (cx === null || cy === null || r === null) continue;
			const dot = nodeIndicatorPosition({ cx, cy, r });
			// Position (NE shoulder) stays proportional via the helper, but the
			// dot radius is fixed at 3 user-space units so the indicator reads
			// at the same size as the workbench-card severity dot
			// (Tailwind `size-1.5` = 6 px diameter).
			const el = document.createElementNS(SVG_NS, 'circle');
			el.setAttribute('cx', dot.cx.toFixed(2));
			el.setAttribute('cy', dot.cy.toFixed(2));
			el.setAttribute('r', '3');
			el.setAttribute('fill', `var(${token})`);
			el.setAttribute('stroke', 'var(--background)');
			el.setAttribute('stroke-width', '0.5');
			el.setAttribute('pointer-events', 'none');
			el.setAttribute('data-warning-indicator', 'node');
			el.setAttribute('data-warning-node-id', nodeId);
			el.setAttribute('data-warning-severity', severity);
			group.appendChild(el);
		}

		// --- Edge indicators (AC8) ----------------------------------------------
		// edgeWarnings keys arrive pre-translated to the workbench `from→to`
		// convention via `validation.setResponse(value, graph.edges)` in
		// loadWindow(). Raw analyser ids that don't match a graph edge fall
		// through unmapped and silently skip the arrow-split below — same
		// graceful fallback as before, just rare in practice (smoke-test fix
		// 2026-04-27).
		const ARROW = '→'; // → (matches dag-map's edgeIndex key format)
		for (const [edgeId, severity] of Object.entries(validation.edgeSeverityById)) {
			const token = severityChromeToken(severity);
			if (token === null) continue;
			const idx = edgeId.indexOf(ARROW);
			if (idx <= 0 || idx === edgeId.length - 1) continue;
			const from = edgeId.slice(0, idx);
			const to = edgeId.slice(idx + 1);
			const selector = buildEdgeSelector({ from, to });
			let path: SVGPathElement | null;
			try {
				path = dagContainer.querySelector(selector) as SVGPathElement | null;
			} catch (err) {
				console.warn('topology: edge indicator selector failed', { edgeId, err });
				continue;
			}
			if (!path) continue;
			// getTotalLength / getPointAtLength are only available on
			// SVGGeometryElement — feature-detect rather than assume.
			if (
				typeof path.getTotalLength !== 'function' ||
				typeof path.getPointAtLength !== 'function'
			) {
				continue;
			}
			const totalLength = path.getTotalLength();
			if (!Number.isFinite(totalLength) || totalLength <= 0) continue;
			const mid = path.getPointAtLength(totalLength / 2);
			// Enlarge from the helper's default r=3 dot — the warning triangle
			// reads better at a slightly larger size against the dag-map's
			// edge stroke.
			const triangle = { cx: mid.x, cy: mid.y, r: 6 };
			const el = document.createElementNS(SVG_NS, 'polygon');
			el.setAttribute('points', triangleIndicatorPoints(triangle));
			el.setAttribute('fill', `var(${token})`);
			el.setAttribute('stroke', 'var(--background)');
			el.setAttribute('stroke-width', '1');
			el.setAttribute('stroke-linejoin', 'round');
			el.setAttribute('pointer-events', 'none');
			el.setAttribute('data-warning-indicator', 'edge');
			el.setAttribute('data-warning-edge-id', edgeId);
			el.setAttribute('data-warning-severity', severity);
			// Append to the path's parent <svg> so the triangle is on top and
			// not constrained by the path element itself (paths cannot have
			// child SVG nodes).
			const svgRoot = path.ownerSVGElement;
			if (svgRoot) svgRoot.appendChild(el);
		}
	});

	// m-E21-08 AC1 — topology keyboard + ARIA retrofit.
	//
	// Walk the dag-map-rendered SVG after each render and apply tabindex /
	// role / aria-label to nodes and edges so the topology surface reaches
	// the heatmap's a11y bar (Tab + arrow + Enter; screen-reader structure;
	// visible focus ring).
	//
	// Re-run triggers mirror the warning-indicator effect above:
	//   - currentMetrics — value text in node aria-labels updates per metric
	//   - selectedIds — dag-map re-renders the SVG; attributes lost
	//   - viewState.activeView — skip when not topology
	//   - dagContainer — mount
	$effect(() => {
		void currentMetrics;
		void selectedIds;
		void viewState.activeView;
		if (!dagContainer) return;
		if (viewState.activeView !== 'topology') return;

		const metricLabel = workbench.selectedMetric.label;

		// --- Nodes ---------------------------------------------------------------
		dagContainer.querySelectorAll('[data-node-id]').forEach((el) => {
			const group = el as SVGGElement;
			const nodeId = group.getAttribute('data-node-id');
			if (!nodeId) return;
			const className = group.getAttribute('data-node-cls');
			const metric = currentMetrics?.get(nodeId);
			group.setAttribute('tabindex', '0');
			group.setAttribute('role', 'button');
			group.setAttribute(
				'aria-label',
				buildNodeAriaLabel({
					nodeId,
					className,
					metricLabel,
					metricValue: metric?.value,
				}),
			);
		});

		// --- Edges (visible layer only) -----------------------------------------
		// data-edge-hit="true" elements are the invisible hit-area layer; skip
		// them so the user has one focusable target per edge, not two.
		dagContainer.querySelectorAll('[data-edge-from]:not([data-edge-hit])').forEach((el) => {
			const path = el as SVGPathElement;
			const from = path.getAttribute('data-edge-from');
			const to = path.getAttribute('data-edge-to');
			if (!from || !to) return;
			path.setAttribute('tabindex', '0');
			path.setAttribute('role', 'button');
			path.setAttribute('aria-label', buildEdgeAriaLabel({ from, to }));
		});
	});

	// m-E21-08 AC1 — keyboard activation handler (Enter / Space) for nodes
	// and edges. Mirrors the click handlers wired via `bindEvents` so the
	// keyboard contract matches the mouse contract exactly.
	function onTopologyKeydown(e: KeyboardEvent) {
		if (e.key !== 'Enter' && e.key !== ' ') return;
		const target = e.target as Element | null;
		if (!target) return;

		const nodeGroup = target.closest('[data-node-id]') as Element | null;
		if (nodeGroup) {
			const nodeId = nodeGroup.getAttribute('data-node-id');
			if (!nodeId) return;
			e.preventDefault();
			const kind = nodeKinds.get(nodeId);
			const wasPinned = workbench.isPinned(nodeId);
			workbench.toggle(nodeId, kind);
			if (!wasPinned) {
				viewState.setSelectedCell(nodeId, viewState.currentBin);
			} else if (viewState.selectedCell?.nodeId === nodeId) {
				viewState.clearSelectedCell();
			}
			return;
		}

		const edgePath = target.closest('[data-edge-from]:not([data-edge-hit])') as Element | null;
		if (edgePath) {
			const from = edgePath.getAttribute('data-edge-from');
			const to = edgePath.getAttribute('data-edge-to');
			if (!from || !to) return;
			e.preventDefault();
			// Same pin + select symmetry as the mouse handler (AC3).
			const key = `${from}→${to}`;
			const wasPinned = workbench.selectedEdgeKeys.has(key);
			workbench.toggleEdge(from, to);
			if (!wasPinned) {
				viewState.setSelectedEdge(from, to);
			} else if (
				viewState.selectedEdge?.from === from &&
				viewState.selectedEdge?.to === to
			) {
				viewState.clearSelectedEdge();
			}
		}
	}

	const selectedIds = $derived(workbench.selectedIds);
	const hasPinnedItems = $derived(workbench.pinned.length > 0 || workbench.pinnedEdges.length > 0);

	onMount(() => {
		const stored = localStorage.getItem('ft.topology.split');
		if (stored) {
			const v = parseFloat(stored);
			if (isFinite(v) && v >= 20 && v <= 80) splitRatio = v;
		}

		(async () => {
			const result = await flowtime.listRuns(1, 50);
			if (result.success && result.value) {
				runs = result.value.items;
				if (runs.length > 0) {
					selectRun(runs[0].runId);
				}
			}
		})();
		return () => {
			if (playInterval) clearInterval(playInterval);
		};
	});

	async function selectRun(runId: string) {
		selectedRunId = runId;
		loading = true;
		error = undefined;
		graph = undefined;
		runIndex = undefined;
		stateNodes = [];
		stateEdges = [];
		windowNodes = [];
		windowTimestamps = undefined;
		viewState.setCurrentBin(0);
		viewState.setBinCount(0);
		availableClasses = [];
		viewState.clearClasses();
		stopPlayback();
		workbench.clear();
		validation.setResponse(null);

		const [graphResult] = await Promise.all([flowtime.getGraph(runId), flowtime.getRun(runId)]);

		if (graphResult.success && graphResult.value) {
			graph = graphResult.value;
			const kinds = new Map<string, string>();
			for (const node of graphResult.value.nodes) {
				if (node.kind) kinds.set(node.id, node.kind);
			}
			nodeKinds = kinds;
		} else {
			error = graphResult.error ?? 'Failed to load graph';
			loading = false;
			return;
		}

		const indexResult = await flowtime.getRunIndex(runId);
		if (indexResult.success && indexResult.value) {
			runIndex = indexResult.value;
			viewState.setBinCount(indexResult.value.grid?.bins ?? 0);
		}

		loading = false;

		if (viewState.binCount > 0) {
			await loadWindow();
			await loadBin(0);

			const classes = discoverClasses(stateNodes);
			availableClasses = classes;

			const best = findHighestUtilizationNode(stateNodes);
			if (best) {
				workbench.pin(best.id, best.kind);
			}
		}
	}

	async function loadWindow() {
		if (!selectedRunId || viewState.binCount <= 0) return;
		loadingWindow = true;
		const windowResult = await flowtime.getStateWindow(
			selectedRunId,
			0,
			viewState.binCount - 1,
			viewState.nodeMode,
		);
		if (windowResult.success && windowResult.value) {
			windowNodes = windowResult.value.nodes as Record<string, unknown>[];
			windowTimestamps = windowResult.value.timestampsUtc;
			// Push the same response into the validation store (AC11 single source
			// of truth). The store derives the row list + per-node + per-edge
			// severity-max maps from `warnings[]` and `edgeWarnings`; the panel and
			// the topology AC7 / AC8 indicators (next chunk) read from it.
			//
			// `graph?.edges` is passed so the store can translate raw `edgeWarnings`
			// keys (analyser ids like `source_to_target`) into the workbench
			// `${from}→${to}` convention used by the panel, topology indicators,
			// and the workbench-edge-card lookup. By the time loadWindow() runs,
			// selectRun() has already awaited the graph fetch so `graph` is set
			// (smoke-test fix 2026-04-27 — bug surfaced when real-bytes lag run had
			// edge warnings but indicators silently skipped due to key mismatch).
			validation.setResponse(windowResult.value, graph?.edges);
		}
		loadingWindow = false;
	}

	async function loadBin(bin: number) {
		if (!selectedRunId || bin < 0 || (viewState.binCount > 0 && bin >= viewState.binCount)) return;
		viewState.setCurrentBin(bin);

		const stateResult = await flowtime.getState(selectedRunId, bin);
		if (stateResult.success && stateResult.value) {
			stateNodes = stateResult.value.nodes as Record<string, unknown>[];
			stateEdges = stateResult.value.edges as Record<string, unknown>[];
		}
	}

	function getNodeState(nodeId: string): Record<string, unknown> | undefined {
		return stateNodes.find((n) => (n.id as string) === nodeId);
	}

	function getEdgeState(from: string, to: string): Record<string, unknown> | undefined {
		return stateEdges.find((e) => {
			const eFrom = (e.from as string) ?? (e.sourceId as string);
			const eTo = (e.to as string) ?? (e.targetId as string);
			return eFrom === from && eTo === to;
		});
	}

	function onMetricSelect(def: MetricDef) {
		workbench.selectedMetric = def;
	}

	function toggleClass(cls: string) {
		viewState.toggleClass(cls);
	}

	function prevBin() {
		if (viewState.currentBin > 0) loadBin(viewState.currentBin - 1);
	}
	function nextBin() {
		if (viewState.currentBin < viewState.binCount - 1) loadBin(viewState.currentBin + 1);
	}

	function togglePlayback() {
		if (viewState.playing) {
			stopPlayback();
		} else {
			viewState.setPlaying(true);
			playInterval = setInterval(() => {
				let next = viewState.currentBin + 1;
				if (next >= viewState.binCount) next = 0;
				loadBin(next);
			}, 500);
		}
	}

	function stopPlayback() {
		viewState.setPlaying(false);
		if (playInterval) {
			clearInterval(playInterval);
			playInterval = undefined;
		}
	}

	function startDrag(e: MouseEvent) {
		dragging = true;
		e.preventDefault();
	}

	function onDrag(e: MouseEvent) {
		if (!dragging || !containerEl) return;
		const rect = containerEl.getBoundingClientRect();
		const pct = ((e.clientY - rect.top) / rect.height) * 100;
		splitRatio = Math.max(20, Math.min(80, pct));
	}

	function stopDrag() {
		if (dragging) {
			dragging = false;
			localStorage.setItem('ft.topology.split', String(splitRatio));
		}
	}

	// Heatmap side-effects --------------------------------------------------------
	function onHeatmapPinAndScrub(nodeId: string, bin: number) {
		stopPlayback();
		const kind = nodeKinds.get(nodeId);
		if (!workbench.isPinned(nodeId)) {
			workbench.pin(nodeId, kind);
		}
		if (bin !== viewState.currentBin) {
			loadBin(bin);
		}
	}

	function onHeatmapUnpin(nodeId: string) {
		unpinAndClearSelection(nodeId);
	}

	// Unpin a node AND clear the persistent selection marker if it pointed at that
	// node. Used by every unpin surface (heatmap pin glyph, topology click-to-toggle,
	// workbench card close button) so selection stays consistent — a stale highlight
	// on a no-longer-pinned card would be worse than no highlight at all.
	function unpinAndClearSelection(nodeId: string) {
		workbench.unpin(nodeId);
		if (viewState.selectedCell?.nodeId === nodeId) {
			viewState.clearSelectedCell();
		}
	}

	// m-E21-08 AC3 — symmetric helper for edges. Same contract as the node
	// helper: when the user closes an edge card and that edge was the
	// currently-selected edge, drop the selection so chrome stays consistent.
	function unpinEdgeAndClearSelection(from: string, to: string) {
		workbench.unpinEdge(from, to);
		if (
			viewState.selectedEdge?.from === from &&
			viewState.selectedEdge?.to === to
		) {
			viewState.clearSelectedEdge();
		}
	}

	// Node-mode toggle re-fetches the window response (the server-side filter by
	// node kind lives in /v1/runs/{runId}/state_window). When the user flips
	// operational ↔ full, we do NOT touch the scrubber position or class filter —
	// both views simply re-render against the fresh response.
	async function onNodeModeChange(mode: 'operational' | 'full') {
		if (mode === viewState.nodeMode) return;
		viewState.setNodeMode(mode);
		if (selectedRunId && viewState.binCount > 0) {
			await loadWindow();
		}
	}

	const SORT_MODES: SortMode[] = ['topological', 'id', 'max', 'mean', 'variance'];

	const heatmapGraphEdges = $derived.by(() => {
		if (!graph) return [] as { from: string; to: string }[];
		return graph.edges.map((e) => ({ from: e.from, to: e.to }));
	});

	// Pinned node cards follow the active sort (topological / id / max / mean /
	// variance) in BOTH heatmap and topology views — sort is a graph-wide preference,
	// not heatmap-specific. The sort dropdown only surfaces in the heatmap toolbar,
	// but its effect carries over: pick "topological" in heatmap, switch to topology,
	// cards stay in topological order. Edge cards are unaffected (no sort equivalent).
	const sortedPinnedNodes = $derived.by(() => {
		if (workbench.pinned.length <= 1) return workbench.pinned;
		const rowInputs: HeatmapRowInput[] = workbench.pinned.map((pin) => ({
			id: pin.id,
			series: sparklineMap.get(pin.id) ?? [],
		}));
		const sorted = sortHeatmapRows(rowInputs, {
			mode: viewState.sortMode,
			edges: heatmapGraphEdges,
		});
		const byId = new Map(workbench.pinned.map((p) => [p.id, p]));
		return sorted.map((r) => byId.get(r.id)).filter((p): p is NonNullable<typeof p> => p !== undefined);
	});
</script>

<svelte:window onmousemove={onDrag} onmouseup={stopDrag} />

<svelte:head>
	<title>Topology - FlowTime</title>
</svelte:head>

<div class="flex h-full flex-col min-w-0" bind:this={containerEl}>
	<!-- Toolbar -->
	<div class="flex items-center gap-2 border-b px-2 py-1" data-heatmap-toolbar>
		<span class="text-xs font-semibold text-muted-foreground">Topology</span>
		{#if runs.length > 0}
			<select
				class="bg-background border-input rounded border px-1.5 py-0.5 text-xs"
				value={selectedRunId}
				onchange={(e) => selectRun((e.target as HTMLSelectElement).value)}
			>
				{#each runs as run}
					<option value={run.runId}>{formatRunOption(run)}</option>
				{/each}
			</select>
		{/if}

		<div class="mx-1 h-3 w-px bg-border"></div>

		<!-- Metric selector -->
		<MetricSelector selected={workbench.selectedMetric} onSelect={onMetricSelect} />

		<!-- Class filter -->
		{#if availableClasses.length > 0}
			<div class="mx-1 h-3 w-px bg-border"></div>
			<span class="text-[10px] text-muted-foreground">Class:</span>
			<div class="flex items-center gap-1">
				{#each availableClasses as cls (cls)}
					<button
						class="rounded px-1.5 py-0.5 text-[10px] transition-colors {viewState.activeClasses.has(cls)
							? 'bg-foreground text-background font-medium'
							: 'bg-muted text-muted-foreground hover:bg-accent'}"
						onclick={() => toggleClass(cls)}
					>
						{cls}
					</button>
				{/each}
				{#if viewState.activeClasses.size > 0}
					<button
						class="text-[10px] text-muted-foreground hover:text-foreground px-1"
						onclick={() => viewState.clearClasses()}
						aria-label="Clear class filter"
					>
						clear
					</button>
				{/if}
			</div>
		{/if}

		<!-- Node-mode toggle (AC15) -->
		<div class="mx-1 h-3 w-px bg-border"></div>
		<NodeModeToggle mode={viewState.nodeMode} onChange={onNodeModeChange} />

		<!-- Heatmap-specific controls: sort + row-stability -->
		{#if viewState.activeView === 'heatmap'}
			<div class="mx-1 h-3 w-px bg-border"></div>
			<span class="text-[10px] text-muted-foreground">Sort:</span>
			<select
				class="bg-background border-input rounded border px-1 py-0.5 text-[10px]"
				value={viewState.sortMode}
				onchange={(e) => viewState.setSortMode((e.target as HTMLSelectElement).value as SortMode)}
				data-testid="heatmap-sort-select"
			>
				{#each SORT_MODES as m}
					<option value={m}>{m}</option>
				{/each}
			</select>
			<label class="flex items-center gap-1 text-[10px] text-muted-foreground">
				<input
					type="checkbox"
					class="align-middle"
					checked={viewState.rowStabilityOn}
					onchange={(e) =>
						viewState.setRowStabilityOn((e.target as HTMLInputElement).checked)}
					data-testid="heatmap-row-stability"
				/>
				Keep filtered rows
			</label>
			<label class="flex items-center gap-1 text-[10px] text-muted-foreground">
				<input
					type="checkbox"
					class="align-middle"
					checked={viewState.fitWidth}
					onchange={(e) => viewState.setFitWidth((e.target as HTMLInputElement).checked)}
					data-testid="heatmap-fit-width"
				/>
				Fit to width
			</label>
		{/if}
	</div>

	<!-- View switcher -->
	{#if graph}
		<div class="px-2 pt-1">
			<ViewSwitcher
				views={views}
				active={viewState.activeView}
				onChange={(id) => viewState.setView(id as 'topology' | 'heatmap')}
			/>
		</div>
	{/if}

	{#if error}
		<div class="flex items-center gap-2 p-3 text-xs text-destructive">
			<AlertCircleIcon class="size-3.5" />
			<span>{error}</span>
		</div>
	{:else if loading}
		<!-- Run-load placeholder. A uniform muted pulse — NOT a structured
		     skeleton — because the eventual content includes a timeline-scrubber
		     strip above the canvas (renders inside `{:else if graph}` once
		     `binCount > 0`), and faking its position with placeholder bars
		     causes a visible layout jump when graph arrives.
		     m-E21-08 AC5 — uniform pulse, no transition. The 160 ms cross-fade
		     I tried in AC6 caused the leaving skeleton to overlap the entering
		     real content in a way that read as a flickering frame; instant
		     state-change is the right behaviour for the topology run-load. -->
		<div
			class="flex-1 bg-muted animate-pulse"
			data-testid="topology-skeleton"
		></div>
	{:else if graph}
		<!-- Timeline scrubber — above the canvas for both views -->
		{#if viewState.binCount > 0}
			<div class="px-2 py-1 border-b">
				<TimelineScrubber
					binCount={viewState.binCount}
					currentBin={viewState.currentBin}
					playing={viewState.playing}
					onBinChange={(bin) => {
						stopPlayback();
						loadBin(bin);
					}}
					onTogglePlay={togglePlayback}
					onPrev={prevBin}
					onNext={nextBin}
				/>
			</div>
		{/if}

		<!-- Split: canvas (topology | heatmap) + Workbench -->
		<div class="flex-1 flex flex-col min-h-0 min-w-0">
			<!-- Canvas area. `min-w-0` is load-bearing: without it, flex-item intrinsic
			     sizing lets the SVG's default width (up to binCount * 18 px for wide
			     runs) grow this div beyond the viewport, defeating both `overflow-auto`
			     and the heatmap's fit-to-width math.

			     m-E21-08 AC5 — when the state_window request is in flight on the
			     initial load (no prior windowNodes to keep visible), render a
			     skeleton in the canvas region instead of an empty SVG / heatmap so
			     the empty→populated flicker is replaced with a placeholder shape. -->
			{#if loadingWindow && windowNodes.length === 0}
				<!-- Geometry matches the dag-container `<div>` below exactly so
				     skeleton → real-content swap is a stroke replacement, not a
				     layout shift. No transition: a 160 ms fade leaves the
				     skeleton lingering past when real content is ready, which
				     reads as a flickering frame. Instant swap is right here. -->
				<div
					style="height: {splitRatio}%"
					class="overflow-hidden min-w-0"
					data-testid="topology-canvas-skeleton"
				>
					<Skeleton class="w-full h-full rounded-none" />
				</div>
			{:else}
			<div
				style="height: {splitRatio}%"
				class="overflow-auto min-w-0"
				bind:this={dagContainer}
				role={viewState.activeView === 'topology' ? 'application' : undefined}
				aria-label={viewState.activeView === 'topology'
					? 'Topology graph — Tab to focus a node or edge, Enter or Space to pin'
					: undefined}
				onkeydown={onTopologyKeydown}
			>
				{#if viewState.activeView === 'topology'}
					<DagMapView {graph} metrics={currentMetrics} selected={selectedIds} />
				{:else if viewState.activeView === 'heatmap'}
					<HeatmapView
						{windowNodes}
						timestampsUtc={windowTimestamps}
						graphEdges={heatmapGraphEdges}
						metric={workbench.selectedMetric}
						binCount={viewState.binCount}
						currentBin={viewState.currentBin}
						activeClasses={viewState.activeClasses}
						pinnedIds={viewState.pinnedIds}
						sortMode={viewState.sortMode}
						rowStabilityOn={viewState.rowStabilityOn}
						grid={runIndex?.grid}
						selectedCell={viewState.selectedCell}
						fitWidth={viewState.fitWidth}
						onPinAndScrub={onHeatmapPinAndScrub}
						onUnpin={onHeatmapUnpin}
						onCellSelect={(id, bin) => viewState.setSelectedCell(id, bin)}
					/>
				{/if}
			</div>
			{/if}

			<!-- Splitter handle -->
			<div
				class="h-1 cursor-row-resize border-y border-border hover:bg-accent flex-shrink-0 {dragging
					? 'bg-accent'
					: ''}"
				role="separator"
				aria-orientation="horizontal"
				onmousedown={startDrag}
			></div>

			<!-- Workbench panel — two-column layout (m-E21-07 AC2 / AC3):
			     Left column: ValidationPanel. Width is driven by validation.state —
			     `issues` → 300 px (per confirmation 1, not user-resizable);
			     `empty` (post-load, zero warnings) → zero width via display:none so
			     the pinned-card region reclaims the full panel width.
			     Right column: existing pinned-card flex row. -->
			<div style="height: {100 - splitRatio}%" class="overflow-hidden bg-background flex min-h-0 min-w-0">
				{#if validation.state === 'issues' || (loading && selectedRunId !== undefined)}
					<div
						class="border-r border-border shrink-0 overflow-hidden"
						style="width: 300px"
					>
						<ValidationPanel loading={loading} />
					</div>
				{/if}
				<div class="flex-1 min-w-0 overflow-auto">
					{#if !hasPinnedItems}
						<div class="flex h-full items-center justify-center text-muted-foreground text-xs">
							Click a node or edge to inspect
						</div>
					{:else}
						<div class="flex gap-2 p-2 flex-wrap items-start">
							{#each sortedPinnedNodes as pin (pin.id)}
								{@const nodeState = getNodeState(pin.id)}
								<div
									animate:flip={{ duration: 220 }}
									in:fade={{ duration: 160 }}
									out:fade={{ duration: 120 }}
								>
									<WorkbenchCard
										nodeId={pin.id}
										kind={pin.kind}
										metrics={nodeState ? extractNodeMetrics(nodeState) : []}
										sparklineValues={sparklineMap.get(pin.id) ?? []}
										sparklineLabel={workbench.selectedMetric.label.toLowerCase()}
										currentBin={viewState.currentBin}
										selected={viewState.selectedCell?.nodeId === pin.id}
										warningSeverity={pickWarningSeverity(validation.nodeSeverityById, pin.id)}
										onSelect={() => viewState.setSelectedCell(pin.id, viewState.currentBin)}
										onClose={() => unpinAndClearSelection(pin.id)}
									/>
								</div>
							{/each}
							{#each workbench.pinnedEdges as edge, edgeIdx (`${edge.from}→${edge.to}`)}
								{@const edgeState = getEdgeState(edge.from, edge.to)}
								<div
									animate:flip={{ duration: 220 }}
									in:fade={{ duration: 160 }}
									out:fade={{ duration: 120 }}
								>
									<WorkbenchEdgeCard
										from={edge.from}
										to={edge.to}
										metrics={edgeState ? extractEdgeMetrics(edgeState) : []}
										warningSeverity={pickWarningSeverity(
											validation.edgeSeverityById,
											`${edge.from}→${edge.to}`,
										)}
										selected={
											viewState.selectedEdge?.from === edge.from &&
											viewState.selectedEdge?.to === edge.to
										}
										onSelect={() => viewState.setSelectedEdge(edge.from, edge.to)}
										onClose={() => unpinEdgeAndClearSelection(edge.from, edge.to)}
									/>
								</div>
							{/each}
						</div>
					{/if}
				</div>
			</div>
		</div>
	{:else if runs.length === 0}
		<div class="flex flex-1 items-center justify-center">
			<p class="text-muted-foreground text-xs">No runs available. Run a model first.</p>
		</div>
	{/if}
</div>

<style>
	/* m-E21-08 AC3 — pinned-edge chrome. Renamed from `.edge-selected`. Every
	   edge pinned to the workbench renders this amber stroke. The single-edge
	   `selected` chrome (turquoise, below) composes on top via source-order
	   specificity — both classes can apply to the same path simultaneously. */
	:global(.edge-pinned) {
		stroke: var(--ft-viz-amber) !important;
		stroke-width: 3 !important;
		opacity: 1 !important;
	}

	/* m-E21-08 AC3 — selected-edge chrome. Single edge whose from/to matches
	   viewState.selectedEdge. Distinct from `.edge-pinned` (amber, every pinned
	   edge): `.edge-selected` reads as "this is my current focus edge", matches
	   the m-E21-08 AC2 .node-selected --ft-highlight convention so node and
	   edge selection chrome share a single hue family. Defined AFTER
	   .edge-pinned so a pinned-and-selected edge renders the turquoise stroke. */
	:global(.edge-selected) {
		stroke: var(--ft-highlight) !important;
		stroke-width: 4 !important;
		opacity: 1 !important;
	}

	/* m-E21-08 AC2 — node selection stroke rule. Recolors the OUTER pin-status
	   ring only (`.dag-map-selected` per `lib/dag-map/src/render.js:296`) — the
	   inner `[data-id]` circle carries the data-encoded metric stroke (the
	   orange the user is reading meaning from), and must be left untouched.
	   Semantics: outer ring is gray when a card exists for the node, turquoise
	   when that card is the currently-selected one. The inner circle's stroke
	   is the data signal and has no chrome overlay. */
	:global(.node-selected .dag-map-selected) {
		stroke: var(--ft-highlight) !important;
	}

	/* m-E21-08 AC1 — keyboard focus ring on topology nodes and edges.
	   Chrome-token (--ft-focus), distinct from --ft-pin / --ft-highlight /
	   --ft-warn / --ft-err. The 2px outline + 2px offset reads as a focus
	   affordance against both light- and dark-mode chrome and stays visually
	   separate from the pin glyph and the m-E21-07 warning indicators. */
	:global([data-node-id]:focus),
	:global([data-edge-from]:not([data-edge-hit]):focus) {
		outline: 2px solid var(--ft-focus);
		outline-offset: 2px;
	}
	:global([data-node-id]:focus:not(:focus-visible)),
	:global([data-edge-from]:not([data-edge-hit]):focus:not(:focus-visible)) {
		outline: none;
	}
</style>
