import { get } from './client.js';
import type { ServiceInfo } from './types.js';

const API = '/api/v1';

export const sim = {
	/** Simple health check — uses the sim /api/v1/healthz to go through the sim proxy */
	async health() {
		return get<{ status: string }>(`${API}/healthz`);
	},

	/** Detailed health with service info */
	async healthDetailed() {
		return get<ServiceInfo>(`${API}/healthz?detailed=true`);
	}
};
