import { get, getText, post } from './client.js';
import type {
	Artifact,
	ArtifactListResponse,
	ArtifactRelationships,
	GraphResponse,
	RunDetail,
	RunIndex,
	RunSummaryResponse,
	ServiceInfo,
	StateSnapshotResponse,
	StateWindowResponse
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
	},

	/** Get run dependency graph */
	async getGraph(runId: string, mode?: string) {
		const qs = mode ? `?mode=${mode}` : '';
		return get<GraphResponse>(`${API}/runs/${encodeURIComponent(runId)}/graph${qs}`);
	},

	/** Get state snapshot at a specific bin */
	async getState(runId: string, binIndex: number) {
		return get<StateSnapshotResponse>(
			`${API}/runs/${encodeURIComponent(runId)}/state?binIndex=${binIndex}`
		);
	},

	/** Get run series index (bin count, grid info) */
	async getRunIndex(runId: string) {
		return get<RunIndex>(`${API}/runs/${encodeURIComponent(runId)}/index`);
	},

	/** Get the compiled model YAML for a run */
	async getRunModel(runId: string) {
		return getText(`${API}/runs/${encodeURIComponent(runId)}/model`);
	},

	/** Get state window (range of bins) */
	async getStateWindow(runId: string, startBin: number, endBin: number) {
		return get<StateWindowResponse>(
			`${API}/runs/${encodeURIComponent(runId)}/state_window?startBin=${startBin}&endBin=${endBin}`
		);
	},

	/** Parameter sweep — POST /v1/sweep */
	async sweep(body: {
		yaml: string;
		paramId: string;
		values: number[];
		captureSeriesIds?: string[];
	}) {
		return post<{
			paramId: string;
			points: { paramValue: number; series: Record<string, number[]> }[];
		}>(`${API}/sweep`, body);
	},

	/** Sensitivity analysis — POST /v1/sensitivity */
	async sensitivity(body: {
		yaml: string;
		paramIds: string[];
		metricSeriesId: string;
		perturbation?: number;
	}) {
		return post<{
			metricSeriesId: string;
			points: { paramId: string; baseValue: number; gradient: number }[];
		}>(`${API}/sensitivity`, body);
	},

	/** Goal seek — POST /v1/goal-seek */
	async goalSeek(body: {
		yaml: string;
		paramId: string;
		metricSeriesId: string;
		target: number;
		searchLo: number;
		searchHi: number;
		tolerance?: number;
		maxIterations?: number;
	}) {
		return post<{
			paramValue: number;
			achievedMetricMean: number;
			converged: boolean;
			iterations: number;
			trace: {
				iteration: number;
				paramValue: number;
				metricMean: number;
				searchLo: number;
				searchHi: number;
			}[];
		}>(`${API}/goal-seek`, body);
	}
};
