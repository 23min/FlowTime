import { get, post } from './client.js';
import type {
	RunCreateRequest,
	RunCreateResponse,
	ServiceInfo,
	TemplateCategoriesResponse,
	TemplateDetail,
	TemplateSummary
} from './types.js';

const API = '/api/v1';

export const sim = {
	/** Simple health check — uses the sim /api/v1/healthz to go through the sim proxy */
	async health() {
		return get<{ status: string }>(`${API}/healthz`);
	},

	/** Detailed health with service info */
	async healthDetailed() {
		return get<ServiceInfo>(`${API}/healthz?detailed=true`);
	},

	/** List all templates, optionally filtered by category */
	async listTemplates(category?: string) {
		const qs = category ? `?category=${encodeURIComponent(category)}` : '';
		return get<TemplateSummary[]>(`${API}/templates${qs}`);
	},

	/** Get template detail including parameters */
	async getTemplate(id: string) {
		return get<TemplateDetail>(`${API}/templates/${encodeURIComponent(id)}`);
	},

	/** List available template categories */
	async getCategories() {
		return get<TemplateCategoriesResponse>(`${API}/templates/categories`);
	},

	/** Create a run (or dry-run preview) via orchestration */
	async createRun(request: RunCreateRequest) {
		return post<RunCreateResponse>(`${API}/orchestration/runs`, request);
	}
};
