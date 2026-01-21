import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import * as z from 'zod/v4';
import type { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

export type ToolHandlers = {
  listTemplates: () => Promise<CallToolResult>;
  runTemplate: (args: RunTemplateArgs) => Promise<CallToolResult>;
  getRunSummary: (args: RunSummaryArgs) => Promise<CallToolResult>;
  getGraph: (args: GraphArgs) => Promise<CallToolResult>;
  getStateWindow: (args: StateWindowArgs) => Promise<CallToolResult>;
  listDrafts: () => Promise<CallToolResult>;
  createDraft: (args: CreateDraftArgs) => Promise<CallToolResult>;
  getDraft: (args: DraftIdArgs) => Promise<CallToolResult>;
  applyDraftPatch: (args: DraftPatchArgs) => Promise<CallToolResult>;
  diffDraft: (args: DraftIdArgs) => Promise<CallToolResult>;
  validateDraft: (args: ValidateDraftArgs) => Promise<CallToolResult>;
  generateModel: (args: GenerateModelArgs) => Promise<CallToolResult>;
  runDraft: (args: RunDraftArgs) => Promise<CallToolResult>;
  ingestSeries: (args: IngestSeriesArgs) => Promise<CallToolResult>;
  summarizeSeries: (args: SummarizeSeriesArgs) => Promise<CallToolResult>;
  fitProfile: (args: FitProfileArgs) => Promise<CallToolResult>;
  previewProfile: (args: PreviewProfileArgs) => Promise<CallToolResult>;
  mapSeriesToInputs: (args: MapSeriesToInputsArgs) => Promise<CallToolResult>;
};

export type RunTemplateArgs = {
  templateId: string;
  mode?: 'simulation';
  parameters?: Record<string, unknown>;
  rngSeed?: number;
  rngKind?: string;
  deterministicRunId?: boolean;
  runId?: string;
  overwriteExisting?: boolean;
  dryRun?: boolean;
};

export type RunDraftArgs = {
  draftId: string;
  mode?: 'simulation';
  parameters?: Record<string, unknown>;
  rngSeed?: number;
  rngKind?: string;
  deterministicRunId?: boolean;
  runId?: string;
  overwriteExisting?: boolean;
  dryRun?: boolean;
};

export type RunSummaryArgs = {
  runId: string;
};

export type GraphArgs = {
  runId: string;
};

export type StateWindowArgs = {
  runId: string;
  startBin: number;
  endBin: number;
  mode?: 'compact' | 'full';
  edgeIds?: string[];
  edgeMetrics?: string[];
  classIds?: string[];
};

export type DraftIdArgs = {
  draftId: string;
};

export type CreateDraftArgs = {
  draftId?: string;
  baseTemplateId?: string;
  content?: string;
};

export type DraftPatchArgs = {
  draftId: string;
  content: string;
  expectedHash?: string;
};

export type ValidateDraftArgs = {
  draftId: string;
  parameters?: Record<string, unknown>;
  mode?: 'simulation' | 'telemetry';
};

export type GenerateModelArgs = {
  draftId: string;
  parameters?: Record<string, unknown>;
  mode?: 'simulation' | 'telemetry';
};

export type SeriesMetadata = {
  units?: string;
  source?: string;
  binSize?: number;
  binUnit?: string;
  timezone?: string;
  timeRange?: {
    start?: string;
    end?: string;
  };
};

export type IngestSeriesArgs = {
  seriesId?: string;
  format?: 'csv' | 'table';
  content: string;
  metadata?: SeriesMetadata;
  detailLevel?: 'basic' | 'expert';
};

export type SummarizeSeriesArgs = {
  seriesId: string;
  detailLevel?: 'basic' | 'expert';
};

export type ProfileSummaryStats = {
  min?: number;
  max?: number;
  avg?: number;
  p50?: number;
  p90?: number;
  p95?: number;
  p99?: number;
  count?: number;
  peakBin?: number;
};

export type ProfileSpec = {
  kind: 'inline';
  weights: number[];
};

export type PmfSpec = {
  values: number[];
  probabilities: number[];
};

export type FitProfileArgs = {
  mode?: 'profile' | 'pmf';
  seriesId?: string;
  samples?: number[];
  summary?: ProfileSummaryStats;
  bins?: number;
  detailLevel?: 'basic' | 'expert';
};

export type PreviewProfileArgs = {
  profile?: ProfileSpec;
  pmf?: PmfSpec;
  detailLevel?: 'basic' | 'expert';
};

export type MapSeriesToInputsArgs = {
  draftId: string;
  nodeId: string;
  profile?: ProfileSpec;
  pmf?: PmfSpec;
  provenance?: Record<string, string>;
  persist?: boolean;
  detailLevel?: 'basic' | 'expert';
};

const TOOL_NAMES = [
  'list_templates',
  'run_template',
  'get_run_summary',
  'get_graph',
  'get_state_window',
  'list_drafts',
  'create_draft',
  'get_draft',
  'apply_draft_patch',
  'diff_draft',
  'validate_draft',
  'generate_model',
  'run_draft',
  'ingest_series',
  'summarize_series',
  'fit_profile',
  'preview_profile',
  'map_series_to_inputs'
] as const;

export const getToolNames = (): string[] => [...TOOL_NAMES];

export const registerTools = (server: McpServer, handlers: ToolHandlers): void => {
  server.registerTool(
    'list_templates',
    {
      description: 'List approved FlowTime templates from FlowTime.Sim.Service.',
      inputSchema: {},
      outputSchema: {
        templates: z.array(z.record(z.string(), z.unknown()))
      }
    },
    async () => handlers.listTemplates()
  );

  server.registerTool(
    'run_template',
    {
      description: 'Run a FlowTime template via FlowTime.Sim.Service and import it into the FlowTime API.',
      inputSchema: {
        templateId: z.string().min(1),
        mode: z.literal('simulation').optional(),
        parameters: z.record(z.string(), z.unknown()).optional(),
        rngSeed: z.number().int().optional(),
        rngKind: z.string().optional(),
        deterministicRunId: z.boolean().optional(),
        runId: z.string().optional(),
        overwriteExisting: z.boolean().optional(),
        dryRun: z.boolean().optional()
      }
    },
    async (args) => handlers.runTemplate(args)
  );

  server.registerTool(
    'get_run_summary',
    {
      description: 'Fetch run metadata and warnings for a runId from the FlowTime API.',
      inputSchema: {
        runId: z.string().min(1)
      }
    },
    async (args) => handlers.getRunSummary(args)
  );

  server.registerTool(
    'get_graph',
    {
      description: 'Fetch graph metadata for a runId from the FlowTime API.',
      inputSchema: {
        runId: z.string().min(1)
      }
    },
    async (args) => handlers.getGraph(args)
  );

  server.registerTool(
    'get_state_window',
    {
      description: 'Fetch a state window for a runId from the FlowTime API.',
      inputSchema: {
        runId: z.string().min(1),
        startBin: z.number().int(),
        endBin: z.number().int(),
        mode: z.enum(['compact', 'full']).optional(),
        edgeIds: z.array(z.string().min(1)).min(1).optional(),
        edgeMetrics: z.array(z.string().min(1)).min(1).optional(),
        classIds: z.array(z.string().min(1)).min(1).optional()
      }
    },
    async (args) => handlers.getStateWindow(args)
  );

  server.registerTool(
    'list_drafts',
    {
      description: 'List draft templates from FlowTime.Sim.Service storage.',
      inputSchema: {},
      outputSchema: {
        drafts: z.array(z.record(z.string(), z.unknown()))
      }
    },
    async () => handlers.listDrafts()
  );

  server.registerTool(
    'create_draft',
    {
      description: 'Create a draft template via FlowTime.Sim.Service storage.',
      inputSchema: {
        draftId: z.string().min(1).optional(),
        baseTemplateId: z.string().min(1).optional(),
        content: z.string().min(1).optional()
      }
    },
    async (args) => handlers.createDraft(args)
  );

  server.registerTool(
    'get_draft',
    {
      description: 'Fetch a draft template and metadata by draftId.',
      inputSchema: {
        draftId: z.string().min(1)
      }
    },
    async (args) => handlers.getDraft(args)
  );

  server.registerTool(
    'apply_draft_patch',
    {
      description: 'Replace draft content with concurrency protection via expected content hash.',
      inputSchema: {
        draftId: z.string().min(1),
        content: z.string().min(1),
        expectedHash: z.string().min(1).optional()
      }
    },
    async (args) => handlers.applyDraftPatch(args)
  );

  server.registerTool(
    'diff_draft',
    {
      description: 'Return a unified diff between a draft and its base template.',
      inputSchema: {
        draftId: z.string().min(1)
      }
    },
    async (args) => handlers.diffDraft(args)
  );

  server.registerTool(
    'validate_draft',
    {
      description: 'Validate a draft template via FlowTime.Sim.Service analyzers.',
      inputSchema: {
        draftId: z.string().min(1),
        parameters: z.record(z.string(), z.unknown()).optional(),
        mode: z.enum(['simulation', 'telemetry']).optional()
      }
    },
    async (args) => handlers.validateDraft(args)
  );

  server.registerTool(
    'generate_model',
    {
      description: 'Generate a model from a draft template via FlowTime.Sim.Service.',
      inputSchema: {
        draftId: z.string().min(1),
        parameters: z.record(z.string(), z.unknown()).optional(),
        mode: z.enum(['simulation', 'telemetry']).optional()
      }
    },
    async (args) => handlers.generateModel(args)
  );

  server.registerTool(
    'run_draft',
    {
      description: 'Run a draft template via FlowTime.Sim.Service and import it into the FlowTime API.',
      inputSchema: {
        draftId: z.string().min(1),
        mode: z.literal('simulation').optional(),
        parameters: z.record(z.string(), z.unknown()).optional(),
        rngSeed: z.number().int().optional(),
        rngKind: z.string().optional(),
        deterministicRunId: z.boolean().optional(),
        runId: z.string().optional(),
        overwriteExisting: z.boolean().optional(),
        dryRun: z.boolean().optional()
      }
    },
    async (args) => handlers.runDraft(args)
  );

  server.registerTool(
    'ingest_series',
    {
      description: 'Ingest a pre-aggregated series (CSV or pasted table) into FlowTime.Sim.Service.',
      inputSchema: {
        seriesId: z.string().min(1).optional(),
        format: z.enum(['csv', 'table']).optional(),
        content: z.string().min(1),
        detailLevel: z.enum(['basic', 'expert']).optional(),
        metadata: z
          .object({
            units: z.string().optional(),
            source: z.string().optional(),
            binSize: z.number().int().positive().optional(),
            binUnit: z.string().optional(),
            timezone: z.string().optional(),
            timeRange: z
              .object({
                start: z.string().optional(),
                end: z.string().optional()
              })
              .optional()
          })
          .optional()
      }
    },
    async (args) => handlers.ingestSeries(args)
  );

  server.registerTool(
    'summarize_series',
    {
      description: 'Summarize a stored series (min/avg/percentiles/periodicity) from FlowTime.Sim.Service.',
      inputSchema: {
        seriesId: z.string().min(1),
        detailLevel: z.enum(['basic', 'expert']).optional()
      }
    },
    async (args) => handlers.summarizeSeries(args)
  );

  server.registerTool(
    'fit_profile',
    {
      description: 'Fit a profile or PMF from samples, summary stats, or an ingested series.',
      inputSchema: {
        mode: z.enum(['profile', 'pmf']).optional(),
        seriesId: z.string().min(1).optional(),
        samples: z.array(z.number()).optional(),
        summary: z
          .object({
            min: z.number().optional(),
            max: z.number().optional(),
            avg: z.number().optional(),
            p50: z.number().optional(),
            p90: z.number().optional(),
            p95: z.number().optional(),
            p99: z.number().optional(),
            count: z.number().int().optional(),
            peakBin: z.number().int().optional()
          })
          .optional(),
        bins: z.number().int().optional(),
        detailLevel: z.enum(['basic', 'expert']).optional()
      }
    },
    async (args) => handlers.fitProfile(args)
  );

  server.registerTool(
    'preview_profile',
    {
      description: 'Preview a profile or PMF before applying it to a draft.',
      inputSchema: {
        profile: z
          .object({
            kind: z.literal('inline'),
            weights: z.array(z.number())
          })
          .optional(),
        pmf: z
          .object({
            values: z.array(z.number()),
            probabilities: z.array(z.number())
          })
          .optional(),
        detailLevel: z.enum(['basic', 'expert']).optional()
      }
    },
    async (args) => handlers.previewProfile(args)
  );

  server.registerTool(
    'map_series_to_inputs',
    {
      description: 'Apply a fitted profile or PMF to a draft node.',
      inputSchema: {
        draftId: z.string().min(1),
        nodeId: z.string().min(1),
        profile: z
          .object({
            kind: z.literal('inline'),
            weights: z.array(z.number())
          })
          .optional(),
        pmf: z
          .object({
            values: z.array(z.number()),
            probabilities: z.array(z.number())
          })
          .optional(),
        provenance: z.record(z.string(), z.string()).optional(),
        persist: z.boolean().optional(),
        detailLevel: z.enum(['basic', 'expert']).optional()
      }
    },
    async (args) => handlers.mapSeriesToInputs(args)
  );
};
