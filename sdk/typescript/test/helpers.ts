import { vi } from 'vitest';
import { NervesClient } from '../src/client.js';
import type { NervesClientOptions } from '../src/http.js';

export interface RecordedCall {
  url: string;
  init: RequestInit;
}

type Responder = () => Response;

export class MockServer {
  readonly calls: RecordedCall[] = [];
  readonly #responses: Responder[] = [];

  readonly fetch = vi.fn(async (input: RequestInfo | URL, init?: RequestInit): Promise<Response> => {
    this.calls.push({ url: String(input), init: init ?? {} });
    const responder = this.#responses.shift();
    if (!responder) {
      throw new Error(`No response queued for ${init?.method ?? 'GET'} ${String(input)}`);
    }
    return responder();
  }) as unknown as typeof globalThis.fetch;

  enqueue(status: number, body?: unknown, headers: Record<string, string> = {}): this {
    this.#responses.push(() => new Response(body === undefined ? null : JSON.stringify(body), {
      status,
      headers: { 'content-type': 'application/json', ...headers },
    }));
    return this;
  }

  enqueueResponse(factory: () => Response): this {
    this.#responses.push(factory);
    return this;
  }
}

export function clientFor(server: MockServer, overrides: Partial<NervesClientOptions> = {}): NervesClient {
  return new NervesClient({
    baseUrl: 'https://nerves.test',
    token: 'pat-test-token',
    fetch: server.fetch,
    maxRetries: 0,
    retryDelayMs: 1,
    ...overrides,
  });
}

export function headersOf(call: RecordedCall): Record<string, string> {
  return (call.init.headers ?? {}) as Record<string, string>;
}

export function bodyOf(call: RecordedCall): Record<string, unknown> {
  return JSON.parse(String(call.init.body)) as Record<string, unknown>;
}

export const ENTITY_ID = '0199a0c0-0000-7000-8000-000000000010';
export const ACCOUNT_ID = '0199a0c0-0000-7000-8000-000000000001';
export const ROLE_ID = '0199a0c0-0000-7000-8000-000000000020';

export const account = {
  id: ACCOUNT_ID,
  legalEntityId: ENTITY_ID,
  realm: 'default',
  username: 'alice',
  email: 'alice@example.com',
  phoneNumber: null,
  displayName: null,
  type: 'personal',
  status: 'active',
  isPrimary: false,
  roles: ['member'],
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
  disabledAt: null,
} as const;

export const legalEntity = {
  id: ENTITY_ID,
  type: 'naturalPerson',
  displayName: 'Alice',
  realName: { documentType: 'passport', country: 'CN' },
  verificationState: 'pending',
  verifiedAt: null,
  providerReference: null,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
} as const;

export const role = {
  id: ROLE_ID,
  name: 'member',
  description: 'Members',
  permissions: ['accounts.read'],
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
} as const;
export const SESSION_ID = '0199a0c0-0000-7000-8000-000000000030';
export const GROUP_ID = '0199a0c0-0000-7000-8000-000000000050';
export const CONSENT_ID = '0199a0c0-0000-7000-8000-000000000070';
export const NOTIFICATION_ID = '0199a0c0-0000-7000-8000-000000000090';

export const session = {
  id: SESSION_ID,
  deviceId: 'web-1',
  createdAt: '2026-01-01T00:00:00Z',
  lastSeenAt: '2026-01-02T00:00:00Z',
  absoluteExpiresAt: '2026-02-01T00:00:00Z',
  revoked: false,
  revokedReason: null,
} as const;

export const group = {
  id: GROUP_ID,
  realm: 'default',
  code: 'ops',
  name: 'Operations',
} as const;

export const consent = {
  id: CONSENT_ID,
  subjectId: 'alice',
  purpose: 'marketing',
  version: 'v1',
  grantedAt: '2026-01-01T00:00:00Z',
  expiresAt: null,
  revokedAt: null,
  revocationReason: null,
} as const;

export const notification = {
  id: NOTIFICATION_ID,
  to: 'alice@example.com',
  template: 'welcome',
  subject: 'Welcome',
  status: 'pending',
  attempts: 0,
  error: null,
  createdAt: '2026-01-01T00:00:00Z',
  sentAt: null,
  nextAttemptAt: null,
} as const;

export const auditEvent = {
  id: '0199a0c0-0000-7000-8000-000000000060',
  stream: 'identity',
  sequence: 1,
  occurredAt: '2026-01-01T00:00:00Z',
  actorType: 'admin',
  actorId: 'admin',
  action: 'account.created',
  targetType: 'account',
  targetId: 'x',
  result: 'success',
  traceId: 't',
  hash: 'h',
} as const;
