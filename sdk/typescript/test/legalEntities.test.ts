import { describe, expect, it } from 'vitest';
import { ENTITY_ID, MockServer, bodyOf, clientFor, legalEntity } from './helpers.js';

describe('legal entities', () => {
  it('create sends the document once and maps the response', async () => {
    const server = new MockServer().enqueue(201, legalEntity);
    const client = clientFor(server);

    const created = await client.legalEntities
      .create()
      .withType('naturalPerson')
      .withDisplayName('Alice')
      .withDocument('passport', 'E12345678', 'CN')
      .send();

    expect(created.id).toBe(ENTITY_ID);
    expect(created.realName?.documentType).toBe('passport');
    expect(created.verificationState).toBe('pending');

    const call = server.calls[0]!;
    expect(call.url).toBe('https://nerves.test/api/v1/legal-entities');
    const body = bodyOf(call);
    expect(body.displayName).toBe('Alice');
    expect(body.type).toBe('naturalPerson');
    expect(body.realName).toEqual({ documentType: 'passport', number: 'E12345678', country: 'CN' });
  });

  it('create requires a document', () => {
    const client = clientFor(new MockServer());
    expect(() => client.legalEntities.create().withDisplayName('Alice').send()).toThrow(/withDocument/);
  });

  it('list filters by state and type with a cursor', async () => {
    const server = new MockServer().enqueue(200, { items: [legalEntity], nextCursor: 'next' });
    const client = clientFor(server);

    const page = await client.legalEntities
      .list()
      .withVerificationState('verified')
      .withType('legalPerson')
      .sort('displayName')
      .limit(10)
      .send();

    expect(page.nextCursor).toBe('next');
    const url = new URL(server.calls[0]!.url);
    expect(url.searchParams.get('filter[verificationState]')).toBe('verified');
    expect(url.searchParams.get('filter[type]')).toBe('legalPerson');
    expect(url.searchParams.get('sort')).toBe('displayName');
    expect(url.searchParams.get('limit')).toBe('10');
  });

  it('stream follows cursors', async () => {
    const server = new MockServer()
      .enqueue(200, { items: [legalEntity], nextCursor: 'next' })
      .enqueue(200, { items: [legalEntity], nextCursor: null });
    const client = clientFor(server);

    const ids: string[] = [];
    for await (const entity of client.legalEntities.list()) {
      ids.push(entity.id);
    }

    expect(ids).toEqual([ENTITY_ID, ENTITY_ID]);
    expect(new URL(server.calls[1]!.url).searchParams.get('cursor')).toBe('next');
  });

  it('update patches verification state with if-match', async () => {
    const server = new MockServer().enqueue(200, legalEntity);
    const client = clientFor(server);

    await client.legalEntities
      .update(ENTITY_ID)
      .withVerificationState('verified')
      .withProviderReference('provider-7')
      .ifMatch('"v2"')
      .send();

    const call = server.calls[0]!;
    expect(call.init.method).toBe('PATCH');
    expect(bodyOf(call)).toEqual({ verificationState: 'verified', providerReference: 'provider-7' });
  });

  it('delete targets the legal-entities path', async () => {
    const server = new MockServer().enqueue(204);
    const client = clientFor(server);

    await client.legalEntities.delete(ENTITY_ID).send();

    expect(server.calls[0]!.url).toBe(`https://nerves.test/api/v1/legal-entities/${ENTITY_ID}`);
  });
});
