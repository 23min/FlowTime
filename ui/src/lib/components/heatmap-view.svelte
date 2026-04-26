<!--
  Heatmap view — m-E21-06 AC3/4/7/8/9/10/11/12.

  Renders an `N rows × B cols` SVG grid sibling to topology. Consumes the
  `state_window` response (already loaded by the route) and the shared view-state
  store (via props for testability). Emits no API calls itself.

  Cell states (AC4):
    - observed              → colored via the shared 99p-clipped domain.
    - no-data-for-bin       → neutral grey + diagonal hatch.
    - metric-undefined-for-node → row-level muted (uniform, not per-cell hatch).

  Interactions:
    - Click on an observed cell → pin node + move scrubber to bin T (AC8).
    - Click on a pin glyph in the row gutter → unpin (AC10).
    - Scrubber column highlight via a single pre-rendered overlay rect (AC9).
    - Keyboard nav: Tab into grid, arrow keys move, Enter pins + scrubs (AC12).
-->

<script lang="ts">
	import PinIcon from '@lucide/svelte/icons/pin';
	import {
		buildHeatmapGrid,
		buildBinAxisLabels,
		type HeatmapCell,
	} from './heatmap-geometry.js';
	import type { StateWindowResponse, RunIndex } from '$lib/api/types.js';
	import type { MetricDef } from '$lib/utils/metric-defs.js';
	import type { SortMode } from '$lib/utils/heatmap-sort.js';
	import { pickBinLabelStride, type BinUnit } from '$lib/utils/bin-label-stride.js';
	import { formatBinTime } from './heatmap-time-format.js';
	import { computeNextFocus } from './heatmap-keyboard-nav.js';
	import { colorScales } from 'dag-map';

	interface Props {
		windowNodes: StateWindowResponse['nodes'];
		timestampsUtc?: string[];
		graphEdges?: ReadonlyArray<{ from: string; to: string }>;
		metric: MetricDef;
		binCount: number;
		currentBin: number;
		activeClasses: ReadonlySet<string>;
		pinnedIds: ReadonlySet<string>;
		sortMode: SortMode;
		rowStabilityOn: boolean;
		grid?: RunIndex['grid'];
		/** Persistent cell selection (last focused / clicked observed cell). Null when
		 *  nothing has been selected yet. Drives the custom SVG selection overlay and
		 *  also the workbench-card title highlight (at the route level). */
		selectedCell: { nodeId: string; bin: number } | null;
		/** When true, compress cell width to fit the entire grid into the visible
		 *  container (no horizontal overflow). Never expands beyond the default 18 px;
		 *  floors at 3 px so tiles remain visible for very wide runs. */
		fitWidth?: boolean;
		/** Invoked when user pins-and-scrubs via a cell click or Enter/Space. */
		onPinAndScrub: (nodeId: string, bin: number) => void;
		/** Invoked when user clicks the pin-glyph in a row label gutter. */
		onUnpin: (nodeId: string) => void;
		/** Invoked when a cell gains focus or is clicked — updates the persistent
		 *  selection marker. Route wires this to `viewState.setSelectedCell`. */
		onCellSelect: (nodeId: string, bin: number) => void;
		/** Optional testid for the root SVG, for Playwright selectors. */
		testId?: string;
	}

	let {
		windowNodes,
		timestampsUtc,
		graphEdges = [],
		metric,
		binCount,
		currentBin,
		activeClasses,
		pinnedIds,
		sortMode,
		rowStabilityOn,
		grid,
		selectedCell,
		fitWidth = false,
		onPinAndScrub,
		onUnpin,
		onCellSelect,
		testId = 'heatmap-grid',
	}: Props = $props();

	// ---- layout constants (compact density per repo convention) --------------------
	const DEFAULT_CELL_W = 18;
	// Absolute minimum cell width in px when fit-to-width is on. At 1 px cells are
	// essentially a density strip (no per-cell detail), but the grid still fits the
	// viewport without horizontal scroll — degraded-but-correct for extreme cases.
	const MIN_CELL_W = 1;
	// Reserved gutter on the right of the grid (in px). Leaves a small margin so
	// the rightmost cell doesn't sit flush with a scrollbar / border.
	const RIGHT_MARGIN = 4;
	const CELL_H = 18;
	const ROW_LABEL_W = 132;
	const TOP_AXIS_H = 24;

	// Container width — the `<div>` wrapping the SVG carries `bind:clientWidth` to
	// this state. Used both for the fit-to-width computation AND for the
	// `shouldFit` auto-derivation that turns compression on whenever the natural
	// grid width would overflow the viewport (regardless of user toggle state).
	let containerWidth = $state<number>(0);

	// Natural (uncompressed) SVG width at DEFAULT_CELL_W.
	const naturalWidth = $derived(ROW_LABEL_W + binCount * DEFAULT_CELL_W);

	// Should the grid compress? True whenever the user has explicitly toggled
	// fit-to-width, OR whenever the natural width exceeds the container (auto
	// compression, even if the user hasn't opted in). The auto case hits the
	// common wide-run scenario where the default overflows off-screen.
	const shouldFit = $derived(
		fitWidth || (containerWidth > 0 && naturalWidth > containerWidth),
	);

	// Cell width: default 18 px, or compressed to fit the container when
	// `shouldFit` is true. Uses fractional pixels so cells fill the available
	// space exactly (integer floor left large right-side gaps at high bin counts).
	// Never expands beyond default; floors at MIN_CELL_W.
	const CELL_W = $derived.by(() => {
		if (!shouldFit || binCount <= 0 || containerWidth <= 0) return DEFAULT_CELL_W;
		const available = Math.max(0, containerWidth - ROW_LABEL_W - RIGHT_MARGIN);
		const candidate = available / binCount;
		return Math.max(MIN_CELL_W, Math.min(DEFAULT_CELL_W, candidate));
	});

	// Strip trailing port suffix like "NodeA:out" → "NodeA".
	function stripPort(id: string): string {
		const idx = id.indexOf(':');
		return idx >= 0 ? id.substring(0, idx) : id;
	}

	const normalizedEdges = $derived.by(() => {
		const seen = new Set<string>();
		const out: { from: string; to: string }[] = [];
		for (const e of graphEdges) {
			const from = stripPort(e.from);
			const to = stripPort(e.to);
			const key = `${from}->${to}`;
			if (seen.has(key)) continue;
			seen.add(key);
			out.push({ from, to });
		}
		return out;
	});

	const geom = $derived.by(() =>
		buildHeatmapGrid({
			windowNodes,
			metric,
			binCount,
			graphEdges: normalizedEdges,
			activeClasses,
			sortMode,
			rowStabilityOn,
		})
	);

	const stride = $derived.by(() =>
		pickBinLabelStride(
			CELL_W,
			grid?.binSize ?? 1,
			(grid?.binUnit ?? 'minutes') as BinUnit,
			binCount,
		)
	);

	const binLabels = $derived.by(() =>
		buildBinAxisLabels({
			binCount,
			stride,
			formatBinLabel: (bin) => formatBinLabel(bin),
		})
	);

	// ---- time formatting -----------------------------------------------------------
	// Tooltip always shows `Bin N · <time>`. Time is absolute when `timestampsUtc`
	// carries a value for that bin; otherwise offset-from-start (`+HH:MM`). Pure
	// helpers live in `heatmap-time-format.ts`; this is the binding into props.
	function formatBinLabel(bin: number): string {
		return formatBinTime(bin, timestampsUtc, grid);
	}

	function tooltipFor(row: (typeof geom.visibleRows)[number], cell: HeatmapCell): string {
		const time = formatBinLabel(cell.bin);
		const binPrefix = `Bin ${cell.bin} · ${time}`;
		if (row.filtered) {
			return `${binPrefix} · ${row.id} · Node filtered by class`;
		}
		if (row.rowState === 'metric-undefined-for-node') {
			return `${binPrefix} · ${row.id} · ${metric.label}: not defined for this node`;
		}
		if (cell.state === 'no-data-for-bin') {
			return `${binPrefix} · ${row.id} · ${metric.label}: — (no data for this bin)`;
		}
		// Observed
		const rendered = metric.format(cell.value!);
		if (cell.value === 0) return `${binPrefix} · ${row.id} · ${metric.label}: ${rendered} (observed)`;
		return `${binPrefix} · ${row.id} · ${metric.label}: ${rendered}`;
	}

	function ariaLabelFor(row: (typeof geom.visibleRows)[number], cell: HeatmapCell): string {
		return tooltipFor(row, cell);
	}

	function cellFill(cell: HeatmapCell): string {
		if (cell.state !== 'observed' || cell.normalized === null) return 'var(--muted)';
		return colorScales.palette(cell.normalized);
	}

	function onCellClick(nodeId: string, cell: HeatmapCell) {
		if (cell.state !== 'observed') return;
		markCellFocused(nodeId, cell.bin);
		onPinAndScrub(nodeId, cell.bin);
	}

	function onCellKeydown(event: KeyboardEvent, nodeId: string, cell: HeatmapCell) {
		if (event.key === 'Enter' || event.key === ' ') {
			if (cell.state !== 'observed') return;
			event.preventDefault();
			onPinAndScrub(nodeId, cell.bin);
			return;
		}
		if (event.key === 'Escape') {
			event.preventDefault();
			// Return focus to the first toolbar control. The toolbar marker lives on
			// a parent element of the SVG (see `+page.svelte`); when the heatmap is
			// rendered standalone in tests, the selector resolves to null and focus
			// stays where it is — that is the intended degrade path.
			const toolbar = document
				.querySelector<HTMLElement>('[data-heatmap-toolbar] button');
			toolbar?.focus();
			return;
		}
		const target = computeNextFocus(
			{
				nodeId,
				bin: cell.bin,
				rowIds: geom.visibleRows.map((r) => r.id),
				binCount,
			},
			event.key,
		);
		if (!target) return;
		event.preventDefault();
		const el = rootEl?.querySelector<SVGGraphicsElement>(
			`[data-cell-node="${CSS.escape(target.nodeId)}"][data-cell-bin="${target.bin}"]`,
		);
		el?.focus();
	}

	let rootEl: SVGSVGElement | undefined = $state();

	// Persistent cell-selection marker — driven by the shared view-state store via the
	// `selectedCell` prop and `onCellSelect` callback. Survives window blur (unlike DOM
	// focus). Rendered as a custom SVG outline on top of cells — the browser-default
	// SVG `<g>` focus indicator is suppressed via CSS since it renders as an ugly
	// beveled rectangle in Chromium and doesn't respect the `outline` token.

	function markCellFocused(nodeId: string, bin: number) {
		onCellSelect(nodeId, bin);
	}

	const selectedRowIdx = $derived.by(() => {
		if (!selectedCell) return -1;
		return geom.visibleRows.findIndex((r) => r.id === selectedCell!.nodeId);
	});
