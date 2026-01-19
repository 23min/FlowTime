export class HttpError extends Error {
  readonly status: number;
  readonly payload: unknown;

  constructor(message: string, status: number, payload: unknown) {
    super(message);
    this.status = status;
    this.payload = payload;
  }
}

const readBody = async (response: Response): Promise<unknown> => {
  const text = await response.text();
  if (!text) {
    return null;
  }
  try {
    return JSON.parse(text);
  } catch {
    return text;
  }
};

export const fetchJson = async <T>(
  url: string,
  options: RequestInit,
  timeoutMs: number
): Promise<T> => {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), timeoutMs);
  try {
    const response = await fetch(url, {
      ...options,
      signal: controller.signal,
      headers: {
        'Content-Type': 'application/json',
        ...(options.headers ?? {})
      }
    });

    const payload = await readBody(response);

    if (!response.ok) {
      const message = typeof payload === 'string'
        ? payload
        : `HTTP ${response.status} for ${url}`;
      throw new HttpError(message, response.status, payload);
    }

    return payload as T;
  } catch (error) {
    if (error instanceof HttpError) {
      throw error;
    }
    if (error instanceof Error && error.name === 'AbortError') {
      throw new Error(`Request timed out after ${timeoutMs}ms.`);
    }
    throw error;
  } finally {
    clearTimeout(timeout);
  }
};
