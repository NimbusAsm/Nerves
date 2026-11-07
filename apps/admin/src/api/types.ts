// Wire-level types for the Nerves admin API (PLAN v0.3 §9.3).
// Enum values are lower-case slugs; the gateway serialises the domain enums that way.

export type AccountStatus = 'active' | 'locked' | 'disabled'
export type AccountType = 'personal' | 'service' | 'managed'
export type LegalEntityType = 'natural' | 'legal' | 'gov' | 'other'
export type IdDocumentType = 'resident-id' | 'passport' | 'uscc' | 'other'
export type VerificationState = 'pending' | 'verified' | 'rejected' | 'expired'
export type CredentialType = 'password' | 'totp' | 'passkey' | 'recovery-code'
export type AuditResult = 'success' | 'failure' | 'denied'
export type ActorType = 'admin' | 'account' | 'system'
export type OrgRole = 'owner' | 'admin' | 'member' | 'viewer'
export type RoleScope = 'global' | 'app' | 'org'
export type ApplicationStatus = 'active' | 'disabled'

/** Cursor pagination contract. Never expose offsets to callers. */
export interface CursorPage<T> {
  items: T[]
  nextCursor: string | null
}

export interface CursorQuery {
  limit?: number
  cursor?: string | null
}

export interface LoginInput {
  username: string
  password: string
}

export interface NervesUser {
  id: string
  username: string
  displayName: string
  email: string
  roles: string[]
  /** Permission codes such as `identity:account:read`; `*` means everything. */
  permissions: string[]
}

export interface LoginResult {
  token: string
  user: NervesUser
}

export interface AccountSummary {
  id: string
  legalEntityId: string
  legalEntityName: string
  realm: string
  username: string
  displayName: string
  email: string
  phone: string
  type: AccountType
  status: AccountStatus
  isPrimary: boolean
  mfa: boolean
  sessionCount: number
  createdAt: string
  lastLoginAt: string | null
}

export interface AccountDetail extends AccountSummary {
  legalEntity: LegalEntitySummary
  department: string | null
  title: string | null
  disabledReason: string | null
  disabledAt: string | null
  credentials: CredentialStatus[]
  customFields: Record<string, string>
}

export interface CredentialStatus {
  type: CredentialType
  enrolledAt: string
  lastUsedAt: string | null
}

export interface SessionInfo {
  id: string
  accountId: string
  device: string
  amr: string[]
  ipDigest: string
  createdAt: string
  lastSeenAt: string
  absoluteExpiresAt: string
}

export interface LegalEntitySummary {
  id: string
  displayName: string
  entityType: LegalEntityType
  country: string
  /** Document kind only; the digest is never part of the list projection. */
  idDocType: IdDocumentType
  verificationState: VerificationState
  verifiedAt: string | null
  accountCount: number
  createdAt: string
}

export interface LegalEntityDetail extends LegalEntitySummary {
  /** Only a keyed digest of the document number is ever stored (PLAN §8.1). */
  idDocType: IdDocumentType
  idDocDigest: string
  providerRef: string | null
}

export interface AuditEvent {
  id: string
  at: string
  realm: string
  actorType: ActorType
  actorId: string
  actorName: string
  action: string
  targetType: string
  targetId: string
  targetLabel: string
  result: AuditResult
  ipDigest: string
  traceId: string
  detail: string
}

export interface Organization {
  id: string
  code: string
  name: string
  description: string
  memberCount: number
  createdAt: string
}

export interface OrgMember {
  id: string
  organizationId: string
  accountId: string
  accountName: string
  username: string
  orgRole: OrgRole
  joinedAt: string
  lastActiveAt: string | null
}

export interface Role {
  id: string
  code: string
  name: string
  description: string
  scope: RoleScope
  appName: string | null
  builtIn: boolean
  permissions: string[]
  memberCount: number
  updatedAt: string
}

export interface Permission {
  code: string
  appName: string
  description: string
}

export interface Application {
  id: string
  name: string
  description: string
  clientId: string
  logoUrl: string | null
  redirectUris: string[]
  scopes: string[]
  status: ApplicationStatus
  createdAt: string
  lastRotatedAt: string | null
}

export interface ApplicationInput {
  name: string
  clientId: string
  description: string
  logoUrl: string | null
  redirectUris: string[]
  scopes: string[]
}

export interface LoginTrendPoint {
  date: string
  success: number
  failed: number
}

/** `/api/v1/stats` is the placeholder data source for the overview page. */
export interface DashboardStats {
  totalAccounts: number
  activeAccounts: number
  lockedAccounts: number
  disabledAccounts: number
  todayLogins: number
  todayFailedLogins: number
  activeSessions: number
  verifiedEntities: number
  pendingVerifications: number
  totalOrganizations: number
  totalApplications: number
  loginTrend: LoginTrendPoint[]
}

export interface AccountQuery extends CursorQuery {
  q?: string
  status?: AccountStatus | ''
  realm?: string
}

export interface LegalEntityQuery extends CursorQuery {
  q?: string
  verificationState?: VerificationState | ''
}

export interface AuditQuery extends CursorQuery {
  /** Inclusive local dates (`YYYY-MM-DD`). */
  from?: string
  to?: string
  actor?: string
  result?: AuditResult | ''
  action?: string
}
