import type { RequestSpec, Transport } from '../http.js';
import type { Group, GroupDetail, GroupMember } from '../types.js';

/** Builds `GET /api/v1/groups`; the endpoint returns a plain array. */
export class GroupsListBuilder {
  constructor(
    private readonly transport: Transport,
    private readonly realm: string | undefined = undefined,
  ) {}

  inRealm(realm: string): GroupsListBuilder {
    return new GroupsListBuilder(this.transport, realm);
  }

  send(): Promise<Group[]> {
    return this.transport.request<Group[]>({
      method: 'GET',
      path: 'groups',
      query: [['realm', this.realm]],
    });
  }
}

/** Builds `POST /api/v1/groups`. */
export class GroupsCreateBuilder {
  constructor(
    private readonly transport: Transport,
    private readonly realm: string | undefined = undefined,
    private readonly code: string | undefined = undefined,
    private readonly name: string | undefined = undefined,
    private readonly idempotencyKey: string | undefined = undefined,
  ) {}

  inRealm(realm: string): GroupsCreateBuilder {
    return new GroupsCreateBuilder(this.transport, realm, this.code, this.name, this.idempotencyKey);
  }

  withCode(code: string): GroupsCreateBuilder {
    return new GroupsCreateBuilder(this.transport, this.realm, code, this.name, this.idempotencyKey);
  }

  withName(name: string): GroupsCreateBuilder {
    return new GroupsCreateBuilder(this.transport, this.realm, this.code, name, this.idempotencyKey);
  }

  withIdempotencyKey(idempotencyKey: string): GroupsCreateBuilder {
    return new GroupsCreateBuilder(this.transport, this.realm, this.code, this.name, idempotencyKey);
  }

  send(): Promise<Group> {
    if (this.realm === undefined || this.realm.trim().length === 0) {
      throw new Error('inRealm() is required before send().');
    }
    if (this.code === undefined || this.code.trim().length === 0) {
      throw new Error('withCode() is required before send().');
    }
    if (this.name === undefined || this.name.trim().length === 0) {
      throw new Error('withName() is required before send().');
    }

    return this.transport.request<Group>({
      method: 'POST',
      path: 'groups',
      body: { realm: this.realm, code: this.code, name: this.name },
      idempotencyKey: this.idempotencyKey ?? crypto.randomUUID(),
    });
  }
}

/** Builds `GET /api/v1/groups/{id}`. */
export class GroupsGetBuilder {
  constructor(
    private readonly transport: Transport,
    private readonly id: string,
  ) {}

  send(): Promise<GroupDetail> {
    return this.transport.request<GroupDetail>({ method: 'GET', path: `groups/${this.id}` });
  }
}

/** Builds `DELETE /api/v1/groups/{id}`. */
export class GroupsDeleteBuilder {
  constructor(
    private readonly transport: Transport,
    private readonly id: string,
  ) {}

  send(): Promise<void> {
    return this.transport.requestVoid({ method: 'DELETE', path: `groups/${this.id}` });
  }
}

/** Builds `GET /api/v1/groups/{id}/members`. */
export class GroupsMembersListBuilder {
  constructor(
    private readonly transport: Transport,
    private readonly id: string,
  ) {}

  send(): Promise<GroupMember[]> {
    return this.transport.request<GroupMember[]>({ method: 'GET', path: `groups/${this.id}/members` });
  }
}

/** Builds `POST /api/v1/groups/{id}/members`. */
export class GroupsMemberAddBuilder {
  constructor(
    private readonly transport: Transport,
    private readonly id: string,
    private readonly accountId: string | undefined = undefined,
    private readonly idempotencyKey: string | undefined = undefined,
  ) {}

  withAccount(accountId: string): GroupsMemberAddBuilder {
    return new GroupsMemberAddBuilder(this.transport, this.id, accountId, this.idempotencyKey);
  }

  withIdempotencyKey(idempotencyKey: string): GroupsMemberAddBuilder {
    return new GroupsMemberAddBuilder(this.transport, this.id, this.accountId, idempotencyKey);
  }

  send(): Promise<void> {
    if (this.accountId === undefined || this.accountId.length === 0) {
      throw new Error('withAccount() is required before send().');
    }

    return this.transport.requestVoid({
      method: 'POST',
      path: `groups/${this.id}/members`,
      body: { accountId: this.accountId },
      idempotencyKey: this.idempotencyKey ?? crypto.randomUUID(),
    });
  }
}

/** Builds `DELETE /api/v1/groups/{id}/members/{accountId}`. */
export class GroupsMemberRemoveBuilder {
  constructor(
    private readonly transport: Transport,
    private readonly id: string,
    private readonly accountId: string,
  ) {}

  send(): Promise<void> {
    return this.transport.requestVoid({
      method: 'DELETE',
      path: `groups/${this.id}/members/${this.accountId}`,
    });
  }
}

/** Entry point for groups and their members. */
export class GroupsResource {
  constructor(private readonly transport: Transport) {}

  list(): GroupsListBuilder {
    return new GroupsListBuilder(this.transport);
  }

  create(): GroupsCreateBuilder {
    return new GroupsCreateBuilder(this.transport);
  }

  get(id: string): GroupsGetBuilder {
    return new GroupsGetBuilder(this.transport, id);
  }

  delete(id: string): GroupsDeleteBuilder {
    return new GroupsDeleteBuilder(this.transport, id);
  }

  members(id: string): GroupsMembersListBuilder {
    return new GroupsMembersListBuilder(this.transport, id);
  }

  addMember(id: string): GroupsMemberAddBuilder {
    return new GroupsMemberAddBuilder(this.transport, id);
  }

  removeMember(id: string, accountId: string): GroupsMemberRemoveBuilder {
    return new GroupsMemberRemoveBuilder(this.transport, id, accountId);
  }
}
