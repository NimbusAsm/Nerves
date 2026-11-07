import { describe, expect, it } from 'vitest';
import { ROLE_ID, MockServer, bodyOf, clientFor, role } from './helpers.js';

describe('roles', () => {
  it('create sends name, description and permissions', async () => {
    const server = new MockServer().enqueue(201, role);
    const client = clientFor(server);

    const created = await client.roles
      .create()
      .withName('member')
      .withDescription('Members')
      .withPermissions('accounts.read', 'accounts.list')
      .send();

    expect(created.name).toBe('member');
    expect(created.permissions).toEqual(['accounts.read']);

    const call = server.calls[0]!;
    expect(call.url).toBe('https://nerves.test/api/v1/roles');
    const body = bodyOf(call);
    expect(body.name).toBe('member');
    expect(body.permissions).toEqual(['accounts.read', 'accounts.list']);
  });

  it('create requires a name', () => {
    const client = clientFor(new MockServer());
    expect(() => client.roles.create().send()).toThrow(/withName/);
  });

  it('get uses the role path', async () => {
    const server = new MockServer().enqueue(200, role);
    const client = clientFor(server);

    const found = await client.roles.get(ROLE_ID).send();

    expect(found.id).toBe(ROLE_ID);
    expect(server.calls[0]!.url).toBe(`https://nerves.test/api/v1/roles/${ROLE_ID}`);
  });

  it('list supports filter, sort, limit and cursor', async () => {
    const server = new MockServer().enqueue(200, { items: [role], nextCursor: 'c1' });
    const client = clientFor(server);

    const page = await client.roles.list().filter('name', 'member').sort('name').limit(1).cursor('start').send();

    expect(page.items[0]?.name).toBe('member');
    expect(page.nextCursor).toBe('c1');

    const url = new URL(server.calls[0]!.url);
    expect(url.searchParams.get('filter[name]')).toBe('member');
    expect(url.searchParams.get('sort')).toBe('name');
    expect(url.searchParams.get('limit')).toBe('1');
    expect(url.searchParams.get('cursor')).toBe('start');
  });

  it('stream walks every page', async () => {
    const server = new MockServer()
      .enqueue(200, { items: [role], nextCursor: 'c2' })
      .enqueue(200, { items: [role], nextCursor: null });
    const client = clientFor(server);

    const names: string[] = [];
    for await (const item of client.roles.list().stream()) {
      names.push(item.name);
    }

    expect(names).toEqual(['member', 'member']);
    expect(new URL(server.calls[1]!.url).searchParams.get('cursor')).toBe('c2');
  });

  it('update replaces permissions and delete is empty', async () => {
    const server = new MockServer().enqueue(200, role).enqueue(204);
    const client = clientFor(server);

    await client.roles.update(ROLE_ID).withPermissions('roles.read').send();
    await client.roles.delete(ROLE_ID).send();

    expect(bodyOf(server.calls[0]!)).toEqual({ permissions: ['roles.read'] });
    expect(server.calls[1]!.init.method).toBe('DELETE');
  });
});