</script>

{#if geom.isEmptyAfterFilter}
	<div
		class="flex h-full items-center justify-center text-xs text-muted-foreground"
		data-testid="heatmap-empty-state"
	>
		No nodes match the current class filter
	</div>
{:else}
	{@const rows = geom.visibleRows}
	{@const width = ROW_LABEL_W + binCount * CELL_W}
	{@const height = TOP_AXIS_H + rows.length * CELL_H}

	<div class="overflow-auto" data-heatmap-root bind:clientWidth={containerWidth}>
		<svg
			bind:this={rootEl}
			{width}
			{height}
			viewBox="0 0 {width} {height}"
			role="grid"
			aria-label="Node-by-bin heatmap for {metric.label}"
			aria-rowcount={rows.length}
			aria-colcount={binCount}
			data-testid={testId}
		>
			<!-- Hatch pattern for no-data-for-bin cells (AC4). -->
			<defs>
				<pattern
					id="heatmap-hatch"
					patternUnits="userSpaceOnUse"
					width="4"
					height="4"
					patternTransform="rotate(45)"
				>
					<rect width="4" height="4" fill="var(--muted)" />
					<line
						x1="0"
						y1="0"
						x2="0"
						y2="4"
						stroke="var(--muted-foreground)"
						stroke-width="0.75"
						opacity="0.5"
					/>
				</pattern>
			</defs>

			<!-- Top axis labels (AC11) -->
			<g class="heatmap-axis" aria-hidden="true">
				{#each binLabels as { bin, label } (bin)}
					{@const x = ROW_LABEL_W + bin * CELL_W + CELL_W / 2}
					<text
						x={x}
						y={TOP_AXIS_H - 10}
						text-anchor="middle"
						class="heatmap-axis-label"
					>
						{label}
					</text>
					<line
						x1={x}
						x2={x}
						y1={TOP_AXIS_H - 6}
						y2={TOP_AXIS_H - 2}
						stroke="var(--muted-foreground)"
						stroke-width="0.5"
						opacity="0.5"
					/>
				{/each}
			</g>

			<!-- Column highlight overlay (AC9). A solid turquoise bar just above the
			     current-bin column — marks the active bin without covering any cells.
			     Cells retain their data-color undisturbed; the bar sits in the top-axis
			     gutter between the bin labels and the first row. -->
			{#if binCount > 0 && currentBin >= 0 && currentBin < binCount}
				<rect
					class="heatmap-col-highlight"
					x={ROW_LABEL_W + currentBin * CELL_W}
					y={TOP_AXIS_H - 3}
					width={CELL_W}
					height={3}
					fill="var(--ft-highlight)"
					pointer-events="none"
					data-testid="heatmap-col-highlight"
				/>
			{/if}

			<!-- Rows -->
			{#each rows as row, rowIdx (row.id)}
				{@const rowY = TOP_AXIS_H + rowIdx * CELL_H}
				{@const isPinned = pinnedIds.has(row.id)}
				{@const isMuted = row.rowState === 'metric-undefined-for-node' || row.filtered}

				<g
					role="row"
					aria-rowindex={rowIdx + 1}
					data-row-id={row.id}
					data-row-pinned={isPinned}
					data-row-filtered={row.filtered}
					data-row-state={row.rowState}
				>
					<!-- Row gutter background for the muted-row case (AC4 row-level optimization). -->
					{#if isMuted}
						<rect
							x={ROW_LABEL_W}
							y={rowY}
							width={binCount * CELL_W}
							height={CELL_H}
							fill={row.filtered ? 'var(--muted)' : 'url(#heatmap-hatch)'}
							opacity="0.35"
							pointer-events="none"
						/>
					{/if}

					<!-- Row label gutter -->
					<foreignObject x="0" y={rowY} width={ROW_LABEL_W} height={CELL_H}>
						<div class="heatmap-row-label {row.filtered ? 'is-filtered' : ''}">
							{#if isPinned}
								<button
									type="button"
									class="heatmap-pin-btn"
									aria-label="Unpin {row.id}"
									onclick={() => onUnpin(row.id)}
								>
									<PinIcon class="size-2.5" />
								</button>
							{:else}
								<span class="heatmap-pin-spacer" aria-hidden="true"></span>
							{/if}
							<span class="heatmap-row-id" title={row.id}>{row.id}</span>
						</div>
					</foreignObject>

					<!-- Cells -->
					{#each row.cells as cell (cell.bin)}
						{@const x = ROW_LABEL_W + cell.bin * CELL_W}
						{@const fill = cellFill(cell)}
						{@const observable = cell.state === 'observed' && !row.filtered}
						<g
							role="gridcell"
							aria-colindex={cell.bin + 1}
							aria-label={ariaLabelFor(row, cell)}
							data-cell-node={row.id}
							data-cell-bin={cell.bin}
							data-cell-state={cell.state}
							data-value-bucket={cell.bucket}
							tabindex={observable ? 0 : -1}
							onclick={() => onCellClick(row.id, cell)}
							onkeydown={(e) => onCellKeydown(e, row.id, cell)}
							onfocus={observable ? () => markCellFocused(row.id, cell.bin) : undefined}
							class="heatmap-cell-group"
						>
							<title>{tooltipFor(row, cell)}</title>
							{#if row.rowState === 'metric-undefined-for-node' || row.filtered}
								<!-- Row-level muted cells are rendered by the row background above.
								     This transparent rect keeps the hit area consistent for keyboard/aria
								     but the click handler is a no-op (observable=false). -->
								<rect
									x={x}
									y={rowY}
									width={CELL_W}
									height={CELL_H}
									fill="transparent"
								/>
							{:else if cell.state === 'no-data-for-bin'}
								<rect
									x={x}
									y={rowY}
									width={CELL_W}
									height={CELL_H}
									fill="url(#heatmap-hatch)"
									stroke="var(--border)"
									stroke-width="0.25"
								/>
							{:else}
								<rect
									x={x}
									y={rowY}
									width={CELL_W}
									height={CELL_H}
									fill={fill}
									stroke="var(--border)"
									stroke-width="0.25"
								/>
							{/if}
						</g>
					{/each}
				</g>
			{/each}

			<!-- Persistent cell-selection outline. Rendered AFTER cells so it sits on top
			     of the fill. Survives window blur (unlike DOM focus indicator). -->
			{#if selectedCell && selectedRowIdx >= 0}
				<rect
					class="heatmap-cell-selection"
					x={ROW_LABEL_W + selectedCell.bin * CELL_W - 1}
					y={TOP_AXIS_H + selectedRowIdx * CELL_H - 1}
					width={CELL_W + 2}
					height={CELL_H + 2}
					fill="none"
					stroke="var(--ft-highlight)"
					stroke-width="2"
					pointer-events="none"
					data-testid="heatmap-cell-selection"
				/>
			{/if}
		</svg>
	</div>
{/if}

<style>
	:global(.heatmap-axis-label) {
		font-size: 9px;
		font-family: ui-monospace, monospace;
		fill: var(--muted-foreground);
	}

	:global(.heatmap-row-label) {
		display: flex;
		align-items: center;
		gap: 2px;
		height: 100%;
		padding: 0 4px 0 2px;
		font-size: 10px;
		color: var(--foreground);
		overflow: hidden;
		white-space: nowrap;
	}

	:global(.heatmap-row-label.is-filtered) {
		color: var(--muted-foreground);
		font-style: italic;
	}

	:global(.heatmap-row-id) {
		overflow: hidden;
		text-overflow: ellipsis;
		white-space: nowrap;
		flex: 1 1 auto;
	}

	:global(.heatmap-pin-btn) {
		display: inline-flex;
		align-items: center;
		justify-content: center;
		width: 14px;
		height: 14px;
		padding: 0;
		background: transparent;
		border: 0;
		color: var(--ft-pin);
		cursor: pointer;
		border-radius: 2px;
		flex: 0 0 auto;
	}

	:global(.heatmap-pin-btn:hover),
	:global(.heatmap-pin-btn:focus-visible) {
		color: var(--ft-pin);
		background: var(--muted);
		outline: none;
	}

	:global(.heatmap-pin-spacer) {
		display: inline-block;
		width: 14px;
		height: 14px;
		flex: 0 0 auto;
	}

	/* Suppress the browser-default SVG <g> focus indicator (renders as an ugly
	   beveled rectangle in Chromium). The persistent `.heatmap-cell-selection`
	   overlay rect takes over as the visible focus/selection marker. */
	:global(.heatmap-cell-group:focus),
	:global(.heatmap-cell-group:focus-visible) {
		outline: none;
	}

	:global(.heatmap-cell-selection) {
		pointer-events: none;
	}

	:global(.heatmap-cell-group) {
		cursor: pointer;
	}

	:global(.heatmap-cell-group[data-cell-state='no-data-for-bin']),
	:global(.heatmap-cell-group[data-row-filtered='true']),
	:global(.heatmap-cell-group[data-row-state='metric-undefined-for-node']) {
		cursor: default;
	}

	:global(.heatmap-col-highlight) {
		pointer-events: none;
	}
</style>
