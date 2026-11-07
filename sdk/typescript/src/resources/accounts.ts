import type { RequestSpec, Transport } from '../http.js';
import { ListQuery } from '../pagination.js';
import type { Account, AccountStatus, AccountType, Page } from '../types.js';

interface CreateAccountSpec {
  legalEntityId?: string;
  realm?: string;
  username?: string;
  email?: string | null;
  phoneNumber?: string | null;
  displayName?: string | null;
  type?: AccountType;
  roles?: string[];
  isPrimary?: boolean;
  idempotencyKey?: string;
}

interface ListScope {
  legalEntityId?: string;
  realm?: string;
  status?: AccountStatus;
}

const ACCOUNT_PATH = 'accounts';

/** Builds `POST /api/v1/accounts`. Immutable: every modifier returns a new builder. */
export class AccountsCreateBuilder {
  constructor(
    private readonly transport: Transport,
    private readonly spec: CreateAccountSpec = {},
  ) {}

  forEntity(legalEntityId: string): AccountsCreateBuilder {
    return new AccountsCreateBuilder(this.transport, { ...this.spec, legalEntityId });
  }

  inRealm(realm: string): AccountsCreateBuilder {
    return new AccountsCreateBuilder(this.transport, { ...this.spec, realm });
  }

  withUsername(username: string): AccountsCreateBuilder {
    return new AccountsCreateBuilder(this.transport, { ...this.spec, username });
  }

  withEmail(email: string | null): AccountsCreateBuilder {
    return new AccountsCreateBuilder(this.transport, { ...this.spec, email });
  }

  withPhoneNumber(phoneNumber: string | null): AccountsCreateBuilder {
    return new AccountsCreateBuilder(this.transport, { ...this.spec, phoneNumber });
  }

  withDisplayName(displayName: string | null): AccountsCreateBuilder {
    return new AccountsCreateBuilder(this.transport, { ...this.spec, displayName });
  }

  withType(type: AccountType): AccountsCreateBuilder {
    return new AccountsCreateBuilder(this.transport, { ...this.spec, type });
  }

  withRoles(...roles: string[]): AccountsCreateBuilder {
    return new AccountsCreateBuilder(this.transport, { ...this.spec, roles: [...roles] });
  }

  withPrimary(isPrimary = true): AccountsCreateBuilder {
    return new AccountsCreateBuilder(this.transport, { ...this.spec, isPrimary });
  }

  withIdempotencyKey(idempotencyKey: string): AccountsCreateBuilder {
    return new AccountsCreateBuilder(this.transport, { ...this.spec, idempotencyKey });
  }

  send(): Promise<Account> {
    const spec = this.spec;
    if (!spec.username || spec.username.trim().length === 0) {
      throw new Error('withUsername() is required before send().');
    }

    const type = spec.type ?? 'personal';
    if (type === 'service' && spec.legalEntityId !== undefined) {
      throw new Error('Service accounts cannot belong to a legal entity.');
    }
    if (type !== 'service' && spec.legalEntityId === undefined) {
      throw new Error('Non-service accounts must call forEntity(...) before send().');
    }

    const body: Record<string, unknown> = { type, username: spec.username };
    if (spec.legalEntityId !== undefined) body.legalEntityId = spec.legalEntityId;
    if (spec.realm !== undefined) body.realm = spec.realm;
    if (spec.email !== undefined) body.email = spec.email;
    if (spec.phoneNumber !== undefined) body.phoneNumber = spec.phoneNumber;
    if (spec.displayName !== undefined) body.displayName = spec.displayName;
    if (spec.roles !== undefined && spec.roles.length > 0) body.roles = spec.roles;
    if (spec.isPrimary !== undefined) body.isPrimary = spec.isPrimary;

    const request: RequestSpec = {
      method: 'POST',
      path: ACCOUNT_PATH,
      body,
      idempotencyKey: spec.idempotencyKey ?? crypto.randomUUID(),
    };
    return this.transport.request<Account>(request);
  }
}

/** Builds `GET /api/v1/accounts/{id}`. */
export class AccountsGetBuilder {
  constructor(
    private readonly transport: Transport,
    private readonly id: string,
  ) {}

  send(): Promise<Account> {
    return this.transport.request<Account>({ method: 'GET', path: `${ACCOUNT_PATH}/${this.id}` });
  }
}

/** Builds `GET /api/v1/accounts` with cursor pagination. */
export class AccountsListBuilder implements AsyncIterable<Account> {
  constructor(
    private readonly transport: Transport,
    private readonly query: ListQuery = ListQuery.empty,
    private readonly scope: ListScope = {},
  ) {}

  forEntity(legalEntityId: string): AccountsListBuilder {
    return new AccountsListBuilder(this.transport, this.query, { ...this.scope, legalEntityId });
  }

  inRealm(realm: string): AccountsListBuilder {
    return new AccountsListBuilder(this.transport, this.query, { ...this.scope, realm });
  }

  withStatus(status: AccountStatus): AccountsListBuilder {
    return new AccountsListBuilder(this.transport, this.query, { ...this.scope, status });
  }

