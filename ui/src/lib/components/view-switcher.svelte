<!--
  View switcher — m-E21-06 AC1 + ADR-m-E21-06-01.

  Typed tab bar rendered above the canvas on `/time-travel/topology`. Takes the list of
  views inline from the parent route (no manifest registry, no Svelte context API) and
  delegates selection via `onChange`. Shadcn-style underline on the active tab; Alt+<N>
  shortcut matching via a pure helper so the parsing rules can be unit-tested.

  Props:
    views   — ordered list of views to render (id, label, optional shortcut).
    active  — currently-active view id.
    onChange(id) — callback fired when the user clicks a tab or triggers a shortcut.

  Shortcut handling:
    - A `svelte:window onkeydown` listener matches Alt+<N> against `views` via the pure
      helper `matchViewShortcut`.
    - When a shortcut is matched, the event is `preventDefault`-ed so the browser does
      not also perform Alt+<N>'s default behaviour (menu nav on some platforms).
    - Matching is exact on modifiers — Ctrl+Alt+1 or Shift+Alt+1 do not fire the
      switcher. See `matchViewShortcut` for the rules.
-->

<script lang="ts">
	import {
		matchViewShortcut,
		type ShortcutEventLike,
	} from './view-switcher-shortcut.js';

	export interface View {
		id: string;
		label: string;
		shortcut?: string;
	}

	interface Props {
		views: ReadonlyArray<View>;
		active: string;
		onChange: (id: string) => void;
	}

	let { views, active, onChange }: Props = $props();

	function handleKeyDown(event: KeyboardEvent) {
		const ev: ShortcutEventLike = {
			altKey: event.altKey,
			ctrlKey: event.ctrlKey,
			metaKey: event.metaKey,
			shiftKey: event.shiftKey,
			key: event.key,
		};
		const matched = matchViewShortcut(ev, views);
		if (matched && matched !== active) {
			event.preventDefault();
			onChange(matched);
		}
	}
</script>

<svelte:window onkeydown={handleKeyDown} />

<div
	role="tablist"
	aria-label="View switcher"
	class="flex items-center gap-1 border-b border-border text-xs"
>
	{#each views as view (view.id)}
		{@const isActive = view.id === active}
		<button
			type="button"
			role="tab"
			aria-selected={isActive}
			aria-controls="view-{view.id}"
			data-view-id={view.id}
			data-active={isActive}
			class="relative px-2 py-1 transition-colors {isActive
				? 'text-foreground font-medium'
				: 'text-muted-foreground hover:text-foreground'}"
			onclick={() => {
				if (!isActive) onChange(view.id);
			}}
		>
			<span>{view.label}</span>
			{#if view.shortcut}
				<span class="ml-1 text-[10px] text-muted-foreground/70">{view.shortcut}</span>
			{/if}
			{#if isActive}
				<span
					class="pointer-events-none absolute inset-x-0 -bottom-px h-0.5 bg-foreground"
					aria-hidden="true"
				></span>
			{/if}
		</button>
	{/each}
</div>
