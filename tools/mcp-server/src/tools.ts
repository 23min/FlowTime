import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import * as z from 'zod/v4';
import type { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

export type ToolHandlers = {
  listTemplates: () => Promise<CallToolResult>;
  runTemplate: (args: RunTemplateArgs) => Promise<CallToolResult>;
  getRunSummary: (args: RunSummaryArgs) => Promise<CallToolResult>;
  getGraph: (args: GraphArgs) => Promise<CallToolResult>;
  getStateWindow: (args: StateWindowArgs) => Promise<CallToolResult>;
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
};

const TOOL_NAMES = [
  'list_templates',
  'run_template',
  'get_run_summary',
  'get_graph',
  'get_state_window'
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
        mode: z.enum(['compact', 'full']).optional()
      }
    },
    async (args) => handlers.getStateWindow(args)
  );
};
