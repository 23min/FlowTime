import path from 'node:path';
import { randomInt } from 'node:crypto';
import type { CallToolResult } from '@modelcontextprotocol/sdk/types.js';
import type { ServerConfig } from './config.js';
import type {
  ToolHandlers,
  RunTemplateArgs,
  RunDraftArgs,
  RunSummaryArgs,
  GraphArgs,
  StateWindowArgs,
  CreateDraftArgs,
  DraftPatchArgs,
  DraftIdArgs,
  ValidateDraftArgs,
  GenerateModelArgs
} from './tools.js';
import { fetchJson, HttpError } from './http.js';
import { assertBinWindow } from './guards.js';
import { DraftStore } from './drafts.js';

type RunCreateResponse = {
  isDryRun: boolean;
  metadata?: {
    runId?: string;
    templateId?: string;
    mode?: string;
  };
  plan?: unknown;
  warnings?: unknown[];
  canReplay?: boolean;
  telemetry?: unknown;
};

const toToolResult = (payload: Record<string, unknown>): CallToolResult => ({
  content: [
    {
      type: 'text',
      text: JSON.stringify(payload, null, 2)
    }
  ],
  structuredContent: payload
});

const buildBundlePath = (config: ServerConfig, runId: string): string =>
  path.join(config.dataDir, 'runs', runId);

const enforceSimulationMode = (mode: string | undefined): string => {
  const resolved = mode ?? 'simulation';
  if (resolved !== 'simulation') {
    throw new Error('Only simulation mode is supported in the M-07.01 PoC.');
  }
  return resolved;
};

const buildRunOptions = (args: RunTemplateArgs) => ({
  deterministicRunId: args.deterministicRunId ?? false,
  runId: args.runId,
  overwriteExisting: args.overwriteExisting ?? false,
  dryRun: args.dryRun ?? false
});

const defaultRngSeed = (): number => randomInt(1, 2_147_483_647);

const buildRng = (
  args: RunTemplateArgs
): { rng: { kind: string; seed: number }; seed: number; kind: string } => {
  const kind = args.rngKind ?? 'pcg32';
  const seed = typeof args.rngSeed === 'number' ? args.rngSeed : defaultRngSeed();
  return { rng: { kind, seed }, seed, kind };
};

const assertParameterBins = (parameters: RunTemplateArgs['parameters'], maxBins: number): void => {
  if (!parameters || typeof parameters !== 'object') {
    return;
  }
  const candidate = (parameters as Record<string, unknown>).bins;
  if (typeof candidate === 'number' && candidate > maxBins) {
    throw new Error(`Requested bins ${candidate} exceeds maxBins ${maxBins}.`);
  }
};

const listTemplates = async (config: ServerConfig): Promise<CallToolResult> => {
  const templates = await fetchJson<unknown[]>(
    `${config.simApiUrl}/templates`,
    { method: 'GET' },
    config.requestTimeoutMs
  );

  return toToolResult({ templates });
};

const runTemplateWithSim = async (
  config: ServerConfig,
  templateId: string,
  args: RunTemplateArgs | RunDraftArgs,
  simApiUrl: string
): Promise<CallToolResult> => {
  const mode = enforceSimulationMode(args.mode);
  assertParameterBins(args.parameters, config.maxBins);

  const rngResult = buildRng(args);
  const requestBody = {
    templateId,
    mode,
    parameters: args.parameters,
    options: buildRunOptions(args),
    rng: rngResult.rng
  };

  const orchestration = await fetchJson<RunCreateResponse>(
    `${simApiUrl}/orchestration/runs`,
    {
      method: 'POST',
      body: JSON.stringify(requestBody)
    },
    config.orchestrationTimeoutMs
  );

  if (orchestration.isDryRun) {
    return toToolResult({
      dryRun: true,
      plan: orchestration.plan ?? null,
      warnings: orchestration.warnings ?? [],
      rngSeed: rngResult.seed,
      rngKind: rngResult.kind
    });
  }

  const runId = orchestration.metadata?.runId;
  if (!runId) {
    throw new Error('Sim orchestration response missing runId.');
  }

  const bundlePath = buildBundlePath(config, runId);
  let importStatus = 'imported';
  let importError: unknown = null;

  try {
    await fetchJson<RunCreateResponse>(
      `${config.engineApiUrl}/runs`,
      {
        method: 'POST',
        body: JSON.stringify({
          bundlePath,
          overwriteExisting: args.overwriteExisting ?? false
        })
      },
      config.requestTimeoutMs
    );
  } catch (error) {
    if (error instanceof HttpError && error.status === 409) {
      importStatus = 'already_exists';
      importError = error.payload;
    } else {
      throw error;
    }
  }

  return toToolResult({
    runId,
    bundlePath,
    warnings: orchestration.warnings ?? [],
    telemetry: orchestration.telemetry ?? null,
    rngSeed: rngResult.seed,
    rngKind: rngResult.kind,
    importStatus,
    importError
  });
};

const runTemplate = async (config: ServerConfig, args: RunTemplateArgs): Promise<CallToolResult> =>
  runTemplateWithSim(config, args.templateId, args, config.simApiUrl);

