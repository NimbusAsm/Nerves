import { describe, expect, it } from 'vitest';
import { bodyOf, clientFor, headersOf, MockServer } from './helpers.js';
import {
  ACCOUNT_ID,
  auditEvent,
  consent,
  CONSENT_ID,
  GROUP_ID,
  group,
  NOTIFICATION_ID,
  notification,
  SESSION_ID,
  session,
} from './helpers.js';

describe('roles', () => {
  const ROLE_ID = '0199a0c0-0000-7000-8000-000000000020';

  it('assigns and revokes a role subject', async () => {
    const server = new MockServer().enqueue(200).enqueue(200);
    const client = clientFor(server);

    await client.roles.assign(ROLE_ID).withSubject('account', ACCOUNT_ID).send();
    await client.roles.revoke(ROLE_ID).withSubject('group', ACCOUNT_ID).send();

    expect(server.calls[0]?.url).toBe('https://nerves.test/api/v1/roles/' + ROLE_ID + '/assign');
    expect(bodyOf(server.calls[0]!)).toEqual({ subjectType: 'account', subjectId: ACCOUNT_ID });
    expect(server.calls[1]?.url).toBe('https://nerves.test/api/v1/roles/' + ROLE_ID + '/revoke');
  });
});

describe('sessions', () => {
  it('lists the sessions of one account', async () => {
    const server = new MockServer().enqueue(200, [session]);
    const client = clientFor(server);

    const sessions = await client.sessions.list(ACCOUNT_ID).send();

    expect(sessions[0]?.deviceId).toBe('web-1');
    expect(server.calls[0]?.url).toBe('https://nerves.test/api/v1/accounts/' + ACCOUNT_ID + '/sessions');
  });

  it('revokes one session', async () => {
    const server = new MockServer().enqueue(200);
    const client = clientFor(server);

    await client.sessions.revoke(SESSION_ID).send();

    expect(server.calls[0]?.init.method).toBe('DELETE');
    expect(server.calls[0]?.url).toBe('https://nerves.test/api/v1/sessions/' + SESSION_ID);
  });
});

describe('permissions', () => {
  const permission = { id: '0199a0c0-0000-7000-8000-000000000040', code: 'accounts.read', description: 'Read accounts' };

  it('lists permission codes', async () => {
    const server = new MockServer().enqueue(200, [permission]);
    const client = clientFor(server);

    const permissions = await client.permissions.list().send();

    expect(permissions[0]?.code).toBe('accounts.read');
  });

  it('creates a permission with an idempotency key', async () => {
    const server = new MockServer().enqueue(201, permission);
    const client = clientFor(server);

    await client.permissions.create().withCode('accounts.read').withDescription('Read accounts').send();

    expect(headersOf(server.calls[0]!)['idempotency-key']).toBeTruthy();
    expect(bodyOf(server.calls[0]!)).toEqual({ code: 'accounts.read', description: 'Read accounts' });
  });

  it('requires a code', () => {
    const server = new MockServer();
    const client = clientFor(server);

    expect(() => client.permissions.create().send()).toThrow(/withCode/);
  });
});

describe('groups', () => {
  it('lists groups in a realm', async () => {
    const server = new MockServer().enqueue(200, [group]);
    const client = clientFor(server);

    await client.groups.list().inRealm('default').send();

    expect(server.calls[0]?.url).toContain('realm=default');
  });

  it('creates a group and reads its member count', async () => {
    const server = new MockServer().enqueue(201, group).enqueue(200, { ...group, memberCount: 3 });
    const client = clientFor(server);

    await client.groups.create().inRealm('default').withCode('ops').withName('Operations').send();
    const detail = await client.groups.get(GROUP_ID).send();

    expect(bodyOf(server.calls[0]!)).toEqual({ realm: 'default', code: 'ops', name: 'Operations' });
    expect(detail.memberCount).toBe(3);
  });

  it('adds and removes members', async () => {
    const server = new MockServer().enqueue(201).enqueue(200);
    const client = clientFor(server);

    await client.groups.addMember(GROUP_ID).withAccount(ACCOUNT_ID).send();
    await client.groups.removeMember(GROUP_ID, ACCOUNT_ID).send();

    expect(bodyOf(server.calls[0]!)).toEqual({ accountId: ACCOUNT_ID });
    expect(server.calls[1]?.init.method).toBe('DELETE');
    expect(server.calls[1]?.url).toBe('https://nerves.test/api/v1/groups/' + GROUP_ID + '/members/' + ACCOUNT_ID);
  });
});

