const API_BASE: string = import.meta.env.VITE_API_BASE ?? '';

/** An error response from the API (RFC 7807 problem details). `traceId` links it to a trace in the dashboard. */
export class ApiError extends Error {
  readonly status: number;
  readonly title: string;
  readonly traceId?: string;

  constructor(status: number, title: string, detail: string, traceId?: string) {
    super(detail);
    this.name = 'ApiError';
    this.status = status;
    this.title = title;
    this.traceId = traceId;
  }
}

let accessToken: string | null = null;
let onUnauthorized: (() => void) | null = null;

export const setAccessToken = (token: string | null) => {
  accessToken = token;
};

export const setUnauthorizedHandler = (handler: (() => void) | null) => {
  onUnauthorized = handler;
};

interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE';
  body?: unknown;
  headers?: Record<string, string>;
  /** false = never send the token (public catalog calls stay cacheable). */
  auth?: boolean;
  signal?: AbortSignal;
}

export async function api<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const { method = 'GET', body, headers = {}, auth = true, signal } = options;

  const finalHeaders: Record<string, string> = { Accept: 'application/json', ...headers };
  if (body !== undefined) finalHeaders['Content-Type'] = 'application/json';
  if (auth && accessToken) finalHeaders.Authorization = `Bearer ${accessToken}`;

  const response = await fetch(`${API_BASE}${path}`, {
    method,
    headers: finalHeaders,
    body: body === undefined ? undefined : JSON.stringify(body),
    signal
  });

  // An expired or invalid token: drop the session so the user is sent back to the login page.
  if (response.status === 401 && auth && accessToken) onUnauthorized?.();

  if (!response.ok) throw await toApiError(response);
  if (response.status === 204) return undefined as T;
  return (await response.json()) as T;
}

async function toApiError(response: Response): Promise<ApiError> {
  try {
    const problem = await response.json();
    return new ApiError(
      response.status,
      problem.title ?? response.statusText,
      problem.detail ?? problem.title ?? `Request failed (${response.status})`,
      problem.traceId
    );
  } catch {
    return new ApiError(response.status, response.statusText, `Request failed (${response.status})`);
  }
}

/**
 * Should the checkout idempotency key be replaced after this failure?
 * - Network errors, 500, 503: the server MAY have created the order, so keep the key; a retry replays safely.
 * - 4xx and 502: the server answered definitively (nothing created, or the order was marked Failed), so start fresh.
 */
export function shouldRotateIdempotencyKey(error: unknown): boolean {
  return error instanceof ApiError && (error.status === 502 || (error.status >= 400 && error.status < 500));
}
