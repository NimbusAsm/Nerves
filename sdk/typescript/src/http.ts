import { NervesApiError, type ProblemDetails } from './errors.js';
import type { Page } from './types.js';

/** One query parameter; `null` and `undefined` values are dropped. */
export type QueryParam = readonly [string, string | null | undefined];

/** Options accepted by {@link NervesClient}. */
export interface NervesClientOptions {
  /** Deployment origin, e.g. `https://accounts.example.com`. */
  baseUrl: string;
  /** Personal access token sent as a bearer token. */
  token?: string;
  /** Fetch implementation; inject one to add tracing, a proxy, or a test double. */
  fetch?: typeof globalThis.fetch;
  /** Extra headers added to every request. */
  headers?: Record<string, string>;
  /** Per-request timeout in milliseconds. Defaults to 30 000. */
  timeoutMs?: number;
  /** Retries for 429, 5xx, and network failures. Defaults to 2. */
  maxRetries?: number;
  /** Base delay for exponential backoff in milliseconds. Defaults to 200. */
  retryDelayMs?: number;
  /** API prefix. Defaults to `/api/v1`. */
  apiBasePath?: string;
}

/** Internal request description handed to {@link Transport}. */
export interface RequestSpec {
  method: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';
  path: string;
  query?: readonly QueryParam[];
  body?: unknown;
  idempotencyKey?: string;
  ifMatch?: string;
  /** Accept header override, e.g. for audit exports. */
  accept?: string;
}

interface PagePayload<T> {
  items?: T[];
  nextCursor?: string | null;
}

/** A downloaded binary document, currently only the audit export. */
export interface NervesFile {
  bytes: Uint8Array;
  contentType: string;
  fileName: string | undefined;
}

/** Encodes a query string; brackets in filter names are percent-encoded. */
export function buildQuery(params: readonly QueryParam[]): string {
  const parts: string[] = [];
  for (const [key, value] of params) {
    if (value === null || value === undefined) {
      continue;
    }
    parts.push(`${encodeURIComponent(key)}=${encodeURIComponent(value)}`);
  }
  return parts.length === 0 ? '' : `?${parts.join('&')}`;
}

function isRetryable(status: number): boolean {
  return status === 429 || status >= 500;
}

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

function parseProblem(response: Response, body: string): NervesApiError {
  let problem: ProblemDetails = {};
  if (body.trim().length > 0) {
    try {
      problem = JSON.parse(body) as ProblemDetails;
    } catch {
      // A proxy or load balancer may answer with HTML or plain text.
    }
  }

  const traceId = problem.traceId ?? response.headers.get('x-trace-id') ?? undefined;
  return new NervesApiError(
    response.status,
    {
      status: problem.status ?? response.status,
      title: problem.title ?? response.statusText,
      ...problem,
      ...(traceId === undefined ? {} : { traceId }),
    },
    body,
  );
}

function parseFileName(disposition: string): string | undefined {
  const star = /filename\*=UTF-8''([^;]+)/i.exec(disposition);
  if (star?.[1] !== undefined) {
    return decodeURIComponent(star[1]);
  }
  const plain = /filename="?([^";]+)"?/i.exec(disposition);
  return plain?.[1];
}

/** HTTP transport shared by every builder. */
export class Transport {
  readonly #fetch: typeof globalThis.fetch;
  readonly #root: string;
  readonly #token: string | undefined;
  readonly #headers: Record<string, string>;
  readonly #timeoutMs: number;
  readonly #maxRetries: number;
  readonly #retryDelayMs: number;

  constructor(options: NervesClientOptions) {
    if (!options || typeof options.baseUrl !== 'string' || options.baseUrl.length === 0) {
      throw new TypeError('baseUrl is required');
    }

    const base = new URL(options.baseUrl);
    const prefix = options.apiBasePath ?? '/api/v1';
    this.#root = `${base.origin}${base.pathname.replace(/\/+$/, '')}${prefix}`;
    this.#fetch = options.fetch ?? globalThis.fetch;
    if (typeof this.#fetch !== 'function') {
      throw new TypeError('A fetch implementation is required');
    }
    this.#token = options.token;
    this.#headers = { ...options.headers };
    this.#timeoutMs = options.timeoutMs ?? 30_000;
    this.#maxRetries = Math.max(0, options.maxRetries ?? 2);
    this.#retryDelayMs = Math.max(0, options.retryDelayMs ?? 200);
  }

