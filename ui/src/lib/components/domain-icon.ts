import type { Component } from 'svelte';
import type { TemplateSummary } from '$lib/api/types.js';
import FactoryIcon from '@lucide/svelte/icons/factory';
import HospitalIcon from '@lucide/svelte/icons/hospital';
import ShoppingCartIcon from '@lucide/svelte/icons/shopping-cart';
import ServerIcon from '@lucide/svelte/icons/server';
import NetworkIcon from '@lucide/svelte/icons/network';
import TruckIcon from '@lucide/svelte/icons/truck';
import WarehouseIcon from '@lucide/svelte/icons/warehouse';
import FlaskConicalIcon from '@lucide/svelte/icons/flask-conical';
import BuildingIcon from '@lucide/svelte/icons/building';
import BoxesIcon from '@lucide/svelte/icons/boxes';

const DOMAIN_KEYWORDS: [string[], Component][] = [
	[['hospital', 'health', 'medical', 'clinic', 'patient', 'pharma'], HospitalIcon],
	[['factory', 'manufacturing', 'assembly', 'production', 'plant'], FactoryIcon],
	[['retail', 'shop', 'store', 'cart', 'ecommerce', 'pos'], ShoppingCartIcon],
	[['logistics', 'shipping', 'transport', 'delivery', 'freight'], TruckIcon],
	[['warehouse', 'inventory', 'storage', 'fulfillment'], WarehouseIcon],
	[['server', 'infrastructure', 'devops', 'deploy', 'cloud'], ServerIcon],
	[['network', 'topology', 'telecom', 'routing'], NetworkIcon],
	[['lab', 'experiment', 'research', 'science', 'test'], FlaskConicalIcon],
	[['office', 'enterprise', 'corporate', 'business'], BuildingIcon]
];

export function getDomainIcon(template: TemplateSummary): Component {
	const searchText = [template.category, template.title, template.description, ...template.tags]
		.join(' ')
		.toLowerCase();
	for (const [keywords, icon] of DOMAIN_KEYWORDS) {
		if (keywords.some((kw) => searchText.includes(kw))) return icon;
	}
	return BoxesIcon;
}