  filter(field: string, value: string): AccountsListBuilder {
    return new AccountsListBuilder(this.transport, this.query.filter(field, value), this.scope);
  }

  sort(expression: string): AccountsListBuilder {
    return new AccountsListBuilder(this.transport, this.query.withSort(expression), this.scope);
  }

  limit(pageSize: number): AccountsListBuilder {
    return new AccountsListBuilder(this.transport, this.query.withLimit(pageSize), this.scope);
  }

  cursor(cursor: string): AccountsListBuilder {
    return new AccountsListBuilder(this.transport, this.query.withCursor(cursor), this.scope);
  }

  async send(): Promise<Page<Account>> {
    const query = [...this.query.params()];
    if (this.scope.legalEntityId !== undefined) {
      query.unshift(['legalEntityId', this.scope.legalEntityId]);
    }
    if (this.scope.realm !== undefined) {
      query.push(['realm', this.scope.realm]);
    }
    if (this.scope.status !== undefined) {
      query.push(['filter[status]', this.scope.status]);
    }

    return this.transport.requestPage<Account>({ method: 'GET', path: ACCOUNT_PATH, query });
  }

  /** Iterates every account, following `nextCursor` until the end. */
  async *stream(): AsyncGenerator<Account, void, undefined> {
    let cursor = this.query.cursor;
    const seen = new Set<string>();

    for (;;) {
      const query = cursor === undefined ? this.query : this.query.withCursor(cursor);
      const page = await new AccountsListBuilder(this.transport, query, this.scope).send();
      yield* page.items;

      const next = page.nextCursor;
      if (next === null || next.length === 0 || seen.has(next)) {
        return;
      }
      seen.add(next);
      cursor = next;
    }
  }

  [Symbol.asyncIterator](): AsyncIterator<Account> {
    return this.stream();
  }
}

/** Builds `PATCH /api/v1/accounts/{id}`. Only fields set with `with*` are sent. */
export class AccountsUpdateBuilder {
  constructor(
    private readonly transport: Transport,
    private readonly id: string,
    private readonly patch: Readonly<Record<string, unknown>> = {},
    private readonly ifMatchTag: string | undefined = undefined,
  ) {}

  withUsername(username: string): AccountsUpdateBuilder {
    return new AccountsUpdateBuilder(this.transport, this.id, { ...this.patch, username }, this.ifMatchTag);
  }

  withEmail(email: string | null): AccountsUpdateBuilder {
    return new AccountsUpdateBuilder(this.transport, this.id, { ...this.patch, email }, this.ifMatchTag);
  }

  withPhoneNumber(phoneNumber: string | null): AccountsUpdateBuilder {
    return new AccountsUpdateBuilder(this.transport, this.id, { ...this.patch, phoneNumber }, this.ifMatchTag);
  }

  withDisplayName(displayName: string | null): AccountsUpdateBuilder {
    return new AccountsUpdateBuilder(this.transport, this.id, { ...this.patch, displayName }, this.ifMatchTag);
  }

  withStatus(status: AccountStatus): AccountsUpdateBuilder {
    return new AccountsUpdateBuilder(this.transport, this.id, { ...this.patch, status }, this.ifMatchTag);
  }

  withRoles(...roles: string[]): AccountsUpdateBuilder {
    return new AccountsUpdateBuilder(this.transport, this.id, { ...this.patch, roles: [...roles] }, this.ifMatchTag);
  }

  ifMatch(etag: string): AccountsUpdateBuilder {
    return new AccountsUpdateBuilder(this.transport, this.id, this.patch, etag);
  }

  send(): Promise<Account> {
    if (Object.keys(this.patch).length === 0) {
      throw new Error('Set at least one field with with...() before send().');
    }
    return this.transport.request<Account>({
      method: 'PATCH',
      path: `${ACCOUNT_PATH}/${this.id}`,
      body: this.patch,
      ifMatch: this.ifMatchTag,
    });
  }
}

/** Builds `DELETE /api/v1/accounts/{id}`. */
export class AccountsDeleteBuilder {
  constructor(
    private readonly transport: Transport,
    private readonly id: string,
    private readonly ifMatchTag: string | undefined = undefined,
  ) {}

  ifMatch(etag: string): AccountsDeleteBuilder {
    return new AccountsDeleteBuilder(this.transport, this.id, etag);
  }

  send(): Promise<void> {
    return this.transport.requestVoid({
      method: 'DELETE',
      path: `${ACCOUNT_PATH}/${this.id}`,
      ifMatch: this.ifMatchTag,
    });
  }
}

/** Entry point for every account operation. */
export class AccountsResource {
  constructor(private readonly transport: Transport) {}

  create(): AccountsCreateBuilder {
    return new AccountsCreateBuilder(this.transport);
  }

  get(id: string): AccountsGetBuilder {
    return new AccountsGetBuilder(this.transport, id);
  }

  list(): AccountsListBuilder {
    return new AccountsListBuilder(this.transport);
  }

  update(id: string): AccountsUpdateBuilder {
    return new AccountsUpdateBuilder(this.transport, id);
  }

  delete(id: string): AccountsDeleteBuilder {
    return new AccountsDeleteBuilder(this.transport, id);
  }
}