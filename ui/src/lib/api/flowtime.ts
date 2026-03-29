import { get, getText } from './client.js';
import type {
	Artifact,
	ArtifactListResponse,
	ArtifactRelationships,
	RunDetail,
	RunSummaryResponse,
	ServiceInfo
} from './types.js';

const API = '/v1';

export const flowtime = {
	/** Simple health check */
	async health() {
		return get<{ status: string }>(`/healthz`);
	},

	/** Detailed health with service info */
	async healthDetailed() {
		return get<ServiceInfo>(`${API}/healthz?detailed=true`);
	},

	/** List runs (paginated) */
	async listRuns(page = 1, pageSize = 50) {
		return get<RunSummaryResponse>(`${API}/runs?page=${page}&pageSize=${pageSize}`);
	},

	/** Get run detail */
	async getRun(runId: string) {
		return get<RunDetail>(`${API}/runs/${encodeURIComponent(runId)}`);
	},

	/** List artifacts with optional filters */
	async listArtifacts(params?: {
		skip?: number;
		limit?: number;
		type?: string;
		search?: string;
		sortBy?: string;
		sortOrder?: string;
	}) {
		const qs = new URLSearchParams();
		if (params?.skip) qs.set('skip', String(params.skip));
		if (params?.limit) qs.set('limit', String(params.limit));
		if (params?.type) qs.set('type', params.type);
		if (params?.search) qs.set('search', params.search);
		if (params?.sortBy) qs.set('sortBy', params.sortBy);
		if (params?.sortOrder) qs.set('sortOrder', params.sortOrder);
		const q = qs.toString();
		return get<ArtifactListResponse>(`${API}/artifacts${q ? `?${q}` : ''}`);
	},

	/** Get single artifact detail */
	async getArtifact(id: string) {
		return get<Artifact>(`${API}/artifacts/${encodeURIComponent(id)}`);
	},

	/** Get artifact relationships */
	async getArtifactRelationships(id: string) {
		return get<ArtifactRelationships>(
			`${API}/artifacts/${encodeURIComponent(id)}/relationships`
		);
	},

	/** Get artifact file content as text */
	async getArtifactFile(id: string, fileName: string) {
		return getText(
			`${API}/artifacts/${encodeURIComponent(id)}/files/${encodeURIComponent(fileName)}`
		);
	}
};
