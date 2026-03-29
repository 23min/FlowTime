<script lang="ts">
	import { page } from '$app/stores';
	import * as Breadcrumb from '$lib/components/ui/breadcrumb/index.js';

	const labels: Record<string, string> = {
		'time-travel': 'Time Travel',
		topology: 'Topology',
		dashboard: 'Dashboard',
		artifacts: 'Artifacts',
		run: 'Run Model',
		health: 'Health'
	};

	function getSegments(pathname: string) {
		const parts = pathname.split('/').filter(Boolean);
		return parts.map((part, i) => ({
			label: labels[part] ?? part,
			href: '/' + parts.slice(0, i + 1).join('/')
		}));
	}

	const segments = $derived(getSegments($page.url.pathname));
</script>

<Breadcrumb.Root>
	<Breadcrumb.List>
		<Breadcrumb.Item>
			<Breadcrumb.Link href="/">Home</Breadcrumb.Link>
		</Breadcrumb.Item>
		{#each segments as segment}
			<Breadcrumb.Separator />
			<Breadcrumb.Item>
				<Breadcrumb.Link href={segment.href}>{segment.label}</Breadcrumb.Link>
			</Breadcrumb.Item>
		{/each}
	</Breadcrumb.List>
</Breadcrumb.Root>