describe('decisions', () => {
  it('checks a permission for an account', async () => {
    const server = new MockServer().enqueue(200, {
      allowed: true,
      source: 'local',
      authorizationVersion: 7,
      permission: 'accounts.read',
    });
    const client = clientFor(server);

    const decision = await client.decisions.check('accounts.read').forAccount(ACCOUNT_ID).send();

    expect(decision.allowed).toBe(true);
    expect(bodyOf(server.calls[0]!)).toEqual({
      accountId: ACCOUNT_ID,
      legalEntityId: null,
      permission: 'accounts.read',
    });
  });

  it('reads cache statistics', async () => {
    const server = new MockServer().enqueue(200, { entries: 2, hits: 10, misses: 2, hitRate: 0.8333 });
    const client = clientFor(server);

    const stats = await client.decisions.stats().send();

    expect(stats.hitRate).toBe(0.8333);
  });
});

describe('audit', () => {
  it('lists events with filters and follows cursors', async () => {
    const server = new MockServer()
      .enqueue(200, { items: [auditEvent], nextCursor: 'c2' })
      .enqueue(200, { items: [auditEvent], nextCursor: null });
    const client = clientFor(server);

    const collected = [];
    for await (const item of client.audit.list().inStream('identity').withAction('account.created').limit(1)) {
      collected.push(item);
    }

    expect(collected).toHaveLength(2);
    expect(server.calls[0]?.url).toContain('stream=identity');
    expect(server.calls[0]?.url).toContain('action=account.created');
    expect(server.calls[1]?.url).toContain('cursor=c2');
  });

  it('verifies one stream', async () => {
    const server = new MockServer().enqueue(200, { valid: false, brokenAt: auditEvent.id, reason: 'hash_mismatch' });
    const client = clientFor(server);

    const result = await client.audit.verify().inStream('identity').send();

    expect(result.reason).toBe('hash_mismatch');
    expect(server.calls[0]?.url).toContain('stream=identity');
  });

  it('exports csv bytes with the server file name', async () => {
    const server = new MockServer().enqueueResponse(() => new Response('id,stream\n1,identity\n', {
      status: 200,
      headers: {
        'content-type': 'text/csv; charset=utf-8',
        'content-disposition': 'attachment; filename="nerves-audit-20260101-000000.csv"',
      },
    }));
    const client = clientFor(server);

    const file = await client.audit.export().as('csv').limit(10).send();

    expect(file.contentType).toBe('text/csv');
    expect(file.fileName).toBe('nerves-audit-20260101-000000.csv');
    expect(new TextDecoder().decode(file.bytes)).toContain('id,stream');
    expect(server.calls[0]?.url).toContain('format=csv');
  });
});

describe('compliance', () => {
  it('lists, grants and revokes consents', async () => {
    const server = new MockServer()
      .enqueue(200, [consent])
      .enqueue(201, consent)
      .enqueue(200, consent);
    const client = clientFor(server);

    await client.compliance.consents().forSubject('alice').send();
    await client.compliance.grantConsent().forSubject('alice').withPurpose('marketing').withVersion('v1').send();
    await client.compliance.revokeConsent(CONSENT_ID).withReason('withdrawn').send();

    expect(server.calls[0]?.url).toContain('subjectId=alice');
    expect(bodyOf(server.calls[1]!)).toMatchObject({
      subjectId: 'alice',
      purpose: 'marketing',
      version: 'v1',
      expiresAt: null,
    });
    expect(bodyOf(server.calls[2]!)).toEqual({ reason: 'withdrawn' });
    expect(server.calls[2]?.url).toBe(
      'https://nerves.test/api/v1/compliance/consents/' + CONSENT_ID + '/revoke',
    );
  });

  it('requires subject, purpose and version when granting', () => {
    const server = new MockServer();
    const client = clientFor(server);

    expect(() => client.compliance.grantConsent().withPurpose('p').withVersion('v').send()).toThrow(/forSubject/);
    expect(() => client.compliance.grantConsent().forSubject('s').withVersion('v').send()).toThrow(/withPurpose/);
    expect(() => client.compliance.grantConsent().forSubject('s').withPurpose('p').send()).toThrow(/withVersion/);
  });

  it('upserts a retention policy with PUT', async () => {
    const server = new MockServer().enqueue(200, {
      id: '0199a0c0-0000-7000-8000-000000000071',
      category: 'audit',
      retentionDays: 365,
      legalBasis: 'CSL',
      updatedAt: '2026-01-01T00:00:00Z',
    });
    const client = clientFor(server);

    await client.compliance.upsertRetentionPolicy('audit').withRetention(365).withLegalBasis('CSL').send();

    expect(server.calls[0]?.init.method).toBe('PUT');
    expect(server.calls[0]?.url).toBe('https://nerves.test/api/v1/compliance/retention-policies/audit');
    expect(bodyOf(server.calls[0]!)).toEqual({ retentionDays: 365, legalBasis: 'CSL' });
  });

  it('reads the summary', async () => {
    const server = new MockServer().enqueue(200, { consents: 10, active: 8, revoked: 2, retentionPolicies: 3 });
    const client = clientFor(server);

    const summary = await client.compliance.summary().send();

    expect(summary.active).toBe(8);
  });
});

