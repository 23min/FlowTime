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

// Run Index
export interface RunIndex {
	schemaVersion: number;
	grid: { bins: number; binSize: number; binUnit: string; timezone?: string };
	series: { id: string; kind: string; path: string; unit: string; componentId: string }[];
	classes?: { id: string; displayName?: string }[];
	classCoverage?: string;
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

// Templates
export interface TemplateSummary {
	id: string;
	title: string;
	description: string;
	narrative?: string;
	category: string;
	tags: string[];
	version: string;
	captureKey?: string;
}

export interface TemplateParameter {
	name: string;
	type: string;
	title: string;
	description: string;
	defaultValue?: unknown;
	min?: number;
	max?: number;
}

export interface TemplateDetail extends TemplateSummary {
	parameters: TemplateParameter[];
}

export interface TemplateCategoriesResponse {
	categories: string[];
}

// Run Orchestration
export type BundleReuseMode = 'reuse' | 'regenerate' | 'fresh';

export interface RunCreateRequest {
	templateId: string;
	mode: string;
	parameters?: Record<string, unknown>;
	telemetry?: {
		captureDirectory: string;
		bindings?: Record<string, string>;
	};
	rng?: { kind: string; seed: number };
	options?: {
		dryRun?: boolean;
		deterministicRunId?: boolean;
		runId?: string;
		overwriteExisting?: boolean;
	};
}

export interface RunCreateResponse {
	isDryRun: boolean;
	metadata?: StateMetadata;
	plan?: RunCreatePlan;
	warnings: StateWarning[];
	canReplay?: boolean;
	telemetry?: {
		available: boolean;
		generatedAtUtc?: string;
		warningCount: number;
		sourceRunId?: string;
	};
	bundleRef?: { kind: string; id: string };
	wasReused: boolean;
}

export interface RunCreatePlan {
	templateId: string;
	mode: string;
	outputRoot: string;
	captureDirectory?: string;
	deterministicRunId: boolean;
	requestedRunId?: string;
	parameters: Record<string, unknown>;
	telemetryBindings: Record<string, string>;
	files: { nodeId: string; metric: string; path: string }[];
	warnings: { code: string; message: string; nodeId?: string }[];
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
