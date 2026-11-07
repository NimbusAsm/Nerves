import { describe, expect, it } from 'vitest';
import { NervesApiError } from '../src/errors.js';
import {
  ACCOUNT_ID,
  ENTITY_ID,
  MockServer,
  account,
  bodyOf,
  clientFor,
  headersOf,
} from './helpers.js';

describe('accounts', () => {
  it('create sends the documented request', async () => {
    const server = new MockServer().enqueue(201, account);
    const client = clientFor(server);

    const created = await client.accounts
      .create()
      .forEntity(ENTITY_ID)
      .inRealm('default')
      .withUsername('alice')
      .withEmail('alice@example.com')
      .withRoles('member', 'admin')
      .withPrimary()
      .send();

    expect(created.username).toBe('alice');
    expect(created.roles).toEqual(['member']);

    expect(server.calls).toHaveLength(1);
    const call = server.calls[0]!;
    expect(call.init.method).toBe('POST');
    expect(call.url).toBe('https://nerves.test/api/v1/accounts');
    expect(headersOf(call).authorization).toBe('Bearer pat-test-token');
    expect(headersOf(call)['idempotency-key']).toBeTruthy();

    const body = bodyOf(call);
    expect(body.legalEntityId).toBe(ENTITY_ID);
    expect(body.realm).toBe('default');
    expect(body.username).toBe('alice');
    expect(body.email).toBe('alice@example.com');
    expect(body.type).toBe('personal');
    expect(body.isPrimary).toBe(true);
    expect(body.roles).toEqual(['member', 'admin']);
  });

  it('rejects a personal account without an entity and never calls fetch', () => {
    const server = new MockServer();
    const client = clientFor(server);

    expect(() => client.accounts.create().withUsername('alice').send()).toThrow(/forEntity/);
    expect(server.calls).toHaveLength(0);
  });

  it('lets service accounts omit the entity', async () => {
    const server = new MockServer().enqueue(201, account);
    const client = clientFor(server);

    await client.accounts.create().withType('service').inRealm('internal').withUsername('svc-billing').send();

    const body = bodyOf(server.calls[0]!);
    expect(body.legalEntityId).toBeUndefined();
    expect(body.type).toBe('service');
  });

  it('builders are immutable and reusable', async () => {
    const server = new MockServer().enqueue(201, account).enqueue(201, account);
    const client = clientFor(server);
    const baseBuilder = client.accounts.create().forEntity(ENTITY_ID).withUsername('alice');

    await baseBuilder.withEmail('first@example.com').send();
    await baseBuilder.withEmail('second@example.com').send();

    expect(bodyOf(server.calls[0]!).email).toBe('first@example.com');
    expect(bodyOf(server.calls[1]!).email).toBe('second@example.com');
    expect(bodyOf(server.calls[0]!).username).toBe('alice');
  });

  it('get reads one account by id', async () => {
    const server = new MockServer().enqueue(200, account);
    const client = clientFor(server);

    const found = await client.accounts.get(ACCOUNT_ID).send();

    expect(found.id).toBe(ACCOUNT_ID);
    expect(server.calls[0]!.init.method).toBe('GET');
    expect(server.calls[0]!.url).toBe(`https://nerves.test/api/v1/accounts/${ACCOUNT_ID}`);
  });

  it('list builds a cursor query and returns the next cursor', async () => {
    const server = new MockServer().enqueue(200, { items: [account], nextCursor: 'cursor-2' });
    const client = clientFor(server);

    const page = await client.accounts
      .list()
      .forEntity(ENTITY_ID)
      .inRealm('default')
      .withStatus('active')
      .filter('username', 'alice')
      .sort('-createdAt')
      .limit(2)
      .send();

    expect(page.items).toHaveLength(1);
    expect(page.nextCursor).toBe('cursor-2');
    expect(page.hasMore).toBe(true);

    const url = new URL(server.calls[0]!.url);
    expect(url.searchParams.get('legalEntityId')).toBe(ENTITY_ID);
    expect(url.searchParams.get('realm')).toBe('default');
    expect(url.searchParams.get('filter[status]')).toBe('active');
    expect(url.searchParams.get('filter[username]')).toBe('alice');
    expect(url.searchParams.get('sort')).toBe('-createdAt');
    expect(url.searchParams.get('limit')).toBe('2');
  });

  it('list stream follows cursors until exhausted', async () => {
    const server = new MockServer()
      .enqueue(200, { items: [account], nextCursor: 'cursor-2' })
      .enqueue(200, { items: [account], nextCursor: null });
    const client = clientFor(server);

    const usernames: string[] = [];
    for await (const item of client.accounts.list().forEntity(ENTITY_ID).limit(1)) {
      usernames.push(item.username);
    }

    expect(usernames).toEqual(['alice', 'alice']);
    expect(server.calls).toHaveLength(2);
    expect(new URL(server.calls[0]!.url).searchParams.get('cursor')).toBeNull();
    expect(new URL(server.calls[1]!.url).searchParams.get('cursor')).toBe('cursor-2');
  });

  it('update sends only changed fields with if-match', async () => {
    const server = new MockServer().enqueue(200, account);
    const client = clientFor(server);

    await client.accounts.update(ACCOUNT_ID).withDisplayName('Alice A.').ifMatch('"v1"').send();

    const call = server.calls[0]!;
    expect(call.init.method).toBe('PATCH');
    expect(headersOf(call)['if-match']).toBe('"v1"');
    expect(bodyOf(call)).toEqual({ displayName: 'Alice A.' });
  });

  it('update rejects an empty patch', () => {
    const client = clientFor(new MockServer());
    expect(() => client.accounts.update(ACCOUNT_ID).send()).toThrow(/at least one field/);
  });

  it('delete accepts no content', async () => {
    const server = new MockServer().enqueue(204);
    const client = clientFor(server);

    await client.accounts.delete(ACCOUNT_ID).ifMatch('"v1"').send();

    const call = server.calls[0]!;
    expect(call.init.method).toBe('DELETE');
    expect(call.url).toBe(`https://nerves.test/api/v1/accounts/${ACCOUNT_ID}`);
    expect(headersOf(call)['if-match']).toBe('"v1"');
  });

  it('parses problem details into NervesApiError', async () => {
    const server = new MockServer().enqueueResponse(() => new Response(JSON.stringify({
      type: 'https://nerves.dev/problems/conflict',
      title: 'Conflict',
      status: 409,
      detail: 'username already exists',
      traceId: 'trace-42',
    }), { status: 409, headers: { 'content-type': 'application/problem+json' } }));
    const client = clientFor(server);

    const error = await client.accounts.create().forEntity(ENTITY_ID).withUsername('alice').send().catch((e: unknown) => e);

    expect(error).toBeInstanceOf(NervesApiError);
    const apiError = error as NervesApiError;
    expect(apiError.status).toBe(409);
    expect(apiError.type).toBe('https://nerves.dev/problems/conflict');
    expect(apiError.traceId).toBe('trace-42');
    expect(apiError.detail).toBe('username already exists');
    expect(apiError.problem.status).toBe(409);
  });

  it('keeps non-JSON errors and the trace header', async () => {
    const server = new MockServer().enqueueResponse(() => new Response('upstream unavailable', {
      status: 502,
      headers: { 'x-trace-id': 'trace-proxy' },
    }));
    const client = clientFor(server);

    const error = await client.accounts.get(ACCOUNT_ID).send().catch((e: unknown) => e);
    const apiError = error as NervesApiError;

    expect(apiError.status).toBe(502);
    expect(apiError.traceId).toBe('trace-proxy');
    expect(apiError.rawBody).toBe('upstream unavailable');
  });

  it('retries transient failures with the same idempotency key', async () => {
    const server = new MockServer().enqueue(503).enqueue(201, account);
    const client = clientFor(server, { maxRetries: 1 });

    await client.accounts.create().forEntity(ENTITY_ID).withUsername('alice').send();

    expect(server.calls).toHaveLength(2);
    expect(headersOf(server.calls[0]!)['idempotency-key']).toBe(headersOf(server.calls[1]!)['idempotency-key']);
  });
});