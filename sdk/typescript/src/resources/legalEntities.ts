import type { RequestSpec, Transport } from '../http.js';
import { ListQuery } from '../pagination.js';
import type { IdDocumentType, LegalEntity, LegalEntityType, Page, VerificationState } from '../types.js';

interface CreateLegalEntitySpec {
  type?: LegalEntityType;
  displayName?: string;
  documentType?: IdDocumentType;
  documentNumber?: string;
  country?: string;
  idempotencyKey?: string;
}

interface ListScope {
  verificationState?: VerificationState;
  type?: LegalEntityType;
}

const ENTITY_PATH = 'legal-entities';

/** Builds `POST /api/v1/legal-entities`. */
export class LegalEntitiesCreateBuilder {
  constructor(
    private readonly transport: Transport,
    private readonly spec: CreateLegalEntitySpec = {},
  ) {}

  withType(type: LegalEntityType): LegalEntitiesCreateBuilder {
    return new LegalEntitiesCreateBuilder(this.transport, { ...this.spec, type });
  }

  withDisplayName(displayName: string): LegalEntitiesCreateBuilder {
    return new LegalEntitiesCreateBuilder(this.transport, { ...this.spec, displayName });
  }

  /**
   * Supplies the identity document. The number is sent once and is never returned;
   * the server persists a keyed hash only.
   */
  withDocument(documentType: IdDocumentType, documentNumber: string, country: string): LegalEntitiesCreateBuilder {
    return new LegalEntitiesCreateBuilder(this.transport, {
      ...this.spec,
      documentType,
      documentNumber,
      country,
    });
  }

  withIdempotencyKey(idempotencyKey: string): LegalEntitiesCreateBuilder {
    return new LegalEntitiesCreateBuilder(this.transport, { ...this.spec, idempotencyKey });
  }

  send(): Promise<LegalEntity> {
    const spec = this.spec;
    if (!spec.displayName || spec.displayName.trim().length === 0) {
      throw new Error('withDisplayName() is required before send().');
    }
    if (!spec.documentType || !spec.documentNumber || !spec.country) {
      throw new Error('withDocument(...) is required before send().');
    }

    const body = {
      type: spec.type ?? 'naturalPerson',
      displayName: spec.displayName,
      realName: {
        documentType: spec.documentType,
        number: spec.documentNumber,
        country: spec.country,
      },
    };

    const request: RequestSpec = {
      method: 'POST',
      path: ENTITY_PATH,
      body,
      idempotencyKey: spec.idempotencyKey ?? crypto.randomUUID(),
    };
    return this.transport.request<LegalEntity>(request);
  }
}

/** Builds `GET /api/v1/legal-entities/{id}`. */
export class LegalEntitiesGetBuilder {
  constructor(
    private readonly transport: Transport,
    private readonly id: string,
  ) {}

  send(): Promise<LegalEntity> {
    return this.transport.request<LegalEntity>({ method: 'GET', path: `${ENTITY_PATH}/${this.id}` });
  }
}

/** Builds `GET /api/v1/legal-entities` with cursor pagination. */
export class LegalEntitiesListBuilder implements AsyncIterable<LegalEntity> {
  constructor(
    private readonly transport: Transport,
    private readonly query: ListQuery = ListQuery.empty,
    private readonly scope: ListScope = {},
  ) {}

  withVerificationState(verificationState: VerificationState): LegalEntitiesListBuilder {
    return new LegalEntitiesListBuilder(this.transport, this.query, { ...this.scope, verificationState });
  }

  withType(type: LegalEntityType): LegalEntitiesListBuilder {
    return new LegalEntitiesListBuilder(this.transport, this.query, { ...this.scope, type });
  }

  filter(field: string, value: string): LegalEntitiesListBuilder {
    return new LegalEntitiesListBuilder(this.transport, this.query.filter(field, value), this.scope);
  }

  sort(expression: string): LegalEntitiesListBuilder {
    return new LegalEntitiesListBuilder(this.transport, this.query.withSort(expression), this.scope);
  }

  limit(pageSize: number): LegalEntitiesListBuilder {
    return new LegalEntitiesListBuilder(this.transport, this.query.withLimit(pageSize), this.scope);
  }

  cursor(cursor: string): LegalEntitiesListBuilder {
    return new LegalEntitiesListBuilder(this.transport, this.query.withCursor(cursor), this.scope);
  }

