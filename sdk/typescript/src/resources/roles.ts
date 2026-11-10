import type { RequestSpec, Transport } from '../http.js';
import { ListQuery } from '../pagination.js';
import type { Page, Role } from '../types.js';

interface CreateRoleSpec {
  name?: string;
  description?: string | null;
  permissions?: string[];
  idempotencyKey?: string;
}

const ROLE_PATH = 'roles';

/** Builds `POST /api/v1/roles`. */
export class RolesCreateBuilder {
  constructor(
    private readonly transport: Transport,
    private readonly spec: CreateRoleSpec = {},
  ) {}

  withName(name: string): RolesCreateBuilder {
    return new RolesCreateBuilder(this.transport, { ...this.spec, name });
  }

  withDescription(description: string | null): RolesCreateBuilder {
    return new RolesCreateBuilder(this.transport, { ...this.spec, description });
  }

  withPermissions(...permissions: string[]): RolesCreateBuilder {
    return new RolesCreateBuilder(this.transport, { ...this.spec, permissions: [...permissions] });
  }

  withIdempotencyKey(idempotencyKey: string): RolesCreateBuilder {
    return new RolesCreateBuilder(this.transport, { ...this.spec, idempotencyKey });
  }

  send(): Promise<Role> {
    const spec = this.spec;
    if (!spec.name || spec.name.trim().length === 0) {
      throw new Error('withName() is required before send().');
    }

    const body: Record<string, unknown> = { name: spec.name };
    if (spec.description !== undefined) body.description = spec.description;
    if (spec.permissions !== undefined && spec.permissions.length > 0) {
      body.permissions = spec.permissions;
    }

    const request: RequestSpec = {
      method: 'POST',
      path: ROLE_PATH,
      body,
      idempotencyKey: spec.idempotencyKey ?? crypto.randomUUID(),
    };
    return this.transport.request<Role>(request);
  }
}

/** Builds `GET /api/v1/roles/{id}`. */
export class RolesGetBuilder {
  constructor(
    private readonly transport: Transport,
    private readonly id: string,
  ) {}

  send(): Promise<Role> {
    return this.transport.request<Role>({ method: 'GET', path: `${ROLE_PATH}/${this.id}` });
  }
}

/** Builds `GET /api/v1/roles` with cursor pagination. */
export class RolesListBuilder implements AsyncIterable<Role> {
  constructor(
    private readonly transport: Transport,
    private readonly query: ListQuery = ListQuery.empty,
  ) {}

  filter(field: string, value: string): RolesListBuilder {
    return new RolesListBuilder(this.transport, this.query.filter(field, value));
  }

  sort(expression: string): RolesListBuilder {
    return new RolesListBuilder(this.transport, this.query.withSort(expression));
  }

  limit(pageSize: number): RolesListBuilder {
    return new RolesListBuilder(this.transport, this.query.withLimit(pageSize));
  }

  cursor(cursor: string): RolesListBuilder {
    return new RolesListBuilder(this.transport, this.query.withCursor(cursor));
  }

  send(): Promise<Page<Role>> {
    return this.transport.requestPage<Role>({ method: 'GET', path: ROLE_PATH, query: this.query.params() });
  }

  /** Iterates every role, following `nextCursor` until the end. */
  async *stream(): AsyncGenerator<Role, void, undefined> {
    let cursor = this.query.cursor;
    const seen = new Set<string>();

    for (;;) {
      const query = cursor === undefined ? this.query : this.query.withCursor(cursor);
      const page = await new RolesListBuilder(this.transport, query).send();
      yield* page.items;

      const next = page.nextCursor;
      if (next === null || next.length === 0 || seen.has(next)) {
        return;
      }
      seen.add(next);
      cursor = next;
    }
  }

  [Symbol.asyncIterator](): AsyncIterator<Role> {
    return this.stream();
  }
}

/** Builds `PATCH /api/v1/roles/{id}`. */
export class RolesUpdateBuilder {
  constructor(
    private readonly transport: Transport,
    private readonly id: string,
    private readonly patch: Readonly<Record<string, unknown>> = {},
    private readonly ifMatchTag: string | undefined = undefined,
  ) {}

  withName(name: string): RolesUpdateBuilder {
    return new RolesUpdateBuilder(this.transport, this.id, { ...this.patch, name }, this.ifMatchTag);
  }