  /** Fetches a JSON document. */
  async request<T>(spec: RequestSpec): Promise<T> {
    const text = await this.#send(spec);
    if (text.trim().length === 0) {
      throw new NervesApiError(200, {
        status: 200,
        title: 'The API returned an empty body where a document was expected.',
      });
    }
    return JSON.parse(text) as T;
  }

  /** Fetches one cursor page, normalising a missing envelope. */
  async requestPage<T>(spec: RequestSpec): Promise<Page<T>> {
    const payload = await this.request<PagePayload<T>>(spec);
    const items = payload.items ?? [];
    const nextCursor = payload.nextCursor ?? null;
    return { items, nextCursor, hasMore: nextCursor !== null && nextCursor.length > 0 };
  }

  /** Sends a request whose success body is irrelevant. */
  async requestVoid(spec: RequestSpec): Promise<void> {
    await this.#send(spec);
  }

  /** Downloads a non-JSON document (audit export). */
  async requestFile(spec: RequestSpec): Promise<NervesFile> {
    const { bytes, response } = await this.#sendRaw(spec);
    const disposition = response.headers.get('content-disposition') ?? '';
    return {
      bytes,
      contentType: response.headers.get('content-type')?.split(';')[0]?.trim() ?? 'application/octet-stream',
      fileName: parseFileName(disposition),
    };
  }

  #url(spec: RequestSpec): string {
    return `${this.#root}/${spec.path.replace(/^\/+/, '')}${buildQuery(spec.query ?? [])}`;
  }

  async #send(spec: RequestSpec): Promise<string> {
    const { bytes } = await this.#sendRaw(spec);
    return new TextDecoder().decode(bytes);
  }

  async #sendRaw(spec: RequestSpec): Promise<{ bytes: Uint8Array; response: Response }> {
    const url = this.#url(spec);
    const body = spec.body === undefined ? undefined : JSON.stringify(spec.body);

    for (let attempt = 0; ; attempt += 1) {
      const headers: Record<string, string> = {
        accept: spec.accept ?? 'application/json',
        ...this.#headers,
      };
      if (body !== undefined) {
        headers['content-type'] = 'application/json';
      }
      if (this.#token !== undefined) {
        headers.authorization = `Bearer ${this.#token}`;
      }
      if (spec.idempotencyKey !== undefined) {
        headers['idempotency-key'] = spec.idempotencyKey;
      }
      if (spec.ifMatch !== undefined) {
        headers['if-match'] = spec.ifMatch;
      }

      const controller = new AbortController();
      const timer = setTimeout(() => controller.abort(), this.#timeoutMs);

      let response: Response;
      try {
        response = await this.#fetch(url, {
          method: spec.method,
          headers,
          body,
          signal: controller.signal,
        });
      } catch (error) {
        clearTimeout(timer);
        if (attempt < this.#maxRetries) {
          await sleep(this.#delay(attempt));
          continue;
        }
        throw error;
      }

      let bytes: Uint8Array;
      try {
        bytes = new Uint8Array(await response.arrayBuffer());
      } finally {
        clearTimeout(timer);
      }

      if (response.ok) {
        return { bytes, response };
      }

      const text = new TextDecoder().decode(bytes);

      if (isRetryable(response.status) && attempt < this.#maxRetries) {
        const retryAfter = Number(response.headers.get('retry-after'));
        const delay = Number.isFinite(retryAfter) && retryAfter > 0
          ? retryAfter * 1000
          : this.#delay(attempt);
        await sleep(delay);
        continue;
      }

      throw parseProblem(response, text);
    }
  }

  #delay(attempt: number): number {
    const exponential = this.#retryDelayMs * 2 ** attempt;
    return exponential + Math.random() * this.#retryDelayMs;
  }
}