<script lang="ts">
	import type { TemplateDetail, BundleReuseMode } from '$lib/api/types.js';
	import { getDomainIcon } from './domain-icon.js';
	import * as Card from '$lib/components/ui/card/index.js';
	import { Badge } from '$lib/components/ui/badge/index.js';
	import { Button } from '$lib/components/ui/button/index.js';
	import { Input } from '$lib/components/ui/input/index.js';
	import * as RadioGroup from '$lib/components/ui/radio-group/index.js';
	import * as Collapsible from '$lib/components/ui/collapsible/index.js';
	import { Separator } from '$lib/components/ui/separator/index.js';
	import ChevronDownIcon from '@lucide/svelte/icons/chevron-down';
	import PlayIcon from '@lucide/svelte/icons/play';
	import EyeIcon from '@lucide/svelte/icons/eye';

	interface RunConfig {
		reuseMode: BundleReuseMode;
		rngSeed: number;
		parameters: Record<string, unknown>;
	}

	interface Props {
		template: TemplateDetail;
		busy: boolean;
		onrun: (config: RunConfig) => void;
		onpreview: (config: RunConfig) => void;
	}

	let { template, busy, onrun, onpreview }: Props = $props();

	let reuseMode = $state<BundleReuseMode>('reuse');
	let rngSeed = $state(123);
	let advancedOpen = $state(false);
	let paramValues = $state<Record<string, unknown>>({});

	const Icon = $derived(getDomainIcon(template));
	const inferredMode = 'simulation';

	// Initialize parameter defaults when template changes
	$effect(() => {
		const defaults: Record<string, unknown> = {};
		for (const p of template.parameters) {
			defaults[p.name] = p.defaultValue ?? (p.type === 'number' ? 0 : '');
		}
		paramValues = defaults;
	});

	function getConfig(): RunConfig {
		return {
			reuseMode,
			rngSeed,
			parameters: { ...paramValues }
		};
	}

	const reuseModes: { value: BundleReuseMode; label: string; description: string }[] = [
		{
			value: 'reuse',
			label: 'Reuse',
			description: 'Reuse existing bundle when inputs match'
		},
		{
			value: 'regenerate',
			label: 'Regenerate',
			description: 'Overwrite the existing deterministic bundle'
		},
		{
			value: 'fresh',
			label: 'Fresh run',
			description: 'Create a new timestamped run'
		}
	];
</script>

<Card.Root>
	<Card.Header>
		<div class="flex items-center gap-3">
			<div class="bg-muted flex size-10 shrink-0 items-center justify-center rounded-lg">
				<Icon class="text-muted-foreground size-5" />
			</div>
			<div class="min-w-0 flex-1">
				<div class="flex items-center gap-2">
					<Card.Title>{template.title}</Card.Title>
					<Badge variant="secondary" class="text-xs">v{template.version}</Badge>
					<Badge
						variant={inferredMode === 'telemetry' ? 'default' : 'outline'}
						class="text-xs"
					>
						{inferredMode}
					</Badge>
				</div>
				{#if template.category !== 'general'}
					<p class="text-muted-foreground mt-0.5 text-xs">{template.category}</p>
				{/if}
			</div>
		</div>
	</Card.Header>

	<Card.Content class="space-y-6">
		<p class="text-sm">{template.narrative ?? template.description}</p>

		<Separator />

		<!-- Bundle Reuse Mode -->
		<div class="space-y-3">
			<span class="text-sm font-medium" id="reuse-group-label">Bundle Reuse</span>
			<RadioGroup.Root bind:value={reuseMode}>
				{#each reuseModes as mode}
					<div class="flex items-start gap-3">
						<RadioGroup.Item value={mode.value} id="reuse-{mode.value}" class="mt-0.5" />
						<label for="reuse-{mode.value}" class="cursor-pointer">
							<span class="text-sm font-medium">{mode.label}</span>
							<p class="text-muted-foreground text-xs">{mode.description}</p>
						</label>
					</div>
				{/each}
			</RadioGroup.Root>
		</div>

		<Separator />

		<!-- RNG Seed -->
		<div class="space-y-2">
			<label for="rng-seed" class="text-sm font-medium">RNG Seed</label>
			<Input
				id="rng-seed"
				type="number"
				bind:value={rngSeed}
				class="w-40"
			/>
		</div>

		<!-- Advanced Parameters -->
		{#if template.parameters.length > 0}
			<Separator />
			<Collapsible.Root bind:open={advancedOpen}>
				<Collapsible.Trigger class="flex w-full items-center gap-2 text-sm font-medium">
					<ChevronDownIcon
						class="size-4 transition-transform duration-150 {advancedOpen
							? 'rotate-0'
							: '-rotate-90'}"
					/>
					Advanced Parameters
					<Badge variant="outline" class="ml-1 text-xs">{template.parameters.length}</Badge>
				</Collapsible.Trigger>
				<Collapsible.Content>
					<div class="mt-3 space-y-4">
						{#each template.parameters as param}
							<div class="space-y-1">
								<label for="param-{param.name}" class="text-sm font-medium">
									{param.title || param.name}
								</label>
								{#if param.description}
									<p class="text-muted-foreground text-xs">{param.description}</p>
								{/if}
								{#if param.type === 'number' || param.type === 'integer' || param.type === 'float' || param.type === 'double'}
									<Input
										id="param-{param.name}"
										type="number"
										min={param.min}
										max={param.max}
										value={paramValues[param.name] as number}
										oninput={(e) => {
											paramValues[param.name] = parseFloat(
												(e.target as HTMLInputElement).value
											);
										}}
										class="w-48"
									/>
								{:else}
									<Input
										id="param-{param.name}"
										type="text"
										value={String(paramValues[param.name] ?? '')}
										oninput={(e) => {
											paramValues[param.name] = (e.target as HTMLInputElement).value;
										}}
									/>
								{/if}
							</div>
						{/each}
					</div>
				</Collapsible.Content>
			</Collapsible.Root>
		{/if}
	</Card.Content>

	<Card.Footer class="flex gap-3">
		<Button onclick={() => onrun(getConfig())} disabled={busy}>
			<PlayIcon class="mr-2 size-4" />
			Run Model
		</Button>
		<Button variant="outline" onclick={() => onpreview(getConfig())} disabled={busy}>
			<EyeIcon class="mr-2 size-4" />
			Preview
		</Button>
	</Card.Footer>
</Card.Root>
