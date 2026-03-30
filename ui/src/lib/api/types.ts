// Shared API types aligned with FlowTime backend contracts

export interface ApiResult<T> {
	value?: T;
	success: boolean;
	statusCode: number;
	error?: string;
}

// Health
export interface ServiceInfo {
	serviceName: string;
	apiVersion: string;
	build: {
		version: string;
		commitHash?: string;
		buildTime?: string;
		environment: string;
	};
	status: string;
	startTime: string;
	uptime: string;
}

// Runs
export interface RunSummary {
	runId: string;
	templateId: string;
	templateTitle?: string;
	templateNarrative?: string;
	templateVersion?: string;
	mode: string;
	createdUtc?: string;
	warningCount: number;
	telemetry?: {
		available: boolean;
		generatedAtUtc?: string;
		warningCount: number;
		sourceRunId?: string;
	};
	rng?: { kind: string; seed: number };
	inputHash?: string;
	classes?: string[];
	classCoverage?: string;
}

export interface RunSummaryResponse {
	items: RunSummary[];
	totalCount: number;
	page: number;
	pageSize: number;
}

export interface StateMetadata {
	runId: string;
	templateId: string;
	templateTitle?: string;
	templateNarrative?: string;
	templateVersion?: string;
	mode: string;
	provenanceHash?: string;
	schema: { id: string; version: string; hash: string };
	edgeQuality: string;
	rng?: { kind: string; seed: number };
	inputHash?: string;
	classCoverage?: string;
}

export interface StateWarning {
	code: string;
	message: string;
	nodeId?: string;
	bins?: number[];
}

export interface RunDetail {
	isDryRun: boolean;
	metadata: StateMetadata;
	warnings: StateWarning[];
	canReplay?: boolean;
	telemetry?: {
		available: boolean;
		generatedAtUtc?: string;
		warningCount: number;
		sourceRunId?: string;
		supportsClassMetrics?: boolean;
		classCoverage?: string;
		classes?: string[];
	};
	wasReused: boolean;
}

// Artifacts
export interface Artifact {
	id: string;
	type: string;
	title: string;
	created: string;
	tags: string[];
	metadata: Record<string, unknown>;
	files: string[];
	totalSize: number;
	lastModified: string;
}

export interface ArtifactListResponse {
	artifacts: Artifact[];
	total: number;
	count: number;
}

export interface ArtifactRelationships {
	artifactId: string;
	derivedFrom: ArtifactReference[];
	derivatives: ArtifactReference[];
	related: ArtifactReference[];
}

export interface ArtifactReference {
	id: string;
	type: string;
	title: string;
	relationshipType?: string;
}

// Graph
export interface GraphResponse {
	nodes: GraphNode[];
	edges: GraphEdge[];
	order: string[];
}

export interface GraphNode {
	id: string;
	kind: string;
	[key: string]: unknown;
}

export interface GraphEdge {
	id: string;
	from: string;
	to: string;
	[key: string]: unknown;
}

// State
export interface StateSnapshotResponse {
	metadata: StateMetadata;
	bin: { index: number; startUtc?: string; endUtc?: string; durationMinutes: number };
	nodes: Record<string, unknown>[];
	edges: Record<string, unknown>[];
	warnings: StateWarning[];
}

export interface StateWindowResponse {
	metadata: StateMetadata;
	window: { startBin: number; endBin: number; binCount: number };
	timestampsUtc: string[];
	nodes: Record<string, unknown>[];
	edges: Record<string, unknown>[];
	warnings: StateWarning[];
}
