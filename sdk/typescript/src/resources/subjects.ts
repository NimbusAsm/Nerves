import type { Transport } from '../http.js';
import type { EraseResult, ErasureRecord, SubjectExport } from '../types.js';

/** Builds `GET /api/v1/compliance/subjects/{subjectId}/export`. */
export class SubjectsExportBuilder {
  constructor(
    private readonly transport: Transport,
    private readonly subjectId: string,
  ) {}

  send(): Promise<SubjectExport> {
    return this.transport.request<SubjectExport>({
      method: 'GET',
      path: `compliance/subjects/${encodeURIComponent(this.subjectId)}/export`,
    });
  }
}

/** Builds `POST /api/v1/compliance/subjects/{subjectId}/erase`. */
export class SubjectsEraseBuilder {
  constructor(
    private readonly transport: Transport,
    private readonly subjectId: string,
    private readonly reason: string | undefined = undefined,
    private readonly idempotencyKey: string | undefined = undefined,
  ) {}

  withReason(reason: string): SubjectsEraseBuilder {
    return new SubjectsEraseBuilder(this.transport, this.subjectId, reason, this.idempotencyKey);
  }

  withIdempotencyKey(idempotencyKey: string): SubjectsEraseBuilder {
    return new SubjectsEraseBuilder(this.transport, this.subjectId, this.reason, idempotencyKey);
  }

  send(): Promise<EraseResult> {
    return this.transport.request<EraseResult>({
      method: 'POST',
      path: `compliance/subjects/${encodeURIComponent(this.subjectId)}/erase`,
      body: { reason: this.reason ?? null },
      idempotencyKey: this.idempotencyKey ?? crypto.randomUUID(),
    });
  }
}

/** Builds `GET /api/v1/compliance/subjects/{subjectId}/erasures`. */
export class SubjectsErasuresListBuilder {
  constructor(
    private readonly transport: Transport,
    private readonly subjectId: string,
  ) {}

  send(): Promise<ErasureRecord[]> {
    return this.transport.request<ErasureRecord[]>({
      method: 'GET',
      path: `compliance/subjects/${encodeURIComponent(this.subjectId)}/erasures`,
    });
  }
}

/** Entry point for data-subject rights. */
export class SubjectsResource {
  constructor(private readonly transport: Transport) {}

  export(subjectId: string): SubjectsExportBuilder {
    return new SubjectsExportBuilder(this.transport, subjectId);
  }

  erase(subjectId: string): SubjectsEraseBuilder {
    return new SubjectsEraseBuilder(this.transport, subjectId);
  }

  erasures(subjectId: string): SubjectsErasuresListBuilder {
    return new SubjectsErasuresListBuilder(this.transport, subjectId);
  }
}
