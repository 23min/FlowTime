<script lang="ts">
	/**
	 * Validation panel — m-E21-07 ACs 2 / 3 / 9 / 10.
	 *
	 * Renders the merged sorted list of validation rows for the currently-loaded
	 * run. Reads exclusively from the shared `validation` store (AC11 single
	 * source of truth) — does NOT trigger its own fetch.
	 *
	 * Click semantics (AC9) — implemented by the pure helper
	 * `handleValidationRowClick` so every branch is vitest-covered without
	 * booting Svelte's reactive runtime:
	 *   - Node row → ensure-pinned (idempotent `workbench.pin`) + always set
	 *     `viewState.setSelectedCell(...)`. Re-clicking the same row is a
	 *     non-destructive selection-anchor reaffirmation; it never unpins.
	 *   - Edge row → `workbench.bringEdgeToFront(from, to)` (ensure-pinned +
	 *     move-to-end), so the cross-link "last-pinned wins" convention focuses
	 *     on the just-clicked edge regardless of previous pin state. The edge
	 *     id is parsed against the workbench's `from→to` convention; if the
	 *     analyser persisted a different format, the click is a no-op.
	 *   - Row with no identity (`key === null`) → no pin affordance; click is a
	 *     no-op and the cursor stays default.
	 *
	 * The click handler NEVER unpins. The unpin path lives only on the
	 * workbench card's close button. This guards against a destructive
	 * re-click regression (the bug that surfaced during smoke-testing
	 * 2026-04-27 — see tracking-doc Decisions).
	 *
	 * Cross-link highlighting (AC10):
	 *   - Reads `viewState.selectedCell?.nodeId` for node selection.
	 *   - Reads the workbench's pinned edges (last-pinned wins) for edge selection.
	 *   - Implementation choice: highlight-and-de-emphasize-others (less
	 *     destructive than filter-only-matching; preserves the user's mental
	 *     model of the full warning list while making the matching rows
	 *     visually obvious). Recorded in tracking-doc Decisions.
	 *
	 * Loading transition vs collapsed state (AC3 + AC4):
	 *   - When the upstream data is in flight (`loading={true}`), the empty-state
	 *     string `No validation issues for this run.` renders so the user sees a
	 *     placeholder before the column collapses to zero width.
	 *   - When the run has confirmed zero warnings, the parent wrapper sets the
	 *     left column to zero width and this component does not render its
	 *     content (the column collapse happens at the layout level).
	 */
	import { validation } from '$lib/stores/validation.svelte.js';
	import { viewState } from '$lib/stores/view-state.svelte.js';
	import { workbench } from '$lib/stores/workbench.svelte.js';
	import {
		handleValidationRowClick,
		rowId,
		rowsMatchingSelection,
		severityChromeToken,
		severityRowBackground,
		type ValidationRow,
		type ValidationSelection,
	} from '$lib/utils/validation-helpers.js';

	interface Props {
		/** True while the route is between selecting a run and loadWindow() resolving.
		 *  Lets the panel render the loading-transition empty-state string before
		 *  the column collapses at the layout level. */
		loading?: boolean;
	}
	let { loading = false }: Props = $props();

	// ---- selection set for cross-link highlighting (AC10) -----------------------
	const selection = $derived.by<ValidationSelection | null>(() => {
		const nodeSel = viewState.selectedCell?.nodeId;
		// Last-pinned edge wins for the cross-link surface — mirrors workbench's
		// "most recently interacted" semantics. If no edge is pinned, the
		// edgeId field stays unset and the cross-link only fires on node selection.
		const edges = workbench.pinnedEdges;
		const edgeSel =
			edges.length > 0 ? `${edges[edges.length - 1].from}→${edges[edges.length - 1].to}` : undefined;
		if (nodeSel === undefined && edgeSel === undefined) return null;
		return { nodeId: nodeSel, edgeId: edgeSel };
	});

	const matchingIds = $derived.by<Set<string>>(() => {
		return rowsMatchingSelection(validation.rows, selection);
	});

	// ---- click semantics (AC9) ---------------------------------------------------
	// Implementation lives in `handleValidationRowClick` (pure helper) so every
	// branch is unit-testable. This wrapper only adapts the runtime stores to
	// the helper's `ValidationRowClickDeps` shape.
	function onRowClick(row: ValidationRow) {
		handleValidationRowClick(row, {
			pin: (id, kind) => workbench.pin(id, kind),
			bringEdgeToFront: (from, to) => workbench.bringEdgeToFront(from, to),
			setSelectedCell: (nodeId, bin) => viewState.setSelectedCell(nodeId, bin),
			currentBin: viewState.currentBin,
		});
	}

	// ---- per-row severity chrome -------------------------------------------------
	function severityStyle(severity: string): string | undefined {
		const token = severityChromeToken(severity);
		return token ? `color: var(${token})` : undefined;
	}