  withDescription(description: string | null): RolesUpdateBuilder {
    return new RolesUpdateBuilder(this.transport, this.id, { ...this.patch, description }, this.ifMatchTag);
  }

  withPermissions(...permissions: string[]): RolesUpdateBuilder {
    return new RolesUpdateBuilder(this.transport, this.id, { ...this.patch, permissions: [...permissions] }, this.ifMatchTag);
  }

  ifMatch(etag: string): RolesUpdateBuilder {
    return new RolesUpdateBuilder(this.transport, this.id, this.patch, etag);
  }

  send(): Promise<Role> {
    if (Object.keys(this.patch).length === 0) {
      throw new Error('Set at least one field with with...() before send().');
    }
    return this.transport.request<Role>({
      method: 'PATCH',
      path: `${ROLE_PATH}/${this.id}`,
      body: this.patch,
      ifMatch: this.ifMatchTag,
    });
  }
}

/** Builds `DELETE /api/v1/roles/{id}`. */
export class RolesDeleteBuilder {
  constructor(
    private readonly transport: Transport,
    private readonly id: string,
    private readonly ifMatchTag: string | undefined = undefined,
  ) {}

  ifMatch(etag: string): RolesDeleteBuilder {
    return new RolesDeleteBuilder(this.transport, this.id, etag);
  }

  send(): Promise<void> {
    return this.transport.requestVoid({
      method: 'DELETE',
      path: `${ROLE_PATH}/${this.id}`,
      ifMatch: this.ifMatchTag,
    });
  }
}

/** Builds `POST /api/v1/roles/{id}/assign`. */
export class RolesAssignBuilder {
  constructor(
    protected readonly transport: Transport,
    protected readonly id: string,
    protected readonly subjectType: string | undefined = undefined,
    protected readonly subjectId: string | undefined = undefined,
    protected readonly idempotencyKey: string | undefined = undefined,
  ) {}

  withSubject(subjectType: string, subjectId: string): RolesAssignBuilder {
    return new RolesAssignBuilder(this.transport, this.id, subjectType, subjectId, this.idempotencyKey);
  }

  withIdempotencyKey(idempotencyKey: string): RolesAssignBuilder {
    return new RolesAssignBuilder(this.transport, this.id, this.subjectType, this.subjectId, idempotencyKey);
  }

  send(): Promise<void> {
    return this.sendTo('assign');
  }

  protected sendTo(action: 'assign' | 'revoke'): Promise<void> {
    if (this.subjectType === undefined || this.subjectId === undefined) {
      throw new Error('withSubject() is required before send().');
    }

    return this.transport.requestVoid({
      method: 'POST',
      path: `${ROLE_PATH}/${this.id}/${action}`,
      body: { subjectType: this.subjectType, subjectId: this.subjectId },
      idempotencyKey: this.idempotencyKey ?? crypto.randomUUID(),
    });
  }
}

/** Builds `POST /api/v1/roles/{id}/revoke`. */
export class RolesRevokeBuilder extends RolesAssignBuilder {
  override withSubject(subjectType: string, subjectId: string): RolesRevokeBuilder {
    return new RolesRevokeBuilder(this.transport, this.id, subjectType, subjectId, this.idempotencyKey);
  }

  override withIdempotencyKey(idempotencyKey: string): RolesRevokeBuilder {
    return new RolesRevokeBuilder(this.transport, this.id, this.subjectType, this.subjectId, idempotencyKey);
  }

  override send(): Promise<void> {
    return this.sendTo('revoke');
  }
}

/** Entry point for every role operation. */
export class RolesResource {
  constructor(private readonly transport: Transport) {}

  create(): RolesCreateBuilder {
    return new RolesCreateBuilder(this.transport);
  }

  get(id: string): RolesGetBuilder {
    return new RolesGetBuilder(this.transport, id);
  }

  list(): RolesListBuilder {
    return new RolesListBuilder(this.transport);
  }

  update(id: string): RolesUpdateBuilder {
    return new RolesUpdateBuilder(this.transport, id);
  }

  delete(id: string): RolesDeleteBuilder {
    return new RolesDeleteBuilder(this.transport, id);
  }

  assign(id: string): RolesAssignBuilder {
    return new RolesAssignBuilder(this.transport, id);
  }

  revoke(id: string): RolesRevokeBuilder {
    return new RolesRevokeBuilder(this.transport, id);
  }
}