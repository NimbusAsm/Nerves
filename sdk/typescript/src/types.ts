/** What a network account is used for. */
export type AccountType = 'personal' | 'service' | 'managed';

/** Account lifecycle. */
export type AccountStatus = 'active' | 'locked' | 'disabled';

/** Kind of legal entity. */
export type LegalEntityType = 'naturalPerson' | 'legalPerson' | 'government' | 'other';

/** Kind of identity document. */
export type IdDocumentType =
  | 'residentIdentityCard'
  | 'passport'
  | 'unifiedSocialCreditCode'
  | 'other';

/** Real-name verification lifecycle. */
export type VerificationState = 'pending' | 'verified' | 'rejected' | 'expired';

/** A login identity returned by `/api/v1/accounts`. */
export interface Account {
  id: string;
  legalEntityId: string | null;
  realm: string;
  username: string;
  email: string | null;
  phoneNumber: string | null;
  displayName: string | null;
  type: AccountType;
  status: AccountStatus;
  isPrimary: boolean;
  roles: string[];
  createdAt: string;
  updatedAt: string;
  disabledAt: string | null;
}

/** The non-secret part of a legal entity's real-name identity. */
export interface RealName {
  documentType: IdDocumentType;
  country: string;
}

/** A verified subject returned by `/api/v1/legal-entities`. */
export interface LegalEntity {
  id: string;
  type: LegalEntityType;
  displayName: string;
  realName: RealName | null;
  verificationState: VerificationState;
  verifiedAt: string | null;
  providerReference: string | null;
  createdAt: string;
  updatedAt: string;
}

/** A role returned by `/api/v1/roles`. */
export interface Role {
  id: string;
  name: string;
  description: string | null;
  permissions: string[];
  createdAt: string;
  updatedAt: string;
}

/** One cursor page. The cursor is opaque; never derive offsets from it. */
export interface Page<T> {
  items: T[];
  nextCursor: string | null;
  hasMore: boolean;
}
/** One active or revoked login session. */
export interface SessionInfo {
  id: string;
  deviceId: string;
  createdAt: string;
  lastSeenAt: string;
  absoluteExpiresAt: string;
  revoked: boolean;
  revokedReason: string | null;
}

/** One permission code. */
export interface PermissionInfo {
  id: string;
  code: string;
  description: string | null;
}

/** A group of accounts. */
export interface Group {
  id: string;
  realm: string;
  code: string;
  name: string;
}

/** A group plus its member count. */
export interface GroupDetail extends Group {
  memberCount: number;
}

/** One group membership. */
export interface GroupMember {
  accountId: string;
  addedAt: string;
}

/** Result of a single authorization decision. */
export interface DecisionResult {
  allowed: boolean;
  reason: string | null;
  source: string;
  authorizationVersion: number;
  permission: string;
}

/** Permission cache statistics. */
export interface DecisionStats {
  entries: number;
  hits: number;
  misses: number;
  hitRate: number;
}

/** One append-only audit record. */
export interface AuditEvent {
  id: string;
  stream: string;
  sequence: number;
  occurredAt: string;
  actorType: string;
  actorId: string | null;
  action: string;
  targetType: string | null;
  targetId: string | null;
  result: string;
  traceId: string | null;
  hash: string;
}

/** Result of verifying one audit stream. */
export interface AuditVerifyResult {
  valid: boolean;
  brokenAt?: string | null;
  reason?: string | null;
  count?: number;
}

/** One consent record. */
export interface ConsentRecord {
  id: string;
  subjectId: string;
  purpose: string;
  version: string;
  grantedAt: string;
  expiresAt: string | null;
  revokedAt: string | null;
  revocationReason: string | null;
}

/** A retention policy for one data category. */
export interface RetentionPolicy {
  id: string;
  category: string;
  retentionDays: number;
  legalBasis: string | null;
  updatedAt: string;
}

/** Aggregate counts for the compliance module. */
export interface ComplianceSummary {
  consents: number;
  active: number;
  revoked: number;
  retentionPolicies: number;
}

/** Result of an erasure request. */
export interface EraseResult {
  id: string;
  subjectId: string;
  status: string;
  consentRevoked: number;
  accountDisabled: boolean;
}

/** One recorded erasure request. */
export interface ErasureRecord {
  id: string;
  status: string;
  reason: string;
  requestedAt: string;
  completedAt: string | null;
}

/** One queued or delivered notification. */
export interface NotificationMessage {
  id: string;
  to: string;
  template: string;
  subject: string;
  status: string;
  attempts: number;
  error: string | null;
  createdAt: string;
  sentAt: string | null;
  nextAttemptAt: string | null;
}

/** One identity domain event. */
export interface IdentityEvent {
  id: string;
  type: string;
  accountId: string | null;
  legalEntityId: string | null;
  payload: string;
  occurredAt: string;
  dispatchedAt: string | null;
}

/** A data-subject export document. */
export interface SubjectExport {
  subjectId: string;
  exportedAt: string;
  account: SubjectExportAccount | null;
  consents: SubjectExportConsent[];
  retentionPolicies: SubjectExportPolicy[];
}

/** Account snapshot inside a subject export. */
export interface SubjectExportAccount {
  accountId: string;
  realm: string;
  username: string;
  displayName: string | null;
  status: string;
}

/** Consent entry inside a subject export. */
export interface SubjectExportConsent {
  purpose: string;
  version: string;
  grantedAt: string;
  expiresAt: string | null;
  revokedAt: string | null;
  revocationReason: string | null;
}

/** Retention policy inside a subject export. */
export interface SubjectExportPolicy {
  category: string;
  retentionDays: number;
  legalBasis: string | null;
}