</script>

<!-- Panel content. The wrapper owning the column width lives in +page.svelte; this
     component only renders the chrome-internal layout. -->
<div class="validation-panel flex h-full flex-col text-xs" data-testid="validation-panel">
	{#if validation.state === 'empty'}
		<!-- Loading transition only — once the run confirms zero warnings, the
		     parent wrapper collapses the column and this component does not render. -->
		{#if loading}
			<div class="flex h-full items-center justify-center text-muted-foreground p-2 text-center">
				<span>No validation issues for this run.</span>
			</div>
		{/if}
	{:else}
		<div class="flex flex-col gap-1 p-1.5 overflow-auto">
			<div class="text-[10px] font-semibold uppercase text-muted-foreground tracking-wide px-0.5">
				Validation
			</div>
			{#each validation.rows as row (rowId(row))}
				{@const isMatch = matchingIds.has(rowId(row))}
				{@const isClickable = row.key !== null}
				{@const dim = matchingIds.size > 0 && !isMatch}
				{@const token = severityChromeToken(row.severity)}
				{@const bgToken = severityRowBackground(row.severity)}
				{@const rowStyle = [
					bgToken ? `background: var(${bgToken})` : null,
					isMatch ? 'border-color: var(--ft-highlight)' : null,
				].filter(Boolean).join('; ')}
				<button
					type="button"
					class="text-left rounded border px-1.5 py-1 flex flex-col gap-0.5 transition-opacity {isClickable
						? 'hover:bg-accent cursor-pointer'
						: 'cursor-default'} {dim ? 'opacity-50' : ''}"
					style={rowStyle || undefined}
					onclick={() => onRowClick(row)}
					disabled={!isClickable}
					data-testid="validation-row"
					data-row-kind={row.kind}
					data-row-key={row.key ?? ''}
					data-row-match={isMatch ? 'true' : undefined}
				>
					<div class="flex items-center gap-1">
						<!-- Severity chip — coloured dot + word label per Q3=B -->
						<span
							class="inline-block size-1.5 rounded-full shrink-0"
							style={token ? `background: var(${token})` : 'background: var(--muted-foreground)'}
							aria-hidden="true"
						></span>
						<span
							class="text-[10px] font-semibold uppercase tracking-wide shrink-0"
							style={severityStyle(row.severity)}
						>{row.severity}</span>
						{#if row.key !== null}
							<span
								class="text-[10px] font-mono text-muted-foreground truncate"
								title={row.key}
							>
								{#if row.kind === 'node'}{row.key}{:else}{row.key}{/if}
							</span>
						{/if}
						{#if typeof row.startBin === 'number' && typeof row.endBin === 'number'}
							<span class="text-[9px] font-mono text-muted-foreground shrink-0 ml-auto">
								{#if row.startBin === row.endBin}
									bin {row.startBin}
								{:else}
									bins {row.startBin}..{row.endBin}
								{/if}
							</span>
						{/if}
					</div>
					<div class="text-[11px] leading-snug whitespace-pre-wrap break-words">
						{row.message}
					</div>
				</button>
			{/each}
		</div>
	{/if}
</div>

<style>
	.validation-panel {
		min-width: 0;
	}
</style>
