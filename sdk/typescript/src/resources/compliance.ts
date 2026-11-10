import type { Transport } from '../http.js';
import type {
  ComplianceSummary,
  ConsentRecord,
  RetentionPolicy,
} from '../types.js';

/** Builds `GET /api/v1/compliance/consents`; the endpoint returns a plain array. */
export class ComplianceConsentsListBuilder {
  constructor(
    private readonly transport: Transport,
    private readonly subjectId: string | undefined = undefined,
    private readonly purpose: string | undefined = undefined,
  ) {}

  forSubject(subjectId: string): ComplianceConsentsListBuilder {
    return new ComplianceConsentsListBuilder(this.transport, subjectId, this.purpose);
  }

  withPurpose(purpose: string): ComplianceConsentsListBuilder {
    return new ComplianceConsentsListBuilder(this.transport, this.subjectId, purpose);
  }

  send(): Promise<ConsentRecord[]> {
    return this.transport.request<ConsentRecord[]>({
      method: 'GET',
      path: 'compliance/consents',
      query: [
        ['subjectId', this.subjectId],
        ['purpose', this.purpose],
      ],
    });
  }
}

/** Builds `POST /api/v1/compliance/consents`. */
export class ComplianceConsentGrantBuilder {
  constructor(
    private readonly transport: Transport,
    private readonly subjectId: string | undefined = undefined,
    private readonly purpose: string | undefined = undefined,
    private readonly version: string | undefined = undefined,
    private readonly expiresAt: string | undefined = undefined,
    private readonly idempotencyKey: string | undefined = undefined,
  ) {}

  forSubject(subjectId: string): ComplianceConsentGrantBuilder {
    return this.with({ subjectId });
  }

  withPurpose(purpose: string): ComplianceConsentGrantBuilder {
    return this.with({ purpose });
  }

  withVersion(version: string): ComplianceConsentGrantBuilder {
    return this.with({ version });
  }

  until(expiresAt: Date | string): ComplianceConsentGrantBuilder {
    return this.with({ expiresAt: typeof expiresAt === 'string' ? expiresAt : expiresAt.toISOString() });
  }

  withIdempotencyKey(idempotencyKey: string): ComplianceConsentGrantBuilder {
    return this.with({ idempotencyKey });
  }

  send(): Promise<ConsentRecord> {
    if (this.subjectId === undefined || this.subjectId.trim().length === 0) {
      throw new Error('forSubject() is required before send().');
    }
    if (this.purpose === undefined || this.purpose.trim().length === 0) {
      throw new Error('withPurpose() is required before send().');
    }
    if (this.version === undefined || this.version.trim().length === 0) {
      throw new Error('withVersion() is required before send().');
    }

    return this.transport.request<ConsentRecord>({
      method: 'POST',
      path: 'compliance/consents',
      body: {
        subjectId: this.subjectId,
        purpose: this.purpose,
        version: this.version,
        expiresAt: this.expiresAt ?? null,
      },
      idempotencyKey: this.idempotencyKey ?? crypto.randomUUID(),
    });
  }

  private with(patch: {
    subjectId?: string;
    purpose?: string;
    version?: string;
    expiresAt?: string;
    idempotencyKey?: string;
  }): ComplianceConsentGrantBuilder {
    return new ComplianceConsentGrantBuilder(
      this.transport,
      patch.subjectId ?? this.subjectId,
      patch.purpose ?? this.purpose,
      patch.version ?? this.version,
      patch.expiresAt ?? this.expiresAt,
      patch.idempotencyKey ?? this.idempotencyKey,
    );
  }
}

/** Builds `POST /api/v1/compliance/consents/{id}/revoke`. */
export class ComplianceConsentRevokeBuilder {
  constructor(
    private readonly transport: Transport,
    private readonly id: string,
    private readonly reason: string | null | undefined = undefined,
  ) {}

  withReason(reason: string | null): ComplianceConsentRevokeBuilder {
    return new ComplianceConsentRevokeBuilder(this.transport, this.id, reason);
  }

  send(): Promise<ConsentRecord> {
    return this.transport.request<ConsentRecord>({
      method: 'POST',
      path: `compliance/consents/${this.id}/revoke`,
      body: { reason: this.reason ?? null },
    });
  }
}

/** Builds `GET /api/v1/compliance/retention-policies`. */
export class ComplianceRetentionListBuilder {
  constructor(private readonly transport: Transport) {}

  send(): Promise<RetentionPolicy[]> {
    return this.transport.request<RetentionPolicy[]>({
      method: 'GET',
      path: 'compliance/retention-policies',
    });
  }
}

/** Builds `PUT /api/v1/compliance/retention-policies/{category}`. */
export class ComplianceRetentionUpsertBuilder {
  constructor(
    private readonly transport: Transport,
    private readonly category: string,
    private readonly retentionDays: number | undefined = undefined,
    private readonly legalBasis: string | null | undefined = undefined,
    private readonly idempotencyKey: string | undefined = undefined,
  ) {}

  withRetention(days: number): ComplianceRetentionUpsertBuilder {
    return new ComplianceRetentionUpsertBuilder(this.transport, this.category, days, this.legalBasis, this.idempotencyKey);
  }

  withLegalBasis(legalBasis: string | null): ComplianceRetentionUpsertBuilder {
    return new ComplianceRetentionUpsertBuilder(this.transport, this.category, this.retentionDays, legalBasis, this.idempotencyKey);
  }

  withIdempotencyKey(idempotencyKey: string): ComplianceRetentionUpsertBuilder {
    return new ComplianceRetentionUpsertBuilder(this.transport, this.category, this.retentionDays, this.legalBasis, idempotencyKey);
  }

  send(): Promise<RetentionPolicy> {
    if (this.retentionDays === undefined) {
      throw new Error('withRetention() is required before send().');
    }

    return this.transport.request<RetentionPolicy>({
      method: 'PUT',
      path: `compliance/retention-policies/${encodeURIComponent(this.category)}`,
      body: { retentionDays: this.retentionDays, legalBasis: this.legalBasis ?? null },
      idempotencyKey: this.idempotencyKey ?? crypto.randomUUID(),
    });
  }
}

/** Builds `GET /api/v1/compliance/summary`. */
export class ComplianceSummaryBuilder {
  constructor(private readonly transport: Transport) {}

  send(): Promise<ComplianceSummary> {
    return this.transport.request<ComplianceSummary>({ method: 'GET', path: 'compliance/summary' });
  }
}

/** Entry point for compliance records. */
export class ComplianceResource {
  constructor(private readonly transport: Transport) {}

  consents(): ComplianceConsentsListBuilder {
    return new ComplianceConsentsListBuilder(this.transport);
  }

  grantConsent(): ComplianceConsentGrantBuilder {
    return new ComplianceConsentGrantBuilder(this.transport);
  }

  revokeConsent(id: string): ComplianceConsentRevokeBuilder {
    return new ComplianceConsentRevokeBuilder(this.transport, id);
  }

  retentionPolicies(): ComplianceRetentionListBuilder {
    return new ComplianceRetentionListBuilder(this.transport);
  }

  upsertRetentionPolicy(category: string): ComplianceRetentionUpsertBuilder {
    return new ComplianceRetentionUpsertBuilder(this.transport, category);
  }

  summary(): ComplianceSummaryBuilder {
    return new ComplianceSummaryBuilder(this.transport);
  }
}
