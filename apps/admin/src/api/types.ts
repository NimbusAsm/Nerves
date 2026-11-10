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
export type NotificationStatus = 'pending' | 'sent' | 'failed'

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

export type LegalEntityStatus = 'active' | 'disabled'

export interface LegalEntitySummary {
  id: string
  displayName: string
  entityType: LegalEntityType
  country: string
  /** Document kind only; the digest is never part of the list projection. */
  idDocType: IdDocumentType
  verificationState: VerificationState
  status: LegalEntityStatus
  verifiedAt: string | null
  accountCount: number
  createdAt: string
}

export interface LegalEntityTransition {
  id: string
  status: LegalEntityStatus
  disabledAccounts: number
  revokedSessions: number
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
  /** The client_id; registered applications are addressed by it. */
  id: string
  clientId: string
  name: string
  description: string
  logoUrl: string | null
  redirectUris: string[]
  postLogoutRedirectUris: string[]
  scopes: string[]
  status: ApplicationStatus
  /** Clients provisioned from configuration; they cannot be deleted. */
  builtIn: boolean
  createdAt: string
  updatedAt: string
}

export interface ApplicationInput {
  name: string
  clientId: string
  description: string
  logoUrl: string | null
  redirectUris: string[]
  postLogoutRedirectUris: string[]
  scopes: string[]
  enabled: boolean
}

/** Partial update: omitted fields stay unchanged on the server. */
export interface ApplicationPatch {
  name?: string
  description?: string
  logoUrl?: string | null
  redirectUris?: string[]
  postLogoutRedirectUris?: string[]
  scopes?: string[]
  enabled?: boolean
}

export interface ApplicationQuery extends CursorQuery {
  q?: string
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

/** A named set of accounts that can receive roles. */
export interface Group {
  id: string
  realm: string
  code: string
  name: string
  memberCount?: number
  members?: GroupMember[]
}

/** One group membership. */
export interface GroupMember {
  accountId: string
  addedAt: string
  displayName?: string
}

/** WebAuthn registration material returned by the identity service. */
export interface PasskeyRegistration {
  challengeId: string
  options: PublicKeyCredentialCreationOptionsJSON
}

/** Options subset the browser needs to call navigator.credentials.create(). */
export interface PublicKeyCredentialCreationOptionsJSON {
  challenge: string
  rp: { id: string; name: string }
  user: { id: string; name: string; displayName: string }
  pubKeyCredParams: Array<{ type: string; alg: number }>
  timeout?: number
  attestation?: AttestationConveyancePreference
  authenticatorSelection?: Record<string, unknown>
}

/** Result of a completed passkey registration. */
export interface PasskeyRegistrationResult {
  active: boolean
}

/** Read-only view of the IdP's configured integrations. */
export interface IntegrationStatus {
  ldap: {
    enabled: boolean
    host: string
    port: number
    useSsl: boolean
    baseDn: string
    userFilter: string
    mailAttribute: string
    timeoutSeconds: number
    serviceAccount: boolean
  }
  federation: Array<{
    id: string
    displayName: string
    authority: string
    clientId: string
    redirectUri: string
  }>
}

/** Read-only view of the authorization PDP configuration. */
export interface PdpStatus {
  enabled: boolean
  url: string | null
  timeoutSeconds: number
  failOpen: boolean
  tokenConfigured: boolean
}

/** Partial update of the LDAP integration (runtime, non-persistent). */
export interface LdapSettingsPatch {
  enabled?: boolean
  host?: string
  port?: number
  useSsl?: boolean
  baseDn?: string
  userFilter?: string
  timeoutSeconds?: number
}

/** Partial update of the policy decision point. */
export interface PdpSettingsPatch {
  enabled?: boolean
  url?: string | null
  timeoutSeconds?: number
  failOpen?: boolean
  token?: string | null
}

/** One row of the notification service delivery log. */
export interface NotificationRecord {
  id: string
  /** Normalised recipient address. */
  to: string
  template: string
  subject: string
  status: NotificationStatus
  attempts: number
  error: string | null
  createdAt: string
  sentAt: string | null
  /** When the retry worker may attempt the next delivery. */
  nextAttemptAt: string
}

export interface NotificationQuery extends CursorQuery {
  status?: NotificationStatus | ''
  /** Exact recipient filter, case-insensitive. */
  to?: string
}

export interface SendNotificationEmailInput {
  to: string
  template: string
  values: Record<string, string>
}