  send(): Promise<Page<LegalEntity>> {
    const query = [...this.query.params()];
    if (this.scope.verificationState !== undefined) {
      query.push(['filter[verificationState]', this.scope.verificationState]);
    }
    if (this.scope.type !== undefined) {
      query.push(['filter[type]', this.scope.type]);
    }

    return this.transport.requestPage<LegalEntity>({ method: 'GET', path: ENTITY_PATH, query });
  }

  /** Iterates every legal entity, following `nextCursor` until the end. */
  async *stream(): AsyncGenerator<LegalEntity, void, undefined> {
    let cursor = this.query.cursor;
    const seen = new Set<string>();

    for (;;) {
      const query = cursor === undefined ? this.query : this.query.withCursor(cursor);
      const page = await new LegalEntitiesListBuilder(this.transport, query, this.scope).send();
      yield* page.items;

      const next = page.nextCursor;
      if (next === null || next.length === 0 || seen.has(next)) {
        return;
      }
      seen.add(next);
      cursor = next;
    }
  }

  [Symbol.asyncIterator](): AsyncIterator<LegalEntity> {
    return this.stream();
  }
}

/** Builds `PATCH /api/v1/legal-entities/{id}`. */
export class LegalEntitiesUpdateBuilder {
  constructor(
    private readonly transport: Transport,
    private readonly id: string,
    private readonly patch: Readonly<Record<string, unknown>> = {},
    private readonly ifMatchTag: string | undefined = undefined,
  ) {}

  withDisplayName(displayName: string): LegalEntitiesUpdateBuilder {
    return new LegalEntitiesUpdateBuilder(this.transport, this.id, { ...this.patch, displayName }, this.ifMatchTag);
  }

  withVerificationState(verificationState: VerificationState): LegalEntitiesUpdateBuilder {
    return new LegalEntitiesUpdateBuilder(
      this.transport,
      this.id,
      { ...this.patch, verificationState },
      this.ifMatchTag,
    );
  }

  withProviderReference(providerReference: string | null): LegalEntitiesUpdateBuilder {
    return new LegalEntitiesUpdateBuilder(
      this.transport,
      this.id,
      { ...this.patch, providerReference },
      this.ifMatchTag,
    );
  }

  ifMatch(etag: string): LegalEntitiesUpdateBuilder {
    return new LegalEntitiesUpdateBuilder(this.transport, this.id, this.patch, etag);
  }

  send(): Promise<LegalEntity> {
    if (Object.keys(this.patch).length === 0) {
      throw new Error('Set at least one field with with...() before send().');
    }
    return this.transport.request<LegalEntity>({
      method: 'PATCH',
      path: `${ENTITY_PATH}/${this.id}`,
      body: this.patch,
      ifMatch: this.ifMatchTag,
    });
  }
}

/** Builds `DELETE /api/v1/legal-entities/{id}`. */
export class LegalEntitiesDeleteBuilder {
  constructor(
    private readonly transport: Transport,
    private readonly id: string,
    private readonly ifMatchTag: string | undefined = undefined,
  ) {}

  ifMatch(etag: string): LegalEntitiesDeleteBuilder {
    return new LegalEntitiesDeleteBuilder(this.transport, this.id, etag);
  }

  send(): Promise<void> {
    return this.transport.requestVoid({
      method: 'DELETE',
      path: `${ENTITY_PATH}/${this.id}`,
      ifMatch: this.ifMatchTag,
    });
  }
}

/** Entry point for every legal-entity operation. */
export class LegalEntitiesResource {
  constructor(private readonly transport: Transport) {}

  create(): LegalEntitiesCreateBuilder {
    return new LegalEntitiesCreateBuilder(this.transport);
  }

  get(id: string): LegalEntitiesGetBuilder {
    return new LegalEntitiesGetBuilder(this.transport, id);
  }

  list(): LegalEntitiesListBuilder {
    return new LegalEntitiesListBuilder(this.transport);
  }

  update(id: string): LegalEntitiesUpdateBuilder {
    return new LegalEntitiesUpdateBuilder(this.transport, id);
  }

  delete(id: string): LegalEntitiesDeleteBuilder {
    return new LegalEntitiesDeleteBuilder(this.transport, id);
  }
}