const runDraft = async (config: ServerConfig, args: RunDraftArgs): Promise<CallToolResult> => {
  const rngResult = buildRng(args);
  const requestBody = {
    source: { type: 'draftId', id: args.draftId },
    mode: enforceSimulationMode(args.mode),
    parameters: args.parameters,
    options: buildRunOptions(args),
    rng: rngResult.rng
  };

  const orchestration = await fetchJson<RunCreateResponse>(
    `${config.draftSimApiUrl}/drafts/run`,
    {
      method: 'POST',
      body: JSON.stringify(requestBody)
    },
    config.orchestrationTimeoutMs
  );

  if (orchestration.isDryRun) {
    return toToolResult({
      dryRun: true,
      plan: orchestration.plan ?? null,
      warnings: orchestration.warnings ?? [],
      rngSeed: rngResult.seed,
      rngKind: rngResult.kind
    });
  }

  const runId = orchestration.metadata?.runId;
  if (!runId) {
    throw new Error('Sim orchestration response missing runId.');
  }

  const bundlePath = buildBundlePath(config, runId);
  let importStatus = 'imported';
  let importError: unknown = null;

  try {
    await fetchJson<RunCreateResponse>(
      `${config.engineApiUrl}/runs`,
      {
        method: 'POST',
        body: JSON.stringify({
          bundlePath,
          overwriteExisting: args.overwriteExisting ?? false
        })
      },
      config.requestTimeoutMs
    );
  } catch (error) {
    if (error instanceof HttpError && error.status === 409) {
      importStatus = 'already_exists';
      importError = error.payload;
    } else {
      throw error;
    }
  }

  return toToolResult({
    runId,
    bundlePath,
    warnings: orchestration.warnings ?? [],
    telemetry: orchestration.telemetry ?? null,
    rngSeed: rngResult.seed,
    rngKind: rngResult.kind,
    importStatus,
    importError
  });
};

const getRunSummary = async (config: ServerConfig, args: RunSummaryArgs): Promise<CallToolResult> => {
  const summary = await fetchJson<RunCreateResponse>(
    `${config.engineApiUrl}/runs/${args.runId}`,
    { method: 'GET' },
    config.requestTimeoutMs
  );

  return toToolResult({ summary });
};

const getGraph = async (config: ServerConfig, args: GraphArgs): Promise<CallToolResult> => {
  const graph = await fetchJson<unknown>(
    `${config.engineApiUrl}/runs/${args.runId}/graph`,
    { method: 'GET' },
    config.requestTimeoutMs
  );

  return toToolResult({ graph });
};

const getStateWindow = async (config: ServerConfig, args: StateWindowArgs): Promise<CallToolResult> => {
  assertBinWindow(args.startBin, args.endBin, config.maxBins);

  const params = new URLSearchParams({
    startBin: String(args.startBin),
    endBin: String(args.endBin)
  });

  if (args.mode) {
    params.set('mode', args.mode);
  }

  const stateWindow = await fetchJson<unknown>(
    `${config.engineApiUrl}/runs/${args.runId}/state_window?${params.toString()}`,
    { method: 'GET' },
    config.requestTimeoutMs
  );

  return toToolResult({ stateWindow });
};

const listDrafts = async (store: DraftStore): Promise<CallToolResult> => {
  const drafts = await store.listDrafts();
  return toToolResult({ drafts });
};

const createDraft = async (store: DraftStore, args: CreateDraftArgs): Promise<CallToolResult> => {
  const draft = await store.createDraft(args);
  return toToolResult({ draft });
};

const getDraft = async (store: DraftStore, args: DraftIdArgs): Promise<CallToolResult> => {
  const draft = await store.getDraft(args.draftId);
  return toToolResult({ draft });
};

const applyDraftPatch = async (store: DraftStore, args: DraftPatchArgs): Promise<CallToolResult> => {
  const draft = await store.applyDraftPatch(args);
  return toToolResult({ draft });
};

const diffDraft = async (store: DraftStore, args: DraftIdArgs): Promise<CallToolResult> => {
  const diff = await store.diffDraft(args.draftId);
  return toToolResult({ diff });
};

const validateDraft = async (config: ServerConfig, args: ValidateDraftArgs): Promise<CallToolResult> => {
  const response = await fetchJson<{
    valid?: boolean;
    metadata?: unknown;
    warnings?: unknown[];
  }>(
    `${config.draftSimApiUrl}/drafts/validate`,
    {
      method: 'POST',
      body: JSON.stringify({
        source: { type: 'draftId', id: args.draftId },
        parameters: args.parameters ?? {},
        mode: args.mode
      })
    },
    config.requestTimeoutMs
  );

  return toToolResult({
    valid: response.valid ?? true,
    metadata: response.metadata ?? null,
    warnings: response.warnings ?? []
  });
};

const generateModel = async (config: ServerConfig, args: GenerateModelArgs): Promise<CallToolResult> => {
  const response = await fetchJson<unknown>(
    `${config.draftSimApiUrl}/drafts/generate`,
    {
      method: 'POST',
      body: JSON.stringify({
        source: { type: 'draftId', id: args.draftId },
        parameters: args.parameters ?? {},
        mode: args.mode
      })
    },
    config.requestTimeoutMs
  );

  return toToolResult({ model: response });
};

export const createHandlers = (config: ServerConfig): ToolHandlers => {
  const draftStore = new DraftStore({
    templatesDir: config.templatesDir,
    draftsDir: config.draftsDir
  });

  return {
    listTemplates: () => listTemplates(config),
    runTemplate: (args) => runTemplate(config, args),
    getRunSummary: (args) => getRunSummary(config, args),
    getGraph: (args) => getGraph(config, args),
    getStateWindow: (args) => getStateWindow(config, args),
    listDrafts: () => listDrafts(draftStore),
    createDraft: (args) => createDraft(draftStore, args),
    getDraft: (args) => getDraft(draftStore, args),
    applyDraftPatch: (args) => applyDraftPatch(draftStore, args),
    diffDraft: (args) => diffDraft(draftStore, args),
    validateDraft: (args) => validateDraft(config, args),
    generateModel: (args) => generateModel(config, args),
    runDraft: (args) => runDraft(config, args)
  };
};