describe('subjects', () => {
  it('exports a subject document', async () => {
    const server = new MockServer().enqueue(200, {
      subjectId: 'alice',
      exportedAt: '2026-01-01T00:00:00Z',
      account: null,
      consents: [],
      retentionPolicies: [],
    });
    const client = clientFor(server);

    const exportDoc = await client.subjects.export('alice').send();

    expect(exportDoc.subjectId).toBe('alice');
    expect(server.calls[0]?.url).toBe('https://nerves.test/api/v1/compliance/subjects/alice/export');
  });

  it('erases a subject and lists erasure history', async () => {
    const server = new MockServer()
      .enqueue(200, {
        id: '0199a0c0-0000-7000-8000-000000000080',
        subjectId: 'alice',
        status: 'completed',
        consentRevoked: 2,
        accountDisabled: true,
      })
      .enqueue(200, [{
        id: '0199a0c0-0000-7000-8000-000000000080',
        status: 'completed',
        reason: 'gdpr',
        requestedAt: '2026-01-01T00:00:00Z',
        completedAt: '2026-01-01T00:01:00Z',
      }]);
    const client = clientFor(server);

    const result = await client.subjects.erase('alice').withReason('gdpr').send();
    const erasures = await client.subjects.erasures('alice').send();

    expect(result.accountDisabled).toBe(true);
    expect(bodyOf(server.calls[0]!)).toEqual({ reason: 'gdpr' });
    expect(erasures[0]?.reason).toBe('gdpr');
  });
});

describe('notifications', () => {
  it('sends a templated email', async () => {
    const server = new MockServer().enqueue(201, notification);
    const client = clientFor(server);

    await client.notifications.sendEmail()
      .to('alice@example.com')
      .withTemplate('welcome')
      .withValue('name', 'Alice')
      .send();

    const call = server.calls[0]!;
    expect(call.url).toBe('https://nerves.test/api/v1/notifications/email');
    expect(headersOf(call)['idempotency-key']).toBeTruthy();
    expect(bodyOf(call)).toEqual({
      to: 'alice@example.com',
      template: 'welcome',
      values: { name: 'Alice' },
    });
  });

  it('lists delivery rows and reads one', async () => {
    const server = new MockServer()
      .enqueue(200, { items: [notification], nextCursor: null })
      .enqueue(200, notification);
    const client = clientFor(server);

    const page = await client.notifications.list().withStatus('pending').to('alice@example.com').limit(5).send();
    const one = await client.notifications.get(NOTIFICATION_ID).send();

    expect(page.items[0]?.status).toBe('pending');
    expect(server.calls[0]?.url).toContain('status=pending');
    expect(server.calls[0]?.url).toContain('to=alice%40example.com');
    expect(one.to).toBe('alice@example.com');
  });
});

describe('events', () => {
  it('reads the incremental identity stream', async () => {
    const server = new MockServer().enqueue(200, [{
      id: '0199a0c0-0000-7000-8000-0000000000a0',
      type: 'account.created',
      accountId: ACCOUNT_ID,
      legalEntityId: null,
      payload: '{}',
      occurredAt: '2026-01-01T00:00:00Z',
      dispatchedAt: null,
    }]);
    const client = clientFor(server);

    const events = await client.events.list()
      .forAccount(ACCOUNT_ID)
      .since('2026-01-01T00:00:00Z')
      .limit(50)
      .send();

    expect(events[0]?.type).toBe('account.created');
    expect(server.calls[0]?.url).toContain('accountId=' + ACCOUNT_ID);
    expect(server.calls[0]?.url).toContain('limit=50');
    expect(server.calls[0]?.url).toContain('after=');
  });
});