import type { Transport } from '../http.js';
import type { DecisionResult, DecisionStats } from '../types.js';

/** Builds `POST /api/v1/decisions/check`. */
export class DecisionsCheckBuilder {
  constructor(
    private readonly transport: Transport,
    private readonly permission: string | undefined = undefined,
    private readonly accountId: string | undefined = undefined,
    private readonly legalEntityId: string | undefined = undefined,
    private readonly idempotencyKey: string | undefined = undefined,
  ) {}

  withPermission(permission: string): DecisionsCheckBuilder {
    return new DecisionsCheckBuilder(this.transport, permission, this.accountId, this.legalEntityId, this.idempotencyKey);
  }

  forAccount(accountId: string): DecisionsCheckBuilder {
    return new DecisionsCheckBuilder(this.transport, this.permission, accountId, this.legalEntityId, this.idempotencyKey);
  }

  forEntity(legalEntityId: string): DecisionsCheckBuilder {
    return new DecisionsCheckBuilder(this.transport, this.permission, this.accountId, legalEntityId, this.idempotencyKey);
  }

  withIdempotencyKey(idempotencyKey: string): DecisionsCheckBuilder {
    return new DecisionsCheckBuilder(this.transport, this.permission, this.accountId, this.legalEntityId, idempotencyKey);
  }

  send(): Promise<DecisionResult> {
    if (this.permission === undefined || this.permission.trim().length === 0) {
      throw new Error('withPermission() is required before send().');
    }

    return this.transport.request<DecisionResult>({
      method: 'POST',
      path: 'decisions/check',
      body: {
        accountId: this.accountId ?? null,
        legalEntityId: this.legalEntityId ?? null,
        permission: this.permission,
      },
      idempotencyKey: this.idempotencyKey ?? crypto.randomUUID(),
    });
  }
}

/** Builds `GET /api/v1/decisions/stats`. */
export class DecisionsStatsBuilder {
  constructor(private readonly transport: Transport) {}

  send(): Promise<DecisionStats> {
    return this.transport.request<DecisionStats>({ method: 'GET', path: 'decisions/stats' });
  }
}

/** Entry point for authorization decisions. */
export class DecisionsResource {
  constructor(private readonly transport: Transport) {}

  check(permission: string): DecisionsCheckBuilder {
    return new DecisionsCheckBuilder(this.transport, permission);
  }

  stats(): DecisionsStatsBuilder {
    return new DecisionsStatsBuilder(this.transport);
  }
}
