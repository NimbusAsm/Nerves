import type { NervesFile, QueryParam, RequestSpec, Transport } from '../http.js';
import type { AuditEvent, AuditVerifyResult, Page } from '../types.js';

const AUDIT_PATH = 'audit-events';

/** Builds `GET /api/v1/audit-events` with cursor pagination. */
export class AuditListBuilder implements AsyncIterable<AuditEvent> {
  constructor(
    private readonly transport: Transport,
    private readonly streamName: string | undefined = undefined,
    private readonly actorId: string | undefined = undefined,
    private readonly action: string | undefined = undefined,
    private readonly result: string | undefined = undefined,
    private readonly from: string | undefined = undefined,
    private readonly to: string | undefined = undefined,
    private readonly pageSize: number | undefined = undefined,
    private readonly cursorToken: string | undefined = undefined,
  ) {}

  inStream(stream: string): AuditListBuilder { return this.with({ stream }); }

  byActor(actorId: string): AuditListBuilder { return this.with({ actorId }); }

  withAction(action: string): AuditListBuilder { return this.with({ action }); }

  withResult(result: string): AuditListBuilder { return this.with({ result }); }

  since(from: Date | string): AuditListBuilder {
    return this.with({ from: typeof from === 'string' ? from : from.toISOString() });
  }

  until(to: Date | string): AuditListBuilder {
    return this.with({ to: typeof to === 'string' ? to : to.toISOString() });
  }

  limit(pageSize: number): AuditListBuilder {
    if (!Number.isInteger(pageSize) || pageSize <= 0) {
      throw new RangeError('page size must be a positive integer');
    }
    return this.with({ pageSize });
  }

  cursor(cursor: string): AuditListBuilder {
    if (cursor.length === 0) {
      throw new RangeError('cursor must not be empty');
    }
    return this.with({ cursorToken: cursor });
  }

  send(): Promise<Page<AuditEvent>> {
    return this.transport.requestPage<AuditEvent>({
      method: 'GET',
      path: AUDIT_PATH,
      query: this.params(),
    });
  }

  async *stream(): AsyncGenerator<AuditEvent, void, undefined> {
    let cursor = this.cursorToken;
    const seen = new Set<string>();

    for (;;) {
      const builder = this.with({ cursorToken: cursor });
      const page = await builder.send();
      yield* page.items;

      const next = page.nextCursor;
      if (next === null || next.length === 0 || seen.has(next)) {
        return;
      }
      seen.add(next);
      cursor = next;
    }
  }

  [Symbol.asyncIterator](): AsyncIterator<AuditEvent> {
    return this.stream();
  }

  private params(): QueryParam[] {
    const params: QueryParam[] = [
      ['stream', this.streamName],
      ['actorId', this.actorId],
      ['action', this.action],
      ['result', this.result],
      ['from', this.from],
      ['to', this.to],
      ['limit', this.pageSize === undefined ? undefined : String(this.pageSize)],
      ['cursor', this.cursorToken],
    ];
    return params;
  }

  private with(patch: {
    stream?: string;
    actorId?: string;
    action?: string;
    result?: string;
    from?: string;
    to?: string;
    pageSize?: number;
    cursorToken?: string;
  }): AuditListBuilder {
    return new AuditListBuilder(
      this.transport,
      patch.stream ?? this.streamName,
      patch.actorId ?? this.actorId,
      patch.action ?? this.action,
      patch.result ?? this.result,
      patch.from ?? this.from,
      patch.to ?? this.to,
      patch.pageSize ?? this.pageSize,
      patch.cursorToken ?? this.cursorToken,
    );
  }
}

/** Builds `GET /api/v1/audit-events/verify`. */
export class AuditVerifyBuilder {
  constructor(
    private readonly transport: Transport,
    private readonly stream: string | undefined = undefined,
  ) {}

  inStream(stream: string): AuditVerifyBuilder {
    return new AuditVerifyBuilder(this.transport, stream);
  }

  send(): Promise<AuditVerifyResult> {
    return this.transport.request<AuditVerifyResult>({
      method: 'GET',
      path: `${AUDIT_PATH}/verify`,
      query: [['stream', this.stream]],
    });
  }
}

/** Builds `GET /api/v1/audit-events/export`. */
export class AuditExportBuilder {
  constructor(
    private readonly transport: Transport,
    private readonly format: string | undefined = undefined,
    private readonly exportLimit: number | undefined = undefined,
  ) {}

  as(format: 'csv' | 'json'): AuditExportBuilder {
    return new AuditExportBuilder(this.transport, format, this.exportLimit);
  }

  limit(rows: number): AuditExportBuilder {
    return new AuditExportBuilder(this.transport, this.format, rows);
  }

  send(): Promise<NervesFile> {
    const spec: RequestSpec = {
      method: 'GET',
      path: `${AUDIT_PATH}/export`,
      query: [
        ['format', this.format],
        ['limit', this.exportLimit === undefined ? undefined : String(this.exportLimit)],
      ],
      accept: 'text/csv, application/json',
    };
    return this.transport.requestFile(spec);
  }
}

/** Entry point for the audit trail. */
export class AuditResource {
  constructor(private readonly transport: Transport) {}

  list(): AuditListBuilder {
    return new AuditListBuilder(this.transport);
  }

  verify(): AuditVerifyBuilder {
    return new AuditVerifyBuilder(this.transport);
  }

  export(): AuditExportBuilder {
    return new AuditExportBuilder(this.transport);
  }
}