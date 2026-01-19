import path from 'node:path';

export type ServerConfig = {
  simApiUrl: string;
  engineApiUrl: string;
  dataDir: string;
  maxBins: number;
  requestTimeoutMs: number;
  orchestrationTimeoutMs: number;
};

const DEFAULTS = {
  simApiUrl: 'http://localhost:8090/api/v1',
  engineApiUrl: 'http://localhost:8080/v1',
  dataDir: 'data',
  maxBins: 1000,
  requestTimeoutMs: 30000,
  orchestrationTimeoutMs: 120000
};

const normalizeUrl = (value: string, label: string): string => {
  try {
    const url = new URL(value);
    return url.toString().replace(/\/$/, '');
  } catch (error) {
    throw new Error(`${label} must be a valid URL.`);
  }
};

const parsePositiveInt = (value: string | undefined, fallback: number, label: string): number => {
  if (value === undefined || value === '') {
    return fallback;
  }
  const parsed = Number.parseInt(value, 10);
  if (!Number.isFinite(parsed) || parsed <= 0) {
    throw new Error(`${label} must be a positive integer.`);
  }
  return parsed;
};

export const loadConfig = (env: NodeJS.ProcessEnv = process.env): ServerConfig => {
  const simApiUrl = normalizeUrl(env.FLOWTIME_SIM_API_URL ?? DEFAULTS.simApiUrl, 'FLOWTIME_SIM_API_URL');
  const engineApiUrl = normalizeUrl(env.FLOWTIME_API_URL ?? DEFAULTS.engineApiUrl, 'FLOWTIME_API_URL');
  const dataDir = path.resolve(env.FLOWTIME_DATA_DIR ?? DEFAULTS.dataDir);
  const maxBins = parsePositiveInt(env.MCP_MAX_BINS, DEFAULTS.maxBins, 'MCP_MAX_BINS');
  const requestTimeoutMs = parsePositiveInt(
    env.MCP_REQUEST_TIMEOUT_MS,
    DEFAULTS.requestTimeoutMs,
    'MCP_REQUEST_TIMEOUT_MS'
  );
  const orchestrationTimeoutMs = parsePositiveInt(
    env.MCP_ORCHESTRATION_TIMEOUT_MS,
    DEFAULTS.orchestrationTimeoutMs,
    'MCP_ORCHESTRATION_TIMEOUT_MS'
  );

  return {
    simApiUrl,
    engineApiUrl,
    dataDir,
    maxBins,
    requestTimeoutMs,
    orchestrationTimeoutMs
  };
};
